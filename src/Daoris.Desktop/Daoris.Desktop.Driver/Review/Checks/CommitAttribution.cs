using System.Text.RegularExpressions;
using Daoris.Ports.Repositories;

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
    /// <summary>The trailer the check reads, as <see cref="IGitRepository.LogAsync"/> is asked for it.</summary>
    internal const string Trailer = "Co-Authored-By";

    // The makers of the built-in agents, and Daoris for the commits its driver writes: the same words the merge check accepts.
    [GeneratedRegex(@"\b(?:Anthropic|Claude|OpenAI|Codex|DeepSeek|Daoris)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Maker();

    [GeneratedRegex(@"^\s*(.+?)\s+<[^<>\s]+@[^<>\s]+>\s*$")]
    private static partial Regex Named();

    /// <summary>The commits of <paramref name="log"/> without a maker's trailer, a merge of the line into the work aside, in the log's order.</summary>
    internal static IReadOnlyList<UnsignedCommit> Unsigned(IReadOnlyList<GitLogEntry> log) =>
    [
        .. log.Where(commit => commit.Parents.Count <= 1 && !commit.Trailers.Any(Signs))
            .Select(commit => new UnsignedCommit(TextBounds.ShortCommit(commit.Id, 8), commit.Subject, commit.Trailers.Count == 0 ? null : string.Join('\n', commit.Trailers))),
    ];

    private static bool Signs(string trailer) => Named().Match(trailer) is { Success: true } match && Maker().IsMatch(match.Groups[1].Value);

    /// <summary>The reading's lines: what is wrong and what to do about it, none where every commit is signed.</summary>
    internal static string? Words(IReadOnlyList<UnsignedCommit> unsigned) => unsigned.Count == 0
        ? null
        : $"{unsigned.Count} {(unsigned.Count == 1 ? "commit carries" : "commits carry")} no `Co-Authored-By` line naming its maker and model, which the merge's commit check refuses: "
          + string.Join("; ", unsigned.Take(8).Select(commit => $"{commit.Sha} {commit.Subject}"))
          + (unsigned.Count > 8 ? $"; and {unsigned.Count - 8} more" : "") + ".";
}
