namespace Daoris.Driver;

/// <summary>
/// The risk judge asked again where a hold would release: a raise to the owner moves the waiting step to the owner role, and
/// a lowering hands it back to the holder the place casts.
/// </summary>
internal static class TenureRejudge
{
    /// <summary>The tenures handed over for the standing attempts that hold one; none where each is already held as the judge reads it now.</summary>
    public static IReadOnlyList<StepTenure> Apply(
        StepTenureFiles tenures, string unit, IEnumerable<(NodeDeclaration Node, NodeStatus Status)> standing, IReadOnlyDictionary<string, StepTenure> held,
        Func<NodeDeclaration, CastNow> castOf, RiskStepJudge? judge, DateTimeOffset now)
    {
        var moved = new List<StepTenure>();
        if (judge is null) return moved;
        foreach (var (node, status) in standing)
        {
            if (!held.TryGetValue(node.Id, out var current) || !IsGate(node, status)) continue;
            var cast = castOf(node);
            var owned = RiskStepGates.Owned(node, status, judge);
            var marked = current.Why == TenureWhy.Marked;
            if (owned == marked || (!owned && cast.Why == CastWhy.Marked)) continue;
            var handed = owned
                ? tenures.HandOver(unit, node.Id, status.Attempt, current.Epoch, CastHolders.Person, TenureWhy.Marked, now)
                : tenures.HandOver(unit, node.Id, status.Attempt, current.Epoch, AttemptTenures.FirstHolder(node, cast), AttemptTenures.WhyOf(cast), now);
            if (handed.Tenure is { } next && next.Epoch != current.Epoch) moved.Add(next);
        }

        return moved;
    }

    private static bool IsGate(NodeDeclaration node, NodeStatus status) =>
        status.State == NodeStates.Active && NodeKit.Of(node.Kind) is IWaitsOnRole waits && waits.Waits(node, status) is { } why && RiskStepHolds.KindOf(why, node) is not null;
}
