using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// The children and background tasks a layer door's events say: a subagent's task, the one whose kind is the
/// subagent's word (<see cref="NativeChildren.Subagent"/>), started and ended as one <see cref="WireReading.ChildSeen"/> and one
/// <see cref="WireReading.ChildEnded"/>, and any other task as one <see cref="WireReading.TaskStarted"/> and one
/// <see cref="WireReading.TaskEnded"/>, each once however the events repeat it, an end only for a start the turn said: the readings Daoris's
/// native reader makes of the frames they stand for (<see cref="NativeChildren"/>, <see cref="NativeTasks"/>). A task's end carries the
/// output file its events named and whether it came after the turn's terminal.
/// </summary>
/// <remarks>One per turn, as the native readers are one per capture.</remarks>
internal sealed class LayerWork
{
    private readonly HashSet<string> _children = new(StringComparer.Ordinal);

    private readonly HashSet<string> _tasks = new(StringComparer.Ordinal);

    private readonly HashSet<string> _ended = new(StringComparer.Ordinal);

    private readonly record struct Update(string Word, bool AfterTurn);

    private readonly Dictionary<string, Update> _updates = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _outputs = new(StringComparer.Ordinal);

    /// <summary>The reading a task's start is: a child's or a background task's; null where it is said already or names no task.</summary>
    public WireReading? Started(AgentTaskStarted started)
    {
        if (started.TaskId is not { Length: > 0 } task || _children.Contains(task) || _tasks.Contains(task)) return null;
        if (started.Kind == NativeChildren.Subagent)
        {
            _children.Add(task);
            return new WireReading.ChildSeen(task, ChildSignal.Stream)
            {
                Call = started.CallId,
                Parent = started.ParentTaskId,
                Depth = started.Depth is int depth and > 0 ? depth : null,
                Type = started.SubagentType,
                Task = started.Description,
            };
        }

        _tasks.Add(task);
        return new WireReading.TaskStarted(task) { Kind = started.Kind, Name = started.Description };
    }

    /// <summary>The reading a task's end is, in the agent's word; null for a start the turn never said and for an end said already.</summary>
    public WireReading? Ended(AgentTaskEnded ended, bool afterTurn)
    {
        if (ended.TaskId is not { Length: > 0 } task || _ended.Contains(task)) return null;
        if (_children.Contains(task))
        {
            _ended.Add(task);
            return new WireReading.ChildEnded(task, ChildSignal.Stream, ended.Status);
        }

        if (!_tasks.Contains(task)) return null;
        _ended.Add(task);
        return new WireReading.TaskEnded(task, ended.Status ?? _updates.GetValueOrDefault(task).Word) { Output = _outputs.GetValueOrDefault(task), AfterTurn = afterTurn };
    }

    public void Updated(AgentTaskUpdated updated, bool afterTurn)
    {
        if (updated.TaskId is { Length: > 0 } task && updated.Status is { } status && _tasks.Contains(task) && !_ended.Contains(task))
            _updates[task] = new Update(status, afterTurn);
    }

    /// <summary>The tasks whose update gave an ending word and whose end never came, ended in that word.</summary>
    public IReadOnlyList<WireReading> Close()
    {
        var closed = new List<WireReading>();
        foreach (var (task, update) in _updates)
        {
            if (update.Word is "running" or "pending" || !_ended.Add(task)) continue;
            closed.Add(new WireReading.TaskEnded(task, update.Word) { AfterTurn = update.AfterTurn });
        }

        return closed;
    }

    /// <summary>The file a task's output is written to, kept for its end: the first its events named.</summary>
    public void Output(AgentTaskOutputFile file)
    {
        if (file.TaskId is { Length: > 0 } task && file.Path is { Length: > 0 } path) _outputs.TryAdd(task, path);
    }
}
