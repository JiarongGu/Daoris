using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The turns a protocol-door session's harness starts by itself (BGWAIT2): when its background work ends, Claude Code wakes its
/// agent with no prompt open (CONSOLE2a), and <c>claude-agent-acp</c> answers no prompt for that turn. What the session's turn
/// that waited on the work waits for next: that woken turn's end, said on the wire, or the quiet that says none began.
/// </summary>
/// <remarks>
/// <para><b>The woken turn's end is the adapter's word.</b> <c>claude-agent-acp</c> 0.79.0 skips a result whose origin is
/// autonomous (<c>acp-agent.js</c> l.233-239: a task notification, a peer, a coordinator, an observer) when it settles turns
/// (l.3546-3573), and absorbs the idle after it (l.2864-2875); the one thing it sends for it is the result's
/// <c>usage_update</c>, carrying the origin in <c>_meta["_claude/origin"]</c> (l.3505-3521), and only where it measured the
/// turn's usage. A prompt sent into that turn is taken by it, and is never answered: the install's session waited 48 minutes on
/// Daoris's go-on for the timeout (2026-10-19).</para>
///
/// <para><b>Read in the wire's order</b>, on the reader's thread, from the moment the session opens: a wake faster than the
/// driver's reading of its quest is never missed. A woken turn is one that began after the work's last ending, with no prompt
/// open, by the agent saying, thinking, calling a tool or planning, and only its own end ends it. A turn that began before the
/// last ending is not the wake for it, and an end said twice is one.</para>
///
/// <para><b>Silence is never an end.</b> A woken turn that began and whose end is never said waits for the session's own
/// bounds, the timeout among them, as BGWAIT1's wait did. Quiet counts only while no turn runs and no prompt is open: then
/// none began, and Daoris's own go-on is a fresh turn the adapter answers.</para>
/// </remarks>
internal sealed class AcpWake
{
    /// <summary>
    /// How long the session waits, once its background work ended, for its harness to wake its agent before Daoris's own go-on
    /// goes. The wake was measured at 1.6 and 2.4 s after the work ended (CONSOLE2a); a go-on sent into a wake that came later
    /// is never answered, which costs the timeout, so the bound is long.
    /// </summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(60);

    /// <summary>The origins <c>claude-agent-acp</c> 0.79.0 calls autonomous (<c>acp-agent.js</c> l.233-239).</summary>
    private static readonly HashSet<string> Autonomous =
        new(["task-notification", "peer", "coordinator", "observer", "observer-activity"], StringComparer.Ordinal);

    private readonly object _gate = new();

    // Positions in the session's own updates, counted from one as they are read; zero is none.
    private long _read;
    private long _ending;
    private long _began;
    private long _woke;
    private TaskCompletionSource _changed = NewSignal();

    /// <summary>
    /// Read one of the session's own updates, never a subagent's, in the wire's order. <paramref name="prompted"/> says whether
    /// a prompt is open as it arrives: what the agent says then is that prompt's turn, never a woken one.
    /// </summary>
    public void Take(JsonElement update, bool prompted)
    {
        lock (_gate)
        {
            _read++;
            switch (AcpSession.Kind(update))
            {
                case "async_task_state_update" when Json.Text(update, "state") is { } state and not ("running" or "paused"):
                    _ending = _read;
                    break;
                case "usage_update" when Woken(update):
                    // The turn running now ended, where one runs; said again, or with none running, it ends nothing.
                    if (_began > 0) (_woke, _began) = (_began, 0);
                    break;
                case { } kind when _began == 0 && !prompted && Turns(kind):
                    _began = _read;
                    break;
            }
        }

        Stir();
    }

    /// <summary>Something besides an update changed what the wait reads: a prompt was answered.</summary>
    public void Stir()
    {
        TaskCompletionSource changed;
        lock (_gate)
        {
            changed = _changed;
            _changed = NewSignal();
        }

        changed.TrySetResult();
    }

    /// <summary>
    /// After the session's background work ended: true once a turn its harness woke after the work's last ending has said its
    /// end; false once <paramref name="quiet"/> has passed with no turn running and no prompt open, so none began; null where
    /// <paramref name="ended"/> completes first, the agent's stream over. A woken turn that never says its end is waited for
    /// until the stream ends or <paramref name="ct"/> is cancelled.
    /// </summary>
    /// <param name="prompted">Whether a prompt is open now: the quiet does not run while one is.</param>
    public async Task<bool?> AfterAsync(Func<bool> prompted, TimeSpan quiet, Task ended, CancellationToken ct)
    {
        while (true)
        {
            // The turn's end comes before the stream's in the wire's order, and the reader takes every update before the stream's
            // end completes: whether the stream is over is read before the turn's end is, never after, so a turn's end the reader
            // took and read past to the stream's end is still read here as the turn's end (BGWAIT2c).
            var over = ended.IsCompleted;
            Task changed;
            bool running;
            lock (_gate)
            {
                if (_woke > _ending) return true;
                changed = _changed.Task;
                running = _began > 0;
            }

            if (over) return null;
            var waits = running || prompted();

            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var silence = Task.Delay(waits ? Timeout.InfiniteTimeSpan : quiet, bounded.Token);
            var first = await Task.WhenAny(changed, ended, silence).ConfigureAwait(false);
            await bounded.CancelAsync().ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            if (first == silence && !changed.IsCompleted && !prompted()) return false;
        }
    }

    /// <summary>
    /// Completes once no turn runs (DONEEND1): none its harness started is running with its end unsaid, and no prompt is open. What a
    /// session whose quest no longer waits on its background work waits for before it ends, so a turn is never cut: a woken turn
    /// whose end is never said holds it, as it holds <see cref="AfterAsync"/>, until the session's own bounds.
    /// </summary>
    /// <param name="prompted">Whether a prompt is open now: its answer is that turn's end.</param>
    public async Task IdleAsync(Func<bool> prompted, CancellationToken ct)
    {
        while (true)
        {
            // The signal is taken before what it guards is read, so a change between the two wakes the wait rather than being lost.
            Task changed;
            bool running;
            lock (_gate)
            {
                changed = _changed.Task;
                running = _began > 0;
            }

            if (!running && !prompted()) return;
            await changed.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>Whether a <c>usage_update</c> carries an autonomous result's origin: an object whose kind is one of those.</summary>
    private static bool Woken(JsonElement update) =>
        update.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object
        && meta.TryGetProperty("_claude/origin", out var origin) && origin.ValueKind == JsonValueKind.Object
        && Json.Text(origin, "kind") is { } kind && Autonomous.Contains(kind);

    /// <summary>What a turn says while it runs: the agent's words and thinking, its calls and its plan.</summary>
    private static bool Turns(string kind) =>
        kind.StartsWith("agent_", StringComparison.Ordinal) || kind.StartsWith("tool_call", StringComparison.Ordinal) || kind == "plan";

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
