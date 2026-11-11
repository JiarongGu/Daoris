namespace Daoris.Driver;

/// <summary>
/// The person's changes on a branch's review answered by a new handback at the same commit: the review's binding is the branch at its tip,
/// which a corrected answer leaves where it was, so the review is asked again on its new record, as the person's send-back asked. Work in a
/// folder, a copy, a checkout or a system needs none of this: its binding is the record's revision, which a new handback moves.
/// </summary>
public static class ChangesAnswered
{
    /// <summary>Each review of <paramref name="governed"/> rejected on an attempt whose record the work's new handback has moved from, asked again.</summary>
    public static void Restart(NodeRecordFiles records, GovernedUnit governed, RunScope scope, string? tip, DateTimeOffset now, List<string> said)
    {
        var unit = governed.Unit.Id;
        if (!ReviewWork.OnBranch(scope) || scope.Branch is not { } branch || tip is null || records.Read(unit) is not { } record) return;
        var standing = RunMoves.Revision(record.Moves, branch.Id, tip);
        foreach (var node in governed.Declared.Values.Where(node => node.Kind == NodeKinds.Review))
        {
            if (WorkPerformer.StatusOf(record, node.Id) is not { State: NodeStates.Rejected } status) continue;
            if (ReviewRecords.Read(records.Home, scope.Run.Binding.Run, node.Id, status.Attempt) is not { } read
                || !string.Equals(read.Reference.Tip, standing, StringComparison.OrdinalIgnoreCase)
                || ReviewWork.Read(records.Home, scope, standing).Revision == read.Revision) continue;

            var (refused, moved) = records.Edit(unit, current =>
            {
                var again = NodeLifecycle.Apply(node, WorkPerformer.StatusOf(current, node.Id), new NodeMove(NodeStates.Ready, NodeCause.Person));
                return again.Moved ? (WorkPerformer.Kept(current, again, "person-changes-answered", now), true) : (null, false);
            });
            if (refused is null && moved) said.Add($"work  {unit}: its work answered your changes at `{node.Id}` with a new handback, so the review asks again on it.");
        }
    }
}
