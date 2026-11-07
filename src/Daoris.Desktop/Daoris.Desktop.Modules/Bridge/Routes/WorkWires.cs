using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The work inventory's answers as the bridge carries them and the work slice pushes them: one builder for both doors, so the pull and the push cannot drift.</summary>
public static class WorkWires
{
    /// <summary>
    /// One unit's milestone as <c>WORK_STATE</c> answers it and the work slice pushes it. A field added here is pushed only once
    /// <see cref="WorkStateSlice.Fields"/> names it.
    /// </summary>
    public static object WorkUnit(IKnowledgeMilestone unit) => new
    {
        unit.Unit, unit.Repository, unit.Branch, unit.Session,
        End = new { unit.End.State, unit.End.By, unit.End.On, unit.End.Revision, unit.End.At, unit.End.Said },
        Result = unit.Result.Select(WorkEntry).ToArray(),
        Cleanup = new { unit.Cleanup.State, Waits = unit.Cleanup.Waits.Select(WorkEntry).ToArray(), Gone = unit.Cleanup.Gone.Select(WorkEntry).ToArray() },
        Recovery = unit.Recovery.Select(WorkEntry).ToArray(),
        Entries = unit.Entries.Select(WorkEntry).ToArray(),
    };

    /// <summary>
    /// One inventory entry as <c>WORK_STATE</c> answers it and the work slice pushes it. Additive: a blocked
    /// entry's <c>code</c> and the <c>facts</c> the page words it with; a page before them shows its detail as recorded.
    /// </summary>
    public static object WorkEntry(InventoryEntry each) =>
        new { each.Unit, each.Kind, each.Resource, each.Role, each.Disposition, each.Detail, each.Until, each.Proof, each.Repository, each.Commit, each.Code, each.Facts };

    /// <summary>
    /// The attempts held for the person as the wire carries them: the page words <c>why</c> by its code. Additive: <c>words</c>,
    /// the words offered on each (a plugin's three; a held close's recoveries and its stop), which a page before the field reads without.
    /// A failure kept for the person, its <c>why</c> <c>failure-kept</c> or <c>tries-spent</c>, its failure's
    /// <c>code</c>, and its one word <c>again</c>. Additive: a held check's <c>verification</c>, what it verifies (its run, repository, commit,
    /// line, base, branch, failed gate and why not known), which a page before the field words as any held step.
    /// </summary>
    public static object[] HeldAnswer(IEnumerable<HeldAttempt> held) =>
    [
        .. held.Select(each => new
        {
            each.Unit, each.Node, each.Attempt, each.Plugin, each.Step, each.NodeState, each.Why, each.Outcome, each.Revision,
            At = each.RecordedAt, each.Resolution, each.ResolvedAt, Said = HeldAttempts.WhySaid(each), each.Words, each.Code, each.Verification,
        }),
    ];

    /// <summary>
    /// The node records that do not read as the wire carries them: the file's name, the unit it names where it still
    /// names one, when it is last written, and the driver's sentence; the page words it in the reader's language.
    /// </summary>
    public static object[] UnreadableAnswer(IEnumerable<UnreadableRecord> unreadable) =>
        [.. unreadable.Select(each => new { each.File, each.Unit, each.At, Said = HeldAttempts.UnreadableSaid(each) })];

    /// <summary>
    /// The closure graphs' ends as the wire carries them: <c>kind</c> <c>overridden</c> with the <c>nodes</c> it stopped,
    /// or <c>partially-closed</c> with the <c>quest</c> lost, its close <c>node</c> and <c>attempt</c>, and <c>words</c> (<c>stop</c>, said
    /// through <c>WORK_HELD</c>) while that node is live again. A page before the field reads without it. Additive: kind
    /// <c>word-taken-back</c>, the person's <c>word</c> on a close no longer listed, taken back since the <c>quest</c>'s submission moved; a
    /// page before it says nothing of that kind.
    /// </summary>
    public static object[] EndsAnswer(IEnumerable<GraphEnd> ends) =>
    [
        .. ends.Select(each => new { each.Unit, each.Kind, each.At, each.Nodes, each.Quest, each.Node, each.Attempt, each.NodeState, each.Resolution, each.Words, each.Word }),
    ];
}
