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

    /// <summary>
    /// What reads the session's subagents and background work off this wire (CONSOLE3c), each a console
    /// stream of its own, or null for a wire that carries none. Asked once, where a console is kept.
    /// </summary>
    /// <param name="streams">The session's streams.</param>
    /// <param name="say">The session's own console, for a line saying one started or ended.</param>
    IStreamsReader? Beside(SessionStreams streams, Action<string> say) => null;
}

/// <summary>
/// A wire's streams beside the session (CONSOLE3c): offered each stdout line before the session's own
/// reader, and ended when the output ends.
/// </summary>
public interface IStreamsReader
{
    /// <summary>True when the line was a stream's, so the session's reader never sees it.</summary>
    bool Take(string line);

    /// <summary>End every stream still open, because the session's output has: each says so.</summary>
    Task EndAllAsync();
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
///
/// <para><b>A denial is the call's refusal</b> (UNBLOCK5, D122 §3.10). Nobody answers a prompt on this
/// door, so what the harness would have asked it denies, and reports as a <c>system</c>
/// <c>permission_denied</c> frame and again in the result's <c>permission_denials</c>. Each is read as an
/// update marking the call <c>refused</c>, once, with the tool's name and what decided it; the call's
/// failed result then reads <c>refused</c> too, as the protocol door's does (HELP4). 🔴 Both shapes are
/// the maker's reference, not yet a frame this machine printed: the tests say so beside them.</para>
/// </remarks>
public sealed class ClaudeStreamJson : IStreamMapper
{
    private string? _message;
    private readonly HashSet<string> _streamed = new(StringComparer.Ordinal);

    /// <summary>Each tool call's title by its id, so its result is said by name rather than by id.</summary>
    private readonly Dictionary<string, string> _titles = new(StringComparer.Ordinal);

    /// <summary>Each tool call's own name by its id: a denial in the result's list is named by it too.</summary>
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);

    /// <summary>The calls the harness denied, by id: each is marked once, and its failed result reads refused.</summary>
    private readonly HashSet<string> _refused = new(StringComparer.Ordinal);
    private long? _context;
    private AcpUsage? _usage;

    public AcpUsage? Usage => _usage;

    /// <summary>A subagent's lines by <c>parent_tool_use_id</c>, and tasks on <c>system</c> lines (CONSOLE3c).</summary>
    public IStreamsReader? Beside(SessionStreams streams, Action<string> say) => new ClaudeStreams(streams, say);

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
            "system" when Str(frame, "subtype") == "permission_denied" => Denied(frame),
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
                return new([], [new SessionEvent { Kind = kind, Id = _message, Text = text }]);
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
                    if (!streamed) events.Add(new SessionEvent { Kind = SessionEventKind.Message, Id = Str(message, "id"), Text = text });
                    break;
                case "thinking" when Str(block, "thinking") is { Length: > 0 } thought:
                    lines.Add($"· {FirstLine(thought)}");
                    if (!streamed) events.Add(new SessionEvent { Kind = SessionEventKind.Thought, Id = Str(message, "id"), Text = thought });
                    break;
                case "tool_use":
                    var call = ToolCall(block);
                    // On one line, however long the command — a heredoc printed raw reads as the agent's words.
                    var named = AcpSession.OneLine(call.Title ?? "a tool");
                    if (call.Id is { } callId)
                    {
                        _titles[callId] = named;
                        if (Str(block, "name") is { } tool) _names[callId] = tool;
                    }

                    lines.Add($"→ {named}");
                    events.Add(call);
                    if (Plan(block) is { } plan) events.Add(plan);
                    break;
                // Kept, never dropped (SessionEvents' own rule; REV3): a block this build has no kind for
                // — a redacted thought, a server tool, an image — is its raw record, under its own name.
                case { } other when other is not ("text" or "thinking"):
                    events.Add(new SessionEvent
                    {
                        Kind = SessionEventKind.Raw, Title = other,
                        Raw = SessionEvents.Cut(block.GetRawText(), SessionEvents.RawLimit),
                    });
                    break;
            }
        }

        return new(lines, events);
    }

    private StreamMapped User(JsonElement frame)
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
            // A call the harness denied fails on the wire; the record says what happened to it (HELP4's twin),
            // and the console, the raw view, keeps the wire's word.
            var status = !failed ? "completed" : id is not null && _refused.Contains(id) ? "refused" : "failed";
            // Said by the call's name, never its id — an id says nothing to a person reading along.
            var name = id is not null && _titles.TryGetValue(id, out var known) ? known : "a tool";
            lines.Add(failed ? $"  ✗ {name} failed" : $"  ✓ {name}");
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

        // The turn's denials, which the reference calls the authoritative record where the message before
        // was best-effort: any not said yet is said now, inside the turn it happened in.
        if (frame.TryGetProperty("permission_denials", out var denials) && denials.ValueKind == JsonValueKind.Array)
        {
            foreach (var denial in denials.EnumerateArray().Where(denial => denial.ValueKind == JsonValueKind.Object))
            {
                Refuse(denial, by: null, lines, events);
            }
        }

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
            // The whole turn's counts, as the result sums them over its calls (CONV5) — never a sum of the
            // streamed messages here, whose output the probe found under-counted.
            Tokens = frame.TryGetProperty("usage", out var usage)
                ? TurnTokens.Read(usage, "input_tokens", "output_tokens", "cache_read_input_tokens", "cache_creation_input_tokens")
                : null,
        });

        return new(lines, events);
    }

    /// <summary>A <c>permission_denied</c> frame, as the maker's reference types it (UNBLOCK5).</summary>
    private StreamMapped Denied(JsonElement frame)
    {
        var lines = new List<string>();
        var events = new List<SessionEvent>();
        Refuse(frame, Str(frame, "decision_reason_type"), lines, events);
        return new(lines, events);
    }

    /// <summary>
    /// One denial, from either shape (<c>tool_name</c> and <c>tool_use_id</c> are in both): a console line,
    /// and the call's refusal in the record, each the first time the call is denied.
    /// </summary>
    /// <remarks>
    /// A call the session's record does not hold, a subagent's whose calls run beside the session
    /// (CONSOLE3c), is said on the console and is no card in the conversation. A denial naming no call is
    /// another shape than the reference's, and an absence.
    /// </remarks>
    private void Refuse(JsonElement denial, string? by, List<string> lines, List<SessionEvent> events)
    {
        if (Str(denial, "tool_use_id") is not { } id || !_refused.Add(id)) return;

        var tool = Str(denial, "tool_name") ?? _names.GetValueOrDefault(id);
        var known = _titles.TryGetValue(id, out var title);
        lines.Add($"  permission refused: {(known ? title : AcpSession.OneLine(tool ?? "a tool"))}{(by is null ? "" : $" ({by})")}");
        if (!known) return;

        events.Add(new SessionEvent
        {
            Kind = SessionEventKind.Tool,
            Id = id,
            Status = "refused",
            ToolKind = tool is null ? null : KindOf(tool),
            ToolName = tool,
            RefusedBy = by,
        });
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

        return new([$"— the agent refused a control request: {Str(response, "error") ?? "it gave no reason"}"], []);
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
