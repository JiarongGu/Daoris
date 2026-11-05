namespace Daoris.Driver;

/// <summary>
/// Who requests a place's integration and where its conflict goes, read outside a run (the integration design §17.7): the <c>integrate</c> step
/// of the newest version of the workflow the place's work follows by default. A line's policy says how the line is integrated and holds
/// neither (<see cref="LandingKey.PolicyProblem"/>), so the doors that ask with no run behind them (the come-up sweep, the steward's act and
/// the authority read again at promotion) read them through <see cref="IWorkplaceWorkflows.IntegrateOf"/>. A place that follows no workflow,
/// whose newest version does not read, or whose workflow has no <c>integrate</c> step names no requester, and reads the <i>Absent</i> column:
/// the owner requests, and a conflict goes to the member's session.
/// </summary>
/// <param name="Request">One of <see cref="LandingIntegrate.Requests"/>.</param>
/// <param name="Conflicts">One of <see cref="LandingIntegrate.ConflictChoices"/>.</param>
/// <param name="Workflow">The workflow whose step said them; null where none did.</param>
public sealed record PlaceIntegrate(string Request, string Conflicts, string? Workflow)
{
    /// <summary>No step names a requester: the <i>Absent</i> column's.</summary>
    public static PlaceIntegrate None { get; } = new(LandingIntegrate.Owner, LandingIntegrate.Session, null);

    /// <summary>The line's policy with every absent key resolved, its requester and conflict route this step's.</summary>
    public LandingIntegrate Over(LandingIntegrate policy) => policy.Resolved() with { Request = Request, Conflicts = Conflicts };
}
