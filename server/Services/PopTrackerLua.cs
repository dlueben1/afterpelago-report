using System.Globalization;
using System.Text;

namespace Afterpelago.Services;

/// <summary>Only recognizes literal data and AddItems calls; no Lua runtime or evaluation.</summary>
internal static class PopTrackerLua
{
    private sealed record Token(string Value, bool IsString = false);
    public sealed record Discovery(IReadOnlyList<string> ItemFiles, IReadOnlyDictionary<string, List<long>> ItemIds, bool HasCustomItems);

    public static Discovery Inspect(string source, CancellationToken ct)
    {
        var tokens = Tokenize(source, ct);
        var files = new List<string>();
        var ids = new Dictionary<string, List<long>>(StringComparer.Ordinal);
        var custom = false;
        for (var i = 0; i < tokens.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var t = tokens[i];
            if (t.IsString) continue;
            if (t.Value == "CreateLuaItem") custom = true;
            if (i + 4 < tokens.Count && t.Value == "Tracker" && tokens[i + 1].Value == ":" && tokens[i + 2].Value == "AddItems" && tokens[i + 3].Value == "(" && tokens[i + 4].IsString)
                files.Add(tokens[i + 4].Value);
            var key = t.Value.Replace("_", "").ToLowerInvariant();
            if (key is not ("itemmapping" or "itemmap") || i + 2 >= tokens.Count || tokens[i + 1].Value != "=" || tokens[i + 2].Value != "{") continue;
            var depth = 1;
            for (var j = i + 3; j < tokens.Count && depth > 0; j++)
            {
                if (tokens[j].IsString) continue;
                if (tokens[j].Value == "{") { depth++; continue; }
                if (tokens[j].Value == "}") { depth--; continue; }
                if (depth != 1 || j + 4 >= tokens.Count || tokens[j].Value != "[" || tokens[j + 2].Value != "]" || tokens[j + 3].Value != "=") continue;
                var number = tokens[j + 1].Value;
                var hex = number.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
                if (!long.TryParse(hex ? number[2..] : number, hex ? NumberStyles.HexNumber : NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) continue;
                var valueIndex = j + 4;
                var array = tokens[valueIndex].Value == "{";
                if (array) valueIndex++;
                if (valueIndex + 1 >= tokens.Count || !tokens[valueIndex].IsString) continue;
                // A following comma/closing brace proves this is a literal, not string concatenation or a call.
                if (tokens[valueIndex + 1].Value is not ("," or "}" or ";")) continue;
                var code = tokens[valueIndex].Value;
                if (code.Length > 256) continue;
                if (!ids.TryGetValue(code, out var list)) ids[code] = list = [];
                if (list.Count >= 4096) throw new GameDataException("A tracker code maps more than 4,096 AP item IDs.");
                if (!list.Contains(id)) list.Add(id);
            }
        }
        return new(files, ids, custom);
    }

    private static List<Token> Tokenize(string text, CancellationToken ct)
    {
        var result = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            ct.ThrowIfCancellationRequested();
            if (result.Count > 1_000_000) throw new GameDataException("A Lua file exceeds the token limit.");
            var c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '-' && i + 1 < text.Length && text[i + 1] == '-')
            {
                i += 2;
                if (TryLongString(text, ref i, out _)) continue;
                while (i < text.Length && text[i] is not ('\r' or '\n')) i++;
                continue;
            }
            if (TryLongString(text, ref i, out var longString)) { result.Add(new(longString, true)); continue; }
            if (c is '\'' or '"')
            {
                var quote = text[i++];
                var value = new StringBuilder();
                while (i < text.Length && text[i] != quote)
                {
                    var ch = text[i++];
                    if (ch == '\\' && i < text.Length)
                    {
                        ch = text[i++];
                        ch = ch switch { 'n' => '\n', 'r' => '\r', 't' => '\t', _ => ch };
                    }
                    value.Append(ch);
                }
                if (i < text.Length) i++;
                result.Add(new(value.ToString(), true));
                continue;
            }
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                var start = i++;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                result.Add(new(text[start..i]));
            }
            else { result.Add(new(c.ToString())); i++; }
        }
        return result;
    }

    private static bool TryLongString(string text, ref int index, out string value)
    {
        value = "";
        if (index >= text.Length || text[index] != '[') return false;
        var j = index + 1;
        while (j < text.Length && text[j] == '=') j++;
        if (j >= text.Length || text[j] != '[') return false;
        var closing = "]" + new string('=', j - index - 1) + "]";
        var end = text.IndexOf(closing, j + 1, StringComparison.Ordinal);
        if (end < 0) { index = text.Length; return true; }
        value = text[(j + 1)..end];
        index = end + closing.Length;
        return true;
    }
}
