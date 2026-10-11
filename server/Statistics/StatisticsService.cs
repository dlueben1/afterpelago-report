using Afterpelago.Data;
using Afterpelago.Domain;
using Afterpelago.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Statistics;

public static class StatisticsRegistration
{
    /// <summary>Registers the calculators. Adding a statistic means writing its class and adding one line here.</summary>
    public static IServiceCollection AddStatistics(this IServiceCollection services)
    {
        services.AddScoped<SessionStatsContextBuilder>();
        services.AddScoped<StatisticsService>();

        services.AddSingleton<ISessionStat, NaturalChecksStat>();
        services.AddSingleton<ISessionStat, AutoResolvedChecksStat>();
        services.AddSingleton<ISessionStat, FirstCheckStat>();
        services.AddSingleton<ISessionStat, LastCheckStat>();
        services.AddSingleton<ISessionStat, FirstClearStat>();
        services.AddSingleton<ISessionStat, LastClearStat>();
        services.AddSingleton<ISessionStat, ChecksPerHourStat>();
        services.AddSingleton<ISessionStat, ChecksPerDayStat>();
        services.AddSingleton<ISessionStat, BusiestDayStat>();
        services.AddSingleton<ISessionStat, MostHintsReceivedStat>();
        services.AddSingleton<ISessionStat, FirstHintStat>();
        services.AddSingleton<ISessionStat, MostHelpfulHintStat>();
        services.AddSingleton<ISessionStat, LeastHelpfulHintStat>();

        services.AddSingleton<ISlotStat, SlotNaturalChecksStat>();
        services.AddSingleton<ISlotStat, SlotAutoResolvedChecksStat>();
        services.AddSingleton<ISlotStat, SlotGameplayHoursStat>();
        services.AddSingleton<ISlotStat, SlotChecksPerHourStat>();
        services.AddSingleton<ISlotStat, SlotNaturalBeforeClearStat>();
        services.AddSingleton<ISlotStat, SlotFirstItemReceivedStat>();
        services.AddSingleton<ISlotStat, SlotItemsReceivedNaturalStat>();
        services.AddSingleton<ISlotStat, SlotItemsReceivedAutoStat>();
        services.AddSingleton<ISlotStat, SlotHintsReceivedStat>();
        services.AddSingleton<ISlotStat, SlotClearedAtStat>();

        services.AddSingleton<IUserStat, UserSessionsPlayedStat>();
        services.AddSingleton<IUserStat, UserSlotsPlayedStat>();
        return services;
    }
}

public sealed record RecalculationSummary(int Sessions, int Users);

/// <summary>
/// Rebuilds derived statistics from normalized session data. Each recalculation replaces its whole scope in one transaction,
/// so running it twice (or after an ownership change) never duplicates anything.
/// </summary>
public sealed class StatisticsService(
    AfterpelagoDbContext db, SessionStatsContextBuilder builder, TimeProvider time, ILogger<StatisticsService> logger,
    IEnumerable<ISessionStat> sessionStats, IEnumerable<ISlotStat> slotStats, IEnumerable<IUserStat> userStats)
{
    /// <summary>Recalculates one session and the lifetime statistics of everyone who owns one of its slots.</summary>
    public async Task<bool> RecalculateSessionAsync(int sessionId, CancellationToken ct) =>
        await RecalculateSessionCoreAsync(sessionId, ct) is { } owners && await RecalculateUsersAsync(owners, ct) >= 0;

    /// <summary>Recalculates lifetime statistics for the given accounts.</summary>
    public async Task<int> RecalculateUsersAsync(IEnumerable<string?> userIds, CancellationToken ct)
    {
        var count = 0;
        foreach (var userId in userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            if (await RecalculateUserAsync(userId!, ct)) count++;
        }
        return count;
    }

    public async Task<bool> RecalculateUserAsync(string userId, CancellationToken ct)
    {
        if (!await db.AccessRecords.AnyAsync(a => a.DiscordUserId == userId, ct)) return false;
        var owned = await db.Slots.AsNoTracking()
            .Where(s => s.OwnerDiscordUserId == userId && s.Ownership == SlotOwnership.User && s.Session.State == SessionState.Published)
            .OrderBy(s => s.SessionId).ThenBy(s => s.Id)
            .Select(s => new OwnedSlot(s.SessionId, s.Id, s.GameName, s.GameId)).ToListAsync(ct);
        var context = new UserStatsContext { DiscordUserId = userId, Slots = owned };
        var results = userStats.Select(s => s.Calculate(context)).ToList();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.UserStatistics.Where(s => s.DiscordUserId == userId).ExecuteDeleteAsync(ct);
        var row = new UserStatistics { DiscordUserId = userId, ResultsJson = StatResult.Serialize(results), EngineVersion = StatisticsEngine.Version, ComputedAt = time.GetUtcNow() };
        db.UserStatistics.Add(row);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.Entry(row).State = EntityState.Detached;
        return true;
    }

    /// <summary>Recalculates every published session and then every account that owns a slot or already has stored results.</summary>
    public async Task<RecalculationSummary> RecalculateAllAsync(CancellationToken ct)
    {
        var sessionIds = await db.Sessions.AsNoTracking().Where(s => s.State == SessionState.Published).OrderBy(s => s.Id).Select(s => s.Id).ToListAsync(ct);
        var sessions = 0;
        foreach (var id in sessionIds) if (await RecalculateSessionCoreAsync(id, ct) is not null) sessions++;
        var users = await AllRelevantUsersAsync(ct);
        return new RecalculationSummary(sessions, await RecalculateUsersAsync(users, ct));
    }

    /// <summary>Calculates results that are missing or were produced by older rules, e.g. for sessions imported before a statistic existed.</summary>
    public async Task BackfillAsync(CancellationToken ct)
    {
        var sessionIds = await db.Sessions.AsNoTracking().Where(s => s.State == SessionState.Published).Select(s => s.Id).ToListAsync(ct);
        var rebuiltSessions = 0;
        foreach (var id in sessionIds)
        {
            if (await IsSessionCurrentAsync(id, ct)) continue;
            await RecalculateSessionCoreAsync(id, ct);
            rebuiltSessions++;
        }
        var current = userStats.Select(s => (s.Id, s.Version)).ToHashSet();
        var stored = await db.UserStatistics.AsNoTracking().Select(s => new { s.DiscordUserId, s.EngineVersion, s.ResultsJson }).ToDictionaryAsync(s => s.DiscordUserId, ct);
        var stale = (await AllRelevantUsersAsync(ct)).Where(id => !stored.TryGetValue(id, out var row) || !IsCurrent(row.EngineVersion, row.ResultsJson, current)).ToList();
        await RecalculateUsersAsync(stale, ct);
        if (rebuiltSessions > 0 || stale.Count > 0)
            logger.LogInformation("Recalculated statistics for {Sessions} sessions and {Users} users.", rebuiltSessions, stale.Count);
    }

    /// <summary>Returns the owners of the session's slots, or null when the session is not published.</summary>
    private async Task<List<string>?> RecalculateSessionCoreAsync(int sessionId, CancellationToken ct)
    {
        var context = await builder.BuildAsync(sessionId, ct);
        if (context is null) return null;
        var sessionResults = sessionStats.Select(s => s.Calculate(context)).ToList();
        var computedAt = time.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SessionStatistics.Where(s => s.SessionId == sessionId).ExecuteDeleteAsync(ct);
        await db.SlotStatistics.Where(s => s.SessionId == sessionId).ExecuteDeleteAsync(ct);
        var added = new List<object>
        {
            new SessionStatistics { SessionId = sessionId, ResultsJson = StatResult.Serialize(sessionResults), EngineVersion = StatisticsEngine.Version, ComputedAt = computedAt },
        };
        foreach (var slot in context.Slots)
        {
            var results = slotStats.Select(s => s.Calculate(context, slot)).ToList();
            added.Add(new SlotStatistics { SlotId = slot.Id, SessionId = sessionId, ResultsJson = StatResult.Serialize(results), EngineVersion = StatisticsEngine.Version, ComputedAt = computedAt });
        }
        db.AddRange(added);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        foreach (var row in added) db.Entry(row).State = EntityState.Detached;
        return context.Slots.Where(s => s.Ownership == SlotOwnership.User && s.OwnerDiscordUserId is not null)
            .Select(s => s.OwnerDiscordUserId!).Distinct(StringComparer.Ordinal).ToList();
    }

    private async Task<bool> IsSessionCurrentAsync(int sessionId, CancellationToken ct)
    {
        var session = await db.SessionStatistics.AsNoTracking().Where(s => s.SessionId == sessionId)
            .Select(s => new { s.EngineVersion, s.ResultsJson }).SingleOrDefaultAsync(ct);
        if (session is null || !IsCurrent(session.EngineVersion, session.ResultsJson, sessionStats.Select(s => (s.Id, s.Version)).ToHashSet())) return false;
        var slotCount = await db.Slots.CountAsync(s => s.SessionId == sessionId, ct);
        var slotRows = await db.SlotStatistics.AsNoTracking().Where(s => s.SessionId == sessionId).Select(s => new { s.EngineVersion, s.ResultsJson }).ToListAsync(ct);
        var expected = slotStats.Select(s => (s.Id, s.Version)).ToHashSet();
        return slotRows.Count == slotCount && slotRows.All(r => IsCurrent(r.EngineVersion, r.ResultsJson, expected));
    }

    private static bool IsCurrent(int engineVersion, string json, HashSet<(string Id, int Version)> expected) =>
        engineVersion == StatisticsEngine.Version && StatResult.Deserialize(json).Select(r => (r.Id, r.Version)).ToHashSet().SetEquals(expected);

    private async Task<List<string>> AllRelevantUsersAsync(CancellationToken ct)
    {
        var owners = await db.Slots.AsNoTracking().Where(s => s.OwnerDiscordUserId != null).Select(s => s.OwnerDiscordUserId!).Distinct().ToListAsync(ct);
        var withRows = await db.UserStatistics.AsNoTracking().Select(s => s.DiscordUserId).ToListAsync(ct);
        return owners.Union(withRows, StringComparer.Ordinal).ToList();
    }
}
