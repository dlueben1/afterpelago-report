namespace Afterpelago.Domain.Sessions
{
    public sealed class Slot
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public Session Session { get; set; } = null!;
        public int TeamNumber { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string? GameName { get; set; }
        public int? GameId { get; set; }
        public Game? Game { get; set; }
        public bool GoalCompleted { get; set; }
        public ICollection<SlotConnectionEvent> Connections { get; set; } = [];
    }
}
