namespace Daoris.Driver;

/// <summary>
/// One check asked of the broker: a declared gate by name, or a command of its own, to run in <paramref name="Session"/>'s tree. Kept as
/// <c>&lt;id&gt;.ask.json</c> under the home's <c>checks</c> folder, written by the connector's <c>check_run</c> or by Daoris's own
/// reading of a hand-back, never changed.
/// </summary>
/// <param name="Gate">The declared gate to run; null with no <paramref name="Command"/> runs every heavy gate.</param>
/// <param name="Command">A command of the asker's own, run as a heavy run (<c>daoris gate run -- &lt;command&gt;</c>).</param>
/// <param name="By">Who asked: the session's agent, or Daoris reading its hand-back.</param>
public sealed record CheckAsk(string Id, string Session, string? Gate, IReadOnlyList<string>? Command, string By, DateTimeOffset At)
{
    public const string ByAgent = "agent";
    public const string ByDaoris = "daoris";
}

/// <summary>The states a check passes through, and the newest of them is its verdict.</summary>
public static class CheckStates
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Reached = "reached";
    public const string Failed = "failed";
    public const string Stopped = "stopped";

    /// <summary>Daoris could not run it: no verdict, never said to the asker.</summary>
    public const string Errored = "errored";

    /// <summary>Whether the state is a verdict: the run is over.</summary>
    public static bool Finished(string state) => state is Reached or Failed or Stopped;
}

/// <summary>
/// A check's state, <c>&lt;id&gt;.state.json</c>: written by the driver alone.
/// </summary>
/// <param name="Exit">The heavy run's exit code, where it ran.</param>
/// <param name="Tail">The end of the run's log, bounded, for a red the asker reads.</param>
/// <param name="Tip">The commit of the asker's tree when the run began: the inputs the verdict is for.</param>
/// <param name="Handed">The word that carried the verdict to the asker, or <see cref="CheckHanding.Quiet"/> where no word is owed.</param>
public sealed record CheckState(string Id, string State, DateTimeOffset At)
{
    public int? Exit { get; init; }

    public string? Tail { get; init; }

    public string? Tip { get; init; }

    public string? Handed { get; init; }

    public string? Error { get; init; }
}
