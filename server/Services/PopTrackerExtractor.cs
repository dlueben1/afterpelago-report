using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Afterpelago.Api;

namespace Afterpelago.Services;

public sealed record PackExtraction(IReadOnlyList<GameCandidateResponse> Candidates, IReadOnlyList<string> Warnings, int WarningCount);

public sealed class PopTrackerExtractor(GameImageConverter converter)
{
    public async Task<PackExtraction> ExtractAsync(PopTrackerArchive archive, PackManifest manifest, Guid draftId, string uploadHash, string outputDirectory, CancellationToken ct)
    {
        var warnings = new HashSet<string>(StringComparer.Ordinal);
        var warningCount = 0;
        void Warn(string message) { if (warnings.Contains(message)) return; warningCount++; if (warnings.Count < 200) warnings.Add(message); }
        var loadedFiles = new HashSet<string>(StringComparer.Ordinal);
        var mappings = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);
        foreach (var path in archive.Entries.Keys.Where(p => p.StartsWith(manifest.Root, StringComparison.Ordinal) && p.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal))
        {
            var lua = PopTrackerLua.Inspect(Encoding.UTF8.GetString(await archive.ReadAsync(path, ct)), ct);
            foreach (var itemFile in lua.ItemFiles) loadedFiles.Add(PopTrackerArchive.Normalize(manifest.Root + itemFile));
            foreach (var (code, ids) in lua.ItemIds)
            {
                if (!mappings.TryGetValue(code, out var set)) mappings[code] = set = [];
                set.UnionWith(ids);
            }
            if (lua.HasCustomItems) Warn($"{path}: Lua-generated items require manual mapping; pack scripts were not executed.");
        }

        var images = new Dictionary<string, ConvertedGameImage?>(StringComparer.Ordinal);
        var candidates = new List<GameCandidateResponse>();
        foreach (var path in archive.Entries.Keys.Where(p => p.StartsWith(manifest.Root, StringComparison.Ordinal) && p.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).OrderBy(p => loadedFiles.Contains(p) ? 0 : 1).ThenBy(p => p, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            JsonDocument doc;
            try { doc = PopTrackerArchive.ParseJson(await archive.ReadAsync(path, ct)); }
            catch (JsonException) { Warn($"{path}: invalid JSON; file skipped."); continue; }
            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;
                var relative = path[manifest.Root.Length..];
                var fileVariant = manifest.Variants.FirstOrDefault(v => relative.StartsWith(v + "/", StringComparison.Ordinal));
                var contexts = fileVariant is not null ? new[] { fileVariant } : new[] { "" }.Concat(manifest.Variants).ToArray();
                var index = -1;
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    index++;
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var type = PopTrackerArchive.String(item, "type");
                    if (type is not ("toggle" or "consumable" or "static" or "progressive"))
                    {
                        if (type.Length > 0 && (loadedFiles.Contains(path) || relative.StartsWith("items/", StringComparison.Ordinal))) Warn($"{path}: unsupported item type '{type}'.");
                        continue;
                    }
                    var name = PopTrackerArchive.String(item, "name");
                    if (name.Length == 0 || name.Length > 500) { Warn($"{path}[{index}]: item name must contain 1–500 characters; skipped."); continue; }
                    var manual = type == "static" || relative.Split('/').Any(s => s is "settings.json" or "internal.json" or "er_tables") || name.StartsWith("Shuffle ", StringComparison.OrdinalIgnoreCase);
                    var stages = item.TryGetProperty("stages", out var stageArray) && stageArray.ValueKind == JsonValueKind.Array
                        ? stageArray.EnumerateArray().Where(s => s.ValueKind == JsonValueKind.Object).ToArray() : new[] { item };
                    foreach (var variant in contexts.Distinct(StringComparer.Ordinal))
                    {
                        // An explicit variant definition overrides the corresponding base file.
                        if (fileVariant is null && variant.Length > 0 && archive.Entries.ContainsKey(manifest.Root + variant + "/" + relative)) continue;
                        var foundDefault = false;
                        var inheritedCodes = new HashSet<string>(Codes(item), StringComparer.Ordinal);
                        for (var stage = 0; stage < stages.Length; stage++)
                        {
                            ct.ThrowIfCancellationRequested();
                            var definition = stages[stage];
                            if (definition.TryGetProperty("inherit_codes", out var inherit) && inherit.ValueKind == JsonValueKind.False) inheritedCodes = new(Codes(item), StringComparer.Ordinal);
                            inheritedCodes.UnionWith(Codes(definition));
                            var image = PopTrackerArchive.String(definition, "img");
                            if (image.Length == 0) continue;
                            var mods = PopTrackerArchive.String(definition, "img_mods");
                            if (mods.Length > 2048) throw new GameDataException("An image modifier exceeds 2,048 characters.");
                            var basePath = PopTrackerArchive.Normalize(manifest.Root + image);
                            var variantPath = variant.Length > 0 ? PopTrackerArchive.Normalize(manifest.Root + variant + "/" + image) : basePath;
                            var imagePath = archive.Entries.ContainsKey(variantPath) ? variantPath : basePath;
                            if (!images.TryGetValue(imagePath, out var converted))
                            {
                                if (!archive.Entries.ContainsKey(imagePath)) Warn($"{imagePath}: missing image.");
                                else
                                {
                                    try
                                    {
                                        converted = converter.Convert(await archive.ReadAsync(imagePath, ct), outputDirectory, ct);
                                        if (converted.Animated) Warn($"{imagePath}: only the first animation frame is used.");
                                    }
                                    catch (InvalidDataException ex) { Warn($"{imagePath}: {ex.Message}"); }
                                }
                                images[imagePath] = converted;
                            }
                            if (converted is null) continue;
                            if (mods.Length > 0) Warn($"{path}: modifiers are not rendered; affected images require manual selection.");
                            var eligible = !manual && !foundDefault && mods.Length == 0;
                            if (mods.Length == 0) foundDefault = true;
                            var codes = inheritedCodes.Order(StringComparer.Ordinal).ToArray();
                            var apIds = codes.Where(mappings.ContainsKey).SelectMany(c => mappings[c]).Distinct().Order().ToArray();
                            var stageName = PopTrackerArchive.String(definition, "name");
                            if (stageName.Length > 500) stageName = name;
                            var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{uploadHash}|{path}|{index}|{variant}|{stage}")));
                            candidates.Add(new(key, stageName.Length > 0 ? stageName : name, name, codes, apIds, relative + $"[{index}]", variant,
                                manifest.Uid, manifest.Version, type == "progressive" ? stage : null, mods, eligible, converted.Hash,
                                converted.Width, converted.Height, converted.ByteLength, $"/api/admin/game-scrapes/{draftId}/images/{converted.Hash}.webp"));
                            if (candidates.Count > 20_000) throw new GameDataException("The pack exceeds 20,000 image candidates.");
                        }
                    }
                }
            }
        }
        foreach (var code in mappings.Keys.Where(code => !candidates.Any(c => c.Codes.Contains(code))).Take(25))
            Warn($"Tracker code '{code}' has an AP ID mapping but no usable image definition.");
        if (candidates.Count == 0) throw new GameDataException("No usable item images were found. The pack may generate its items dynamically in Lua.");
        return new(candidates, warnings.ToArray(), warningCount);
    }

    private static IEnumerable<string> Codes(JsonElement item)
    {
        var codes = PopTrackerArchive.String(item, "codes").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (codes.Length > 256 || codes.Any(c => c.Length > 256)) throw new GameDataException("An item exceeds the limit of 256 codes of 256 characters each.");
        return codes;
    }
}
