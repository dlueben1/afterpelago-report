using System.Text.Json;
using System.Security.Claims;
using Afterpelago.Auth;
using Afterpelago.Data;
using Afterpelago.Domain.Sessions;
using Afterpelago.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Api;

/// <summary>Maps session importing and browsing endpoints.</summary>
public static class SessionEndpoints
{
    /// <summary>Maps the session API.</summary>
    public static RouteGroupBuilder MapSessionEndpoints(this IEndpointRouteBuilder routes)
    {
        var api = routes.MapGroup("/api/sessions").WithTags("Sessions").RequireAuthorization();

        api.MapPut("/imports/weblog/{requestId:guid}", ImportWebLog)
            .WithName("ImportWebLog").RequireAuthorization(AuthSetup.AdminOnlyPolicy)
            .Produces<SessionImportResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        api.MapGet("/imports/{requestId:guid}", GetImport)
            .WithName("GetSessionImport").RequireAuthorization(AuthSetup.AdminOnlyPolicy)
            .Produces<SessionImportResponse>().ProducesProblem(StatusCodes.Status404NotFound);
        api.MapPut("/{id:int}/publish", Publish)
            .WithName("PublishSession").RequireAuthorization(AuthSetup.AdminOnlyPolicy)
            .Produces<SessionSummaryResponse>().ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        api.MapDelete("/{id:int}", Delete)
            .WithName("DeleteSession").RequireAuthorization(AuthSetup.AdminOnlyPolicy)
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        api.MapGet("", GetSessions).WithName("GetSessions");
        api.MapGet("/needs-review", GetNeedsReview).WithName("GetSessionsNeedingReview")
            .Produces<IReadOnlyList<NeedsReviewResponse>>();
        api.MapPut("/{id:int}/claims", ClaimSlots).WithName("ClaimSessionSlots")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        api.MapPut("/{id:int}/slots/{slotId:int}/ownership", SetSlotOwnership).WithName("SetSlotOwnership")
            .RequireAuthorization(AuthSetup.AdminOnlyPolicy)
            .Produces<SlotOwnerResponse>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound);
        api.MapPost("/{id:int}/favorite", SetFavorite).WithName("SetSessionFavorite")
            .Produces<SessionFavoriteResponse>().ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
        api.MapGet("/{id:int}", GetSession).WithName("GetSessionDetails")
            .Produces<SessionDetailsResponse>().Produces(StatusCodes.Status404NotFound);
        api.MapGet("/{id:int}/checks", GetChecks).WithName("GetSessionChecks")
            .Produces<IReadOnlyList<CheckResponse>>().Produces(StatusCodes.Status404NotFound);
        api.MapGet("/{id:int}/checks/daily", GetDailyChecks).WithName("GetSessionChecksDaily")
            .Produces<IReadOnlyList<DailyCheckCountResponse>>().Produces(StatusCodes.Status404NotFound);
        api.MapGet("/{id:int}/imports/{importId:int}/file", DownloadImport).WithName("DownloadSessionImport").ExcludeFromDescription();
        return api;
    }

    /// <summary>Imports a WebLog using a replay-safe request identifier.</summary>
    [RequestSizeLimit(27 * 1024 * 1024)]
    private static async Task<IResult> ImportWebLog(Guid requestId, [FromForm] string name, IFormFile file, SessionService sessions, HttpContext context)
    {
        try
        {
            var result = await sessions.ImportWebLogAsync(requestId, name, file, context.RequestAborted);
            return TypedResults.Created($"/api/sessions/imports/{requestId}", result);
        }
        catch (SessionInputException ex) { return TypedResults.Problem(title: "The WebLog could not be imported.", detail: ex.Message, statusCode: 400); }
        catch (SessionConflictException ex) { return TypedResults.Problem(title: "Import request conflict.", detail: ex.Message, statusCode: 409); }
    }

    /// <summary>Loads review data for a draft import.</summary>
    private static async Task<IResult> GetImport(Guid requestId, SessionService sessions, CancellationToken ct)
    {
        var result = await sessions.GetImportAsync(requestId, ct);
        return result is null ? TypedResults.Problem(title: "Draft import not found.", statusCode: 404) : TypedResults.Ok(result);
    }

    /// <summary>Publishes a draft with its selected source UTC offset.</summary>
    private static async Task<IResult> Publish(int id, PublishSessionRequest request, SessionService sessions, ClaimsPrincipal user, CancellationToken ct)
    {
        try
        {
            var result = await sessions.PublishAsync(id, request.SourceUtcOffsetMinutes, user.FindFirstValue(ClaimTypes.NameIdentifier)!, ct);
            return result is null ? TypedResults.Problem(title: "Draft session not found.", statusCode: 404) : TypedResults.Ok(result);
        }
        catch (SessionInputException ex) { return TypedResults.Problem(title: "Invalid source UTC offset.", detail: ex.Message, statusCode: 400); }
        catch (SessionConflictException ex) { return TypedResults.Problem(title: "Session publication conflict.", detail: ex.Message, statusCode: 409); }
    }

    /// <summary>Deletes a session and its stored import files.</summary>
    private static async Task<IResult> Delete(int id, SessionService sessions, CancellationToken ct) =>
        await sessions.DeleteAsync(id, ct)
            ? TypedResults.NoContent()
            : TypedResults.Problem(title: "Session deletion is incomplete.", detail: "The session remains hidden and deletion can be retried.", statusCode: 503);

    /// <summary>Lists published sessions without loading their event histories.</summary>
    private static Task<IReadOnlyList<SessionSummaryResponse>> GetSessions(AfterpelagoDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
        SessionOverview.ListAsync(db, user.FindFirstValue(ClaimTypes.NameIdentifier)!, null, ct);

    /// <summary>Lists published sessions the caller still needs to review.</summary>
    private static async Task<IResult> GetNeedsReview(SlotOwnershipService ownership, ClaimsPrincipal user, CancellationToken ct) =>
        TypedResults.Ok(await ownership.GetNeedsReviewAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, ct));

    /// <summary>Saves the caller's slot claims for a session and marks it reviewed.</summary>
    private static async Task<IResult> ClaimSlots(int id, ClaimSlotsRequest request, SlotOwnershipService ownership, ClaimsPrincipal user, CancellationToken ct)
    {
        try
        {
            return await ownership.ClaimAsync(id, user.FindFirstValue(ClaimTypes.NameIdentifier)!, request, ct)
                ? TypedResults.NoContent() : TypedResults.Problem(title: "Session not found.", statusCode: 404);
        }
        catch (SessionInputException ex) { return TypedResults.Problem(title: "Invalid slot claim.", detail: ex.Message, statusCode: 400); }
        catch (SessionConflictException ex) { return TypedResults.Problem(title: "Slot claim conflict.", detail: ex.Message, statusCode: 409); }
    }

    /// <summary>Lets an admin set a slot's owner, including on draft imports.</summary>
    private static async Task<IResult> SetSlotOwnership(int id, int slotId, SetSlotOwnershipRequest request, SlotOwnershipService ownership, CancellationToken ct)
    {
        try
        {
            var result = await ownership.SetOwnershipAsync(id, slotId, request, ct);
            return result is null ? TypedResults.Problem(title: "Slot not found.", statusCode: 404) : TypedResults.Ok(result);
        }
        catch (SessionInputException ex) { return TypedResults.Problem(title: "Invalid slot owner.", detail: ex.Message, statusCode: 400); }
    }

    /// <summary>Sets the caller's bookmark atomically; repeated requests do not toggle it.</summary>
    private static async Task<IResult> SetFavorite(int id, SetSessionFavoriteRequest request, AfterpelagoDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Sessions.AnyAsync(s => s.Id == id && s.State == SessionState.Published, ct))
            return TypedResults.NotFound();
        if (request.IsFavorite)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SessionFavorites (DiscordUserId, SessionId) VALUES ({userId}, {id}) ON CONFLICT (DiscordUserId, SessionId) DO NOTHING", ct);
        }
        else
        {
            await db.SessionFavorites.Where(f => f.DiscordUserId == userId && f.SessionId == id).ExecuteDeleteAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return TypedResults.Ok(new SessionFavoriteResponse(id, request.IsFavorite));
    }

    /// <summary>Returns published session details without the full check history.</summary>
    private static async Task<IResult> GetSession(int id, AfterpelagoDbContext db, SessionPlaytimeService playtimeService, ClaimsPrincipal user, CancellationToken ct)
    {
        var session = await db.Sessions.AsNoTracking().AsSplitQuery()
            .Include(s => s.Slots).ThenInclude(s => s.Connections).ThenInclude(c => c.LogEvent)
            .Include(s => s.Slots).ThenInclude(s => s.Game)
            .Include(s => s.Slots).ThenInclude(s => s.OwnerUser)
            .Include(s => s.Imports)
            .SingleOrDefaultAsync(s => s.Id == id && s.State == SessionState.Published, ct);
        if (session is null) return TypedResults.NotFound();
        var goalTimes = await db.SessionLogEvents.AsNoTracking()
            .Where(e => e.SessionId == id && e.Category == SessionEventCategory.GoalCompleted && e.SlotId != null && e.TimestampUtc != null)
            .Select(e => new { e.SlotId, Timestamp = e.TimestampUtc!.Value }).ToListAsync(ct);
        var goalBySlot = goalTimes.GroupBy(e => e.SlotId!.Value).ToDictionary(g => g.Key, g => g.Min(e => e.Timestamp));
        var slots = session.Slots.OrderBy(x => x.TeamNumber).ThenBy(x => x.PlayerName, StringComparer.Ordinal)
            .Select(x => new SessionSlotResponse(x.Id, x.TeamNumber, x.PlayerName, x.GameName, x.GoalCompleted,
                x.Connections.OrderBy(c => c.LogEvent.TimestampUtc).ThenBy(c => c.LogEvent.SourceLineNumber)
                    .Select(c => new ConnectionResponse(c.Id, c.LogEvent.TimestampUtc!.Value, c.Action.ToString(), c.Kind.ToString(), c.ClientVersion,
                        c.TagsJson is null ? [] : JsonSerializer.Deserialize<string[]>(c.TagsJson) ?? [])).ToList(),
                x.Game?.CoverHash is null ? null : $"/api/games/{x.Game.Id}/cover.webp",
                goalBySlot.TryGetValue(x.Id, out var goalUtc) ? goalUtc : null,
                x.Ownership.ToString(), x.OwnerDiscordUserId, x.OwnerUser?.DisplayName, x.OwnerUser?.AvatarHash, x.OwnershipSource?.ToString())).ToList();
        var imports = session.Imports.OrderBy(i => i.ImportedAt)
            .Select(i => new SessionImportInfoResponse(i.Id, i.ImportedAt, i.Method.ToString(), i.OriginalFileName, i.ByteLength, i.Sha256, i.WarningCount)).ToList();
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isFavorite = await db.SessionFavorites.AnyAsync(f => f.SessionId == id && f.DiscordUserId == userId, ct);
        var counts = await db.Sessions.AsNoTracking().Where(s => s.Id == id && s.State == SessionState.Published)
            .Select(s => new { CheckCount = s.Checks.Count, HintCount = s.Hints.Count }).SingleOrDefaultAsync(ct);
        if (counts is null) return TypedResults.NotFound();
        var playtime = await playtimeService.GetPlaytimeAsync(id, session.StartUtc!.Value, session.EndUtc!.Value, ct);
        var checkTimes = await db.Checks.AsNoTracking().Where(c => c.SessionId == id)
            .Select(c => c.LogEvent.TimestampUtc!.Value).ToListAsync(ct);
        var episodes = playtime.Episodes
            .Select((e, i) => new EpisodeResponse(i + 1, e.Start, e.End, checkTimes.Count(t => t >= e.Start && t <= e.End))).ToList();
        return TypedResults.Ok(new SessionDetailsResponse(session.Id, session.Name, session.StartUtc!.Value, session.EndUtc!.Value,
            slots, imports, session.Status.ToString(), isFavorite, counts.CheckCount, counts.HintCount, playtime.TotalSeconds,
            session.SourceUtcOffsetMinutes ?? 0, episodes));
    }

    /// <summary>Returns the complete ordered check history for a published session.</summary>
    private static async Task<IResult> GetChecks(int id, AfterpelagoDbContext db, CancellationToken ct)
    {
        if (!await db.Sessions.AnyAsync(s => s.Id == id && s.State == SessionState.Published, ct)) return TypedResults.NotFound();
        var rows = await db.Checks.AsNoTracking().Where(c => c.SessionId == id)
            .OrderBy(c => c.LogEvent.TimestampUtc).ThenBy(c => c.LogEvent.SourceLineNumber).ThenBy(c => c.LogEventId)
            .Select(c => new
            {
                c.Id, TimestampUtc = c.LogEvent.TimestampUtc!.Value, c.SenderSlot.TeamNumber, c.SenderSlotId,
                SenderName = c.SenderSlot.PlayerName, c.ReceiverSlotId, ReceiverName = c.ReceiverSlot.PlayerName,
                c.ItemName, c.LocationName, c.LogEvent.SourceLineNumber, c.ReceiverSlot.GameId,
                ImageHash = c.ReceiverSlot.GameId != null
                    ? db.GameItems.Where(i => i.GameId == c.ReceiverSlot.GameId && i.Name == c.ItemName)
                        .Select(i => i.Image != null && i.Image.Candidate != null ? i.Image.Candidate.Asset.Hash : null)
                        .FirstOrDefault()
                    : null,
            }).ToListAsync(ct);
        var checks = rows.Select(r => new CheckResponse(r.Id, r.TimestampUtc, r.TeamNumber, r.SenderSlotId, r.SenderName,
            r.ReceiverSlotId, r.ReceiverName, r.ItemName, r.LocationName, r.SourceLineNumber,
            r.GameId is null || r.ImageHash is null ? null : $"/api/games/{r.GameId}/images/{r.ImageHash}.webp")).ToList();
        return TypedResults.Ok(checks);
    }

    /// <summary>Returns per-day check totals for a published session, grouped by the session's source-local date.</summary>
    private static async Task<IResult> GetDailyChecks(int id, AfterpelagoDbContext db, CancellationToken ct)
    {
        var offsetMinutes = await db.Sessions.AsNoTracking().Where(s => s.Id == id && s.State == SessionState.Published)
            .Select(s => s.SourceUtcOffsetMinutes).SingleOrDefaultAsync(ct);
        if (offsetMinutes is null) return TypedResults.NotFound();
        var offset = TimeSpan.FromMinutes(offsetMinutes.Value);
        var timestamps = await db.Checks.AsNoTracking().Where(c => c.SessionId == id)
            .Select(c => c.LogEvent.TimestampUtc!.Value).ToListAsync(ct);
        var days = timestamps.GroupBy(t => DateOnly.FromDateTime((t + offset).DateTime)).OrderBy(g => g.Key)
            .Select(g => new DailyCheckCountResponse(g.Key, g.Count())).ToList();
        return TypedResults.Ok(days);
    }

    /// <summary>Downloads an original source file for a published session.</summary>
    private static async Task<IResult> DownloadImport(int id, int importId, AfterpelagoDbContext db, DataPaths paths, CancellationToken ct)
    {
        var import = await db.SessionImports.AsNoTracking().Where(i => i.Id == importId && i.SessionId == id && i.Session.State == SessionState.Published)
            .Select(i => new { i.OriginalFileName }).SingleOrDefaultAsync(ct);
        if (import is null) return TypedResults.NotFound();
        var path = Path.Combine(paths.ImportsDirectory, $"{importId}.txt");
        return File.Exists(path) ? TypedResults.PhysicalFile(path, "text/plain; charset=utf-8", import.OriginalFileName) : TypedResults.NotFound();
    }
}
