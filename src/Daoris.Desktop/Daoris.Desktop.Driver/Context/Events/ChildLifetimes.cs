namespace Daoris.Driver;

/// <summary>
/// The session's record's part of what a run runs beside itself: each subagent's and background task's start and end its door said
/// (<see cref="WireReading.ChildSeen"/>, <see cref="WireReading.ChildEnded"/>, <see cref="WireReading.TaskStarted"/>,
/// <see cref="WireReading.TaskEnded"/>) as one <see cref="SessionEventKind.Child"/> or <see cref="SessionEventKind.Task"/> event under its
/// own id, its start linked to the call that started it where its door named one.
/// </summary>
/// <remarks>
/// <para><b>Each once, an end only for a start kept.</b> A replay of a resume's history is no new child. An end its door said is
/// <c>reported</c>; one only its session's close implies, its stream ended with the session (<see cref="SessionStream.SessionEnded"/>), is
/// <c>inferred</c> and <see cref="ChildEnds.Lost"/>, and an end its door says after it (the native door's close, a task's last update) is
/// kept after it, so the later end under the same id is how it ended.</para>
///
/// <para>One per run, as its sink is: a child outlives no process.</para>
/// </remarks>
internal sealed class ChildLifetimes
{
    private enum Held
    {
        Open,
        Inferred,
        Ended,
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Held> _children = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Held> _tasks = new(StringComparer.Ordinal);

    /// <summary>The event the session's record keeps of this reading, or null where it keeps none.</summary>
    public SessionEvent? Read(WireReading reading)
    {
        if (reading.Replayed) return null;
        lock (_gate)
        {
            return reading switch
            {
                WireReading.ChildSeen seen when _children.TryAdd(seen.Child, Held.Open) => new SessionEvent
                {
                    Kind = SessionEventKind.Child,
                    Id = seen.Child,
                    Call = Said(seen.Call),
                    Parent = Said(seen.Parent),
                    Depth = seen.Depth,
                    Name = Said(seen.Name),
                    Task = Said(seen.Task),
                    By = By(seen.By),
                },
                WireReading.ChildEnded ended => Ended(_children, SessionEventKind.Child, ended.Child, ChildEnds.Of(ended.Word), ended.By),
                WireReading.TaskStarted started when _tasks.TryAdd(started.Task, Held.Open) => new SessionEvent
                {
                    Kind = SessionEventKind.Task,
                    Id = started.Task,
                    Call = Said(started.Call),
                    Name = Said(started.Name),
                    By = By(started.By),
                },
                WireReading.TaskEnded ended => Ended(_tasks, SessionEventKind.Task, ended.Task, ChildEnds.Of(ended.Word), ended.By),
                WireReading.StreamEnded { State: SessionStream.SessionEnded } closed => Closed(closed.Stream),
                _ => null,
            };
        }
    }

    /// <summary>A stream its session's close ended: the child's or task's end it implies, where its start is kept and no end is.</summary>
    private SessionEvent? Closed(string stream)
    {
        var (held, kind, id) = stream.Split('/', 2) switch
        {
            [SessionStreamKind.Subagent, var child] => (_children, SessionEventKind.Child, child),
            [SessionStreamKind.Task, var task] => (_tasks, SessionEventKind.Task, task),
            _ => (null, "", ""),
        };
        if (held is null || held.GetValueOrDefault(id, Held.Ended) != Held.Open) return null;
        held[id] = Held.Inferred;
        return new SessionEvent { Kind = kind, Id = id, End = ChildEnds.Lost, By = By(ReadingBy.Inferred) };
    }

    private static SessionEvent? Ended(Dictionary<string, Held> held, string kind, string id, string end, ReadingBy by)
    {
        if (!held.TryGetValue(id, out var was) || was == Held.Ended) return null;
        held[id] = Held.Ended;
        return new SessionEvent { Kind = kind, Id = id, End = end, By = By(by) };
    }

    private static string? Said(string? text) => text is { Length: > 0 } ? text : null;

    private static string By(ReadingBy by) => by.ToString().ToLowerInvariant();
}
