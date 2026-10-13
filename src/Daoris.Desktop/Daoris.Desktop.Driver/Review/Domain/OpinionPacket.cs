namespace Daoris.Driver;

/// <summary>One path the candidate changes, as git names it: its status letter, and the path from the root where it now is.</summary>
public sealed record OpinionPath(string Status, string Path);

/// <summary>
/// The candidate as git read it (XAGENT1d; the second-agent design §1, §4): exactly what a pass reads, a base and a tip by their
/// full ids, the commits between them oldest first, and the paths the work changes.
/// </summary>
public sealed record OpinionCandidateRead(
    string Repository, string Base, string Tip, IReadOnlyList<string> Commits, IReadOnlyList<OpinionPath> Paths);

/// <summary>
/// The repository's own rules and records as they stand in the candidate (design §4), named by path and never quoted: the
/// reviewer reads them in its own harness's terms. Paths from the repository's root, with forward slashes.
/// </summary>
/// <param name="Doctrine">Its root instructions, then the indexes it keeps for its own documents.</param>
/// <param name="Decisions">Where it records its decisions, as its manifest declares it; null where it declares none here.</param>
/// <param name="Gates">What it declares safe to run (D122), its manifest's or the file by its own name; null where there is none.</param>
public sealed record OpinionRulesRead(IReadOnlyList<string> Doctrine, string? Decisions, string? Gates)
{
    /// <summary>A tree that holds none of them.</summary>
    public static OpinionRulesRead None { get; } = new([], null, null);

    public bool Equals(OpinionRulesRead? other) =>
        other is not null && Doctrine.SequenceEqual(other.Doctrine) && Decisions == other.Decisions && Gates == other.Gates;

    public override int GetHashCode() => HashCode.Combine(Doctrine.Count, Decisions, Gates);
}

/// <summary>
/// What a pass hands its reviewer (XAGENT1d, D155 point 6; design §4), assembled by Daoris with no model, from facts: the
/// candidate; what was asked, the quests of the work with the person's quoted requirements and the ask's words; what the
/// working session claims, each quest's closing note; what Daoris read of the evidence; and the repository's own rules. Never
/// the working session's conversation: a reviewer anchored on the worker's reasoning reads the work the way the worker did.
/// </summary>
/// <param name="Occasion">Why it is read: <c>landing</c>, <c>steps</c>, <c>failure</c> or <c>asked</c> (design §2.1).</param>
public sealed record OpinionPacket(string Occasion, OpinionCandidateRead Candidate)
{
    /// <summary>The quests of the work in this repository, as the service answered them; empty for a session's tip asked alone.</summary>
    public IReadOnlyList<QuestView> Quests { get; init; } = [];

    /// <summary>The person's words on the ask the work was asked by (DRIFT1b); null for work no ask asked.</summary>
    public AskWords? Words { get; init; }

    /// <summary>The repository's own rules and records, named by path.</summary>
    public OpinionRulesRead Rules { get; init; } = OpinionRulesRead.None;

    /// <summary>
    /// The whole diff's file, in the opinion's own folder under the home, which the reviewer is handed a read of; null where it
    /// could not be written, and the reviewer reads the diff from git in its copy.
    /// </summary>
    public string? Diff { get; init; }

    /// <summary>Whether the reviewer may build and run what the repository declares safe, in its copy (design §5.3).</summary>
    public bool Verify { get; init; }

    /// <summary>How long the pass may take, in minutes: the rule's bound.</summary>
    public int Minutes { get; init; } = OpinionRules.DefaultMinutes;

    /// <summary>
    /// For the one recheck (XAGENT1e, design §4, §6.5): the first pass's findings and each answer, so it says of each whether it
    /// stands or is withdrawn. Null for a first pass.
    /// </summary>
    public OpinionRecheckOf? Rechecks { get; init; }
}

/// <summary>
/// The packet's own rules (XAGENT1d): an opinion's folder, the most commits a candidate names, and git's lists read as commits and
/// paths. Reading the candidate from git, writing its diff and naming the rules in a copy are Review's Infrastructure's, named
/// through this type as before (<see cref="OpinionPacketReads"/>; STRUCT3c).
/// </summary>
public static class OpinionPackets
{
    /// <summary>The most commits a candidate names, the service's own bound (<c>Opinions.MostCommits</c>).</summary>
    public const int MostCommits = 1000;

    /// <summary>The diff's file in an opinion's folder.</summary>
    public const string DiffName = "candidate.diff";

    /// <summary>
    /// An opinion's own folder under the home (design §4), where the larger parts of its packet are kept. Refused for an id
    /// that is not one plain name, so no id names a path elsewhere.
    /// </summary>
    /// <exception cref="DriverException">The id is not one plain name.</exception>
    public static string Folder(string home, string opinion)
    {
        var id = opinion.Trim();
        if (id.Length == 0 || id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
        {
            throw new DriverException($"`{opinion}` is no second opinion's id, so it names no folder.");
        }

        return Path.Combine(home, "opinions", id);
    }

    /// <summary>The commits git listed, each a full id, each once, in its order; a line that is no full id is none.</summary>
    internal static IReadOnlyList<string> Commits(string listed)
    {
        var commits = new List<string>();
        foreach (var line in listed.Split('\n').Select(line => line.Trim()))
        {
            if (EvidenceCodes.IsObjectId(line) && !commits.Contains(line, StringComparer.OrdinalIgnoreCase)) commits.Add(line.ToLowerInvariant());
        }

        return commits;
    }

    /// <summary>
    /// The paths of <c>git diff --name-status -z</c>: a status, then its path, or for a rename or a copy the path it came from and
    /// the path it is now, which is the one named.
    /// </summary>
    internal static IReadOnlyList<OpinionPath> Paths(string nameStatus)
    {
        var fields = nameStatus.Split('\0');
        var paths = new List<OpinionPath>();
        for (var at = 0; at < fields.Length; at++)
        {
            var status = fields[at].Trim();
            if (status.Length == 0) continue;
            var letter = status[..1];
            var named = letter is "R" or "C" ? at + 2 : at + 1;
            if (named >= fields.Length) break;
            paths.Add(new OpinionPath(letter, fields[named]));
            at = named;
        }

        return paths;
    }
}
