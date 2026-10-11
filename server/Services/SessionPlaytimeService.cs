using Afterpelago.Data;
using Afterpelago.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Services;

/// <summary>A continuous span with at least one gameplay client connected.</summary>
public sealed record PlayEpisode(DateTimeOffset Start, DateTimeOffset End);

/// <summary>Total online seconds (null when no join was observed) and the episodes that make it up.</summary>
public sealed record SessionPlaytime(long? TotalSeconds, IReadOnlyList<PlayEpisode> Episodes);

/// <summary>Measures elapsed session time with at least one gameplay client connected.</summary>
public sealed class SessionPlaytimeService(AfterpelagoDbContext db)
{
    public async Task<SessionPlaytime> GetPlaytimeAsync(int sessionId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        if (end < start) return new SessionPlaytime(null, []);

        var events = db.SessionLogEvents.AsNoTracking()
            .Where(e => e.SessionId == sessionId && e.TimestampUtc != null && e.TimestampUtc <= end
                && (((e.Category == SessionEventCategory.GameplayConnected || e.Category == SessionEventCategory.GameplayDisconnected)
                        && e.SlotId != null)
                    || e.Category == SessionEventCategory.ServerShutdown || e.Category == SessionEventCategory.HostingStarted))
            .OrderBy(e => e.TimestampUtc).ThenBy(e => e.ImportId).ThenBy(e => e.SourceLineNumber).ThenBy(e => e.Id)
            .Select(e => new OnlineEvent(e.TimestampUtc!.Value, e.Category, e.SlotId));

        var connectedSlots = new HashSet<int>();
        var episodes = new List<PlayEpisode>();
        DateTimeOffset? episodeStart = null;
        var observedJoin = false;
        await foreach (var logEvent in events.AsAsyncEnumerable().WithCancellation(ct))
        {
            var timestamp = logEvent.Timestamp < start ? start : logEvent.Timestamp;

            switch (logEvent.Category)
            {
                case SessionEventCategory.GameplayConnected:
                    observedJoin = true;
                    if (connectedSlots.Count == 0) episodeStart = timestamp;
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

            if (connectedSlots.Count == 0 && episodeStart is { } opened)
            {
                episodes.Add(new PlayEpisode(opened, timestamp));
                episodeStart = null;
            }
        }

        // Missing disconnects are bounded by the imported end, never the current clock.
        if (episodeStart is { } open) episodes.Add(new PlayEpisode(open, end));
        var totalTicks = episodes.Sum(e => (e.End - e.Start).Ticks);
        return new SessionPlaytime(observedJoin ? totalTicks / TimeSpan.TicksPerSecond : null, episodes);
    }

    private sealed record OnlineEvent(DateTimeOffset Timestamp, SessionEventCategory Category, int? SlotId);
}
