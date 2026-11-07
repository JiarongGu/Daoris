using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A dependency's folder as the tree's files record holds it: its rule's path, its key's hash, how it came (<c>placed</c>,
/// <c>sealed</c>, <c>installed</c>), the manifest of what was placed, and <see cref="Installing"/> while Daoris's own install of it runs.
/// </summary>
internal sealed record TreePlacement(string Path, string Key, string How, DependencyManifest? Manifest)
{
    internal bool Installing { get; init; }
}

/// <summary>
/// The tree's files record: <see cref="Unworked"/> while no agent or terminal has been handed the tree since Daoris made or took it,
/// <see cref="HookOwed"/> while the <c>post-checkout</c> hook the making held has not run, and each placement.
/// </summary>
internal sealed record TreeFilesState(bool Unworked, bool HookOwed, IReadOnlyList<TreePlacement> Placements)
{
    internal static readonly TreeFilesState Made = new(true, true, []);

    internal TreePlacement? Of(string path) => Placements.FirstOrDefault(each => string.Equals(each.Path, path, StringComparison.Ordinal));

    internal TreeFilesState With(TreePlacement placement) => this with
    {
        Placements = [.. Placements.Where(each => !string.Equals(each.Path, placement.Path, StringComparison.Ordinal)), placement],
    };

    internal TreeFilesState Without(string path) => this with
    {
        Placements = [.. Placements.Where(each => !string.Equals(each.Path, path, StringComparison.Ordinal))],
    };
}

/// <summary>
/// The tree's files record in the home, beside every other tree's (<c>work/files/trees/</c>), written whole under a lock of its own,
/// so no writer puts back a mark another has removed. A tree with no record, or one that does not read, is worked: every tree standing
/// before the store, and any tree an agent has been handed.
/// </summary>
internal static class TreeFilesRecord
{
    internal static string PathOf(string home, string tree) => DaorisHome.Entry(home, HomeEntry.TreeFiles, IdOf(tree) + ".json");

    /// <summary>The tree's record by its full path's hash, so the record names no path a reader would take for the tree.</summary>
    internal static string IdOf(string tree)
    {
        var full = Path.GetFullPath(tree).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (OperatingSystem.IsWindows()) full = full.ToUpperInvariant();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(full)))[..20];
    }

    internal static RecordLock Lock(string home, string tree) => RecordLock.Take(RecordLock.PathOf(home, "tree-files-" + IdOf(tree)));

    /// <summary>The record, or null where there is none or it does not read: either way the tree is worked.</summary>
    internal static TreeFilesState? Read(string home, string tree)
    {
        var path = PathOf(home, tree);
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            var placements = new List<TreePlacement>();
            foreach (var each in root.GetProperty("placements").EnumerateArray())
            {
                placements.Add(new TreePlacement(
                    each.GetProperty("path").GetString()!, each.GetProperty("key").GetString()!, each.GetProperty("how").GetString()!,
                    each.TryGetProperty("manifest", out var manifest) ? DependencyManifest.FromJson(manifest) : null)
                {
                    Installing = each.TryGetProperty("installing", out var installing) && installing.GetBoolean(),
                });
            }

            return new TreeFilesState(root.GetProperty("unworked").GetBoolean(), root.GetProperty("hookOwed").GetBoolean(), placements);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException
            or KeyNotFoundException)
        {
            return null;
        }
    }

    /// <summary>The tree marked unworked, its hook owed, holding nothing yet: written by the step that makes or remakes it.</summary>
    internal static void Mark(string home, string tree)
    {
        using var held = Lock(home, tree);
        Write(home, tree, TreeFilesState.Made);
    }

    /// <summary>
    /// <c>unworked</c> removed, under the record's lock, before anything is handed the tree: true where the tree holds no mark after it,
    /// false where the removal could not be written, which hands nothing.
    /// </summary>
    internal static bool Unmark(string home, string tree)
    {
        try
        {
            using var held = Lock(home, tree);
            if (Read(home, tree) is not { Unworked: true } state) return true;
            Write(home, tree, state with { Unworked = false });
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// One of the stage's writes in an unworked tree: the mark read again under the lock, and <paramref name="change"/> written while
    /// it stands. False where it is gone, since a terminal's visit has taken it, and nothing is written.
    /// </summary>
    internal static bool WhileUnworked(string home, string tree, Func<TreeFilesState, TreeFilesState> change)
    {
        using var held = Lock(home, tree);
        if (Read(home, tree) is not { Unworked: true } state) return false;
        Write(home, tree, change(state));
        return true;
    }

    /// <summary>The record changed under its lock whatever its mark; a tree with none gets one, worked.</summary>
    internal static void Change(string home, string tree, Func<TreeFilesState, TreeFilesState> change)
    {
        using var held = Lock(home, tree);
        Write(home, tree, change(Read(home, tree) ?? new TreeFilesState(false, false, [])));
    }

    /// <summary>
    /// The mark read again under the lock and <paramref name="act"/> run while holding it, so a terminal's visit waits for this one
    /// write at most: false where the mark is gone and nothing ran.
    /// </summary>
    internal static bool Holding(string home, string tree, Action act)
    {
        using var held = Lock(home, tree);
        if (Read(home, tree) is not { Unworked: true }) return false;
        act();
        return true;
    }

    internal static void Forget(string home, string tree)
    {
        try
        {
            File.Delete(PathOf(home, tree));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A record of a gone tree is never read again: its path's hash names no tree that stands.
        }
    }

    private static void Write(string home, string tree, TreeFilesState state)
    {
        var path = PathOf(home, tree);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var placements = new JsonArray();
        foreach (var each in state.Placements)
        {
            var written = new JsonObject { ["path"] = each.Path, ["key"] = each.Key, ["how"] = each.How };
            if (each.Installing) written["installing"] = true;
            if (each.Manifest is { } manifest) written["manifest"] = manifest.ToJson();
            placements.Add(written);
        }

        AtomicFile.WriteText(path, new JsonObject { ["unworked"] = state.Unworked, ["hookOwed"] = state.HookOwed, ["placements"] = placements }.ToJsonString());
    }
}
