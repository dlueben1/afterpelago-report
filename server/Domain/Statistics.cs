using Afterpelago.Domain.Sessions;

namespace Afterpelago.Domain;

/// <summary>Computed results for one session. The row is replaced wholesale on every recalculation.</summary>
public sealed class SessionStatistics
{
    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;
    public string ResultsJson { get; set; } = "[]";
    public int EngineVersion { get; set; }
    public DateTimeOffset ComputedAt { get; set; }
}

/// <summary>Computed results for one slot of a session.</summary>
public sealed class SlotStatistics
{
    public int SlotId { get; set; }
    public Slot Slot { get; set; } = null!;
    public int SessionId { get; set; }
    public string ResultsJson { get; set; } = "[]";
    public int EngineVersion { get; set; }
    public DateTimeOffset ComputedAt { get; set; }
}

/// <summary>Computed lifetime results for one account, aggregated over the slots they own.</summary>
public sealed class UserStatistics
{
    public required string DiscordUserId { get; set; }
    public AccessRecord User { get; set; } = null!;
    public string ResultsJson { get; set; } = "[]";
    public int EngineVersion { get; set; }
    public DateTimeOffset ComputedAt { get; set; }
}
