namespace Afterpelago.Api;

public sealed record ImportWebLogForm(string Name, IFormFile File);
public sealed record ImportWarningResponse(int LineNumber, string Reason);
public sealed record SlotResponse(int Id, int TeamNumber, string PlayerName, string? GameName, bool GoalCompleted);
public sealed record ImportCountsResponse(int Slots, int Checks, int Hints, int Events, int GameplayConnections, int TrackerConnections);
public sealed record SessionImportResponse(
    int SessionId, int ImportId, Guid RequestId, string Name, DateTime OriginalStart, DateTime OriginalEnd,
    IReadOnlyList<SlotResponse> Slots, ImportCountsResponse Counts, int WarningCount, IReadOnlyList<ImportWarningResponse> Warnings);
public sealed record PublishSessionRequest(int SourceUtcOffsetMinutes);
public sealed record SetSessionFavoriteRequest
{
    public required bool IsFavorite { get; init; }
}
public sealed record SessionFavoriteResponse(int SessionId, bool IsFavorite);
public sealed record SessionSummaryResponse(
    int Id, string Name, DateTimeOffset StartUtc, DateTimeOffset EndUtc, int SlotCount, int CheckCount, int CompletedSlotCount,
    string Status, bool IsFavorite, IReadOnlyList<string> GameNames);
public sealed record ConnectionResponse(
    long Id, DateTimeOffset TimestampUtc, string Action, string Kind, string? ClientVersion, IReadOnlyList<string> Tags);
public sealed record SessionSlotResponse(
    int Id, int TeamNumber, string PlayerName, string? GameName, bool GoalCompleted, IReadOnlyList<ConnectionResponse> Connections,
    string? CoverUrl, DateTimeOffset? GoalCompletedUtc);
public sealed record SessionImportInfoResponse(
    int Id, DateTimeOffset ImportedAt, string Method, string OriginalFileName, long ByteLength, string Sha256, int WarningCount);
public sealed record EpisodeResponse(int Index, DateTimeOffset StartUtc, DateTimeOffset EndUtc, int CheckCount);
public sealed record SessionDetailsResponse(
    int Id, string Name, DateTimeOffset StartUtc, DateTimeOffset EndUtc,
    IReadOnlyList<SessionSlotResponse> Slots, IReadOnlyList<SessionImportInfoResponse> Imports, string Status, bool IsFavorite,
    int CheckCount, int HintCount, long? TotalPlaytimeSeconds, int SourceUtcOffsetMinutes, IReadOnlyList<EpisodeResponse> Episodes);
public sealed record CheckResponse(
    long Id, DateTimeOffset TimestampUtc, int TeamNumber, int SenderSlotId, string SenderName,
    int ReceiverSlotId, string ReceiverName, string ItemName, string LocationName, int SourceLineNumber, string? ImageUrl);
public sealed record DailyCheckCountResponse(DateOnly Date, int Count);
