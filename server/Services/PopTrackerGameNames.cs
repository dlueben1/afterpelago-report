namespace Afterpelago.Services;

public static class PopTrackerGameNames
{
    // Explicit known aliases only: fuzzy game-name matching could associate the wrong game's assets.
    public static bool Match(string packName, string gameName) => Canonical(packName).Equals(Canonical(gameName), StringComparison.OrdinalIgnoreCase);
    private static string Canonical(string name) => name.Trim().Equals("The Legend of Zelda: The Wind Waker", StringComparison.OrdinalIgnoreCase) ? "The Wind Waker" : name.Trim();
}
