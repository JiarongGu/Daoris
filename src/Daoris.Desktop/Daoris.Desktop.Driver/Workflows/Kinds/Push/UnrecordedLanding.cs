using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A <c>push</c> whose landing no entry of the landings file names any more (its trace dropped past the traces kept), read from its session
/// branch's own record instead, since nothing else of the landing is left to read. Held on the line (merged, or the same change, as a
/// squash-merged pull request leaves it), the push is passed over with words (<see cref="NothingToDeliver"/>); standing off the line, it is
/// started once on its door, which reads the line first (<see cref="PushLineRead"/>); neither, the door fails it once,
/// <see cref="PushLineRead.BranchGone"/>, saying why.
/// </summary>
internal static class UnrecordedLanding
{
    // The starts made, by home, start and attempt: in memory, so a restart starts each once more.
    private static readonly ConcurrentDictionary<string, byte> Started = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Why a delivery step past a landing on <paramref name="unit"/> has nothing to deliver where no entry names that landing and its session
    /// branch's record is held on the line; else null.
    /// </summary>
    public static string? HeldWords(WorkUnit unit) =>
        unit.Records.Landings.Count == 0 && unit.Records.Branch is { Held: { By: not EndBy.Empty } held } branch
            ? $"nothing left to deliver: `{branch.Branch}` is on `{held.On}` already ({WorkInventory.ProofSaid(held.By, held.On)}), and no record names "
                + "the branch its landing made any more, so there is nothing to push and no pull request to open from this run."
            : null;

    /// <summary>
    /// The run's active push, past a <c>land</c> reached on a landing no entry names, started on <paramref name="door"/> once per attempt and
    /// tip: the branch the land's evidence names, pushed where the session branch's record stands, else failed by the door with the sentence.
    /// Nothing where the land is not reached on a branch, a landing is on record, or the line holds the branch (the evaluation passes it over).
    /// </summary>
    public static void Keep(NodeKept kept, IPushDoor door)
    {
        if (NodeEvaluations.Landed(kept.Governed, kept.Record.Statuses) is not { } landed || landed.Unit.Records.Landings.Count > 0) return;
        if (HeldWords(landed.Unit) is not null) return;
        var grown = landed.Unit.Records.Branch;
        var named = BranchOf(kept);
        var branch = named ?? grown?.Branch ?? landed.Unit.Id;
        var watched = kept.Governed.Watched;
        var start = new PushStart(kept.Governed.Unit.Id, kept.Node, watched.Repository, watched.Workspace, watched.Root, branch, landed.Tip)
        {
            Authority = kept.Authority,
            Unrecorded = named is not null && grown is { GoneAt: null } ? null : Gone(branch, grown),
        };
        var key = $"{kept.Home}\n{start.Key}\n{kept.Status.Attempt}";
        if (!Started.ContainsKey(key) && door.Start(start)) Started.TryAdd(key, 0);
    }

    /// <summary>The door's answer for <paramref name="start"/> before it reads git: the failure its keeping found, or null where the push goes on.</summary>
    public static PushLineRead.Answer? Refusal(PushStart start) => start.Unrecorded is { } said ? new(PushLineRead.BranchGone, said) : null;

    /// <summary>The branch the run's <c>land</c> reached on, as its evidence names its landing; null where it names none.</summary>
    private static string? BranchOf(NodeKept kept)
    {
        var prefix = $"{LandedBranches.FileName}#";
        return kept.Governed.Graph.Nodes.Where(node => NodeKit.Of(node.Kind) is IMakesLanding)
            .Select(node => kept.Record.Statuses.GetValueOrDefault(node.Id)?.Acceptance?.Evidence?.Ref)
            .FirstOrDefault(evidence => evidence?.StartsWith(prefix, StringComparison.Ordinal) == true)?[prefix.Length..] is { Length: > 0 } branch
            ? branch
            : null;
    }

    private static string Gone(string branch, GrownBranch? grown) =>
        $"no record names the landing of `{branch}` any more, and its session branch {(grown is null ? "is no longer on record" : "is gone without the line holding it")}, "
        + "so nothing was pushed: restore the branch, or skip this step if the work is delivered.";
}
