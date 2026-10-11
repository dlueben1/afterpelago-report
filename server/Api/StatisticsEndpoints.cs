using Afterpelago.Auth;
using Afterpelago.Data;
using Afterpelago.Domain.Sessions;
using Afterpelago.Statistics;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Api;

/// <summary>Maps read access to calculated statistics and admin-triggered recalculation.</summary>
public static class StatisticsEndpoints
{
    public static void MapStatisticsEndpoints(this IEndpointRouteBuilder routes)
    {
        var api = routes.MapGroup("/api").WithTags("Statistics").RequireAuthorization();
        api.MapGet("/sessions/{id:int}/statistics", GetSessionStatistics).WithName("GetSessionStatistics")
            .Produces<SessionStatisticsResponse>().Produces(StatusCodes.Status404NotFound);
        api.MapGet("/sessions/{id:int}/slots/{slotId:int}/statistics", GetSlotStatistics).WithName("GetSlotStatistics")
            .Produces<SlotStatisticsResponse>().Produces(StatusCodes.Status404NotFound);
        api.MapPost("/admin/statistics/recalculate", Recalculate).WithName("RecalculateStatistics")
            .RequireAuthorization(AuthSetup.AdminOnlyPolicy)
            .Produces<RecalculationResponse>().ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetSessionStatistics(int id, AfterpelagoDbContext db, CancellationToken ct)
    {
        var session = await db.Sessions.AsNoTracking().Where(s => s.Id == id && s.State == SessionState.Published)
            .Select(s => new { s.Name }).SingleOrDefaultAsync(ct);
        if (session is null) return TypedResults.NotFound();
        var row = await db.SessionStatistics.AsNoTracking().Where(s => s.SessionId == id).SingleOrDefaultAsync(ct);
        var slots = await LoadSlotsAsync(db, id, ct);
        IReadOnlyList<StatResponse> stats = row is null ? [] : await MapAsync(db, slots, StatResult.Deserialize(row.ResultsJson), ct);
        return TypedResults.Ok(new SessionStatisticsResponse(id, session.Name, row?.ComputedAt, stats));
    }

    private static async Task<IResult> GetSlotStatistics(int id, int slotId, AfterpelagoDbContext db, CancellationToken ct)
    {
        var session = await db.Sessions.AsNoTracking().Where(s => s.Id == id && s.State == SessionState.Published)
            .Select(s => new { s.Name }).SingleOrDefaultAsync(ct);
        Dictionary<int, SlotRef> slots = session is null ? new() : await LoadSlotsAsync(db, id, ct);
        if (session is null || !slots.TryGetValue(slotId, out var slot)) return TypedResults.NotFound();
        var row = await db.SlotStatistics.AsNoTracking().Where(s => s.SlotId == slotId).SingleOrDefaultAsync(ct);
        IReadOnlyList<StatResponse> stats = row is null ? [] : await MapAsync(db, slots, StatResult.Deserialize(row.ResultsJson), ct);
        var team = await db.Slots.AsNoTracking().Where(s => s.Id == slotId).Select(s => s.TeamNumber).SingleAsync(ct);
        return TypedResults.Ok(new SlotStatisticsResponse(id, session.Name, slot.Response, team, row?.ComputedAt, stats));
    }

    private static async Task<IResult> Recalculate(int? sessionId, string? userId, StatisticsService statistics, CancellationToken ct)
    {
        if (sessionId is not null)
            return await statistics.RecalculateSessionAsync(sessionId.Value, ct)
                ? TypedResults.Ok(new RecalculationResponse(1, 0)) : TypedResults.Problem(title: "Published session not found.", statusCode: 404);
        if (!string.IsNullOrEmpty(userId))
            return await statistics.RecalculateUserAsync(userId, ct)
                ? TypedResults.Ok(new RecalculationResponse(0, 1)) : TypedResults.Problem(title: "User not found.", statusCode: 404);
        var summary = await statistics.RecalculateAllAsync(ct);
        return TypedResults.Ok(new RecalculationResponse(summary.Sessions, summary.Users));
    }

    private sealed record SlotRef(StatSlotResponse Response, int? GameId);

    private static async Task<Dictionary<int, SlotRef>> LoadSlotsAsync(AfterpelagoDbContext db, int sessionId, CancellationToken ct)
    {
        var rows = await db.Slots.AsNoTracking().Where(s => s.SessionId == sessionId)
            .Select(s => new
            {
                s.Id, s.PlayerName, s.GameName, s.GameId, CoverHash = s.Game == null ? null : s.Game.CoverHash,
                s.Ownership, s.OwnerDiscordUserId, OwnerName = s.OwnerUser == null ? null : s.OwnerUser.DisplayName,
                OwnerAvatar = s.OwnerUser == null ? null : s.OwnerUser.AvatarHash,
            }).ToListAsync(ct);
        return rows.ToDictionary(s => s.Id, s => new SlotRef(new StatSlotResponse(
            s.Id, s.PlayerName, s.GameName, s.CoverHash is null || s.GameId is null ? null : $"/api/games/{s.GameId}/cover.webp",
            s.Ownership.ToString(), s.OwnerDiscordUserId, s.OwnerName, s.OwnerAvatar), s.GameId));
    }

    private static async Task<IReadOnlyList<StatResponse>> MapAsync(
        AfterpelagoDbContext db, IReadOnlyDictionary<int, SlotRef> slots, IReadOnlyList<StatResult> results, CancellationToken ct)
    {
        var wanted = results.Where(r => r.Value is { Item: not null, ItemReceiverSlotId: not null }
                && slots.TryGetValue(r.Value.ItemReceiverSlotId.Value, out var slot) && slot.GameId is not null)
            .Select(r => (GameId: slots[r.Value!.ItemReceiverSlotId!.Value].GameId!.Value, Item: r.Value.Item!)).Distinct().ToList();
        var images = new Dictionary<(int, string), string>();
        if (wanted.Count > 0)
        {
            var gameIds = wanted.Select(w => w.GameId).Distinct().ToList();
            var names = wanted.Select(w => w.Item).Distinct().ToList();
            var rows = await db.GameItems.AsNoTracking().Where(i => gameIds.Contains(i.GameId) && names.Contains(i.Name))
                .Select(i => new { i.GameId, i.Name, Hash = i.Image != null && i.Image.Candidate != null ? i.Image.Candidate.Asset.Hash : null })
                .ToListAsync(ct);
            foreach (var row in rows.Where(r => r.Hash is not null)) images[(row.GameId, row.Name)] = $"/api/games/{row.GameId}/images/{row.Hash}.webp";
        }

        StatSlotResponse? Slot(int? id) => id is { } value && slots.TryGetValue(value, out var slot) ? slot.Response : null;
        return results.Select(r =>
        {
            var v = r.Value;
            string? image = null;
            if (v is { Item: not null, ItemReceiverSlotId: not null } && slots.TryGetValue(v.ItemReceiverSlotId.Value, out var receiver) && receiver.GameId is { } gameId)
                image = images.GetValueOrDefault((gameId, v.Item));
            return new StatResponse(r.Id, r.Available, r.Reason, v?.Number, v?.Unit, Slot(v?.SlotId), Slot(v?.OtherSlotId),
                v?.Timestamp, v?.Item, v?.Location, image, v?.DurationSeconds, v?.Text);
        }).ToList();
    }
}
