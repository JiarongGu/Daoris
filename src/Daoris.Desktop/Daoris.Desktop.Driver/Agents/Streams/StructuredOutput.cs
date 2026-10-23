using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>What one line of a structured stdout became: lines for the console, events for the record.</summary>
public sealed record StreamMapped(IReadOnlyList<string> Lines, IReadOnlyList<SessionEvent> Events)
{
    public static readonly StreamMapped Nothing = new([], []);

    /// <summary>
    /// What the line said about the account's windows, apart from the agent's words (TOOL6c, D130 §5.2): the frame's object,
    /// for the agent's table to read. Null for every other line. Neither the console nor the record shows it.
    /// </summary>
    public JsonElement? Limits { get; init; }

    /// <summary>
    /// Whether the line was outside the harness's protocol (AGT3c): words it printed itself, which no agent wrote, since an
    /// agent's words arrive inside the frames. Shown on the console as ever.
    /// </summary>
    public bool Outside { get; init; }

    /// <summary>
    /// A turn's failure in the harness's own words, apart from its agent's (AGT3c): a failed <c>result</c>'s. Null for every
    /// other line, a turn that did not fail among them.
    /// </summary>
    public string? Failed { get; init; }
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
    /// The harness's own conversation id, once its wire named it (ANSWER1a, D131 §1): what an answer to a park resumes.
    /// Null for a wire that names none, or before it has.
    /// </summary>
    string? Conversation => null;

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
/// <para>The reported model names ride the turn event to D205's machine-local provenance record before token attribution.
/// Token shares remain the usage history's (<see cref="SessionEvent.Models"/>); monetary cost is not kept.</para>
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

    // The models the turn's own answers named, and the result's split as this process last said it (USAGECAP1a2): whether
    // `modelUsage` is the turn's or the process's running total is not recorded, and the turn's models read either.
    private readonly List<string?> _named = [];
    private IReadOnlyDictionary<string, TurnTokens>? _split;

    public AcpUsage? Usage => _usage;

    /// <summary>
    /// The <c>init</c> line's <c>session_id</c>, the first one said (ANSWER1a): <c>SDKSystemMessage</c> declares it, and
    /// <c>--resume</c> takes it. 🔴 The maker's published shape, not yet a line this machine printed.
    /// </summary>
    public string? Conversation { get; private set; }

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
            // Not a frame: the harness said something outside its protocol. Shown, never dropped, and its own words (AGT3c).
            return new([line], []) { Outside = true };
        }

        if (frame.ValueKind != JsonValueKind.Object) return new([line], []) { Outside = true };

        return Text(frame, "type") switch
        {
            "stream_event" => Stream(frame),
            "assistant" => Assistant(frame),
            "user" => User(frame),
            "result" => Result(frame),
            "control_response" => Control(frame),
            "system" when Text(frame, "subtype") == "permission_denied" => Denied(frame),
            "system" when Text(frame, "subtype") == "init" => Init(frame),
            // The account's windows (TOOL6c, limit-signals evidence §1): handed on for the agent's table, and still not the
            // conversation, so neither a line nor an event.
            "rate_limit_event" => frame.TryGetProperty("rate_limit_info", out var info) && info.ValueKind == JsonValueKind.Object
                ? StreamMapped.Nothing with { Limits = info.Clone() }
                : StreamMapped.Nothing,
            // Known and deliberately not the conversation: the session's setup and its status.
            "system" => StreamMapped.Nothing,
            var kind => new([], [new SessionEvent
            {
                Kind = SessionEventKind.Raw, Title = kind ?? "frame", Raw = SessionEventText.Cut(line, SessionEventText.RawLimit),
            }]),
        };
    }

    /// <summary>The session's setup (ANSWER1a): its conversation's id kept, the first said, and nothing rendered, as before.</summary>
    private StreamMapped Init(JsonElement frame)
    {
        if (Conversation is null && Text(frame, "session_id") is { Length: > 0 } id) Conversation = id;
        return StreamMapped.Nothing;
    }

    private StreamMapped Stream(JsonElement frame)
    {
        if (!frame.TryGetProperty("event", out var e) || e.ValueKind != JsonValueKind.Object) return StreamMapped.Nothing;

        switch (Text(e, "type"))
        {
            case "message_start":
                _message = e.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.Object ? Text(m, "id") : null;
                return StreamMapped.Nothing;
            case "content_block_delta" when e.TryGetProperty("delta", out var d) && d.ValueKind == JsonValueKind.Object:
                var (kind, text) = Text(d, "type") switch
                {
                    "text_delta" => (SessionEventKind.Message, Text(d, "text")),
                    "thinking_delta" => (SessionEventKind.Thought, Text(d, "thinking")),
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

        var streamed = Text(message, "id") is { } id && _streamed.Contains(id);
        Measure(message);
        // The model that answered the turn (USAGECAP1a2): a subagent's answer, on a line of its own beside the session, names none.
        // 🔴 Nor does the tool's own message: a refused model or a limit is said as `<synthetic>` (MODELROLE1a's evidence), which
        // no model ran.
        if ((!frame.TryGetProperty("parent_tool_use_id", out var parent) || parent.ValueKind != JsonValueKind.String)
            && Text(message, "model") != "<synthetic>")
        {
            _named.Add(Text(message, "model"));
        }

        var lines = new List<string>();
        var events = new List<SessionEvent>();
        foreach (var block in Blocks(message))
        {
            switch (Text(block, "type"))
            {
                case "text" when Text(block, "text") is { Length: > 0 } text:
                    lines.AddRange(text.Split('\n'));
                    if (!streamed) events.Add(new SessionEvent { Kind = SessionEventKind.Message, Id = Text(message, "id"), Text = text });
                    break;
                case "thinking" when Text(block, "thinking") is { Length: > 0 } thought:
                    lines.Add($"· {FirstLine(thought)}");
                    if (!streamed) events.Add(new SessionEvent { Kind = SessionEventKind.Thought, Id = Text(message, "id"), Text = thought });
                    break;
                case "tool_use":
                    var call = ToolCall(block);
                    // On one line, however long the command — a heredoc printed raw reads as the agent's words.
                    var named = AcpSession.OneLine(call.Title ?? "a tool");
                    if (call.Id is { } callId)
                    {
                        _titles[callId] = named;
                        if (Text(block, "name") is { } tool) _names[callId] = tool;
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
                        Raw = SessionEventText.Cut(block.GetRawText(), SessionEventText.RawLimit),
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
            if (Text(block, "type") != "tool_result") continue;
            var failed = block.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True;
            var id = Text(block, "tool_use_id");
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
        var subtype = Text(frame, "subtype") ?? "unknown";
        // 🔴 A turn the person stopped is `error_during_execution` on this wire, and only its terminal
        // reason says it was stopped rather than broken — the two the probe measured (CONV4a).
        var stopped = Text(frame, "terminal_reason") is "aborted_streaming" or "aborted_tools";
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

        // A failure's words reach the transcript for the person, and the capture as the harness's own (AGT3c): a refused
        // credential is read from them there, never from the transcript, whose last lines may be the agent's words.
        var failure = !stopped && failed && Text(frame, "result") is { Length: > 0 } why ? why : null;
        lines.Add(stopped
            ? "— the turn was stopped"
            : failure is not null
                ? $"— the turn failed ({subtype}): {failure}"
                : $"— the turn ended: {subtype}");
        // The whole turn's counts, as the result sums them over its calls (CONV5) — never a sum of the
        // streamed messages here, whose output the probe found under-counted.
        var tokens = frame.TryGetProperty("usage", out var usage)
            ? TurnTokens.Read(usage, "input_tokens", "output_tokens", "cache_read_input_tokens", "cache_creation_input_tokens")
            : null;
        // Which models they were spent on, for the usage history alone (USAGECAP1a2): `modelUsage` by model where it agrees with
        // the total, read as the turn's own or as the process's running total less the result before.
        var split = frame.TryGetProperty("modelUsage", out var byModel) ? TurnModels.Read(byModel) : null;
        var models = TurnModels.Of(tokens, _named, TurnModels.FromMessages, split, _split, TurnModels.FromModelUsage, TurnModels.Names(byModel));
        _named.Clear();
        if (split is { Count: > 0 }) _split = split;
        events.Add(new SessionEvent
        {
            Kind = SessionEventKind.Turn,
            // The protocol door's words for an ordinary ending and a stopped one, so each reads as one
            // thing whichever door it happened on.
            StopReason = stopped ? "cancelled" : !failed && subtype == "success" ? "end_turn" : subtype,
            Tokens = tokens,
            Models = models,
        });

        return new(lines, events) { Failed = failure };
    }

    /// <summary>A <c>permission_denied</c> frame, as the maker's reference types it (UNBLOCK5).</summary>
    private StreamMapped Denied(JsonElement frame)
    {
        var lines = new List<string>();
        var events = new List<SessionEvent>();
        Refuse(frame, Text(frame, "decision_reason_type"), lines, events);
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
        if (Text(denial, "tool_use_id") is not { } id || !_refused.Add(id)) return;

        var tool = Text(denial, "tool_name") ?? _names.GetValueOrDefault(id);
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
            || Text(response, "subtype") != "error")
        {
            return StreamMapped.Nothing;
        }

        return new([$"— the agent refused a control request: {Text(response, "error") ?? "it gave no reason"}"], []);
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
        var name = Text(block, "name") ?? "tool";
        var input = block.TryGetProperty("input", out var i) && i.ValueKind == JsonValueKind.Object ? i : default;
        var path = input.ValueKind == JsonValueKind.Object
            ? Text(input, "file_path") ?? Text(input, "notebook_path") ?? Text(input, "path")
            : null;

        return new SessionEvent
        {
            Kind = SessionEventKind.Tool,
            Id = Text(block, "id"),
            Title = Title(name, input, path),
            ToolKind = KindOf(name),
            Status = "in_progress",
            Locations = path is null ? null : [path],
            Content = Diffs(name, input, path),
            Input = input.ValueKind == JsonValueKind.Object ? SessionEventText.Cut(input.GetRawText(), SessionEventText.RawLimit) : null,
        };
    }

    /// <summary>A tool's title from its own input — what it acted on, in the tool's own terms.</summary>
    private static string Title(string name, JsonElement input, string? path)
    {
        string? Field(string field) => input.ValueKind == JsonValueKind.Object ? Text(input, field) : null;
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
        return TextBounds.Clip(title, 160);
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
                return [new ToolContent("diff", Path: path, OldText: Text(input, "old_string"), NewText: Text(input, "new_string"))];
            case "Write":
                return [new ToolContent("diff", Path: path, NewText: Text(input, "content"))];
            case "MultiEdit" when input.TryGetProperty("edits", out var edits) && edits.ValueKind == JsonValueKind.Array:
                return [.. edits.EnumerateArray()
                    .Where(edit => edit.ValueKind == JsonValueKind.Object)
                    .Select(edit => new ToolContent("diff", Path: path, OldText: Text(edit, "old_string"), NewText: Text(edit, "new_string")))];
            default:
                return null;
        }
    }

    /// <summary>The harness's to-do list as the plan: it is the one it keeps.</summary>
    private static SessionEvent? Plan(JsonElement block)
    {
        if (Text(block, "name") != "TodoWrite"
            || !block.TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.Object
            || !input.TryGetProperty("todos", out var todos) || todos.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return new SessionEvent
        {
            Kind = SessionEventKind.Plan,
            Entries = [.. todos.EnumerateArray()
                .Where(todo => todo.ValueKind == JsonValueKind.Object && Text(todo, "content") is not null)
                .Select(todo => new PlanEntry(Text(todo, "content")!, Text(todo, "status")))],
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
                .Where(part => part.ValueKind == JsonValueKind.Object && Text(part, "type") == "text")
                .Select(part => Text(part, "text"))
                .OfType<string>()),
            _ => null,
        };
    }

    private static IEnumerable<JsonElement> Blocks(JsonElement message) =>
        message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array
            ? content.EnumerateArray().Where(block => block.ValueKind == JsonValueKind.Object)
            : [];

    private static string FirstLine(string text) => text.Trim().Split('\n', 2)[0];
}
