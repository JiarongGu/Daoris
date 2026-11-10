namespace Daoris.Driver;

/// <summary>
/// The changing verbs of <c>daoris browser</c> (<c>favorite add|remove</c>, <c>use</c>, <c>links</c>, <c>extensions</c>) as one path: the
/// typed edit of the home's browser files and its words. The person's press where Daoris runs, which says what this says; else the edit
/// alone, which the route runs too.
/// </summary>
public static class BrowserPresses
{
    /// <summary>A favorite kept (<c>add</c>) or dropped (<c>remove</c>): 0 done, 2 refused in the library's words.</summary>
    public static int Favorite(string home, string action, string address, string? title, TextWriter output, TextWriter error)
    {
        try
        {
            output.WriteLine(action == "add"
                ? BrowserWords.Kept(BrowserFavorites.Add(home, address, title))
                : BrowserWords.Dropped(address, BrowserFavorites.Remove(home, address)));
            return 0;
        }
        catch (DriverException refused)
        {
            return Refused(refused, error);
        }
    }

    /// <summary>One of the browser's three settings, <c>use</c>, <c>links</c> or <c>extensions</c>, set to <paramref name="value"/>.</summary>
    public static int Setting(string home, string area, string value, TextWriter output, TextWriter error)
    {
        try
        {
            switch (area)
            {
                case "extensions": BrowserSettings.SetExtensions(home, value); break;
                case "use": BrowserSettings.SetBrowser(home, value); break;
                case "links": BrowserSettings.SetLinks(home, value); break;
                default: throw new DriverException($"`{area}` is not a setting of Daoris's browser — one of use, links or extensions.");
            }

            output.WriteLine(BrowserWords.Set(area, value));
            return 0;
        }
        catch (DriverException refused)
        {
            return Refused(refused, error);
        }
    }

    /// <summary>The person's press of a favorite through the running Daoris where one answers; else <see cref="Favorite"/>.</summary>
    public static Task<int> PressFavoriteAsync(
        string home, string action, string address, string? title, TextWriter output, TextWriter error, InstanceWorld? instance, CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(
            instance, PersonPresses.BrowserFavorite, InstanceDoor.Said(("action", action), ("address", address), ("title", title)),
            () => Task.FromResult(Favorite(home, action, address, title, output, error)), output, ct);

    /// <summary>The person's press of a setting through the running Daoris where one answers; else <see cref="Setting"/>.</summary>
    public static Task<int> PressSettingAsync(
        string home, string area, string value, TextWriter output, TextWriter error, InstanceWorld? instance, CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(
            instance, PersonPresses.BrowserSetting, InstanceDoor.Said(("setting", area), ("value", value)),
            () => Task.FromResult(Setting(home, area, value, output, error)), output, ct);

    private static int Refused(DriverException refused, TextWriter error)
    {
        var said = FileSaid.Refused(refused.Message);
        foreach (var line in said.Err) error.WriteLine(line);
        return said.Exit;
    }
}
