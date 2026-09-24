using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What one line of a structured stdout became: lines for the console, events for the record.</summary>
public sealed record StreamMapped(IReadOnlyList<string> Lines, IReadOnlyList<SessionEvent> Events)
{
    public static readonly StreamMapped Nothing = new([], []);
}

/// <summary>
/// A harness's own structured stdout, read by its adapter (D76 §1) — the native door's twin of what
/// <see cref="AcpSession"/> does for the protocol door.
/// </summary>
/// <remarks>
/// Stateful per session, because a wire's meaning can span lines: a streamed message and the whole one
/// that repeats it, a tool call and the result that answers it. A new mapper per session, never shared.
/// </remarks>
public interface IStreamMapper
{
    /// <summary>One stdout line, as what the console shows and what the record keeps.</summary>
    StreamMapped Read(string line);

    /// <summary>Context at its high-water mark, as the harness reported it (TOOL3) — null when it reported none.</summary>
    AcpUsage? Usage { get; }
}

/// <summary>
/// Claude Code's <c>stream-json</c> output, in Daoris's vocabulary (CONV3) — every shape read off the
/// binary first (docs/2026-09-25-stream-json-evidence.md).
/// </summary>
/// <remarks>
/// <para>🔴 <b>The words arrive twice</b> with partial messages on: as <c>text_delta</c>s, and again whole
/// in the <c>assistant</c> message. The deltas are the conversation's words, live; the whole message's
/// text is skipped when its deltas already streamed, and kept when they did not.</para>
///
/// <para><b>Every field is optional and shape-checked</b> — the ACP2 lesson: a wire that is somebody
/// else's grows, and a field of an unexpected shape is an absence here, never a throw.</para>
///
/// <para><b>Leaves the model's name and the cost</b> on the wire (D24, TOOL3): both are there, and
/// neither is Daoris's to record.</para>
/// </remarks>
public sealed class ClaudeStreamJson : IStreamMapper
{
    private string? _message;
    private readonly HashSet<string> _streamed = new(StringComparer.Ordinal);
    private long? _context;
    private AcpUsage? _usage;

    public AcpUsage? Usage => _usage;

    public StreamMapped Read(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return StreamMapped.Nothing;

        JsonElement frame;
        try
        {
            frame = JsonDocument.Parse(line).RootElement.Clone();
        }
        catch (JsonException)
        {
            // Not a frame: the harness said something outside its protocol. Shown, never dropped.
            return new([line], []);
        }

        if (frame.ValueKind != JsonValueKind.Object) return new([line], []);

        return Str(frame, "type") switch
        {
            "stream_event" => Stream(frame),
            "assistant" => Assistant(frame),
            "user" => User(frame),
            "result" => Result(frame),
            "control_response" => Control(frame),
            // Known and deliberately not the conversation: the session's setup, its status, its limits.
            "system" or "rate_limit_event" => StreamMapped.Nothing,
            var kind => new([], [new SessionEvent
            {
                Kind = SessionEventKind.Raw, Title = kind ?? "frame", Raw = SessionEvents.Cut(line, SessionEvents.RawLimit),
            }]),
        };
    }

    private StreamMapped Stream(JsonElement frame)
    {
        if (!frame.TryGetProperty("event", out var e) || e.ValueKind != JsonValueKind.Object) return StreamMapped.Nothing;

        switch (Str(e, "type"))
        {
            case "message_start":
                _message = e.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.Object ? Str(m, "id") : null;
                return StreamMapped.Nothing;
            case "content_block_delta" when e.TryGetProperty("delta", out var d) && d.ValueKind == JsonValueKind.Object:
                var (kind, text) = Str(d, "type") switch
                {
                    "text_delta" => (SessionEventKind.Message, Str(d, "text")),
                    "thinking_delta" => (SessionEventKind.Thought, Str(d, "thinking")),
                    _ => (null, null),
                };
                if (kind is null || string.IsNullOrEmpty(text)) return StreamMapped.Nothing;
                if (_message is not null) _streamed.Add(_message);
                return new([], [new SessionEvent { Kind = kind, Text = text }]);
            default:
                return StreamMapped.Nothing;
        }
    }

    private StreamMapped Assistant(JsonElement frame)
    {
        if (!frame.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
        {
            return StreamMapped.Nothing;
        }

        var streamed = Str(message, "id") is { } id && _streamed.Contains(id);
        Measure(message);

        var lines = new List<string>();
        var events = new List<SessionEvent>();
        foreach (var block in Blocks(message))
        {
            switch (Str(block, "type"))
            {
                case "text" when Str(block, "text") is { Length: > 0 } text:
                    lines.AddRange(text.Split('\n'));
                    if (!streamed) events.Add(new SessionEvent { Kind = SessionEventKind.Message, Text = text });
                    break;
                case "thinking" when Str(block, "thinking") is { Length: > 0 } thought:
                    lines.Add($"· {FirstLine(thought)}");
                    if (!streamed) events.Add(new SessionEvent { Kind = SessionEventKind.Thought, Text = thought });
                    break;
                case "tool_use":
                    var call = ToolCall(block);
                    lines.Add($"→ {call.Title}");
                    events.Add(call);
                    if (Plan(block) is { } plan) events.Add(plan);
                    break;
            }
        }

        return new(lines, events);
    }

    private static StreamMapped User(JsonElement frame)
    {
        if (!frame.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
        {
            return StreamMapped.Nothing;
        }

        var lines = new List<string>();
        var events = new List<SessionEvent>();
        foreach (var block in Blocks(message))
        {
            if (Str(block, "type") != "tool_result") continue;
            var failed = block.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True;
            var id = Str(block, "tool_use_id");
            var status = failed ? "failed" : "completed";
            lines.Add($"  {id ?? "tool"} → {status}");
            events.Add(new SessionEvent
            {
                Kind = SessionEventKind.Tool,
                Id = id,
                Status = status,
                Content = ResultText(block) is { Length: > 0 } text ? [new ToolContent("text", Text: text)] : null,
            });
        }

        return new(lines, events);
    }

    private StreamMapped Result(JsonElement frame)
    {
        var failed = frame.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True;
        var subtype = Str(frame, "subtype") ?? "unknown";
        // 🔴 A turn the person stopped is `error_during_execution` on this wire, and only its terminal
        // reason says it was stopped rather than broken — the two the probe measured (CONV4a).
        var stopped = Str(frame, "terminal_reason") is "aborted_streaming" or "aborted_tools";
        var lines = new List<string>();
        var events = new List<SessionEvent>();

        // Context used against the window the harness names — a number it volunteered, carried as given.
        if (_context is { } used && Window(frame) is { } size)
        {
            if (_usage is null || used > _usage.Used) _usage = new AcpUsage(used, size);
            events.Add(new SessionEvent { Kind = SessionEventKind.Usage, Used = used, Size = size });
        }

        // A failure's words reach the transcript: the refusal detector reads its last lines (D49 §4).
        lines.Add(stopped
            ? "— the turn was stopped"
            : failed && Str(frame, "result") is { Length: > 0 } why
                ? $"— the turn failed ({subtype}): {why}"
                : $"— the turn ended: {subtype}");
        events.Add(new SessionEvent
        {
            Kind = SessionEventKind.Turn,
            // The protocol door's words for an ordinary ending and a stopped one, so each reads as one
            // thing whichever door it happened on.
            StopReason = stopped ? "cancelled" : !failed && subtype == "success" ? "end_turn" : subtype,
        });

        return new(lines, events);
    }

    /// <summary>
    /// The harness's answer to one of the driver's control requests — the driver's business, not the
    /// conversation's. A refusal is said on the console; an acceptance says nothing, because what it
    /// did arrives as the turn's own ending.
    /// </summary>
    private static StreamMapped Control(JsonElement frame)
    {
        if (!frame.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Object
            || Str(response, "subtype") != "error")
        {
            return StreamMapped.Nothing;
        }

        return new([$"— the harness refused a control request: {Str(response, "error") ?? "it gave no reason"}"], []);
    }

    /// <summary>The last assistant message's context: its input, cache-creation and cache-read tokens.</summary>
    private void Measure(JsonElement message)
    {
        if (!message.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return;
        var total = (Long(usage, "input_tokens") ?? 0) + (Long(usage, "cache_creation_input_tokens") ?? 0)
                    + (Long(usage, "cache_read_input_tokens") ?? 0);
        if (total > 0) _context = total;
    }

    /// <summary>The context window the harness names in <c>modelUsage</c> — the first entry's, the model unnamed here.</summary>
    private static long? Window(JsonElement frame)
    {
        if (!frame.TryGetProperty("modelUsage", out var models) || models.ValueKind != JsonValueKind.Object) return null;
        foreach (var model in models.EnumerateObject())
        {
            if (model.Value.ValueKind == JsonValueKind.Object && Long(model.Value, "contextWindow") is { } window) return window;
        }

        return null;
    }

    private static SessionEvent ToolCall(JsonElement block)
    {
        var name = Str(block, "name") ?? "tool";
        var input = block.TryGetProperty("input", out var i) && i.ValueKind == JsonValueKind.Object ? i : default;
        var path = input.ValueKind == JsonValueKind.Object
            ? Str(input, "file_path") ?? Str(input, "notebook_path") ?? Str(input, "path")
            : null;

        return new SessionEvent
        {
            Kind = SessionEventKind.Tool,
            Id = Str(block, "id"),
            Title = Title(name, input, path),
            ToolKind = KindOf(name),
            Status = "in_progress",
            Locations = path is null ? null : [path],
            Content = Diffs(name, input, path),
            Input = input.ValueKind == JsonValueKind.Object ? SessionEvents.Cut(input.GetRawText(), SessionEvents.RawLimit) : null,
        };
    }

    /// <summary>A tool's title from its own input — what it acted on, in the tool's own terms.</summary>
    private static string Title(string name, JsonElement input, string? path)
    {
        string? Field(string field) => input.ValueKind == JsonValueKind.Object ? Str(input, field) : null;
        var said = name switch
        {
            "Read" or "Edit" or "MultiEdit" or "Write" or "NotebookEdit" when path is not null =>
                $"{(name is "MultiEdit" ? "Edit" : name)} {path}",
            "Bash" => Field("description") ?? Field("command"),
            "Grep" => Field("pattern") is { } pattern ? $"Search \"{pattern}\"" : null,
            "Glob" => Field("pattern") is { } glob ? $"Find {glob}" : null,
            "WebFetch" => Field("url") is { } url ? $"Fetch {url}" : null,
            "WebSearch" => Field("query") is { } query ? $"Search the web: {query}" : null,
            "TodoWrite" => "Update the plan",
            "Task" => Field("description") is { } task ? $"Agent: {task}" : null,
            _ when name.StartsWith("mcp__", StringComparison.Ordinal) => name[(name.LastIndexOf("__", StringComparison.Ordinal) + 2)..],
            _ => null,
        };
        var title = said ?? name;
        return title.Length <= 160 ? title : $"{title[..160]}…";
    }

    /// <summary>A tool's name as ACP's kind vocabulary, so both doors' cards wear the same glyphs.</summary>
    private static string KindOf(string name) => name switch
    {
        "Read" or "NotebookRead" => "read",
        "Edit" or "MultiEdit" or "Write" or "NotebookEdit" => "edit",
        "Bash" or "BashOutput" or "KillShell" => "execute",
        "Grep" or "Glob" => "search",
        "WebFetch" or "WebSearch" => "fetch",
        "TodoWrite" => "think",
        _ => "other",
    };

    /// <summary>What an edit changes, as ACP's diff content: one per edit, a write as a new text.</summary>
    private static IReadOnlyList<ToolContent>? Diffs(string name, JsonElement input, string? path)
    {
        if (input.ValueKind != JsonValueKind.Object) return null;
        switch (name)
        {
            case "Edit":
                return [new ToolContent("diff", Path: path, OldText: Str(input, "old_string"), NewText: Str(input, "new_string"))];
            case "Write":
                return [new ToolContent("diff", Path: path, NewText: Str(input, "content"))];
            case "MultiEdit" when input.TryGetProperty("edits", out var edits) && edits.ValueKind == JsonValueKind.Array:
                return [.. edits.EnumerateArray()
                    .Where(edit => edit.ValueKind == JsonValueKind.Object)
                    .Select(edit => new ToolContent("diff", Path: path, OldText: Str(edit, "old_string"), NewText: Str(edit, "new_string")))];
            default:
                return null;
        }
    }

    /// <summary>The harness's to-do list as the plan: it is the one it keeps.</summary>
    private static SessionEvent? Plan(JsonElement block)
    {
        if (Str(block, "name") != "TodoWrite"
            || !block.TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.Object
            || !input.TryGetProperty("todos", out var todos) || todos.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return new SessionEvent
        {
            Kind = SessionEventKind.Plan,
            Entries = [.. todos.EnumerateArray()
                .Where(todo => todo.ValueKind == JsonValueKind.Object && Str(todo, "content") is not null)
                .Select(todo => new PlanEntry(Str(todo, "content")!, Str(todo, "status")))],
        };
    }

    /// <summary>A tool result's words: a string, or the text blocks of a list.</summary>
    private static string? ResultText(JsonElement block)
    {
        if (!block.TryGetProperty("content", out var content)) return null;
        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString(),
            JsonValueKind.Array => string.Join("\n", content.EnumerateArray()
                .Where(part => part.ValueKind == JsonValueKind.Object && Str(part, "type") == "text")
                .Select(part => Str(part, "text"))
                .OfType<string>()),
            _ => null,
        };
    }

    private static IEnumerable<JsonElement> Blocks(JsonElement message) =>
        message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array
            ? content.EnumerateArray().Where(block => block.ValueKind == JsonValueKind.Object)
            : [];

    private static string FirstLine(string text) => text.Trim().Split('\n', 2)[0];

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? Long(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n)
            ? n
            : null;
}
