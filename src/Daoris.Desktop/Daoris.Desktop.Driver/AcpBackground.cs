using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The background work a protocol-door session's harness says it runs (BGWAIT1): each task <c>async_task_spawned</c>
/// announced on the session's own id, running until an <c>async_task_state_update</c> gives it an ending. What a driven
/// session's turn that ends on that work waits for, where the driver says its quest would otherwise park it on the person.
/// </summary>
/// <remarks>
/// <para><b>The harness's word, never the agent's.</b> The task frames are the adapter's report of processes it runs,
/// which the agent's words cannot write (docs/2026-09-28-console2-streams-evidence.md). They arrive only where the client
/// asked for AIR's <c>asyncTasks</c> at <c>initialize</c>, which a session whose streams are kept does (CONSOLE2); on a wire
/// that carries none, nothing is running here, and the session ends as it always did.</para>
///
/// <para><b>The last state is the ending.</b> The adapter says <c>stopped</c> and then <c>completed</c> in one breath for a
/// task that finished on its own; <c>running</c> and <c>paused</c> are not endings.</para>
///
/// <para>The console's own copy of the same frames is <see cref="AcpStreams"/>, which exists only where streams are kept.</para>
/// </remarks>
internal sealed class AcpBackground
{
    private sealed class Work(string id, string name)
    {
        public string Id { get; } = id;
        public string Name { get; } = name;

        /// <summary>Its last ending, or null while it runs.</summary>
        public string? State { get; set; }
    }

    private readonly object _gate = new();
    private readonly List<Work> _work = [];
    private TaskCompletionSource _changed = NewSignal();

    /// <summary>Read one update of the session's own: a task announced, or a task's state. Anything else is not this.</summary>
    public void Take(JsonElement update)
    {
        switch (AcpSession.Kind(update))
        {
            case "async_task_spawned" when Str(update, "asyncTaskId") is { Length: > 0 } id:
                lock (_gate)
                {
                    // Announced once, whatever the wire repeats.
                    if (_work.Any(work => work.Id == id)) return;
                    _work.Add(new Work(id, Str(update, "name") is { Length: > 0 } name ? AcpSession.OneLine(name) : id));
                }

                break;

            case "async_task_state_update" when Str(update, "asyncTaskId") is { } id
                                                && Str(update, "state") is { } state and not ("running" or "paused"):
                lock (_gate)
                {
                    if (_work.FirstOrDefault(work => work.Id == id) is not { } known) return;
                    known.State = state;
                }

                break;

            default:
                return;
        }

        TaskCompletionSource changed;
        lock (_gate)
        {
            changed = _changed;
            _changed = NewSignal();
        }

        changed.TrySetResult();
    }

    /// <summary>The work still running, by its names, in the order it was announced.</summary>
    public IReadOnlyList<string> Running
    {
        get
        {
            lock (_gate) return [.. _work.Where(work => work.State is null).Select(work => work.Name)];
        }
    }

    /// <summary>
    /// Wait until none of the work runs, as the wire says, then a breath for its last word (<see cref="AcpStreams.Settle"/>);
    /// and answer every piece that ran at the start or was announced since, with how it ended. Null when
    /// <paramref name="ended"/> completes first: the agent's stream is over, and nothing more will be said of the work.
    /// </summary>
    public async Task<IReadOnlyList<(string Name, string State)>?> EndedAsync(Task ended, CancellationToken ct)
    {
        HashSet<string> watched;
        int since;
        lock (_gate)
        {
            watched = [.. _work.Where(work => work.State is null).Select(work => work.Id)];
            since = _work.Count;
        }

        while (true)
        {
            Task changed;
            bool running;
            lock (_gate)
            {
                changed = _changed.Task;
                running = _work.Any(work => work.State is null);
            }

            if (!running)
            {
                // The ending's last word arrives in the same breath as its first: wait for it, and for any work the agent
                // starts as it is woken, before the work is said to have ended.
                await Task.WhenAny(Task.Delay(AcpStreams.Settle, ct), ended).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                if (ended.IsCompleted) return null;
                lock (_gate)
                {
                    if (_work.Any(work => work.State is null)) continue;
                    return [.. _work
                        .Select((work, at) => (work, at))
                        .Where(pair => watched.Contains(pair.work.Id) || pair.at >= since)
                        .Select(pair => (pair.work.Name, pair.work.State ?? "ended"))];
                }
            }

            await Task.WhenAny(changed, ended).WaitAsync(ct).ConfigureAwait(false);
            if (ended.IsCompleted) return null;
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
