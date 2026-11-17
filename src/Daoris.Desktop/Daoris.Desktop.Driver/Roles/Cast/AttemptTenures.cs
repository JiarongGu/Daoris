namespace Daoris.Driver;

/// <summary>
/// The tenures' producer at an attempt's start: each attempt standing on a run's record (under way, or held) that holds no tenure yet is held by
/// the holder the place casts its step's role to now (<see cref="PlaceCastBook.At"/>), its first epoch, since the attempt's own start. Asked once
/// a pass for each run the pass governed, after its rounds; a pass whose attempts all hold a tenure reads one file and writes nothing.
/// </summary>
public static class AttemptTenures
{
    /// <summary>The tenures begun for <paramref name="unit"/>'s standing attempts; none where every one holds one or its record does not read.</summary>
    public static IReadOnlyList<StepTenure> Keep(
        NodeRecordFiles records, string unit, IEnumerable<NodeDeclaration> nodes, string workspace, string? repository, PlaceRiskBook? risk, DateTimeOffset now)
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
        if (unheld.Count == 0) return [];

        var book = new PlaceCastFiles(records.Home).Book();
        var place = (risk ?? PlaceRiskBook.Unwritten).At(workspace, repository);
        return tenures.Begin(unit, [.. unheld.Select(each =>
        {
            var role = StepRoles.Of(each.Node);
            return (each.Node.Id, each.Status.Attempt, role, book.At(role, workspace, repository, place).Holder, each.Status.Since ?? now);
        })]);
    }
}
