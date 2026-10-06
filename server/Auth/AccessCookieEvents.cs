using System.Security.Claims;
using Afterpelago.Data;
using Afterpelago.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Auth;

/// <summary>This is an API-only surface: never redirect to a login page, answer 401/403 instead.</summary>
public sealed class AccessCookieEvents : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    /// <summary>Re-checks the access record on every request so revoking/denying someone ends their session immediately.</summary>
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var discordUserId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        AccessRecord? record = null;

        if (!string.IsNullOrEmpty(discordUserId))
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AfterpelagoDbContext>();
            record = await db.AccessRecords.AsNoTracking()
                .SingleOrDefaultAsync(a => a.DiscordUserId == discordUserId, context.HttpContext.RequestAborted);
        }

        if (record is not { Status: AccessStatus.Approved })
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(AuthSchemes.Application);
            return;
        }

        var current = context.Principal!;
        if (current.FindFirstValue(ClaimTypes.Name) != record.DisplayName
            || current.FindFirstValue(ClaimTypes.Role) != record.Role
            || current.FindFirstValue(AuthClaims.DiscordAvatarHash) != record.AvatarHash)
        {
            context.ReplacePrincipal(AuthClaims.CreateSessionPrincipal(record));
            context.ShouldRenew = true;
        }
    }
}
