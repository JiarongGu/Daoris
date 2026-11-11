namespace Daoris.Driver;

/// <summary>What one rename moved from a plugin's old id to its new one, and what stayed because the new id already kept its own.</summary>
/// <param name="Plugin">The id the plugin has now.</param>
/// <param name="Old">The old id it declares.</param>
/// <param name="Moved">Each of <see cref="PluginRenames"/>' parts moved.</param>
/// <param name="Left">Each part that stayed under the old id.</param>
public sealed record PluginRename(string Plugin, string Old, IReadOnlyList<string> Moved, IReadOnlyList<string> Left);

/// <summary>
/// A plugin whose manifest declares old ids takes over what the person had under each: the
/// copy installed under the old id is taken out, and its switches, its data folder, its private storage and its approvals move to the
/// new id. What the new id already keeps is never overwritten; that part stays, said. Moving leaves nothing of the old id behind, so a
/// second start finds nothing to move. Daoris names no plugin here: every id comes from a manifest.
/// </summary>
public static class PluginRenames
{
    public const string Copy = "copy";
    public const string Switches = "switches";
    public const string Data = "data";
    public const string Storage = "storage";
    public const string Approvals = "approvals";

    public const string Attempts = "attempts";

    /// <summary>
    /// Every rename the installed plugins declare, moved where there is anything to move, each logged once as <c>plugin.renamed</c> by
    /// ids and part names alone. A home with no plugin declaring an old id reads its manifests and writes nothing.
    /// </summary>
    /// <param name="log">The machine log the renames are said in; one is opened for them where none is handed.</param>
    public static IReadOnlyList<PluginRename> Move(string home, MachineLog? log = null)
    {
        var catalog = PluginCatalog.Load(home);
        var renames = new List<PluginRename>();
        foreach (var (former, now) in PluginFormerly.Of(catalog))
        {
            var moved = new List<string>();
            var left = new List<string>();
            void Part(string part, bool? done)
            {
                if (done == true) moved.Add(part);
                else if (done == false) left.Add(part);
            }

            Part(Copy, TakeOut(home, catalog, former));
            Part(Switches, Switched(home, former, now));
            Part(Data, Carry(DataOf(home, former), DataOf(home, now)));
            Part(Storage, Carry(PluginPrivate.Folder(home, former), PluginPrivate.Folder(home, now)));
            var (approved, stayed) = HelpGrants.Renamed(home, former, now);
            Part(Approvals, approved > 0 ? true : stayed > 0 ? false : null);
            Part(Attempts, PluginRenamedAttempts.Current.Carry(home, former, now));
            if (moved.Count > 0) renames.Add(new PluginRename(now, former, moved, left));
        }

        if (renames.Count == 0) return renames;
        var own = log is null ? new MachineLog(home, "driver") : null;
        try
        {
            foreach (var rename in renames)
            {
                (log ?? own)!.Info("plugin.renamed", ("plugin", rename.Plugin), ("formerly", rename.Old),
                    ("moved", string.Join(",", rename.Moved)), ("left", string.Join(",", rename.Left)));
            }
        }
        finally
        {
            own?.Dispose();
        }

        return renames;
    }

    /// <summary>The plugin's own switch and its interfaces' carried over; false where <c>plugins.json</c> does not read, so nothing is rewritten over it.</summary>
    private static bool? Switched(string home, string former, string now)
    {
        try
        {
            return PluginState.Renamed(home, former, now) | PluginInterfaceSwitch.Renamed(home, former, now) ? true : null;
        }
        catch (DriverException)
        {
            return false;
        }
    }

    private static string DataOf(string home, string id) => Path.Combine(DaorisHome.Entry(home, HomeEntry.Plugins), PluginCatalog.DataFolder, id);

    /// <summary>
    /// The copy installed under the old id, moved aside whole and deleted as a remove takes it, its switch left to the rename; null where
    /// there is none, false where a process still holds it, so the next start takes it out.
    /// </summary>
    private static bool? TakeOut(string home, PluginCatalog catalog, string former)
    {
        var entry = catalog.Plugins.FirstOrDefault(each => string.Equals(each.Manifest.Id, former, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFileName(each.Folder), former, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;
        var aside = Path.Combine(DaorisHome.Entry(home, HomeEntry.Plugins), $".removing-{former}-{Guid.NewGuid():N}");
        try
        {
            PluginFolders.Move(entry.Folder, aside);
        }
        catch (Exception held) when (held is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        PluginFolders.Forget(aside);
        return true;
    }

    /// <summary>A folder renamed to the new id's place; null where there is none, false where the new id already has its own.</summary>
    private static bool? Carry(string from, string to)
    {
        if (!PluginFolders.There(from)) return null;
        if (PluginFolders.There(to)) return false;
        try
        {
            PluginFolders.Make(Path.GetDirectoryName(to)!);
            PluginFolders.Move(from, to);
            return true;
        }
        catch (Exception held) when (held is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
