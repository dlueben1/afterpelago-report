using Afterpelago.Data;
using Afterpelago.Domain.Sessions;
using Afterpelago.Services;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Statistics;

/// <summary>How a check came to be. Released and collected checks are bulk deliveries after a goal or !release/!collect.</summary>
public enum CheckResolution { Natural, Released, Collected }

public sealed record SlotInfo(
    int Id, int TeamNumber, string PlayerName, string? GameName, int? GameId,
    SlotOwnership Ownership, string? OwnerDiscordUserId,
    DateTimeOffset? GoalCompletedUtc, DateTimeOffset? CollectedUtc, DateTimeOffset? ReleasedUtc,
    IReadOnlyList<PlayEpisode> GameplayEpisodes);

public sealed record CheckInfo(
    long Id, DateTimeOffset Timestamp, int Line, int SenderSlotId, int ReceiverSlotId,
    string Item, string Location, CheckResolution Resolution);

public sealed record HintInfo(
    long Id, DateTimeOffset Timestamp, int Line, int SenderSlotId, int ReceiverSlotId,
    string Item, string Location, string Status);

/// <summary>
/// Read-only, normalized view of one published session. Every statistic and medal rule reads from this
/// instead of querying the database, so results depend only on the imported data.
/// </summary>
public sealed class SessionStatsContext
{
    public required int SessionId { get; init; }
    public required string Name { get; init; }
    public required DateTimeOffset StartUtc { get; init; }
    public required DateTimeOffset EndUtc { get; init; }
    public required int SourceUtcOffsetMinutes { get; init; }
    public required IReadOnlyList<SlotInfo> Slots { get; init; }
    /// <summary>Every check in source order, including bulk-delivered ones.</summary>
    public required IReadOnlyList<CheckInfo> Checks { get; init; }
    public required IReadOnlyList<HintInfo> Hints { get; init; }
    /// <summary>Spans with at least one gameplay client connected.</summary>
    public required IReadOnlyList<PlayEpisode> Episodes { get; init; }

    public IReadOnlyDictionary<int, SlotInfo> SlotById => slotById ??= Slots.ToDictionary(s => s.Id);
    /// <summary>Checks found through gameplay, in source order. Rates, rankings, and streaks use these.</summary>
    public IReadOnlyList<CheckInfo> NaturalChecks => natural ??= Checks.Where(c => c.Resolution == CheckResolution.Natural).ToList();
    /// <summary>First occurrence of each distinct hint; the server repeats hints whenever they are re-requested.</summary>
    public IReadOnlyList<HintInfo> DistinctHints => distinct ??= Hints
        .GroupBy(h => (h.SenderSlotId, h.ReceiverSlotId, h.Item, h.Location)).Select(g => g.First()).ToList();

    private Dictionary<int, SlotInfo>? slotById;
    private List<CheckInfo>? natural;
    private List<HintInfo>? distinct;
}

/// <summary>Builds <see cref="SessionStatsContext"/> from the normalized rows of a published session.</summary>
public sealed class SessionStatsContextBuilder(AfterpelagoDbContext db)
{
    public async Task<SessionStatsContext?> BuildAsync(int sessionId, CancellationToken ct)
    {
        var session = await db.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId && s.State == SessionState.Published)
            .Select(s => new { s.Id, s.Name, s.StartUtc, s.EndUtc, s.SourceUtcOffsetMinutes }).SingleOrDefaultAsync(ct);
        if (session?.StartUtc is not { } start || session.EndUtc is not { } end) return null;

        var slotRows = await db.Slots.AsNoTracking().Where(s => s.SessionId == sessionId).OrderBy(s => s.Id).ToListAsync(ct);
        var events = await db.SessionLogEvents.AsNoTracking().Where(e => e.SessionId == sessionId && e.TimestampUtc != null)
            .OrderBy(e => e.TimestampUtc).ThenBy(e => e.ImportId).ThenBy(e => e.SourceLineNumber).ThenBy(e => e.Id)
            .Select(e => new EventRow(e.Id, e.SourceLineNumber, e.TimestampUtc!.Value, e.Category, e.SlotId)).ToListAsync(ct);
        var checkRows = await db.Checks.AsNoTracking().Where(c => c.SessionId == sessionId)
            .Select(c => new { c.Id, c.LogEventId, c.SenderSlotId, c.ReceiverSlotId, c.ItemName, c.LocationName }).ToListAsync(ct);
        var hintRows = await db.Hints.AsNoTracking().Where(h => h.SessionId == sessionId)
            .Select(h => new { h.Id, h.LogEventId, h.SenderSlotId, h.ReceiverSlotId, h.ItemName, h.LocationName, h.Priority }).ToListAsync(ct);

        var checkByEvent = checkRows.ToDictionary(c => c.LogEventId);
        var hintByEvent = hintRows.ToDictionary(h => h.LogEventId);
        var checks = new List<CheckInfo>(checkRows.Count);
        var hints = new List<HintInfo>(hintRows.Count);
        var first = new Dictionary<(int, SessionEventCategory), DateTimeOffset>();

        // Release and collect notices are followed by an unbroken run of lines that deliver the items; any other line ends the run.
        int? collecting = null, releasing = null;
        foreach (var e in events)
        {
            switch (e.Category)
            {
                case SessionEventCategory.GoalCompleted:
                case SessionEventCategory.ItemsCollected:
                case SessionEventCategory.ItemsReleased:
                    if (e.SlotId is { } owner) first.TryAdd((owner, e.Category), e.Timestamp);
                    if (e.Category == SessionEventCategory.ItemsCollected) { collecting = e.SlotId; releasing = null; }
                    else if (e.Category == SessionEventCategory.ItemsReleased) { releasing = e.SlotId; collecting = null; }
                    break;
                case SessionEventCategory.Check when checkByEvent.TryGetValue(e.Id, out var c):
                    var resolution = collecting is not null && c.ReceiverSlotId == collecting ? CheckResolution.Collected
                        : releasing is not null && c.SenderSlotId == releasing ? CheckResolution.Released
                        : CheckResolution.Natural;
                    if (resolution == CheckResolution.Natural) collecting = releasing = null;
                    checks.Add(new CheckInfo(c.Id, e.Timestamp, e.Line, c.SenderSlotId, c.ReceiverSlotId, c.ItemName, c.LocationName, resolution));
                    break;
                case SessionEventCategory.Hint when hintByEvent.TryGetValue(e.Id, out var h):
                    hints.Add(new HintInfo(h.Id, e.Timestamp, e.Line, h.SenderSlotId, h.ReceiverSlotId, h.ItemName, h.LocationName, h.Priority));
                    collecting = releasing = null;
                    break;
                default:
                    collecting = releasing = null;
                    break;
            }
        }

        var (episodes, perSlot) = BuildEpisodes(events, start, end);
        var slots = slotRows.Select(s => new SlotInfo(
            s.Id, s.TeamNumber, s.PlayerName, s.GameName, s.GameId, s.Ownership, s.OwnerDiscordUserId,
            first.TryGetValue((s.Id, SessionEventCategory.GoalCompleted), out var goal) ? goal : null,
            first.TryGetValue((s.Id, SessionEventCategory.ItemsCollected), out var collected) ? collected : null,
            first.TryGetValue((s.Id, SessionEventCategory.ItemsReleased), out var released) ? released : null,
            perSlot.GetValueOrDefault(s.Id) ?? [])).ToList();

        return new SessionStatsContext
        {
            SessionId = session.Id, Name = session.Name, StartUtc = start, EndUtc = end,
            SourceUtcOffsetMinutes = session.SourceUtcOffsetMinutes ?? 0,
            Slots = slots, Checks = checks, Hints = hints, Episodes = episodes,
        };
    }

    /// <summary>Mirrors <see cref="SessionPlaytimeService"/> for the whole session and also tracks each slot's own connected spans.</summary>
    private static (List<PlayEpisode> All, Dictionary<int, List<PlayEpisode>> PerSlot) BuildEpisodes(
        IReadOnlyList<EventRow> events, DateTimeOffset start, DateTimeOffset end)
    {
        var all = new List<PlayEpisode>();
        var perSlot = new Dictionary<int, List<PlayEpisode>>();
        var connections = new Dictionary<int, int>();
        var slotStart = new Dictionary<int, DateTimeOffset>();
        DateTimeOffset? allStart = null;

        void CloseSlot(int slotId, DateTimeOffset at)
        {
            if (!slotStart.Remove(slotId, out var opened)) return;
            if (!perSlot.TryGetValue(slotId, out var list)) perSlot[slotId] = list = [];
            list.Add(new PlayEpisode(opened, at));
        }

        foreach (var e in events)
        {
            if (e.Timestamp > end) break;
            var at = e.Timestamp < start ? start : e.Timestamp;
            switch (e.Category)
            {
                case SessionEventCategory.GameplayConnected when e.SlotId is { } slot:
                    if (connections.Count == 0) allStart = at;
                    var open = connections.GetValueOrDefault(slot);
                    connections[slot] = open + 1;
                    if (open == 0) slotStart[slot] = at;
                    break;
                case SessionEventCategory.GameplayDisconnected when e.SlotId is { } slot:
                    var current = connections.GetValueOrDefault(slot);
                    if (current <= 1) { connections.Remove(slot); CloseSlot(slot, at); }
                    else connections[slot] = current - 1;
                    break;
                case SessionEventCategory.ServerShutdown:
                case SessionEventCategory.HostingStarted:
                    foreach (var slotId in connections.Keys.ToList()) CloseSlot(slotId, at);
                    connections.Clear();
                    break;
            }
            if (connections.Count == 0 && allStart is { } opened)
            {
                all.Add(new PlayEpisode(opened, at));
                allStart = null;
            }
        }

        // A missing disconnect is bounded by the imported end, never the current clock.
        foreach (var slotId in slotStart.Keys.ToList()) CloseSlot(slotId, end);
        if (allStart is { } tail) all.Add(new PlayEpisode(tail, end));
        return (all, perSlot);
    }

    private sealed record EventRow(long Id, int Line, DateTimeOffset Timestamp, SessionEventCategory Category, int? SlotId);
}
