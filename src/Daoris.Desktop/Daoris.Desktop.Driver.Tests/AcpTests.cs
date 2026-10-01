using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The protocol door (D53/ACP1), driven against a fake agent over in-memory streams.
/// </summary>
/// <remarks>
/// <para>No process and no model: the wire is the thing under test, and a fake that answers frames is
/// a truer subject than a real harness would be — it can be made to send the updates a real one sends
/// rarely (a permission request mid-turn, a refusal, a cancel).</para>
///
/// <para>What the driver takes from this wire is deliberately narrow (D46): the updates ENRICH the
/// console and the transcript, and the session RECORD still moves on the exit code and the quest's
/// own state. The wire flattens an aborted or errored turn to `end_turn`, so believing its stop reason
/// would be believing a self-report over an observation.</para>
/// </remarks>
public sealed class AcpTests
{
    /// <summary>A scripted agent: a handler turns each frame the client sends into the frames it gets back.</summary>
    private sealed class FakeAgent
    {
        private readonly Channel<string> _toClient = Channel.CreateUnbounded<string>();
        private readonly Func<JsonElement, FakeAgent, string?> _handle;

        public FakeAgent(Func<JsonElement, FakeAgent, string?> handle) => _handle = handle;

        public List<string> Sent { get; } = [];

        public TextReader Incoming => new ChannelReader(_toClient);

        public TextWriter Outgoing => new HandlerWriter(line =>
        {
            Sent.Add(line);
            var frame = JsonDocument.Parse(line).RootElement;
            var reply = _handle(frame, this);
            if (reply is not null) Push(reply);
        });

        /// <summary>Send an unsolicited frame — a notification, or a request of the agent's own.</summary>
        public void Push(string json) => _toClient.Writer.TryWrite(json);

        public void Close() => _toClient.Writer.TryComplete();

        /// <summary>The method name of the nth frame the client sent, for asserting call order.</summary>
        public string Method(int index) =>
            JsonDocument.Parse(Sent[index]).RootElement.TryGetProperty("method", out var m)
                ? m.GetString()! : "(response)";

        public JsonElement Frame(int index) => JsonDocument.Parse(Sent[index]).RootElement;

        private sealed class ChannelReader(Channel<string> channel) : TextReader
        {
            public override Task<string?> ReadLineAsync() => ReadLineAsync(CancellationToken.None).AsTask();

            /// <summary>Honours the token, so a cancelled run's read pump ends rather than hanging.</summary>
            public override async ValueTask<string?> ReadLineAsync(CancellationToken ct) =>
                await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false)
                && channel.Reader.TryRead(out var line) ? line : null;
        }

        private sealed class HandlerWriter(Action<string> onLine) : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;

            public override Task WriteLineAsync(string? value)
            {
                if (value is not null) onLine(value);
                return Task.CompletedTask;
            }
        }
    }

    private static string Ok(JsonElement request, string resultJson) =>
        $$"""{"jsonrpc":"2.0","id":{{request.GetProperty("id").GetRawText()}},"result":{{resultJson}}}""";

    /// <summary>The ordinary run: handshake, a session on the tree, a prompt, updates, a stop reason.</summary>
    [Fact]
    public async Task A_session_runs_initialize_new_and_prompt_in_order_on_the_tree()
    {
        var lines = new List<string>();
        var agent = new FakeAgent((frame, self) =>
        {
            var method = frame.GetProperty("method").GetString();
            switch (method)
            {
                case "initialize":
                    return Ok(frame, """{"protocolVersion":1,"agentCapabilities":{}}""");
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"working on it"}}}}""");
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default:
                    // A real agent answers the requests it knows and ignores notifications.
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        var outcome = await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add)
            .RunAsync("D:/fam/Game", "take quest #abc123", CancellationToken.None);

        Assert.Equal("initialize", agent.Method(0));
        Assert.Equal("session/new", agent.Method(1));
        Assert.Equal("D:/fam/Game", agent.Frame(1).GetProperty("params").GetProperty("cwd").GetString());
        Assert.Equal("session/prompt", agent.Method(2));
        Assert.Equal(
            "take quest #abc123",
            agent.Frame(2).GetProperty("params").GetProperty("prompt")[0].GetProperty("text").GetString());
        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Contains(lines, line => line.Contains("working on it"));
    }

    /// <summary>
    /// The rules Daoris composed ride `session/new` as the adapter names them (PERM1, D72) — and a
    /// session given none sends no `_meta` at all, exactly as before the rules existed.
    /// </summary>
    [Fact]
    public async Task Session_new_carries_the_adapters_meta_and_none_when_there_is_none()
    {
        FakeAgent Agent() => new((frame, self) =>
        {
            var method = frame.GetProperty("method").GetString();
            return method switch
            {
                "initialize" => Ok(frame, """{"protocolVersion":1,"agentCapabilities":{}}"""),
                "session/new" => Ok(frame, """{"sessionId":"s-1"}"""),
                "session/prompt" => Ok(frame, """{"stopReason":"end_turn"}"""),
                _ => frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null,
            };
        });

        var with = Agent();
        await new AcpSession(with.Incoming, with.Outgoing, _ => { },
                meta: new { claudeCode = new { options = new { settings = "C:/somewhere/data/spawn/s1.settings.json" } } })
            .RunAsync("D:/fam/Game", "do the thing", CancellationToken.None);
        Assert.Equal(
            "C:/somewhere/data/spawn/s1.settings.json",
            with.Frame(1).GetProperty("params").GetProperty("_meta").GetProperty("claudeCode")
                .GetProperty("options").GetProperty("settings").GetString());

        var without = Agent();
        await new AcpSession(without.Incoming, without.Outgoing, _ => { })
            .RunAsync("D:/fam/Game", "do the thing", CancellationToken.None);
        Assert.False(without.Frame(1).GetProperty("params").TryGetProperty("_meta", out _));
    }

    /// <summary>
    /// A permission request is answered by REJECTING, always (D52: a better approval surface must not
    /// widen autonomy). A request reaching the driver at all means the repository's own checked-in
    /// posture did not already cover the action — and the driver is not the party that may widen it.
    /// </summary>
    [Fact]
    public async Task A_permission_request_is_refused_rather_than_granted()
    {
        string? answered = null;
        var agent = new FakeAgent((frame, self) =>
        {
            if (!frame.TryGetProperty("method", out var method))
            {
                // The client's ANSWER to the permission request came back as a response frame.
                answered = frame.GetProperty("result").GetProperty("outcome").GetRawText();
                return null;
            }

            switch (method.GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("""
                        {"jsonrpc":"2.0","id":900,"method":"session/request_permission","params":{"sessionId":"s-1","toolCall":{"title":"push"},"options":[{"optionId":"yes","name":"Allow","kind":"allow_once"},{"optionId":"no","name":"Reject","kind":"reject_once"}]}}
                        """.Trim());
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                // A real agent answers the requests it knows and ignores notifications.
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/fam/Game", "push the branch", CancellationToken.None);

        Assert.NotNull(answered);
        Assert.Contains("selected", answered);
        // The REJECT option, by its kind — never the allow, and never by position.
        Assert.Contains("\"no\"", answered);
    }

    /// <summary>
    /// With no reject option offered, the answer is `cancelled` — never the first option that happens
    /// to be there. Fail closed means closed even when the menu is unhelpful.
    /// </summary>
    [Fact]
    public async Task A_permission_request_with_no_refusal_offered_is_cancelled()
    {
        string? answered = null;
        var agent = new FakeAgent((frame, self) =>
        {
            if (!frame.TryGetProperty("method", out var method))
            {
                answered = frame.GetProperty("result").GetProperty("outcome").GetRawText();
                return null;
            }

            switch (method.GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("""{"jsonrpc":"2.0","id":901,"method":"session/request_permission","params":{"sessionId":"s-1","toolCall":{"title":"rm -rf"},"options":[{"optionId":"yes","name":"Allow","kind":"allow_always"}]}}""");
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                // A real agent answers the requests it knows and ignores notifications.
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/fam/Game", "delete everything", CancellationToken.None);

        Assert.NotNull(answered);
        Assert.Contains("cancelled", answered);
        Assert.DoesNotContain("selected", answered);
    }

    /// <summary>
    /// A permission request whose shape this client cannot read is still ANSWERED — cancelled, which is
    /// the refusal (REV3 chat F10). It used to throw while reading the options, and the pump showed the
    /// frame as unreadable and went on, while the agent waited for an answer that never came: a turn
    /// hung for ever.
    /// </summary>
    [Theory]
    [InlineData("""{"sessionId":"s-1","options":"not a list"}""")]
    [InlineData("""{"sessionId":"s-1","options":[1, "x", {"kind":7,"optionId":"no"}]}""")]
    [InlineData("\"params that are a string\"")]
    public async Task A_permission_request_of_an_unreadable_shape_is_still_answered(string parameters)
    {
        string? answered = null;
        var agent = new FakeAgent((frame, self) =>
        {
            if (!frame.TryGetProperty("method", out var method))
            {
                if (frame.TryGetProperty("id", out var id) && id.GetRawText() == "902")
                {
                    answered = frame.GetProperty("result").GetProperty("outcome").GetRawText();
                }

                return null;
            }

            switch (method.GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push($$"""{"jsonrpc":"2.0","id":902,"method":"session/request_permission","params":{{parameters}}}""");
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/fam/Game", "do it", CancellationToken.None);

        Assert.NotNull(answered);
        Assert.Contains("cancelled", answered);
    }

    /// <summary>
    /// A tool call and its outcome reach the console as readable lines — the structured source the
    /// timeline needs, rendered for the transcript without anything being parsed out of stdout.
    /// </summary>
    [Fact]
    public async Task Tool_calls_and_their_outcomes_are_rendered_for_the_console()
    {
        var lines = new List<string>();
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call","toolCallId":"c1","title":"read","status":"in_progress"}}}""");
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call_update","toolCallId":"c1","status":"completed"}}}""");
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                // A real agent answers the requests it knows and ignores notifications.
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add)
            .RunAsync("D:/fam/Game", "read the readme", CancellationToken.None);

        Assert.Equal(["→ read", "  ✓ read"], lines.Where(line => line.Contains("read")));
    }

    /// <summary>
    /// 🔴 A console full of <c>?</c>. Measured in FG5's
    /// transcripts: every tool call was followed by five to eight updates that carry no status (its
    /// input and output arriving in pieces), each printed as <c>toolu_… → ?</c>, and its end printed
    /// its id, which says nothing to a person. An update with no status says nothing to the console,
    /// and an ending names the tool by the title its call gave it.
    /// </summary>
    /// <summary>
    /// 🔴 Measured on FG5's apply session: a shell call's title is its whole command, heredoc and all,
    /// and it printed across a dozen raw lines — which then read as the agent's own words, so a commit
    /// message became the "last words" a parked card quoted. A tool is named on one line.
    /// </summary>
    [Fact]
    public async Task A_tool_is_named_on_one_line_however_long_its_command()
    {
        var lines = new List<string>();
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call","toolCallId":"t1","title":"git commit -F - <<'EOF'\ndocs: name the role\n\nThe body of the message.\nEOF","status":"pending"}}}""");
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call_update","toolCallId":"t1","status":"completed"}}}""");
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add)
            .RunAsync("D:/fam/Game", "commit it", CancellationToken.None);

        Assert.Equal(["→ git commit -F - <<'EOF' …", "  ✓ git commit -F - <<'EOF' …"], lines.Where(line => line.Contains("git commit")));
        Assert.DoesNotContain(lines, line => line.Contains("The body of the message."));
    }

    [Fact]
    public async Task A_tools_progress_updates_print_nothing_and_its_end_names_the_tool()
    {
        var lines = new List<string>();
        static string Update(string fields) =>
            $$$$"""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call_update","toolCallId":"toolu_01AB",{{{{fields}}}}}}}""";
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call","toolCallId":"toolu_01AB","title":"grep","status":"pending"}}}""");
                    self.Push(Update("\"rawInput\":{\"pattern\":\"note\"}"));
                    self.Push(Update("\"content\":[]"));
                    self.Push(Update("\"status\":\"in_progress\""));
                    self.Push(Update("\"title\":\"grep note in src\""));
                    self.Push(Update("\"status\":\"completed\""));
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call","toolCallId":"toolu_02CD","title":"Terminal","status":"pending"}}}""");
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call_update","toolCallId":"toolu_02CD","status":"failed"}}}""");
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add)
            .RunAsync("D:/fam/Game", "find the notes", CancellationToken.None);

        var tools = lines.Where(line => line.TrimStart().StartsWith('→') || line.Contains('✓') || line.Contains('✗')).ToList();
        Assert.Equal(["→ grep", "  ✓ grep note in src", "→ Terminal", "  ✗ Terminal failed"], tools);
        Assert.DoesNotContain(lines, line => line.Contains('?'));
        Assert.DoesNotContain(lines, line => line.Contains("toolu_"));
    }

    /// <summary>
    /// D76 §1 (CONV1): the wire's structure reaches the record as typed events, where it used to be
    /// flattened into console lines and lost — the message, the thought, the tool call with its kind,
    /// its places, its input and its diff, the plan, the usage, and the turn's end. The console still
    /// gets its lines; the events are beside them, not instead.
    /// </summary>
    [Fact]
    public async Task The_wires_structure_reaches_the_record_as_typed_events()
    {
        var lines = new List<string>();
        var events = new List<SessionEvent>();
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"agent_thought_chunk","content":{"type":"text","text":"the cap belongs in the streamer"}}}}""");
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"plan","entries":[{"content":"read the streamer","priority":"high","status":"completed"},{"content":"cap it","priority":"medium","status":"in_progress"}]}}}""");
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call","toolCallId":"c1","title":"Edit src/chunk.rs","kind":"edit","status":"pending","locations":[{"path":"src/chunk.rs","line":12}],"rawInput":{"file":"src/chunk.rs"},"content":[]}}}""");
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call_update","toolCallId":"c1","status":"completed","content":[{"type":"diff","path":"src/chunk.rs","oldText":"let cap = 0;","newText":"let cap = 4;"},{"type":"content","content":{"type":"text","text":"Edited."}}]}}}""");
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"Capped at "}}}}""");
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"4 per frame."}}}}""");
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"usage_update","used":38000,"size":200000}}}""");
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add, onEvent: events.Add)
            .RunAsync("D:/fam/Game", "cap the hydration", CancellationToken.None);

        Assert.Equal(
            [SessionEventKind.Thought, SessionEventKind.Plan, SessionEventKind.Tool, SessionEventKind.Tool,
             SessionEventKind.Message, SessionEventKind.Message, SessionEventKind.Usage, SessionEventKind.Turn],
            events.Select(e => e.Kind));

        Assert.Equal("the cap belongs in the streamer", events[0].Text);
        Assert.Equal(["read the streamer", "cap it"], events[1].Entries!.Select(entry => entry.Content));
        Assert.Equal("in_progress", events[1].Entries![1].Status);

        var call = events[2];
        Assert.Equal("c1", call.Id);
        Assert.Equal("Edit src/chunk.rs", call.Title);
        Assert.Equal("edit", call.ToolKind);
        Assert.Equal("pending", call.Status);
        Assert.Equal(["src/chunk.rs"], call.Locations);
        Assert.Equal(12, call.Line);
        Assert.Contains("src/chunk.rs", call.Input);

        var done = events[3];
        Assert.Equal("c1", done.Id);
        Assert.Equal("completed", done.Status);
        Assert.Equal("diff", done.Content![0].Type);
        Assert.Equal("let cap = 4;", done.Content[0].NewText);
        Assert.Equal("let cap = 0;", done.Content[0].OldText);
        Assert.Equal("text", done.Content[1].Type);
        Assert.Equal("Edited.", done.Content[1].Text);

        Assert.Equal("Capped at 4 per frame.", string.Concat(events[4].Text, events[5].Text));
        Assert.Equal((38000L, 200000L), (events[6].Used!.Value, events[6].Size!.Value));
        Assert.Equal("end_turn", events[7].StopReason);

        // The console is unchanged: the lines are still there for the raw view.
        Assert.Contains(lines, line => line.Contains("Edit src/chunk.rs"));
    }

    /// <summary>
    /// A location's line rides with its path (LEFT2): the line of the first location that names a path, which is
    /// the path a card opens, and never a later location's. A line that is not a whole number of zero or more is
    /// no line: the wire is shape-checked, as every read of it is.
    /// </summary>
    [Theory]
    [InlineData("""[{"path":"src/a.ts","line":40},{"path":"src/b.ts","line":3}]""", 40L)]
    [InlineData("""[{"path":"src/a.ts"},{"path":"src/b.ts","line":3}]""", null)]
    [InlineData("""[{"line":7},{"path":"src/b.ts","line":3}]""", 3L)]
    [InlineData("""[{"path":"src/a.ts","line":"40"}]""", null)]
    [InlineData("""[{"path":"src/a.ts","line":-1}]""", null)]
    [InlineData("""[{"path":"src/a.ts","line":1.5}]""", null)]
    [InlineData("""[]""", null)]
    public void A_location_s_line_rides_with_the_first_path(string locations, long? line)
    {
        using var update = JsonDocument.Parse(
            $$"""{"sessionUpdate":"tool_call","toolCallId":"c1","title":"Edit src/a.ts","kind":"edit","locations":{{locations}}}""");

        var mapped = AcpSession.Map(update.RootElement)!;

        Assert.Equal(line, mapped.Line);
        // The line pairs with the first path the event keeps, so an event with no path has no line either.
        if (mapped.Locations is null) Assert.Null(mapped.Line);
    }

    /// <summary>One streamed piece of the agent's words, as the wire sends it.</summary>
    private static string Chunk(string kind, string text) => new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["method"] = "session/update",
        ["params"] = new JsonObject
        {
            ["sessionId"] = "s-1",
            ["update"] = new JsonObject
            {
                ["sessionUpdate"] = kind,
                ["content"] = new JsonObject { ["type"] = "text", ["text"] = text },
            },
        },
    }.ToJsonString();

    /// <summary>
    /// 🔴 UX5 U3: a message arrives in chunks, and the console wrote each chunk as a line of its own, so
    /// the raw view broke words across lines, driven sessions included. A chunk joins the line it
    /// continues, a newline in the words ends one, and anything else the wire says ends the open line
    /// first. The record is untouched: it still keeps each chunk as the wire sent it.
    /// </summary>
    [Fact]
    public async Task A_streamed_message_reaches_the_console_as_its_lines_not_its_chunks()
    {
        var lines = new List<string>();
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push(Chunk("agent_thought_chunk", "the cap belongs "));
                    self.Push(Chunk("agent_thought_chunk", "in the streamer"));
                    self.Push(Chunk("agent_message_chunk", "Capped at "));
                    self.Push(Chunk("agent_message_chunk", "4 per frame.\r\nThe te"));
                    self.Push(Chunk("agent_message_chunk", "sts pass."));
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call","toolCallId":"c1","title":"Run tests","status":"pending"}}}""");
                    self.Push(Chunk("agent_message_chunk", "Do"));
                    self.Push(Chunk("agent_message_chunk", "ne."));
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        // No quiet flush here, so a loaded machine that pauses the reader between two chunks cannot
        // split a line: going quiet is its own test, below.
        await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add, quiet: Timeout.InfiniteTimeSpan)
            .RunAsync("D:/fam/Game", "cap the hydration", CancellationToken.None);

        Assert.Equal(
            ["· the cap belongs in the streamer", "Capped at 4 per frame.", "The tests pass.", "→ Run tests", "Done."],
            lines);
    }

    /// <summary>
    /// 🔴 A line the agent's words leave open is shown once they go quiet, while the turn is still
    /// running. Joining chunks alone held `acp heard: hold this turn` until the next update, and an
    /// agent that says something and then waits sends none: the family rehearsal's stop never came,
    /// because the words it waited for never reached the console.
    /// </summary>
    [Fact]
    public async Task Words_left_on_an_open_line_are_shown_once_the_agent_goes_quiet()
    {
        var lines = new ConcurrentQueue<string>();
        using var cts = new CancellationTokenSource();
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push(Chunk("agent_message_chunk", "acp heard: "));
                    self.Push(Chunk("agent_message_chunk", "hold this turn"));
                    return null; // held: the turn runs until it is stopped
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        var run = new AcpSession(agent.Incoming, agent.Outgoing, lines.Enqueue, quiet: TimeSpan.FromMilliseconds(50))
            .RunAsync("D:/fam/Game", "hold this turn", cts.Token);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!lines.Contains("acp heard: hold this turn") && DateTime.UtcNow < deadline) await Task.Delay(20);
        // Read BEFORE the stop: stopping ends the reader, whose last act shows the open line anyway.
        var shownWhileHeld = lines.Contains("acp heard: hold this turn") && !run.IsCompleted;
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.Equal(["acp heard: hold this turn"], lines);
        Assert.True(shownWhileHeld, "the words reached the console only after the turn ended");
    }

    /// <summary>
    /// And a line still open when the agent's stream ends is shown, not lost with the process: an
    /// agent's last words before it died are the ones a person reads the console for.
    /// </summary>
    [Fact]
    public async Task The_words_an_agent_said_before_its_stream_ended_are_still_shown()
    {
        var lines = new List<string>();
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push(Chunk("agent_message_chunk", "out of "));
                    self.Push(Chunk("agent_message_chunk", "memory"));
                    self.Close();
                    return null;
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        await Assert.ThrowsAsync<DriverException>(() => new AcpSession(agent.Incoming, agent.Outgoing, lines.Add, quiet: Timeout.InfiniteTimeSpan)
            .RunAsync("D:/fam/Game", "hello", CancellationToken.None));

        Assert.Equal(["out of memory"], lines);
    }

    /// <summary>A refusal is part of what happened in the conversation, so it is in the record too.</summary>
    [Fact]
    public async Task A_refused_permission_is_a_note_in_the_record()
    {
        var events = new List<SessionEvent>();
        var agent = new FakeAgent((frame, self) =>
        {
            if (!frame.TryGetProperty("method", out var method)) return null; // the client's answer

            switch (method.GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("""{"jsonrpc":"2.0","id":900,"method":"session/request_permission","params":{"sessionId":"s-1","toolCall":{"toolCallId":"c9","title":"git push"},"options":[{"optionId":"no","name":"Reject","kind":"reject_once"}]}}""");
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: events.Add)
            .RunAsync("D:/fam/Game", "push it", CancellationToken.None);

        var note = Assert.Single(events, e => e.Kind == SessionEventKind.Note);
        Assert.Contains("permission refused", note.Text);
        // SESS1 S6: the call by its title, as a reader knows it — never the request's JSON — and the
        // call's id, so the page keeps the refusal with the call it refused.
        Assert.Contains("`git push`", note.Text);
        Assert.DoesNotContain("toolCallId", note.Text);
        Assert.Equal("c9", note.Id);
    }

    /// <summary>
    /// HELP4: a call the driver refused ends on the wire as `failed`, and the record says `refused`: the
    /// person saw Ask Daoris "fail" at a command it was never allowed to run. The console keeps the
    /// wire's own word, beside the refusal's line.
    /// </summary>
    [Fact]
    public async Task A_call_the_driver_refused_is_recorded_as_refused_not_failed()
    {
        var events = new List<SessionEvent>();
        var lines = new List<string>();
        var agent = new FakeAgent((frame, self) =>
        {
            if (!frame.TryGetProperty("method", out var method)) return null; // the client's answer

            switch (method.GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push(Update("s-1", """{"sessionUpdate":"tool_call","toolCallId":"c9","title":"ls data","kind":"execute","status":"pending"}"""));
                    self.Push("""{"jsonrpc":"2.0","id":900,"method":"session/request_permission","params":{"sessionId":"s-1","toolCall":{"toolCallId":"c9","title":"ls data"},"options":[{"optionId":"no","name":"Reject","kind":"reject_once"}]}}""");
                    self.Push(Update("s-1", """{"sessionUpdate":"tool_call_update","toolCallId":"c9","status":"failed"}"""));
                    self.Push(Update("s-1", """{"sessionUpdate":"tool_call","toolCallId":"c10","title":"cargo build","kind":"execute","status":"pending"}"""));
                    self.Push(Update("s-1", """{"sessionUpdate":"tool_call_update","toolCallId":"c10","status":"failed"}"""));
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add, onEvent: events.Add)
            .RunAsync("D:/fam/Game", "look", CancellationToken.None);

        Assert.Equal("refused", events.Last(e => e.Kind == SessionEventKind.Tool && e.Id == "c9").Status);
        // A call nobody refused that failed is still a failure.
        Assert.Equal("failed", events.Last(e => e.Kind == SessionEventKind.Tool && e.Id == "c10").Status);
        Assert.Contains("  ✗ ls data failed", lines);
    }

    /// <summary>
    /// UNBLOCK5 (D122 §3.10), the protocol door end to end: each permission request the driver refused, once
    /// the agent reports its call failed, is one ask in the machine log, by the kind the call was announced
    /// with. The request's title and input never reach the log.
    /// </summary>
    [Fact]
    public async Task A_call_the_driver_refused_is_one_ask_in_the_machine_log()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-acp-asks-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        try
        {
            var events = new SessionEvents(Path.Combine(home, "sessions"));
            using (var log = new MachineLog(home, "desktop"))
            using (var client = new ServiceClient("http://ledger.test", null, new HttpClient()))
            using (new SessionLog(log, client, events))
            {
                var agent = new FakeAgent((frame, self) =>
                {
                    if (!frame.TryGetProperty("method", out var method)) return null;

                    switch (method.GetString())
                    {
                        case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                        case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                        case "session/prompt":
                            self.Push(Update("s-1", """{"sessionUpdate":"tool_call","toolCallId":"c9","title":"git push origin main","kind":"execute","status":"pending","rawInput":{"command":"git push origin main"}}"""));
                            self.Push("""{"jsonrpc":"2.0","id":900,"method":"session/request_permission","params":{"sessionId":"s-1","toolCall":{"toolCallId":"c9","title":"git push origin main"},"options":[{"optionId":"no","name":"Reject","kind":"reject_once"}]}}""");
                            self.Push(Update("s-1", """{"sessionUpdate":"tool_call_update","toolCallId":"c9","status":"failed"}"""));
                            self.Push(Update("s-1", """{"sessionUpdate":"tool_call","toolCallId":"c10","title":"cargo build","kind":"execute","status":"pending"}"""));
                            self.Push(Update("s-1", """{"sessionUpdate":"tool_call_update","toolCallId":"c10","status":"failed"}"""));
                            return Ok(frame, """{"stopReason":"end_turn"}""");
                        default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
                    }
                });

                await new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: e => events.Append("d1", e))
                    .RunAsync("D:/fam/Game", "push it", CancellationToken.None);
            }

            var written = string.Join('\n', Directory.GetFiles(Path.Combine(home, MachineLog.Folder)).Select(File.ReadAllText));
            var asks = written.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => JsonDocument.Parse(line).RootElement)
                .Where(line => line.GetProperty("event").GetString() == "permission.refused")
                .Select(line => line.GetProperty("data"))
                .ToList();
            var ask = Assert.Single(asks);
            Assert.Equal(("d1", "execute"), (ask.GetProperty("session").GetString(), ask.GetProperty("kind").GetString()));
            Assert.DoesNotContain("git push", written);
            Assert.DoesNotContain("cargo build", written);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>A call the wire names by no title is refused as what it is, bounded, still never its JSON.</summary>
    [Fact]
    public async Task A_refused_call_with_no_title_is_named_by_its_kind()
    {
        var events = new List<SessionEvent>();
        var lines = new List<string>();
        var agent = new FakeAgent((frame, self) =>
        {
            if (!frame.TryGetProperty("method", out var method)) return null;

            switch (method.GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("""{"jsonrpc":"2.0","id":901,"method":"session/request_permission","params":{"sessionId":"s-1","toolCall":{"toolCallId":"c10","kind":"execute","rawInput":{"command":"rm -rf build"}},"options":[{"optionId":"no","name":"Reject","kind":"reject_once"}]}}""");
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add, onEvent: events.Add)
            .RunAsync("D:/fam/Game", "clean it", CancellationToken.None);

        var note = Assert.Single(events, e => e.Kind == SessionEventKind.Note);
        Assert.Contains("an unnamed `execute` call", note.Text);
        Assert.DoesNotContain("rawInput", note.Text);
        // The console is the raw view, and keeps the request as the wire said it.
        Assert.Contains(lines, line => line.Contains("rawInput", StringComparison.Ordinal));
    }

    /// <summary>
    /// An update this build does not know is kept raw rather than dropped — the record's version of
    /// the console's rule that a wire that grows must not leave a silent hole.
    /// </summary>
    [Fact]
    public async Task An_unknown_update_is_kept_raw_in_the_record()
    {
        var events = new List<SessionEvent>();
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"weather_update","forecast":[{"name":"compact"}]}}}""");
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: events.Add)
            .RunAsync("D:/fam/Game", "hello", CancellationToken.None);

        var raw = Assert.Single(events, e => e.Kind == SessionEventKind.Raw);
        Assert.Equal("weather_update", raw.Title);
        Assert.Contains("compact", raw.Raw);
    }

    /// <summary>
    /// The session's own settings — the commands it offers, its mode, its config options (the model
    /// catalogue among them, which D24 keeps Daoris out of) — are known updates and not the conversation:
    /// shown on the console, kept out of the record. On the window they read as two "an update this
    /// version does not know" rows over a chat nobody had spoken in yet, under a false "working…" (CONV3b).
    /// </summary>
    [Fact]
    public async Task The_sessions_own_settings_reach_the_console_and_not_the_conversation()
    {
        var events = new List<SessionEvent>();
        var lines = new List<string>();
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new":
                    foreach (var kind in new[] { "available_commands_update", "current_mode_update", "config_option_update", "session_info_update" })
                    {
                        self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"KIND"}}}""".Replace("KIND", kind));
                    }

                    return Ok(frame, """{"sessionId":"s-1"}""");
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, """{"stopReason":"end_turn"}""") : null;
            }
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add, onEvent: events.Add)
            .RunAsync("D:/fam/Game", "hello", CancellationToken.None);

        Assert.DoesNotContain(events, e => e.Kind == SessionEventKind.Raw);
        Assert.Contains(lines, line => line.Contains("available_commands_update"));
    }

    /// <summary>
    /// 🔴 The first real Claude Code run over this door (ACP2, 2026-09-24) died at its first tool call,
    /// three sessions in a row, each "exited without touching its quest". A real `tool_call` carries
    /// `content` as a LIST, and the renderer read it as an object: the throw killed the reader, whose
    /// `finally` then said the agent's stream had ended — a false sentence over a lost exception. The
    /// stub had only ever sent the flat shape.
    /// </summary>
    [Fact]
    public async Task A_real_tool_call_whose_content_is_a_list_is_rendered_and_the_turn_goes_on()
    {
        var lines = new List<string>();
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call","toolCallId":"c1","title":"mcp__daoris-knowledge__quest_respond","kind":"other","status":"pending","rawInput":{"quest":"abc123","action":"take"},"content":[]}}}""");
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"tool_call_update","toolCallId":"c1","status":"completed","content":[{"type":"content","content":{"type":"text","text":"Taken."}}]}}}""");
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        var outcome = await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add)
            .RunAsync("D:/fam/Game", "take quest #abc123", CancellationToken.None);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Contains(lines, line => line.Contains("quest_respond"));
        Assert.Contains(lines, line => line.Contains("✓ mcp__daoris-knowledge__quest_respond"));
    }

    /// <summary>
    /// And whatever else a wire that is somebody else's may send: an update this client cannot read is
    /// shown as itself, and the turn goes on. One unreadable frame must never cost a session.
    /// </summary>
    [Fact]
    public async Task An_update_this_client_cannot_read_is_shown_and_the_turn_goes_on()
    {
        var lines = new List<string>();
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":7,"content":"not an object"}}}""");
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        var outcome = await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add)
            .RunAsync("D:/fam/Game", "do something", CancellationToken.None);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Contains(lines, line => line.Contains("not an object"));
    }

    /// <summary>
    /// An update shape this build has never seen is rendered as itself rather than dropped. The wire
    /// is somebody else's and it grows; a console that silently omitted the one new update type would
    /// be a transcript with a hole in it that nothing reports.
    /// </summary>
    [Fact]
    public async Task An_unknown_update_is_still_shown_rather_than_swallowed()
    {
        var lines = new List<string>();
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"quantum_entanglement_report","spooky":true}}}""");
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                // A real agent answers the requests it knows and ignores notifications.
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add)
            .RunAsync("D:/fam/Game", "do something new", CancellationToken.None);

        Assert.Contains(lines, line => line.Contains("quantum_entanglement_report"));
    }

    /// <summary>
    /// The stop shape (D49's two endings, on the wire): cancelling sends `session/cancel` for the
    /// session actually in flight. The person's interrupt is a verb, not a killed process.
    /// </summary>
    [Fact]
    public async Task Cancelling_sends_session_cancel_for_the_live_session()
    {
        using var cts = new CancellationTokenSource();
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-9"}""");
                case "session/prompt":
                    cts.Cancel();       // the person presses stop while the turn is in flight
                    return null;        // …and the agent never answers, as a cancelled turn would not
                // A real agent answers the requests it knows and ignores notifications.
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.RunAsync("D:/fam/Game", "a long task", cts.Token));

        Assert.Contains(agent.Sent, line => line.Contains("session/cancel") && line.Contains("s-9"));
    }

    /// <summary>
    /// A conversation (CONV3b, D76 §4): one handshake and one session, then a turn per prompt on it —
    /// not a session per message, which would forget everything said before.
    /// </summary>
    [Fact]
    public async Task A_conversation_is_one_session_with_a_turn_per_prompt()
    {
        var events = new List<SessionEvent>();
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-7"}""");
                case "session/prompt":
                    var said = frame.GetProperty("params").GetProperty("prompt")[0].GetProperty("text").GetString();
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-7","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"heard: SAID"}}}}""".Replace("SAID", said));
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: events.Add);
        await session.OpenAsync("D:/fam/Game", CancellationToken.None);
        Assert.Equal("end_turn", await session.PromptAsync("first", CancellationToken.None));
        Assert.Equal("end_turn", await session.PromptAsync("second", CancellationToken.None));
        await session.CloseAsync(CancellationToken.None);
        session.Release();

        Assert.Equal(
            ["initialize", "session/new", "session/prompt", "session/prompt", "session/close"],
            Enumerable.Range(0, agent.Sent.Count).Select(agent.Method));
        Assert.All([agent.Frame(2), agent.Frame(3)],
            prompt => Assert.Equal("s-7", prompt.GetProperty("params").GetProperty("sessionId").GetString()));
        Assert.Equal(["heard: first", "heard: second"],
            events.Where(e => e.Kind == SessionEventKind.Message).Select(e => e.Text));
        Assert.Equal(2, events.Count(e => e.Kind == SessionEventKind.Turn));
    }

    /// <summary>
    /// CONV5: a turn's tokens are the prompt response's <c>usage</c> — the turn's own totals, measured
    /// on <c>claude-code-acp</c> (the probe's second turn). The total, the cost and the by-model split
    /// stay on the wire; a response with no <c>usage</c> leaves the turn's tokens unknown, never zero.
    /// </summary>
    [Fact]
    public async Task A_turn_carries_the_tokens_its_prompt_response_reported()
    {
        var events = new List<SessionEvent>();
        var answers = new Queue<string>([
            """{"stopReason":"end_turn","usage":{"inputTokens":2,"outputTokens":3,"cachedReadTokens":35480,"cachedWriteTokens":39,"totalTokens":35524},"_meta":{"quota":{"model_usage":[{"model":"some-model"}]}}}""",
            """{"stopReason":"end_turn"}""",
        ]);
        var agent = new FakeAgent((frame, self) => frame.GetProperty("method").GetString() switch
        {
            "initialize" => Ok(frame, """{"protocolVersion":1}"""),
            "session/new" => Ok(frame, """{"sessionId":"s-8"}"""),
            "session/prompt" => Ok(frame, answers.Dequeue()),
            _ => frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null,
        });

        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: events.Add);
        await session.OpenAsync("D:/fam/Game", CancellationToken.None);
        await session.PromptAsync("one word", CancellationToken.None);
        await session.PromptAsync("another", CancellationToken.None);
        session.Release();

        var turns = events.Where(e => e.Kind == SessionEventKind.Turn).ToList();
        Assert.Equal(new TurnTokens(Input: 2, Output: 3, CacheRead: 35480, CacheWrite: 39), turns[0].Tokens);
        Assert.Null(turns[1].Tokens);
        Assert.All(events, e => Assert.DoesNotContain("some-model", e.Raw ?? ""));
    }

    /// <summary>
    /// Seen on the window (CONV5): <c>claude-code-acp</c> tallies a turn at its <c>result</c>, so a turn
    /// cancelled before one answers with every count zero — after reading 38,000 tokens of context. No
    /// turn that ran read nothing, so a report of nothing at all is no report, and the turn's tokens stay
    /// unknown rather than reading as a turn that cost nothing.
    /// </summary>
    [Fact]
    public void A_report_whose_every_count_is_zero_is_no_report()
    {
        using var zeros = JsonDocument.Parse("""{"inputTokens":0,"outputTokens":0,"cachedReadTokens":0,"cachedWriteTokens":0,"totalTokens":0}""");
        using var some = JsonDocument.Parse("""{"inputTokens":0,"outputTokens":5,"cachedReadTokens":0,"cachedWriteTokens":0}""");

        Assert.Null(TurnTokens.Read(zeros.RootElement, "inputTokens", "outputTokens", "cachedReadTokens", "cachedWriteTokens"));
        Assert.Equal(new TurnTokens(0, 5, 0, 0),
            TurnTokens.Read(some.RootElement, "inputTokens", "outputTokens", "cachedReadTokens", "cachedWriteTokens"));
    }

    /// <summary>
    /// Stopping a TURN is not ending the conversation: `session/cancel` for the turn in flight, its
    /// ending as the agent reports it, and the session still there for the next message.
    /// </summary>
    [Fact]
    public async Task A_cancelled_turn_ends_on_the_agents_word_and_the_conversation_goes_on()
    {
        string? held = null;
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-7"}""");
                case "session/prompt" when held is null:
                    held = frame.GetProperty("id").GetRawText();   // a long turn: answered only when cancelled
                    return null;
                case "session/prompt":
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                case "session/cancel":
                    return $$$"""{"jsonrpc":"2.0","id":{{{held}}},"result":{"stopReason":"cancelled"}}""";
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });
        await session.OpenAsync("D:/fam/Game", CancellationToken.None);
        var first = session.PromptAsync("a long task", CancellationToken.None);
        while (held is null) await Task.Delay(10);

        await session.CancelTurnAsync();

        Assert.Equal("cancelled", await first);
        Assert.Contains(agent.Sent, line => line.Contains("session/cancel") && line.Contains("s-7"));
        Assert.Equal("end_turn", await session.PromptAsync("and then this", CancellationToken.None));
        session.Release();
    }

    /// <summary>A prompt with no session to carry it is refused in a sentence, never sent with a null id.</summary>
    [Fact]
    public async Task Nothing_is_prompted_before_the_session_is_open()
    {
        var agent = new FakeAgent((_, _) => null);

        var error = await Assert.ThrowsAsync<DriverException>(
            () => new AcpSession(agent.Incoming, agent.Outgoing, _ => { }).PromptAsync("hello", CancellationToken.None));

        Assert.Contains("not open", error.Message);
        Assert.Empty(agent.Sent);
    }

    /// <summary>
    /// An agent that answers the prompt and then stops answering must not hang a run that is already
    /// over. The courtesy close is bounded — found the hard way by a test that never returned, which
    /// is the failure this pins so it cannot come back.
    /// </summary>
    [Fact]
    public async Task An_unanswered_close_does_not_hang_a_finished_run()
    {
        var agent = new FakeAgent((frame, self) =>
        {
            var method = frame.GetProperty("method").GetString();
            return method switch
            {
                "initialize" => Ok(frame, """{"protocolVersion":1}"""),
                "session/new" => Ok(frame, """{"sessionId":"s-1"}"""),
                "session/prompt" => Ok(frame, """{"stopReason":"end_turn"}"""),
                _ => null, // …and then it goes silent, close included
            };
        });

        var outcome = await new AcpSession(
                agent.Incoming, agent.Outgoing, _ => { }, closeTimeout: TimeSpan.FromMilliseconds(150))
            .RunAsync("D:/fam/Game", "a task", CancellationToken.None);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Contains(agent.Sent, line => line.Contains("session/close"));
    }

    /// <summary>
    /// An agent that dies mid-handshake fails with a sentence naming the protocol, not a null
    /// reference three frames later.
    /// </summary>
    [Fact]
    public async Task A_stream_that_ends_during_the_handshake_fails_with_a_readable_sentence()
    {
        var agent = new FakeAgent((_, self) => { self.Close(); return null; });

        var error = await Assert.ThrowsAsync<DriverException>(
            () => new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
                .RunAsync("D:/fam/Game", "hello", CancellationToken.None));

        Assert.Contains("ACP", error.Message);
    }

    /// <summary>
    /// 🔴 <b>The permission posture is a MODE on this wire, not a command-line flag</b> (ACP2, and
    /// the evaluation's §1a where Claude Code's own modes were observed on `session/new`:
    /// `default`, `acceptEdits`, `plan`, `auto`, `bypassPermissions`).
    /// </summary>
    /// <remarks>
    /// <b>D81</b> (a session does as much as the harness would on its own): Claude Code drives in <c>auto</c>, its own mode where the
    /// harness judges each action, rather than <c>acceptEdits</c> with every command refused. Still
    /// never <c>bypassPermissions</c>, which judges nothing, however available the wire makes it.
    /// </remarks>
    [Fact]
    public async Task The_permission_posture_is_set_as_a_mode_when_the_agent_offers_one()
    {
        var agent = PostureAgent("""
            {"id":"default","name":"Manual"},{"id":"acceptEdits","name":"Accept edits"},
            {"id":"auto","name":"Auto"},{"id":"bypassPermissions","name":"Bypass"}
            """);

        // 🔴 The posture comes from the ADAPTER, not from a constant in the session (ACP3) — taken
        // here from the real one, so this test fails if the Claude adapter ever stops naming it.
        var posture = AdapterSet.Built().Resolve("claude-code-acp").AcpPosture;

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, closeTimeout: null, posture)
            .RunAsync("D:/fam/Game", "do the thing", CancellationToken.None);

        Assert.Equal("auto", ModeSet(agent));
        // 🔴 Never the one that judges nothing, however available the wire makes it.
        Assert.DoesNotContain(agent.Sent, line => line.Contains("bypassPermissions"));
    }

    /// <summary>
    /// An adapter too old to offer <c>auto</c> is driven in the next posture it names — the one Daoris
    /// always used — never in a neighbouring mode the adapter did not list (D81, ACP3).
    /// </summary>
    [Fact]
    public async Task Without_auto_on_offer_the_next_named_posture_is_set_and_nothing_wider()
    {
        var agent = PostureAgent("""
            {"id":"default","name":"Manual"},{"id":"acceptEdits","name":"Accept edits"},
            {"id":"bypassPermissions","name":"Bypass"}
            """);

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, closeTimeout: null,
                AdapterSet.Built().Resolve("claude-code-acp").AcpPosture)
            .RunAsync("D:/fam/Game", "do the thing", CancellationToken.None);

        Assert.Equal("acceptEdits", ModeSet(agent));
        Assert.DoesNotContain(agent.Sent, line => line.Contains("bypassPermissions"));
    }

    private static FakeAgent PostureAgent(string modes) => new((frame, self) =>
    {
        _ = self;
        var method = frame.TryGetProperty("method", out var m) ? m.GetString() : null;
        return method switch
        {
            // The shape §1a observed: modes, with a current one, on the session.
            "session/new" => Ok(frame, $$$"""
                {"sessionId":"s-1","modes":{"currentModeId":"default","availableModes":[{{{modes}}}]}}
                """),
            "session/prompt" => Ok(frame, """{"stopReason":"end_turn"}"""),
            _ => frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null,
        };
    });

    private static string? ModeSet(FakeAgent agent) => agent.Sent
        .Select(line => JsonDocument.Parse(line).RootElement)
        .Where(f => f.TryGetProperty("method", out var m) && m.GetString() == "session/set_mode")
        .Select(f => f.GetProperty("params").GetProperty("modeId").GetString())
        .FirstOrDefault();

    /// <summary>
    /// 🔴 <b>An agent that offers the posture is still not set to it when the ADAPTER names none</b>
    /// (ACP3). This is dsh's shape — its wire carries no modes — and it is the case that would have
    /// been invisible: the old constant would have set `acceptEdits` on any agent that happened to
    /// offer one, which is a posture nobody chose arriving through a harness nobody asked.
    /// </summary>
    [Fact]
    public async Task An_adapter_that_names_no_posture_sets_none_even_when_the_agent_offers_one()
    {
        var agent = new FakeAgent((frame, self) =>
        {
            _ = self;
            var method = frame.TryGetProperty("method", out var m) ? m.GetString() : null;
            return method switch
            {
                "session/new" => Ok(frame, """
                    {"sessionId":"s-1","modes":{"currentModeId":"default","availableModes":[
                      {"id":"default","name":"Manual"},{"id":"acceptEdits","name":"Accept edits"}]}}
                    """),
                "session/prompt" => Ok(frame, """{"stopReason":"end_turn"}"""),
                _ => frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null,
            };
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, closeTimeout: null, posture: null)
            .RunAsync("D:/fam/Game", "do the thing", CancellationToken.None);

        Assert.DoesNotContain(agent.Sent, line => line.Contains("session/set_mode"));
    }

    /// <summary>
    /// The second harness's own id goes on the wire unchanged — `agent`, not a translation of it.
    /// Codex's vocabulary is Codex's.
    /// </summary>
    [Fact]
    public async Task A_second_harness_posture_is_sent_in_that_harness_own_vocabulary()
    {
        var agent = new FakeAgent((frame, self) =>
        {
            _ = self;
            var method = frame.TryGetProperty("method", out var m) ? m.GetString() : null;
            return method switch
            {
                // codex-acp@1.12.0's three modes, as its own bundle defines them.
                "session/new" => Ok(frame, """
                    {"sessionId":"s-1","modes":{"currentModeId":"read-only","availableModes":[
                      {"id":"read-only","name":"Ask for approval"},{"id":"agent","name":"Approve for me"},
                      {"id":"agent-full-access","name":"Full access"}]}}
                    """),
                "session/prompt" => Ok(frame, """{"stopReason":"end_turn"}"""),
                _ => frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null,
            };
        });

        var posture = AdapterSet.Built().Resolve("codex-acp").AcpPosture;

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, closeTimeout: null, posture)
            .RunAsync("D:/fam/Game", "do the thing", CancellationToken.None);

        var mode = agent.Sent
            .Select(line => JsonDocument.Parse(line).RootElement)
            .FirstOrDefault(f => f.TryGetProperty("method", out var m) && m.GetString() == "session/set_mode");

        Assert.Equal("agent", mode.GetProperty("params").GetProperty("modeId").GetString());
        // 🔴 Never the one that turns approvals off entirely, however available the wire makes it.
        Assert.DoesNotContain(agent.Sent, line => line.Contains("agent-full-access"));
    }

    /// <summary>
    /// An agent that offers no modes is not asked to set one — and the turn proceeds. The stub, and
    /// any agent whose permissions are its own business, are that shape.
    /// </summary>
    [Fact]
    public async Task An_agent_that_offers_no_modes_is_not_asked_to_change_one()
    {
        var agent = new FakeAgent((frame, self) =>
        {
            _ = self;
            var method = frame.TryGetProperty("method", out var m) ? m.GetString() : null;
            return method switch
            {
                "session/new" => Ok(frame, """{"sessionId":"s-1"}"""),
                "session/prompt" => Ok(frame, """{"stopReason":"end_turn"}"""),
                _ => frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null,
            };
        });

        var outcome = await new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/fam/Game", "do the thing", CancellationToken.None);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.DoesNotContain(agent.Sent, line => line.Contains("session/set_mode"));
    }

    /// <summary>
    /// An agent that offers modes but not the one asked for is left alone rather than set to
    /// something else. Guessing a neighbouring mode is how a permission posture silently widens.
    /// </summary>
    [Fact]
    public async Task A_mode_the_agent_does_not_offer_is_not_substituted()
    {
        var agent = new FakeAgent((frame, self) =>
        {
            _ = self;
            var method = frame.TryGetProperty("method", out var m) ? m.GetString() : null;
            return method switch
            {
                "session/new" => Ok(frame, """
                    {"sessionId":"s-1","modes":{"currentModeId":"default","availableModes":[
                      {"id":"default","name":"Manual"},{"id":"bypassPermissions","name":"Bypass"}]}}
                    """),
                "session/prompt" => Ok(frame, """{"stopReason":"end_turn"}"""),
                _ => frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null,
            };
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/fam/Game", "do the thing", CancellationToken.None);

        Assert.DoesNotContain(agent.Sent, line => line.Contains("session/set_mode"));
    }

    /// <summary>
    /// 🔴 <b>The measurement source</b> (TOOL3/D57 §4). ACP reports context pressure per turn, and
    /// until now it was rendered into a transcript line and structurally discarded. It is the ONLY
    /// structured usage any door delivers, which is why measurement is an argument for the protocol
    /// door rather than a reason to parse the pipe door's text.
    /// </summary>
    [Fact]
    public async Task Context_pressure_is_read_as_a_number_rather_than_rendered_and_lost()
    {
        var agent = new FakeAgent((frame, self) =>
        {
            var method = frame.TryGetProperty("method", out var m) ? m.GetString() : null;
            if (method != "session/prompt")
            {
                return frame.TryGetProperty("id", out _)
                    ? Ok(frame, method == "session/new" ? """{"sessionId":"s-1"}""" : "{}")
                    : null;
            }

            self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"usage_update","used":1200,"size":200000}}}""");
            // The high-water mark is what a person wants, so a later, larger reading wins…
            self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"usage_update","used":48000,"size":200000}}}""");
            // …and a later, SMALLER one does not: a compaction drops the number, and reporting the
            // last reading would say a session that nearly filled its window used very little.
            self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"usage_update","used":900,"size":200000}}}""");
            return Ok(frame, """{"stopReason":"end_turn"}""");
        });

        var outcome = await new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/fam/Game", "measure me", CancellationToken.None);

        Assert.Equal(48000, outcome.Usage?.Used);
        Assert.Equal(200000, outcome.Usage?.Size);
    }

    /// <summary>
    /// An agent that reports nothing leaves usage <b>absent</b>, never zero. "Nothing was measured"
    /// and "it used nothing" are different claims, and only one of them is true here (SES1's rule
    /// about a bound, applied to a different number).
    /// </summary>
    [Fact]
    public async Task An_agent_that_reports_no_usage_leaves_it_absent_rather_than_zero()
    {
        // Named rather than `_`: a lambda parameter called `_` is in scope, so `out _` binds to IT
        // rather than to a discard — which reads as a type error twenty lines away.
        var agent = new FakeAgent((frame, self) =>
        {
            _ = self;
            var method = frame.TryGetProperty("method", out var m) ? m.GetString() : null;
            return frame.TryGetProperty("id", out _)
                ? Ok(frame, method switch
                {
                    "session/new" => """{"sessionId":"s-1"}""",
                    "session/prompt" => """{"stopReason":"end_turn"}""",
                    _ => "{}",
                })
                : null;
        });

        var outcome = await new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/fam/Game", "say nothing about usage", CancellationToken.None);

        Assert.Null(outcome.Usage);
    }

    /// <summary>
    /// A usage update whose numbers are missing or the wrong shape is not a measurement. Somebody
    /// else's protocol version is free to change, and a malformed frame must not become a zero on a
    /// person's screen — nor take the turn down.
    /// </summary>
    [Fact]
    public async Task A_usage_update_that_is_not_a_measurement_is_ignored_rather_than_believed()
    {
        var agent = new FakeAgent((frame, self) =>
        {
            var method = frame.TryGetProperty("method", out var m) ? m.GetString() : null;
            if (method != "session/prompt")
            {
                return frame.TryGetProperty("id", out _)
                    ? Ok(frame, method == "session/new" ? """{"sessionId":"s-1"}""" : "{}")
                    : null;
            }

            self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"usage_update"}}}""");
            self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"usage_update","used":"lots","size":null}}}""");
            return Ok(frame, """{"stopReason":"end_turn"}""");
        });

        var outcome = await new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/fam/Game", "report nonsense", CancellationToken.None);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Null(outcome.Usage);
    }

    // ── the servers the door hands over (ACP4) ────────────────────────────────────────────────────
    //
    // 🔴 Measured in ACP2's first real driven run: the session came up, streamed "I'll start by
    // taking the quest", called `take`, had no such tool, and ended its turn having touched nothing.
    // The composed target instructs every session to claim its quest over its own connector, and the
    // PIPE door only works because the repository's own `.mcp.json` wires that — which an adopted
    // repository may not have, and which the driver may never reach in and write.
    //
    // The protocol carries the wiring instead, so this hands the session its voice with nothing
    // written anywhere. The env shape is an ARRAY of {name,value} pairs — read from the adapter's own
    // source, where it does `Object.fromEntries(server.env.map(e => [e.name, e.value]))`.

    private static readonly AcpMcpServer Knowledge = new(
        "daoris-knowledge",
        "daoris-knowledge",
        ["--stdio"],
        new Dictionary<string, string> { ["DAORIS_KNOWLEDGE_DB"] = "D:/scratch/knowledge.db" });

    [Fact]
    public async Task The_session_is_offered_the_knowledge_server_it_is_told_to_use()
    {
        var agent = Simple();

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/fam/Game", "take quest #abc123", CancellationToken.None, [Knowledge]);

        var servers = agent.Frame(1).GetProperty("params").GetProperty("mcpServers");
        var server = Assert.Single(servers.EnumerateArray());
        Assert.Equal("daoris-knowledge", server.GetProperty("name").GetString());
        Assert.Equal("daoris-knowledge", server.GetProperty("command").GetString());
        Assert.Equal("--stdio", server.GetProperty("args")[0].GetString());

        var env = Assert.Single(server.GetProperty("env").EnumerateArray());
        Assert.Equal("DAORIS_KNOWLEDGE_DB", env.GetProperty("name").GetString());
        Assert.Equal("D:/scratch/knowledge.db", env.GetProperty("value").GetString());
    }

    /// <summary>
    /// 🔴 Offering NOTHING stays an empty array, never an absent field. A machine with no host found
    /// still drives — the session simply has no connector, exactly as it did before ACP4 — and an
    /// agent that reads `params.mcpServers.length` must not meet `undefined`.
    /// </summary>
    [Fact]
    public async Task A_session_with_no_servers_still_sends_an_empty_list()
    {
        var agent = Simple();

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/fam/Game", "do the work", CancellationToken.None);

        var servers = agent.Frame(1).GetProperty("params").GetProperty("mcpServers");
        Assert.Equal(JsonValueKind.Array, servers.ValueKind);
        Assert.Empty(servers.EnumerateArray());
    }

    // ── CONSOLE2: what a session runs beside itself (docs/2026-09-28-console2-streams-evidence.md) ──

    /// <summary>An agent that answers the calls, and says <paramref name="during"/> inside its one turn.</summary>
    private static FakeAgent Turn(Action<FakeAgent> during) => new((frame, self) =>
    {
        switch (frame.GetProperty("method").GetString())
        {
            case "initialize":
                return Ok(frame, """{"protocolVersion":1,"agentCapabilities":{}}""");
            case "session/new":
                return Ok(frame, """{"sessionId":"s-1"}""");
            case "session/prompt":
                during(self);
                return Ok(frame, """{"stopReason":"end_turn"}""");
            default:
                return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
        }
    });

    private static string Update(string session, string update) =>
        """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":""" + JsonSerializer.Serialize(session)
        + ""","update":""" + update + "}}";

    /// <summary>
    /// A session whose streams are kept asks for them, by the spelling that arrives: AIR's two
    /// capabilities, and the protocol's own <c>subagents</c> for when the SDK's schema carries it. It
    /// never asks for <c>terminal_output</c>, which streams nothing and moves a command's output out of
    /// the field the record reads. A session given nowhere to keep them asks for nothing.
    /// </summary>
    [Fact]
    public async Task A_session_given_streams_asks_for_subagents_and_tasks_and_one_given_none_asks_for_nothing()
    {
        var kept = Simple();
        await new AcpSession(kept.Incoming, kept.Outgoing, _ => { }, streams: new SessionStreams(new SessionOutput(), "d-1"))
            .RunAsync("D:/fam/Game", "do the work", CancellationToken.None);

        var asked = kept.Frame(0).GetProperty("params").GetProperty("clientCapabilities");
        Assert.Equal(JsonValueKind.Object, asked.GetProperty("subagents").ValueKind);
        var air = asked.GetProperty("_meta").GetProperty("jetbrains").GetProperty("air");
        Assert.Equal(1, air.GetProperty("version").GetInt32());
        Assert.Equal(["asyncTasks", "nativeSubagentSessions"],
            air.GetProperty("capabilities").EnumerateArray().Select(c => c.GetString()).Order());
        Assert.False(asked.GetProperty("_meta").TryGetProperty("terminal_output", out _));

        var plain = Simple();
        await new AcpSession(plain.Incoming, plain.Outgoing, _ => { })
            .RunAsync("D:/fam/Game", "do the work", CancellationToken.None);

        var none = plain.Frame(0).GetProperty("params").GetProperty("clientCapabilities");
        Assert.False(none.TryGetProperty("subagents", out _));
        Assert.False(none.TryGetProperty("_meta", out _));
    }

    /// <summary>
    /// 🔴 A subagent's updates come under its own <c>params.sessionId</c>, and a client that ignores it
    /// merges the child into the parent. Here the child's words and tools go to its own stream. The
    /// parent's console says it started and how it ended, and the parent's record holds it as one card,
    /// because declaring the capability took the <c>Agent</c> call off the parent's wire.
    /// </summary>
    [Fact]
    public async Task A_subagent_streams_as_its_own_and_is_one_card_in_its_parents_record()
    {
        var output = new SessionOutput();
        var lines = new List<string>();
        var events = new List<SessionEvent>();
        var agent = Turn(self =>
        {
            self.Push(Update("s-1", """{"sessionUpdate":"subagent_spawned","subagentSessionId":"a2fe","name":"Read README first line","task":"Read README.md and reply with its first line.","capabilities":{}}"""));
            self.Push(Update("a2fe", """{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"I'll read it.\n"}}"""));
            self.Push(Update("a2fe", """{"sessionUpdate":"tool_call","toolCallId":"toolu_r","title":"Read README.md","kind":"read","status":"pending"}"""));
            self.Push(Update("a2fe", """{"sessionUpdate":"tool_call_update","toolCallId":"toolu_r","status":"completed"}"""));
            self.Push(Update("s-1", """{"sessionUpdate":"subagent_state_update","subagentSessionId":"a2fe","state":"completed"}"""));
            self.Push(Update("s-1", """{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"It says hello.\n"}}"""));
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add, onEvent: events.Add, streams: new SessionStreams(output, "d-1"))
            .RunAsync("D:/fam/Game", "do the work", CancellationToken.None);

        Assert.Contains("→ subagent: Read README first line", lines);
        Assert.Contains("  ✓ subagent: Read README first line", lines);
        Assert.Contains("It says hello.", lines);
        Assert.DoesNotContain(lines, line => line.Contains("I'll read it.") || line.Contains("README.md"));

        var child = output.Tail(SessionOutput.Key("d-1", "subagent/a2fe")).Lines.Select(l => l.Text).ToList();
        Assert.Equal(["I'll read it.", "→ Read README.md", "  ✓ Read README.md"], child);
        var listed = Assert.Single(output.Streams("d-1"));
        Assert.Equal((SessionStreamKind.Subagent, "Read README first line", false, "completed"),
            (listed.Kind, listed.Name, listed.Live, listed.State));

        var cards = events.Where(e => e.Kind == SessionEventKind.Tool).ToList();
        Assert.Equal(2, cards.Count);
        Assert.All(cards, card => Assert.Equal(("a2fe", "subagent/a2fe"), (card.Id, card.Stream)));
        Assert.Equal(("Read README first line", "think", "in_progress"), (cards[0].Title, cards[0].ToolKind, cards[0].Status));
        Assert.Equal("Read README.md and reply with its first line.", Assert.Single(cards[0].Content!).Text);
        Assert.Equal("completed", cards[1].Status);
        Assert.DoesNotContain(events, e => e.Text is { } text && text.Contains("I'll read it."));
    }

    /// <summary>
    /// A background task's output is a file its harness writes, and the task names it (CONSOLE2a): the
    /// stream is that file, read as it grows. Its ending is the LAST state the wire gave, since a task
    /// that finished on its own is said <c>stopped</c> and then <c>completed</c> in one breath.
    /// </summary>
    [Fact]
    public async Task A_background_task_streams_its_output_file_and_ends_with_its_last_state()
    {
        var file = Path.Combine(Path.GetTempPath(), $"daoris-task-{Guid.NewGuid():N}.output");
        File.WriteAllText(file, "bg tick 1\nbg tick 2\n\n[exited with code 0]\n");
        try
        {
            var output = new SessionOutput();
            var lines = new List<string>();
            var events = new List<SessionEvent>();
            var path = JsonSerializer.Serialize(file);
            var agent = Turn(self =>
            {
                self.Push(Update("s-1", """{"sessionUpdate":"async_task_spawned","asyncTaskId":"bs00","name":"Start a ticker","taskType":"shell","showInTranscript":false,"canStop":true}"""));
                self.Push(Update("s-1", """{"sessionUpdate":"async_task_progress","asyncTaskId":"bs00","toolCallId":"toolu_b"}"""));
                self.Push(Update("s-1", $$"""{"sessionUpdate":"async_task_progress","asyncTaskId":"bs00","outputFilePath":{{path}},"toolCallId":"toolu_b"}"""));
                self.Push(Update("s-1", $$"""{"sessionUpdate":"async_task_state_update","asyncTaskId":"bs00","state":"stopped","outputFilePath":{{path}}}"""));
                self.Push(Update("s-1", $$"""{"sessionUpdate":"async_task_state_update","asyncTaskId":"bs00","state":"completed","outputFilePath":{{path}}}"""));
            });

            await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add, onEvent: events.Add, streams: new SessionStreams(output, "d-1"))
                .RunAsync("D:/fam/Game", "do the work", CancellationToken.None);

            Assert.Equal(["bg tick 1", "bg tick 2", "", "[exited with code 0]"],
                output.Tail(SessionOutput.Key("d-1", "task/bs00")).Lines.Select(l => l.Text));
            var listed = Assert.Single(output.Streams("d-1"));
            Assert.Equal((SessionStreamKind.Task, "Start a ticker", false, "completed"),
                (listed.Kind, listed.Name, listed.Live, listed.State));

            Assert.Contains("→ background: Start a ticker", lines);
            Assert.Single(lines, line => line.Contains("background: Start a ticker") && !line.StartsWith('→'));
            Assert.Contains("  ✓ background: Start a ticker", lines);
            // Its tool call is its card (`showInTranscript: false`): the task adds none, and no raw row.
            Assert.DoesNotContain(events, e => e.Kind is SessionEventKind.Tool or SessionEventKind.Raw);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// A session's end is its streams' end (ORPHAN1's twin, said on the console): a subagent or a task
    /// still open when the session closes is ended with it, says so, and its card says so too.
    /// </summary>
    [Fact]
    public async Task A_stream_still_open_when_the_session_ends_ends_with_it_and_says_so()
    {
        var output = new SessionOutput();
        var events = new List<SessionEvent>();
        var agent = Turn(self =>
        {
            self.Push(Update("s-1", """{"sessionUpdate":"subagent_spawned","subagentSessionId":"a9","name":"Long reader","task":"read everything","capabilities":{}}"""));
            self.Push(Update("s-1", """{"sessionUpdate":"async_task_spawned","asyncTaskId":"t9","name":"dev server","taskType":"shell","canStop":true}"""));
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: events.Add, streams: new SessionStreams(output, "d-1"))
            .RunAsync("D:/fam/Game", "do the work", CancellationToken.None);

        var streams = output.Streams("d-1");
        Assert.Equal(2, streams.Count);
        Assert.All(streams, stream => Assert.Equal((false, SessionStream.SessionEnded), (stream.Live, stream.State)));
        Assert.All(streams, stream => Assert.Equal("— ended with its session", output.Tail(stream.Key).Lines.Last().Text));
        Assert.Equal(SessionStream.SessionEnded, events.Last(e => e.Kind == SessionEventKind.Tool).Status);
    }

    /// <summary>
    /// CONSOLE3a: a task its harness says can be stopped (<c>canStop</c>) is listed as one, and a stop
    /// is the adapter's own request, <c>_session/async_task/stop</c> with the session and the task. The
    /// stream ends on the wire's word after it, like any other ending, never on the request's answer.
    /// </summary>
    [Fact]
    public async Task A_stoppable_task_is_stopped_over_the_wire_and_ends_on_the_wires_word()
    {
        var output = new SessionOutput();
        var agent = new FakeAgent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize": return Ok(frame, """{"protocolVersion":1,"agentCapabilities":{}}""");
                case "session/new": return Ok(frame, """{"sessionId":"s-1"}""");
                case "_session/async_task/stop":
                    self.Push(Update("s-1", """{"sessionUpdate":"async_task_state_update","asyncTaskId":"t9","state":"stopped"}"""));
                    return Ok(frame, """{"stopped":true}""");
                default: return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });

        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, streams: new SessionStreams(output, "c-1"));
        await session.OpenAsync("D:/fam/Game", CancellationToken.None);
        agent.Push(Update("s-1", """{"sessionUpdate":"async_task_spawned","asyncTaskId":"t9","name":"dev server","taskType":"shell","canStop":true}"""));
        agent.Push(Update("s-1", """{"sessionUpdate":"async_task_spawned","asyncTaskId":"t8","name":"a probe","taskType":"shell","canStop":false}"""));
        await Until(() => output.Streams("c-1").Count == 2);

        Assert.Equal([true, false], output.Streams("c-1").Select(stream => stream.CanStop));
        Assert.True(await session.StopTaskAsync("t9", CancellationToken.None));

        var stop = JsonDocument.Parse(agent.Sent.Single(line => line.Contains("_session/async_task/stop"))).RootElement.GetProperty("params");
        Assert.Equal(("s-1", "t9"), (stop.GetProperty("sessionId").GetString(), stop.GetProperty("asyncTaskId").GetString()));
        await Until(() => !output.Streams("c-1")[0].Live);
        Assert.Equal("stopped", output.Streams("c-1")[0].State);
        // Ended, a stream is no longer one to stop.
        Assert.False(output.Streams("c-1")[0].CanStop);

        session.Release();
    }

    /// <summary>A stop before the session is open has nothing to send, and says so as false.</summary>
    [Fact]
    public async Task A_stop_before_the_session_is_open_sends_nothing()
    {
        var agent = Simple();
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, streams: new SessionStreams(new SessionOutput(), "c-1"));

        Assert.False(await session.StopTaskAsync("t9", CancellationToken.None));
        Assert.Empty(agent.Sent);
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++) await Task.Delay(10);
        Assert.True(condition(), "the condition did not hold within two seconds");
    }

    /// <summary>
    /// Two messages the agent sent one after another are two, by the id the wire gives each message
    /// (found looking at CONSOLE2): joined, the window read <c>DONESubagent finished</c>. Chunks of one
    /// message still join, and the id rides the record so the page can tell them apart too.
    /// </summary>
    [Fact]
    public async Task Two_messages_in_a_row_are_two_by_the_ids_the_wire_gives_them()
    {
        var lines = new List<string>();
        var events = new List<SessionEvent>();
        var agent = Turn(self =>
        {
            self.Push(Update("s-1", """{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"DO"},"messageId":"msg_1"}"""));
            self.Push(Update("s-1", """{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"NE"},"messageId":"msg_1"}"""));
            self.Push(Update("s-1", """{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"Subagent finished."},"messageId":"msg_2"}"""));
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add, onEvent: events.Add)
            .RunAsync("D:/fam/Game", "do the work", CancellationToken.None);

        Assert.Contains("DONE", lines);
        Assert.Contains("Subagent finished.", lines);
        Assert.Equal(["msg_1", "msg_1", "msg_2"],
            events.Where(e => e.Kind == SessionEventKind.Message).Select(e => e.Id));
    }

    /// <summary>With nowhere to keep them, a child's updates are the session's, exactly as before CONSOLE2.</summary>
    [Fact]
    public async Task Without_streams_a_childs_update_is_the_sessions_as_before()
    {
        var lines = new List<string>();
        var agent = Turn(self =>
            self.Push(Update("a2fe", """{"sessionUpdate":"tool_call","toolCallId":"toolu_r","title":"Read README.md","kind":"read","status":"pending"}""")));

        await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add)
            .RunAsync("D:/fam/Game", "do the work", CancellationToken.None);

        Assert.Contains("→ Read README.md", lines);
    }

    // ——— One conversation's model and effort (AGT6b, D98): the options the agent offers, kept and changed.

    /// <summary>
    /// What <c>claude-agent-acp</c> 0.84.0 answers <c>session/new</c> with (<c>acp-agent.js</c>,
    /// <c>buildConfigOptions</c>): the mode, the model, the effort as a thought level — here in the
    /// protocol's grouped form, which the schema allows — and fast mode as a boolean.
    /// </summary>
    private const string Offered = """
        [{"id":"mode","name":"Mode","category":"mode","type":"select","currentValue":"acceptEdits","options":[{"value":"default","name":"Default"},{"value":"acceptEdits","name":"Accept Edits"}]},
         {"id":"model","name":"Model","description":"AI model to use","category":"model","type":"select","currentValue":"default","options":[{"value":"default","name":"Default (recommended)","description":"the tool's default"},{"value":"sonnet","name":"Sonnet"}]},
         {"id":"effort","name":"Effort","category":"thought_level","type":"select","currentValue":"high","options":[{"group":"levels","name":"Levels","options":[{"value":"low","name":"Low"},{"value":"high","name":"High"},{"value":"max","name":"Max"}]}]},
         {"id":"fast","name":"Fast mode","category":"model_config","type":"boolean","currentValue":false}]
        """;

    /// <summary>An agent that offers <see cref="Offered"/>, and answers everything else with <paramref name="more"/>.</summary>
    private static FakeAgent Offering(Func<JsonElement, FakeAgent, string?>? more = null) => new((frame, self) =>
        frame.GetProperty("method").GetString() switch
        {
            "initialize" => Ok(frame, """{"protocolVersion":1,"agentCapabilities":{}}"""),
            "session/new" => Ok(frame, $$"""{"sessionId":"s-1","configOptions":{{Offered}}}"""),
            _ => more?.Invoke(frame, self) ?? (frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null),
        });

    /// <summary>
    /// The options are kept as the agent offered them: every select, its current value, and its choices —
    /// a grouped list flattened, since a group is a heading and not a value. A boolean is not a choice of
    /// values, and is left out.
    /// </summary>
    [Fact]
    public async Task The_options_an_agent_offers_on_session_new_are_kept_as_it_offered_them()
    {
        var agent = Offering();
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });
        await session.OpenAsync("D:/fam/Game", CancellationToken.None);

        Assert.Equal(["mode", "model", "effort"], session.ConfigOptions.Select(option => option.Id));
        var model = session.ConfigOptions.Single(option => option.Id == "model");
        Assert.Equal(("Model", "model", "default"), (model.Name, model.Category, model.Current));
        Assert.Equal(["default", "sonnet"], model.Choices.Select(choice => choice.Value));
        Assert.Equal("the tool's default", model.Choices[0].Description);
        var effort = session.ConfigOptions.Single(option => option.Id == "effort");
        Assert.Equal("thought_level", effort.Category);
        Assert.Equal(["low", "high", "max"], effort.Choices.Select(choice => choice.Value));
        session.Release();
    }

    [Fact]
    public async Task An_agent_that_offers_no_options_has_none()
    {
        var agent = Simple();
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });
        await session.OpenAsync("D:/fam/Game", CancellationToken.None);

        Assert.Empty(session.ConfigOptions);
        session.Release();
    }

    /// <summary>
    /// A change is <c>session/set_config_option</c> with the session, the option and the value, and the
    /// agent's answer is the whole list after it — the effort's levels follow the model — which replaces
    /// what was kept, and is told.
    /// </summary>
    [Fact]
    public async Task Setting_an_option_sends_set_config_option_and_keeps_the_agents_answer()
    {
        var told = new List<IReadOnlyList<AcpConfigOption>>();
        var agent = Offering((frame, self) => frame.GetProperty("method").GetString() == "session/set_config_option"
            ? Ok(frame, """
                {"configOptions":[{"id":"model","name":"Model","category":"model","type":"select","currentValue":"sonnet","options":[{"value":"default","name":"Default"},{"value":"sonnet","name":"Sonnet"}]}]}
                """)
            : null);
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onOptions: told.Add);
        await session.OpenAsync("D:/fam/Game", CancellationToken.None);

        var after = await session.SetConfigOptionAsync("model", "sonnet", CancellationToken.None);

        var sent = agent.Frame(agent.Sent.Count - 1);
        Assert.Equal("session/set_config_option", sent.GetProperty("method").GetString());
        Assert.Equal("""{"sessionId":"s-1","configId":"model","value":"sonnet"}""", sent.GetProperty("params").GetRawText());
        Assert.Equal("sonnet", Assert.Single(after).Current);
        Assert.Equal(after, session.ConfigOptions);
        // Told once when the session opened, and once for the change.
        Assert.Equal(2, told.Count);
        Assert.Equal(after, told[^1]);
        session.Release();
    }

    /// <summary>
    /// The agent may change the options itself — a model it fell back to, a switch it made — and says so in
    /// a <c>config_option_update</c>, which replaces what was kept exactly as an answer does.
    /// </summary>
    [Fact]
    public async Task An_update_the_agent_sends_replaces_the_options()
    {
        var told = new ConcurrentQueue<IReadOnlyList<AcpConfigOption>>();
        var agent = Offering((frame, self) =>
        {
            if (frame.GetProperty("method").GetString() != "session/prompt") return null;
            self.Push(Update("s-1", """
                {"sessionUpdate":"config_option_update","configOptions":[{"id":"model","name":"Model","category":"model","type":"select","currentValue":"haiku","options":[{"value":"haiku","name":"Haiku"}]}]}
                """));
            return Ok(frame, """{"stopReason":"end_turn"}""");
        });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onOptions: told.Enqueue);
        await session.OpenAsync("D:/fam/Game", CancellationToken.None);

        await session.PromptAsync("go on", CancellationToken.None);
        await Poll.Until(() => session.ConfigOptions.Count == 1);

        Assert.Equal("haiku", session.ConfigOptions[0].Current);
        Assert.Equal("haiku", told.Last()[0].Current);
        session.Release();
    }

    /// <summary>A change the agent refuses is its refusal, in its words, and what was kept stays as it was.</summary>
    [Fact]
    public async Task A_setting_the_agent_refuses_is_its_words_and_changes_nothing()
    {
        var agent = Offering((frame, self) => frame.GetProperty("method").GetString() == "session/set_config_option"
            ? $$$"""{"jsonrpc":"2.0","id":{{{frame.GetProperty("id").GetRawText()}}},"error":{"code":-32603,"message":"Invalid value for config option model: nonsense"}}"""
            : null);
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });
        await session.OpenAsync("D:/fam/Game", CancellationToken.None);

        var refused = await Assert.ThrowsAsync<DriverException>(() => session.SetConfigOptionAsync("model", "nonsense", CancellationToken.None));

        Assert.Contains("Invalid value for config option model: nonsense", refused.Message);
        Assert.Equal("default", session.ConfigOptions.Single(option => option.Id == "model").Current);
        session.Release();
    }

    /// <summary>Nothing can be set on a session that is not open: there is no session id to name.</summary>
    [Fact]
    public async Task An_option_cannot_be_set_before_the_session_opens()
    {
        var agent = Offering();
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });

        await Assert.ThrowsAsync<DriverException>(() => session.SetConfigOptionAsync("model", "sonnet", CancellationToken.None));
        Assert.Empty(agent.Sent);
    }

    /// <summary>An agent that answers the three calls and nothing else — the shape most tests need.</summary>
    // 🔴 The parameter is `self`, not `_`. A lambda parameter named `_` SHADOWS the discard in
    // `TryGetProperty("id", out _)` below, and the error it produces names neither.
    private static FakeAgent Simple() => new((frame, self) =>
        frame.GetProperty("method").GetString() switch
        {
            "initialize" => Ok(frame, """{"protocolVersion":1,"agentCapabilities":{}}"""),
            "session/new" => Ok(frame, """{"sessionId":"s-1"}"""),
            "session/prompt" => Ok(frame, """{"stopReason":"end_turn"}"""),
            _ => frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null,
        });
}
