using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A verification tree whose <c>regenerate</c> failed because the work does not build with the line: its compiler named errors in the work's
/// code, which no new attempt on the same work answers, so the check fails at the <see cref="Gate"/> and goes back to the work with them.
/// A failure with no compiler error (a network, a locked file, a missing tool) stays the tree's, unprepared, and is asked again.
/// </summary>
internal static partial class UnbuiltTrees
{
    /// <summary>The gate a check that does not build fails at.</summary>
    internal const string Gate = "regenerate";

    /// <summary>The most errors a failure names.</summary>
    internal const int Most = 20;

    /// <summary>The compiler's error lines in <paramref name="output"/>, each once, relative to <paramref name="tree"/>, their project's bracket dropped.</summary>
    internal static IReadOnlyList<string> Errors(string output, string tree)
    {
        var root = tree.TrimEnd('\\', '/');
        return
        [
            .. output.ReplaceLineEndings("\n").Split('\n')
                .Where(line => Compiler().IsMatch(line))
                .Select(line => Bracket().Replace(line, "")
                    .Replace(root + "\\", "", StringComparison.OrdinalIgnoreCase).Replace(root + "/", "", StringComparison.OrdinalIgnoreCase).Trim())
                .Distinct(StringComparer.Ordinal)
                .Take(Most),
        ];
    }

    // A source location before it: an error the code makes has one, while the compiler's own I/O (a locked output it cannot write) has none.
    [GeneratedRegex(@"(\(\d+,\d+(,\d+,\d+)?\)|:\d+:\d+ -):? error (CS|TS|FS|BC)\d{4,5}:", RegexOptions.CultureInvariant)]
    private static partial Regex Compiler();

    [GeneratedRegex(@"\s+\[[^\[\]]+\.(cs|fs|vb)proj\]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex Bracket();
}
