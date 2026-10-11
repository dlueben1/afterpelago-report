using System.Text.Json;
using System.Text.Json.Serialization;

namespace Afterpelago.Statistics;

/// <summary>Bump when the shared normalization (check classification, episode building) changes; it forces a full recalculation.</summary>
public static class StatisticsEngine
{
    public const int Version = 1;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>
/// The payload of one statistic. Stats fill only the fields they need, so adding a stat never changes the schema.
/// Slot ids refer to the session's slots.
/// </summary>
public sealed record StatValue
{
    public double? Number { get; init; }
    public string? Unit { get; init; }
    /// <summary>The slot the stat is about (finder, receiver, or winner depending on the stat).</summary>
    public int? SlotId { get; init; }
    public int? OtherSlotId { get; init; }
    /// <summary>The slot that owns <see cref="Item"/>; used to look up the item's artwork.</summary>
    public int? ItemReceiverSlotId { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public string? Item { get; init; }
    public string? Location { get; init; }
    public double? DurationSeconds { get; init; }
    public string? Text { get; init; }
}

/// <summary>One calculated statistic. Statistics lacking source data are unavailable rather than zero.</summary>
public sealed record StatResult(string Id, int Version, bool Available, string? Reason, StatValue? Value)
{
    public static StatResult Ok(string id, int version, StatValue value) => new(id, version, true, null, value);
    public static StatResult Unavailable(string id, int version, string reason) => new(id, version, false, reason, null);

    public static string Serialize(IReadOnlyList<StatResult> results) => JsonSerializer.Serialize(results, StatisticsEngine.Json);

    public static IReadOnlyList<StatResult> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<StatResult>>(json, StatisticsEngine.Json) ?? [];
}
