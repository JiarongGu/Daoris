using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What a session on the native door runs beside itself (CONSOLE3c): the twin of <see cref="AcpStreams"/>
/// for Claude Code's <c>stream-json</c>, each subagent and each piece of background work a console
/// stream of its own.
/// </summary>
/// <remarks>
/// <para><b>Read off the wire, never inferred</b> (docs/2026-09-30-console3-native-streams-evidence.md).
/// Work is announced by a <c>system</c> <c>task_started</c>, <c>local_agent</c> for a subagent and
/// <c>local_bash</c> for a command, and ends with its <c>task_notification</c> in the wire's word. A
/// subagent's lines ride the session's stdout marked with the <c>parent_tool_use_id</c> of the call that
/// spawned it. A command's output is a file whose path the harness gives only in its result's words.</para>
///
/// <para><b>Beside the session, never in it</b>, for the protocol door's reason: a child's words in the
/// transcript would be quoted as the session's. The session's console says each started and how it
/// ended; its record already holds the <c>Agent</c> call as a card, which this door keeps.</para>
///
/// <para><b>Nothing stops one here</b>: this door has no request for it, so no stream is <c>CanStop</c>.
/// And the harness ends its own background work when its turn ends, so what is still open when the
/// output ends is ended with the session, and says so.</para>
/// </remarks>
internal sealed class ClaudeStreams(SessionStreams streams, Action<string> say, TimeSpan? tailEvery = null) : IStreamsReader
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
        public string? State { get; set; }
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

    public bool Take(string line)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(line);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return false;
        }

        if (root.ValueKind != JsonValueKind.Object) return false;

        if (Str(root, "parent_tool_use_id") is { Length: > 0 } parent)
        {
            Child? child;
            lock (_gate) child = _children.GetValueOrDefault(parent);
            // A child nobody announced (a synchronous one, which has no task) is still not the session's.
            child ??= Open(parent, $"subagent/{parent}", "subagent");
            foreach (var text in child.Words.Read(line).Lines) streams.Line(child.StreamId, text);
            return true;
        }

        switch (Str(root, "type"))
        {
            case "system":
                return System(root);
            case "user":
                Watch(root);
                return false;
        }

        return false;
    }

    private bool System(JsonElement root)
    {
        switch (Str(root, "subtype"))
        {
            case "task_started":
            {
                if (Str(root, "task_id") is not { Length: > 0 } id) return false;
                var name = Str(root, "description") is { Length: > 0 } described ? described : id;
                if (Str(root, "task_type") == "local_agent")
                {
                    if (Str(root, "tool_use_id") is not { Length: > 0 } call) return false;
                    lock (_gate) _subagentOfTask[id] = call;
                    Open(call, $"subagent/{id}", name);
                    return true;
                }

                Work work;
                lock (_gate)
                {
                    if (_work.ContainsKey(id)) return true;
                    work = _work[id] = new Work($"task/{id}", name);
                }

                streams.Open(new SessionStream(work.StreamId, SessionStreamKind.Task, name));
                say($"→ background: {AcpSession.OneLine(name)}");
                return true;
            }

            case "task_updated":
            {
                // The ending's first word; the notification that follows is the one said.
                if (Str(root, "task_id") is { } id && root.TryGetProperty("patch", out var patch) && Str(patch, "status") is { } status)
                {
                    lock (_gate)
                    {
                        if (_work.TryGetValue(id, out var work) && !work.Ended) work.State = status;
                    }
                }

                return true;
            }

            case "task_notification":
            {
                if (Str(root, "task_id") is not { } id) return false;
                var status = Str(root, "status");

                string? call;
                lock (_gate) call = _subagentOfTask.GetValueOrDefault(id);
                if (call is not null)
                {
                    Child? child;
                    lock (_gate) child = _children.GetValueOrDefault(call);
                    if (child is not null) EndChild(child, status ?? "ended");
                    return true;
                }

                Work? work;
                lock (_gate) work = _work.GetValueOrDefault(id);
                if (work is null) return false;
                Watch(work, Str(root, "output_file"));
                EndWork(work, status ?? work.State ?? "ended");
                return true;
            }

            // Known, and not the conversation: the live set and a subagent's progress, which the others say.
            case "task_progress":
            case "background_tasks_changed":
                return true;
        }

        return false;
    }

    /// <summary>A backgrounded command's result names its file in words: read the file from there on.</summary>
    private void Watch(JsonElement root)
    {
        if (!root.TryGetProperty("tool_use_result", out var result) || result.ValueKind != JsonValueKind.Object) return;
        if (Str(result, "backgroundTaskId") is not { } id) return;

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
        say($"→ subagent: {AcpSession.OneLine(name)}");
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
            work.State = state;
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
        name = AcpSession.OneLine(name);
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
                    if (Str(part, "text") is { } text) yield return text;
                }
            }
        }
    }

    private static string? Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
