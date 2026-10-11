namespace Afterpelago.Domain.Sessions
{
    /// <summary>Who a slot's results belong to.</summary>
    public enum SlotOwnership
    {
        /// <summary>Nobody has claimed it yet.</summary>
        Unclaimed,
        /// <summary>Owned by <see cref="Slot.OwnerDiscordUserId"/>.</summary>
        User,
        /// <summary>Played by everyone, so its stats map to no single player and it never groups with others.</summary>
        Shared,
        /// <summary>Played by someone without an Afterpelago account; resolved as nobody's.</summary>
        NonUser,
    }

    public enum SlotOwnershipSource { Admin, SelfClaim }

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
        public SlotOwnership Ownership { get; set; } = SlotOwnership.Unclaimed;
        public string? OwnerDiscordUserId { get; set; }
        public AccessRecord? OwnerUser { get; set; }
        public SlotOwnershipSource? OwnershipSource { get; set; }
        public DateTimeOffset? OwnershipUpdatedAt { get; set; }
        public ICollection<SlotConnectionEvent> Connections { get; set; } = [];
    }
}
