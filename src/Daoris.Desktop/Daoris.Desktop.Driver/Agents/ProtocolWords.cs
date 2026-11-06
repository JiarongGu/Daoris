namespace Daoris.Driver;

/// <summary>
/// A protocol door's words as its run carries them, the port beside the native door's <see cref="DoorInput"/> (D246): the one place a
/// conversation's prompt, its word said into a running turn and its steer reach the session's wire, held by <see cref="AgentRun.Words"/>.
/// The conversation's turns (<c>ProtocolChat</c>) say words here and never write through the session. Each is a request the session answers,
/// so a word's acknowledgement is its answer and no process is restarted to replay it: the door's retry is the transient episode's, on the
/// same session.
/// </summary>
internal sealed class ProtocolWords(AcpSession session)
{
    /// <summary>Whether the agent said it takes a prompt during a turn: a word said then goes at once.</summary>
    public bool NextStep => session.NextStep;

    /// <summary>Whether the person's *Send now* steers into the running turn.</summary>
    public bool Steers => session.Steers;

    /// <summary>Whether the session waits between tries of a transient failure.</summary>
    public bool WaitsOnTransient => session.WaitsOnTransient;

    /// <summary>The transient retry and the words its episode holds, handed once the session is open.</summary>
    public void Retries(TransientRetry retry, IEpisodeWords words) => session.Retries(retry, words);

    /// <summary>One turn of text, its files each a link after it; answered with the turn's stop reason.</summary>
    public Task<string> PromptAsync(string text, Action? sent, IReadOnlyList<KeptFile>? files) =>
        session.PromptAsync(text, CancellationToken.None, sent, files);

    /// <summary>One turn of one or more blocks, a conversation resumed with the person's words waiting.</summary>
    public Task<string> PromptAsync(IReadOnlyList<string> blocks, Action? sent, IReadOnlyList<KeptFile>? files) =>
        session.PromptAsync(blocks, CancellationToken.None, sent, files);

    /// <summary>A turn's prompt tried again while its failure is transient, inside the one turn.</summary>
    public Task<string> RetriedAsync(Func<Task<string>> prompt, CancellationToken cut) => session.RetriedAsync(prompt, cut);

    /// <summary>A word that was taken and whose own prompt then failed, recovered as the turn's prompt failing.</summary>
    public Task<string> RecoveredAsync(AcpRefusal refusal, CancellationToken cut) => session.RecoveredAsync(refusal, cut);

    /// <summary>A word said into the running turn, handed on at once; answered with the turn's stop reason at its real end.</summary>
    public Task<string> SayAsync(IAgentWord words) => session.SayAsync(words, CancellationToken.None);

    /// <summary>The person's *Send now*, steered into the running turn; never thrown.</summary>
    public Task<string> SteeredAsync(IAgentWord words, Func<IAgentWord, bool> handBack) =>
        session.SteeredAsync(words, CancellationToken.None, "conversation", handBack);

    /// <summary>Told each of the person's words as the session takes them.</summary>
    public void OnTaken(Action<IAgentWord> taken) => session.OnTaken(taken);
}
