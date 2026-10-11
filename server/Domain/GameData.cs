namespace Afterpelago.Domain;

public sealed class GameItem
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string Name { get; set; } = "";
    public GameItemImage? Image { get; set; }
}

public sealed class GameImageAsset
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string Hash { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public long ByteLength { get; set; }
}

public sealed class GameImageCandidate
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int AssetId { get; set; }
    public GameImageAsset Asset { get; set; } = null!;
    public Guid? ImportId { get; set; }
    public string SourceKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string CodesJson { get; set; } = "[]";
    public string ApIdsJson { get; set; } = "[]";
    public string SourceFile { get; set; } = "";
    public string Variant { get; set; } = "";
    public string PackUid { get; set; } = "";
    public string PackVersion { get; set; } = "";
    public string Modifiers { get; set; } = "";
    public int? Stage { get; set; }
    public bool AutomaticEligible { get; set; }
}

// A row with CandidateId=null is an explicit manual "no image", not an unresolved item.
public sealed class GameItemImage
{
    public int GameItemId { get; set; }
    public GameItem GameItem { get; set; } = null!;
    public int? CandidateId { get; set; }
    public GameImageCandidate? Candidate { get; set; }
    public string Origin { get; set; } = "Manual";
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class GameScrape
{
    public Guid Id { get; set; }
    public int? GameId { get; set; }
    public int? ExpectedRevision { get; set; }
    public string State { get; set; } = "Uploaded";
    public string OriginalFileName { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string ManifestJson { get; set; } = "{}";
    public string ResultsJson { get; set; } = "{}";
    public string? CommitResponseJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
