namespace Afterpelago.Domain.Sessions;

/// <summary>Records a hint about an item and its location.</summary>
public sealed class Hint
{
    public long Id { get; set; }
    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;
    public long LogEventId { get; set; }
    public SessionLogEvent LogEvent { get; set; } = null!;
    public int SenderSlotId { get; set; }
    public Slot SenderSlot { get; set; } = null!;
    public int ReceiverSlotId { get; set; }
    public Slot ReceiverSlot { get; set; } = null!;
    public string ItemName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
}
