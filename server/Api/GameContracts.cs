namespace Afterpelago.Api;

public sealed record GameResponse(int Id, string Name, string? CoverUrl, int? CoverWidth, int? CoverHeight);
public sealed record GameItemResponse(int Id, string Name, string? ImageUrl);
public sealed record PackManifest(string Name, string GameName, string Uid, string Version, string Root, IReadOnlyList<string> Variants);
public sealed record GameCandidateResponse(
    string Key, string Name, string ItemName, IReadOnlyList<string> Codes, IReadOnlyList<long> ApIds,
    string SourceFile, string Variant, string PackUid, string PackVersion, int? Stage,
    string Modifiers, bool AutomaticEligible, string Hash, int Width, int Height, long ByteLength, string ImageUrl);
public sealed record ItemMappingResponse(int Id, string Name, string? CandidateKey, string Origin, string Evidence, string? ImageUrl);
public sealed record GameMappingsResponse(int GameId, int Revision, IReadOnlyList<ItemMappingResponse> Items, IReadOnlyList<GameCandidateResponse> Candidates);
public sealed record GameScrapeResponse(Guid Id, string State, int? GameId, int? ExpectedRevision,
    PackManifest Manifest, IReadOnlyList<int> CompatibleGameIds, IReadOnlyList<string> Warnings, int WarningCount, GameMappingsResponse? Review);
public sealed record ScrapeGameRequest(int GameId);
// Unresolved rows need not be sent. null explicitly suppresses future automatic matching.
public sealed record ItemMappingEdit(string Name, string? CandidateKey);
public sealed record SaveGameMappingsRequest(int ExpectedRevision, IReadOnlyList<ItemMappingEdit> Mappings);
public sealed record CustomGameImageResponse(int Revision, GameCandidateResponse Candidate);
