namespace Daoris.Driver;

/// <summary>
/// The installed plugin running commands a terminal's word names: the one whose id it is, else the one whose id without its family
/// (<see cref="PluginFamily.Tail"/>) it is, where no other installed plugin running commands has that tail. A tail two of them share
/// is refused, the candidates named, so <c>daoris tickets …</c> never reaches the wrong family's plugin.
/// </summary>
public static class PluginTerminalWord
{
    /// <summary>The installed plugins whose id, or whose id's tail, <paramref name="word"/> is: one, several (ambiguous), or none.</summary>
    public static IReadOnlyList<PluginEntry> Named(PluginCatalog catalog, string word)
    {
        var commanding = catalog.Plugins.Where(each => each.Manifest.Interfaces.Commands.Count > 0).ToList();
        var whole = commanding.Where(each => string.Equals(each.Manifest.Id, word, StringComparison.OrdinalIgnoreCase)).ToList();
        return whole.Count > 0
            ? whole
            : [.. commanding.Where(each => string.Equals(PluginFamily.Tail(each.Manifest.Id), word, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>The id <paramref name="word"/> names; <paramref name="word"/> itself where it names none, which the run then refuses.</summary>
    /// <exception cref="DriverException">The word is the tail of more than one plugin's id.</exception>
    public static string IdOf(PluginCatalog catalog, string word) => Named(catalog, word) switch
    {
        [var one] => one.Manifest.Id,
        [] => word,
        var several => throw new DriverException(
            $"`{word}` names more than one plugin: {string.Join(", ", several.Select(each => $"`{each.Manifest.Id}`").Order(StringComparer.Ordinal))}"
            + $" — name one by its id, as `daoris {several[0].Manifest.Id} …`."),
    };
}
