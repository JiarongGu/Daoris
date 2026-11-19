namespace Daoris.Driver;

/// <summary>
/// What the <c>opinion</c> step says while it stands on someone: with its findings handed to the work's session it waits on that
/// session's answer (<see cref="Answer"/>, naming it), the agent's wait and never <i>working</i>; and a pass whose reviewer ended without giving
/// an opinion, or whose minutes ran out first, fails it, saying so, until another pass reaches it.
/// </summary>
public static class OpinionStepWaits
{
    /// <summary>The opinion's declared wait: its findings are with the work's session, which answers them.</summary>
    public const string Answer = "answer";

    /// <summary>A failed pass's code (the gate's <see cref="OpinionGateState.Code"/>): its reviewer's session ended without giving one.</summary>
    public const string Ended = "ended";

    /// <summary>A failed pass's code: its reviewer's minutes ran out before it gave one.</summary>
    public const string OutOfTime = "out-of-time";

    /// <summary>The words a step a pass left with no opinion fails with; null for every other code, a wait for a reviewer among them.</summary>
    public static string? EndedSaid(string? code) => code switch
    {
        Ended => "Its reviewer's session ended without giving an opinion, so no agent's reading stands for this work: ask again, or pass it over.",
        OutOfTime => "Its reviewer's minutes ran out before it gave an opinion, so no agent's reading stands for this work: ask again, or pass it over.",
        _ => null,
    };

    /// <summary>
    /// The wait of an active <c>opinion</c> whose gate stands with the work's session (<see cref="OpinionGateStates.WithSession"/>): the
    /// session the findings went to, as the gate read it; null for any other state.
    /// </summary>
    public static WorkflowRunWait? WaitOf(NodeStatus status, OpinionGateState? gate)
    {
        if (status.State != NodeStates.Active || status.Holds != OpinionGateStates.WithSession) return null;
        var session = gate?.Opinion?.HandedTo ?? gate?.Opinion?.Working ?? gate?.Session;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (session is { Length: > 0 }) values["session"] = session;
        return new WorkflowRunWait(Answer, values, status.Since);
    }

    /// <summary>The terminal's words for the wait: on whose answer.</summary>
    public static string Said(WorkflowRunWait wait) =>
        wait.Values.GetValueOrDefault("session") is { Length: > 0 } session
            ? $"Waits on `{session}`'s answer to the second opinion."
            : "Waits on the work's session's answer to the second opinion.";
}
