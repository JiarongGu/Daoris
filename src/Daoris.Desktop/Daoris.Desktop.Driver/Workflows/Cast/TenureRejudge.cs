namespace Daoris.Driver;

/// <summary>
/// The risk judge asked again where a hold would release: a raise to the owner moves the waiting step to the owner role, and
/// a lowering hands it back to the holder the place casts; each move writes its own <c>risk.judged</c> line at release.
/// </summary>
internal static class TenureRejudge
{
    public const string Raised = "raised to the owner by the risk policy: the step moves to the owner role";

    public const string Lowered = "lowered by the risk policy: the step goes back to the holder the place casts";

    /// <summary>The tenures handed over for the standing attempts that hold one; none where each is already held as the judge reads it now.</summary>
    public static IReadOnlyList<StepTenure> Apply(
        StepTenureFiles tenures, string unit, IEnumerable<(NodeDeclaration Node, NodeStatus Status)> standing, IReadOnlyDictionary<string, StepTenure> held,
        Func<NodeDeclaration, CastNow> castOf, RiskStepJudge? judge, DateTimeOffset now, string? home = null)
    {
        var moved = new List<StepTenure>();
        if (judge is null) return moved;
        foreach (var (node, status) in standing)
        {
            if (!held.TryGetValue(node.Id, out var current) || GateOf(node, status) is not { } why) continue;
            var cast = castOf(node);
            var owned = RiskStepGates.Owned(node, status, judge);
            var marked = current.Why == TenureWhy.Marked;
            if (owned == marked || (!owned && cast.Why == CastWhy.Marked)) continue;
            var handed = owned
                ? tenures.HandOver(unit, node.Id, status.Attempt, current.Epoch, CastHolders.Person, TenureWhy.Marked, now)
                : tenures.HandOver(unit, node.Id, status.Attempt, current.Epoch, AttemptTenures.FirstHolder(node, cast), AttemptTenures.WhyOf(cast), now);
            if (handed.Tenure is not { } next || next.Epoch == current.Epoch) continue;
            moved.Add(next);
            if (home is not null) Journal(home, unit, node, status, why, next, owned, judge, now);
        }

        return moved;
    }

    private static string? GateOf(NodeDeclaration node, NodeStatus status) =>
        status.State == NodeStates.Active && NodeKit.Of(node.Kind) is IWaitsOnRole waits && waits.Waits(node, status) is { } why && RiskStepHolds.KindOf(why, node) is not null ? why : null;

    private static void Journal(string home, string unit, NodeDeclaration node, NodeStatus status, string why, StepTenure next, bool raised, RiskStepJudge judge, DateTimeOffset now)
    {
        var verdict = RiskStepGates.Verdict(node, why, judge) ?? judge.Act(RiskAct.Of(RiskStepHolds.KindOf(why, node)!));
        var run = RunUnits.Is(unit) ? unit[RunUnits.Prefix.Length..] : unit;
        var place = judge.Place;
        // The hold's key with the epoch it moved to, so the shadow's own open and release of the hold are never read as this line's.
        var key = $"{RiskStepHolds.Domain}:{run}/{node.Id}#{status.Attempt}:{why}@epoch{next.Epoch}";
        try
        {
            RiskJournal.Append(home, new RiskJudged(now, RiskJudged.Released, key, why, verdict.Kind, verdict.Qualifier, next.Holder, place?.Workspace, place?.Repository,
                place?.Mode ?? RiskPlace.RestrictedMode, run, node.Id, $"run {run}, step {node.Id}, attempt {status.Attempt}", verdict.Outcome, verdict.Class, verdict.By,
                verdict.Source, verdict.Revision, verdict.Reason, raised ? Raised : Lowered, verdict.Outcome != RiskVerdict.Pass));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The line records the move, not the move: the tenure is handed over whether or not the journal takes it.
        }
    }
}
