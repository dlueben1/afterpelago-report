using System.Security.Claims;
using Afterpelago.Auth;
using Afterpelago.Data;
using Afterpelago.Domain;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Api;

public static class AuthEndpoints
{
    public const string DevUserId = "100000000000000001";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth");

        // ---- JSON contract (generated into the TypeScript client) ----

        group.MapGet("/session", (ClaimsPrincipal user, AuthCapabilities capabilities) =>
        {
            var signedIn = user.Identity?.IsAuthenticated == true;
            var sessionUser = signedIn
                ? new SessionUser(
                    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                    user.FindFirstValue(ClaimTypes.Name) ?? "",
                    user.FindFirstValue(ClaimTypes.Role) ?? "",
                    user.FindFirstValue(AuthClaims.DiscordAvatarHash))
                : null;

            return TypedResults.Ok(new SessionResponse(signedIn, sessionUser, capabilities.Discord, capabilities.DevLogin));
        }).WithName("GetSession");

        group.MapGet("/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
            TypedResults.Ok(new AntiforgeryTokenResponse(antiforgery.GetAndStoreTokens(context).RequestToken!))
        ).WithName("GetAntiforgeryToken");

        group.MapPost("/logout", async Task<NoContent> (HttpContext context) =>
        {
            await context.SignOutAsync(AuthSchemes.Application);
            return TypedResults.NoContent();
        }).WithName("Logout");

        // ---- Browser navigation endpoints (redirects, not JSON): kept out of the generated client ----

        group.MapGet("/login", IResult (string? returnUrl, AuthCapabilities capabilities) =>
        {
            if (!capabilities.Discord)
            {
                return TypedResults.Problem(title: "Discord sign-in is not configured on this server.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var properties = new AuthenticationProperties
            {
                RedirectUri = "/api/auth/callback?returnUrl=" + Uri.EscapeDataString(SafeReturnUrl(returnUrl)),
            };
            return TypedResults.Challenge(properties, [AuthSchemes.Discord]);
        }).ExcludeFromDescription();

        group.MapGet("/callback", async Task<IResult> (HttpContext context, AfterpelagoDbContext db, TimeProvider time, string? returnUrl) =>
        {
            var external = await context.AuthenticateAsync(AuthSchemes.External);
            var discordId = external.Principal?.FindFirstValue(AuthClaims.DiscordId);
            if (!external.Succeeded || string.IsNullOrEmpty(discordId))
            {
                return TypedResults.Redirect("/login?signin=failed");
            }

            // The temporary Discord identity is consumed here; it never grants application access by itself.
            await context.SignOutAsync(AuthSchemes.External);

            var username = external.Principal!.FindFirstValue(AuthClaims.DiscordUsername);
            var globalName = external.Principal.FindFirstValue(AuthClaims.DiscordGlobalName);
            var avatarHash = external.Principal.FindFirstValue(AuthClaims.DiscordAvatarHash);
            var record = await UpsertAccessRecordAsync(db, discordId, string.IsNullOrWhiteSpace(globalName) ? username : globalName, username, avatarHash, time.GetUtcNow(), context.RequestAborted);

            switch (record.Status)
            {
                case AccessStatus.Approved:
                    await context.SignInAsync(AuthSchemes.Application, AuthClaims.CreateSessionPrincipal(record));
                    return TypedResults.Redirect(SafeReturnUrl(returnUrl));
                case AccessStatus.Denied:
                    return TypedResults.Redirect("/access-pending?status=denied");
                default:
                    return TypedResults.Redirect("/access-pending?status=pending");
            }
        }).ExcludeFromDescription();
    }

    /// <summary>Development-only explicit test identity (never mapped unless Development and opted in).</summary>
    public static void MapDevLoginEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/auth/dev-login", async Task<IResult> (HttpContext context, AfterpelagoDbContext db, TimeProvider time, string? returnUrl) =>
        {
            // A known non-loopback caller is refused; in-memory test transports report no address at all.
            if (context.Connection.RemoteIpAddress is { } remote && !System.Net.IPAddress.IsLoopback(remote))
            {
                return TypedResults.NotFound();
            }

            var record = await UpsertAccessRecordAsync(db, DevUserId, "Dev User", "dev-user", null, time.GetUtcNow(), context.RequestAborted);
            if (record.Status != AccessStatus.Approved)
            {
                record.Status = AccessStatus.Approved;
                await db.SaveChangesAsync(context.RequestAborted);
            }

            await context.SignInAsync(AuthSchemes.Application, AuthClaims.CreateSessionPrincipal(record));
            return TypedResults.Redirect(SafeReturnUrl(returnUrl));
        }).ExcludeFromDescription();
    }

    /// <summary>Records the sign-in. New accounts start Pending; existing accounts keep whatever status an admin set.</summary>
    internal static async Task<AccessRecord> UpsertAccessRecordAsync(
        AfterpelagoDbContext db, string discordUserId, string? displayName, string? username, string? avatarHash, DateTimeOffset now, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var record = await db.AccessRecords.SingleOrDefaultAsync(a => a.DiscordUserId == discordUserId, ct);
            if (record is null)
            {
                record = new AccessRecord { DiscordUserId = discordUserId, RequestedAt = now };
                db.AccessRecords.Add(record);
            }

            record.DisplayName = string.IsNullOrWhiteSpace(displayName) ? discordUserId : displayName;
            record.Username = username;
            record.AvatarHash = avatarHash;
            record.LastSeenAt = now;

            try
            {
                await db.SaveChangesAsync(ct);
                return record;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // Two first sign-ins raced to insert the same Discord ID; retry as an update.
                db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>Only local, absolute-path redirects are allowed (prevents open redirects).</summary>
    internal static string SafeReturnUrl(string? returnUrl) =>
        returnUrl is { Length: > 0 } && returnUrl[0] == '/' && !returnUrl.StartsWith("//", StringComparison.Ordinal) && !returnUrl.Contains('\\')
            ? returnUrl
            : "/demo";
}
