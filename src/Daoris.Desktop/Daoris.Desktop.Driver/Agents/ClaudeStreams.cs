using System.Text.Json;
using System.Text.RegularExpressions;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// What a session on the native door runs beside itself: the twin of <see cref="AcpStreams"/>
/// for Claude Code's <c>stream-json</c>, each subagent and each piece of background work a console
/// stream of its own.
/// </summary>
/// <remarks>
/// <para><b>Read off the wire, never inferred</b> (docs/2026-09-30-console3-native-streams-evidence.md).
/// Each subagent's and each command's start and end is the reading the capture said of it
/// (<see cref="WireReading.ChildSeen"/> and <see cref="WireReading.ChildEnded"/>, <see cref="WireReading.TaskStarted"/>
/// and <see cref="WireReading.TaskEnded"/>), in the wire's word. A subagent's lines ride the session's stdout
/// marked with the <c>parent_tool_use_id</c> of the call that spawned it, its reading's call. A command's output
/// is a file whose path the harness gives only in its result's words, and at its end.</para>
///
/// <para><b>Beside the session, never in it</b>, for the protocol door's reason: a child's words in the
/// transcript would be quoted as the session's. The session's console says each started and how it
/// ended; its record already holds the <c>Agent</c> call as a card, which this door keeps.</para>
///
/// <para><b>Nothing stops one here</b>: this door has no request for it, so no stream is <c>CanStop</c>.
/// And the harness ends its own background work when its turn ends, so what is still open when the
/// output ends is ended with the session, and says so.</para>
/// </remarks>
internal sealed class ClaudeStreams(ISessionStreams streams, Action<string> say, TimeSpan? tailEvery = null) : IStreamsReader
{
    private sealed class Child(string streamId, string name)
    {
        public string StreamId { get; } = streamId;
        public string Name { get; } = name;
        public ClaudeStreamJson Words { get; } = new();
        public bool Ended { get; set; }
    }

    private sealed class Work(string streamId, string name)
    {
        public string StreamId { get; } = streamId;
        public string Name { get; } = name;
        public OutputTail? Tail { get; set; }
        public bool Ended { get; set; }
    }

    /// <summary>
    /// The path in a backgrounded command's result: the harness's words to the model, the only place
    /// it names the file while the command runs. Words can change, and a path not found here leaves the
    /// stream saying it started and how it ended.
    /// </summary>
    private static readonly Regex OutputPath = new(@"Output is being written to: (?<path>\S.*?)\.(?:\s|$)", RegexOptions.CultureInvariant);

    private readonly object _gate = new();
    private readonly Dictionary<string, Child> _children = new(StringComparer.Ordinal); // by the spawning call's id
    private readonly Dictionary<string, Work> _work = new(StringComparer.Ordinal);      // by task id
    private readonly Dictionary<string, string> _subagentOfTask = new(StringComparer.Ordinal);

    public void Read(WireReading reading)
    {
        switch (reading)
        {
            // A subagent the stream announced, keyed by the call its lines name as theirs; one with no call has no lines here.
            case WireReading.ChildSeen { Call: { Length: > 0 } call } seen:
                lock (_gate) _subagentOfTask[seen.Child] = call;
                Open(call, $"subagent/{seen.Child}", seen.Task is { Length: > 0 } described ? described : seen.Child);
                break;

            case WireReading.ChildEnded ended:
            {
                Child? child;
                lock (_gate) child = _subagentOfTask.GetValueOrDefault(ended.Child) is { } call ? _children.GetValueOrDefault(call) : null;
                if (child is not null) EndChild(child, ended.Word ?? "ended");
                break;
            }

            case WireReading.TaskStarted started:
            {
                var name = started.Name is { Length: > 0 } named ? named : started.Task;
                Work work;
                lock (_gate)
                {
                    if (_work.ContainsKey(started.Task)) return;
                    work = _work[started.Task] = new Work($"task/{started.Task}", name);
                }

                streams.Open(new SessionStream(work.StreamId, SessionStreamKind.Task, name));
                say($"→ background: {AcpRender.OneLine(name)}");
                break;
            }

            case WireReading.TaskEnded ended:
            {
                Work? work;
                lock (_gate) work = _work.GetValueOrDefault(ended.Task);
                if (work is null) return;
                Watch(work, ended.Output);
                EndWork(work, ended.Word ?? "ended");
                break;
            }
        }
    }

    public bool Take(NativeFrame frame)
    {
        if (!frame.IsObject) return false;

        if (frame.Text("parent_tool_use_id") is { Length: > 0 } parent)
        {
            var child = ChildOf(parent);
            foreach (var text in child.Words.Read(frame).Lines) streams.Line(child.StreamId, text);
            return true;
        }

        if (frame.Type == "user") Watch(frame.Root);
        return false;
    }

    public void Followed(string task, string path)
    {
        Work? work;
        lock (_gate) work = _work.GetValueOrDefault(task);
        if (work is not null) Watch(work, path);
    }

    public void Said(string call, string text) => streams.Line(ChildOf(call).StreamId, text);

    /// <summary>The child its spawning call names; one nobody announced (a synchronous one, which has no task) is still not the session's.</summary>
    private Child ChildOf(string call)
    {
        Child? child;
        lock (_gate) child = _children.GetValueOrDefault(call);
        return child ?? Open(call, $"subagent/{call}", "subagent");
    }

    /// <summary>A backgrounded command's result names its file in words: read the file from there on.</summary>
    private void Watch(JsonElement root)
    {
        if (!root.TryGetProperty("tool_use_result", out var result) || result.ValueKind != JsonValueKind.Object) return;
        if (Text(result, "backgroundTaskId") is not { } id) return;

        Work? work;
        lock (_gate) work = _work.GetValueOrDefault(id);
        if (work is null) return;

        foreach (var text in Texts(root))
        {
            if (OutputPath.Match(text) is { Success: true } found)
            {
                Watch(work, found.Groups["path"].Value);
                return;
            }
        }
    }

    private void Watch(Work work, string? path)
    {
        if (path is not { Length: > 0 }) return;
        lock (_gate)
        {
            if (work.Tail is not null || work.Ended) return;
            work.Tail = new OutputTail(path, text => streams.Line(work.StreamId, text), tailEvery);
        }
    }

    private Child Open(string call, string streamId, string name)
    {
        Child child;
        lock (_gate)
        {
            if (_children.TryGetValue(call, out var known)) return known;
            child = _children[call] = new Child(streamId, name);
        }

        streams.Open(new SessionStream(streamId, SessionStreamKind.Subagent, name));
        say($"→ subagent: {AcpRender.OneLine(name)}");
        return child;
    }

    private void EndChild(Child child, string state)
    {
        lock (_gate)
        {
            if (child.Ended) return;
            child.Ended = true;
        }

        Ended(SessionStreamKind.Subagent, child.StreamId, child.Name, state);
    }

    private void EndWork(Work work, string state)
    {
        OutputTail? tail;
        lock (_gate)
        {
            if (work.Ended) return;
            work.Ended = true;
            tail = work.Tail;
        }

        // The file's last lines before the ending is said. Synchronous here: the reader is the
        // session's stdout, and an ending said out of order would read as the task outliving it.
        tail?.FinishAsync().GetAwaiter().GetResult();
        if (state == SessionStream.SessionEnded) streams.Line(work.StreamId, "— ended with its session");
        Ended(SessionStreamKind.Task, work.StreamId, work.Name, state);
    }

    public Task EndAllAsync()
    {
        List<Child> children;
        List<Work> work;
        lock (_gate)
        {
            children = [.. _children.Values.Where(child => !child.Ended)];
            work = [.. _work.Values.Where(task => !task.Ended)];
        }

        foreach (var child in children)
        {
            streams.Line(child.StreamId, "— ended with its session");
            EndChild(child, SessionStream.SessionEnded);
        }

        foreach (var task in work) EndWork(task, SessionStream.SessionEnded);
        return Task.CompletedTask;
    }

    private void Ended(string kind, string streamId, string name, string state)
    {
        streams.End(streamId, state);
        var what = kind == SessionStreamKind.Subagent ? "subagent" : "background";
        name = AcpRender.OneLine(name);
        say(state switch
        {
            "completed" => $"  ✓ {what}: {name}",
            "failed" => $"  ✗ {what}: {name} failed",
            "stopped" or "killed" => $"  ■ {what}: {name} stopped",
            SessionStream.SessionEnded => $"  {what}: {name} ended with its session",
            _ => $"  {what}: {name}: {state}",
        });
    }

    /// <summary>The text a user line's tool results carry, whether a string or a list of text blocks.</summary>
    private static IEnumerable<string> Texts(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var block in content.EnumerateArray())
        {
            if (!block.TryGetProperty("content", out var inner)) continue;
            if (inner.ValueKind == JsonValueKind.String) yield return inner.GetString()!;
            else if (inner.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in inner.EnumerateArray())
                {
                    if (Text(part, "text") is { } text) yield return text;
                }
            }
        }
    }
}
