using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STEER1 (D136): what the person tells a driven session reaches it at its next step where the agent says it takes a
/// prompt during a turn, and at the turn's end where it does not — and the record says when the session took the words,
/// in the order the wire said it. Against a fake agent over in-memory streams, shaped on what
/// <c>claude-agent-acp</c> 0.84.0 did in the one measured session (docs/2026-10-03-steer-evidence.md §6): the earlier
/// prompt is answered <c>end_turn</c> the moment the words are taken, and the later one at the real end. No process and no
/// model, so the fast half.
/// </summary>
public sealed class AcpSteerTests
{
    /// <summary>A scripted agent: each frame the client sends is answered by the handler, which may push frames first.</summary>
    private sealed class Agent(Func<JsonElement, Agent, string?> handle)
    {
        private readonly Channel<string> _toClient = Channel.CreateUnbounded<string>();

        public List<string> Sent { get; } = [];

        public TextReader Incoming => new Reader(_toClient);

        public TextWriter Outgoing => new Writer(line =>
        {
            lock (Sent) Sent.Add(line);
            var reply = handle(JsonDocument.Parse(line).RootElement, this);
            if (reply is not null) Push(reply);
        });

        public void Push(string json) => _toClient.Writer.TryWrite(json);

        public void Close() => _toClient.Writer.TryComplete();

        public IReadOnlyList<string> Methods
        {
            get
            {
                lock (Sent)
                {
                    return [.. Sent.Select(line => JsonDocument.Parse(line).RootElement)
                        .Select(frame => frame.TryGetProperty("method", out var m) ? m.GetString()! : "(response)")];
                }
            }
        }

        public IReadOnlyList<string> Prompts
        {
            get
            {
                lock (Sent)
                {
                    return [.. Sent.Select(line => JsonDocument.Parse(line).RootElement)
                        .Where(frame => frame.TryGetProperty("method", out var m) && m.GetString() == "session/prompt")
                        .Select(frame => frame.GetProperty("params").GetProperty("prompt")[0].GetProperty("text").GetString()!)];
                }
            }
        }

        private sealed class Reader(Channel<string> channel) : TextReader
        {
            public override Task<string?> ReadLineAsync() => ReadLineAsync(CancellationToken.None).AsTask();

            public override async ValueTask<string?> ReadLineAsync(CancellationToken ct) =>
                await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false) && channel.Reader.TryRead(out var line) ? line : null;
        }

        private sealed class Writer(Action<string> onLine) : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;

            public override Task WriteLineAsync(string? value)
            {
                if (value is not null) onLine(value);
                return Task.CompletedTask;
            }
        }
    }

    private static string Ok(JsonElement request, string result) =>
        $$"""{"jsonrpc":"2.0","id":{{request.GetProperty("id").GetRawText()}},"result":{{result}}}""";

    private static string Answer(int id, string stopReason) =>
        $$$"""{"jsonrpc":"2.0","id":{{{id}}},"result":{"stopReason":"{{{stopReason}}}"}}""";

    private static string Said(string text) =>
        """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"""
        + JsonSerializer.Serialize(text) + "}}}}";

    /// <summary>What <c>claude-agent-acp</c> 0.84.0 answers at <c>initialize</c> about prompts during a turn (evidence §1).</summary>
    private const string Queues = """{"protocolVersion":1,"agentCapabilities":{"_meta":{"claudeCode":{"promptQueueing":true}}}}""";

    private const string Silent = """{"protocolVersion":1,"agentCapabilities":{}}""";

    /// <summary>
    /// What <c>claude-agent-acp</c> 0.84.0 answers at <c>initialize</c> where it also offers the steering extension (evidence §2,
    /// <c>acp-agent.js:1291-1307</c>): the top-level <c>_meta.steering.supported</c>.
    /// </summary>
    private const string Steers =
        """{"protocolVersion":1,"_meta":{"steering":{"supported":true}},"agentCapabilities":{"_meta":{"claudeCode":{"promptQueueing":true}}}}""";

    /// <summary>A tool call announced or updated, as the adapter streams it.</summary>
    private static string Call(string kind, string id, string? status) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method = "session/update",
            @params = new { sessionId = "s-1", update = new { sessionUpdate = kind, toolCallId = id, title = "Read", kind = "read", status } },
        });

    /// <summary>The run's record and what it was told, in one order: the events, each word said and each word taken.</summary>
    private sealed class Record
    {
        private readonly List<string> _lines = [];

        public IReadOnlyList<string> Lines
        {
            get { lock (_lines) return [.. _lines]; }
        }

        private readonly List<SessionEvent> _notes = [];

        /// <summary>Every note event, whole: its parts are what CONVNOTE2d1 holds.</summary>
        public IReadOnlyList<SessionEvent> Notes
        {
            get { lock (_lines) return [.. _notes]; }
        }

        public void Event(SessionEvent e)
        {
            if (e.Kind == SessionEventKind.Note) lock (_lines) _notes.Add(e);
            var line = e.Kind switch
            {
                SessionEventKind.Turn => $"turn:{e.StopReason}",
                SessionEventKind.Message => $"message:{e.Text}",
                SessionEventKind.Note => $"note:{e.Text}",
                SessionEventKind.Tool => $"tool:{e.Id}:{e.Status}",
                _ => null,
            };
            if (line is not null) lock (_lines) _lines.Add(line);
        }

        public void Asked(ChatMessage message)
        {
            lock (_lines) _lines.Add($"taken:{message.Id}:{message.Text}");
        }

        public void SaidTo(ChatMessage message, DrivenReach reach)
        {
            lock (_lines) _lines.Add($"said:{message.Id}:{reach}:{message.Text}");
        }
    }

    /// <summary>
    /// 🔴 The measured shape (evidence §6, turn A): an agent that says it takes prompts mid-turn is sent the person's words
    /// the moment they are said, while its first prompt is still open. The first is answered when the words are taken, and
    /// the run goes on until the words' own prompt is answered — never closed while one is open — and the record reads: the
    /// words said, the turn the wire ended there, the words taken, and what the agent did after.
    /// </summary>
    [Fact]
    public async Task An_agent_that_takes_prompts_mid_turn_is_sent_the_words_at_once_and_the_run_waits_for_their_answer()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt" when !first.Task.IsCompleted:
                    // The task: its turn runs on, answered only once the person's words are taken.
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "session/prompt":
                    // The words, folded in at the next step: the earlier prompt handed off, the work going on as the words'.
                    self.Push(Answer(first.Task.Result, "end_turn"));
                    self.Push(Said("PINEAPPLE"));
                    second.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnSaid(record.SaidTo);

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync("D:/fam/Game", "read the five files", CancellationToken.None, inbox: inbox, asked: record.Asked);
        await first.Task.WaitAsync(Poll.Patience);
        Assert.True(inbox.Hold(new ChatMessage("put PINEAPPLE after the words", [])));
        var words = await second.Task.WaitAsync(Poll.Patience);

        // 🔴 The task's prompt is answered, and the session is still working on the words: nothing closes it yet.
        await Poll.Until(() => record.Lines.Contains("message:PINEAPPLE"), () => "the work after the hand-off never arrived", Poll.Patience);
        await Task.Delay(100);
        Assert.False(run.IsCompleted);
        Assert.DoesNotContain("session/close", agent.Methods);

        agent.Push(Answer(words, "end_turn"));
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal(["read the five files", "put PINEAPPLE after the words"], agent.Prompts);
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/prompt", "session/close"], agent.Methods);
        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(
            [
                "said:said-1:NextStep:put PINEAPPLE after the words",
                "turn:end_turn",
                "taken:said-1:put PINEAPPLE after the words",
                "message:PINEAPPLE",
                "turn:end_turn",
            ],
            record.Lines);
        Assert.False(inbox.Hold(new ChatMessage("after it closed", [])));
    }

    /// <summary>
    /// A word said while the session was still opening is told the moment the door is known, and sent only once the task
    /// is on the wire: it never overtakes the prompt it follows.
    /// </summary>
    [Fact]
    public async Task A_word_said_before_the_task_left_goes_right_after_it()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt" when !first.Task.IsCompleted:
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "session/prompt":
                    self.Push(Answer(first.Task.Result, "end_turn"));
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnSaid(record.SaidTo);
        Assert.True(inbox.Hold(new ChatMessage("said while it opened", [])));

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync("D:/fam/Game", "read the five files", CancellationToken.None, inbox: inbox, asked: record.Asked)
            .WaitAsync(Poll.Patience);

        Assert.Equal(["read the five files", "said while it opened"], agent.Prompts);
        Assert.Equal(
            ["said:said-1:NextStep:said while it opened", "turn:end_turn", "taken:said-1:said while it opened", "turn:end_turn"],
            record.Lines);
    }

    /// <summary>
    /// An agent that does not say it takes a prompt during a turn is never sent one (evidence §5: <c>codex-acp</c> resets
    /// its running turn on a second prompt). The words wait for the turn's end, and the record says so when they are said.
    /// </summary>
    [Fact]
    public async Task An_agent_that_does_not_say_so_hears_the_words_when_its_turn_ends()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Silent);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt" when !first.Task.IsCompleted:
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "session/prompt":
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnSaid(record.SaidTo);

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync("D:/fam/Game", "read the five files", CancellationToken.None, inbox: inbox, asked: record.Asked);
        var id = await first.Task.WaitAsync(Poll.Patience);
        Assert.True(inbox.Hold(new ChatMessage("the budget is in level.json", [])));
        // Held: nothing more goes to the agent while its turn runs.
        await Task.Delay(100);
        Assert.Single(agent.Prompts);

        agent.Push(Answer(id, "end_turn"));
        await run.WaitAsync(Poll.Patience);

        Assert.Equal(["read the five files", "the budget is in level.json"], agent.Prompts);
        Assert.Equal(
            [
                "said:said-1:TurnEnd:the budget is in level.json",
                "turn:end_turn",
                "taken:said-1:the budget is in level.json",
                "turn:end_turn",
            ],
            record.Lines);
    }

    /// <summary>
    /// COWORK1b (the message-urgency design §4.3, trap 17): Daoris's word held for the turn's end is judged again where it is
    /// delivered, by the key it carries. Its fact still holding, it goes as the next prompt and is told delivered; answered by a
    /// later submission meanwhile, it is dropped with a coded note naming it, never said late, and the run ends.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Daoris_word_held_for_the_turns_end_goes_while_its_fact_holds_and_is_dropped_once_answered(bool stands)
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Silent);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt" when !first.Task.IsCompleted:
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "session/prompt":
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnSaid(record.SaidTo);
        var delivered = 0;
        var told = new ChatMessage("Daoris: the person reopened it.", [])
        {
            Daoris = new DaorisWord("reopen/q1/desk/44") { Stands = _ => Task.FromResult(stands), Delivered = () => delivered++ },
        };

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync("X:/daoris/Game", "read the five files", CancellationToken.None, inbox: inbox, asked: record.Asked);
        var id = await first.Task.WaitAsync(Poll.Patience);
        Assert.True(inbox.Hold(told));
        agent.Push(Answer(id, "end_turn"));
        await run.WaitAsync(Poll.Patience);

        Assert.Equal(stands ? ["read the five files", "Daoris: the person reopened it."] : ["read the five files"], agent.Prompts);
        Assert.Equal(stands ? 1 : 0, delivered);
        var dropped = record.Notes.Where(note => note.Code == SessionEventCodes.Dropped).ToList();
        if (stands)
        {
            Assert.Empty(dropped);
            return;
        }

        Assert.Equal(["said-1"], Assert.Single(dropped).Words);
        Assert.DoesNotContain(record.Lines, line => line.StartsWith("taken:", StringComparison.Ordinal));
    }

    /// <summary>
    /// The agent's own word decides, and only a <c>true</c> it gave: absent, false, a string, or the key elsewhere is an
    /// agent never sent a prompt during a turn (ACP3's rule for the posture, again: null is never a licence to guess).
    /// </summary>
    [Theory]
    [InlineData(Queues, true)]
    [InlineData(Silent, false)]
    [InlineData("""{"protocolVersion":1}""", false)]
    [InlineData("""{"agentCapabilities":{"_meta":{"claudeCode":{"promptQueueing":false}}}}""", false)]
    [InlineData("""{"agentCapabilities":{"_meta":{"claudeCode":{"promptQueueing":"true"}}}}""", false)]
    [InlineData("""{"agentCapabilities":{"_meta":{"promptQueueing":true}}}""", false)]
    [InlineData("""{"_meta":{"steering":{"supported":true}},"agentCapabilities":{}}""", false)]
    [InlineData("""[]""", false)]
    public void Whether_an_agent_takes_prompts_mid_turn_is_its_own_word(string initialized, bool takes)
    {
        using var answer = JsonDocument.Parse(initialized);
        Assert.Equal(takes, AcpSession.TakesWordsMidTurn(answer.RootElement));
    }

    /// <summary>
    /// Words sent while the turn ran, which the session never took because it ended first, are said in the record — the
    /// counterpart of what never left the inbox (SESS3) — rather than shown for ever as on their way.
    /// </summary>
    [Fact]
    public async Task Words_on_their_way_when_the_session_ends_are_said_in_the_record()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt" when !first.Task.IsCompleted:
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "session/prompt":
                    // The agent dies with both prompts open.
                    self.Close();
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync("D:/fam/Game", "read the five files", CancellationToken.None, inbox: inbox, asked: record.Asked);
        await first.Task.WaitAsync(Poll.Patience);
        Assert.True(inbox.Hold(new ChatMessage("one more thing", [])));

        await Assert.ThrowsAsync<DriverException>(() => run.WaitAsync(Poll.Patience));
        Assert.DoesNotContain(record.Lines, line => line.StartsWith("taken:", StringComparison.Ordinal));
        Assert.Contains(record.Lines, line => line.StartsWith("note:", StringComparison.Ordinal) && line.Contains("1 message(s)"));

        // CONVNOTE2d1: the line is worded by its code, the reason a part of its own, the English unchanged.
        var note = Assert.Single(record.Notes, e => e.Text!.Contains("message(s)"));
        Assert.Equal("— 1 message(s) the person sent while it worked never reached it: it ended first.", note.Text);
        var part = Assert.Single(note.Parts!);
        Assert.Equal(("words.unreached", 1L, "ended-first"), (part.Code, Convert.ToInt64(part.Value("count")), part.Value("why")));
        NoteAssert.Holds(note.Text, note.Parts);
    }

    /// <summary>
    /// An agent that said it takes prompts mid-turn and then refuses one costs that word, said in the record in the
    /// agent's words, never the run: the session's own work goes on to its end.
    /// </summary>
    [Fact]
    public async Task A_word_the_agent_refuses_mid_turn_is_said_and_the_run_goes_on()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt" when !first.Task.IsCompleted:
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "session/prompt":
                    self.Push($$$"""{"jsonrpc":"2.0","id":{{{frame.GetProperty("id").GetRawText()}}},"error":{"code":-32603,"message":"Internal error: busy"}}""");
                    self.Push(Answer(first.Task.Result, "end_turn"));
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync("D:/fam/Game", "read the five files", CancellationToken.None, inbox: inbox, asked: record.Asked);
        await first.Task.WaitAsync(Poll.Patience);
        Assert.True(inbox.Hold(new ChatMessage("one more thing", [])));
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Contains(record.Lines, line => line.StartsWith("note:", StringComparison.Ordinal) && line.Contains("Internal error: busy"));
        // CONVNOTE2d1: Daoris's lead is coded, what the agent said rides as its words.
        var note = Assert.Single(record.Notes, e => e.Text!.Contains("would not take"));
        Assert.StartsWith("— the session would not take what the person added: ", note.Text);
        Assert.Equal(["words.refused", null], NoteAssert.Codes(note.Parts));
        Assert.Equal(NoteBy.Agent, note.Parts![1].By);
        Assert.Contains("Internal error: busy", note.Parts[1].Words);
        NoteAssert.Holds(note.Text, note.Parts);
        Assert.Equal("session/close", agent.Methods[^1]);
    }

    /// <summary>
    /// 🔴 USERSHOWN1: a word sent mid-turn waits behind the prompt before it, and that prompt's answer hands the turn to it. An
    /// answer that is an error hands nothing on: the door failed and the agent never read the word, which the install's record
    /// kept as taken at the instant its connection dropped (2026-10-14). The run hands it back as never reached, for the driver
    /// to keep waiting.
    /// </summary>
    [Fact]
    public async Task A_word_sent_mid_turn_is_not_taken_when_the_turn_before_it_fails()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt" when !first.Task.IsCompleted:
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "session/prompt":
                    // The word queues behind the turn; the turn's own prompt then fails, as a dropped connection does.
                    self.Push($$$"""{"jsonrpc":"2.0","id":{{{first.Task.Result}}},"error":{"code":-32603,"message":"Internal error: API Error: Connection dropped (ECONNRESET)"}}""");
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event);

        var run = session.RunAsync("D:/fam/Game", "read the five files", CancellationToken.None, inbox: inbox, asked: record.Asked);
        await first.Task.WaitAsync(Poll.Patience);
        Assert.True(inbox.Hold(new ChatMessage("stuck?", [])));

        var failed = await Assert.ThrowsAnyAsync<DriverException>(() => run.WaitAsync(Poll.Patience));

        Assert.Contains("ECONNRESET", failed.Message);
        Assert.DoesNotContain(record.Lines, line => line.StartsWith("taken:", StringComparison.Ordinal));
        Assert.Equal([("said-1", "stuck?")], session.Unreached.Select(word => (word.Id, word.Text)));
    }

    /// <summary>
    /// USERSHOWN1, a conversation's door, which goes on after a turn's error: the word behind the failed turn is taken by the
    /// first thing the agent does for it, and said before that thing, so the answer never sits above the question.
    /// </summary>
    [Fact]
    public async Task A_conversations_word_behind_a_failed_turn_is_taken_when_the_agent_answers_it()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt" when !first.Task.IsCompleted:
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "session/prompt":
                    self.Push($$$"""{"jsonrpc":"2.0","id":{{{first.Task.Result}}},"error":{"code":-32603,"message":"Internal error: overloaded"}}""");
                    self.Push(Said("PINEAPPLE"));
                    second.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event);

        await session.OpenAsync("D:/fam/Game", CancellationToken.None);
        session.OnTaken(record.Asked);
        var turn = session.PromptAsync("read the five files", CancellationToken.None);
        await first.Task.WaitAsync(Poll.Patience);
        var words = session.SayAsync(new ChatMessage("put PINEAPPLE after the words", []) { Id = "said-c1" }, CancellationToken.None);
        var id = await second.Task.WaitAsync(Poll.Patience);

        await Assert.ThrowsAnyAsync<DriverException>(() => turn.WaitAsync(Poll.Patience));
        agent.Push(Answer(id, "end_turn"));
        Assert.Equal("end_turn", await words.WaitAsync(Poll.Patience));
        Assert.Equal(["taken:said-c1:put PINEAPPLE after the words", "message:PINEAPPLE", "turn:end_turn"], record.Lines);
        session.Release();
    }

    /// <summary>
    /// 🔴 A conversation on the next-step door (MSG1c, D137 §2.1): the session says so once it opens, and the person's word
    /// said while a turn runs goes at once as a prompt of the same session. It is taken when the agent hands the turn before
    /// it off, and the record reads as the driven door's does: the turn the wire ended there, the words taken, the work after.
    /// </summary>
    [Fact]
    public async Task A_conversations_word_said_during_a_turn_goes_at_once_and_is_taken_at_the_hand_off()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt" when !first.Task.IsCompleted:
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "session/prompt":
                    self.Push(Answer(first.Task.Result, "end_turn"));
                    self.Push(Said("PINEAPPLE"));
                    second.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event);

        await session.OpenAsync("D:/fam/Game", CancellationToken.None);
        session.OnTaken(record.Asked);
        Assert.True(session.NextStep);
        var turn = session.PromptAsync("read the five files", CancellationToken.None);
        await first.Task.WaitAsync(Poll.Patience);
        var words = session.SayAsync(new ChatMessage("put PINEAPPLE after the words", []) { Id = "said-c1" }, CancellationToken.None);
        var id = await second.Task.WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", await turn.WaitAsync(Poll.Patience));
        await Poll.Until(() => record.Lines.Contains("message:PINEAPPLE"), () => string.Join(" | ", record.Lines), Poll.Patience);
        Assert.False(words.IsCompleted);

        agent.Push(Answer(id, "end_turn"));
        Assert.Equal("end_turn", await words.WaitAsync(Poll.Patience));
        Assert.Equal(["read the five files", "put PINEAPPLE after the words"], agent.Prompts);
        Assert.Equal(
            ["turn:end_turn", "taken:said-c1:put PINEAPPLE after the words", "message:PINEAPPLE", "turn:end_turn"],
            record.Lines);
        session.Release();
    }

    /// <summary>
    /// 🔴 A conversation whose agent did not say it takes prompts during a turn is never sent one (evidence §5): the session
    /// says so, and a word handed to it anyway is refused before anything reaches the wire.
    /// </summary>
    [Fact]
    public async Task A_conversation_whose_agent_does_not_say_so_is_never_sent_a_word_mid_turn()
    {
        var agent = new Agent((frame, self) => frame.GetProperty("method").GetString() switch
        {
            "initialize" => Ok(frame, Silent),
            "session/new" => Ok(frame, """{"sessionId":"s-1"}"""),
            _ => frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null,
        });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });

        await session.OpenAsync("D:/fam/Game", CancellationToken.None);

        Assert.False(session.NextStep);
        await Assert.ThrowsAsync<DriverException>(() => session.SayAsync(new ChatMessage("one more thing", []), CancellationToken.None));
        Assert.DoesNotContain("session/prompt", agent.Methods);
        session.Release();
    }

    /// <summary>
    /// STEER2a: *Send now* steers only where the agent said both that it takes words during a turn (the next-step door) and that
    /// it offers the steering extension, each by its own <c>true</c>. Codex offers the extension without the first, and stays a
    /// turn-end door until STEER3 measures its <c>turn/steer</c>.
    /// </summary>
    [Theory]
    [InlineData(Steers, true)]
    [InlineData(Queues, false)]
    [InlineData("""{"_meta":{"steering":{"supported":true}},"agentCapabilities":{}}""", false)]
    [InlineData("""{"_meta":{"steering":{"supported":"true"}},"agentCapabilities":{"_meta":{"claudeCode":{"promptQueueing":true}}}}""", false)]
    [InlineData("""{"agentCapabilities":{"_meta":{"claudeCode":{"promptQueueing":true},"steering":{"supported":true}}}}""", false)]
    [InlineData("""[]""", false)]
    public void Whether_send_now_steers_is_the_agents_own_word(string initialized, bool steers)
    {
        using var answer = JsonDocument.Parse(initialized);
        Assert.Equal(steers, AcpSession.SteersNow(answer.RootElement));
    }

    /// <summary>
    /// 🔴 STEER2a, the measured shape (evidence §6, turn B): *Send now* while the task's first call is announced sends the words
    /// by <c>_session/steering</c>, in the adapter's own parameters, asking it never to start a turn of its own. Answered
    /// <c>injected</c>, they are taken there and the running turn goes on as their answer; the call the steer cut never hears
    /// again, and the record closes it as cut when the turn ends, before the turn's end, never leaving it running.
    /// </summary>
    [Fact]
    public async Task Sending_now_steers_the_words_into_the_running_turn_and_the_call_it_cut_is_closed_at_the_turns_end()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        JsonElement? steering = null;
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Steers);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    // The task's first call, its input not yet streamed (turn B at 34,668 ms).
                    self.Push(Call("tool_call", "read-1", "pending"));
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "_session/steering":
                    steering = frame.GetProperty("params").Clone();
                    self.Push(Ok(frame, """{"outcome":"injected"}"""));
                    // The cut call never hears again; the work goes on in the same prompt, as the words' answer.
                    self.Push(Call("tool_call", "read-2", "pending"));
                    self.Push(Call("tool_call_update", "read-2", "completed"));
                    self.Push(Said("MANGO"));
                    self.Push(Answer(first.Task.Result, "end_turn"));
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnSaid(record.SaidTo);

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync("D:/fam/Game", "read the five files in reverse", CancellationToken.None, inbox: inbox, asked: record.Asked);
        await first.Task.WaitAsync(Poll.Patience);
        await Poll.Until(() => record.Lines.Contains("tool:read-1:pending"), () => string.Join(" | ", record.Lines), Poll.Patience);

        var now = await inbox.HoldNowAsync(new ChatMessage("put MANGO after the words", []));
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal(new SentNow("now", true, null), now);
        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(["initialize", "session/new", "session/prompt", "_session/steering", "session/close"], agent.Methods);
        Assert.Equal("s-1", steering!.Value.GetProperty("sessionId").GetString());
        Assert.Equal("put MANGO after the words", steering.Value.GetProperty("prompt")[0].GetProperty("text").GetString());
        Assert.Equal("promptRequired", steering.Value.GetProperty("_meta").GetProperty("steering").GetProperty("idleBehavior").GetString());
        Assert.Equal(
            [
                "tool:read-1:pending",
                "said:said-1:Steered:put MANGO after the words",
                "taken:said-1:put MANGO after the words",
                "tool:read-2:pending",
                "tool:read-2:completed",
                "message:MANGO",
                "tool:read-1:cut",
                "turn:end_turn",
            ],
            record.Lines);
    }

    /// <summary>
    /// 🔴 STEER2a (the owner's case): a word queued at the next step is on its way when the person, finding it urgent, presses
    /// *Send now* with new words. Those are steered past it, the person's choice to go first; nothing is stopped, the queued
    /// word is still taken at its next step, and the record reads in the wire's order.
    /// </summary>
    [Fact]
    public async Task Sending_now_past_a_word_on_its_way_steers_and_the_word_is_still_taken_at_its_next_step()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Steers);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt" when !first.Task.IsCompleted:
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "session/prompt":
                    // The queued word: on its way, taken at the next step.
                    second.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "_session/steering":
                    self.Push(Ok(frame, """{"outcome":"injected"}"""));
                    self.Push(Said("MANGO"));
                    // The next step: the task's prompt handed off to the queued word, which the agent works on to the end.
                    self.Push(Answer(first.Task.Result, "end_turn"));
                    self.Push(Said("PINEAPPLE"));
                    self.Push(Answer(second.Task.Result, "end_turn"));
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnSaid(record.SaidTo);

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync("D:/fam/Game", "read the five files", CancellationToken.None, inbox: inbox, asked: record.Asked);
        await first.Task.WaitAsync(Poll.Patience);
        Assert.True(inbox.Hold(new ChatMessage("put PINEAPPLE after the words", [])));
        await second.Task.WaitAsync(Poll.Patience);

        var now = await inbox.HoldNowAsync(new ChatMessage("stop: the tests are in /spec", []));
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal(new SentNow("now", true, null), now);
        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/prompt", "_session/steering", "session/close"], agent.Methods);
        Assert.Equal(
            [
                "said:said-1:NextStep:put PINEAPPLE after the words",
                "said:said-2:Steered:stop: the tests are in /spec",
                "taken:said-2:stop: the tests are in /spec",
                "message:MANGO",
                "turn:end_turn",
                "taken:said-1:put PINEAPPLE after the words",
                "message:PINEAPPLE",
                "turn:end_turn",
            ],
            record.Lines);
    }

    /// <summary>
    /// 🔴 STEER2a (a second maker's review): a call that finished stays finished through an update that says no status, so a
    /// later steer never closes it as cut.
    /// </summary>
    [Fact]
    public async Task A_call_that_finished_is_never_closed_as_cut_by_a_later_steer()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Steers);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push(Call("tool_call", "read-1", "pending"));
                    self.Push(Call("tool_call_update", "read-1", "completed"));
                    self.Push(Call("tool_call_update", "read-1", null));
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "_session/steering":
                    self.Push(Ok(frame, """{"outcome":"injected"}"""));
                    self.Push(Answer(first.Task.Result, "end_turn"));
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync("D:/fam/Game", "read the five files", CancellationToken.None, inbox: inbox, asked: record.Asked);
        await first.Task.WaitAsync(Poll.Patience);
        await Poll.Until(() => record.Lines.Contains("tool:read-1:"), () => string.Join(" | ", record.Lines), Poll.Patience);
        await inbox.HoldNowAsync(new ChatMessage("one more thing", []));
        await run.WaitAsync(Poll.Patience);

        Assert.Contains("turn:end_turn", record.Lines);
        Assert.DoesNotContain("tool:read-1:cut", record.Lines);
    }

    /// <summary>
    /// STEER2a (D136 point 6): a steer the agent refuses, or answers with anything but <c>injected</c>, costs those words, said
    /// in the record by its code and the words' id, so the page settles them; the run's own work goes on to its end.
    /// </summary>
    [Theory]
    [InlineData("""{"error":{"code":-32603,"message":"Internal error: Session ended"}}""", "words.refused")]
    [InlineData("""{"result":{"outcome":"somethingElse"}}""", "words.not-reached")]
    public async Task A_steer_the_agent_does_not_take_costs_only_those_words_and_the_run_goes_on(string answer, string code)
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Steers);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "_session/steering":
                    self.Push($$"""{"jsonrpc":"2.0","id":{{frame.GetProperty("id").GetRawText()}},{{answer[1..^1]}}}""");
                    self.Push(Answer(first.Task.Result, "end_turn"));
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync("D:/fam/Game", "read the five files", CancellationToken.None, inbox: inbox, asked: record.Asked);
        await first.Task.WaitAsync(Poll.Patience);
        Assert.Equal(new SentNow("now", true, null), await inbox.HoldNowAsync(new ChatMessage("one more thing", [])));
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.DoesNotContain(record.Lines, line => line.StartsWith("taken:", StringComparison.Ordinal));
        var note = Assert.Single(record.Notes);
        Assert.Equal(code, note.Parts![0].Code);
        Assert.Equal(["said-1"], note.Words);
        NoteAssert.Holds(note.Text, note.Parts);
        Assert.Equal("session/close", agent.Methods[^1]);
    }

    /// <summary>
    /// STEER2d: a steer answered <c>promptRequired</c> (the turn ended between the press and the steer) is not lost: the adapter
    /// left the words to the client, so they are handed back to the held queue and prompted next. They are said once and taken
    /// once, where they are finally taken, and no <c>words.not-reached</c> note is written.
    /// </summary>
    [Fact]
    public async Task A_steer_answered_prompt_required_goes_as_the_next_prompt_and_is_taken_there()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var prompts = new List<string>();
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Steers);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    lock (prompts) prompts.Add(frame.GetProperty("params").GetProperty("prompt")[0].GetProperty("text").GetString()!);
                    if (prompts.Count == 1)
                    {
                        first.SetResult(frame.GetProperty("id").GetInt32());
                        return null;
                    }

                    self.Push(Said("MANGO"));
                    return Answer(frame.GetProperty("id").GetInt32(), "end_turn");
                case "_session/steering":
                    self.Push(Ok(frame, """{"outcome":"promptRequired","reason":"noRunningTurn"}"""));
                    self.Push(Answer(first.Task.Result, "end_turn"));
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });
        inbox.OnSaid(record.SaidTo);

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync("D:/fam/Game", "read the five files", CancellationToken.None, inbox: inbox, asked: record.Asked);
        await first.Task.WaitAsync(Poll.Patience);
        Assert.Equal(new SentNow("now", true, null), await inbox.HoldNowAsync(new ChatMessage("one more thing", [])));
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(["read the five files", "one more thing"], prompts);
        Assert.Empty(record.Notes);
        Assert.Single(record.Lines, line => line.StartsWith("said:", StringComparison.Ordinal));
        Assert.Single(record.Lines, line => line.StartsWith("taken:", StringComparison.Ordinal));
        Assert.Contains("taken:said-1:one more thing", record.Lines);
        Assert.Equal("session/close", agent.Methods[^1]);
    }

    /// <summary>
    /// STEER2a: a conversation's *Send now* is the same steer, taken through <see cref="AcpSession.OnTaken"/> where the agent
    /// answered <c>injected</c>, before anything after the answer is read; an agent that never said it steers is sent nothing.
    /// </summary>
    [Fact]
    public async Task A_conversations_steer_is_taken_where_the_agent_answers_injected_and_never_sent_to_one_that_does_not_steer()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Steers);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "_session/steering":
                    self.Push(Ok(frame, """{"outcome":"injected"}"""));
                    self.Push(Said("MANGO"));
                    self.Push(Answer(first.Task.Result, "end_turn"));
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event);
        await session.OpenAsync("D:/fam/Game", CancellationToken.None);
        session.OnTaken(record.Asked);
        Assert.True(session.Steers);
        var turn = session.PromptAsync("read the five files", CancellationToken.None);
        await first.Task.WaitAsync(Poll.Patience);

        var steered = await session.SteerAsync(new ChatMessage("put MANGO after the words", []) { Id = "said-c1" }, CancellationToken.None);

        Assert.Equal("injected", steered);
        Assert.Equal("end_turn", await turn.WaitAsync(Poll.Patience));
        Assert.Equal(["taken:said-c1:put MANGO after the words", "message:MANGO", "turn:end_turn"], record.Lines);
        session.Release();

        var silent = new Agent((frame, self) => frame.GetProperty("method").GetString() switch
        {
            "initialize" => Ok(frame, Queues),
            "session/new" => Ok(frame, """{"sessionId":"s-1"}"""),
            _ => frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null,
        });
        var queues = new AcpSession(silent.Incoming, silent.Outgoing, _ => { });
        await queues.OpenAsync("D:/fam/Game", CancellationToken.None);
        Assert.False(queues.Steers);
        await Assert.ThrowsAsync<DriverException>(() => queues.SteerAsync(new ChatMessage("one more thing", []), CancellationToken.None));
        Assert.DoesNotContain("_session/steering", silent.Methods);
        queues.Release();
    }
}
