namespace Daoris.Driver;

/// <summary>
/// ACCESSSTEP1's driver half: a plugin's wait on the credentials keeper (凭据管理人, <see cref="StepRoles.CredentialsKeeper"/>) that reaches
/// its deadline climbs the reporting line (<see cref="ReportingLine.Climb"/>) instead of ending as <c>timeout</c>. Sign-ins and credentials
/// are an owner-class kind (<see cref="OwnerClass.SignIn"/>), so the climb passes every level below the owner role whoever holds it, and the
/// attempt is held there with its plugin's page: the step stays under way, its gate held, until its holder's word (a new attempt once the key
/// is put right, or done outside Daoris).
/// </summary>
public static class AccessWaits
{
    /// <summary>The held attempt's code: its wait for access climbed to the owner role at its deadline.</summary>
    public const string Climbed = "access-climbed";

    /// <summary>The code a plugin's wait ends with at its deadline.</summary>
    public const string Timeout = "timeout";

    /// <summary>Whether <paramref name="outcome"/> ends a wait on the credentials keeper at its deadline.</summary>
    public static bool Is(NodeAttempt attempt, WorkOutcome outcome) =>
        outcome is { State: WorkStates.Failed, Code: Timeout } && attempt.Progress?.Actor == StepRoles.CredentialsKeeper;

    /// <summary>Where help for access waits: an owner-class kind, past every level to the owner role.</summary>
    public static LineWait Line() => ReportingLine.Of([], OwnerClass.SignIn, new RolePlace()).Climb(ownerClass: true);

    /// <summary>The attempt held at the owner role, the climb said before the plugin's words and its page kept.</summary>
    public static NodeAttempt Held(NodeAttempt attempt, string outcome, int generation, DateTimeOffset at) => attempt with
    {
        Outcome = outcome, Generation = generation,
        Progress = new StepProgress(StepRoles.Owner, $"{Line().Said()}. {attempt.Progress?.Words}".Trim(), attempt.Progress?.Url, null, at),
    };
}
