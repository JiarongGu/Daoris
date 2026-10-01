using System.Text.RegularExpressions;

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

    /// <summary>Why a process stopped: switched off, removed, updated, its manifest changed, or its owner ended.</summary>
    public const string Off = "off";

    public const string Removed = "removed";

    public const string Updated = "updated";

    public const string Changed = "changed";

    public const string Ended = "ended";

    /// <summary>Where a failure happened, besides a point's name: the start, or the process between calls.</summary>
    public const string AtStart = "start";

    public const string AtProcess = "process";

    /// <summary>What kind of failure: it did not start, it exited, it answered late, unreadably, or with an error.</summary>
    public const string Unstartable = "unstartable";

    public const string Exited = "exited";

    public const string Late = "late";

    public const string Unreadable = "unreadable";

    public const string Errored = "errored";

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

/// <summary>
/// What Daoris did with a plugin, into the machine log (PLUGUI1d, D119 §4.2), and into the loop's record of its
/// health (<see cref="PluginHealth"/>) where one is kept. The one writer of the <c>plugin.*</c> lines: the hook
/// set, the landing, the hand-off, the handing of servers and the kit's trial all call it.
/// </summary>
/// <remarks>
/// <para><b>Never anyone's words</b> (D94 §5): a hold's reason, an answer's message, a pull request's address, a
/// frame, a command line, an environment value and anything the plugin wrote to stderr are never parameters
/// here, so they cannot reach a line. Each name is written only if it has a name's shape; a sentence put where
/// a name goes is null.</para>
///
/// <para><b>The record is fed from the same call</b>, so a line and the loop's record never say two things. A trial
/// and a server handed are not the plugin's work, and feed the record nothing (D119 §2).</para>
/// </remarks>
public sealed class PluginLog(MachineLog? log, PluginHealth? health = null)
{
    /// <summary>A writer that writes nothing and keeps no record.</summary>
    public static PluginLog None { get; } = new(null);

    // A plugin id, a point, a server, a session, or one of the words above: never a phrase.
    private static readonly Regex NameShape = new("^[A-Za-z0-9][A-Za-z0-9_.:/-]{0,119}$", RegexOptions.CultureInvariant);

    public void Started(string plugin, IReadOnlyList<string> points, long? ms, string by)
    {
        log?.Info(PluginEvents.Started,
            ("plugin", Name(plugin)), ("points", string.Join(",", points.Select(Name).OfType<string>())), ("ms", ms), ("by", Name(by)));
        health?.Observe(new PluginWord(PluginEvents.Started, plugin, By: by, Points: points));
    }

    public void Stopped(string plugin, string why, string by)
    {
        log?.Info(PluginEvents.Stopped, ("plugin", Name(plugin)), ("why", Name(why)), ("by", Name(by)));
        health?.Observe(new PluginWord(PluginEvents.Stopped, plugin, By: by, Why: why));
    }

    /// <param name="answer"><see cref="PluginEvents.Allow"/>, <c>Hold</c>, <c>Answered</c>, <c>Pushed</c> or <c>NotPushed</c>: the answer's word, never its sentence.</param>
    public void Called(string plugin, string point, string answer, long? ms)
    {
        log?.Info(PluginEvents.Called, ("plugin", Name(plugin)), ("point", Name(point)), ("answer", Name(answer)), ("ms", ms));
        health?.Observe(new PluginWord(PluginEvents.Called, plugin, Point: point));
    }

    /// <param name="where"><see cref="PluginEvents.AtStart"/>, <see cref="PluginEvents.AtProcess"/>, or the point.</param>
    /// <param name="code">The process's exit code, where it exited and the code is known.</param>
    public void Failed(string plugin, string where, string kind, int? code, long? ms, string by)
    {
        log?.Warn(PluginEvents.Failed,
            ("plugin", Name(plugin)), ("where", Name(where)), ("kind", Name(kind)), ("code", code), ("ms", ms), ("by", Name(by)));
        health?.Observe(new PluginWord(PluginEvents.Failed, plugin, By: by, Where: where, Kind: kind));
    }

    /// <param name="handed">False for a server withheld from the session, as one that drives Daoris's browser is where no shell answers (D78).</param>
    public void Served(string plugin, string server, string session, bool handed) =>
        log?.Info(PluginEvents.Served, ("plugin", Name(plugin)), ("server", Name(server)), ("session", Name(session)), ("handed", handed));

    /// <summary>
    /// One line per server the contributing plugins declare, for one session: handed if it is among
    /// <paramref name="handed"/>, withheld if not. Read from the catalogue the session's servers came from.
    /// </summary>
    public void Served(PluginCatalog catalog, string session, IReadOnlyList<AcpMcpServer> handed)
    {
        if (log is null) return;
        foreach (var plugin in catalog.Contributing)
        {
            foreach (var server in plugin.Manifest.Servers)
            {
                Served(plugin.Manifest.Id, server.Name, session, handed.Any(each => string.Equals(each.Name, server.Name, StringComparison.Ordinal)));
            }
        }
    }

    /// <summary>A trial, from <paramref name="door"/>: a warning when the plugin failed a check. Its sentences are never written.</summary>
    public void Tried(PluginTrial trial, long ms, string door) =>
        log?.Write(trial.Passed ? "info" : "warn", PluginEvents.Tried,
        [
            ("plugin", Name(trial.Plugin)), ("passed", trial.Passed), ("checks", trial.Steps.Count),
            ("failed", trial.Steps.Count(step => !step.Ok)), ("ms", ms), ("door", Name(door)),
        ]);

    /// <summary>A run of the plugin's own tests, from <paramref name="door"/>. Its shape is reserved here; its runner is PLUGUI1g's.</summary>
    public void Tested(string plugin, bool passed, int? code, long ms, string door) =>
        log?.Write(passed ? "info" : "warn", PluginEvents.Tested,
            [("plugin", Name(plugin)), ("passed", passed), ("code", code), ("ms", ms), ("door", Name(door))]);

    /// <summary>A name as written, or null for anything that is not one.</summary>
    private static string? Name(string? value) => value is not null && NameShape.IsMatch(value) ? value : null;
}
