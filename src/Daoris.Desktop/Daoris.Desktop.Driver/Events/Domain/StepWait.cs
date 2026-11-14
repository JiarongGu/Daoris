namespace Daoris.Driver;

/// <summary>
/// What a session whose agent's turn ended waits on (<c>awaiting-step</c>): its workflow's run, and where known the node, its attempt and
/// the tip it handed; or the check its agent asked the broker for. It runs nothing meanwhile, and the step's word wakes it.
/// </summary>
/// <param name="Run">The graph run its quest's chain is bound to, or null for a check it asked.</param>
/// <param name="Node">The step it waits on, where one is named.</param>
/// <param name="Attempt">That step's attempt, where one is named.</param>
/// <param name="Tip">The commit its turn handed, where read.</param>
public sealed record StepWait(string? Run, string? Node = null, int? Attempt = null, string? Tip = null)
{
    /// <summary>The node a session waiting on its own broker check names: the check is its step, keyed by the ask.</summary>
    public const string CheckRun = "check_run";

    /// <summary>A wait on the check its agent asked the broker for.</summary>
    public static StepWait Check { get; } = new(null, CheckRun);

    /// <summary>The state a waiting session's record is in.</summary>
    public const string State = "awaiting-step";

    /// <summary>Its note: what it waits on, in the coded line the page words.</summary>
    public Noted Note() => Run is null
        ? Noted.Of(EndNoteCodes.WaitsCheck,
            "It asked for a check and its turn ended while the check waits its turn: it goes on in the same conversation with the verdict.")
        : Noted.Of(EndNoteCodes.WaitsStep,
            $"Its turn ended and it waits on its workflow's next step (run `{Run}`): it goes on in the same conversation where a step sends its "
            + "work back, and ends with its workflow.",
            ("run", Run));
}
