using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a native run's harness wrote that its agent could not (AGT3c), kept as its output passes: its stderr and the lines it
/// printed outside its protocol, the last <see cref="HarnessEnding.LinesKept"/> of them, and the words its failed turn gave.
/// One per session, begun again by each run, so the conclusion reads the run that ended last.
/// </summary>
public sealed class HarnessWords
{
    private readonly object _gate = new();
    private readonly Queue<string> _lines = new();
    private string? _failure;

    // BGWAIT1c: the commands it backgrounded still running, by task id; whether its turn's `result` came; what the harness ended after it.
    private readonly Dictionary<string, string> _running = new(StringComparer.Ordinal);
    private readonly List<string> _cut = [];
    private bool _turnEnded;
    private bool _modelRefused;

    /// <summary>A run begins: what the run before it said is not this one's ending.</summary>
    public void Begin()
    {
        lock (_gate)
        {
            _lines.Clear();
            _failure = null;
            _modelRefused = false;
            _running.Clear();
            _cut.Clear();
            _turnEnded = false;
        }
    }

    /// <summary>
    /// A line of the native door's structured stdout, read for the session's own background work (BGWAIT1c;
    /// docs/2026-09-30-console3-native-streams-evidence.md): a command it backgrounded (<c>task_started</c>, never a subagent's),
    /// the turn's end (<c>result</c>), and that command ended after it (<c>task_updated</c> <c>killed</c>, or its
    /// <c>task_notification</c> <c>stopped</c>), which only the harness does there, since the door has no stop for one task.
    /// Work that ended before the turn did is no cut. Read before a console's streams take the line, so a run with no console
    /// reads the same.
    /// </summary>
    public void Heard(string line)
    {
        // Most lines are the agent's words: only a task's line or the turn's result is parsed.
        if (!line.Contains("\"task_", StringComparison.Ordinal) && !line.Contains("\"result\"", StringComparison.Ordinal)) return;
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(line);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return;
        }

        if (root.ValueKind != JsonValueKind.Object) return;
        var type = Text(root, "type");
        lock (_gate)
        {
            if (type == "result")
            {
                _turnEnded = true;
                return;
            }

            if (type != "system" || Text(root, "task_id") is not { Length: > 0 } id) return;
            switch (Text(root, "subtype"))
            {
                case "task_started" when Text(root, "task_type") != "local_agent":
                    _running[id] = Text(root, "description") is { Length: > 0 } named ? named : id;
                    break;
                case "task_updated" when root.TryGetProperty("patch", out var patch) && patch.ValueKind == JsonValueKind.Object:
                    Ended(id, Text(patch, "status"));
                    break;
                case "task_notification":
                    Ended(id, Text(root, "status"));
                    break;
            }
        }

        // Called under the gate: an ending of a command still running, cut where the turn had ended before it.
        void Ended(string id, string? status)
        {
            if (status is null or "running" or "pending" || !_running.Remove(id, out var name)) return;
            if (_turnEnded && status is "killed" or "stopped") _cut.Add(name);
        }

        static string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>A line the harness wrote itself: on its stderr, or outside its protocol.</summary>
    public void Said(string line)
    {
        lock (_gate)
        {
            _lines.Enqueue(line);
            while (_lines.Count > HarnessEnding.LinesKept) _lines.Dequeue();
        }
    }

    /// <summary>A turn the harness said failed, in its words.</summary>
    public void Failed(string failure)
    {
        lock (_gate) _failure = failure;
    }

    /// <summary>The failed turn was the door's refusal of the model it was handed (MODELROLE1c3, <see cref="HarnessEnding.ModelRefused"/>).</summary>
    public void ModelRefused()
    {
        lock (_gate) _modelRefused = true;
    }

    /// <summary>The run's ending, with the process's exit.</summary>
    public HarnessEnding Ended(int? exit)
    {
        lock (_gate) return new(exit, _failure, [.. _lines]) { BackgroundCut = [.. _cut], ModelRefused = _modelRefused };
    }
}
