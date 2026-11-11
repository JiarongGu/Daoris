namespace Daoris.Driver;

/// <summary>
/// The node's side of a landing supervisor's release, over the run's own records: the step still waiting on the decision's attempt, the
/// review record that attempt reads still the one the decision names (<see cref="SupervisorReviewBinding"/>), and the person's holds on its
/// line; then the word kept on the run's record as the supervisor's, with its attempt and revision, for the next pass to apply.
/// </summary>
internal static class LandingReleaseLine
{
    public static LandingRelease Of(string home, NodeRecordFiles records, Func<SupervisorLandingDue, bool> ownerHeld) => new(
        due => Task.FromResult(Why(home, records, due, ownerHeld)),
        due => RunNodeWords.Word(
            records, due.Run, due.Node, due.Do,
            due.Do == SupervisorLandingActs.Reject ? due.Words : null, by: new NodeWordBy(due.Generation, due.Id), attempt: due.Attempt, revision: due.Revision).Refused);

    private static string? Why(string home, NodeRecordFiles records, SupervisorLandingDue due, Func<SupervisorLandingDue, bool> ownerHeld)
    {
        var status = WorkPerformer.StatusOf(records.Read(RunUnits.IdOf(due.Run)) ?? new NodeRecord(RunUnits.IdOf(due.Run)), due.Node);
        if (status.State != NodeStates.Active || status.Attempt != due.Attempt) return SupervisorWhy.Answered;
        return SupervisorReviewBinding.Refused(home, due.Run, due.Node, due.Attempt, due.Revision) ?? (ownerHeld(due) ? SupervisorWhy.OwnerHeld : null);
    }
}
