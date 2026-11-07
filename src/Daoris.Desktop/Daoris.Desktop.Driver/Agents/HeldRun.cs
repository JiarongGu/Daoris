namespace Daoris.Driver;

/// <summary>
/// A run as <see cref="SessionHold"/> holds it (AGENTLYNTAI1d): the <see cref="AgentRun"/> and the executor's steps around it, its spawn made
/// ready before the open, the rest of its start handed later, whether it goes on with held words, and its letting go before its end. Today's
/// transports (<see cref="TransportRun"/>) and, where the driver carries Lyntai's agent layer, a bound door's driven kind each are one, so the
/// hold takes the same steps on either (<see cref="DrivenRuns"/>).
/// </summary>
internal abstract class HeldRun : AgentRun, IAsyncDisposable
{
    /// <summary>Whether a go-on resumes its conversation with held words: a native run handed what it goes on in.</summary>
    public abstract bool GoesOn { get; }

    /// <summary>The start the run holds, for an executor that completes it later (<see cref="HandStart"/>).</summary>
    internal abstract TransportStart Start { get; }

    /// <summary>The first process made ready, or up, before the open, so the executor's own steps come between (D205 row b, SESSWATCH1a).</summary>
    internal abstract Task SpawnAsync(string sessionId, CancellationToken ct);

    /// <summary>The start as its executor completed it before the open: the same process's, its session, prompt, wait and words.</summary>
    internal abstract void HandStart(TransportStart completed);

    /// <summary>The run let go before its end (ANSWER2): its process ended and its end written unclean.</summary>
    internal abstract Task AbandonAsync();

    public abstract ValueTask DisposeAsync();
}
