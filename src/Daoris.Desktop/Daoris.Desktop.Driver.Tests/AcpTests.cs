using System.Text;
using System.Text.Json;
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

        Assert.Contains(lines, line => line.Contains("read"));
        Assert.Contains(lines, line => line.Contains("completed"));
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
        Assert.Contains("git push", note.Text);
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
        Assert.Contains(lines, line => line.Contains("completed"));
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
    /// The posture itself is unchanged and is D37's: edits auto-accept because they are reversible
    /// and in-repository, and <b>nothing at the outward boundary is ever auto-approved</b> — which
    /// is why <c>bypassPermissions</c> is not what is asked for even though the wire offers it.
    /// </remarks>
    [Fact]
    public async Task The_permission_posture_is_set_as_a_mode_when_the_agent_offers_one()
    {
        var agent = new FakeAgent((frame, self) =>
        {
            _ = self;
            var method = frame.TryGetProperty("method", out var m) ? m.GetString() : null;
            return method switch
            {
                // The shape §1a observed: modes, with a current one, on the session.
                "session/new" => Ok(frame, """
                    {"sessionId":"s-1","modes":{"currentModeId":"default","availableModes":[
                      {"id":"default","name":"Manual"},{"id":"acceptEdits","name":"Accept edits"},
                      {"id":"bypassPermissions","name":"Bypass"}]}}
                    """),
                "session/prompt" => Ok(frame, """{"stopReason":"end_turn"}"""),
                _ => frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null,
            };
        });

        // 🔴 The posture comes from the ADAPTER, not from a constant in the session (ACP3) — taken
        // here from the real one, so this test fails if the Claude adapter ever stops naming it.
        var posture = AdapterSet.Built().Resolve("claude-code-acp").AcpPosture;

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, closeTimeout: null, posture)
            .RunAsync("D:/fam/Game", "do the thing", CancellationToken.None);

        var mode = agent.Sent
            .Select(line => JsonDocument.Parse(line).RootElement)
            .FirstOrDefault(f => f.TryGetProperty("method", out var m) && m.GetString() == "session/set_mode");

        Assert.Equal("acceptEdits", mode.GetProperty("params").GetProperty("modeId").GetString());
        // 🔴 Never the one that would widen the boundary, however available the wire makes it.
        Assert.DoesNotContain(agent.Sent, line => line.Contains("bypassPermissions"));
    }

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
