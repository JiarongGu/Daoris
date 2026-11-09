namespace Daoris.Driver;

/// <summary>
/// The object a full ref names, read from the refs' files: its loose file whole at each ask, else its line of <c>packed-refs</c> (the
/// parse made once a content, <see cref="GitPackedRefs"/>), a symbolic ref followed to a depth of five. Nothing is left out, so a
/// <c>daoris/</c> line reads as any other. A reftable repository's, or a ref its files cannot say, is git's.
/// </summary>
internal static class TipOf
{
    private const int Depth = 5;

    /// <summary>The id <paramref name="name"/> holds in <paramref name="folder"/>'s repository; null where it holds none or git cannot say.</summary>
    internal static async Task<string?> OfAsync(string folder, string name, CancellationToken ct)
    {
        var (known, id) = FromFiles(folder, name);
        if (known) return id;
        var (code, stdout, _) = await WorkingTree.GitAsync(folder, ["rev-parse", "--verify", "--quiet", "--end-of-options", name + "^{commit}"], ct)
            .ConfigureAwait(false);
        return code == 0 && GitObjectHash.IsId(stdout.Trim()) ? stdout.Trim() : null;
    }

    /// <summary>
    /// What the files say: known, with the id or with none where no loose file and no packed line names it; not known for a reftable
    /// repository, a file that does not read, a value that is no id, or a chain past five.
    /// </summary>
    internal static (bool Known, string? Id) FromFiles(string folder, string name)
    {
        if (!Full(name)) throw new ArgumentException($"'{name}' is not a full ref name", nameof(name));
        if (RefFiles.GitDirOf(folder) is not { } own || RefFiles.CommonDirOf(folder) is not { } common) return (false, null);
        if (Directory.Exists(Path.Combine(common, "reftable"))) return (false, null);

        for (var depth = 0; depth <= Depth; depth++)
        {
            var loose = Path.Combine(Own(name) ? own : common, name.Replace('/', Path.DirectorySeparatorChar));
            string text;
            try
            {
                if (!File.Exists(loose))
                {
                    if (name == "HEAD") return (false, null);
                    var (packed, refs) = GitPackedRefs.Of(common);
                    return packed.Unread ? (false, null) : (true, refs.Tips.GetValueOrDefault(name));
                }


                text = File.ReadAllText(loose).Trim();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return (false, null);
            }

            if (!text.StartsWith("ref:", StringComparison.Ordinal)) return GitObjectHash.IsId(text) ? (true, text.ToLowerInvariant()) : (false, null);
            name = text["ref:".Length..].Trim();
            if (!Full(name)) return (false, null);
        }

        return (false, null);
    }

    private static bool Full(string name) =>
        name == "HEAD" || (name.StartsWith("refs/", StringComparison.Ordinal) && !name.EndsWith('/') && !name.Contains("..", StringComparison.Ordinal)
                           && !name.Contains('\\', StringComparison.Ordinal));

    // A tree's own refs live in its own git directory; every other in the common one.
    private static bool Own(string name) =>
        name == "HEAD" || name.StartsWith("refs/worktree/", StringComparison.Ordinal) || name.StartsWith("refs/bisect/", StringComparison.Ordinal)
        || name.StartsWith("refs/rewritten/", StringComparison.Ordinal);
}
