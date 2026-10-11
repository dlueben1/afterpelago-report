namespace Afterpelago.Domain.Sessions;

/// <summary>Records an item sent by one slot to another at a checked location.</summary>
public sealed class Check
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
}
