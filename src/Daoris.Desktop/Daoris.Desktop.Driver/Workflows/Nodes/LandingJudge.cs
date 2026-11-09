namespace Daoris.Driver;

/// <summary>
/// What a landing step itself refuses a supervisor's act, in the order the keeping reads it: the step moved on from the attempt the decision
/// names, a go-ahead holds the run, the act is not one the step offers, and the person stopped or paused the line. A leave is refused for the
/// first two alone: it releases nothing.
/// </summary>
public static class LandingJudge
{
    /// <param name="step">The step as it waits now, or null where it no longer waits on that attempt.</param>
    /// <param name="act">The landing act, or <see cref="SupervisorLandingActs.Hold"/> for a leave.</param>
    /// <param name="ownerHeld">Whether the person stopped or paused the step's line (<see cref="OwnerHolds"/>).</param>
    public static string? Refused(LandingStep? step, string act, bool ownerHeld)
    {
        if (step is null) return SupervisorWhy.Answered;
        if (step.GoAheadOpen) return SupervisorWhy.GoAheadOpen;
        if (act == SupervisorLandingActs.Hold) return null;
        if (!step.Acts.Contains(act)) return SupervisorWhy.NotReferred;
        return ownerHeld ? SupervisorWhy.OwnerHeld : null;
    }
}
