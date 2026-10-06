using System.Security.Claims;
using Afterpelago.Domain;

namespace Afterpelago.Auth;

public static class AuthSchemes
{
    /// <summary>The real application session cookie. Only issued for Approved access records.</summary>
    public const string Application = "Afterpelago";

    /// <summary>Short-lived cookie holding the Discord identity between the OAuth callback and our approval check.</summary>
    public const string External = "Afterpelago.External";

    public const string Discord = "Discord";
}

public static class AuthClaims
{
    public const string DiscordId = "afterpelago:discord_id";
    public const string DiscordUsername = "afterpelago:discord_username";
    public const string DiscordGlobalName = "afterpelago:discord_global_name";
    public const string DiscordAvatarHash = "afterpelago:discord_avatar_hash";

    public static ClaimsPrincipal CreateSessionPrincipal(AccessRecord record)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, record.DiscordUserId),
                new Claim(ClaimTypes.Name, record.DisplayName),
                new Claim(ClaimTypes.Role, record.Role),
            ],
            AuthSchemes.Application,
            ClaimTypes.Name,
            ClaimTypes.Role);

        if (!string.IsNullOrEmpty(record.AvatarHash))
        {
            identity.AddClaim(new Claim(DiscordAvatarHash, record.AvatarHash));
        }

        return new ClaimsPrincipal(identity);
    }
}

/// <summary>What sign-in methods this process was configured with (affects UI and the contract's session response).</summary>
public sealed record AuthCapabilities(bool Discord, bool DevLogin);

public sealed class DevLoginOptions
{
    public const string SectionName = "Authentication:DevLogin";

    public bool Enabled { get; set; }
}
