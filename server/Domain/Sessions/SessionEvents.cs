namespace Afterpelago.Domain.Sessions;

public enum SessionEventCategory
{
    Unknown, Check, Hint, GameplayConnected, GameplayDisconnected, TrackerConnected,
    TrackerDisconnected, GoalCompleted, ItemsReleased, ItemsCollected, TagsChanged,
    TeamCompleted, HostingStarted, ServerShutdown, DataPackageLoaded, SaveLoaded,
    Chat, PlayerNotice, ConnectionRefused,
}
public enum SlotConnectionAction { Connected, Disconnected }
public enum SlotConnectionKind { Gameplay, Tracker }

/// <summary>Preserves one timestamped line from the source log.</summary>
public sealed class SessionLogEvent
{
    public long Id { get; set; }
    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;
    public int ImportId { get; set; }
    public SessionImport Import { get; set; } = null!;
    public int SourceLineNumber { get; set; }
    public DateTime OriginalTimestamp { get; set; }
    public DateTimeOffset? TimestampUtc { get; set; }
    public SessionEventCategory Category { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? TeamNumber { get; set; }
    public int? SlotId { get; set; }
    public Slot? Slot { get; set; }
    public string? DataJson { get; set; }
}

/// <summary>Records a gameplay or tracker connection transition.</summary>
public sealed class SlotConnectionEvent
{
    public long Id { get; set; }
    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;
    public int SlotId { get; set; }
    public Slot Slot { get; set; } = null!;
    public long LogEventId { get; set; }
    public SessionLogEvent LogEvent { get; set; } = null!;
    public SlotConnectionAction Action { get; set; }
    public SlotConnectionKind Kind { get; set; }
    public string? ClientVersion { get; set; }
    public string? TagsJson { get; set; }
}
