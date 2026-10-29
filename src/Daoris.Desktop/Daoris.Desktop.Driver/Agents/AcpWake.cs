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
/// none began, and Daoris's own go-on is a fresh turn the adapter answers. One exception, where the quest no longer waits
/// (RUNENDS1, <see cref="Judge"/>): a turn silent for the bound is left, its silence read from what it did, never from a signal.</para>
///
/// <para><b>Every change is read under one gate</b> (RUNENDS1 after review, finding 4): an update, an answer, a request and a hold
/// each change what the waits read and replace their signal in the same step, so a wait that reads under the gate never takes a
/// change it was not yet signalled for as silence.</para>
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

    /// <summary>
    /// The clock every wait here is read off (RUNENDS1, <c>.claude/knowledge/held-clocks.md</c>): the wake's quiet and the quiet a turn
    /// whose quest no longer waits is left after. The machine's in production; the session hands its own (<see cref="AcpSession.Clock"/>).
    /// </summary>
    public TimeProvider Clock { get; set; } = TimeProvider.System;

    private readonly object _gate = new();

    // Positions in the session's own updates, counted from one as they are read; zero is none.
    private long _read;
    private long _ending;
    private long _began;
    private long _woke;

    // BGWAIT3: the agent's own last step, and the last update Daoris's go-on said everything up to.
    private long _step;
    private long _told;

    // BGWAIT3: where each piece of work's last ending was read, by the task's id.
    private readonly Dictionary<string, long> _endings = new(StringComparer.Ordinal);
    private TaskCompletionSource _changed = NewSignal();

    // RUNENDS1: what a closed quest's turn is judged quiet by. When the agent last did anything a turn does, on the clock; the calls the
    // running turn has open; the permission requests held for the person.
    private DateTimeOffset _spoke = DateTimeOffset.MinValue;
    private readonly HashSet<string> _calls = new(StringComparer.Ordinal);
    private int _holding;

    /// <summary>
    /// Read one of the session's own updates, never a subagent's, in the wire's order. <paramref name="prompted"/> says whether
    /// a prompt is open as it arrives: what the agent says then is that prompt's turn, never a woken one.
    /// </summary>
    /// <remarks>
    /// RUNENDS1: what the turn says, thinks, calls or plans is its speaking, a call a subagent makes on the session's own id among them,
    /// and is when its quiet counts from; its background work's progress and endings, and its meters, are not. Its calls are open from
    /// their announcement, or an update <c>pending</c> or <c>in_progress</c>, until any other status, and none outlives its turn.
    /// </remarks>
    public void Take(JsonElement update, bool prompted)
    {
        TaskCompletionSource changed;
        lock (_gate)
        {
            _read++;
            var kind = AcpSession.Kind(update);
            if (kind is "tool_call" or "tool_call_update" && !OfSubagent(update)) _step = _read;
            if (kind is { } speaks && Turns(speaks)) _spoke = Clock.GetUtcNow();
            if (kind is "tool_call" or "tool_call_update") Track(update, kind);
            switch (kind)
            {
                case "async_task_state_update" when Json.Text(update, "state") is { } state and not ("running" or "paused"):
                    _ending = _read;
                    if (Json.Text(update, "asyncTaskId") is { Length: > 0 } id) _endings[id] = _read;
                    break;
                case "usage_update" when Woken(update):
                    // The turn running now ended, where one runs; said again, or with none running, it ends nothing.
                    if (_began > 0)
                    {
                        (_woke, _began) = (_began, 0);
                        _calls.Clear();
                    }

                    break;
                case { } said when _began == 0 && !prompted && Turns(said):
                    _began = _read;
                    break;
            }

            changed = Swap();
        }

        changed.TrySetResult();
    }

    /// <summary>A call opened or closed by <paramref name="update"/> (RUNENDS1); an update that says no status keeps what the call was. Under the gate.</summary>
    private void Track(JsonElement update, string kind)
    {
        if (Json.Text(update, "toolCallId") is not { Length: > 0 } id) return;
        switch (Json.Text(update, "status"))
        {
            case "pending" or "in_progress":
                _calls.Add(id);
                break;
            case null:
                if (kind == "tool_call") _calls.Add(id);
                break;
            default:
                _calls.Remove(id);
                break;
        }
    }

    /// <summary>
    /// The work whose ending its agent was never handed (BGWAIT3), by the tasks' ids: each ending read after the agent's last step,
    /// after the last turn its harness woke, and after Daoris's last go-on. Claude Code hands the agent an ending at its next step
    /// in the turn, or wakes it with the ending once the turn is over; one said after the turn's last step reaches it only by that
    /// wake, so a turn that ends then is owed one, whatever the agent last said of the work.
    /// </summary>
    public IReadOnlyList<string> Unseen
    {
        get
        {
            lock (_gate)
            {
                var seen = Math.Max(_step, Math.Max(_woke, _told));
                return [.. _endings.Where(ending => ending.Value > seen).Select(ending => ending.Key)];
            }
        }
    }

    /// <summary>Whether a turn its harness started is running, its end unsaid (AGENTERRORS1a: a transient episode never prompts into one).</summary>
    public bool Running
    {
        get
        {
            lock (_gate) return _began > 0;
        }
    }

    /// <summary>Completes once a turn its harness started is running (AGENTERRORS1a: it ends a transient episode's wait, never the episode).</summary>
    public async Task WokeAsync(CancellationToken ct)
    {
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                if (_began > 0) return;
                changed = _changed.Task;
            }

            await changed.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A prompt was answered (BGWAIT2): the waits read again whether theirs may run. Its turn is over, and so are the calls it had open,
    /// and its answer is the agent's last word for a closed quest's quiet (RUNENDS1).
    /// </summary>
    public void Answered()
    {
        TaskCompletionSource changed;
        lock (_gate)
        {
            _calls.Clear();
            _spoke = Clock.GetUtcNow();
            changed = Swap();
        }

        changed.TrySetResult();
    }

    /// <summary>The agent asked the client something, a permission (RUNENDS1): its turn did something, so its quiet counts from here.</summary>
    public void Asked() => Moved(0);

    /// <summary>A permission request is held for the person (RUNENDS1): the turn waits on them, and no quiet runs while it does.</summary>
    public void Held() => Moved(1);

    /// <summary>A held request was answered or released (RUNENDS1): the turn goes on, and its quiet counts from here.</summary>
    public void Released() => Moved(-1);

    private void Moved(int holds)
    {
        TaskCompletionSource changed;
        lock (_gate)
        {
            _holding = Math.Max(0, _holding + holds);
            _spoke = Clock.GetUtcNow();
            changed = Swap();
        }

        changed.TrySetResult();
    }

    /// <summary>
    /// The signal the waits hold, replaced in the step that changed what they read (RUNENDS1 after review, finding 4): a wait that reads
    /// it under the gate is never handed a change it was not signalled for. Under the gate; the old one is completed outside it.
    /// </summary>
    private TaskCompletionSource Swap()
    {
        var changed = _changed;
        _changed = NewSignal();
        return changed;
    }

    /// <summary>
    /// After the session's background work ended: true once a turn its harness woke after the work's last ending has said its
    /// end; false once <paramref name="quiet"/> has passed with no turn running and no prompt open, so none began, and every ending
    /// read so far counts as told by the go-on the caller then sends (BGWAIT3); null where
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
            var silence = Task.Delay(waits ? Timeout.InfiniteTimeSpan : quiet, Clock, bounded.Token);
            var first = await Task.WhenAny(changed, ended, silence).ConfigureAwait(false);
            await bounded.CancelAsync().ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            if (first == silence && !prompted())
            {
                lock (_gate)
                {
                    // Nothing was read through the quiet: the go-on the caller sends tells the agent everything read so far
                    // (BGWAIT3), so no ending it names is owed a wake again.
                    if (_changed.Task == changed)
                    {
                        _told = _read;
                        return false;
                    }
                }
            }
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

    /// <summary>
    /// What a session whose quest no longer waits reads at the moment it would end (RUNENDS1, D83's note), in one look under the gate:
    /// <see cref="QuestDone.Idle"/> where no turn its harness started runs and no prompt is open; <see cref="QuestDone.Quiet"/> where
    /// one does, it has no call open and no request held for the person, and it did nothing for <paramref name="quiet"/>, counted from
    /// the later of <paramref name="since"/> and its last word; else <see cref="QuestDone.Busy"/>, with how long the quiet has left, or
    /// null while a call is open or a request held. Beside it, the signal the next change completes.
    /// </summary>
    /// <remarks>
    /// The turn an agent's own scheduled wakeup starts says its end by no origin the adapter calls autonomous, so its end may never be
    /// said, and a wait for no turn to run would hold the session until the timeout. A turn that speaks, works or asks is never cut.
    /// </remarks>
    public (QuestDone Now, Task Changed, TimeSpan? Left) Judge(Func<bool> prompted, DateTimeOffset since, TimeSpan quiet)
    {
        lock (_gate)
        {
            var changed = _changed.Task;
            if (_began == 0 && !prompted()) return (QuestDone.Idle, changed, null);
            if (_calls.Count > 0 || _holding > 0) return (QuestDone.Busy, changed, null);
            var left = (since > _spoke ? since : _spoke) + quiet - Clock.GetUtcNow();
            return left > TimeSpan.Zero ? (QuestDone.Busy, changed, left) : (QuestDone.Quiet, changed, null);
        }
    }

    /// <summary>Completes once <see cref="Judge"/> reads <see cref="QuestDone.Idle"/> or <see cref="QuestDone.Quiet"/> (RUNENDS1).</summary>
    public async Task IdleOrQuietAsync(Func<bool> prompted, DateTimeOffset since, TimeSpan quiet, CancellationToken ct)
    {
        while (true)
        {
            var (now, changed, left) = Judge(prompted, since, quiet);
            if (now != QuestDone.Busy) return;

            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var silence = Task.Delay(left ?? Timeout.InfiniteTimeSpan, Clock, bounded.Token);
            await Task.WhenAny(changed, silence).ConfigureAwait(false);
            await bounded.CancelAsync().ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
        }
    }

    /// <summary>Whether a <c>usage_update</c> carries an autonomous result's origin: an object whose kind is one of those.</summary>
    private static bool Woken(JsonElement update) =>
        update.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object
        && meta.TryGetProperty("_claude/origin", out var origin) && origin.ValueKind == JsonValueKind.Object
        && Json.Text(origin, "kind") is { } kind && Autonomous.Contains(kind);

    /// <summary>
    /// Whether a call is a subagent's, stamped with the call that spawned it (<c>_meta.claudeCode.parentToolUseId</c>,
    /// docs/2026-09-28-console2-streams-evidence.md): where streams are not kept, a subagent's calls arrive on the session's own
    /// id, and a step of the subagent's hands the session's agent nothing.
    /// </summary>
    private static bool OfSubagent(JsonElement update) =>
        update.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object
        && meta.TryGetProperty("claudeCode", out var claude) && claude.ValueKind == JsonValueKind.Object
        && Json.Text(claude, "parentToolUseId") is { Length: > 0 };

    /// <summary>What a turn says while it runs: the agent's words and thinking, its calls and its plan.</summary>
    private static bool Turns(string kind) =>
        kind.StartsWith("agent_", StringComparison.Ordinal) || kind.StartsWith("tool_call", StringComparison.Ordinal) || kind == "plan";

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>What a session whose quest no longer waits reads at the moment it would end (RUNENDS1, <see cref="AcpWake.Judge"/>).</summary>
internal enum QuestDone
{
    /// <summary>No turn its harness started runs and no prompt is open: the session ends as any does.</summary>
    Idle,

    /// <summary>A turn runs, or a prompt is open, and it did nothing for the bound: it is left.</summary>
    Quiet,

    /// <summary>The turn speaks, has a call open or waits on the person: it is the agent's, and is waited for.</summary>
    Busy,
}
