namespace Daoris.Driver;

/// <summary>
/// What a kind's subject makes of the one walk's attempt (<see cref="SupervisePass"/>): the prompt its chosen reader is handed, a turn's
/// end kept, and no choice ready said where the kind says it. A decision's is the decision door's (<see cref="SuperviseDecisionLife"/>);
/// a reading of Ask Daoris's acts keeps its own on its range's report.
/// </summary>
internal interface ISuperviseLife
{
    /// <summary>The prompt for <paramref name="choice"/>'s reader; null where the subject no longer waits.</summary>
    Task<string?> PromptAsync(SupervisorWaiting waiting, ReviewerChoice choice, CancellationToken ct);

    /// <summary>A turn that ended neither limited nor transient, kept or said; the attempt's outcome.</summary>
    Task<string> SettleAsync(SuperviseRunAsk ask, SuperviseRunEnd end, CancellationToken ct);

    /// <summary>No choice is ready: <paramref name="sentence"/> said once, until a choice is ready again.</summary>
    void Unavailable(SupervisorWaiting waiting, string sentence);
}
