using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The plugins' answer to where the <see cref="ChangeTopics.Plugins"/> topic's files are (FOUNDATION1 mod-vii, the design §14.3): the plugins
/// folder, heard as a plugin arrives or leaves and as its manifest changes. Installed when the driver's assembly loads, before any watch is built.
/// </summary>
internal static class PluginHomeTopic
{
    [ModuleInitializer]
    internal static void Install() => HomeTopics.Answer(ChangeTopics.Plugins, home => [Folder(DaorisHome.Entry(home, HomeEntry.Plugins))]);

    /// <summary>
    /// The plugins folder: a plugin arriving or leaving (a folder made, renamed or removed under it) and its manifest changing, never the
    /// <see cref="PluginCatalog.DataFolder"/> its plugins keep their own state in, nor any folder named with a dot.
    /// </summary>
    internal static HomePart Folder(string folder)
    {
        var full = Path.GetFullPath(folder);
        return new(
            full,
            Subfolders: true,
            (moved, _) =>
            {
                var parts = moved.Split('/');
                return !parts[0].StartsWith('.')
                    && (parts.Length == 1 || (parts.Length == 2 && string.Equals(parts[1], PluginCatalog.ManifestName, StringComparison.OrdinalIgnoreCase)));
            },
            () => Directory.Exists(full)
                ? Directory.EnumerateDirectories(full)
                    .Where(dir => !Path.GetFileName(dir).StartsWith('.'))
                    .Select(dir => KeyValuePair.Create(dir, HomeWrites.Stamp(Path.Combine(dir, PluginCatalog.ManifestName))))
                : []);
    }
}
