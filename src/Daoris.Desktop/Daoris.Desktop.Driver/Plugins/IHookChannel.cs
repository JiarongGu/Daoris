using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One plugin's side of the wire, as the host sees it — what a process gives once it speaks, and what a
/// test can hand in without one. The module's port (STRUCT3a): the hook set and the landing ask through it, a test's
/// fake and <see cref="HookPeer"/> speak it, and <see cref="HookProcess"/> is the real one.
/// </summary>
public interface IHookChannel : IAsyncDisposable
{
    /// <summary>The points the plugin actually listens on, as it answered `initialize`.</summary>
    IReadOnlyList<string> Points { get; }

    /// <summary>Whether the other side is still there. A channel that is not is not asked.</summary>
    bool Alive { get; }

    /// <summary>The process's exit code once it has exited, where there is a process to have one (PLUGUI1d).</summary>
    int? ExitCode => null;

    Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct);

    Task EndedAsync(object payload, CancellationToken ct);

    /// <summary>
    /// A landed branch's pull request (PLUGHOOK1a, D148), answered with its state. A channel that has no such answer fails as
    /// the plugin's error, which keeps everything where it is.
    /// </summary>
    Task<PullRequestState> StateAsync(object payload, CancellationToken ct) =>
        Task.FromException<PullRequestState>(PluginFailures.Mark(
            new DriverException($"this channel does not answer `{HookPoints.State}`."), PluginEvents.Errored));

    /// <summary>
    /// What moved of what the driver holds (STATEHUB1g, D169 point 9), its answer not read. A channel that has no such answer fails
    /// as the plugin's error, which an observation contains.
    /// </summary>
    Task ChangedAsync(object payload, CancellationToken ct) =>
        Task.FromException(PluginFailures.Mark(
            new DriverException($"this channel does not answer `{HookPoints.StateChanged}`."), PluginEvents.Errored));

    /// <summary>The capabilities the plugin's handshake named that this build knows (FABRIC-PERFORM1a): <see cref="WorkWire.Capability"/>.</summary>
    IReadOnlyList<string> Capabilities => [];

    /// <summary>
    /// Where the plugin's <see cref="HookPoints.Step"/> events go (FABRIC-PERFORM1a, §C4): each event's params, or null for a frame dropped
    /// unread for its length. Only the lease's loop listens, on a process it started; a channel nobody listens to drops every event, as a
    /// landing's process does, and a channel with no events has nothing to give.
    /// </summary>
    void Listen(Action<JsonElement?>? steps)
    {
    }

    /// <summary>The awaited set (§C4), its answer waited for and never read. A channel that has no such answer fails as the plugin's error.</summary>
    Task WatchAsync(WorkWatchFrame frame, CancellationToken ct) =>
        Task.FromException(PluginFailures.Mark(
            new DriverException($"this channel does not answer `{HookPoints.Watch}`."), PluginEvents.Errored));

    /// <summary>One attempt (§D6), answered with its receipt; the outcome comes later as an event. Fails where receipt is not acknowledged.</summary>
    Task PerformAsync(WorkPerformCall call, CancellationToken ct) =>
        Task.FromException(PluginFailures.Mark(
            new DriverException($"this channel does not answer `{HookPoints.Perform}`."), PluginEvents.Errored));

    /// <summary>What became of one attempt (§D6). Fails where the answer is not one.</summary>
    Task<WorkReconcileAnswer> ReconcileStepAsync(WorkReconcileCall call, CancellationToken ct) =>
        Task.FromException<WorkReconcileAnswer>(PluginFailures.Mark(
            new DriverException($"this channel does not answer `{PluginSteps.ReconcilePoint}`."), PluginEvents.Errored));

    /// <summary>One receipt's removal asked of the plugin that created it (FABRIC-PERFORM1d, §D3). Fails where the answer is not a disposition.</summary>
    Task<WorkCleanupAnswer> CleanupAsync(WorkCleanupCall call, CancellationToken ct) =>
        Task.FromException<WorkCleanupAnswer>(PluginFailures.Mark(
            new DriverException($"this channel does not answer `{PluginSteps.CleanupPoint}`."), PluginEvents.Errored));
}
