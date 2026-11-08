namespace Daoris.Driver;

/// <summary>Reads a run's node record for the start's sweep: whether a landing supervisor's word stands applied there.</summary>
public static class LandingApplied
{
    /// <summary>The step stands reached or rejected by the supervisor on the attempt the decision names.</summary>
    public static Func<SupervisorLandingView, bool> Of(string home) => view =>
        new NodeRecordFiles(home, () => false).Read(RunUnits.IdOf(view.Run)) is { } record
        && record.Statuses.GetValueOrDefault(view.Node) is { By: NodeSource.Supervisor, Attempt: var attempt } && attempt == view.Attempt;
}
