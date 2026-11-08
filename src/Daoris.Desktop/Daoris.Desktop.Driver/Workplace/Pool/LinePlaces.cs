namespace Daoris.Driver;

/// <summary>
/// The one answer every folder-reader of a record's tree asks (<see cref="TreePool.WhereIs"/> read through the record's path): the
/// folder the line works in, and whether it stands. With no slot of the line in the home's pool, the answer is the record's own
/// path and the disk's.
/// </summary>
/// <param name="home">The Daoris home whose pool and trees are read.</param>
public sealed class LinePlaces(string home)
{
    private readonly TreePool _pool = new(home);

    /// <summary>Where the tree a record names is, read from the record's path alone; a path outside the home's trees is the disk's.</summary>
    public LineWhere Of(string tree)
    {
        var (workspace, repository) = OwnerOf(tree);
        return workspace.Length == 0 ? Disk(tree) : Of(workspace, repository, tree);
    }

    /// <summary>Where the line a record's <paramref name="tree"/> names is, in <paramref name="repository"/>'s pool.</summary>
    public LineWhere Of(string workspace, string repository, string tree)
    {
        var line = Path.GetFileName(Path.TrimEndingDirectorySeparator(tree));
        var held = _pool.WhereIs(workspace, repository, line);
        if (held is not null && Directory.Exists(held.Path)) return new(held.Path, LineStand.Stands, held);
        if (Directory.Exists(tree)) return new(tree, LineStand.Stands, null);
        return held is null && _pool.LastEnd(workspace, repository, line) is not null
            ? new(tree, LineStand.CanBeBroughtBack, null)
            : new(tree, LineStand.Gone, null);
    }

    /// <summary>The slots a line has worked in, the present one first: where an agent filed what it kept by folder.</summary>
    public IReadOnlyList<KeptTree> SlotsOf(string workspace, string repository, string tree)
    {
        var line = Path.GetFileName(Path.TrimEndingDirectorySeparator(tree));
        var slots = new List<KeptTree>();
        if (_pool.WhereIs(workspace, repository, line) is { } held) slots.Add(held);
        foreach (var end in _pool.Ends(workspace, repository, line))
        {
            if (_pool.Slot(workspace, repository, end.Number) is { } at && slots.All(each => each.Number != at.Number)) slots.Add(at);
        }

        return slots;
    }

    /// <summary>A tree's workspace and repository from the layout the home chose, or empty for a path outside it.</summary>
    public (string Workspace, string Repository) OwnerOf(string tree)
    {
        var full = Path.GetFullPath(tree);
        foreach (var root in DaorisHome.Places(home, HomeEntry.Trees))
        {
            var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(top, StringComparison.OrdinalIgnoreCase)) continue;
            var parts = full[top.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Length >= 3 ? (parts[0], parts[1]) : ("", "");
        }

        return ("", "");
    }

    private static LineWhere Disk(string tree) => new(tree, Directory.Exists(tree) ? LineStand.Stands : LineStand.Gone, null);
}
