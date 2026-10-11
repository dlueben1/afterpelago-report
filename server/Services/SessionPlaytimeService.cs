using Afterpelago.Data;
using Afterpelago.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Services;

/// <summary>Measures elapsed session time with at least one gameplay client connected.</summary>
public sealed class SessionPlaytimeService(AfterpelagoDbContext db)
{
    public async Task<long?> GetTotalPlaytimeSecondsAsync(int sessionId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        if (end < start) return null;

        var events = db.SessionLogEvents.AsNoTracking()
            .Where(e => e.SessionId == sessionId && e.TimestampUtc != null && e.TimestampUtc <= end
                && (((e.Category == SessionEventCategory.GameplayConnected || e.Category == SessionEventCategory.GameplayDisconnected)
                        && e.SlotId != null)
                    || e.Category == SessionEventCategory.ServerShutdown || e.Category == SessionEventCategory.HostingStarted))
            .OrderBy(e => e.TimestampUtc).ThenBy(e => e.ImportId).ThenBy(e => e.SourceLineNumber).ThenBy(e => e.Id)
            .Select(e => new OnlineEvent(e.TimestampUtc!.Value, e.Category, e.SlotId));

        var connectedSlots = new HashSet<int>();
        var previous = start;
        var observedJoin = false;
        long onlineTicks = 0;
        await foreach (var logEvent in events.AsAsyncEnumerable().WithCancellation(ct))
        {
            var timestamp = logEvent.Timestamp < start ? start : logEvent.Timestamp;
            if (connectedSlots.Count > 0) onlineTicks += (timestamp - previous).Ticks;
            previous = timestamp;

            switch (logEvent.Category)
            {
                case SessionEventCategory.GameplayConnected:
                    observedJoin = true;
                    connectedSlots.Add(logEvent.SlotId!.Value);
                    break;
                case SessionEventCategory.GameplayDisconnected:
                    connectedSlots.Remove(logEvent.SlotId!.Value);
                    break;
                case SessionEventCategory.ServerShutdown:
                case SessionEventCategory.HostingStarted:
                    connectedSlots.Clear();
                    break;
            }
        }

        // Missing disconnects are bounded by the imported end, never the current clock.
        if (connectedSlots.Count > 0) onlineTicks += (end - previous).Ticks;
        return observedJoin ? onlineTicks / TimeSpan.TicksPerSecond : null;
    }

    private sealed record OnlineEvent(DateTimeOffset Timestamp, SessionEventCategory Category, int? SlotId);
}
