using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A plugin's id as the settings and the catalogue read it (<see cref="PluginCatalog.IsId"/> answers through this), which a landing rule
/// names a plugin by; and <see cref="NameShape"/>, a name it gives inside itself, a step's, a port's or a system's, which a department's
/// system is named by. Twin of the second: the CLI's <c>STEP_NAME</c>.
/// </summary>
public static class PluginIds
{
    private static readonly Regex Shape = new(@"^[a-z0-9][a-z0-9.-]*\z", RegexOptions.CultureInvariant);

    public static readonly Regex NameShape = new(@"^[a-z0-9][a-z0-9-]{0,39}\z", RegexOptions.CultureInvariant);

    /// <summary>Whether a name has a plugin id's shape and is one the disk keeps as written, since it names the plugin's folder.</summary>
    public static bool IsId(string id) => Shape.IsMatch(id) && SafeNames.IsFolderId(id);
}
