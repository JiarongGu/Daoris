using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A press's run: its id, its door, when it started and ended, how it ended, and each kind
/// waiting, running or done. The page reads it in <c>SYSTEM_SPACE</c>'s <c>run</c>, the current or last run of the process, and the
/// terminal reads it until it ends. Each kind's end is told (<see cref="KindEnded"/>), the whole's end completes <see cref="Finished"/>.
/// </summary>
public sealed class SpaceRun
{
    private readonly object _gate = new();
    private readonly List<SpaceRunKind> _kinds;
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal SpaceRun(string id, string door, DateTimeOffset started, IEnumerable<string> kinds)
    {
        (Id, Door, Started) = (id, door, started);
        _kinds = [.. kinds.Select(kind => new SpaceRunKind(kind, "waiting", 0, 0, new Dictionary<string, int>()))];
    }

    /// <summary>Its id, which <c>keeping.json</c>'s <c>last</c> and the machine log's <c>space.freed</c> name: the run lock's.</summary>
    public string Id { get; }

    /// <summary>Its door: <c>press</c> from a window (a terminal's over the pipe included), <c>terminal</c> alone.</summary>
    public string Door { get; }

    public DateTimeOffset Started { get; }

    public DateTimeOffset? Ended { get; private set; }

    /// <summary><c>done</c> (every unit went), <c>kept</c> (one was kept) or <c>stopped</c> (it ended before its last unit); null while it runs.</summary>
    public string? Outcome { get; private set; }

    /// <summary>Each kind, in the order it runs.</summary>
    public IReadOnlyList<SpaceRunKind> Kinds
    {
        get
        {
            lock (_gate) return [.. _kinds];
        }
    }

    /// <summary>Told each kind's id as it ends.</summary>
    public event Action<string>? KindEnded;

    /// <summary>Completes once the run has ended, however it ended.</summary>
    public Task Finished => _finished.Task;

    internal void Begin(string kind) => Set(kind, each => each with { State = "running" });

    internal void End(string kind, SpaceTally tally)
    {
        Set(kind, each => each with { State = "done", Removed = tally.Removed, Bytes = tally.Bytes, Kept = new Dictionary<string, int>(tally.Kept) });
        try
        {
            KindEnded?.Invoke(kind);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Telling is best-effort: the run is read whole by its next reader.
        }
    }

    internal void Done() => Finish(Kinds.Any(each => each.Kept.Count > 0) ? "kept" : "done");

    internal void Stopped() => Finish("stopped");

    private void Finish(string outcome)
    {
        lock (_gate)
        {
            if (Outcome is not null) return;
            (Outcome, Ended) = (outcome, DateTimeOffset.UtcNow);
        }
    }

    /// <summary>The run lock let go: <see cref="Finished"/> completes, so a run after it finds the lock free.</summary>
    internal void Release() => _finished.TrySetResult();

    private void Set(string kind, Func<SpaceRunKind, SpaceRunKind> change)
    {
        lock (_gate)
        {
            var at = _kinds.FindIndex(each => each.Id == kind);
            if (at >= 0) _kinds[at] = change(_kinds[at]);
        }
    }

    /// <summary>The run as <c>SYSTEM_SPACE</c>'s <c>run</c> answers it.</summary>
    public JsonObject Json()
    {
        var kinds = new JsonArray();
        foreach (var each in Kinds)
        {
            var kept = new JsonObject();
            foreach (var (why, count) in each.Kept.OrderBy(pair => pair.Key, StringComparer.Ordinal)) kept[why] = count;
            kinds.Add(new JsonObject
            {
                ["id"] = each.Id, ["state"] = each.State,
                ["removed"] = new JsonObject { ["count"] = each.Removed, ["bytes"] = each.Bytes }, ["kept"] = kept,
            });
        }

        return new JsonObject
        {
            ["id"] = Id, ["door"] = Door, ["started"] = Moments.Write(Started), ["ended"] = Moments.Write(Ended), ["outcome"] = Outcome,
            ["kinds"] = kinds,
        };
    }
}
