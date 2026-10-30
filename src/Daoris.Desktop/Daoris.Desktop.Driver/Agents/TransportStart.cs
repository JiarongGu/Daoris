using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// What a <see cref="TransportRun"/> starts and how it holds it (AGENTPROVIDER1m-i part 1): the process a door's spawn prepared, whose it is
/// at the start gate, and what the run needs beside it for its wait, its go-on and its retry. Built by the executor that holds the run, which
/// is the one place that knows its target and its selection.
/// </summary>
/// <param name="Info">The process to start, prepared and handed everything a start is handed.</param>
/// <param name="Whose">
/// Whose start it is at the start gate (PARALLEL1d): the loop's first run is the background's, one going on with the person's words the person's.
/// The run's own go-on and retry are always the person's.
/// </param>
internal sealed record TransportStart(ProcessStartInfo Info, StartClass Whose)
{
    /// <summary>The target the spawn was prepared for, which a go-on prepares its resume from; null where the run goes on in nothing.</summary>
    public SessionTarget? Target { get; init; }

    /// <summary>What the harness is selected as (its account's home, its binary), which a go-on is handed as the first run was; null with no target.</summary>
    public HarnessSelection? Selection { get; init; }

    /// <summary>Why this session takes no person's line, in the driver's words; null for a conversation, the one kind that takes turns (SessionProcesses.Track).</summary>
    public string? RefusesInput { get; init; }

    /// <summary>Whether it was handed a server that drives Daoris's browser (BRW8).</summary>
    public bool DrivesBrowser { get; init; }

    /// <summary>A partner's piece's job (COAGENT1b), which its harness joins beneath this process's agents' job; null for every other run.</summary>
    public string? Piece { get; init; }

    /// <summary>How long it may run before it is stopped, where that is not the machine's session timeout: a pass's own minutes (XAGENT1d).</summary>
    public int? Minutes { get; init; }

    /// <summary>Told the bound, in minutes, when it ends the process, before the kill (BGWAIT2).</summary>
    public Action<int>? TimingOut { get; init; }

    /// <summary>The watcher's end of the run (SESSWATCH1a): a headless <c>--quiet-end</c>, its own cause, never a registry stop.</summary>
    public CancellationToken Quiet { get; init; }

    /// <summary>Told before the watcher's kill, as <see cref="TimingOut"/> is before a bound's (BGWAIT2).</summary>
    public Action? QuietEnding { get; init; }

    /// <summary>
    /// Told when the native harness named its conversation (SESSCTX1c): what a resume learns on the way, <c>Named</c> and <c>Prompted</c>. Null for
    /// a run that is no resume.
    /// </summary>
    public Action? Named { get; init; }

    /// <summary>
    /// Told as the same start is made once more (SESSCTX1c): the resume's retry mark. Null where the run is no resume, which
    /// <see cref="AgentRun.RestartAsync"/> refuses.
    /// </summary>
    public Action? Again { get; init; }

    /// <summary>The protocol door's session over the process: what its open is handed. Null on a native door.</summary>
    public TransportProtocol? Protocol { get; init; }
}

/// <summary>
/// The protocol door's session over a run's process (AGENTPROVIDER1m-i part 1): where it works, the rules it is handed and what the executor
/// wires onto it, each as the driven capture (<c>Driver.CaptureAcpAsync</c>) does today.
/// </summary>
/// <param name="Cwd">The folder the session works in.</param>
internal sealed record TransportProtocol(string Cwd)
{
    /// <summary>The permission posture, in the adapter's own vocabulary (ACP3); null asks none.</summary>
    public string? Posture { get; init; }

    /// <summary>Whether its agent is asked for typed session failures (CODEXSTATUS1, <see cref="AcpSession.TypedFailures"/>).</summary>
    public bool TypedFailures { get; init; }

    /// <summary>What <c>session/new</c> carries for the rules composed for this session (PERM1).</summary>
    public object? Meta { get; init; }

    /// <summary>The servers the session is offered (ACP4, D65 §1f).</summary>
    public IReadOnlyList<AcpMcpServer>? Servers { get; init; }

    /// <summary>Where the run's wait says the timeout before its kill, handed the session once it is made (BGWAIT2, DONEEND1).</summary>
    public AcpEnding? Ending { get; init; }

    /// <summary>What the executor sets on the session before it runs: its permissions, its background look, its retry, the checks its start kept.</summary>
    public Action<AcpSession>? Configure { get; init; }

    /// <summary>
    /// What the session does once its process is up, given the conversation a resume names (null for a start). Null holds the session open
    /// instead: opened or resumed and left to the words beside it, the conversation's shape (<see cref="TransportRun.Session"/>).
    /// </summary>
    public Func<AcpSession, string?, CancellationToken, Task<AcpOutcome>>? Run { get; init; }

    /// <summary>The driven shape: the session opened, prompted with <paramref name="prompt"/> and closed, as <c>AcpSession.RunAsync</c> does.</summary>
    public static TransportProtocol Driven(string cwd, string prompt, IReadOnlyList<AcpMcpServer>? servers = null) =>
        new(cwd)
        {
            Servers = servers,
            Run = (session, conversation, ct) => session.RunAsync(cwd, prompt, ct, servers, resume: conversation),
        };
}

/// <summary>
/// What a <see cref="TransportRun"/> asks of the loop that holds it (AGENTPROVIDER1m-i part 1): the driver's own waits, its watch, its
/// kept conversations and downloads. A port because the run sits in Agents and the driver above it; <c>DriverTransportHost</c> answers it
/// from the driver, so the run's steps are the driver's, not copies of them.
/// </summary>
internal interface ITransportHost
{
    /// <summary>The home whose launcher and machine log a start uses.</summary>
    string Home { get; }

    /// <summary>The sessions' processes, which each run's is tracked in.</summary>
    SessionProcesses Processes { get; }

    /// <summary>The machine log, or null for the home's own file.</summary>
    MachineLog? Log { get; }

    /// <summary>A stream a run reads, stamping its session heard at each line (SESSWATCH1a).</summary>
    TextReader Hearing(TextReader reader, string sessionId);

    /// <summary>The exit, or null when a bound killed it; the tree dies with it (<c>Driver.WaitAsync</c>).</summary>
    Task<int?> WaitAsync(Process process, CancellationToken ct, int? minutes, Action<int>? timingOut, CancellationToken quiet, Action? quietEnding);

    /// <summary>Whether the capture finished within the bound past the exit, which ends a stray's hold on it (<c>Driver.CaptureWithinAsync</c>, SESSWATCH1a).</summary>
    Task<bool> CaptureWithinAsync(Task capture, string sessionId);

    /// <summary>The downloads a start is handed, kept on its record before it is spawned (KEEP1b).</summary>
    Task KeepHandedAsync(string sessionId, ProcessStartInfo info, CancellationToken ct);

    /// <summary>The conversation the harness named, kept for the record under the adapter (ANSWER1a).</summary>
    void KeepConversation(string sessionId, string adapter, string conversation);

    /// <summary>The command configured for a door, or null for its own.</summary>
    IReadOnlyList<string>? Command(string adapter);

    /// <summary>A resume's process given the environment a start has (<c>Driver.Prepare</c>).</summary>
    ProcessStartInfo Prepared(ISessionAdapter adapter, SessionTarget target, HarnessSelection selection, ProcessStartInfo next);

    /// <summary>How long an agent whose session ended with its own background work still running is given to exit once its stdin closed (DONEEND1).</summary>
    TimeSpan LingerBound { get; }

    /// <summary>Waits for an agent to exit after its stdin closed, and ends its tree past the bound (DONEEND1); true where it ended it here.</summary>
    Task<bool> EndedLingeringAsync(Process process, AcpEnding? ending);
}
