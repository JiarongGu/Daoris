using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The private-name check a landing makes before anything is made: the lines the session's commits add to the line, read against the
/// private patterns file its repository declares (<c>sensitive.patternsFile</c>), taken from the repository's own checkout because a session's tree
/// is a worktree that holds none of the ignored <c>local/</c>. A repository declaring none, or whose checkout lacks the file, is unaffected.
/// </summary>
/// <remarks>
/// 🔴 A hold names the path and the pattern's number in the file, never its text: a private name is not repeated in a sentence, a log or a record.
/// </remarks>
internal static class SensitiveLandings
{
    /// <summary>The sentence that refuses the landing, or null where nothing the work adds matches a declared private pattern.</summary>
    internal static async Task<string?> RefusalAsync(string checkout, string tree, string line, CancellationToken ct)
    {
        if (TreeLocalInputs.PatternsFile(checkout) is not { } declared) return null;
        var file = Path.Combine(checkout, declared);
        if (!File.Exists(file)) return null;

        var patterns = new List<(int Number, Regex Pattern)>();
        var number = 0;
        foreach (var each in await File.ReadAllLinesAsync(file, ct).ConfigureAwait(false))
        {
            var text = each.Trim();
            if (text.Length == 0 || text.StartsWith('#')) continue;
            number++;
            try
            {
                patterns.Add((number, new Regex(text, RegexOptions.IgnoreCase)));
            }
            catch (ArgumentException)
            {
                return $"Nothing was landed: pattern {number} of `{declared}` is not a valid expression, so the work cannot be checked for private names.";
            }
        }

        if (patterns.Count == 0) return null;
        var (code, said, err) = await WorkingTree.GitAsync(
            tree, ["--no-optional-locks", "diff", "--unified=0", "--no-color", "--no-ext-diff", "--no-renames", $"{line}...HEAD"], ct).ConfigureAwait(false);
        if (code != 0) return $"Nothing was landed: the work could not be read for private names ({err.Trim().Split('\n')[0].Trim()}).";

        var path = "";
        var hits = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var added in said.Split('\n'))
        {
            if (added.StartsWith("+++ ", StringComparison.Ordinal))
            {
                path = added.StartsWith("+++ b/", StringComparison.Ordinal) ? added[6..].TrimEnd('\r') : "";
                continue;
            }

            if (added.Length == 0 || added[0] != '+' || path.Length == 0) continue;
            foreach (var (n, pattern) in patterns)
            {
                if (pattern.IsMatch(added[1..])) hits.Add($"`{path}` (pattern {n})");
            }
        }

        return hits.Count == 0
            ? null
            : $"Nothing was landed: the work adds a line matching a private pattern of `{declared}` in {string.Join(", ", hits)}. "
              + "Take it out of the work and commit again.";
    }
}
