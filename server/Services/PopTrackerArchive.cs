using System.IO.Compression;
using System.Text.Json;
using Afterpelago.Api;

namespace Afterpelago.Services;

public sealed class GameDataException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}

/// <summary>Reads bounded entries in place. Archive names are never used as filesystem destinations.</summary>
public sealed class PopTrackerArchive : IDisposable
{
    public const long MaximumUpload = 50L * 1024 * 1024;
    public const long MaximumExpanded = 250L * 1024 * 1024;
    public const int MaximumEntry = 16 * 1024 * 1024;
    public static readonly JsonDocumentOptions JsonOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 64 };
    private readonly ZipArchive archive;
    private long bytesRead;
    public Dictionary<string, ZipArchiveEntry> Entries { get; } = new(StringComparer.Ordinal);

    public PopTrackerArchive(string path)
    {
        archive = ZipFile.OpenRead(path);
        try
        {
            if (archive.Entries.Count > 10_000) throw new GameDataException("The ZIP contains more than 10,000 entries.");
            long total = 0;
            foreach (var entry in archive.Entries)
            {
                var name = Normalize(entry.FullName);
                if (entry.IsEncrypted) throw new GameDataException("Encrypted ZIP entries are not supported.");
                var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
                if (unixType is not (0 or 0x8000 or 0x4000)) throw new GameDataException("The ZIP contains a link or special file.");
                if (!Entries.TryAdd(name, entry)) throw new GameDataException($"Duplicate ZIP path: {name}");
                total = checked(total + entry.Length);
                if (total > MaximumExpanded) throw new GameDataException("The expanded ZIP exceeds 250 MiB.");
            }
        }
        catch { archive.Dispose(); throw; }
    }

    public static string Normalize(string name)
    {
        name = name.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(name) || name.Length > 1024 || name.StartsWith('/') || name.Contains(':') || name.Any(char.IsControl))
            throw new GameDataException("The pack contains an unsafe path.");
        var parts = name.TrimEnd('/').Split('/');
        if (parts.Any(p => p is ".." or "" || p.EndsWith(' ') || p.EndsWith('.' ) && p != "."))
            throw new GameDataException($"Unsafe pack path: {name}");
        return string.Join('/', parts.Where(p => p != "."));
    }

    public async Task ValidateContentsAsync(CancellationToken ct)
    {
        var buffer = new byte[81920];
        foreach (var entry in Entries.Values)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.FullName.EndsWith('/')) continue;
            await using var stream = entry.Open();
            long size = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) != 0)
            {
                size += read;
                Count(read);
                if (size > entry.Length) throw new GameDataException("A ZIP entry exceeds its declared size.");
            }
            if (size != entry.Length) throw new GameDataException("A ZIP entry is truncated.");
        }
        bytesRead = 0;
    }

    public async Task<byte[]> ReadAsync(string name, CancellationToken ct)
    {
        if (!Entries.TryGetValue(name, out var entry)) throw new FileNotFoundException($"Missing pack entry: {name}");
        if (entry.Length > MaximumEntry) throw new GameDataException($"Pack entry exceeds 16 MiB: {name}");
        await using var input = entry.Open();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) != 0)
        {
            Count(read);
            if (output.Length + read > MaximumEntry) throw new GameDataException($"Pack entry exceeds 16 MiB: {name}");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    public async Task<PackManifest> ManifestAsync(CancellationToken ct)
    {
        var manifests = Entries.Keys.Where(n => n.Split('/').Last() == "manifest.json" && !n.StartsWith("__MACOSX/")).ToList();
        if (manifests.Count != 1) throw new GameDataException("Upload one PopTracker pack containing exactly one manifest.json.");
        using var doc = ParseJson(await ReadAsync(manifests[0], ct));
        var m = doc.RootElement;
        if (m.ValueKind != JsonValueKind.Object) throw new GameDataException("The pack manifest must be a JSON object.");
        var variants = m.TryGetProperty("variants", out var v) && v.ValueKind == JsonValueKind.Object
            ? v.EnumerateObject().Select(p => Normalize(p.Name)).ToArray() : [];
        var gameName = String(m, "game_name");
        if (string.IsNullOrWhiteSpace(gameName)) throw new GameDataException("The pack manifest must identify game_name.");
        var name = String(m, "name");
        var uid = String(m, "package_uid");
        var version = String(m, "package_version");
        if (variants.Length > 32 || new[] { name, gameName, uid, version }.Any(s => s.Length > 500))
            throw new GameDataException("Manifest metadata exceeds 500 characters or 32 variants.");
        return new PackManifest(name, gameName, uid, version, manifests[0][..^13], variants);
    }

    public static JsonDocument ParseJson(byte[] bytes) => JsonDocument.Parse(bytes.AsMemory(bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }) ? 3 : 0), JsonOptions);
    public static string String(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private void Count(int count)
    {
        bytesRead += count;
        if (bytesRead > MaximumExpanded) throw new GameDataException("Processing the pack exceeded the 250 MiB read budget.");
    }
    public void Dispose() => archive.Dispose();
}
