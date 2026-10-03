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

    /// <summary>The run's record and what it was told, in one order: the events, each word said and each word taken.</summary>
    private sealed class Record
    {
        private readonly List<string> _lines = [];

        public IReadOnlyList<string> Lines
        {
            get { lock (_lines) return [.. _lines]; }
        }

        public void Event(SessionEvent e)
        {
            var line = e.Kind switch
            {
                SessionEventKind.Turn => $"turn:{e.StopReason}",
                SessionEventKind.Message => $"message:{e.Text}",
                SessionEventKind.Note => $"note:{e.Text}",
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
        await first.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(inbox.Hold(new ChatMessage("put PINEAPPLE after the words", [])));
        var words = await second.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // 🔴 The task's prompt is answered, and the session is still working on the words: nothing closes it yet.
        await Poll.Until(() => record.Lines.Contains("message:PINEAPPLE"), () => "the work after the hand-off never arrived", TimeSpan.FromSeconds(10));
        await Task.Delay(100);
        Assert.False(run.IsCompleted);
        Assert.DoesNotContain("session/close", agent.Methods);

        agent.Push(Answer(words, "end_turn"));
        var outcome = await run.WaitAsync(TimeSpan.FromSeconds(10));

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
            .WaitAsync(TimeSpan.FromSeconds(10));

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
        var id = await first.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(inbox.Hold(new ChatMessage("the budget is in level.json", [])));
        // Held: nothing more goes to the agent while its turn runs.
        await Task.Delay(100);
        Assert.Single(agent.Prompts);

        agent.Push(Answer(id, "end_turn"));
        await run.WaitAsync(TimeSpan.FromSeconds(10));

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
        await first.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(inbox.Hold(new ChatMessage("one more thing", [])));

        await Assert.ThrowsAsync<DriverException>(() => run.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.DoesNotContain(record.Lines, line => line.StartsWith("taken:", StringComparison.Ordinal));
        Assert.Contains(record.Lines, line => line.StartsWith("note:", StringComparison.Ordinal) && line.Contains("1 message(s)"));
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
        await first.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(inbox.Hold(new ChatMessage("one more thing", [])));
        var outcome = await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Contains(record.Lines, line => line.StartsWith("note:", StringComparison.Ordinal) && line.Contains("Internal error: busy"));
        Assert.Equal("session/close", agent.Methods[^1]);
    }
}
