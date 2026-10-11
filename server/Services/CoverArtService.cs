using Afterpelago.Api;
using Afterpelago.Data;
using Afterpelago.Domain;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Services;

public sealed class CoverArtService(AfterpelagoDbContext db, DataPaths paths, GameImageConverter converter,
    GameScrapeGate gate)
{
    public const long MaximumCoverUpload = 10 * 1024 * 1024;

    public async Task<GameResponse> UploadAsync(int gameId, IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > MaximumCoverUpload)
            throw new GameDataException("Choose a nonempty image no larger than 10 MiB.", 413);
        var bytes = await ReadBoundedAsync(file.OpenReadStream(), MaximumCoverUpload, ct);
        return await SaveAsync(gameId, bytes, ct);
    }

    private async Task<GameResponse> SaveAsync(int gameId, byte[] bytes, CancellationToken ct)
    {
        if (!gate.Semaphore.Wait(0)) throw new GameDataException("Another game-data operation is running. Please retry shortly.", 503);
        var temporaryDirectory = "";
        try
        {
            var game = await db.Games.SingleOrDefaultAsync(g => g.Id == gameId, ct) ?? throw new GameDataException("Game not found.", 404);
            var gameDirectory = Path.Combine(paths.GamesDirectory, gameId.ToString(System.Globalization.CultureInfo.InvariantCulture), "cover");
            temporaryDirectory = Path.Combine(gameDirectory, ".tmp-" + Guid.NewGuid().ToString("N"));
            ConvertedGameImage converted;
            try { converted = converter.Convert(bytes, temporaryDirectory, ct); }
            catch (InvalidDataException ex) { throw new GameDataException(ex.Message); }
            Directory.CreateDirectory(gameDirectory);
            var target = Path.Combine(gameDirectory, converted.Hash + ".webp");
            if (!File.Exists(target)) File.Move(Path.Combine(temporaryDirectory, converted.Hash + ".webp"), target);
            game.CoverHash = converted.Hash;
            game.CoverWidth = converted.Width;
            game.CoverHeight = converted.Height;
            await db.SaveChangesAsync(ct);
            return ToResponse(game);
        }
        catch (DbUpdateConcurrencyException) { throw new GameDataException("The game changed while saving its cover. Reload and try again.", 409); }
        finally
        {
            try
            {
                if (temporaryDirectory.Length > 0 && Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
            }
            finally { gate.Semaphore.Release(); }
        }
    }

    public static GameResponse ToResponse(Game game) => new(game.Id, game.Name,
        game.CoverHash is null ? null : $"/api/games/{game.Id}/cover.webp", game.CoverWidth, game.CoverHeight);

    private static async Task<byte[]> ReadBoundedAsync(Stream input, long maximumBytes, CancellationToken ct)
    {
        await using (input)
        using (var output = new MemoryStream())
        {
            var buffer = new byte[81920];
            long length = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                length += read;
                if (length > maximumBytes) throw new GameDataException("The image exceeds 10 MiB.", 413);
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            return output.ToArray();
        }
    }

}