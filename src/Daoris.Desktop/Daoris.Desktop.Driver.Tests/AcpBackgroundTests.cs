using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BGWAIT1: a driven turn that ends while the session's own background work still runs is not a question. Against a fake
/// agent over in-memory streams, shaped on the frames <c>claude-agent-acp</c> 0.79.0 sent in CONSOLE2a's first run
/// (docs/2026-09-28-console2-streams-evidence.md): <c>async_task_spawned</c> for a command its Bash call backgrounded, the
/// call itself completed at once with <c>backgrounded: true</c>, the prompt answered <c>end_turn</c> while the task ran, and
/// the task ended later by <c>async_task_state_update</c>, <c>stopped</c> then <c>completed</c> in one breath. The agent's last
/// words are the owner's real case (2026-10-08). No process and no model, so the fast half.
/// </summary>
public sealed class AcpBackgroundTests
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

    private static string Update(string update) =>
        """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":""" + update + "}}";

    private static string Said(string text) =>
        Update("""{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":""" + JsonSerializer.Serialize(text) + "}}");

    /// <summary>What <c>claude-agent-acp</c> answers at <c>initialize</c> (CONSOLE2a; docs/2026-10-03-steer-evidence.md §1).</summary>
    private const string Queues = """{"protocolVersion":1,"agentCapabilities":{"_meta":{"claudeCode":{"promptQueueing":true}}}}""";

    /// <summary>The last words of the driven session in the owner's real case, which parked on them.</summary>
    private const string Waiting =
        "Gates are running in the background (lint → prettier → knowledge check → doc links → v3 tests → build → Angular Jest); "
        + "I'll pick up when they finish.";

    /// <summary>
    /// The turn the wire recorded, in its order: the Bash call that backgrounds the gates, the task announced and its
    /// output named, the call's result naming the task, the call completed at once, and the agent's words. The task is
    /// still running when the prompt is answered.
    /// </summary>
    private static void BackgroundsTheGates(Agent agent, string task = "bdd847eoq")
    {
        agent.Push(Update("""{"sessionUpdate":"tool_call","toolCallId":"toolu_bg","title":"npm run gates","kind":"execute","status":"pending","rawInput":{"command":"npm run gates","description":"Run the gates","run_in_background":true}}"""));
        agent.Push(Update($$"""{"sessionUpdate":"async_task_spawned","asyncTaskId":"{{task}}","name":"Run the gates","taskType":"shell","description":"Run the gates","showInTranscript":false,"canStop":true}"""));
        agent.Push(Update($$"""{"sessionUpdate":"async_task_progress","asyncTaskId":"{{task}}","toolCallId":"toolu_bg"}"""));
        agent.Push(Update($$$"""{"_meta":{"claudeCode":{"toolResponse":{"stdout":"","stderr":"","interrupted":false,"isImage":false,"noOutputExpected":false,"backgroundTaskId":"{{{task}}}"},"toolName":"Bash"}},"toolCallId":"toolu_bg","sessionUpdate":"tool_call_update"}"""));
        agent.Push(Update("""{"_meta":{"claudeCode":{"toolName":"Bash"},"jetbrains":{"air":{"version":1,"asyncTasks":{"backgrounded":true}}}},"toolCallId":"toolu_bg","sessionUpdate":"tool_call_update","status":"completed"}"""));
        agent.Push(Said(Waiting));
    }

    /// <summary>The task's ending as the wire said it: <c>stopped</c>, then <c>completed</c> in the same millisecond.</summary>
    private static void GatesEnd(Agent agent, string task = "bdd847eoq", string state = "completed")
    {
        agent.Push(Update($$"""{"sessionUpdate":"async_task_state_update","asyncTaskId":"{{task}}","state":"stopped"}"""));
        agent.Push(Update($$"""{"sessionUpdate":"async_task_state_update","asyncTaskId":"{{task}}","state":"{{state}}"}"""));
    }

    /// <summary>The run's record: the turns, the agent's words and the driver's notes, in order.</summary>
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
    }

    /// <summary>
    /// 🔴 The owner's real case (2026-10-08): the agent backgrounded its gates, said it would pick up when they finish, and
    /// ended its turn holding its quest. The session is not closed at the turn's end, so the record stays working and
    /// nothing parks it on the person; once the wire says the work ended, the session is told so in a turn of its own,
    /// and only that turn's end closes it.
    /// </summary>
    [Fact]
    public async Task A_turn_that_ends_on_its_own_background_work_keeps_working_and_goes_on_when_the_work_ends()
    {
        var ended = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt" when self.Prompts.Count == 1:
                    BackgroundsTheGates(self);
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                case "session/prompt":
                    ended.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var asked = 0;

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync(
                "D:/fam/Game", "close quest #12", CancellationToken.None,
                waitsOnBackground: _ =>
                {
                    Interlocked.Increment(ref asked);
                    return Task.FromResult(true);
                });

        // The turn has ended and the work still runs: the session waits, open, and says why.
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal) && line.Contains("Run the gates")),
            () => string.Join(" | ", record.Lines), TimeSpan.FromSeconds(10));
        await Task.Delay(300);
        Assert.False(run.IsCompleted);
        Assert.DoesNotContain("session/close", agent.Methods);
        Assert.Equal(1, Volatile.Read(ref asked));

        // The work ends; the harness wakes its agent itself, with no prompt open (CONSOLE2a), and then the driver's turn goes.
        GatesEnd(agent);
        agent.Push(Said("The gates passed."));
        var id = await ended.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(run.IsCompleted);

        agent.Push(Answer(id, "end_turn"));
        var outcome = await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/prompt", "session/close"], agent.Methods);
        var resumed = agent.Prompts[1];
        Assert.Contains("background work", resumed);
        Assert.Contains("`Run the gates` (completed)", resumed);
        Assert.DoesNotContain("stopped", resumed);
        Assert.Equal(
            [
                $"message:{Waiting}",
                "turn:end_turn",
                "note:— its turn ended while its own background work runs: `Run the gates`. The session waits for that work, "
                + "and goes on when it ends.",
                "message:The gates passed.",
                "note:— its background work ended: `Run the gates` (completed). The session goes on.",
                "turn:end_turn",
            ],
            record.Lines);
    }

    /// <summary>
    /// 🔴 A real question still parks: a turn that ends with no background work running ends the run as before, one prompt
    /// and the close, and nothing asks whether to wait. A task that ended inside the turn is not running (CONSOLE2a's second
    /// run: the ticker finished while an async subagent held the turn open).
    /// </summary>
    [Fact]
    public async Task A_turn_that_asks_with_no_background_work_running_ends_as_before()
    {
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    BackgroundsTheGates(self);
                    GatesEnd(self);
                    self.Push(Said("Which branch should the release land on?"));
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var asked = 0;

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync(
                "D:/fam/Game", "close quest #12", CancellationToken.None,
                waitsOnBackground: _ =>
                {
                    Interlocked.Increment(ref asked);
                    return Task.FromResult(true);
                })
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(["initialize", "session/new", "session/prompt", "session/close"], agent.Methods);
        Assert.Equal(0, asked);
        Assert.DoesNotContain(record.Lines, line => line.StartsWith("note:", StringComparison.Ordinal));
    }

    /// <summary>
    /// D105 stands where the quest does not wait on it: a turn whose session no longer holds its quest as a park would (it
    /// closed it, or asked another repository) ends with its background work still running, and that work ends with the
    /// session. The driver is asked once, and its no is the run's end.
    /// </summary>
    [Fact]
    public async Task Background_work_a_quest_no_longer_waits_on_ends_with_the_session()
    {
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    BackgroundsTheGates(self);
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var asked = 0;

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync(
                "D:/fam/Game", "close quest #12", CancellationToken.None,
                waitsOnBackground: _ =>
                {
                    Interlocked.Increment(ref asked);
                    return Task.FromResult(false);
                })
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(["initialize", "session/new", "session/prompt", "session/close"], agent.Methods);
        Assert.Equal(1, asked);
    }

    /// <summary>
    /// The wait has an end besides the work's: an agent whose stream ends while the session waits (it exited, or the person's
    /// stop or the timeout killed it) ends the run at once as the door's failure, never as a clean turn a park would read.
    /// </summary>
    [Fact]
    public async Task An_agent_that_ends_while_its_session_waits_ends_the_run_as_a_failure()
    {
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    BackgroundsTheGates(self);
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None, waitsOnBackground: _ => Task.FromResult(true));
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), TimeSpan.FromSeconds(10));
        agent.Close();

        var failed = await Assert.ThrowsAsync<DriverException>(() => run.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Contains("background work", failed.Message);
        Assert.Single(agent.Prompts);
    }

    /// <summary>
    /// The person's words still reach a session that waits on its background work: on the next-step door they go at once
    /// (STEER1), are answered in a turn of their own, and the session goes on waiting for the work, then is told it ended.
    /// </summary>
    [Fact]
    public async Task Words_said_while_the_session_waits_go_at_once_and_the_wait_goes_on()
    {
        var words = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ended = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt" when self.Prompts.Count == 1:
                    BackgroundsTheGates(self);
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                case "session/prompt" when self.Prompts.Count == 2:
                    words.SetResult(frame.GetProperty("id").GetInt32());
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                case "session/prompt":
                    ended.SetResult(frame.GetProperty("id").GetInt32());
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync(
                "D:/fam/Game", "close quest #12", CancellationToken.None, inbox: inbox, waitsOnBackground: _ => Task.FromResult(true));
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), TimeSpan.FromSeconds(10));

        Assert.True(inbox.Hold(new ChatMessage("skip the Angular Jest run", [])));
        await words.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(300);
        Assert.False(run.IsCompleted);
        Assert.False(ended.Task.IsCompleted);

        GatesEnd(agent);
        await ended.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(3, agent.Prompts.Count);
        Assert.Equal("skip the Angular Jest run", agent.Prompts[1]);
        Assert.Contains("background work", agent.Prompts[2]);
        Assert.Equal("session/close", agent.Methods[^1]);
    }

    /// <summary>
    /// Words held for the turn's end go before the wait, on an agent that takes no prompt mid-turn: the person said them
    /// while the turn ran, so they are its next prompt. The wait comes after their turn, and the work's end after that.
    /// </summary>
    [Fact]
    public async Task Words_held_for_the_turns_end_go_before_the_wait()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, """{"protocolVersion":1,"agentCapabilities":{}}""");
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt" when self.Prompts.Count == 1:
                    BackgroundsTheGates(self);
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "session/prompt":
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var inbox = new DrivenInbox(_ => { });

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync(
                "D:/fam/Game", "close quest #12", CancellationToken.None, inbox: inbox, waitsOnBackground: _ => Task.FromResult(true));
        var id = await first.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(inbox.Hold(new ChatMessage("skip the Angular Jest run", [])));
        agent.Push(Answer(id, "end_turn"));

        await Poll.Until(() => agent.Prompts.Count == 2, () => string.Join(" | ", agent.Prompts), TimeSpan.FromSeconds(10));
        Assert.Equal("skip the Angular Jest run", agent.Prompts[1]);
        await Task.Delay(300);
        Assert.False(run.IsCompleted);

        GatesEnd(agent);
        await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(3, agent.Prompts.Count);
        Assert.Contains("background work", agent.Prompts[2]);
    }

    /// <summary>
    /// Work the session starts while it waits is waited for too, and the turn that says the work ended names every piece of
    /// it, each in the state the wire last gave: a failed one as failed.
    /// </summary>
    [Fact]
    public async Task Work_started_while_the_session_waits_is_waited_for_and_named_in_its_last_state()
    {
        var ended = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt" when self.Prompts.Count == 1:
                    BackgroundsTheGates(self);
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                case "session/prompt":
                    ended.SetResult(frame.GetProperty("id").GetInt32());
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();

        var run = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event)
            .RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None, waitsOnBackground: _ => Task.FromResult(true));
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), TimeSpan.FromSeconds(10));

        // Woken by the first ending, the agent starts the build in the background before the first is said.
        agent.Push(Update("""{"sessionUpdate":"async_task_spawned","asyncTaskId":"b2","name":"Build","taskType":"shell","canStop":true}"""));
        GatesEnd(agent, state: "failed");
        await Task.Delay(500);
        Assert.False(ended.Task.IsCompleted);

        GatesEnd(agent, task: "b2");
        await ended.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await run.WaitAsync(TimeSpan.FromSeconds(10));

        var resumed = agent.Prompts[1];
        Assert.Contains("`Run the gates` (failed)", resumed);
        Assert.Contains("`Build` (completed)", resumed);
    }
}
