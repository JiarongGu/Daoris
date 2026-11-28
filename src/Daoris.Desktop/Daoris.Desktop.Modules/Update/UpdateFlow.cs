namespace Daoris.Desktop;

/// <summary>The states an update is in, as the page and the terminal read them. One word each; <see cref="UpdateFlow"/> says which may follow which.</summary>
public static class UpdateStates
{
    /// <summary>Nothing staged, or no install at all.</summary>
    public const string None = "none";

    /// <summary>A build is staged and the person said *Not now* (or `--cancel`): work goes on, and it installs on a word.</summary>
    public const string Staged = "staged";

    /// <summary>Staged and installed when idle: nothing new starts, and what runs is counted down.</summary>
    public const string Draining = "draining";

    /// <summary>Staged, its drain past its bound: new work starts again, and it installs at the first look nothing runs.</summary>
    public const string Deferred = "deferred";

    /// <summary>
    /// The application stops taking work for the build and is making ready to close: the build checked, the launcher started. It says what
    /// it waits on and is bounded (<see cref="InstallUpdater.StopBound"/>); a refusal, a word or a newer build ends it at once.
    /// </summary>
    public const string Stopping = "stopping";

    /// <summary>The launcher is started and the application closing for it, within <see cref="InstallUpdater.CloseBound"/>.</summary>
    public const string Applying = "applying";

    /// <summary>The staged build failed its check, the stop ran out of its bound, or the launcher would not start: nothing is replaced, and work goes on.</summary>
    public const string Refused = "refused";

    /// <summary>A swap this start confirmed, until the person puts it away with *Dismiss*.</summary>
    public const string Installed = "installed";

    public static readonly IReadOnlyList<string> All =
        [None, Staged, Draining, Deferred, Stopping, Applying, Refused, Installed];
}

/// <summary>Why a state is entered: each transition of an update says one of these, in the machine log and to the page.</summary>
public static class UpdateWhy
{
    /// <summary>A look found a build staged.</summary>
    public const string Seen = "seen";

    /// <summary>Nothing is staged any longer (the launcher took it, or someone removed it).</summary>
    public const string Gone = "gone";

    /// <summary>Another build now stands in the staged folder: what is under way for the one before ends and the new one is read.</summary>
    public const string Restaged = "restaged";

    /// <summary>The person's word *Update when idle* (the default, absent a word).</summary>
    public const string WhenIdle = "when-idle";

    /// <summary>The person's word *Update now*.</summary>
    public const string Now = "now";

    /// <summary>The person's word *Not now*, or `daoris update --cancel`.</summary>
    public const string NotNow = "not-now";

    /// <summary>No driven session runs a turn, no conversation's turn and no run's plugin step is in flight.</summary>
    public const string Idle = "idle";

    /// <summary>The drain held new work for the machine's bound (<c>drain</c>) and holds it no longer.</summary>
    public const string Bound = "bound";

    /// <summary>Work began after the look read none, and the stop, which read it again, went back to the drain.</summary>
    public const string WorkBegan = "work-began";

    /// <summary>The build passed its check and the launcher started.</summary>
    public const string Checked = "checked";

    /// <summary>The stop did not end within <see cref="InstallUpdater.StopBound"/>.</summary>
    public const string StopBound = "stop-bound";

    /// <summary>The close did not take within <see cref="InstallUpdater.CloseBound"/>, or the close threw.</summary>
    public const string Close = "close";

    /// <summary>A check refused the build: the transition's detail is the check's code.</summary>
    public const string Refusal = "refusal";

    /// <summary>The launcher would not start.</summary>
    public const string Launcher = "launcher";

    /// <summary>A start of the application confirmed a swap.</summary>
    public const string Started = "started";

    /// <summary>A start found the launcher's journal ended in a roll-back or a refusal.</summary>
    public const string Swap = "swap";

    /// <summary>The person put the outcome away.</summary>
    public const string Dismissed = "dismissed";
}

/// <summary>One change of state, whole: from which state, to which, why, and for which build.</summary>
/// <param name="Detail">A word more where the why is general: the check's code for <see cref="UpdateWhy.Refusal"/>.</param>
public sealed record UpdateTransition(string From, string To, string Why, DateTimeOffset At, string? Build, string? Detail = null);

/// <summary>What a stopping application waits on, said each second: a code the page words, how long it has waited and the bound it is held to.</summary>
/// <param name="On"><see cref="UpdateWaits"/>'s words.</param>
public sealed record UpdateWaiting(string On, int Seconds, int Bound);

/// <summary>What a stop waits on.</summary>
public static class UpdateWaits
{
    public const string Place = "place";

    /// <summary>The staged build's check: its files, a going back and its start on a copy of the home.</summary>
    public const string Check = "check";

    /// <summary>The launcher's start, through the start gate.</summary>
    public const string Launcher = "launcher";

    /// <summary>The close the launcher waits on, once started.</summary>
    public const string Close = "close";
}

/// <summary>
/// An update's one state and the table of what may follow what. Every change goes through <see cref="Move"/>, which refuses one the table does
/// not list (a defect in the caller, thrown) and answers the transition it made, which the updater says once in the log and to the page.
/// Not thread-safe: the updater holds its gate.
/// </summary>
public sealed class UpdateFlow
{
    // Every state but `applying` is entered from any other; `applying` only from `stopping`, the one place the launcher is started.
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Allowed = UpdateStates.All.ToDictionary(
        from => from,
        from => (IReadOnlySet<string>)UpdateStates.All.Where(to => to != from && (to != UpdateStates.Applying || from == UpdateStates.Stopping)).ToHashSet(StringComparer.Ordinal),
        StringComparer.Ordinal);

    /// <summary>Whether <paramref name="to"/> may follow <paramref name="from"/>.</summary>
    public static bool May(string from, string to) => Allowed.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>The state now.</summary>
    public string State { get; private set; } = UpdateStates.None;

    /// <summary>The build the state is for, or null with none.</summary>
    public string? Build { get; private set; }

    /// <summary>When the state began.</summary>
    public DateTimeOffset? Since { get; private set; }

    /// <summary>The last transition; null until the first look moves the state.</summary>
    public UpdateTransition? Last { get; private set; }

    /// <summary>Why the state holds: the last transition's why.</summary>
    public string? Why => Last?.Why;

    /// <summary>
    /// Enter <paramref name="to"/> for <paramref name="build"/>; the transition, or null where the state and its build are already these
    /// (a look that changed nothing says nothing). <see cref="UpdateStates.Refused"/> says itself again for a build it refused anew.
    /// </summary>
    /// <exception cref="InvalidOperationException">The table does not let <paramref name="to"/> follow the state.</exception>
    public UpdateTransition? Move(string to, string why, string? build, DateTimeOffset at, string? detail = null)
    {
        if (to == State)
        {
            if (string.Equals(build, Build, StringComparison.Ordinal)) return null;
            Build = build;
            return Last = new UpdateTransition(State, to, why, at, build, detail);
        }

        if (!May(State, to)) throw new InvalidOperationException($"an update does not go from `{State}` to `{to}`.");
        var made = new UpdateTransition(State, to, why, at, build, detail);
        State = to;
        Build = build;
        Since = at;
        return Last = made;
    }
}
