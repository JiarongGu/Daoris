using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// What a <see cref="TransportRun"/> starts and how it holds it: the process a door's spawn prepared, whose it is at the
/// start gate, and what the run needs beside it for its open, its wait, its go-on and its retry. Built by the executor that holds the run,
/// which is the one place that knows its target and its selection; an executor whose own steps come between the bracket or the spawn and the
/// open hands the rest before the open (<see cref="TransportRun.HandStart"/>).
/// </summary>
/// <param name="Info">The process to start, prepared and handed everything a start is handed.</param>
/// <param name="Whose">
/// Whose start it is at the start gate: the loop's first run is the background's, one going on with the person's words the person's.
/// The run's own go-on and retry are always the person's.
/// </param>
internal sealed record TransportStart(ProcessStartInfo Info, StartClass Whose)
{
    /// <summary>The session record the run is, which its process is tracked, waited and bracketed under; required by the open.</summary>
    public string? Session { get; init; }

    /// <summary>Where the run's own transcript is written: the text of its wire a person reads; required by the open.</summary>
    public string? Transcript { get; init; }

    /// <summary>The events the record opens with, said once: a start's target or a resume's answer, never said again by a restart.</summary>
    public IReadOnlyList<SessionEvent>? Opening { get; init; }

    /// <summary>A fact about how the run is set up, written ahead of anything the process says; null says none.</summary>
    public string? Preamble { get; init; }

    /// <summary>Where the harness's own words are kept for its ending, which its executor reads after; null keeps the run's own.</summary>
    public HarnessWords? Own { get; init; }

    /// <summary>The target the spawn is prepared for, which a go-on's resume is equipped for as its start is.</summary>
    public SessionTarget? Target { get; init; }

    /// <summary>What the spawn is asked, composed by the run's executor, which a go-on asks again naming the conversation it resumes.</summary>
    public SpawnRequest? Request { get; init; }

    /// <summary>
    /// What the harness is selected as (its account's home, its binary), which a go-on is handed as the first run is; null where the run goes
    /// on in nothing, so its executor holds no words for it (<see cref="TransportRun.GoesOn"/>).
    /// </summary>
    public HarnessSelection? Selection { get; init; }

    /// <summary>Why this session takes no person's line, in the driver's words; null for a conversation, the one kind that takes turns (SessionProcesses.Track).</summary>
    public string? RefusesInput { get; init; }

    /// <summary>Whether it is handed a server that drives Daoris's browser.</summary>
    public bool DrivesBrowser { get; init; }

    /// <summary>A partner's piece's job, which its harness joins beneath this process's agents' job; null for every other run.</summary>
    public string? Piece { get; init; }

    /// <summary>
    /// The lineage token its run's move issued, whose <c>start</c> is written already: a resume moved before its spawn. The
    /// first process takes it; null issues that process its own, as every later one is.
    /// </summary>
    public string? Moved { get; init; }

    /// <summary>
    /// The native door's prompt, written on the process's input once its output is being read, then closed: a start's
    /// composed target or a resume's words. The protocol door's goes on its wire (<see cref="TransportProtocol.Run"/>); null writes none.
    /// </summary>
    public string? Prompt { get; init; }

    /// <summary>
    /// Told as the first process's native prompt's write returns: where a carry-on's moved words are <c>sent</c>,
    /// never the process's start, which a crash can precede the write by.
    /// </summary>
    public Action? Written { get; init; }

    /// <summary>How long it may run before it is stopped, where that is not the machine's session timeout: a pass's own minutes.</summary>
    public int? Minutes { get; init; }

    /// <summary>Told the bound, in minutes, when it ends the process, before the kill.</summary>
    public Action<int>? TimingOut { get; init; }

    /// <summary>The watcher's end of the run: a headless <c>--quiet-end</c>, its own cause, never a registry stop.</summary>
    public CancellationToken Quiet { get; init; }

    /// <summary>Told before the watcher's kill, as <see cref="TimingOut"/> is before a bound's.</summary>
    public Action? QuietEnding { get; init; }

    /// <summary>
    /// Told the moment the native harness names its conversation, once that conversation is kept: what a resume learns on the
    /// way, <c>Named</c> and <c>Prompted</c>, and where a conversation going on says its words went. Null tells none.
    /// </summary>
    public Action? Named { get; init; }

    /// <summary>
    /// Told each event the native capture keeps, once the record holds it: how a conversation learns its turn ended, so its next words
    /// land behind that end. Null tells none.
    /// </summary>
    public Action<SessionEvent>? Observed { get; init; }

    /// <summary>Told the uuid of each line the native harness echoed where it took it, in the wire's order beside <see cref="Observed"/>.</summary>
    public Action<string>? Echoed { get; init; }

    /// <summary>The protocol door's session over the process: what its open is handed. Null on a native door.</summary>
    public TransportProtocol? Protocol { get; init; }
}

/// <summary>
/// The protocol door's session over a run's process: where it works, the rules it is handed and what the executor wires
/// onto it. A driven executor's session is its own closure here (<see cref="Configure"/>, <see cref="Run"/>, <see cref="Ended"/>): what it
/// sets on the session, the prompt it runs and what it says of the words that never reached it, beside the run's own lines.
/// </summary>
/// <param name="Cwd">The folder the session works in.</param>
internal sealed record TransportProtocol(string Cwd)
{
    /// <summary>The permission posture, in the adapter's own vocabulary; null asks none.</summary>
    public string? Posture { get; init; }

    /// <summary>Whether its agent is asked for typed session failures (<see cref="AcpSession.TypedFailures"/>).</summary>
    public bool TypedFailures { get; init; }

    /// <summary>What <c>session/new</c> carries for the rules composed for this session.</summary>
    public object? Meta { get; init; }

    /// <summary>The connectors the session is offered, the plan its open reads.</summary>
    public ConnectorPlan? Connectors { get; init; }

    /// <summary>Where the run's wait says the timeout before its kill, handed the session once it is made.</summary>
    public AcpEnding? Ending { get; init; }

    /// <summary>What the executor sets on the session before it runs: its permissions, its background look, its retry, the checks its start kept.</summary>
    public Action<AcpSession>? Configure { get; init; }

    /// <summary>
    /// What the session does once its process is up, given the conversation a resume names (null for a start). Null holds the session open
    /// instead: opened or resumed and left to the words beside it, the conversation's shape (<see cref="TransportRun.Session"/>, <see cref="Opened"/>).
    /// </summary>
    public Func<AcpSession, string?, CancellationToken, Task<AcpOutcome>>? Run { get; init; }

    /// <summary>
    /// Told as a driven session's run ends, whatever ended it, once the turn's end is said and before the input closes: where the executor
    /// says what the person said that never reached it. Null tells nothing.
    /// </summary>
    public Action? Ended { get; init; }

    /// <summary>Told the session's config options each time they change, as the session is made with them; null tells none.</summary>
    public Action<IReadOnlyList<AcpConfigOption>>? Options { get; init; }

    /// <summary>
    /// Told once a held session opened or resumed (the conversation's shape, <see cref="Run"/> null): its turns may begin, on the run's
    /// <see cref="TransportRun.Session"/>. Null tells nothing.
    /// </summary>
    public Action? Opened { get; init; }

    /// <summary>
    /// Told the failure that ended a held session's open, before the run ends its process: where the executor says why in its transcript and lets
    /// go of what waited on the open. Null tells nothing.
    /// </summary>
    public Action<Exception>? Unopened { get; init; }

    /// <summary>The driven shape: the session opened, prompted with <paramref name="prompt"/> and closed, as <c>AcpSession.RunAsync</c> does.</summary>
    public static TransportProtocol Driven(string cwd, string prompt, ConnectorPlan? plan = null) =>
        new(cwd)
        {
            Connectors = plan,
            Run = (session, conversation, ct) => session.RunAsync(cwd, prompt, ct, plan, resume: conversation),
        };
}

/// <summary>
/// What a <see cref="TransportRun"/> asks of the loop that holds it: the driver's own waits, its watch, its kept
/// conversations and downloads, and each process's lineage bracket, whose file is Participants'. A port because the run sits in
/// Agents and the driver above it; <c>DriverTransportHost</c> answers it from the driver, so the run's steps are the driver's, not copies of them.
/// </summary>
internal interface ITransportHost
{
    /// <summary>The home whose launcher and machine log a start uses.</summary>
    string Home { get; }

    /// <summary>The sessions' processes, which each run's is tracked in.</summary>
    SessionProcesses Processes { get; }

    /// <summary>The machine log, or null for the home's own file.</summary>
    MachineLog? Log { get; }

    /// <summary>A stream a run reads, stamping its session heard at each line.</summary>
    TextReader Hearing(TextReader reader, string sessionId);

    /// <summary>The same stamp for a run that reads no stream of its own, told each raw line its runner reads.</summary>
    void Heard(string sessionId)
    {
    }

    /// <summary>
    /// A process's lineage token: issued, its <c>start</c> written and set on the spawn's environment, all before the spawn;
    /// <paramref name="moved"/> where its run's move issued it already (<c>Driver.Bracketed</c>).
    /// </summary>
    string Bracketed(string sessionId, ProcessStartInfo info, string? moved);

    /// <summary>The spawn under <paramref name="proc"/>: its process handed the token, or the token let go where it made none (<c>Driver.Spawned</c>).</summary>
    Task<Process?> SpawnedAsync(string proc, Func<Task<Process?>> spawn);

    /// <summary>
    /// The exit, or null when a bound killed it; the tree dies with it (<c>Driver.WaitAsync</c>). The exit read is told the token set, and a
    /// wait thrown as the loop closes writes the process's end unclean before it leaves (<c>Driver.WaitedAsync</c>).
    /// </summary>
    Task<int?> WaitAsync(
        string sessionId, string proc, Process process, CancellationToken ct, int? minutes, Action<int>? timingOut, CancellationToken quiet,
        Action? quietEnding);

    /// <summary>
    /// Whether the capture finished within the bound past the exit, which ends a stray's hold on it; one that faults writes the
    /// process's end unclean before the fault leaves (<c>Driver.DrainedAsync</c>).
    /// </summary>
    Task<bool> DrainedAsync(string sessionId, string proc, Task capture);

    /// <summary>A process's end once its exit is read and its capture drained: clean, or killed, stopped or failed (<c>Driver.Ended</c>).</summary>
    void Ended(string sessionId, string proc, int? exit);

    /// <summary>
    /// A run let go before its end (<c>SessionWaits.AbandonedAsync</c>): its process ended, its end written unclean once its exit is read,
    /// and its capture observed, a failure there written with its reason.
    /// </summary>
    Task AbandonedAsync(string sessionId, string proc, Process process, Task capture);

    /// <summary>The downloads a start is handed, kept on its record before it is spawned.</summary>
    Task KeepHandedAsync(string sessionId, ProcessStartInfo info, CancellationToken ct);

    /// <summary>
    /// A resume's process given the environment a start has (<c>Driver.Prepare</c>), its door's facts read from <paramref name="adapters"/>, the
    /// registry the run is started from: never the host's registry, which a plugin's change since may have made without the door. Never
    /// asked of a host whose runs are handed no selection, as a conversation's are: it goes on through a start of its own.
    /// </summary>
    ProcessStartInfo Prepared(ProviderRegistry adapters, AgentDoor adapter, SessionTarget target, HarnessSelection selection, ProcessStartInfo next) =>
        throw new NotSupportedException("a conversation goes on through a start of its own, never through its run's go-on.");

    /// <summary>How long an agent whose session ended with its own background work still running is given to exit once its stdin closed.</summary>
    TimeSpan LingerBound { get; }

    /// <summary>Waits for an agent to exit after its stdin closed, and ends its tree past the bound; true where it ended it here.</summary>
    Task<bool> EndedLingeringAsync(Process process, AcpEnding? ending);
}
