using Afterpelago.Api;
using Afterpelago.Data;
using Afterpelago.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Services;

/// <summary>Projects lightweight, account-specific overviews without loading log histories.</summary>
public static class SessionOverview
{
    public static async Task<IReadOnlyList<SessionSummaryResponse>> ListAsync(AfterpelagoDbContext db, string userId, int? sessionId, CancellationToken ct)
    {
        var query = db.Sessions.AsNoTracking().Where(s => s.State == SessionState.Published);
        if (sessionId is not null) query = query.Where(s => s.Id == sessionId);
        var summaries = await query.OrderByDescending(s => s.StartUtc).ThenByDescending(s => s.Id)
            .Select(s => new SessionSummaryResponse(s.Id, s.Name, s.StartUtc!.Value, s.EndUtc!.Value,
                s.Slots.Count, s.Checks.Count, s.Slots.Count(slot => slot.GoalCompleted),
                s.Status.ToString(), s.Favorites.Any(f => f.DiscordUserId == userId), Array.Empty<string>()))
            .ToListAsync(ct);
        if (summaries.Count == 0) return summaries;

        // A correlated DISTINCT collection requires APPLY, which SQLite does not support.
        // Fetch just the distinct session/game pairs in one additional query, never per row.
        var games = await db.Slots.AsNoTracking()
            .Where(slot => slot.Session.State == SessionState.Published && (sessionId == null || slot.SessionId == sessionId)
                && slot.GameName != null && slot.GameName.Trim() != "")
            .Select(slot => new { slot.SessionId, slot.GameName }).Distinct().ToListAsync(ct);
        var names = games.ToLookup(game => game.SessionId, game => game.GameName!);
        return summaries.Select(summary => summary with { GameNames = names[summary.Id].Order(StringComparer.OrdinalIgnoreCase).ToArray() }).ToList();
    }
}
