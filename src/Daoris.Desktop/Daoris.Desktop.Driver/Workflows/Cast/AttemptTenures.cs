namespace Daoris.Driver;

/// <summary>
/// The tenures' producer at an attempt's start: each attempt standing on a run's record (under way, or held) that holds no tenure yet is held by
/// the holder the place casts its step's role to now (<see cref="PlaceCastBook.At"/>), its first epoch, since the attempt's own start. Asked
/// twice a pass for each run the pass governed, before its rounds (the attempts standing from before it) and after them (those it started); a
/// pass whose attempts all hold a tenure reads one file and writes nothing.
/// </summary>
public static class AttemptTenures
{
    /// <summary>The tenures begun for <paramref name="unit"/>'s standing attempts; none where every one holds one or its record does not read.</summary>
    public static IReadOnlyList<StepTenure> Keep(
        NodeRecordFiles records, string unit, IEnumerable<NodeDeclaration> nodes, string workspace, string? repository, PlaceRiskBook? risk, DateTimeOffset now,
        RiskStepJudge? judge = null)
    {
        if (records.Read(unit) is not { Ended: null } record) return [];
        var standing = new List<(NodeDeclaration Node, NodeStatus Status)>();
        foreach (var node in nodes)
        {
            var status = WorkPerformer.StatusOf(record, node.Id);
            if (status.State is NodeStates.Active or NodeStates.Unknown && status.Attempt >= 1) standing.Add((node, status));
        }

        if (standing.Count == 0) return [];
        var tenures = new StepTenureFiles(records.Home, records.HoldsLease);
        var held = tenures.Holding(unit, standing.ToDictionary(each => each.Node.Id, each => each.Status.Attempt, StringComparer.Ordinal));
        var unheld = standing.Where(each => !held.ContainsKey(each.Node.Id)).ToList();
        var book = new PlaceCastFiles(records.Home).Book();
        var place = (risk ?? PlaceRiskBook.Unwritten).At(workspace, repository);
        var judged = TenureRejudge.Apply(tenures, unit, standing.Where(each => held.ContainsKey(each.Node.Id)), held,
            node => book.At(StepRoles.Of(node), workspace, repository, place), judge, now, records.Home);
        if (unheld.Count == 0) return judged;
        return [.. judged, .. tenures.Begin(unit, [.. unheld.Select(each =>
        {
            var role = StepRoles.Of(each.Node);
            // A hold the risk policy classes the owner's is the owner role's from its start, as a marked approver is.
            var cast = RiskStepGates.Owned(each.Node, each.Status, judge)
                ? new CastNow(role, CastHolders.Person, CastWhy.Marked, RiskPolicy.Key)
                : book.At(role, workspace, repository, place);
            return (each.Node.Id, each.Status.Attempt, role, FirstHolder(each.Node, cast), each.Status.Since ?? now, WhyOf(cast));
        })])];
    }

    public static IReadOnlyList<StepTenure> KeepAt(
        NodeRecordFiles records, string unit, IEnumerable<NodeDeclaration> nodes, string workspace, string? repository, DriverConfig? config, DateTimeOffset now) =>
        Keep(records, unit, nodes, workspace, repository, config?.PlaceRisk, now, RiskStepGates.At(config, workspace, repository));

    /// <summary>
    /// Who first holds an attempt of <paramref name="node"/>, and whom <i>Give back</i> returns it to: the person where the step's own word says
    /// <c>you</c> (<see cref="HolderWords.First"/>), else the holder the place casts its role to now. Every first epoch is cast by this one rule.
    /// </summary>
    public static string FirstHolder(NodeDeclaration node, CastNow cast) => HolderWords.First(node) ?? cast.Holder;

    /// <summary>Why an attempt's first holder holds it: the owner's mark where the place keeps the role the owner's (<see cref="SettleDoor.Marked"/>), else the cast.</summary>
    public static string WhyOf(CastNow cast) => cast.Why == CastWhy.Marked ? TenureWhy.Marked : TenureWhy.Cast;
}
