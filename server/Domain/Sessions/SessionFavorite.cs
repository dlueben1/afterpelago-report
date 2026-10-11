using Afterpelago.Domain;

namespace Afterpelago.Domain.Sessions;

/// <summary>A session bookmarked by one signed-in account, independently of imported player slots.</summary>
public sealed class SessionFavorite
{
    public required string DiscordUserId { get; set; }
    public AccessRecord User { get; set; } = null!;
    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;
}
