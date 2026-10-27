namespace Daoris.Driver;

/// <summary>
/// The files a repository declares generated (LANDREBASE1, D87's note): those its <c>.gitattributes</c> marks
/// <c>linguist-generated</c>, the declaration the platforms already read to fold such files out of a review. A landing's replay that
/// conflicts only in these is not the session's conflict: the line's copy is taken, and whoever merges the work writes them again.
/// </summary>
/// <remarks>
/// <para><c>-merge</c> is not read as the declaration, though this repository marks its generated index so: git's <c>binary</c> macro
/// sets it too, and taking the line's copy of an image both sides changed would drop the session's change without a word, since the
/// replay's proof allows every file the line brought.</para>
/// <para>Nothing here runs a repository's generator. The command that writes a generated folder is a declaration of its own, run only
/// as the person accepted it (<see cref="GeneratedWriters"/>, GENREGEN1); without one, a replay leaves them at the line's copy, and a
/// landing names them.</para>
/// </remarks>
internal static class GeneratedPaths
{
    /// <summary>The git attribute read: set or <c>true</c> marks a path generated.</summary>
    internal const string Attribute = "linguist-generated";

    /// <summary>How many stops one replay resolves at most before it gives up and refuses, as a conflict would.</summary>
    internal const int StopsAtMost = 500;

    /// <summary>
    /// Whether the repository at <paramref name="place"/> marks every one of <paramref name="paths"/> generated: by the attributes in its
    /// working tree, or as they stand at <paramref name="source"/> (a commit or a tree) where one is named. Null where git cannot say
    /// (a git before <c>check-attr --source</c> among them), which is no.
    /// </summary>
    internal static async Task<bool?> AllAsync(string place, IReadOnlyCollection<string> paths, CancellationToken ct, string? source = null)
    {
        if (paths.Count == 0) return false;
        var (code, output, _) = await WorkingTree.GitAsync(
            place, ["check-attr", "-z", .. source is null ? Array.Empty<string>() : [$"--source={source}"], Attribute, "--", .. paths], ct)
            .ConfigureAwait(false);
        if (code != 0) return null;
        var marked = Marked(output);
        return paths.All(marked.Contains);
    }

    /// <summary>
    /// Which of <paramref name="paths"/> the repository at <paramref name="place"/> marks generated, read as <see cref="AllAsync"/> reads
    /// them; null where git cannot say. The paths go on git's input, since a branch's files may be more than a command line holds.
    /// </summary>
    internal static async Task<IReadOnlySet<string>?> MarkedAsync(
        string place, IReadOnlyCollection<string> paths, CancellationToken ct, string? source = null)
    {
        if (paths.Count == 0) return new HashSet<string>(StringComparer.Ordinal);
        var (code, output, _) = await WorkingTree.GitAsync(
            place, ["check-attr", "-z", "--stdin", .. source is null ? Array.Empty<string>() : [$"--source={source}"], Attribute],
            string.Concat(paths.Select(path => path + "\0")), ct).ConfigureAwait(false);
        return code != 0 ? null : Marked(output);
    }

    /// <summary><c>git check-attr -z</c>'s answer read: each path whose attribute is set or <c>true</c>.</summary>
    internal static IReadOnlySet<string> Marked(string output)
    {
        var fields = output.Split('\0');
        var marked = new HashSet<string>(StringComparer.Ordinal);
        for (var at = 0; at + 2 < fields.Length; at += 3)
        {
            if (fields[at + 2] is "set" or "true") marked.Add(fields[at]);
        }

        return marked;
    }
}
