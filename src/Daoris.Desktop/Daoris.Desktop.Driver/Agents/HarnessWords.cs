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

    // BGWAIT1c: the commands it backgrounded still running, by task id, and those the harness ended after its turn.
    private readonly Dictionary<string, string> _running = new(StringComparer.Ordinal);
    private readonly List<string> _cut = [];
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
        }
    }

    /// <summary>
    /// A reading of the session's own background work (BGWAIT1c; docs/2026-09-30-console3-native-streams-evidence.md): a command it
    /// backgrounded started (<see cref="WireReading.TaskStarted"/>, never a subagent, which is a child), and that command ended after its turn's
    /// result in a kill's or a stop's word (<see cref="WireReading.TaskEnded"/>), which only the harness does there, since the door has no stop
    /// for one task. Work that ended before the turn did is no cut. Told as each reading is said, whether or not a console keeps the streams
    /// beside; every other reading passes over.
    /// </summary>
    public void Read(WireReading reading)
    {
        lock (_gate)
        {
            switch (reading)
            {
                case WireReading.TaskStarted started:
                    _running[started.Task] = started.Name is { Length: > 0 } named ? named : started.Task;
                    break;
                case WireReading.TaskEnded { Word: not (null or "running" or "pending") } ended when _running.Remove(ended.Task, out var name):
                    if (ended.AfterTurn && ended.Word is "killed" or "stopped") _cut.Add(name);
                    break;
            }
        }
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
