namespace Afterpelago.Domain;

public enum AccessStatus
{
    Pending,
    Approved,
    Denied,
}

/// <summary>
/// One row per Discord account that has ever signed in. Signing in only records/refreshes the row;
/// application access requires <see cref="Status"/> to be <see cref="AccessStatus.Approved"/> (set by an admin in SQLite).
/// </summary>
public sealed class AccessRecord
{
    /// <summary>Immutable Discord snowflake. Always a string: it exceeds JavaScript's safe integer range.</summary>
    public required string DiscordUserId { get; set; }

    public string DisplayName
    {
        get => field;
        set => field = value.Length > 100 ? value[..100] : value;
    } = "";

    public string? Username { get; set; }

    public AccessStatus Status { get; set; } = AccessStatus.Pending;

    public string Role { get; set; } = "Member";

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
}
