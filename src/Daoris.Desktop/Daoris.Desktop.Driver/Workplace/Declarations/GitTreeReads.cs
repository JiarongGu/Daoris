using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The door for git reads that carry a path: a declaration's own fact is the store's, every other path as git answers it. Its members
/// take typed arguments, never an argument list, and each works inside the door's scope (<see cref="DeclarationScope"/>).
/// </summary>
/// <remarks>
/// A search is git's with every declaration excluded (<see cref="DeclarationNames.Excluded"/>), and the declarations' lines are found in
/// the store's bytes as git's search and <see cref="FileGit.LinesAsync"/> find them: a file holding a NUL in its first 8,000 bytes gives
/// none, each line split at LF is matched by ordinal, and a matched line is cut at the first other line ending
/// <see cref="string.ReplaceLineEndings()"/> knows, since that split keeps only the piece carrying the file's name.
/// </remarks>
internal static class GitTreeReads
{
    // Git's search takes a file holding a NUL this early for binary and searches none of it.
    private const int Binary = 8000;

    // A carriage return, a form feed, the next-line mark, and the line and paragraph separators.
    private static readonly char[] Endings = [(char)0x0D, (char)0x0C, (char)0x85, (char)0x2028, (char)0x2029];

    /// <summary>The file <paramref name="path"/> at <paramref name="commit"/>: a declaration from the store, any other path git's by path.</summary>
    internal static async Task<DeclarationAnswer> AtAsync(string root, string commit, string path, CancellationToken ct, int limit = RepositoryDeclarations.Bound)
    {
        using var _ = DeclarationScope.Enter(DeclarationAsker.Door);
        return DeclarationNames.Repository.Contains(path)
            ? await RepositoryDeclarations.AtAsync(root, commit, path, ct, limit).ConfigureAwait(false)
            : await RepositoryDeclarations.PassedAsync(root, commit, path, limit, ct).ConfigureAwait(false);
    }

    /// <summary>Whether <paramref name="path"/> differs between two commits, by its two entries' ids; null where either did not read.</summary>
    internal static async Task<bool?> ChangedAsync(string root, string from, string to, string path, CancellationToken ct)
    {
        using var _ = DeclarationScope.Enter(DeclarationAsker.Door);
        var before = await AtAsync(root, from, path, ct).ConfigureAwait(false);
        var after = await AtAsync(root, to, path, ct).ConfigureAwait(false);
        if (before is DeclarationAnswer.Unread || after is DeclarationAnswer.Unread) return null;
        return Identity(before) != Identity(after);
    }

    /// <summary>
    /// Each tracked line at <paramref name="commit"/> holding one of <paramref name="names"/> in the files <paramref name="pathspecs"/>
    /// name, as (file, line), in path order: git's search for every file but the declarations, the declarations' lines from the store.
    /// </summary>
    internal static async Task<IReadOnlyList<(string File, string Line)>> LinesAsync(
        string root, string commit, IReadOnlyCollection<string> names, IReadOnlyList<string> pathspecs, CancellationToken ct)
    {
        using var _ = DeclarationScope.Enter(DeclarationAsker.Door);
        if (names.Count == 0) return [];
        var found = (await FileGit.LinesAsync(root, commit, names, [.. pathspecs, .. DeclarationNames.Excluded], ct).ConfigureAwait(false)).ToList();
        foreach (var declaration in DeclarationNames.Repository.Where(name => pathspecs.Any(spec => Covers(spec, name))))
        {
            if (await RepositoryDeclarations.AtAsync(root, commit, declaration, ct).ConfigureAwait(false) is DeclarationAnswer.Present { Content: { } content })
            {
                found.AddRange(Lines(content, names).Select(line => (declaration, line)));
            }
        }

        return [.. found.OrderBy(each => each.File, StringComparer.Ordinal)];
    }

    /// <summary>The lines of <paramref name="content"/> git's search answers for <paramref name="names"/>, each as that search's reader keeps it.</summary>
    internal static IEnumerable<string> Lines(DeclarationBytes content, IReadOnlyCollection<string> names)
    {
        var bytes = content.Bytes.AsSpan();
        if (bytes[..Math.Min(bytes.Length, Binary)].Contains((byte)0)) return [];
        var text = Encoding.UTF8.GetString(bytes);
        var lines = text.Split('\n');
        return lines.Take(text.EndsWith('\n') ? lines.Length - 1 : lines.Length)
            .Where(line => names.Any(name => line.Contains(name, StringComparison.Ordinal)))
            .Select(line => line.IndexOfAny(Endings) is >= 0 and var cut ? line[..cut] : line)
            .ToList();
    }

    // A pathspec without magic matches as git's wildcard does, a star crossing folders, or names the path or a folder holding it.
    private static bool Covers(string spec, string path)
    {
        if (spec is "" or "." or ":/" || spec == path || path.StartsWith(spec.TrimEnd('/') + "/", StringComparison.Ordinal)) return true;
        if (spec.StartsWith(':')) return false;
        return Wild(spec, path);
    }

    private static bool Wild(ReadOnlySpan<char> spec, ReadOnlySpan<char> path)
    {
        while (!spec.IsEmpty)
        {
            if (spec[0] == '*')
            {
                for (var at = 0; at <= path.Length; at++) if (Wild(spec[1..], path[at..])) return true;
                return false;
            }

            if (path.IsEmpty || (spec[0] != '?' && spec[0] != path[0])) return false;
            spec = spec[1..];
            path = path[1..];
        }

        return path.IsEmpty;
    }

    private static string Identity(DeclarationAnswer answer) => answer switch
    {
        DeclarationAnswer.Present { Entry: { } entry } => entry.Id.Length > 0 ? entry.Id : entry.Type,
        DeclarationAnswer.Present { Content: { } content } => content.Id?.Hex ?? content.Key.Hex,
        _ => "",
    };
}
