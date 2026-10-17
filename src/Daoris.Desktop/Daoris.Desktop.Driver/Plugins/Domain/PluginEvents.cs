namespace Daoris.Driver;

/// <summary>
/// The machine log's plugin events (PLUGUI1d, D119 §4.2, D94), and the words each field may take. Every value is
/// a name, a count, a flag or a time: never a plugin's words.
/// </summary>
public static class PluginEvents
{
    /// <summary>A plugin's process started and answered the handshake: <c>plugin</c>, <c>points</c>, <c>ms</c>, <c>by</c>.</summary>
    public const string Started = "plugin.started";

    /// <summary>A plugin's process was stopped: <c>plugin</c>, <c>why</c>, <c>by</c>.</summary>
    public const string Stopped = "plugin.stopped";

    /// <summary>A plugin answered at a point: <c>plugin</c>, <c>point</c>, <c>answer</c>, <c>ms</c>.</summary>
    public const string Called = "plugin.called";

    /// <summary>A plugin failed, at its start, between calls or at a point: <c>plugin</c>, <c>where</c>, <c>kind</c>, <c>code</c>, <c>ms</c>, <c>by</c>.</summary>
    public const string Failed = "plugin.failed";

    /// <summary>A session was handed a plugin's server, or it was withheld: <c>plugin</c>, <c>server</c>, <c>session</c>, <c>handed</c>.</summary>
    public const string Served = "plugin.served";

    /// <summary>A plugin was tried with the kit: <c>plugin</c>, <c>passed</c>, <c>checks</c>, <c>failed</c>, <c>ms</c>, <c>door</c>.</summary>
    public const string Tried = "plugin.tried";

    /// <summary>A plugin's own tests ran: <c>plugin</c>, <c>passed</c>, <c>code</c>, <c>ms</c>, <c>door</c>. Its runner is PLUGUI1g's.</summary>
    public const string Tested = "plugin.tested";

    public static readonly IReadOnlyList<string> All = [Started, Stopped, Called, Failed, Served, Tried, Tested];

    /// <summary>Who started the process: the driver loop, a landing, or a hand-off after one.</summary>
    public const string ByLoop = "loop";

    public const string ByLanding = "landing";

    public const string ByHand = "hand";

    /// <summary>An occasion's asks about landed branches' pull requests (PLUGHOOK1a, D148 point 6).</summary>
    public const string ByState = "state";

    /// <summary>Why a process stopped: switched off, removed, updated, its manifest changed, or its owner ended.</summary>
    public const string Off = "off";

    public const string Removed = "removed";

    public const string Updated = "updated";

    public const string Changed = "changed";

    public const string Ended = "ended";

    /// <summary>Why the loop stopped a work process (FABRIC-PERFORM1a): no unit awaits its steps, or this process no longer holds the home.</summary>
    public const string Idle = "idle";

    public const string NotHeld = "not-held";

    /// <summary>A frame from a plugin not taken (FABRIC-PERFORM1a, §C4): its code (<see cref="WorkRefusals"/>) and how many it stands for.</summary>
    public const string Step = "plugin.step";

    /// <summary>Where a failure happened, besides a point's name: the start, or the process between calls.</summary>
    public const string AtStart = "start";

    public const string AtProcess = "process";

    /// <summary>What kind of failure: it did not start, it exited, it answered late, unreadably, or with an error.</summary>
    public const string Unstartable = "unstartable";

    public const string Exited = "exited";

    public const string Late = "late";

    public const string Unreadable = "unreadable";

    public const string Errored = "errored";

    /// <summary>Three unasked exits of its work process within an hour (FABRIC-PERFORM1a): not started again until the person tries again.</summary>
    public const string Unsound = "unsound";

    /// <summary>What a plugin answered: a decision's word, an observation's, or what a landing did.</summary>
    public const string Allow = "allow";

    public const string Hold = "hold";

    public const string Answered = "answered";

    public const string Pushed = "pushed";

    public const string NotPushed = "not-pushed";

    /// <summary>Which door a trial or a test run came from.</summary>
    public const string Screen = "screen";

    public const string Terminal = "terminal";
}

/// <summary>
/// The kind of failure a <see cref="DriverException"/> from a plugin's wire is (PLUGUI1d): marked where it is
/// thrown, read where it is logged. The sentence stays the exception's; the log takes only the kind.
/// </summary>
/// <remarks>
/// A mark on the exception rather than a type of its own: every catch and every test that knows a plugin's
/// failure as a <see cref="DriverException"/> stays as it is.
/// </remarks>
public static class PluginFailures
{
    private const string Key = "daoris.plugin.failure";

    /// <summary>Mark <paramref name="error"/> as <paramref name="kind"/>, one of <see cref="PluginEvents"/>' kinds.</summary>
    public static DriverException Mark(DriverException error, string kind)
    {
        error.Data[Key] = kind;
        return error;
    }

    /// <summary>The kind <paramref name="error"/> was marked with, or <paramref name="otherwise"/> where it carries none.</summary>
    public static string KindOf(Exception error, string otherwise) => error.Data[Key] as string ?? otherwise;
}
