using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The background tasks a native door's stream says (AGENTPROVIDER1l; BGWAIT1c, CONSOLE3c): Claude Code's <c>stream-json</c> announces a command
/// it backgrounded with a <c>system</c> <c>task_started</c> of any type but a subagent's, and ends it with the last of its two ending lines, its
/// <c>task_notification</c>, in that line's word or else its <c>task_updated</c>'s (docs/2026-09-30-console3-native-streams-evidence.md). Each
/// is read from the line's one <see cref="NativeFrame"/> into one <see cref="WireReading.TaskStarted"/> or <see cref="WireReading.TaskEnded"/>,
/// once per task however the wire repeats it, the end marked where it came after the turn's <c>result</c>; a subagent's task is a child
/// (<see cref="NativeChildren"/>), and every other line says none. At the stream's close (<see cref="Close"/>), a task whose update gave its
/// ending word and whose notification never came is ended in that word.
/// </summary>
/// <remarks>One per capture: an end is read only for a start its own stream said, and after a result only its own process's.</remarks>
internal sealed class NativeTasks
{
    /// <summary>A task's last <c>task_updated</c>: its word, and whether it came after the turn's result.</summary>
    private readonly record struct Update(string? Word, bool AfterTurn);

    private readonly Dictionary<string, Update> _updated = new(StringComparer.Ordinal); // each task started, by id
    private readonly HashSet<string> _ended = new(StringComparer.Ordinal);
    private bool _turnEnded;

    /// <summary>
    /// The ends the stream's close says (BGWAIT1c): each task whose update gave its ending word, a kill or a completion, and whose notification
    /// never came, as where Daoris ended the process at its turn's end before the harness wrote it, ended in that word and marked where that
    /// update came after the turn's result. A task with no ending word says none: its end is unsaid.
    /// </summary>
    public IReadOnlyList<WireReading> Close()
    {
        var ended = new List<WireReading>();
        foreach (var (task, update) in _updated)
        {
            if (update.Word is null or "running" or "pending" || !_ended.Add(task)) continue;
            ended.Add(new WireReading.TaskEnded(task, update.Word) { AfterTurn = update.AfterTurn });
        }

        return ended;
    }

    /// <summary>The reading the frame says of a background task, or null.</summary>
    public WireReading? Read(NativeFrame frame)
    {
        if (frame.Type == "result") _turnEnded = true;
        if (frame.Type != "system" || frame.Text("task_id") is not { Length: > 0 } task) return null;
        switch (frame.Subtype)
        {
            case "task_started" when frame.Text("task_type") != NativeChildren.Subagent:
                return _updated.TryAdd(task, default)
                    ? new WireReading.TaskStarted(task) { Kind = frame.Text("task_type"), Name = frame.Text("description") }
                    : null;

            // The ending's first word: the notification that follows is the end said, in this word where it gives none, and the close's
            // where none follows.
            case "task_updated" when _updated.ContainsKey(task) && !_ended.Contains(task)
                && frame.Root.TryGetProperty("patch", out var patch) && patch.ValueKind == JsonValueKind.Object:
                if (Json.Text(patch, "status") is { } status) _updated[task] = new Update(status, _turnEnded);
                return null;

            case "task_notification" when _updated.TryGetValue(task, out var updated) && _ended.Add(task):
                return new WireReading.TaskEnded(task, frame.Text("status") ?? updated.Word)
                {
                    Output = frame.Text("output_file"),
                    AfterTurn = _turnEnded,
                };

            default:
                return null;
        }
    }
}
