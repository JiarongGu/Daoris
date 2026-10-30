namespace Daoris.Driver;

/// <summary>
/// What an executor holds of a live run (the design §2.5, §4.1): open or resume, apply a refused choice's later one, take words, steer,
/// stop a turn or a task, and close, and beside those the five members every executor uses today (AGENTPROVIDER1m-i): the records handed
/// at the open, the carriers, the run's end, the go-on and the restart. Each operation is admitted by the run's own evidence (§2.4) and
/// one it lacks is answered <see cref="Unsupported"/> naming the ability, before anything is written. Row b declares it; row m-i makes the
/// executors hold it over <see cref="TransportRun"/>, and m-ii and the rows after it add the parameters the design names there
/// (<c>DoorInput</c>, <c>IChoiceReask</c>, <c>IResumeState</c>) that no type holds yet.
/// </summary>
/// <remarks>
/// The words' operations (<see cref="SendAsync"/>, <see cref="SteerAsync"/>) carry no id, while today's retry marks a word attempted
/// before the wire and takes it off by its id once written, which is <c>DoorInput</c>'s (m-ii): no executor calls them before then, and
/// the words' paths stay beneath the run, on the session and process it holds today.
/// </remarks>
public abstract class AgentRun
{
    /// <summary>The answer of an operation the run's evidence does not admit, naming the ability.</summary>
    public static DriverException Unsupported(string ability) =>
        new($"this door does not support {ability} — nothing was written.");

    public abstract Task OpenAsync(CancellationToken ct);

    public abstract Task ResumeAsync(string conversation, CancellationToken ct);

    /// <summary>A refused model's later choice, carried on the same session (O4-F5).</summary>
    public abstract Task ApplyChoiceAsync(ActorAsked choice, CancellationToken ct);

    public abstract Task SendAsync(string text, CancellationToken ct);

    public abstract Task SteerAsync(string text, CancellationToken ct);

    public abstract Task StopTurnAsync(CancellationToken ct);

    public abstract Task StopTaskAsync(string task, CancellationToken ct);

    public abstract Task CloseAsync(CancellationToken ct);

    /// <summary>
    /// The records the run is handed at its open, as today's capture is handed them (design §4.1): the transcript, the session's events,
    /// the lineage hook, the streams beside, the conversation named and the window readings. Handed before <see cref="OpenAsync"/> or
    /// <see cref="ResumeAsync"/>. <b>Replaced at row l</b> by the sink, which the run writes its readings to itself.
    /// </summary>
    public abstract void HandRecords(RunRecords records);

    /// <summary>
    /// The carriers handed to the start and to each go-on (design §4.1): the settings and the servers files the run before it was handed, so
    /// each go-on runs under the same rules. Kept: the downloads a start hands are kept by the run's open, as today's start's are.
    /// </summary>
    public abstract void HandCarriers(RunCarriers carriers);

    /// <summary>
    /// The run's end as the conclusion reads it today (design §4.1): its exit, its usage at the high-water mark, the
    /// <see cref="HarnessEnding"/> and whether the conversation was named. Waits for the process's exit, then for the capture, bounded
    /// past the exit (SESSWATCH1a). Kept.
    /// </summary>
    public abstract Task<AgentRunEnd> EndAsync(CancellationToken ct);

    /// <summary>
    /// A native run's next process with the held words as one <paramref name="prompt"/>, resuming <paramref name="conversation"/> under the
    /// carriers (MSG1b, D137 §2.1), its opening the events said first; the run's end is the one it answers. <b>Replaced at m-ii</b> by the
    /// native retry and <c>DoorInput</c>; today's <c>GoOnWith</c> factory it stands in place of.
    /// </summary>
    public abstract Task<AgentRunEnd> GoOnAsync(string conversation, string prompt, IReadOnlyList<SessionEvent> opening, CancellationToken ct);

    /// <summary>
    /// The same start once more, its transcript appended and its opening not said again (SESSCTX1c, design §4.1): the native resume's one
    /// retry, whose rule stays the executor's, reading the end's named and exit. <b>Replaced at m-ii</b> with the retry it serves.
    /// </summary>
    public abstract Task<AgentRunEnd> RestartAsync(CancellationToken ct);
}

/// <summary>
/// How a run ended, as the conclusion reads it (design §4.1): the process's exit (null where a bound killed it), its usage at the context
/// high-water mark (null where it reported none), what the harness itself said of the end, and whether its conversation was named, which
/// the native resume's one retry reads.
/// </summary>
public sealed record AgentRunEnd(int? Exit, AcpUsage? Usage, HarnessEnding Ending, bool Named);

/// <summary>
/// The records a run is handed at its open (design §4.1), until row l replaces them with the sink: where its transcript is, the events
/// and the console its lines reach, the home whose lineage file hears each subagent, the adapter its conversation is kept under, the
/// harness's own words, what the stream says of the account's windows, and what the transcript opens with.
/// </summary>
/// <param name="Output">The live console a run's lines reach, and the streams beside; null where nobody watches.</param>
/// <param name="Events">The session's record, which each event is kept on; null keeps none.</param>
/// <param name="LineageHome">The home whose lineage file is told each subagent's start and end (FABRIC-C1a); null tells none.</param>
/// <param name="KeepAs">The adapter a quest's session keeps its harness's conversation id under (ANSWER1a); null keeps none.</param>
/// <param name="Own">Where the harness's own words are kept for its ending (AGT3c); null keeps none.</param>
/// <param name="Said">Told what the stream says of the account's windows (TOOL6c); null tells none.</param>
/// <param name="Opening">The events the record opens with, said once: a start's target or a resume's answer, never said again by a restart.</param>
/// <param name="Preamble">A fact about how the run was set up, written ahead of anything the process says; null says none.</param>
public sealed record RunRecords(
    string SessionId, string Transcript, ISessionOutput? Output = null, ISessionEvents? Events = null, string? LineageHome = null,
    string? KeepAs = null, HarnessWords? Own = null, Action<System.Text.Json.JsonElement>? Said = null,
    IReadOnlyList<SessionEvent>? Opening = null, string? Preamble = null);

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
