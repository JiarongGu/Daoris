namespace Daoris.Driver;

/// <summary>
/// What <c>browser</c> says: one words function per verb over what the command read and the operation's result,
/// the lines the TypeScript verb said (<c>fixtures/driver-files-text.json</c>). A file that could not be read is said first and the
/// verb exits 1 with the default held.
/// </summary>
[CommandGroup("browser", "favorite", "extensions", "use", "links", Nested = ["favorite: list add remove"])]
public static class BrowserWords
{
    public static FileSaid Favorites(string home, FavoritesRead read)
    {
        if (read.Problem is { } problem) return new([$"daoris: ⚠ {problem}"], [], 1);
        if (read.Favorites.Count == 0)
        {
            return FileSaid.Said(
            [
                "daoris: no favorites yet — `daoris browser favorite add <address>` keeps a page, and",
                "  Daoris's browser shows them in a Daoris folder on its bookmarks bar.",
            ]);
        }

        var width = read.Favorites.Max(favorite => favorite.Title.Length);
        return FileSaid.Said([$"daoris: {BrowserFavorites.FilePath(home)}", .. read.Favorites.Select(favorite => $"  {favorite.Title.PadRight(width)}  {favorite.Url}")]);
    }

    public static string Kept(Favorite favorite) => $"daoris: {favorite.Title} ({favorite.Url}) is a favorite.";

    public static string Dropped(string typed, bool dropped)
    {
        var page = BrowserAddress.Page(typed) ?? typed;
        return dropped ? $"daoris: {page} is no longer a favorite." : $"daoris: {page} was not a favorite; nothing changed.";
    }

    /// <summary>What <c>browser extensions</c>, <c>use</c> or <c>links</c> says with no value: the setting, with the unreadable file said first.</summary>
    public static FileSaid Setting(string area, BrowserSettingsRead read)
    {
        List<string> lines = [];
        if (read.Problem is { } problem) lines.Add($"daoris: ⚠ {problem}; the default holds.");
        lines.Add(area switch
        {
            "extensions" when read.Extensions == ExtensionsSetting.Refuse =>
                "daoris: extensions other software registered for Chrome are refused by Daoris's browser.",
            "extensions" =>
                "daoris: extensions other software registered for Chrome are offered by Daoris's browser, each for your approval.",
            "use" when read.Browser == BrowserChoice.Edge =>
                "daoris: sessions and you use your Edge, on a profile of Daoris's under the home.",
            "use" => "daoris: sessions and you use Daoris's own browser, the engine it ships.",
            "links" when read.Links == LinksSetting.Daoris =>
                "daoris: links on Daoris's page open in Daoris's browser — its own, or your Edge where `browser use edge` chose it.",
            "links" => "daoris: links on Daoris's page open in the system's browser.",
            _ => throw new ArgumentOutOfRangeException(nameof(area), area, "browser says extensions, use or links"),
        });
        return FileSaid.Said(lines, read.Problem is null ? 0 : 1);
    }

    /// <summary>What <c>browser extensions</c>, <c>use</c> or <c>links</c> says once it has set <paramref name="value"/>.</summary>
    public static string Set(string area, string value) => (area, value) switch
    {
        ("extensions", _) => $"daoris: extensions other software registered for Chrome will be {(value == ExtensionsSetting.Refuse ? "refused" : "offered")} "
                             + "the next time Daoris's browser starts.",
        ("use", BrowserChoice.Edge) =>
            "daoris: Edge, from the next time the browser is opened. Edge signs its profile in to your Microsoft "
            + "account on its own, and its sync and extensions are yours to turn on. Your default Edge profile "
            + "cannot be driven: Chromium refuses a debug port there, so Daoris gives Edge a profile of its own.",
        ("use", _) => "daoris: Daoris's own browser, from the next time the browser is opened.",
        ("links", LinksSetting.Daoris) =>
            "daoris: links on Daoris's page open in Daoris's browser. No restart: an open window takes it up when "
            + "it is next brought to the front. A sign-in link still opens in the system's browser, where your own "
            + "sign-ins are.",
        ("links", _) =>
            "daoris: links on Daoris's page open in the system's browser. No restart: an open window takes it up "
            + "when it is next brought to the front.",
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, "browser sets extensions, use or links"),
    };
}
