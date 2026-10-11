namespace Afterpelago.Api;

/// <summary>A slot referenced by a statistic, with enough detail to render it without a second lookup.</summary>
public sealed record StatSlotResponse(
    int Id, string PlayerName, string? GameName, string? CoverUrl, string Ownership, string? OwnerDiscordUserId, string? OwnerDisplayName,
    string? OwnerAvatarHash);

/// <summary>
/// One statistic. Fields a statistic does not use are null. An unavailable statistic carries a reason instead of a value,
/// so missing source data is never shown as zero.
/// </summary>
public sealed record StatResponse(
    string Id, bool Available, string? Reason,
    double? Number, string? Unit, StatSlotResponse? Slot, StatSlotResponse? OtherSlot,
    DateTimeOffset? Timestamp, string? Item, string? Location, string? ImageUrl, double? DurationSeconds, string? Text);

public sealed record SessionStatisticsResponse(int SessionId, string SessionName, DateTimeOffset? ComputedAt, IReadOnlyList<StatResponse> Stats);

public sealed record SlotStatisticsResponse(
    int SessionId, string SessionName, StatSlotResponse Slot, int TeamNumber, DateTimeOffset? ComputedAt, IReadOnlyList<StatResponse> Stats);

public sealed record RecalculationResponse(int Sessions, int Users);
