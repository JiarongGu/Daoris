namespace Daoris.Driver;

/// <summary>
/// A plugin's private storage: <c>profiles/plugins/&lt;id&gt;/</c>, limited to the person's account before a byte is written, handed
/// as <see cref="Variable"/> to that plugin's own processes alone, and only where its manifest implements <c>storage</c> and the person
/// left it on. The plugin reads and writes it itself; Daoris reads nothing in it, logs nothing of it, and removes it with the plugin.
/// </summary>
public static class PluginPrivate
{
    /// <summary>The variable the folder travels under.</summary>
    public const string Variable = ChildMarks.PluginPrivateVariable;

    /// <summary>The folder of a plugin's storage, by its id.</summary>
    public static string Folder(string home, string id) => Path.Combine(DaorisHome.Entry(home, HomeEntry.PluginPrivate), id);

    /// <summary>The folder a start of <paramref name="plugin"/> is handed, made at its first; null where it is handed none.</summary>
    public static string? Handed(string home, PluginEntry plugin)
    {
        var id = plugin.Manifest.Id;
        if (plugin.Manifest.Interfaces.Storage is null || !PluginInterfaceSwitch.IsOn(home, id, PluginInterfaces.Storage)) return null;
        var folder = Folder(home, id);
        if (Directory.Exists(folder)) return folder;
        try
        {
            GitPrivateFolder.Make(folder, $"plugin `{id}`'s private storage", "storage-exists");
        }
        catch (DriverException) when (Directory.Exists(folder))
        {
            // Another start of the same plugin made it first.
        }

        return folder;
    }

    /// <summary>A removed plugin's storage, gone with it; nothing to remove is an answer.</summary>
    public static void Remove(string home, string id)
    {
        var folder = Folder(home, id);
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }
}
