namespace Afterpelago.Domain.Sessions;

/// <summary>Records that one account has finished reviewing a session's slots, so it leaves their Needs Review list.</summary>
public sealed class SessionReview
{
    public required string DiscordUserId { get; set; }
    public AccessRecord User { get; set; } = null!;
    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;
    public DateTimeOffset ReviewedAt { get; set; }

    /// <summary>True when the account declared they did not take part in the session.</summary>
    public bool NotInSession { get; set; }
}
