namespace Daoris.Driver;

/// <summary>
/// What git says at a commit of what the line and the person's branches hold of a branch's work (LANDNODE1k), as a landing door reads it.
/// </summary>
/// <param name="Carried">The head's sentence where the line or a branch of the person's holds the work by content (SQUASHTIDY1f); else null.</param>
/// <param name="Unlanded">D88's proof: how many of its commits no branch of the person's holds; null where git could not say.</param>
/// <param name="Ahead">How many of its commits the line lacks; null where git could not say.</param>
public sealed record HoldContent(string? Carried, int? Unlanded, int? Ahead);

/// <summary>What git says of a session's tree for <c>content.holds</c> (LANDNODE1k), read as a landing door reads it.</summary>
public static class HoldReads
{
    /// <summary>
    /// What the line holds of <paramref name="tree"/>'s work at <paramref name="tip"/>: the head's sentence where the line or a branch of the person's
    /// holds it by content, and D88's proof. Null where the tree does not stand at that commit, so nothing is read at another.
    /// </summary>
    public static async Task<HoldContent?> ContentAsync(SessionTrees trees, string tree, string tip, CancellationToken ct = default)
    {
        if (!string.Equals(TreeTips.Read(tree), tip, StringComparison.OrdinalIgnoreCase)) return null;
        var carried = await trees.DiscardOfferAsync(tree, ct).ConfigureAwait(false);
        var unlanded = await trees.UnlandedHereAsync(tree, ct).ConfigureAwait(false);
        var ahead = unlanded == 0 ? await trees.AheadOfLineAsync(tree, ct).ConfigureAwait(false) : unlanded;
        return new HoldContent(carried?.NotLanded, unlanded, ahead);
    }
}

/// <summary>
/// The holds a landing's start judged outside the graph, as steps of it (LANDNODE1k; D145's and D173's notes of 2026-11-01): <c>quest.holds</c>
/// and <c>content.holds</c>, each observed by Daoris at the bound commit, begun as every observed built-in is. Pure: the caller hands in what git
/// read, and nothing is judged again or written.
/// </summary>
/// <remarks>
/// <para><b>A hold keeps the step active, never failed.</b> Each can let go later (a yes, a verdict, a new commit), so nothing fails it; the person's
/// pass with words is the way past one, as with <c>work.requests</c>.</para>
/// <para><b>Under Current a door judges them where it lands</b>, so a landing its record keeps reaches both, as a landed opinion's gate does.</para>
/// </remarks>
public static class HoldNodes
{
    /// <summary><c>quest.holds</c>: whether a done held for the person's yes (D133) holds the work.</summary>
    public const string Yes = "yes";

    /// <summary><c>quest.holds</c>: whether a done held for its evidence's verdict (D144) holds the work.</summary>
    public const string Evidence = "evidence";

    /// <summary><c>content.holds</c>: whether work the line or a branch of the person's holds by content holds it (SQUASHTIDY1f).</summary>
    public const string Line = "line";

    /// <summary><c>content.holds</c>: whether D88's proof holds it, where no commit is left that a branch of the person's lacks.</summary>
    public const string Proof = "proof";

    /// <summary>Each hold's options, in the order an editor lists them; each is <c>true</c> or <c>false</c>, and true where it is not written.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Options = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        [NodeKinds.QuestHolds] = [Yes, Evidence],
        [NodeKinds.ContentHolds] = [Line, Proof],
    };

    /// <summary>The first of a hold's options written as anything but <c>true</c> or <c>false</c>, by its name; null where each holds.</summary>
    public static string? Problem(NodeDeclaration node) =>
        Options.GetValueOrDefault(node.Kind)?.FirstOrDefault(name => node.Options.TryGetValue(name, out var raw) && NodeOptions.Value(raw) is not bool);

    /// <summary>Whether a hold's option is on: true where it is not written.</summary>
    public static bool On(NodeDeclaration node, string option) => !node.Options.TryGetValue(option, out var raw) || NodeOptions.Value(raw) is not false;

    /// <summary>Why the quest's own holds hold the work, or null where they let it go (the look's own <c>unreviewed</c> is the look's).</summary>
    public static string? QuestHeld(QuestView? quest, bool yes = true, bool evidence = true) => quest switch
    {
        null => "its quest was not read",
        { Status: "Declined" } => "its quest was declined",
        _ when !QuestResults.Judged(quest) => "its quest's done is not in, or was taken back",
        { Held: true, Hold: null } when yes => "its done is held for your yes",
        { Held: true, Hold: { } code } when evidence && code != EvidenceCodes.Unreviewed => "its done is held for its evidence's verdict",
        _ => null,
    };

    /// <summary>Why what the line holds holds the work, or null where it lets it go.</summary>
    public static string? ContentHeld(HoldContent read, bool line = true, bool proof = true) =>
        line && read.Carried is { } carried ? carried.TrimEnd()
        : !proof ? null
        : read.Unlanded is null ? "git could not say which of its commits no branch of yours holds"
        : read.Unlanded > 0 ? null
        : read.Ahead == 0 ? "it holds no commit the line lacks: nothing to land"
        : "a branch of yours already holds each of its commits (D88's proof), so nothing is landed again";

    /// <summary>What Daoris reads of <paramref name="node"/> for <paramref name="unit"/> on <paramref name="binding"/>: reached once nothing it judges holds, else none.</summary>
    public static NodeObservation Observe(WorkUnit unit, NodeDeclaration node, NodeAcceptance binding, NodeGitReads? git)
    {
        NodeObservation Seen(string? outcome, string said) => new(unit.Id, node.Id, node.Kind, outcome, said) { Attempt = binding.Attempt };

        NodeObservation Reached(string record, string? revision, string said) => Seen(NodeStates.Reached, said) with
        {
            Source = NodeSource.Holds,
            Acceptance = binding with { Evidence = new NodePort(BuiltInNodes.EvidenceSchema(node.Kind), $"{record}#{unit.Id}", revision, binding.Id) },
        };

        if (!Options.ContainsKey(node.Kind)) return Seen(null, "this kind is not a hold") with { Refused = NodeRefusal.NotObserved };
        if (binding.Observes != unit.Id || binding.Node != node.Id || binding.NodeVersion != node.Version)
        {
            return Seen(null, "its binding names another subject, node or version") with { Refused = NodeRefusal.NotBound };
        }

        // A landing recorded at the bound commit: what its door judged let it go, so the hold keeps that reading.
        if (unit.Kind == WorkUnitKind.Branch && LandedAt(unit, binding) is { } landed)
        {
            return Reached(LandedBranches.FileName, binding.SubjectRevision ?? landed.Tip, $"`{landed.Branch}` landed: what its door judged let it go");
        }

        if (node.Kind == NodeKinds.QuestHolds)
        {
            if (unit.Kind != WorkUnitKind.Quest) return Seen(null, "its door judges the quest's holds where it lands");
            return QuestHeld(unit.Records.Quest, On(node, Yes), On(node, Evidence)) is { } why
                ? Seen(null, why)
                : Reached(WorkRecord.Quests, binding.SubjectRevision, "nothing of its quest's own holds it");
        }

        if (unit.Kind != WorkUnitKind.Branch) return Seen(null, $"it reads a branch's work, and this is a {unit.Kind}");
        if (git?.Content?.Invoke(unit.Id, binding.SubjectRevision) is not { } read)
        {
            return Seen(null, "what the line holds of it was not read at the bound commit");
        }

        return ContentHeld(read, On(node, Line), On(node, Proof)) is { } held
            ? Seen(null, held)
            : Reached("git", binding.SubjectRevision, $"{read.Unlanded} of its commits no branch of yours holds, and the line does not hold it by content");
    }

    /// <summary>
    /// The landing a branch unit's records keep for <paramref name="binding"/>: on a run's binding, the one made at or squashing its bound commit,
    /// or the merge its session branch records there; on a unit's own, its newest, or the merge its session branch records. Null where none is kept.
    /// </summary>
    private static LandedBranch? LandedAt(WorkUnit unit, NodeAcceptance binding)
    {
        if (binding.Subject is not null)
        {
            if (binding.SubjectRevision is not { } bound) return null;
            // LANDNODE1m: a merge keeps no landing record, and a replayed one leaves its tree on the commit merged, which the run then binds.
            return unit.Records.Landings.LastOrDefault(each => each.Tip == bound || each.Squashed == bound)
                ?? (unit.Records.Branch?.Held is { } held && string.Equals(held.Tip, bound, StringComparison.OrdinalIgnoreCase) ? Merged(unit) : null);
        }

        return BuiltInNodes.LandingOf(unit) ?? Merged(unit);
    }

    /// <summary>The merge a branch unit's session branch records, read as a landing at the commit the line holds; null where none is held.</summary>
    private static LandedBranch? Merged(WorkUnit unit) =>
        unit.Records.Branch is { Held: { } merged } branch
            ? new LandedBranch(unit.Owner.Repository ?? "", unit.Owner.Workspace, branch.Branch, null, merged.Tip, unit.Executors.LastOrDefault() ?? "", null, null, merged.At)
            : null;
}
