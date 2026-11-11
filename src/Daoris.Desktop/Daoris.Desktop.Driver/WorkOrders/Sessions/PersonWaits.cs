namespace Daoris.Driver;

/// <summary>A step of a run that waits on the person: only their press, or their word on a held attempt, moves it.</summary>
/// <param name="Run">The run, as its binding names it: its chain's first quest in its repository.</param>
/// <param name="Node">The step's id in the run's graph.</param>
/// <param name="Why">What it waits for, one of <see cref="PersonWaits"/>' words.</param>
/// <param name="Repository">The run's repository.</param>
/// <param name="Since">When the step came to stand where it does; null on a record written before it carried a clock.</param>
public sealed record PersonWait(string Run, string Node, string Why, string Repository, DateTimeOffset? Since);

/// <summary>
/// What a run step waits on the person for: a review's Accept, a person's task or approval, a go-ahead, and an attempt held unknown for
/// them. Workflows reads the steps (<see cref="PersonWaitReader"/>); Sessions' groups and *What needs you* place the session each run
/// stands for by them.
/// </summary>
public static class PersonWaits
{
    /// <summary>A <c>review</c> waiting for the person's Accept.</summary>
    public const string Accept = "accept";

    /// <summary>A <c>human</c> approval waiting for their Approve or Reject.</summary>
    public const string Approval = "approval";

    /// <summary>A <c>human</c> task waiting for their Done.</summary>
    public const string Task = "task";

    /// <summary>A <c>go-ahead</c> waiting for their yes.</summary>
    public const string GoAhead = "go-ahead";

    /// <summary>A step whose attempt is held unknown for the person's word.</summary>
    public const string Held = "held";

    /// <summary>A <c>return</c> holding files changed on both sides for the person's choice of each.</summary>
    public const string Choose = "choose";

    /// <summary>
    /// A run that ended failed at this step after its quest's done, so the steps after it never ran: unfinished work, which asks the
    /// person for its next step and is set aside by their archive of its session. Never read from a run still going.
    /// </summary>
    public const string Left = "left";
}
