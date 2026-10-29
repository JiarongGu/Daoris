using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A repository's declared line rules (INTEGRATE1d; D215 point 6, the integration design §4.1's <c>merge</c>, §5.2 step 4):
/// <c>daoris.gates.json</c>'s <c>integration.merge</c> entries <c>{ "paths": file, "line": pattern, "run": command line }</c>. A
/// landing's replay that conflicts in such a file, where both sides changed only lines the pattern matches, takes the line's side
/// of those lines and has the rule's writer recompute them from the replayed tree (<see cref="GeneratedWriters.WriteAsync"/>), in a
/// commit on top; every other change of either side is kept as the merge by the base keeps it (<see cref="BacklogMerge.ByBase"/>). No
/// rule keeps one side's copy: a writer that fails refuses the replay as the conflict it was.
/// </summary>
/// <remarks>
/// <para><b>Only under the owner's acceptance for integration.</b> The writer is a command read from the repository, so the rules are
/// read from the blob the person accepted in <c>integrationGates</c> (<see cref="IntegrationAcceptance"/>), and only where that blob
/// is the declaration at the commit the replay builds on: a declaration the line changed since waits for the owner, and a session's
/// own copy is never read, so a session never runs a command its own branch declared. Without that, the file's conflict is the
/// session's, as it was before (INTEGRATE1c passed these rules over).</para>
/// <para>The merge tool runs no line rule yet: it has no twin here, and row j's runner will read the same accepted blob.</para>
/// </remarks>
internal static class LineRules
{
    /// <summary>How long a declared pattern may take over one line before it is read as matching nothing.</summary>
    private static readonly TimeSpan PatternBound = TimeSpan.FromMilliseconds(250);

    /// <summary>One file a replay settled by its line rule, and the writer that writes its lines again.</summary>
    internal sealed record Settled(string Path, string Run);

    /// <summary>
    /// The line rules <paramref name="repository"/>'s integration acceptance accepted, as declared at <paramref name="onto"/>: none
    /// where nothing is accepted, the accepted blob is not the one there, or its declaration does not read (D215 point 2, §4.3).
    /// </summary>
    internal static async Task<IReadOnlyList<IntegrationMergeRule>> AcceptedAsync(
        DriverConfig config, string root, string repository, string onto, CancellationToken ct)
    {
        var text = await IntegrationAcceptance.AcceptedTextAsync(config, root, repository, onto, ct).ConfigureAwait(false);
        return IntegrationDeclaration.Read(text).Facts?.Merge.Where(rule => !rule.ByRows).ToList() ?? [];
    }

    /// <summary>A rule's pattern as a regular expression over one line, or null where it does not read as one: such a rule settles nothing.</summary>
    internal static Regex? Pattern(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;
        try
        {
            return new Regex(line, RegexOptions.CultureInvariant, PatternBound);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// <paramref name="ours"/> (the side the work lands on) and <paramref name="theirs"/> merged by their <paramref name="base"/>, line by
    /// line as <see cref="BacklogMerge.ByBase"/> merges them, except that where both sides changed one place and every line either side
    /// took out or put in there matches <paramref name="line"/>, our side's change is kept there and theirs dropped, for the rule's
    /// writer to recompute. Null where both sides changed a place holding any other line: the conflict stays.
    /// </summary>
    internal static string? ByLine(string @base, string ours, string theirs, Regex line)
    {
        var b = @base.Split('\n');
        var mine = BacklogMerge.LineHunks(b, ours.Split('\n'));
        var yours = BacklogMerge.LineHunks(b, theirs.Split('\n'));
        var hunks = mine.Select(hunk => (Hunk: hunk, Side: 0)).ToList();
        foreach (var hunk in yours)
        {
            if (mine.Any(other => BacklogMerge.Same(other, hunk))) continue;
            var met = mine.Where(other => BacklogMerge.Collide(other, hunk)).ToList();
            if (met.Count > 0)
            {
                // The declared line both sides changed: the line's side stands until the writer writes it again.
                if (Lined(hunk) && met.All(Lined)) continue;
                return null;
            }

            hunks.Add((hunk, 1));
        }

        var output = new List<string>();
        var at = 0;
        foreach (var (hunk, _) in hunks.OrderBy(each => each.Hunk.Start).ThenBy(each => each.Hunk.End).ThenBy(each => each.Side))
        {
            if (hunk.Start > at) output.AddRange(b[at..hunk.Start]);
            output.AddRange(hunk.Lines);
            at = Math.Max(at, hunk.End);
        }

        if (at < b.Length) output.AddRange(b[at..]);
        return string.Join('\n', output);

        bool Lined(BacklogMerge.Hunk hunk) =>
            b[hunk.Start..hunk.End].Concat(hunk.Lines).All(text => Matches(line, text));
    }

    /// <summary>Whether <paramref name="text"/> matches, a pattern that runs past its bound read as no match, which keeps the conflict.</summary>
    private static bool Matches(Regex line, string text)
    {
        try
        {
            return line.IsMatch(text.TrimEnd('\r'));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// The words of a replay its line rules' writers could not finish, as a conflict's: the files they hold and why, for the session to
    /// settle in its own tree, since no rule keeps one side's copy.
    /// </summary>
    internal static string Unwritten(IReadOnlyList<Settled> settled, string failed) =>
        $"CONFLICT (line rule): {string.Join(", ", settled.Select(each => $"`{each.Path}`").Distinct(StringComparer.Ordinal))} "
        + $"settle only by the accepted writer, and writing them again from the replayed tree failed: {failed}.";
}
