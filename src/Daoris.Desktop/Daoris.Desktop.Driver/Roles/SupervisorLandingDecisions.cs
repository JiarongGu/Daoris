namespace Daoris.Driver;

/// <summary>What a landing decision does with its step, as its file and the page name it: accepted a review, approved or rejected an approval, or left to the person.</summary>
public static class SupervisorLandingActs
{
    public const string Accept = "accept";
    public const string Approve = "approve";
    public const string Reject = "reject";
    public const string Hold = "hold";
}

/// <summary>What a holder decides on a landing step: an act its grants cover, or a leave as a <c>hold</c> with its bound.</summary>
public sealed record SupervisorLanding(
    string Session, string Run, string Node, int Attempt, string Generation, string Do, IReadOnlyList<SupervisorReason> Reasons, SupervisorHolder Holder,
    string? Bound = null)
{
    public SupervisorSubject Subject => SupervisorSubject.Landing(Session, Run, Node, Attempt);
}

/// <summary>A landing decision as memory keeps it: one per step and attempt, held for the window, released as the node's word, then applied or closed.</summary>
internal sealed class SupervisorLandingDecision
{
    public required string Id { get; init; }
    public required string Session { get; init; }
    public required string Run { get; init; }
    public required string Node { get; init; }
    public required int Attempt { get; init; }
    public required string Repository { get; init; }
    public required string Do { get; init; }
    public required string Generation { get; init; }
    public required string Words { get; init; }
    public required DateTimeOffset GoesOnAt { get; init; }
    public SupervisorState State { get; set; } = SupervisorState.Held;
    public DateTimeOffset StateAt { get; set; }
    public string? Why { get; set; }

    public SupervisorSubject Subject => SupervisorSubject.Landing(Session, Run, Node, Attempt);
}

/// <summary>A held landing decision whose moment came, as the release hands it to the node's door.</summary>
public sealed record SupervisorLandingDue(
    string Id, string Session, string Run, string Node, int Attempt, string Repository, string Do, string Generation, string Words);

/// <summary>
/// The node's side of a landing release: whether the step still waits for the decision's act and the person's holds on its line allow it
/// (null where it may go on, else why not), and the word kept on the run's record (null where kept, else the refusal).
/// </summary>
public sealed record LandingRelease(Func<SupervisorLandingDue, Task<string?>> Why, Func<SupervisorLandingDue, string?> Word);
