namespace Daoris.Driver;

/// <summary>
/// The paths the person's own rules (<c>daoris files &lt;repository&gt; set output|intermediate|cache|scratch &lt;path&gt;</c>) say a
/// repository's tools write again, which a removal takes with a session's tree: what <see cref="FileOutputs"/> is for the repository's
/// file at its line's tip, read from the person's layer, which no session writes.
/// </summary>
/// <remarks>
/// <para><b>The floor is judged in the tree being cleaned.</b> The page's floor reads the registered checkout, whose <c>local/</c>
/// may hold another repository, so it holds every path under <c>local/</c> as one; a session's own tree holds what it holds, and a
/// removal asks of each path the rule covers whether, there, it is or lies inside another repository or tree (a <c>.git</c> at or above
/// it, below the tree's top, or a bare repository's shape). Where it does, nothing covers it: the rule never reaches another repository's
/// files.</para>
/// <para>A rule of another kind (<c>local</c>, <c>dependency</c>, <c>repository</c>) covers nothing here: it is the person's or has its
/// own door. The person's file that does not read covers nothing, which keeps the tree.</para>
/// </remarks>
internal static class FileRemade
{
    private static readonly FileKind[] Remade = [FileKind.Output, FileKind.Intermediate, FileKind.Cache, FileKind.Scratch];

    /// <summary>
    /// Whether a file of <paramref name="tree"/> is one the person's rules say is regenerated, or null where they say none is (so the
    /// removal asks nothing more). The path is relative to the tree, <c>/</c>-separated.
    /// </summary>
    internal static Func<string, bool>? Of(string home, string repository, string tree)
    {
        var person = PersonFiles.Read(home);
        if (person.Unread is not null) return null;
        var rules = FileRules.Of([], [], person, repository);
        if (!rules.Rules.Any(each => Remade.Contains(each.Rule.Kind))) return null;
        var none = new FileFloorFacts([], []);
        return path => rules.For(path, none) is { Kind: { } kind, Floor: null } && Remade.Contains(kind) && !InAnotherRepository(tree, path);
    }

    /// <summary>
    /// Whether <paramref name="path"/> of <paramref name="tree"/> is, or lies inside, another repository or tree: a <c>.git</c> in a
    /// folder above it below the tree's top, a bare repository's shape, or (for a folder) one it holds.
    /// </summary>
    internal static bool InAnotherRepository(string tree, string path)
    {
        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var folder = Path.GetFullPath(tree);
        for (var at = 0; at < segments.Length; at++)
        {
            folder = Path.Combine(folder, segments[at]);
            if (!Directory.Exists(folder)) break;
            var last = at == segments.Length - 1;
            // A folder the path ends at is walked, for any repository below it; one above it is read at its top alone.
            if (last && path.EndsWith('/') ? FileWalk.Of(folder).Holds : FileWalk.Repo(folder)) return true;
        }

        return false;
    }
}
