using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>A commit of the handed-back work with no <c>Co-Authored-By</c> line naming a maker: its short id, subject, and the trailer it does carry, if any.</summary>
internal sealed record UnsignedCommit(string Sha, string Subject, string? Trailer);

/// <summary>
/// The merge tool's commit check (<c>tools/merge/attribution.ts</c>) read at the hand-back: every commit the work adds, a merge of the line
/// into it aside, carries a <c>Co-Authored-By</c> line whose name is a maker this repository allows. A session's brief asks for the line
/// (<see cref="CommitTrailers"/>) and a harness may not write it, so Daoris says what the tree holds before the merge refuses it.
/// </summary>
internal static partial class CommitAttribution
{
    /// <summary>The merge check's log format, maker rule and trailer shape, held to one fixture by a test on each side.</summary>
    internal const string Format = "--format=%h%x1f%p%x1f%s%x1f%(trailers:key=Co-Authored-By,valueonly)%x1e";

    internal const string MakerPattern = @"\b(?:Anthropic|Claude|OpenAI|Codex|DeepSeek|Daoris)\b";

    internal const string NamedPattern = @"^\s*(.+?)\s+<[^<>\s]+@[^<>\s]+>\s*$";

    internal const string Remedy = "Rewrite each of them so its message ends with a `Co-Authored-By: <your maker> (<your model>) <address>` line naming you, for example by `git rebase --exec` with `git commit --amend --trailer`, and write that line in every commit you make from now on.";

    [GeneratedRegex(MakerPattern, RegexOptions.IgnoreCase)]
    private static partial Regex Maker();

    [GeneratedRegex(NamedPattern)]
    private static partial Regex Named();

    /// <summary>The commits in <paramref name="mergeBase"/>..<paramref name="tip"/> without a maker's trailer, oldest first; null where git could not say.</summary>
    internal static async Task<IReadOnlyList<UnsignedCommit>?> UnsignedAsync(string folder, string mergeBase, string tip, CancellationToken ct)
    {
        var (code, stdout, _) = await WorkingTree.GitAsync(folder, ["--no-optional-locks", "log", "--reverse", Format, $"{mergeBase}..{tip}"], ct).ConfigureAwait(false);
        return code == 0 ? Unsigned(stdout) : null;
    }

    /// <summary>The unsigned commits of <paramref name="log"/>, a <c>git log</c> in the check's format.</summary>
    internal static IReadOnlyList<UnsignedCommit> Unsigned(string log)
    {
        var found = new List<UnsignedCommit>();
        foreach (var entry in log.Split('\x1e').Select(each => each.Trim()).Where(each => each.Length > 0))
        {
            var parts = entry.Split('\x1f');
            if (parts.Length < 4 || parts[1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 1) continue;
            var trailer = parts[3].Trim();
            var named = trailer.Split('\n').Any(line => Named().Match(line) is { Success: true } match && Maker().IsMatch(match.Groups[1].Value));
            if (!named) found.Add(new(parts[0], parts[2], trailer.Length == 0 ? null : trailer));
        }

        return found;
    }

    /// <summary>The reading's lines: what is wrong and what to do about it, none where every commit is signed.</summary>
    internal static string? Words(IReadOnlyList<UnsignedCommit> unsigned) => unsigned.Count == 0
        ? null
        : $"{unsigned.Count} {(unsigned.Count == 1 ? "commit carries" : "commits carry")} no `Co-Authored-By` line naming its maker and model, which the merge's commit check refuses: "
          + string.Join("; ", unsigned.Take(8).Select(commit => $"{commit.Sha} {commit.Subject}"))
          + (unsigned.Count > 8 ? $"; and {unsigned.Count - 8} more" : "") + ".";
}
