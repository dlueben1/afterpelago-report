using System.Text.Json;
using Afterpelago.Auth;
using Afterpelago.Data;
using Afterpelago.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Afterpelago.Api;

public sealed class GamePackUploadMetadata;
public sealed class GameImageUploadMetadata;
public sealed class GameCoverUploadMetadata;

public static class GameEndpoints
{
    public static void MapGameEndpoints(this IEndpointRouteBuilder routes)
    {
        var games = routes.MapGroup("/api/games").WithTags("Games").RequireAuthorization().AddEndpointFilter<GameDataErrorFilter>();
        games.MapGet("", async (AfterpelagoDbContext db, CancellationToken ct) =>
            await db.Games.AsNoTracking().OrderBy(g => g.Name).Select(g => new GameResponse(g.Id, g.Name,
                g.CoverHash == null ? null : $"/api/games/{g.Id}/cover.webp", g.CoverWidth, g.CoverHeight)).ToListAsync(ct))
            .WithName("GetGames");
        games.MapGet("/{gameId:int}/items", GetItems).WithName("GetGameItems").Produces<IReadOnlyList<GameItemResponse>>().ProducesProblem(404);
        games.MapGet("/{gameId:int}/images/{hash}.webp", GetImage).WithName("GetGameImage").ExcludeFromDescription();
        games.MapGet("/{gameId:int}/cover.webp", GetCover).WithName("GetGameCover").ExcludeFromDescription();

        var admin = routes.MapGroup("/api/admin").WithTags("GameScraper").RequireAuthorization(AuthSetup.AdminOnlyPolicy).AddEndpointFilter<GameDataErrorFilter>();
        admin.MapPost("/game-scrapes", Upload).WithMetadata(new GamePackUploadMetadata()).WithName("UploadGamePack")
            .Produces<GameScrapeResponse>(201).ProducesProblem(400).ProducesProblem(413).ProducesProblem(503);
        admin.MapPost("/game-scrapes/{id:guid}/scrape", async (Guid id, ScrapeGameRequest request, GameScrapeService service, CancellationToken ct) => await service.ScrapeAsync(id, request.GameId, ct))
            .WithName("ScrapeGamePack").Produces<GameScrapeResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(408).ProducesProblem(503);
        admin.MapGet("/game-scrapes/{id:guid}", async (Guid id, GameScrapeService service, CancellationToken ct) => await service.GetAsync(id, ct))
            .WithName("GetGameScrape").Produces<GameScrapeResponse>().ProducesProblem(404).ProducesProblem(410);
        admin.MapGet("/game-scrapes/{id:guid}/images/{hash}.webp", GetPreview).WithName("GetGameScrapeImage").ExcludeFromDescription();
        admin.MapPost("/game-scrapes/{id:guid}/commit", Commit).WithName("CommitGameScrape")
            .Produces<GameMappingsResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        admin.MapGet("/games/{gameId:int}/image-mappings", async (int gameId, GameScrapeService service, CancellationToken ct) => await service.MappingsAsync(gameId, ct))
            .WithName("GetGameImageMappings").Produces<GameMappingsResponse>().ProducesProblem(404);
        admin.MapPut("/games/{gameId:int}/image-mappings", async (int gameId, SaveGameMappingsRequest request, GameScrapeService service, CancellationToken ct) => await service.SaveAsync(gameId, request, null, ct))
            .WithName("SaveGameImageMappings").Produces<GameMappingsResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        admin.MapPost("/games/{gameId:int}/items/images", async (int gameId, IFormFile file, GameScrapeService service, CancellationToken ct) =>
                TypedResults.Ok(await service.AddCustomImageAsync(gameId, file, ct)))
            .WithMetadata(new GameImageUploadMetadata()).WithName("UploadCustomGameImage")
            .Produces<CustomGameImageResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(413).ProducesProblem(503);
        admin.MapPost("/games/{gameId:int}/cover", async (int gameId, IFormFile file, CoverArtService service, CancellationToken ct) =>
                TypedResults.Ok(await service.UploadAsync(gameId, file, ct)))
            .WithMetadata(new GameCoverUploadMetadata()).WithName("UploadGameCover")
            .Produces<GameResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(413).ProducesProblem(503);
    }

    private static async Task<IResult> Upload(IFormFile file, GameScrapeService service, CancellationToken ct)
    {
        var result = await service.UploadAsync(file, ct);
        return TypedResults.Created($"/api/admin/game-scrapes/{result.Id}", result);
    }

    private static async Task<IResult> Commit(Guid id, SaveGameMappingsRequest request, AfterpelagoDbContext db, GameScrapeService service, CancellationToken ct)
    {
        var gameId = await db.GameScrapes.Where(d => d.Id == id).Select(d => d.GameId).SingleOrDefaultAsync(ct);
        if (gameId is null) throw new GameDataException("A completed scrape is required before saving.", 404);
        return TypedResults.Ok(await service.SaveAsync(gameId.Value, request, id, ct));
    }

    private static async Task<IResult> GetItems(int gameId, AfterpelagoDbContext db, CancellationToken ct)
    {
        if (!await db.Games.AnyAsync(g => g.Id == gameId, ct)) return TypedResults.NotFound();
        var items = await db.GameItems.AsNoTracking().Where(i => i.GameId == gameId).OrderBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, Hash = i.Image != null && i.Image.Candidate != null ? i.Image.Candidate.Asset.Hash : null }).ToListAsync(ct);
        return TypedResults.Ok(items.Select(i => new GameItemResponse(i.Id, i.Name, i.Hash is null ? null : $"/api/games/{gameId}/images/{i.Hash}.webp")).ToList());
    }

    private static async Task<IResult> GetImage(int gameId, string hash, AfterpelagoDbContext db, GameScrapeService service, HttpContext context, CancellationToken ct)
    {
        if (!ValidHash(hash) || !await db.GameImageAssets.AnyAsync(a => a.GameId == gameId && a.Hash == hash, ct)) return TypedResults.NotFound();
        return FileResult(Path.Combine(service.GameDirectory(gameId), hash + ".webp"), hash, context);
    }

    private static async Task<IResult> GetCover(int gameId, AfterpelagoDbContext db, DataPaths paths, HttpContext context, CancellationToken ct)
    {
        var hash = await db.Games.AsNoTracking().Where(g => g.Id == gameId).Select(g => g.CoverHash).SingleOrDefaultAsync(ct);
        if (hash is null || !ValidHash(hash)) return TypedResults.NotFound();
        var gameDirectory = Path.Combine(paths.GamesDirectory, gameId.ToString(System.Globalization.CultureInfo.InvariantCulture), "cover");
        return FileResult(Path.Combine(gameDirectory, hash + ".webp"), hash, context);
    }

    private static async Task<IResult> GetPreview(Guid id, string hash, AfterpelagoDbContext db, GameScrapeService service, TimeProvider time, HttpContext context, CancellationToken ct)
    {
        if (!ValidHash(hash)) return TypedResults.NotFound();
        var draft = await db.GameScrapes.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, ct);
        if (draft is null || draft.State == "Uploaded" || draft.State != "Committed" && draft.CreatedAt < time.GetUtcNow().AddDays(-7)) return TypedResults.NotFound();
        var extraction = JsonSerializer.Deserialize<PackExtraction>(draft.ResultsJson)!;
        if (!extraction.Candidates.Any(c => c.Hash == hash)) return TypedResults.NotFound();
        var directory = draft.State == "Committed" ? service.GameDirectory(draft.GameId!.Value) : service.DraftDirectory(id);
        var path = Path.Combine(directory, hash + ".webp");
        return FileResult(path, hash, context);
    }

    private static bool ValidHash(string hash) => hash.Length == 64 && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static IResult FileResult(string path, string hash, HttpContext context)
    {
        if (!File.Exists(path)) return TypedResults.NotFound();
        context.Response.Headers.CacheControl = "private, no-cache";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return TypedResults.PhysicalFile(path, "image/webp", entityTag: new EntityTagHeaderValue('"' + hash + '"'));
    }
}

public sealed class GameDataErrorFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (GameDataException ex)
        {
            if (ex.Status == 503) context.HttpContext.Response.Headers.RetryAfter = "3";
            return TypedResults.Problem(title: "Game data could not be processed.", detail: ex.Message, statusCode: ex.Status);
        }
        catch (InvalidDataException) { return TypedResults.Problem(title: "Invalid PopTracker ZIP.", detail: "The archive is corrupt or contains unsupported compressed data.", statusCode: 400); }
        catch (JsonException) { return TypedResults.Problem(title: "Invalid pack manifest.", detail: "The manifest could not be read as JSON.", statusCode: 400); }
    }
}
