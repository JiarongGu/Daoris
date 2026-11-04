namespace Daoris.Driver;

/// <summary>
/// Where a run's step stands (WORKFLOW1c; the workflow design §5.2), as a code a page words and a test holds. The design's table,
/// less what this build cannot reach yet: a service's wait comes with a check (WORKFLOW1k).
/// </summary>
public static class WorkflowRunStates
{
    /// <summary>The work has not come to it.</summary>
    public const string NotReached = "not-reached";

    /// <summary>Someone is at it and nobody needs to act yet: an agent, the set-up step, Daoris's own look.</summary>
    public const string Working = "working";

    /// <summary>It waits for the person, by the press its detail names.</summary>
    public const string WaitingOnYou = "waiting-on-you";

    /// <summary>It waits for an agent's answer: a set-up shown again, findings answered, a question another repository answers.</summary>
    public const string WaitingOnAgent = "waiting-on-agent";

    /// <summary>What it waits on could not be read, which is never taken as passed or failed (D148 point 5).</summary>
    public const string NotKnown = "not-known";

    /// <summary>It cannot go on as it stands: its detail says why, and the person's door fixes it.</summary>
    public const string CannotStart = "cannot-start";

    public const string Done = "done";

    /// <summary>Passed over for this work: by the person's skip, or a choice on the chain or the ask.</summary>
    public const string Skipped = "skipped";

    /// <summary>An outcome: a session failed, or a reviewer's pass ended without an opinion.</summary>
    public const string Failed = "failed";

    /// <summary>Ended without going on: the person's stop, a decline, a pull request abandoned, a tree discarded.</summary>
    public const string Stopped = "stopped";

    /// <summary>The states that let the run go past a step: what <see cref="WorkflowRun.At"/> passes over.</summary>
    public static bool Settled(string state) => state is Done or Skipped;
}
