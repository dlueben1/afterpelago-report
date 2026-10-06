namespace Afterpelago.Api;

public sealed record SessionUser(string DiscordUserId, string DisplayName, string Role, string? AvatarHash);

public sealed record SessionResponse(
    bool IsAuthenticated,
    SessionUser? User,
    bool DiscordConfigured,
    bool DevLoginAvailable);

public sealed record AntiforgeryTokenResponse(string Token);
