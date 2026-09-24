using System.Diagnostics;

namespace Daoris.Driver;

/// <param name="SessionId">The record's id, when one was opened — null when nothing started.</param>
/// <param name="Message">What to tell the person: the ledger's sentence, or why nothing began.</param>
public sealed record ChatStart(string? SessionId, string Message);

/// <summary>
/// A conversation with an agent in one repository (D49 §3) — the session D46 built, entered by a
/// person instead of planned from a quest.
/// </summary>
/// <remarks>
/// <para><b>Nothing here is a chat loop.</b> Daoris pipes text and observes; the harness carries the
/// model and the conversation (D24, `model-decoupling`). This class spawns a process, relays the
/// person's lines into it, keeps the transcript and the console exactly as the driven path does, and
/// concludes the record from what it can see. A Daoris-owned loop calling a model API was rejected in
/// the design: it would duplicate what every harness already is, and produce sessions with no
/// doctrine path into them.</para>
///
/// <para><b>A chat may open on a dirty tree</b>, where a driven session may not (D46 §3). The
/// clean-tree rule exists because an unattended agent would entangle itself with somebody's work in
/// flight; in a conversation the person IS present, and it is their work in flight.</para>
///
/// <para><b>The lock is the ledger's</b>, not this class's: one active session per repository,
/// judged in one place for every door. This asks, and reports the refusal verbatim.</para>
/// </remarks>
public sealed class ChatRunner(
    ServiceClient service,
    AdapterSet adapters,
    string home,
    SessionProcesses processes,
    SessionOutput? output = null,
    HarnessRoster? harnesses = null)
{
    // Shared with the driver where a shell has both, so a probe is paid for once; its own where it
    // does not, which is the headless chat door's case.
    private readonly HarnessRoster _harnesses = harnesses ?? new HarnessRoster(adapters);

    /// <summary>
    /// Open a chat and put a harness behind it. The record is the service's; the process is this
    /// machine's, and never leaves it (D46 §7).
    /// </summary>
    /// <param name="onEnded">
    /// Called when the conversation ends, with the state the record took. The caller decides what that
    /// becomes — a console line, an IPC event, nothing at all.
    /// </param>
    /// <param name="profile">
    /// The per-session picker (D49 §4): which credential profile this conversation runs as. Null takes
    /// the workspace's default, then the machine's, then the harness's own configuration home.
    /// </param>
    /// <param name="ownTree">
    /// Open the conversation in a session tree of its own (D51) — the per-conversation choice, beside
    /// the repository's standing opt-in in the config. Either says yes; a chat is exactly the case the
    /// decision was made for, since the person is usually IN the root it would otherwise hold.
    /// </param>
    public async Task<ChatStart> StartAsync(
        string repository, string adapter, DriverConfig config,
        Func<string, string, Task>? onEnded = null, string? profile = null, bool ownTree = false,
        CancellationToken ct = default)
    {
        // Through the roster's LIVE set, not the one this runner was built with: the driver's tick
        // hands the roster the build's adapters plus whatever the plugins declare (D64), so a harness
        // declared since the shell started is a harness a conversation can run on now.
        var resolved = _harnesses.Adapters.Resolve(adapter);
        if (!resolved.Interactive)
        {
            // Asked BEFORE the record is opened: a session record for a conversation that could never
            // have happened would hold the repository and explain nothing.
            return new(
                null,
                $"the `{resolved.Name}` adapter does not hold conversations — it spawns an agent that "
                + "takes its target once and runs to completion.");
        }

        var snapshot = await service.SnapshotAsync(ct).ConfigureAwait(false);
        var known = snapshot.Repositories
            .FirstOrDefault(r => string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase));
        var root = known?.Root;
        if (string.IsNullOrWhiteSpace(root))
        {
            return new(
                null,
                $"`{repository}` has no checkout on this machine — a conversation runs IN a working "
                + "tree. `daoris connect` from inside it, or add it from Projects.");
        }

        // The harness and the account (D49 §4), asked before the record exists — the same place the
        // `interactive` question is asked, and for the same reason. The circle comes from the registry
        // row, which is the machine's own wiring and the only honest source for it (D48 §2).
        var selection = await _harnesses
            .SelectAsync(resolved.Name, config, known!.Workspace, profile, ct)
            .ConfigureAwait(false);
        if (!selection.Allowed) return new(null, selection.Refusal!);

        // A tree of the conversation's own, when asked for — per conversation, or as the repository's
        // standing choice (D51). Grown before the record, like every refusal above this line.
        TreeOpened? opened = null;
        if (ownTree || config.OpensOwnTree(repository))
        {
            try
            {
                opened = await new SessionTrees(home)
                    .OpenAsync(root!, known.Repository, known.Workspace, ct).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                return new(null, error.Message);
            }
        }

        var workTree = opened?.Path ?? root!;

        // Where the tree stands before the conversation begins (SURF6). A chat may open on a DIRTY
        // tree (D49 §3), so this is the commit — not the working state — and the review it feeds is
        // committed work only, for exactly that reason.
        var before = await WorkingTree.HeadAsync(workTree, ct).ConfigureAwait(false);

        var (sessionId, message) = await service
            // The tree the conversation runs in (D51) — its own where one was grown, the checkout
            // found above otherwise, stated by the side that is about to spawn into it.
            .OpenChatAsync(
                repository, resolved.Name, selection.Version, selection.Profile, workTree, before, ct)
            .ConfigureAwait(false);
        if (sessionId is null)
        {
            // Fresh and workless, so the clean path removes it; anything already in it is kept.
            if (opened is not null) await new SessionTrees(home).RemoveAsync(opened.Path, ct: ct).ConfigureAwait(false);
            return new(null, message);
        }

        var transcript = Path.Combine(home, "sessions", $"{sessionId}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

        Process process;
        string? rules = null;
        try
        {
            await service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);

            var info = resolved.PrepareChat(
                new ChatTarget(repository, workTree, service.BaseUrl),
                config.Commands.GetValueOrDefault(resolved.Name));
            if (resolved.Toolchain is { } toolchain)
            {
                HarnessProbe.Apply(
                    info, toolchain, selection.ProfileHome, selection.Binary, selection.ClaudeExecutable,
                    selection.Environment);
            }

            // What the conversation's agent may do (PERM1, D72) — the same union a driven session in
            // this repository is handed. The pipe door only: over the protocol door Daoris sends a
            // conversation no `session/new` of its own, so nothing there could carry it.
            if (resolved.TakesSettings && resolved.Wire == SessionWire.Pipe)
            {
                var file = PermissionRules.Load(home);
                rules = SpawnSettings.Write(
                    home, sessionId, PermissionRules.Compose(file, known?.Workspace, repository),
                    PermissionRules.GuardsTree(file) ? TreeGuard.For(home, workTree) : null);
                if (rules is not null) resolved.HandSettings(info, rules);
            }

            process = Process.Start(info)
                ?? throw new DriverException($"the {resolved.Name} adapter's process did not start");
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The record exists and must say what happened, or it sits at `starting` forever holding
            // the repository — the same rule the driven path follows.
            SpawnSettings.Remove(rules);
            await Conclude(sessionId, "failed", error.Message, onEnded).ConfigureAwait(false);
            return new(null, error.Message);
        }

        // The conversation outlives this call: the person types, the harness answers, and the record
        // moves when the process does. Watched on an unbound token deliberately — a chat is not ended
        // by the request that started it.
        _ = WatchAsync(sessionId, process, transcript, onEnded, rules);

        return new(sessionId, message);
    }

    /// <summary>Send a person's message to a live chat. False when there is nothing listening.</summary>
    public bool Say(string sessionId, string message) => processes.Send(sessionId, message);

    /// <param name="rules">The conversation's rules file (PERM1), which goes when the conversation does.</param>
    private async Task WatchAsync(
        string sessionId, Process process, string transcript, Func<string, string, Task>? onEnded,
        string? rules = null)
    {
        using var tracked = processes.Track(sessionId, process);
        try
        {
            var capture = Driver.CaptureAsync(process, transcript, sessionId, output, CancellationToken.None);
            await service.AdvanceAsync(sessionId, "working", transcript: transcript).ConfigureAwait(false);

            await process.WaitForExitAsync().ConfigureAwait(false);
            await capture.ConfigureAwait(false);

            // The person's stop outranks the observation, exactly as for a driven session: a killed
            // process leaves the same signals as one that ended on its own, and only this flag knows
            // whose decision it was. Otherwise a conversation simply ended — `completed`, with no
            // evidence claim beyond whatever its commits already say.
            var stopped = processes.WasStopRequested(sessionId);
            await Conclude(
                sessionId,
                stopped ? "stopped" : "completed",
                stopped
                    ? "the person ended the conversation."
                    : "the conversation ended; its commits are its record.",
                onEnded).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            await Conclude(sessionId, "failed", error.Message, onEnded).ConfigureAwait(false);
        }
        finally
        {
            output?.Close(sessionId);
            SpawnSettings.Remove(rules);
        }
    }

    private async Task Conclude(
        string sessionId, string state, string note, Func<string, string, Task>? onEnded)
    {
        try
        {
            await service.AdvanceAsync(sessionId, state, note: note).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException)
        {
            // Best-effort by construction: the host may already be gone on the same shutdown, and the
            // first failure is the report.
        }

        if (onEnded is not null)
        {
            try
            {
                await onEnded(sessionId, state).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A listener's failure is its own; the record is already written.
            }
        }
    }
}
