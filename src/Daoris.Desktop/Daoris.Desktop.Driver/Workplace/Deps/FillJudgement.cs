using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What an install changed in its tree, from git's status read before and after: <see cref="Tracked"/>, each tracked file it changed
/// or path git does not ignore that it wrote, and <see cref="Beside"/>, each ignored path new outside its rule's paths.
/// </summary>
internal sealed record FillChanges(IReadOnlyList<string> Tracked, IReadOnlyList<string> Beside);

/// <summary>
/// The fill's judgement (R5.6): an install seals only where it changed nothing git tracks or does not ignore, wrote nothing new outside
/// its folder, left no link leading outside the tree, and wrote no file holding the tree's path (in UTF-8 or UTF-16, with either slash,
/// or as JSON escapes it) or a value its rule marks <c>secret</c>.
/// </summary>
internal static class FillJudgement
{
    /// <summary>Every path git's status names in the tree, ignored ones at their tops, with its two-letter state.</summary>
    internal static async Task<IReadOnlyDictionary<string, string>> StatusAsync(string tree, bool ignored, CancellationToken ct)
    {
        string[] ask = ignored
            ? ["status", "--porcelain", "-z", "--untracked-files=all", "--ignored=matching"]
            : ["status", "--porcelain", "-z", "--untracked-files=all"];
        var (code, said, error) = await WorkingTree.GitAsync(tree, ask, ct).ConfigureAwait(false);
        if (code != 0) throw new DriverException($"git could not read the tree's status: {error.Trim()}");
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        var entries = said.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var at = 0; at < entries.Length; at++)
        {
            var entry = entries[at];
            if (entry.Length < 4) continue;
            var state = entry[..2];
            paths[entry[3..]] = state;
            // A rename or a copy names its source in the entry after it.
            if (state[0] is 'R' or 'C') at++;
        }

        return paths;
    }

    internal static FillChanges Changes(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after, StoreDependency dependency)
    {
        var tracked = new List<string>();
        var beside = new List<string>();
        foreach (var (path, state) in after)
        {
            if (before.TryGetValue(path, out var was) && was == state && state is "!!" or "??") continue;
            if (state == "!!")
            {
                if (!dependency.Rule.Matches(path) && !path.StartsWith(dependency.Path, StringComparison.Ordinal)) beside.Add(path);
            }
            else if (!before.TryGetValue(path, out var earlier) || earlier != state)
            {
                tracked.Add(path);
            }
        }

        return new FillChanges([.. tracked.Order(StringComparer.Ordinal)], [.. beside.Order(StringComparer.Ordinal)]);
    }

    /// <summary>Why the folder may not be stored, or null where it may: a link outside the tree, the tree's path, or a secret value in a file.</summary>
    internal static DepsReason? Unsealable(string tree, string folder, DependencyManifest manifest, string? outside, IEnumerable<string> secrets)
    {
        if (outside is not null) return DepsWhy.LinksOut(outside);
        var patterns = new List<(byte[] Bytes, DepsReason Why)>();
        foreach (var spelled in DependencyKeys.TreeSpellings(tree).SelectMany(Cases))
        {
            patterns.Add((Encoding.UTF8.GetBytes(spelled), DepsWhy.NamesItsTree()));
            patterns.Add((Encoding.Unicode.GetBytes(spelled), DepsWhy.NamesItsTree()));
        }

        foreach (var secret in secrets.Where(value => value.Length > 0))
        {
            patterns.Add((Encoding.UTF8.GetBytes(secret), DepsWhy.HoldsSecret()));
            patterns.Add((Encoding.Unicode.GetBytes(secret), DepsWhy.HoldsSecret()));
        }

        foreach (var file in manifest.Files)
        {
            var bytes = File.ReadAllBytes(Path.Combine(tree, folder, file.Path));
            foreach (var (pattern, why) in patterns)
            {
                if (bytes.AsSpan().IndexOf(pattern) >= 0) return why;
            }
        }

        return null;
    }

    /// <summary>A path as a tool may spell its drive: as given, and with its drive letter in the other case.</summary>
    private static IEnumerable<string> Cases(string path)
    {
        yield return path;
        if (path.Length > 1 && path[1] == ':' && char.IsAsciiLetter(path[0]))
        {
            yield return (char.IsUpper(path[0]) ? char.ToLowerInvariant(path[0]) : char.ToUpperInvariant(path[0])) + path[1..];
        }
    }
}
