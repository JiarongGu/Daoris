using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a session on the protocol door runs beside itself (CONSOLE2): each subagent its harness
/// spawns, and each piece of background work it starts, as a console stream of its own.
/// </summary>
/// <remarks>
/// <para><b>Read off the wire, never inferred</b> (docs/2026-09-28-console2-streams-evidence.md). A
/// subagent is announced by <c>subagent_spawned</c>, speaks under its own <c>params.sessionId</c>, and
/// ends with <c>subagent_state_update</c>. A task is announced by <c>async_task_spawned</c>, names the
/// file its output goes to in <c>async_task_progress</c>, and ends with <c>async_task_state_update</c>.
/// Nothing of a task's output is on the wire, so its stream is that file, read as it grows.</para>
///
/// <para><b>Beside the session, never in it.</b> A child's words and tools go to its own stream, not
/// the session's console, record or transcript. The transcript is where the driver reads what the
/// session last said to the person (a parked card's question), and a subagent's words there would be
/// quoted as the session's. The session's console says each one started and how it ended, and its
/// record holds a subagent as one card, because declaring the capability took the <c>Agent</c> call
/// that would have been its card off the session's wire.</para>
///
/// <para><b>A session's end is theirs.</b> Nothing the session started outlives it (ORPHAN1's job
/// object), so whatever is still open when it ends is ended with it, and says so.</para>
/// </remarks>
internal sealed class AcpStreams(
    SessionStreams streams, AcpConsole root, Action<SessionEvent> emit, TimeSpan quiet, TimeSpan? tailEvery = null)
{
    /// <summary>
    /// How long a task's ending waits for a later word before it is said. The adapter says
    /// <c>stopped</c> and then <c>completed</c> in the same millisecond for a task that finished on its
    /// own, and the last is the ending (CONSOLE2a).
    /// </summary>
    internal static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(250);

    /// <summary>What the kind a subagent's card wears: the adapter's own for the <c>Agent</c> call it replaces.</summary>
    private const string SubagentToolKind = "think";

    private sealed class Child(string id, string streamId, string name, bool onRoot, AcpConsole console)
    {
        public string Id { get; } = id;
        public string StreamId { get; } = streamId;
        public string Name { get; } = name;

        /// <summary>Spawned by the session itself, so its card is in the session's record.</summary>
        public bool OnRoot { get; } = onRoot;

        public AcpConsole Console { get; } = console;
        public bool Ended { get; set; }
    }

    private sealed class Background(string streamId, string name)
    {
        public string StreamId { get; } = streamId;
        public string Name { get; } = name;
        public OutputTail? Tail { get; set; }

        /// <summary>Its ending has taken the tail for its last read: a file named after that is not read.</summary>
        public bool TailTaken { get; set; }

        public string? State { get; set; }
        public bool Finishing { get; set; }
        public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Child> _children = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Background> _tasks = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _settling = new();

    /// <summary>
    /// Take one update if it belongs to a stream: a stream's own lifecycle, or a child's words. False
    /// leaves it the session's, as every update was before CONSOLE2.
    /// </summary>
    /// <param name="from">The update's <c>params.sessionId</c>.</param>
    /// <param name="rootId">The session's own id on the wire.</param>
    public bool Take(string? from, string? rootId, JsonElement update)
    {
        switch (AcpSession.Kind(update))
        {
            case "subagent_spawned":
                return Spawned(from, rootId, update);
            case "subagent_state_update":
                return SubagentEnded(update);
            case "async_task_spawned":
                return TaskSpawned(update);
            case "async_task_progress":
                // Known, and not the conversation: the file it names is the stream.
                if (TaskOf(update) is { } progressed) Watch(progressed, Str(update, "outputFilePath"));
                return true;
            case "async_task_state_update":
                return TaskState(update);
        }

        Child? child;
        lock (_gate)
        {
            if (from is null || !_children.TryGetValue(from, out child)) return false;
        }

        child.Console.Update(update, AcpSession.Map(update));
        return true;
    }

    /// <summary>
    /// End everything still open, because the session has ended: each says so on its stream, and the
    /// session's console and record say how. Waits for every task's last read. Once is enough, and
    /// again is nothing.
    /// </summary>
    public async Task EndAllAsync()
    {
        _settling.Cancel();

        List<Child> open;
        List<Background> unfinished = [];
        List<Task> waits = [];
        lock (_gate)
        {
            open = [.. _children.Values.Where(child => !child.Ended)];
            foreach (var child in open) child.Ended = true;
            foreach (var task in _tasks.Values)
            {
                if (!task.Finishing)
                {
                    task.Finishing = true;
                    task.State = SessionStream.SessionEnded;
                    unfinished.Add(task);
                }

                waits.Add(task.Done.Task);
            }
        }

        foreach (var child in open)
        {
            child.Console.End();
            streams.Line(child.StreamId, EndedWithSession);
            Ended(SessionStreamKind.Subagent, child.StreamId, child.Name, SessionStream.SessionEnded);
            if (child.OnRoot) Card(child.Id, child.StreamId, SessionStream.SessionEnded);
        }

        foreach (var task in unfinished) _ = FinishAsync(task, settle: false);
        await Task.WhenAll(waits).ConfigureAwait(false);

        List<Child> all;
        lock (_gate) all = [.. _children.Values];
        foreach (var child in all) child.Console.Dispose();
    }

    private const string EndedWithSession = "— ended with its session";

    private bool Spawned(string? from, string? rootId, JsonElement update)
    {
        // An announcement this build cannot read is kept raw by the session, never dropped.
        if (Str(update, "subagentSessionId") is not { Length: > 0 } id) return false;
        var name = Str(update, "name") is { Length: > 0 } called ? called : id;
        var streamId = $"subagent/{id}";
        var onRoot = from is null || from == rootId;

        lock (_gate)
        {
            // Announced once, whatever the wire repeats.
            if (_children.ContainsKey(id)) return true;
            _children[id] = new Child(id, streamId, name, onRoot, new AcpConsole(text => streams.Line(streamId, text), quiet));
        }

        streams.Open(new SessionStream(streamId, SessionStreamKind.Subagent, name));
        Say($"→ subagent: {AcpSession.OneLine(name)}");
        if (onRoot)
        {
            emit(new SessionEvent
            {
                Kind = SessionEventKind.Tool,
                Id = id,
                Stream = streamId,
                Title = name,
                ToolKind = SubagentToolKind,
                Status = "in_progress",
                Content = Str(update, "task") is { Length: > 0 } task ? [new ToolContent("text", Text: task)] : null,
            });
        }

        return true;
    }

    private bool SubagentEnded(JsonElement update)
    {
        if (Str(update, "subagentSessionId") is not { } id) return false;
        var state = Str(update, "state") ?? "ended";

        Child? child;
        lock (_gate)
        {
            // A child never announced is the wire saying something this build cannot place: raw.
            if (!_children.TryGetValue(id, out child)) return false;
            if (child.Ended) return true;
            child.Ended = true;
        }

        child.Console.End();
        Ended(SessionStreamKind.Subagent, child.StreamId, child.Name, state);
        if (child.OnRoot) Card(child.Id, child.StreamId, state);
        return true;
    }

    private bool TaskSpawned(JsonElement update)
    {
        if (Str(update, "asyncTaskId") is not { Length: > 0 } id) return false;
        var name = Str(update, "name") is { Length: > 0 } called ? called : id;
        var streamId = $"task/{id}";

        Background task;
        lock (_gate)
        {
            if (_tasks.ContainsKey(id)) return true;
            task = _tasks[id] = new Background(streamId, name);
        }

        // Stoppable only when the harness says so (CONSOLE3a): its word, never a guess from the task's kind.
        var canStop = update.TryGetProperty("canStop", out var stoppable) && stoppable.ValueKind == JsonValueKind.True;
        streams.Open(new SessionStream(streamId, SessionStreamKind.Task, name, canStop));
        Say($"→ background: {AcpSession.OneLine(name)}");
        Watch(task, Str(update, "outputFilePath"));
        return true;
    }

    private bool TaskState(JsonElement update)
    {
        if (TaskOf(update) is not { } task) return true;
        Watch(task, Str(update, "outputFilePath"));

        var state = Str(update, "state");
        if (state is null or "running" or "paused") return true;

        bool start;
        lock (_gate)
        {
            // The last ending wins, until it has been said.
            if (task.Finishing && task.State == SessionStream.SessionEnded) return true;
            task.State = state;
            start = !task.Finishing;
            task.Finishing = true;
        }

        if (start) _ = FinishAsync(task, settle: true);
        return true;
    }

    /// <summary>Begin reading a task's output file, once, from the first update that names it.</summary>
    private void Watch(Background task, string? path)
    {
        if (path is not { Length: > 0 }) return;
        lock (_gate)
        {
            if (task.Tail is not null || task.TailTaken) return;
            task.Tail = new OutputTail(path, text => streams.Line(task.StreamId, text), tailEvery);
        }
    }

    /// <summary>
    /// A task's ending: wait for the wire's last word, read the file's last lines, then say how it
    /// ended. Never throws: an ending that failed to be said must not take the session's reader down.
    /// </summary>
    private async Task FinishAsync(Background task, bool settle)
    {
        try
        {
            if (settle)
            {
                try
                {
                    await Task.Delay(Settle, _settling.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // The session ended first: say it now.
                }
            }

            OutputTail? tail;
            lock (_gate)
            {
                tail = task.Tail;
                task.TailTaken = true;
            }
            if (tail is not null) await tail.FinishAsync().ConfigureAwait(false);

            string state;
            lock (_gate) state = task.State ?? SessionStream.SessionEnded;
            if (state == SessionStream.SessionEnded) streams.Line(task.StreamId, EndedWithSession);
            Ended(SessionStreamKind.Task, task.StreamId, task.Name, state);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The console's own failure; the harness's file is still its record.
        }
        finally
        {
            task.Done.TrySetResult();
        }
    }

    private Background? TaskOf(JsonElement update)
    {
        if (Str(update, "asyncTaskId") is not { } id) return null;
        lock (_gate) return _tasks.GetValueOrDefault(id);
    }

    /// <summary>A stream ended: its own state, and a line on the session's console saying how.</summary>
    private void Ended(string kind, string streamId, string name, string state)
    {
        streams.End(streamId, state);
        var what = kind == SessionStreamKind.Subagent ? "subagent" : "background";
        name = AcpSession.OneLine(name);
        Say(state switch
        {
            "completed" => $"  ✓ {what}: {name}",
            "failed" => $"  ✗ {what}: {name} failed",
            SessionStream.SessionEnded => $"  {what}: {name} ended with its session",
            "stopped" => $"  ■ {what}: {name} stopped",
            _ => $"  {what}: {name}: {state}",
        });
    }

    /// <summary>A subagent's card, updated to how it ended — the wire's word.</summary>
    private void Card(string id, string streamId, string state) =>
        emit(new SessionEvent { Kind = SessionEventKind.Tool, Id = id, Stream = streamId, Status = state });

    private void Say(string line) => root.Line(line);

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
