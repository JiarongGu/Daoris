namespace Daoris.Driver;

/// <summary>
/// What an executor holds of a live run (the design §2.5, §4.1): open or resume, apply a refused choice's later one, take words, steer,
/// stop a turn or a task, and close, and beside those the four members every executor uses today (AGENTPROVIDER1m-i): the sink its readings
/// go to, the carriers, the run's end and the go-on; a native resume's one retry is the run's own (m-ii, <see cref="IResumeState"/>). Each
/// operation is admitted by the run's own evidence (§2.4) and one it lacks is answered <see cref="Unsupported"/> naming the ability, before
/// anything is written. Row b declares it; row m-i makes the executors hold it over <see cref="TransportRun"/>, and m-ii hands it the
/// words through <see cref="DoorInput"/>.
/// </summary>
/// <remarks>
/// A protocol door's words stay on its session (<see cref="SendAsync"/> and <see cref="SteerAsync"/> refuse them there) until its
/// own words move behind the input.
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
    /// A word through the run's <see cref="DoorInput"/> (D243): the one way a process is handed words, its id kept until the harness names
    /// its conversation, so the run's retry replays what that process never acknowledged. A go-on's words are held on the input and the
    /// next process opened with <see cref="ResumeAsync"/>.
    /// </summary>
    internal abstract Task<Delivery> SendAsync(DoorWord word, CancellationToken ct);

    /// <summary>The run's one queue of words (D243): nothing above the run writes to a process's stdin.</summary>
    internal abstract DoorInput Input { get; }

    public abstract Task SteerAsync(string text, CancellationToken ct);

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
