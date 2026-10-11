using System.Text.Json;
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
        api.MapGet("/{id:int}", GetSession).WithName("GetSessionDetails")
            .Produces<SessionDetailsResponse>().Produces(StatusCodes.Status404NotFound);
        api.MapGet("/{id:int}/checks", GetChecks).WithName("GetSessionChecks")
            .Produces<IReadOnlyList<CheckResponse>>().Produces(StatusCodes.Status404NotFound);
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
    private static async Task<IResult> Publish(int id, PublishSessionRequest request, SessionService sessions, CancellationToken ct)
    {
        try
        {
            var result = await sessions.PublishAsync(id, request.SourceUtcOffsetMinutes, ct);
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
    private static async Task<IReadOnlyList<SessionSummaryResponse>> GetSessions(AfterpelagoDbContext db, CancellationToken ct) =>
        await db.Sessions.AsNoTracking().Where(s => s.State == SessionState.Published)
            .OrderByDescending(s => s.StartUtc)
            .Select(s => new SessionSummaryResponse(s.Id, s.Name, s.StartUtc!.Value, s.EndUtc!.Value, s.Slots.Count, s.Checks.Count, s.Slots.Count(x => x.GoalCompleted)))
            .ToListAsync(ct);

    /// <summary>Returns published session details without the full check history.</summary>
    private static async Task<IResult> GetSession(int id, AfterpelagoDbContext db, CancellationToken ct)
    {
        var session = await db.Sessions.AsNoTracking().AsSplitQuery()
            .Include(s => s.Slots).ThenInclude(s => s.Connections).ThenInclude(c => c.LogEvent)
            .Include(s => s.Imports)
            .SingleOrDefaultAsync(s => s.Id == id && s.State == SessionState.Published, ct);
        if (session is null) return TypedResults.NotFound();
        var slots = session.Slots.OrderBy(x => x.TeamNumber).ThenBy(x => x.PlayerName, StringComparer.Ordinal)
            .Select(x => new SessionSlotResponse(x.Id, x.TeamNumber, x.PlayerName, x.GameName, x.GoalCompleted,
                x.Connections.OrderBy(c => c.LogEvent.TimestampUtc).ThenBy(c => c.LogEvent.SourceLineNumber)
                    .Select(c => new ConnectionResponse(c.Id, c.LogEvent.TimestampUtc!.Value, c.Action.ToString(), c.Kind.ToString(), c.ClientVersion,
                        c.TagsJson is null ? [] : JsonSerializer.Deserialize<string[]>(c.TagsJson) ?? [])).ToList())).ToList();
        var imports = session.Imports.OrderBy(i => i.ImportedAt)
            .Select(i => new SessionImportInfoResponse(i.Id, i.ImportedAt, i.Method.ToString(), i.OriginalFileName, i.ByteLength, i.Sha256, i.WarningCount)).ToList();
        return TypedResults.Ok(new SessionDetailsResponse(session.Id, session.Name, session.StartUtc!.Value, session.EndUtc!.Value, slots, imports));
    }

    /// <summary>Returns the complete ordered check history for a published session.</summary>
    private static async Task<IResult> GetChecks(int id, AfterpelagoDbContext db, CancellationToken ct)
    {
        if (!await db.Sessions.AnyAsync(s => s.Id == id && s.State == SessionState.Published, ct)) return TypedResults.NotFound();
        var checks = await db.Checks.AsNoTracking().Where(c => c.SessionId == id)
            .OrderBy(c => c.LogEvent.TimestampUtc).ThenBy(c => c.LogEvent.SourceLineNumber).ThenBy(c => c.LogEventId)
            .Select(c => new CheckResponse(c.Id, c.LogEvent.TimestampUtc!.Value, c.SenderSlot.TeamNumber, c.SenderSlotId, c.SenderSlot.PlayerName,
                c.ReceiverSlotId, c.ReceiverSlot.PlayerName, c.ItemName, c.LocationName, c.LogEvent.SourceLineNumber)).ToListAsync(ct);
        return TypedResults.Ok(checks);
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
