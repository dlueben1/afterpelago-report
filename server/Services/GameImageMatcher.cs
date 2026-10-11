using System.Text.RegularExpressions;
using Afterpelago.Api;

namespace Afterpelago.Services;

public static partial class GameImageMatcher
{
    private static string Normalize(string value) => Whitespace().Replace(value.Trim(), " ").ToUpperInvariant();
    public static (GameCandidateResponse? Candidate, string Evidence) Match(string name, IEnumerable<GameCandidateResponse> candidates)
    {
        var eligible = candidates.Where(c => c.AutomaticEligible).ToList();
        var exact = eligible.Where(c => c.ItemName == name || c.Name == name).ToList();
        var matches = exact.Count > 0 ? exact : eligible.Where(c => Normalize(c.ItemName) == Normalize(name) || Normalize(c.Name) == Normalize(name)).ToList();
        if (matches.Count == 0) return (null, "No matching tracker name");
        if (matches.Select(c => c.Hash).Distinct().Count() > 1) return (null, "Conflicting images; choose manually");
        return (matches.OrderBy(c => c.Key, StringComparer.Ordinal).First(), exact.Count > 0 ? "Exact item name" : "Unique name after whitespace/case normalization");
    }
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
