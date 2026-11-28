using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>What the drain waits on: driven sessions running a turn, conversations with a turn in flight, and runs' plugin steps.</summary>
public sealed record UpdateWork(int Driven, int Turns)
{
    /// <summary>The runs' plugin steps in flight the loop's evaluation runtime governs, their plugin up (<see cref="PluginStepsInFlight"/>).</summary>
    public int Steps { get; init; }

    public bool Idle => InstallUpdate.Idle(Driven, Turns, Steps);
}

/// <summary>The staged build, as the banner names it: no path of this machine's.</summary>
public sealed record UpdateBuild(string Id, string Version, string? Commit, DateTimeOffset? At);

/// <summary>Why the staged build is not being installed: the check's code, which the page words, and the driver's sentence.</summary>
public sealed record UpdateProblem(string Code, string Message);

/// <summary>How a swap ended: its phase (installed, rolled back or refused), the build, and the reason.</summary>
public sealed record UpdateOutcome(string Phase, string? Build, string? Version, string? Commit, string? Reason, string? Detail);

/// <summary>Where an install's update stands, whole: what the banner renders, <c>UPDATE_STATE</c> carries and <c>daoris update</c> prints (through <c>update/state.json</c>).</summary>
/// <param name="State"><see cref="UpdateStates"/>'s words.</param>
/// <param name="Mode">The mode that holds for the staged build (<see cref="UpdateMode"/>), or null when none is staged.</param>
/// <param name="Outcome">The last swap, said once at the start after it and kept until the banner's *Dismiss*.</param>
/// <param name="Last">
/// The last swap as the journal records it, told or not, on every state: what Settings → Updates' *Last update* row says
/// and the terminal's plain <c>daoris update</c> says. Null with no journal, and while a swap is under way.
/// </param>
/// <param name="Since">
/// When the drain for the staged build began, while it drains and as it stops: beside <paramref name="Driven"/> and
/// <paramref name="Turns"/>, what lets the page say how long it has waited. No end is said, since the drain ends only when the work does.
/// Null with no drain, and for *Update now* asked before any.
/// </param>
public sealed record UpdateState(
    string State, UpdateBuild? Staged, string? Mode, int Driven, int Turns, UpdateProblem? Problem, UpdateOutcome? Outcome,
    UpdateOutcome? Last, DateTimeOffset? Since = null)
{
    /// <summary>The plugin steps in flight the drain waits on (<see cref="UpdateWork.Steps"/>), while it drains and as it stops.</summary>
    public int Steps { get; init; }

    /// <summary>When the drain stops holding new work, by the machine's bound (<see cref="DrainSetting"/>); null with no drain.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>Why the state holds: the why of the transition that entered it (<see cref="UpdateWhy"/>).</summary>
    public string? Why { get; init; }

    /// <summary>When the state began.</summary>
    public DateTimeOffset? Entered { get; init; }

    /// <summary>The last change of state, whole.</summary>
    public UpdateTransition? Transition { get; init; }

    /// <summary>What a stop or a close waits on, with how long and the bound it is held to; null in every other state.</summary>
    public UpdateWaiting? Waiting { get; init; }
}
