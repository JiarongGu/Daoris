namespace Daoris.Driver;

/// <summary>
/// What <c>sessions</c> reads and touches: the service, the home's choices and files, this machine's process markers and
/// the machine log.
/// </summary>
/// <param name="Unadopted">Whether the configured agent may drive a repository that has not adopted, which the planner's verdicts are read by.</param>
/// <param name="Log">Where the archive's and the delete's lines go, as the terminal's door; null writes none.</param>
public sealed record SessionsWorld(ServiceClient Service, string Home, DriverConfig Config, bool Unadopted, MachineLog? Log) : ISupervisorWorld, IParticipantsWorld
{
    /// <summary>The markers every driver on the home leaves for a live process: whether something here runs a session.</summary>
    public SessionProcesses Processes { get; init; } = new(DaorisHome.Entry(Home, HomeEntry.SessionPids));

    /// <summary>How a delete removes a file from the home: the disk, or a test's that refuses a path as a held file does.</summary>
    public Action<string, bool> Remover { get; init; } = SessionHomeFiles.FromDisk;

    /// <summary>
    /// Where the running Daoris is looked for: the listing is its <c>SESSION_GROUPS</c> where it answers, in the same words. Null
    /// looks for none, as a test of the alone path does.
    /// </summary>
    public InstanceWorld? Instance { get; init; }

    /// <summary>How long a request is waited for unless a world says otherwise: ten seconds.</summary>
    public static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(10);

    /// <summary>How long a request is waited for: <see cref="DefaultWait"/>.</summary>
    public TimeSpan Wait { get; init; } = DefaultWait;

    /// <summary>How often the record is read while a request waits.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The clock <see cref="Wait"/> and <see cref="Answer"/> are read by: the machine's, or a test's that stands still until the
    /// loop has taken the words, so the wait a test means is not the load's.
    /// </summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>
    /// How long the answer of a loop that took a say is waited for once <see cref="Wait"/> is over: that loop's own bound on a say,
    /// its client's timeout, since a loop that took one always answers it, a failure included.
    /// </summary>
    public TimeSpan Answer { get; init; } = DriverHttp.Timeout;

    /// <summary>
    /// Whether a driver loop holds this home (<see cref="HomeLease.HeldBy"/>): where none does, nothing would take a say up, so the
    /// verb keeps the words on the record itself. A verb's or a migration's hold is no loop: only an instance's counts, and this
    /// verb's own lease among them never does.
    /// </summary>
    public Func<bool> LoopRuns { get; init; } = () => HomeLease.HeldBy(Home) is { IsInstance: true };

    /// <summary>
    /// Told why a held word's line fell back to "at the driver's next look" instead of naming what holds it: the failure's kind, or
    /// that the planner gave no verdict. A test reads the cause; the person reads the same line.
    /// </summary>
    public Action<string>? Fell { get; init; }

    /// <summary>
    /// The cool-off a start on an adapter would read for an account, or null where it is ready (<c>hand-over</c> naming no agent): read from this
    /// home's cool-offs by the account's owner, as the roster reads them, plugins' harnesses included.
    /// </summary>
    public Func<string?, string?, CoolingEntry?>? CoolingOf { get; init; }

    /// <summary>
    /// The conversation opened with the words a session could not go on with (<c>start-from</c>): the host's chat door, which sends the
    /// route to the running Daoris, holds what it opens and answers its exit. Null where nothing here can hold a conversation.
    /// </summary>
    public Func<string, CancellationToken, Task<int>>? StartFrom { get; init; }

    /// <summary>
    /// The door for a conversation handed to another agent: the host's chat door, which sends the route and holds what it opens, as
    /// <see cref="StartFrom"/> does its start, and answers its exit. Takes the session, the agent and the account, or null for the
    /// roster's pick. Null where nothing here can hold a conversation.
    /// </summary>
    public Func<string, string, string?, CancellationToken, Task<int>>? HandOver { get; init; }

    /// <summary>
    /// The keeping stop (<c>sessions stop &lt;id&gt; --turn</c>): stop the session's turn so what waits goes next, through the Daoris
    /// whose loop holds its turns, answering a <see cref="KeepingStops"/> code. Null where nothing here holds them: the verb alone,
    /// which sends it to the running Daoris instead.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? StopTurn { get; init; }

    /// <summary>
    /// The agents a quest's session may go on with: each one's accounts and door, as the loop's roster reads them for a start. Null
    /// reads this build's adapters and the home's plugins.
    /// </summary>
    public Func<string, HandOverAgent?>? Drives { get; init; }

    /// <summary>
    /// Where a verb's line on a session's record goes: the running Daoris's own record and transcript where the verb runs in it, so
    /// one writer numbers the record; null writes it to this home's sessions folder, as the verb alone does.
    /// </summary>
    public Action<string, Noted>? Notes { get; init; }

    /// <summary>
    /// How a piece's lead's tree is read at the person's ask (<c>sessions partner</c>): its commit and uncommitted paths, git's by
    /// default, a test's without a process.
    /// </summary>
    public Func<string, CancellationToken, Task<PieceBase>> PieceBase { get; init; } = PartnerPersonAsk.ReadTreeAsync;

    /// <summary>
    /// The holder key this verb took from its terminal's environment at its entry (<see cref="Daoris.Driver.HolderKey.Taken"/>), or a
    /// test's sentinel: <c>sessions supervise</c>'s alone, never an argument.
    /// </summary>
    public Func<string?> HolderKey { get; init; } = () => Daoris.Driver.HolderKey.Taken;
}

/// <summary>What the keeping stop came to: <c>CANCEL_TURN</c>'s answer to a terminal's <c>sessions stop &lt;id&gt; --turn</c>, which the verb words.</summary>
public static class KeepingStops
{
    /// <summary>The turn was asked to stop, keeping the session: what waits goes next.</summary>
    public const string Stopped = "stopped";

    /// <summary>No turn ran, or nothing waited for its end: nothing was stopped, since ending the work is the session's own stop.</summary>
    public const string Nothing = "nothing";

    /// <summary>The door has no stop that keeps the session (the native door's run): nothing was stopped.</summary>
    public const string NoInterrupt = "no-interrupt";

    /// <summary>Nothing here runs the session.</summary>
    public const string NotHere = "not-here";
}
