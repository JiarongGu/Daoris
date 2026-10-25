using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A plugin's id as the settings and the catalogue read it (FOUNDATION1 mod-iii): lowercase letters, digits, dots and dashes, never
/// leading with a dot or a dash. A landing rule names a plugin by it, so the settings' parser holds the shape, and the catalogue's
/// <see cref="PluginCatalog.IsId"/> answers through this.
/// </summary>
public static class PluginIds
{
    private static readonly Regex Shape = new(@"^[a-z0-9][a-z0-9.-]*\z", RegexOptions.CultureInvariant);

    /// <summary>Whether a name has a plugin id's shape: lowercase letters, digits, dots and dashes, never leading with a dot or a dash.</summary>
    public static bool IsId(string id) => Shape.IsMatch(id);
}
