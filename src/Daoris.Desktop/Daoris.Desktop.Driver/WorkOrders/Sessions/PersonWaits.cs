namespace Daoris.Driver;

/// <summary>A step of a run that waits on the person: only their press, or their word on a held attempt, moves it.</summary>
/// <param name="Run">The run, as its binding names it: its chain's first quest in its repository.</param>
/// <param name="Node">The step's id in the run's graph.</param>
/// <param name="Why">What it waits for, one of <see cref="PersonWaits"/>' words.</param>
/// <param name="Repository">The run's repository.</param>
/// <param name="Since">When the step came to stand where it does; null on a record written before it carried a clock.</param>
public sealed record PersonWait(string Run, string Node, string Why, string Repository, DateTimeOffset? Since)
{
    /// <summary>The role it waits on (<see cref="StepWaiting.Read"/>): a plugin's actor as its plugin names it, else its wait's role.</summary>
    public string Role { get; init; } = "";

    /// <summary>Whether its run ended failed at it (<see cref="FailedHeldRuns"/>): its own <c>step</c> row carries it, whatever its quest's state.</summary>
    public bool Ended { get; init; }

    public bool Vacant { get; init; }
}

/// <summary>
/// What a run step waits on the person for: a review's Accept, a person's task or approval, a go-ahead, an attempt held unknown for
/// them, a plugin step's role they hold, a second opinion nobody is free to give, and an unchecked done's pass. Workflows reads the steps
/// (<see cref="PersonWaitReader"/>); Sessions' groups and *What needs you* place the session each run stands for by them.
/// </summary>
public static class PersonWaits
{
    /// <summary>A <c>review</c> waiting for the person's Accept.</summary>
    public const string Accept = "accept";

    /// <summary>A <c>task</c> approval waiting for its holder's Approve or Reject.</summary>
    public const string Approval = "approval";

    /// <summary>A <c>task</c> waiting for its holder's Done or a decision's answer.</summary>
    public const string Task = "task";

    /// <summary>A <c>go-ahead</c> waiting for their yes.</summary>
    public const string GoAhead = "go-ahead";

    /// <summary>A step whose attempt is held unknown for the person's word.</summary>
    public const string Held = "held";

    /// <summary>A <c>return</c> holding files changed on both sides for the person's choice of each.</summary>
    public const string Choose = "choose";

    /// <summary>A plugin step waiting on a role the person holds (<see cref="StepWaiting"/>): its act is taken on its plugin's page.</summary>
    public const string Actor = "actor";

    /// <summary>A step failed and kept for the person after its quest's done, or the step a run ended failed at whatever its quest's state: what follows it never ran.</summary>
    public const string Left = "left";

    /// <summary>An <c>opinion</c> its gate holds for want of a reviewer: the vacancy goes up to the line's lead (<see cref="OpinionVacancy"/>).</summary>
    public const string Vacant = "vacant";

    /// <summary>A <c>quest.holds</c> holding a done nothing checked (<c>UncheckedDones</c>) for the person's pass with their words.</summary>
    public const string Pass = "pass";

    /// <summary>Every wait a step can stand at: what the census of <i>What needs you</i> holds a row with a press for, each.</summary>
    public static IReadOnlyList<string> All { get; } = [Accept, Approval, Task, GoAhead, Held, Choose, Actor, Left, Vacant, Pass];
}
