namespace Daoris.Driver;

/// <summary>
/// What an executor holds of a live run (the design §2.5, §4.1): open or resume, apply a refused choice's later one, its words on the port
/// its transport has, stop a turn or a task, and close, and beside those the four members every executor uses today (AGENTPROVIDER1m-i): the
/// sink its readings go to, the carriers, the run's end and the go-on; a native resume's one retry is the run's own (m-ii,
/// <see cref="IResumeState"/>). Each operation is admitted by the run's own evidence (§2.4) and one it lacks is answered
/// <see cref="Unsupported"/> naming the ability, before anything is written.
/// </summary>
/// <remarks>
/// <b>Two named ports for the words, one per transport</b> (D246): a native door's lines are <see cref="Input"/>'s, each acknowledged once
/// the harness names its conversation and replayed to the next process where it never was; a protocol door's prompt, word into a running
/// turn and steer are <see cref="Words"/>', each a request its session answers, acknowledged by that answer and retried on the same session.
/// Each door's other port refuses or is absent.
/// </remarks>
public abstract class AgentRun
{
    /// <summary>The answer of an operation the run's evidence does not admit, naming the ability.</summary>
    public static DriverException Unsupported(string ability) =>
        new($"this door does not support {ability} — nothing was written.");

    public abstract Task OpenAsync(CancellationToken ct);

    /// <summary>
    /// Resume <paramref name="conversation"/>; on a native run that ended, its next process under the carriers with the words held on the
    /// input as one prompt (MSG1b, D137 §2.1), the run's end read again by <see cref="EndAsync"/>. A native resume handed its <paramref name="state"/> is tried once more by the run itself where
    /// its harness ended on a failed exit before it named the conversation (design §2.6, row m-ii): <see cref="EndAsync"/> answers that
    /// retry's end. Null hands none, and the run concludes on its first end.
    /// </summary>
    public abstract Task ResumeAsync(string conversation, CancellationToken ct, IResumeState? state = null);

    /// <summary>A refused model's later choice, carried on the same session (O4-F5).</summary>
    public abstract Task ApplyChoiceAsync(ActorAsked choice, CancellationToken ct);

    /// <summary>
    /// A native door's words (D243): the one way its process is handed a line, each word's id kept until the harness names its
    /// conversation, so the run's retry replays what that process never acknowledged; a go-on's words are held here and the next process
    /// opened with <see cref="ResumeAsync"/>. Nothing above the run writes to a process's stdin. On a protocol door it refuses every line.
    /// </summary>
    internal abstract DoorInput Input { get; }

    /// <summary>
    /// A protocol door's words once its session opened (D246): its prompt, a word into the running turn and the steer, each a request the
    /// session answers. Null before the open and on a native door.
    /// </summary>
    internal abstract ProtocolWords? Words { get; }

    public abstract Task StopTurnAsync(CancellationToken ct);

    public abstract Task StopTaskAsync(string task, CancellationToken ct);

    public abstract Task CloseAsync(CancellationToken ct);

    /// <summary>
    /// The sink the run writes its readings to (design §3.1, row l), composed by its executor of its owners' parts: the record's events, the
    /// console's lines and the streams beside, the conversation named, the account's windows and every failure, one <see cref="WireReading"/>
    /// each. Handed before <see cref="OpenAsync"/> or <see cref="ResumeAsync"/>.
    /// </summary>
    public abstract void HandSink(IReadingSink sink);

    /// <summary>
    /// The carriers handed to the start and to each go-on (design §4.1): the settings and the servers files the run before it was handed, so
    /// each go-on runs under the same rules. Kept: the downloads a start hands are kept by the run's spawn, as a start's are.
    /// </summary>
    public abstract void HandCarriers(RunCarriers carriers);

    /// <summary>
    /// The run's end as the conclusion reads it today (design §4.1): its exit, its usage at the high-water mark, the
    /// <see cref="HarnessEnding"/> and whether the conversation was named. Waits for the process's exit, then for the capture, bounded
    /// past the exit (SESSWATCH1a). Kept.
    /// </summary>
    public abstract Task<AgentRunEnd> EndAsync(CancellationToken ct);
}

/// <summary>
/// How a run ended, as the conclusion reads it (design §4.1): the process's exit (null where a bound killed it), its usage at the context
/// high-water mark (null where it reported none), what the harness itself said of the end, and whether its conversation was named, which
/// the native resume's one retry reads.
/// </summary>
public sealed record AgentRunEnd(int? Exit, AcpUsage? Usage, HarnessEnding Ending, bool Named);

/// <summary>
/// The carriers of a run (design §4.1): the settings file and the servers file its start was handed, handed again to each go-on so it runs
/// under the same rules. Null where the start was handed none.
/// </summary>
public sealed record RunCarriers(string? Settings = null, string? Servers = null);

/// <summary>
/// The name a protocol door's resume carries (the design §2.6, X): the transport's default recorded as eligibility to negotiate, which
/// planning reads and the open settles by the agent's own answer, never a resume known.
/// </summary>
public static class Eligibility
{
    public const string Applies = "eligibility, negotiated at the open";
}
