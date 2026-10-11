using System.Text.Json;
using Afterpelago.Api;
using Afterpelago.Data;
using Afterpelago.Domain;
using Afterpelago.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Services;

public sealed class GameCatalogService(AfterpelagoDbContext db, TimeProvider time)
{
    // Called inside the session import transaction after the check/hint rows have been saved.
    public async Task ObserveSessionAsync(Session session, CancellationToken ct)
    {
        var games = await db.Games.ToDictionaryAsync(g => g.Name, StringComparer.Ordinal, ct);
        foreach (var slot in session.Slots.Where(s => !string.IsNullOrWhiteSpace(s.GameName)))
        {
            if (!games.TryGetValue(slot.GameName!, out var game))
            {
                game = new Game { Name = slot.GameName! };
                db.Games.Add(game);
                games.Add(game.Name, game);
            }
            slot.Game = game;
        }
        await db.SaveChangesAsync(ct);
        var observed = await db.Checks.Where(c => c.SessionId == session.Id && c.ReceiverSlot.GameId != null)
            .Select(c => new { GameId = c.ReceiverSlot.GameId!.Value, Name = c.ItemName })
            .Union(db.Hints.Where(h => h.SessionId == session.Id && h.ReceiverSlot.GameId != null).Select(h => new { GameId = h.ReceiverSlot.GameId!.Value, Name = h.ItemName })).ToListAsync(ct);
        foreach (var group in observed.Where(i => !string.IsNullOrWhiteSpace(i.Name)).GroupBy(i => i.GameId))
        {
            var existing = (await db.GameItems.Where(i => i.GameId == group.Key).Select(i => i.Name).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
            var added = group.Where(i => !existing.Contains(i.Name)).Select(i => new GameItem { GameId = i.GameId, Name = i.Name }).ToList();
            if (added.Count == 0) continue;
            db.GameItems.AddRange(added);
            var candidates = (await db.GameImageCandidates.Include(c => c.Asset).Where(c => c.GameId == group.Key).ToListAsync(ct)).Select(ToResponse).ToList();
            foreach (var item in added)
            {
                var (candidate, _) = GameImageMatcher.Match(item.Name, candidates);
                if (candidate is not null)
                {
                    var stored = db.GameImageCandidates.Local.Single(c => c.GameId == group.Key && c.SourceKey == candidate.Key);
                    item.Image = new GameItemImage { GameItem = item, Candidate = stored, Origin = "Automatic", UpdatedAt = time.GetUtcNow() };
                }
            }
            games.Values.Single(g => g.Id == group.Key).Revision++;
        }
        await db.SaveChangesAsync(ct);
    }

    public static GameCandidateResponse ToResponse(GameImageCandidate c) => new(c.SourceKey, c.Name, c.ItemName,
        JsonSerializer.Deserialize<string[]>(c.CodesJson)!, JsonSerializer.Deserialize<long[]>(c.ApIdsJson)!, c.SourceFile, c.Variant, c.PackUid,
        c.PackVersion, c.Stage, c.Modifiers, c.AutomaticEligible, c.Asset.Hash, c.Asset.Width, c.Asset.Height, c.Asset.ByteLength,
        $"/api/games/{c.GameId}/images/{c.Asset.Hash}.webp");
}
