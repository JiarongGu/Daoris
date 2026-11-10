namespace Daoris.Driver;

/// <summary>
/// A department reached only through its console, as its registration and its plans name it: the adapter plugin's id and the system's
/// own id there (<see cref="PluginSystem"/>), written <c>&lt;plugin&gt;/&lt;system&gt;</c>, never a path.
/// </summary>
public sealed record SystemPlace(string Plugin, string System)
{
    /// <summary>The place a <c>&lt;plugin&gt;/&lt;system&gt;</c> word names, or null where it names none.</summary>
    public static SystemPlace? Of(string? word)
    {
        var slash = word?.IndexOf('/', StringComparison.Ordinal) ?? -1;
        if (word is null || slash <= 0 || slash == word.Length - 1 || word.IndexOf('/', slash + 1) >= 0) return null;
        var (plugin, system) = (word[..slash], word[(slash + 1)..]);
        return PluginCatalog.IsId(plugin) && PluginSteps.NameShape.IsMatch(system) ? new SystemPlace(plugin, system) : null;
    }

    public override string ToString() => $"{Plugin}/{System}";
}
