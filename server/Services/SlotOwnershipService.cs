using Afterpelago.Api;
using Afterpelago.Data;
using Afterpelago.Domain;
using Afterpelago.Domain.Sessions;
using Afterpelago.Statistics;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Services;

/// <summary>Assigns slots to accounts, either by admin decision or by the account's own claim.</summary>
public sealed class SlotOwnershipService(AfterpelagoDbContext db, TimeProvider time, StatisticsService statistics)
{
    /// <summary>Sets a slot's owner regardless of its current state. Works on drafts so owners can be mapped during import.</summary>
    public async Task<SlotOwnerResponse?> SetOwnershipAsync(int sessionId, int slotId, SetSlotOwnershipRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<SlotOwnership>(request.Ownership, out var ownership) || !Enum.IsDefined(ownership))
            throw new SessionInputException("Ownership must be Unclaimed, User, Shared, or NonUser.");
        if (ownership == SlotOwnership.User && string.IsNullOrEmpty(request.OwnerDiscordUserId))
            throw new SessionInputException("An owner is required when the slot belongs to a user.");

        var slot = await db.Slots.Include(s => s.Session)
            .SingleOrDefaultAsync(s => s.Id == slotId && s.SessionId == sessionId && s.Session.State != SessionState.Deleting, ct);
        if (slot is null) return null;

        AccessRecord? owner = null;
        if (ownership == SlotOwnership.User)
        {
            owner = await db.AccessRecords.SingleOrDefaultAsync(a => a.DiscordUserId == request.OwnerDiscordUserId, ct);
            if (owner is null || owner.Status != AccessStatus.Approved) throw new SessionInputException("The owner must be an approved user.");
        }

        var previousOwner = slot.OwnerDiscordUserId;
        slot.Ownership = ownership;
        slot.OwnerDiscordUserId = owner?.DiscordUserId;
        slot.OwnershipSource = ownership == SlotOwnership.Unclaimed ? null : SlotOwnershipSource.Admin;
        slot.OwnershipUpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);

        if (slot.Session.State == SessionState.Published) await statistics.RecalculateUsersAsync([previousOwner, owner?.DiscordUserId], ct);
        return new SlotOwnerResponse(slot.Id, ownership.ToString(), owner?.DiscordUserId, owner?.DisplayName, owner?.AvatarHash);
    }

    /// <summary>
    /// Saves the caller's claims for a published session and marks it reviewed. Slots an admin assigned to the caller stay theirs;
    /// self-claimed slots left out of the request are released so a claim can be corrected later.
    /// </summary>
    public async Task<bool> ClaimAsync(int sessionId, string userId, ClaimSlotsRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Sessions.AnyAsync(s => s.Id == sessionId && s.State == SessionState.Published, ct)) return false;
        var slots = await db.Slots.Where(s => s.SessionId == sessionId).ToListAsync(ct);
        var desired = request.SlotIds.Distinct().ToList();
        if (desired.Any(id => slots.All(s => s.Id != id))) throw new SessionInputException("A selected slot does not belong to this session.");
        var adminOwned = slots.Where(s => s.Ownership == SlotOwnership.User && s.OwnerDiscordUserId == userId && s.OwnershipSource == SlotOwnershipSource.Admin).ToList();
        if (request.NotInSession && (desired.Count > 0 || adminOwned.Count > 0))
            throw new SessionInputException("You cannot skip a session that has slots assigned to you.");

        var now = time.GetUtcNow();
        foreach (var slot in slots)
        {
            var mine = slot.Ownership == SlotOwnership.User && slot.OwnerDiscordUserId == userId;
            var wanted = desired.Contains(slot.Id);
            if (wanted && !mine)
            {
                if (slot.Ownership != SlotOwnership.Unclaimed) throw new SessionConflictException($"{slot.PlayerName} is already assigned to someone else.");
                slot.Ownership = SlotOwnership.User;
                slot.OwnerDiscordUserId = userId;
                slot.OwnershipSource = SlotOwnershipSource.SelfClaim;
                slot.OwnershipUpdatedAt = now;
            }
            else if (!wanted && mine && slot.OwnershipSource == SlotOwnershipSource.SelfClaim)
            {
                slot.Ownership = SlotOwnership.Unclaimed;
                slot.OwnerDiscordUserId = null;
                slot.OwnershipSource = null;
                slot.OwnershipUpdatedAt = now;
            }
        }

        var review = await db.SessionReviews.SingleOrDefaultAsync(r => r.SessionId == sessionId && r.DiscordUserId == userId, ct);
        if (review is null) db.SessionReviews.Add(new SessionReview { DiscordUserId = userId, SessionId = sessionId, ReviewedAt = now, NotInSession = request.NotInSession });
        else { review.ReviewedAt = now; review.NotInSession = request.NotInSession; }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await statistics.RecalculateUsersAsync([userId], ct);
        return true;
    }

    /// <summary>
    /// Published sessions the account has neither reviewed nor been assigned to by an admin, and that still have unclaimed slots.
    /// </summary>
    public async Task<IReadOnlyList<NeedsReviewResponse>> GetNeedsReviewAsync(string userId, CancellationToken ct) =>
        await db.Sessions.AsNoTracking()
            .Where(s => s.State == SessionState.Published
                && !db.SessionReviews.Any(r => r.SessionId == s.Id && r.DiscordUserId == userId)
                && !s.Slots.Any(slot => slot.Ownership == SlotOwnership.User && slot.OwnerDiscordUserId == userId)
                && s.Slots.Any(slot => slot.Ownership == SlotOwnership.Unclaimed))
            .OrderByDescending(s => s.StartUtc).ThenByDescending(s => s.Id)
            .Select(s => new NeedsReviewResponse(s.Id, s.Name, s.StartUtc!.Value, s.Slots.Count, s.Slots.Count(slot => slot.Ownership == SlotOwnership.Unclaimed)))
            .ToListAsync(ct);
}
