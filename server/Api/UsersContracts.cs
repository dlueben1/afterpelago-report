namespace Afterpelago.Api;

/// <summary>A registered Afterpelago user and their current access approval details.</summary>
public sealed record UserApprovalResponse(
    string DiscordUserId,
    string DisplayName,
    string? Username,
    string? AvatarHash,
    string Status,
    string Role,
    DateTimeOffset RequestedAt,
    DateTimeOffset LastSeenAt);

/// <summary>The access status an admin wants to assign to a registered user.</summary>
public sealed record UpdateUserApprovalRequest(string Status);
