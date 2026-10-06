namespace Afterpelago.Data;

/// <summary>Bound from the "Afterpelago" configuration section (env: Afterpelago__DataDirectory).</summary>
public sealed class AppDataOptions
{
    public const string SectionName = "Afterpelago";

    // C# 14 field-backed property: normalizes whatever the configuration providers hand us.
    public string DataDirectory
    {
        get => field;
        set => field = value?.Trim() ?? "";
    } = "";
}
