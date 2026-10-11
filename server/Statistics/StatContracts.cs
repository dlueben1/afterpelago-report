namespace Afterpelago.Statistics;

/// <summary>
/// A statistic about a whole session. Implementations must be pure: read only the context, never the database or the clock,
/// so recalculating a session always yields the same result.
/// </summary>
public interface ISessionStat
{
    string Id { get; }
    /// <summary>Bump when the calculation changes so stored results are recalculated.</summary>
    int Version { get; }
    StatResult Calculate(SessionStatsContext context);
}

/// <summary>A statistic about one slot of a session. Same purity rules as <see cref="ISessionStat"/>.</summary>
public interface ISlotStat
{
    string Id { get; }
    int Version { get; }
    StatResult Calculate(SessionStatsContext context, SlotInfo slot);
}

/// <summary>A lifetime statistic about one account, aggregated across the slots it owns.</summary>
public interface IUserStat
{
    string Id { get; }
    int Version { get; }
    StatResult Calculate(UserStatsContext context);
}

public abstract class SessionStat(string id, int version = 1) : ISessionStat
{
    public string Id { get; } = id;
    public int Version { get; } = version;
    public abstract StatResult Calculate(SessionStatsContext context);
    protected StatResult Ok(StatValue value) => StatResult.Ok(Id, Version, value);
    protected StatResult Missing(string reason) => StatResult.Unavailable(Id, Version, reason);
}

public abstract class SlotStat(string id, int version = 1) : ISlotStat
{
    public string Id { get; } = id;
    public int Version { get; } = version;
    public abstract StatResult Calculate(SessionStatsContext context, SlotInfo slot);
    protected StatResult Ok(StatValue value) => StatResult.Ok(Id, Version, value);
    protected StatResult Missing(string reason) => StatResult.Unavailable(Id, Version, reason);
}

public abstract class UserStat(string id, int version = 1) : IUserStat
{
    public string Id { get; } = id;
    public int Version { get; } = version;
    public abstract StatResult Calculate(UserStatsContext context);
    protected StatResult Ok(StatValue value) => StatResult.Ok(Id, Version, value);
    protected StatResult Missing(string reason) => StatResult.Unavailable(Id, Version, reason);
}

/// <summary>The published-session slots an account owns.</summary>
public sealed record OwnedSlot(int SessionId, int SlotId, string? GameName, int? GameId);

public sealed class UserStatsContext
{
    public required string DiscordUserId { get; init; }
    public required IReadOnlyList<OwnedSlot> Slots { get; init; }
}
