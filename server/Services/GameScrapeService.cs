using System.Security.Cryptography;
using System.Text.Json;
using Afterpelago.Api;
using Afterpelago.Data;
using Afterpelago.Domain;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Services;

public sealed class GameScrapeGate
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
}

public sealed class GameScrapeService(AfterpelagoDbContext db, DataPaths paths, PopTrackerExtractor extractor,
    GameScrapeGate gate, TimeProvider time, GameImageConverter imageConverter, ILogger<GameScrapeService> logger)
{
    public const long MaximumCustomImageUpload = 5 * 1024 * 1024;
    public string DraftDirectory(Guid id) => Path.Combine(paths.GameStagingDirectory, id.ToString("N"));
    public string GameDirectory(int id) => Path.Combine(paths.GamesDirectory, id.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public async Task<CustomGameImageResponse> AddCustomImageAsync(int gameId, IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > MaximumCustomImageUpload)
            throw new GameDataException("Choose a nonempty image no larger than 5 MiB.", 413);
        Enter();
        try
        {
            var game = await db.Games.SingleOrDefaultAsync(g => g.Id == gameId, ct) ?? throw new GameDataException("Game not found.", 404);
            var bytes = await ReadImageAsync(file, ct);
            ConvertedGameImage converted;
            try { converted = imageConverter.Convert(bytes, GameDirectory(gameId), ct); }
            catch (InvalidDataException ex) { throw new GameDataException(ex.Message); }

            var key = "custom:" + converted.Hash;
            var candidate = await db.GameImageCandidates.Include(c => c.Asset)
                .SingleOrDefaultAsync(c => c.GameId == gameId && c.SourceKey == key, ct);
            if (candidate is null)
            {
                var asset = await db.GameImageAssets.SingleOrDefaultAsync(a => a.GameId == gameId && a.Hash == converted.Hash, ct);
                if (asset is null)
                {
                    asset = new GameImageAsset { GameId = gameId, Hash = converted.Hash, Width = converted.Width, Height = converted.Height, ByteLength = converted.ByteLength };
                    db.GameImageAssets.Add(asset);
                }
                candidate = new GameImageCandidate
                {
                    GameId = gameId, Asset = asset, SourceKey = key, Name = "Custom image", ItemName = "Custom image",
                    SourceFile = "Custom upload", Variant = "Custom", AutomaticEligible = false,
                };
                db.GameImageCandidates.Add(candidate);
                game.Revision++;
                await db.SaveChangesAsync(ct);
            }
            return new(game.Revision, GameCatalogService.ToResponse(candidate));
        }
        catch (DbUpdateConcurrencyException) { throw new GameDataException("Game data changed. Reload the review before uploading another image.", 409); }
        finally { gate.Semaphore.Release(); }
    }

    private static async Task<byte[]> ReadImageAsync(IFormFile file, CancellationToken ct)
    {
        await using var input = file.OpenReadStream();
        using var output = new MemoryStream((int)Math.Min(file.Length, MaximumCustomImageUpload));
        var buffer = new byte[81920];
        long length = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            length += read;
            if (length > MaximumCustomImageUpload) throw new GameDataException("The image exceeds 5 MiB.", 413);
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return output.ToArray();
    }

    public async Task<GameScrapeResponse> UploadAsync(IFormFile file, CancellationToken ct)
    {
        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || file.Length is <= 0 or > PopTrackerArchive.MaximumUpload)
            throw new GameDataException("Choose a nonempty .zip file no larger than 50 MiB.");
        Enter();
        var id = Guid.NewGuid();
        var directory = DraftDirectory(id);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            await CleanupCoreAsync(timeout.Token);
            Directory.CreateDirectory(directory);
            var archivePath = Path.Combine(directory, "pack.zip");
            await using (var input = file.OpenReadStream())
            await using (var output = File.Create(archivePath))
            {
                var buffer = new byte[81920];
                long length = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    length += read;
                    if (length > PopTrackerArchive.MaximumUpload) throw new GameDataException("The ZIP exceeds 50 MiB.", 413);
                    await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                }
            }
            using var archive = new PopTrackerArchive(archivePath);
            await archive.ValidateContentsAsync(timeout.Token);
            var manifest = await archive.ManifestAsync(timeout.Token);
            await using var hashInput = File.OpenRead(archivePath);
            var draft = new GameScrape
            {
                Id = id, CreatedAt = time.GetUtcNow(), OriginalFileName = Path.GetFileName(file.FileName),
                Sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(hashInput, timeout.Token)), ManifestJson = JsonSerializer.Serialize(manifest),
            };
            db.GameScrapes.Add(draft);
            await db.SaveChangesAsync(timeout.Token);
            return new(id, draft.State, null, null, manifest, await CompatibleGamesAsync(manifest.GameName, timeout.Token), [], 0, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { TryDelete(directory); throw new GameDataException("ZIP validation exceeded two minutes. Try a smaller pack.", 408); }
        catch { TryDelete(directory); throw; }
        finally { gate.Semaphore.Release(); }
    }

    public async Task<GameScrapeResponse> ScrapeAsync(Guid id, int gameId, CancellationToken ct)
    {
        Enter();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var draft = await DraftAsync(id, timeout.Token);
            if (draft.State == "Committed") throw new GameDataException("This draft was already saved. Upload a pack to start another review.", 409);
            var game = await db.Games.FindAsync([gameId], timeout.Token) ?? throw new GameDataException("Game not found.", 404);
            if (draft.GameId is not null && draft.GameId != gameId) throw new GameDataException("This draft belongs to a different game. Upload the pack again.", 409);
            var manifest = JsonSerializer.Deserialize<PackManifest>(draft.ManifestJson)!;
            if (!PopTrackerGameNames.Match(manifest.GameName, game.Name))
                throw new GameDataException($"This pack is for '{manifest.GameName}', but you selected '{game.Name}'.");
            // A retry after a lost response recovers the same completed review.
            if (draft.State == "Ready") return await ResponseAsync(draft, timeout.Token);
            var zipPath = Path.Combine(DraftDirectory(id), "pack.zip");
            if (!File.Exists(zipPath)) throw new GameDataException("The uploaded ZIP is no longer available. Upload it again.", 410);
            using var archive = new PopTrackerArchive(zipPath);
            var result = await extractor.ExtractAsync(archive, manifest, id, draft.Sha256, DraftDirectory(id), timeout.Token);
            draft.GameId = gameId;
            draft.ExpectedRevision = game.Revision;
            draft.ResultsJson = JsonSerializer.Serialize(result);
            draft.State = "Ready";
            await db.SaveChangesAsync(timeout.Token);
            logger.LogInformation("Scraped pack {DraftId} for game {GameId}: {Candidates} candidates, {Warnings} warnings in {ElapsedMs} ms", id, gameId, result.Candidates.Count, result.WarningCount, watch.ElapsedMilliseconds);
            return await ResponseAsync(draft, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new GameDataException("Scraping exceeded two minutes. Try a smaller pack.", 408); }
        finally { gate.Semaphore.Release(); }
    }

    public async Task<GameScrapeResponse> GetAsync(Guid id, CancellationToken ct) => await ResponseAsync(await DraftAsync(id, ct), ct);

    public async Task<GameMappingsResponse> MappingsAsync(int gameId, CancellationToken ct, IReadOnlyList<GameCandidateResponse>? staged = null)
    {
        var game = await db.Games.AsNoTracking().SingleOrDefaultAsync(g => g.Id == gameId, ct) ?? throw new GameDataException("Game not found.", 404);
        var saved = await db.GameImageCandidates.AsNoTracking().Include(c => c.Asset).Where(c => c.GameId == gameId).ToListAsync(ct);
        var candidates = saved.Select(GameCatalogService.ToResponse).Concat(staged ?? []).DistinctBy(c => c.Key).ToList();
        var items = await db.GameItems.AsNoTracking().Include(i => i.Image).ThenInclude(m => m!.Candidate).ThenInclude(c => c!.Asset).Where(i => i.GameId == gameId).OrderBy(i => i.Name).ToListAsync(ct);
        var rows = items.Select(i =>
        {
            if (i.Image is { } mapping)
                return new ItemMappingResponse(i.Id, i.Name, mapping.Candidate?.SourceKey, mapping.Origin, mapping.Candidate is null ? "Explicitly no image" : "Saved assignment", mapping.Candidate is null ? null : GameCatalogService.ToResponse(mapping.Candidate).ImageUrl);
            var (candidate, evidence) = GameImageMatcher.Match(i.Name, candidates);
            return new ItemMappingResponse(i.Id, i.Name, candidate?.Key, candidate is null ? "Unresolved" : "Suggested", evidence, candidate?.ImageUrl);
        }).ToList();
        return new(game.Id, game.Revision, rows, candidates);
    }

    public async Task<GameMappingsResponse> SaveAsync(int gameId, SaveGameMappingsRequest request, Guid? draftId, CancellationToken ct)
    {
        if (request.Mappings is null || request.Mappings.Count > 20_000) throw new GameDataException("Provide at most 20,000 mapping edits.");
        Enter();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            GameScrape? draft = draftId is { } id ? await DraftAsync(id, ct) : null;
            if (draft is { State: "Committed", CommitResponseJson: not null }) return JsonSerializer.Deserialize<GameMappingsResponse>(draft.CommitResponseJson)!;
            if (draft is not null && (draft.State != "Ready" || draft.GameId != gameId)) throw new GameDataException("The draft is not ready for this game.", 409);
            var game = await db.Games.SingleOrDefaultAsync(g => g.Id == gameId, ct) ?? throw new GameDataException("Game not found.", 404);
            if (game.Revision != request.ExpectedRevision)
                throw new GameDataException("Game data changed while you were reviewing. Reload the review before saving.", 409);
            var candidates = await db.GameImageCandidates.Include(c => c.Asset).Where(c => c.GameId == gameId).ToDictionaryAsync(c => c.SourceKey, StringComparer.Ordinal, ct);
            if (draft is not null)
            {
                var extraction = JsonSerializer.Deserialize<PackExtraction>(draft.ResultsJson)!;
                var assets = await db.GameImageAssets.Where(a => a.GameId == gameId).ToDictionaryAsync(a => a.Hash, ct);
                foreach (var c in extraction.Candidates)
                {
                    ct.ThrowIfCancellationRequested();
                    if (candidates.ContainsKey(c.Key)) continue;
                    if (!assets.TryGetValue(c.Hash, out var asset))
                    {
                        var targetDir = GameDirectory(gameId);
                        Directory.CreateDirectory(targetDir);
                        var target = Path.Combine(targetDir, c.Hash + ".webp");
                        if (!File.Exists(target))
                        {
                            var source = Path.Combine(DraftDirectory(draft.Id), c.Hash + ".webp");
                            if (!File.Exists(source)) throw new GameDataException("A preview image is missing. Upload the pack again.", 409);
                            var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                            try { File.Copy(source, temporary); File.Move(temporary, target); }
                            finally { if (File.Exists(temporary)) File.Delete(temporary); }
                        }
                        asset = new GameImageAsset { GameId = gameId, Hash = c.Hash, Width = c.Width, Height = c.Height, ByteLength = c.ByteLength };
                        db.GameImageAssets.Add(asset);
                        assets[c.Hash] = asset;
                    }
                    var candidate = new GameImageCandidate
                    {
                        GameId = gameId, Asset = asset, ImportId = draft.Id, SourceKey = c.Key, Name = c.Name, ItemName = c.ItemName,
                        CodesJson = JsonSerializer.Serialize(c.Codes), ApIdsJson = JsonSerializer.Serialize(c.ApIds), SourceFile = c.SourceFile,
                        Variant = c.Variant, PackUid = c.PackUid, PackVersion = c.PackVersion, Stage = c.Stage, Modifiers = c.Modifiers, AutomaticEligible = c.AutomaticEligible,
                    };
                    db.GameImageCandidates.Add(candidate);
                    candidates.Add(c.Key, candidate);
                }
            }
            var items = await db.GameItems.Include(i => i.Image).Where(i => i.GameId == gameId).ToDictionaryAsync(i => i.Name, StringComparer.Ordinal, ct);
            var edited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var edit in request.Mappings)
            {
                if (edit is null || string.IsNullOrWhiteSpace(edit.Name) || edit.Name.Length > 500 || edit.Name.Any(char.IsControl)) throw new GameDataException("Item names must contain 1–500 characters without control characters.");
                if (!edited.Add(edit.Name)) throw new GameDataException("Each item may be edited only once per save.");
                if (edit.CandidateKey is not null && !candidates.ContainsKey(edit.CandidateKey)) throw new GameDataException($"The selected candidate for '{edit.Name}' does not belong to this game or draft.");
                if (!items.TryGetValue(edit.Name, out var item))
                {
                    item = new GameItem { GameId = gameId, Name = edit.Name };
                    db.GameItems.Add(item);
                    items.Add(edit.Name, item);
                }
                item.Image ??= new GameItemImage { GameItem = item };
                item.Image.Candidate = edit.CandidateKey is null ? null : candidates[edit.CandidateKey];
                if (edit.CandidateKey is null) item.Image.CandidateId = null;
                item.Image.Origin = "Manual";
                item.Image.UpdatedAt = time.GetUtcNow();
            }
            var responses = candidates.Values.Select(GameCatalogService.ToResponse).ToList();
            foreach (var item in items.Values.Where(i => i.Image is null))
            {
                var (candidate, _) = GameImageMatcher.Match(item.Name, responses);
                if (candidate is not null) item.Image = new GameItemImage { GameItem = item, Candidate = candidates[candidate.Key], Origin = "Automatic", UpdatedAt = time.GetUtcNow() };
            }
            game.Revision++;
            if (draft is not null) draft.State = "Committed";
            await db.SaveChangesAsync(ct);
            var result = await MappingsAsync(gameId, ct);
            if (draft is not null)
            {
                draft.CommitResponseJson = JsonSerializer.Serialize(result);
                await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            if (draft is not null) TryDelete(DraftDirectory(draft.Id));
            logger.LogInformation("Saved game {GameId} image revision {Revision}, draft {DraftId}, {Edits} explicit edits", gameId, game.Revision, draftId, edited.Count);
            return result;
        }
        catch (DbUpdateConcurrencyException) { throw new GameDataException("Game data changed. Reload the review before saving.", 409); }
        finally { gate.Semaphore.Release(); }
    }

    private async Task<GameScrapeResponse> ResponseAsync(GameScrape draft, CancellationToken ct)
    {
        var manifest = JsonSerializer.Deserialize<PackManifest>(draft.ManifestJson)!;
        var compatibleGames = await CompatibleGamesAsync(manifest.GameName, ct);
        if (draft.State == "Uploaded") return new(draft.Id, draft.State, null, null, manifest, compatibleGames, [], 0, null);
        var extraction = JsonSerializer.Deserialize<PackExtraction>(draft.ResultsJson)!;
        var review = await MappingsAsync(draft.GameId!.Value, ct, draft.State == "Ready" ? extraction.Candidates : null);
        // Reloading rebuilds proposals against current game data, without mutating the draft on GET.
        return new(draft.Id, draft.State, draft.GameId, review.Revision, manifest, compatibleGames, extraction.Warnings, extraction.WarningCount, review);
    }

    private async Task<int[]> CompatibleGamesAsync(string packName, CancellationToken ct) =>
        (await db.Games.AsNoTracking().Select(g => new { g.Id, g.Name }).ToListAsync(ct)).Where(g => PopTrackerGameNames.Match(packName, g.Name)).Select(g => g.Id).ToArray();

    private async Task<GameScrape> DraftAsync(Guid id, CancellationToken ct)
    {
        var draft = await db.GameScrapes.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw new GameDataException("Scrape draft not found.", 404);
        if (draft.State != "Committed" && draft.CreatedAt < time.GetUtcNow().AddDays(-7)) throw new GameDataException("This draft expired. Upload the pack again.", 410);
        return draft;
    }

    public async Task CleanupAsync(CancellationToken ct)
    {
        Enter();
        try { await CleanupCoreAsync(ct); }
        finally { gate.Semaphore.Release(); }
    }
    private async Task CleanupCoreAsync(CancellationToken ct)
    {
        paths.EnsureCreated();
        var cutoff = time.GetUtcNow().AddDays(-7);
        var expired = await db.GameScrapes.Where(d => d.State != "Committed" && d.CreatedAt < cutoff).ToListAsync(ct);
        foreach (var draft in expired) { TryDelete(DraftDirectory(draft.Id)); db.GameScrapes.Remove(draft); }
        await db.SaveChangesAsync(ct);
        var active = (await db.GameScrapes.Where(d => d.State != "Committed").Select(d => d.Id).ToListAsync(ct)).ToHashSet();
        foreach (var dir in Directory.EnumerateDirectories(paths.GameStagingDirectory))
            if (Guid.TryParseExact(Path.GetFileName(dir), "N", out var id) && !active.Contains(id)) TryDelete(dir);
    }
    private void Enter()
    {
        if (!gate.Semaphore.Wait(0)) throw new GameDataException("Another game-data operation is running. Please retry shortly.", 503);
    }
    private void TryDelete(string directory)
    {
        // All callers supply a generated GUID directory under staging; never accept an archive/user path here.
        var full = Path.GetFullPath(directory);
        var root = Path.GetFullPath(paths.GameStagingDirectory) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Cleanup path is outside staging.");
        try { if (Directory.Exists(full)) Directory.Delete(full, true); }
        catch (IOException ex) { logger.LogWarning(ex, "Could not remove game staging directory {Directory}", full); }
        catch (UnauthorizedAccessException ex) { logger.LogWarning(ex, "Could not remove game staging directory {Directory}", full); }
    }
}
