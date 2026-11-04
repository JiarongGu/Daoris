using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Each of the session's own updates rendered twice from one reading: as an event in the record (<see cref="Map"/>) and as the
/// console's line (<see cref="Render"/>, through <see cref="AcpConsole"/>), with a call this client refused said as refused.
/// </summary>
/// <remarks>
/// After <c>measure</c>, so the options an update changes are kept before the update is shown. The readings are the protocol door's
/// one vocabulary for an update, read by its other units too.
/// </remarks>
[TurnStage("render", TurnPhase.Update, "measure")]
internal sealed class AcpRender(ITurnWiring wiring) : ITurnStage
{
    public ValueTask<bool> RunAsync(TurnFrame frame)
    {
        var update = frame.Update;
        var structured = Refused(Map(update));
        wiring.Track(update, structured);
        wiring.Console.Update(update, structured);
        if (structured is not null) wiring.Emit(structured);
        return ValueTask.FromResult(false);
    }

    /// <summary>
    /// A call this client refused ends on the wire as <c>failed</c>, and the record says <c>refused</c>: the person read Ask Daoris as
    /// failing at a command it was never allowed to run. The console, the raw view, keeps the wire's word beside the refusal's own line.
    /// </summary>
    private SessionEvent? Refused(SessionEvent? mapped) =>
        mapped is { Kind: SessionEventKind.Tool, Status: "failed", Id: { } id } && wiring.Refused.Contains(id)
            ? mapped with { Status = "refused" }
            : mapped;

    /// <summary>
    /// One update as a console line: the structured source rendered for a transcript a person reads.
    /// </summary>
    /// <remarks>
    /// <para>An update shape this build has never seen is rendered as ITSELF rather than dropped. The wire belongs to somebody else and
    /// it grows; a console that silently omitted the one update type it did not recognise would be a transcript with a hole in it that
    /// nothing reports.</para>
    ///
    /// <para>The agent's words are not rendered here: they arrive in chunks, and <see cref="AcpConsole"/> joins them into lines from
    /// the event <see cref="Map"/> made.</para>
    ///
    /// <para>🔴 <b>A tool is named by its title, and only its end is a line</b>. A call streams its input and output as updates that carry
    /// no status, five to eight per call, and each printed as <c>toolu_… → ?</c>. The record keeps every one of them as an event. The
    /// console says the call, then how it ended, by the name the call gave it, which <paramref name="titles"/> remembers per session.</para>
    /// </remarks>
    internal static string? Render(JsonElement update, IDictionary<string, string> titles)
    {
        var kind = Kind(update);
        var id = Field(update, "toolCallId");
        if (kind is "tool_call" or "tool_call_update" && id is not null && Field(update, "title") is { Length: > 0 } title)
        {
            titles[id] = OneLine(title);
        }

        var name = id is not null && titles.TryGetValue(id, out var known) ? known : OneLine(Field(update, "title") ?? "a tool");

        return kind switch
        {
            "agent_message_chunk" or "agent_thought_chunk" => null,
            "tool_call" => $"→ {name}" + (Field(update, "status") is { } s and not ("pending" or "in_progress") ? $" [{s}]" : ""),
            "tool_call_update" => Field(update, "status") switch
            {
                "completed" => $"  ✓ {name}",
                "failed" => $"  ✗ {name} failed",
                // Progress, and the pieces of a call's input and output, say nothing a line should.
                null or "pending" or "in_progress" => null,
                var other => $"  {name}: {other}",
            },
            "usage_update" => Field(update, "used") is { } used && Field(update, "size") is { } size
                ? $"  context {used}/{size}"
                : null,
            // As the record keeps none (Map): the door's notes say the waits on the console.
            "async_task_spawned" or "async_task_progress" or "async_task_state_update" => null,
            null => $"[update] {Compact(update)}",
            _ => $"[{kind}] {Compact(update)}",
        };
    }

    /// <summary>
    /// A tool's name as one console line: a shell call's title is its whole command, and a heredoc printed across raw lines that then
    /// read as the agent's words. The first line, capped.
    /// </summary>
    internal static string OneLine(string title, int limit = 160)
    {
        var first = title.ReplaceLineEndings("\n").Split('\n', 2);
        var line = first[0].TrimEnd();
        var cut = line.Length > limit || first.Length > 1;
        return (line.Length > limit ? line[..limit].TrimEnd() : line) + (cut ? " …" : "");
    }

    /// <summary>
    /// One update as an event in Daoris's vocabulary: the structure <see cref="Render"/> flattens for the console, kept.
    /// </summary>
    /// <remarks>
    /// <para><b>The protocol's own fields, renamed and nothing more.</b> A tool call's kind, title, status, places and content are what
    /// the wire says they are; nothing is inferred from a title or parsed out of text.</para>
    ///
    /// <para>🔴 <b>Every read is shape-checked.</b> The wire is somebody else's and it grows: a real <c>tool_call</c> carries
    /// <c>content</c> as a list where a stub sent an object, and reading one as the other took a whole turn down. A field of an
    /// unexpected shape is absent here, never a throw.</para>
    ///
    /// <para><b>An update this build does not know is kept raw</b>, with its kind as the title, for the same reason the console shows
    /// it: a record with a silent hole in it reports nothing.</para>
    /// </remarks>
    internal static SessionEvent? Map(JsonElement update)
    {
        var kind = Kind(update);
        return kind switch
        {
            // The message a chunk belongs to rides with it: two messages in a row are two, which the chunks alone cannot say.
            "agent_message_chunk" => Text(update) is { } message
                ? new SessionEvent { Kind = SessionEventKind.Message, Id = StringField(update, "messageId"), Text = message }
                : null,
            "agent_thought_chunk" => Text(update) is { } thought
                ? new SessionEvent { Kind = SessionEventKind.Thought, Id = StringField(update, "messageId"), Text = thought }
                : null,
            "user_message_chunk" => Text(update) is { } said
                ? new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = said }
                : null,
            "tool_call" or "tool_call_update" => new SessionEvent
            {
                Kind = SessionEventKind.Tool,
                Id = StringField(update, "toolCallId"),
                Title = StringField(update, "title"),
                ToolKind = StringField(update, "kind"),
                Status = StringField(update, "status"),
                Locations = Locations(update),
                Line = LocationLine(update),
                Content = Contents(update),
                Input = update.TryGetProperty("rawInput", out var input) ? Compact(input) : null,
                Output = update.TryGetProperty("rawOutput", out var output) ? Compact(output) : null,
            },
            "plan" => new SessionEvent { Kind = SessionEventKind.Plan, Entries = Entries(update) },
            "usage_update" => new SessionEvent
            {
                Kind = SessionEventKind.Usage, Used = Json.Long(update, "used"), Size = Json.Long(update, "size"),
            },
            // Known, and deliberately not the conversation: the session's own settings, the commands it offers, its mode, its config
            // options. The options are kept by the session itself, and a person's change to one is the record's note, not the update.
            // The console shows them; in the record they were rows over a chat nobody had spoken in yet.
            "available_commands_update" or "current_mode_update" or "config_option_update" or "session_info_update" => null,
            // The session's background tasks, where no stream takes them. The call that started one is its card, the door's notes say
            // what it waited on, and a raw row would carry the harness's output path into the record.
            "async_task_spawned" or "async_task_progress" or "async_task_state_update" => null,
            _ => new SessionEvent { Kind = SessionEventKind.Raw, Title = kind ?? "update", Raw = Compact(update) },
        };
    }

    /// <summary>A string field, or null when absent or not a string.</summary>
    internal static string? StringField(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>The paths a tool call names in <c>locations</c>, when it is a list of objects with one.</summary>
    private static IReadOnlyList<string>? Locations(JsonElement update)
    {
        var paths = Located(update).Select(location => StringField(location, "path")!).ToList();
        return paths.Count == 0 ? null : paths;
    }

    /// <summary>
    /// The line the first location with a path names, when it is a whole number of zero or more: the one <see cref="Locations"/>'
    /// first path pairs with, so a line never rides with a path it was not said of.
    /// </summary>
    private static long? LocationLine(JsonElement update) =>
        Located(update).FirstOrDefault() is { ValueKind: JsonValueKind.Object } first
        && first.TryGetProperty("line", out var line)
        && line.ValueKind == JsonValueKind.Number
        && line.TryGetInt64(out var number)
        && number >= 0
            ? number
            : null;

    /// <summary>The entries of <c>locations</c> that are objects naming a path, in the wire's order.</summary>
    private static IEnumerable<JsonElement> Located(JsonElement update) =>
        update.TryGetProperty("locations", out var locations) && locations.ValueKind == JsonValueKind.Array
            ? locations.EnumerateArray().Where(location => location.ValueKind == JsonValueKind.Object && StringField(location, "path") is not null)
            : [];

    /// <summary>
    /// A tool call's <c>content</c> in the protocol's three shapes, a content block (its text), a diff, a terminal, when it is a list.
    /// Anything else in the list is kept as its raw JSON, as text.
    /// </summary>
    private static IReadOnlyList<ToolContent>? Contents(JsonElement update)
    {
        if (!update.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = new List<ToolContent>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            switch (StringField(item, "type"))
            {
                case "diff":
                    items.Add(new ToolContent("diff", Path: StringField(item, "path"),
                        OldText: StringField(item, "oldText"), NewText: StringField(item, "newText")));
                    break;
                case "terminal":
                    items.Add(new ToolContent("terminal", Text: StringField(item, "terminalId")));
                    break;
                case "content" when item.TryGetProperty("content", out var block) && block.ValueKind == JsonValueKind.Object:
                    items.Add(StringField(block, "type") == "text"
                        ? new ToolContent("text", Text: StringField(block, "text"))
                        : new ToolContent(StringField(block, "type") ?? "content", Text: Compact(block)));
                    break;
                default:
                    items.Add(new ToolContent("raw", Text: Compact(item)));
                    break;
            }
        }

        return items.Count == 0 ? null : items;
    }

    /// <summary>A plan's entries, each with its content; one without content is skipped.</summary>
    private static IReadOnlyList<PlanEntry>? Entries(JsonElement update)
    {
        if (!update.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return [.. entries.EnumerateArray()
            .Where(entry => entry.ValueKind == JsonValueKind.Object && StringField(entry, "content") is not null)
            .Select(entry => new PlanEntry(StringField(entry, "content")!, StringField(entry, "status"), StringField(entry, "priority")))];
    }

    /// <summary><c>content.text</c>, which is where every textual update carries its words.</summary>
    /// <remarks>
    /// 🔴 Only when <c>content</c> is an OBJECT. A real <c>tool_call</c> carries it as a list, and reading a list as an object throws,
    /// which, in the reader, took the whole turn down at the first tool call of the first real run.
    /// </remarks>
    private static string? Text(JsonElement update) =>
        update.TryGetProperty("content", out var content) ? Json.Text(content, "text") : null;

    /// <summary>Which update this is, or null when the wire did not say in a string.</summary>
    internal static string? Kind(JsonElement update) =>
        update.TryGetProperty("sessionUpdate", out var kind) && kind.ValueKind == JsonValueKind.String
            ? kind.GetString()
            : null;

    private static string? Field(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText()
            : null;

    /// <summary>Bounded raw JSON: enough to recognise, never a log file on one line.</summary>
    internal static string Compact(JsonElement element)
    {
        var raw = element.GetRawText();
        return TextBounds.ClipCounted(raw, 400);
    }
}
