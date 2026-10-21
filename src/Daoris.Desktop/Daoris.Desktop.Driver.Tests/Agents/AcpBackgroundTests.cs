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

    private static string Update(string update) => UpdateOf("s-1", update);

    private static string UpdateOf(string session, string update) =>
        """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":""" + JsonSerializer.Serialize(session) + ""","update":"""
        + update + "}}";

    /// <summary>
    /// BGWAIT2: how <c>claude-agent-acp</c> 0.79.0 says a turn its harness started itself has ended (<c>acp-agent.js</c>
    /// l.3505-3521): the result's <c>usage_update</c>, carrying the result's origin, an autonomous kind (l.233-239). No turn is
    /// answered for it (l.3546-3573), and the idle after it is absorbed (l.2864-2875).
    /// </summary>
    private static string WokenEnd(string kind = "task-notification") =>
        """{"sessionUpdate":"usage_update","used":61234,"size":200000,"cost":{"amount":1.25,"currency":"USD"},"_meta":{"_claude/origin":{"kind":"""
        + JsonSerializer.Serialize(kind) + "}}}";

    /// <summary>The woken turn's end as the install's record said it on 2026-10-19: the same <c>usage</c> twice, and no turn.</summary>
    private static void WokenEnds(Agent agent)
    {
        agent.Push(Update(WokenEnd()));
        agent.Push(Update(WokenEnd()));
    }

    /// <summary>A bound no test reaches: the go-on never goes in a test of the harness's own wake.</summary>
    private static readonly TimeSpan Never = TimeSpan.FromHours(1);

    /// <summary>A bound a test of the go-on waits past.</summary>
    private static readonly TimeSpan Soon = TimeSpan.FromMilliseconds(300);

    /// <summary>The session as these tests run it: the go-on goes only after <paramref name="wake"/> with no wake.</summary>
    private static AcpSession Session(Agent agent, Record record, TimeSpan wake, TimeSpan? close = null) =>
        new(agent.Incoming, agent.Outgoing, _ => { }, closeTimeout: close, onEvent: record.Event, wakeQuiet: wake);

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

    /// <summary>The background command of the BGWAIT3 cases, as the spawn names it.</summary>
    private const string Census = "dotnet test --filter Census";

    /// <summary>The last words of the install's session in BGWAIT3's case, said after its command had ended unseen.</summary>
    private const string ClosesWhenItEnds =
        "Everything else is committed. The census is still building and running in the background. I'll close the quest when it finishes.";

    /// <summary>
    /// BGWAIT3: a background Bash as <c>claude-agent-acp</c> 0.84.0 says it (<c>async-tasks.js</c>, read): the spawn goes at the
    /// call's result, which the adapter reads for the task's id and its output file, and names the command, the file and the
    /// call; then the call's result, the task named in its text; then the agent's other calls, the last one completed.
    /// </summary>
    private static void BackgroundsTheCensus(Agent agent, string task = "b71")
    {
        agent.Push(Update("""{"sessionUpdate":"tool_call","toolCallId":"toolu_census","title":"Terminal","kind":"execute","status":"pending"}"""));
        agent.Push(Update($$"""{"sessionUpdate":"tool_call_update","toolCallId":"toolu_census","title":"{{Census}}","rawInput":{"command":"{{Census}}","run_in_background":true} }"""));
        agent.Push(Update($$"""{"sessionUpdate":"async_task_spawned","asyncTaskId":"{{task}}","name":"{{Census}}","taskType":"shell","description":"{{Census}}","showInTranscript":false,"canStop":true,"outputFilePath":"D:/fam/tasks/{{task}}.output","toolCallId":"toolu_census"}"""));
        var result = $"Command running in background with ID: {task}. Output is being written to: D:/fam/tasks/{task}.output. "
            + "You will be notified when it completes.";
        agent.Push(Update(
            """{"sessionUpdate":"tool_call_update","toolCallId":"toolu_census","status":"completed","content":[{"type":"content","content":{"type":"text","text":"""
            + JsonSerializer.Serialize(result)
            + """}}],"_meta":{"jetbrains":{"air":{"version":1,"asyncTasks":{"backgrounded":true}}}}}"""));
        LooksAtSomethingElse(agent);
    }

    /// <summary>One more call of the agent's own, completed: a step of its turn, after which its harness hands it what ended.</summary>
    private static void LooksAtSomethingElse(Agent agent, string call = "toolu_tsc")
    {
        agent.Push(Update($$"""{"sessionUpdate":"tool_call","toolCallId":"{{call}}","title":"npm run typecheck","kind":"execute","status":"pending"}"""));
        agent.Push(Update($$"""{"sessionUpdate":"tool_call_update","toolCallId":"{{call}}","status":"completed"}"""));
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
    /// A prompt after the first, which the install's adapter never answered (BGWAIT2): its echo took the woken turn, whose
    /// result is autonomous and settles nothing. Told to <paramref name="goOn"/>, and never answered here either.
    /// </summary>
    private static string? Unanswered(JsonElement frame, TaskCompletionSource<int> goOn)
    {
        goOn.TrySetResult(frame.GetProperty("id").GetInt32());
        return null;
    }

    /// <summary>Waits for the run, or for a prompt it should never have sent: the latter fails at once rather than at the patience.</summary>
    private static async Task<AcpOutcome> EndsWithoutGoOn(Task<AcpOutcome> run, Task<int> goOn)
    {
        await Task.WhenAny(run, goOn).WaitAsync(Poll.Patience);
        Assert.False(goOn.IsCompleted, "Daoris's own go-on went into the turn its harness woke");
        return await run;
    }

    /// <summary>
    /// 🔴 The owner's real case (2026-10-08): the agent backgrounded its gates, said it would pick up when they finish, and
    /// ended its turn holding its quest. The session is not closed at the turn's end, so the record stays working and
    /// nothing parks it on the person. 🔴 BGWAIT2 (2026-10-19, "one session just failed"): when the work ends the harness
    /// wakes its agent itself, and that turn's end, the result's autonomous <c>usage_update</c>, is the turn's end the run
    /// waits for. Daoris's go-on, sent into that turn, was never answered on the install, and the session waited 48 minutes
    /// for the timeout, which recorded it failed: here no second prompt goes, and one sent would never be answered.
    /// </summary>
    [Fact]
    public async Task A_turn_that_ends_on_its_own_background_work_keeps_working_and_ends_with_the_turn_its_harness_woke()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
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
                    return Unanswered(frame, goOn);
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var asked = 0;

        var run = Session(agent, record, Never)
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
            () => string.Join(" | ", record.Lines), Poll.Patience);
        await Task.Delay(300);
        Assert.False(run.IsCompleted);
        Assert.DoesNotContain("session/close", agent.Methods);
        Assert.Equal(1, Volatile.Read(ref asked));

        // The work ends; the harness wakes its agent itself, with no prompt open (CONSOLE2a), and says when its turn ended.
        GatesEnd(agent);
        agent.Push(Said("The gates passed."));
        WokenEnds(agent);
        var outcome = await EndsWithoutGoOn(run, goOn.Task);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/close"], agent.Methods);
        Assert.Equal(1, Volatile.Read(ref asked));
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
    /// 🔴 BGWAIT2d: the work the note names is what the wire announced, read before the quest's read, never after it. Work that ends
    /// while the quest is read is still the work the turn ended on, so its note names it, never "ended: " and an empty list.
    /// </summary>
    [Fact]
    public async Task Work_that_ends_while_the_quest_is_read_is_still_named_in_the_note_of_its_ending()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
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
                    return Unanswered(frame, goOn);
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();

        var run = Session(agent, record, Never)
            .RunAsync(
                "D:/fam/Game", "close quest #12", CancellationToken.None,
                waitsOnBackground: async _ =>
                {
                    // The quest's read is slow, and the work ends, and its woken turn ends, before the read answers.
                    GatesEnd(agent);
                    agent.Push(Said("The gates passed."));
                    WokenEnds(agent);
                    await Task.Delay(500);
                    return true;
                });

        var outcome = await EndsWithoutGoOn(run, goOn.Task);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Contains("note:— its background work ended: `Run the gates` (completed). The session goes on.", record.Lines);
    }

    /// <summary>
    /// 🔴 BGWAIT1b: the answered park's resumed run waits on its own background work as a start does. The agent offers
    /// <c>session/resume</c>, the resumed conversation's turn backgrounds the gates and ends, and the run waits for them and
    /// ends with the turn its harness woke (BGWAIT2), as a start's does.
    /// </summary>
    [Fact]
    public async Task A_resumed_run_waits_on_its_own_background_work_as_a_start_does()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, """{"protocolVersion":1,"agentCapabilities":{"sessionCapabilities":{"resume":{}}}}""");
                case "session/resume":
                    return Ok(frame, "{}");
                case "session/prompt" when self.Prompts.Count == 1:
                    BackgroundsTheGates(self);
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                case "session/prompt":
                    return Unanswered(frame, goOn);
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var asked = 0;

        var run = Session(agent, record, Never)
            .RunAsync(
                "D:/fam/Game", "Port 8080.", CancellationToken.None, resume: "s-1",
                waitsOnBackground: _ =>
                {
                    Interlocked.Increment(ref asked);
                    return Task.FromResult(true);
                });

        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal) && line.Contains("Run the gates")),
            () => string.Join(" | ", record.Lines), Poll.Patience);
        await Task.Delay(300);
        Assert.False(run.IsCompleted);
        Assert.Equal(1, Volatile.Read(ref asked));

        GatesEnd(agent);
        agent.Push(Said("The gates passed."));
        WokenEnds(agent);
        var outcome = await EndsWithoutGoOn(run, goOn.Task);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(["initialize", "session/resume", "session/prompt", "session/close"], agent.Methods);
        Assert.Equal(2, record.Lines.Count(line => line == "turn:end_turn"));
    }

    /// <summary>
    /// The go-on is the fallback (BGWAIT2): where the harness wakes nothing, no update of any kind for the bound after the work
    /// ended, Daoris's own prompt says the work ended, as a fresh turn the adapter answers, and its answer is the turn's end.
    /// </summary>
    [Fact]
    public async Task Where_the_harness_wakes_nothing_within_its_bound_the_go_on_goes_and_its_answer_ends_the_turn()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
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
                    return Unanswered(frame, goOn);
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();

        var run = Session(agent, record, Soon)
            .RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None, waitsOnBackground: _ => Task.FromResult(true));
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), Poll.Patience);

        var since = System.Diagnostics.Stopwatch.StartNew();
        GatesEnd(agent);
        var id = await goOn.Task.WaitAsync(Poll.Patience);
        Assert.True(since.Elapsed >= Soon, $"the go-on went {since.Elapsed.TotalMilliseconds:0} ms after the work ended");
        Assert.False(run.IsCompleted);

        agent.Push(Answer(id, "end_turn"));
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/prompt", "session/close"], agent.Methods);
        var resumed = agent.Prompts[1];
        Assert.Contains("background work", resumed);
        Assert.Contains("`Run the gates` (completed)", resumed);
        Assert.DoesNotContain("stopped", resumed);
    }

    /// <summary>
    /// 🔴 A woken turn whose end the wire never says keeps the session waiting, bounded by the session's timeout as before, and
    /// nothing is sent into it: silence is never read as its end. Not an end: no <c>usage_update</c> at all (the adapter sends
    /// none where it measured no usage, <c>acp-agent.js</c> l.3505), one under a subagent's session, an origin that is no
    /// object, or that names no kind, a prompted turn's origin, and a <c>usage_update</c> with no origin.
    /// </summary>
    [Theory]
    [InlineData("absent")]
    [InlineData("child")]
    [InlineData("text")]
    [InlineData("kindless")]
    [InlineData("user")]
    [InlineData("plain")]
    public async Task A_woken_turn_whose_end_is_not_said_keeps_the_session_waiting_and_nothing_goes_into_it(string end)
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
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
                    return Unanswered(frame, goOn);
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();

        var run = Session(agent, record, Soon)
            .RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None, waitsOnBackground: _ => Task.FromResult(true));
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), Poll.Patience);

        GatesEnd(agent);
        agent.Push(Said("The gates passed."));
        switch (end)
        {
            case "child":
                agent.Push(UpdateOf("s-child", WokenEnd()));
                break;
            case "text":
                agent.Push(Update("""{"sessionUpdate":"usage_update","used":61234,"size":200000,"_meta":{"_claude/origin":"task-notification"}}"""));
                break;
            case "kindless":
                agent.Push(Update("""{"sessionUpdate":"usage_update","used":61234,"size":200000,"_meta":{"_claude/origin":{"kind":7}}}"""));
                break;
            case "user":
                agent.Push(Update(WokenEnd("user")));
                break;
            case "plain":
                agent.Push(Update("""{"sessionUpdate":"usage_update","used":61234,"size":200000}"""));
                break;
        }

        await Task.Delay(Soon * 4);
        Assert.False(run.IsCompleted);
        Assert.False(goOn.Task.IsCompleted);
        Assert.Single(agent.Prompts);
        Assert.DoesNotContain("session/cancel", agent.Methods);

        // Its bound is the session's: the timeout ends the process, and the reader with it.
        agent.Close();
        var failed = await Assert.ThrowsAsync<DriverException>(() => run.WaitAsync(Poll.Patience));
        Assert.Contains("background work", failed.Message);
        Assert.Single(agent.Prompts);
    }

    /// <summary>
    /// A long call in the woken turn is that turn working, never quiet: the bound that sends the go-on runs only while no turn
    /// does, so the call is neither cut nor joined, and the turn's own end still ends the run.
    /// </summary>
    [Fact]
    public async Task A_long_call_in_the_woken_turn_is_never_cut_and_its_end_ends_the_run()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
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
                    return Unanswered(frame, goOn);
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();

        var run = Session(agent, record, Soon)
            .RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None, waitsOnBackground: _ => Task.FromResult(true));
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), Poll.Patience);

        GatesEnd(agent);
        agent.Push(Update("""{"sessionUpdate":"tool_call","toolCallId":"toolu_e2e","title":"npm run e2e","kind":"execute","status":"pending","rawInput":{"command":"npm run e2e"}}"""));
        await Task.Delay(Soon * 4);
        Assert.False(run.IsCompleted);
        Assert.False(goOn.Task.IsCompleted);
        Assert.DoesNotContain("session/cancel", agent.Methods);

        agent.Push(Update("""{"sessionUpdate":"tool_call_update","toolCallId":"toolu_e2e","status":"completed"}"""));
        agent.Push(Said("The end-to-end run passed."));
        WokenEnds(agent);
        var outcome = await EndsWithoutGoOn(run, goOn.Task);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/close"], agent.Methods);
    }

    /// <summary>
    /// Work the woken turn starts is waited for again, past the 250 ms the first ending is given: the turn's end goes back
    /// through the run's loop as an answered turn's does, the driver is asked again, and the second work's own woken turn
    /// ends the session. The first turn's repeated end never stands for the second's.
    /// </summary>
    [Fact]
    public async Task Work_the_woken_turn_starts_is_waited_for_again_and_its_own_woken_turn_ends_the_session()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
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
                    return Unanswered(frame, goOn);
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var asked = 0;

        var run = Session(agent, record, Never)
            .RunAsync(
                "D:/fam/Game", "close quest #12", CancellationToken.None,
                waitsOnBackground: _ =>
                {
                    Interlocked.Increment(ref asked);
                    return Task.FromResult(true);
                });
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), Poll.Patience);

        GatesEnd(agent);
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:— its background work ended", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), Poll.Patience);

        // Woken, the agent starts the build in the background, and its turn ends while the build runs.
        agent.Push(Said("The gates passed; building."));
        agent.Push(Update("""{"sessionUpdate":"tool_call","toolCallId":"toolu_b2","title":"npm run build","kind":"execute","status":"pending","rawInput":{"command":"npm run build","run_in_background":true}}"""));
        agent.Push(Update("""{"sessionUpdate":"async_task_spawned","asyncTaskId":"b2","name":"Build","taskType":"shell","canStop":true}"""));
        agent.Push(Update("""{"sessionUpdate":"tool_call_update","toolCallId":"toolu_b2","status":"completed"}"""));
        WokenEnds(agent);
        await Poll.Until(
            () => Volatile.Read(ref asked) == 2
                  && record.Lines.Count(line => line.StartsWith("note:— its turn ended while", StringComparison.Ordinal)) == 2,
            () => string.Join(" | ", record.Lines), Poll.Patience);
        await Task.Delay(300);
        Assert.False(run.IsCompleted);
        Assert.Contains(record.Lines, line => line.StartsWith("note:", StringComparison.Ordinal) && line.Contains("`Build`"));

        GatesEnd(agent, task: "b2");
        agent.Push(Said("The build passed."));
        WokenEnds(agent);
        var outcome = await EndsWithoutGoOn(run, goOn.Task);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(2, Volatile.Read(ref asked));
        Assert.Equal(3, record.Lines.Count(line => line == "turn:end_turn"));
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/close"], agent.Methods);
    }

    /// <summary>
    /// The person's words said around the woken turn's end go as they always did, once, and are answered: before it (the turn
    /// still runs), at it (answered in the breath the turn ends), and after it (while the session waits again, on work the woken
    /// turn started). Nothing of Daoris's own goes beside them, and nothing is cancelled.
    /// </summary>
    [Theory]
    [InlineData("before")]
    [InlineData("at")]
    [InlineData("after")]
    public async Task The_persons_words_said_around_the_woken_turns_end_go_once_and_are_answered(string when)
    {
        const string words = "skip the Angular Jest run";
        var taken = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
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
                case "session/prompt" when self.Prompts[^1] == words:
                    taken.TrySetResult(frame.GetProperty("id").GetInt32());
                    if (when == "at") WokenEnds(self);
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                case "session/prompt":
                    return Unanswered(frame, goOn);
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });
        var asked = 0;

        var run = Session(agent, record, Never)
            .RunAsync(
                "D:/fam/Game", "close quest #12", CancellationToken.None, inbox: inbox,
                waitsOnBackground: _ =>
                {
                    Interlocked.Increment(ref asked);
                    return Task.FromResult(true);
                });
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), Poll.Patience);

        GatesEnd(agent);
        if (when == "after")
        {
            // Past the 250 ms the ending is given, which would fold the build into the same wait.
            await Poll.Until(
                () => record.Lines.Any(line => line.StartsWith("note:— its background work ended", StringComparison.Ordinal)),
                () => string.Join(" | ", record.Lines), Poll.Patience);
            agent.Push(Said("Reading the gates' output."));
            agent.Push(Update("""{"sessionUpdate":"async_task_spawned","asyncTaskId":"b2","name":"Build","taskType":"shell","canStop":true}"""));
            WokenEnds(agent);
            await Poll.Until(() => Volatile.Read(ref asked) == 2, () => string.Join(" | ", record.Lines), Poll.Patience);
        }
        else
        {
            agent.Push(Said("Reading the gates' output."));
            await Poll.Until(
                () => record.Lines.Contains("message:Reading the gates' output."), () => string.Join(" | ", record.Lines), Poll.Patience);
        }

        Assert.True(inbox.Hold(new ChatMessage(words, [])));
        await taken.Task.WaitAsync(Poll.Patience);
        if (when == "before") WokenEnds(agent);
        if (when == "after")
        {
            await Task.Delay(300);
            Assert.False(run.IsCompleted);
            GatesEnd(agent, task: "b2");
            agent.Push(Said("The build passed."));
            WokenEnds(agent);
        }

        await EndsWithoutGoOn(run, goOn.Task);

        Assert.Equal(["close quest #12", words], agent.Prompts);
        Assert.DoesNotContain("session/cancel", agent.Methods);
        Assert.Equal("session/close", agent.Methods[^1]);
    }

    /// <summary>
    /// A woken turn that ends in the breath the agent's stream does ends the run as that turn: the wire said it ended before
    /// it said nothing more, so the run is not the door's failure.
    /// </summary>
    [Fact]
    public async Task A_woken_turn_that_ends_as_the_stream_does_ends_the_run_as_that_turn()
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

        var run = Session(agent, record, Never, close: TimeSpan.FromMilliseconds(200))
            .RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None, waitsOnBackground: _ => Task.FromResult(true));
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), Poll.Patience);

        GatesEnd(agent);
        agent.Push(Said("The gates passed."));
        WokenEnds(agent);
        agent.Close();
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Single(agent.Prompts);
    }

    /// <summary>
    /// 🔴 BGWAIT2c: work whose ending the wire said before its stream ended has ended, however the wait is woken. The reader
    /// hands the ending to the wait and reads on, so the stream can be over before the wait looks; the wait that then read the
    /// stream's end first failed a session whose work had ended, as the case above did now and then alone. Here the test's own
    /// thread is the reader, the ending and the stream's end one after the other, the wait parked before either: run many
    /// times, since which of the two the wait sees first is the thread pool's to say.
    /// </summary>
    [Fact]
    public async Task Work_whose_ending_was_said_before_the_stream_ended_has_ended_however_the_wait_is_woken()
    {
        var spawned = Element("""{"sessionUpdate":"async_task_spawned","asyncTaskId":"bdd847eoq","name":"Run the gates"}""");
        var stopped = Element("""{"sessionUpdate":"async_task_state_update","asyncTaskId":"bdd847eoq","state":"stopped"}""");
        var completed = Element("""{"sessionUpdate":"async_task_state_update","asyncTaskId":"bdd847eoq","state":"completed"}""");

        for (var run = 0; run < 200; run++)
        {
            var background = new AcpBackground();
            background.Take(spawned);
            var stream = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var waits = background.EndedAsync(stream.Task, CancellationToken.None);

            background.Take(stopped);
            background.Take(completed);
            stream.SetResult();

            var ended = await waits.WaitAsync(Poll.Patience);
            Assert.True(ended is not null, $"run {run}: the stream's end was read before the ending the wire said first");
            Assert.Equal([("Run the gates", "completed")], ended);
        }
    }

    private static JsonElement Element(string json) => JsonDocument.Parse(json).RootElement.Clone();

    /// <summary>The driver's shutdown ends the wait for the woken turn, as it ends the wait for the work, and sends nothing.</summary>
    [Fact]
    public async Task The_drivers_shutdown_ends_the_wait_for_the_woken_turn()
    {
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
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new Record();
        using var shutdown = new CancellationTokenSource();

        var run = Session(agent, record, Never)
            .RunAsync("D:/fam/Game", "close quest #12", shutdown.Token, waitsOnBackground: _ => Task.FromResult(true));
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), Poll.Patience);

        GatesEnd(agent);
        agent.Push(Said("The gates passed."));
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:— its background work ended", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), Poll.Patience);
        shutdown.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(Poll.Patience));
        Assert.Single(agent.Prompts);
    }

    /// <summary>
    /// 🔴 BGWAIT2: the timeout kills the agent's process, and its stream ends. Told to the door before the kill, that end is
    /// said as the timeout, by its own coded line, never as the agent having exited: the install's record said "the ACP
    /// agent's stream ended before it answered — the agent exited" of a session the timeout had killed.
    /// </summary>
    [Fact]
    public async Task A_stream_the_timeout_ends_while_the_session_waits_is_said_as_the_timeout()
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
        var session = Session(agent, record, Never);

        var run = session.RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None, waitsOnBackground: _ => Task.FromResult(true));
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), Poll.Patience);

        session.Ending(Observation.TimedOut(90));
        agent.Close();

        var ended = await Assert.ThrowsAsync<AcpEnded>(() => run.WaitAsync(Poll.Patience));
        Assert.Equal("timed out after 90 minutes and was killed.", ended.Message);
        Assert.Equal("ended.timeout", Assert.Single(ended.Why.Parts).Code);
    }

    /// <summary>
    /// The same for a turn the timeout cuts while its prompt is unanswered: the prompt's failure is the timeout's. Said through
    /// the driver's carrier (<see cref="AcpEnding"/>), which its wait tells before the kill and its capture hands the door,
    /// in either order.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_turn_the_timeout_cuts_is_said_as_the_timeout(bool saidFirst)
    {
        var prompted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    prompted.TrySetResult();
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var session = Session(agent, new Record(), Never);
        var ending = new AcpEnding();
        if (saidFirst) ending.Say(Observation.TimedOut(90));
        ending.Open(session);

        var run = session.RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None);
        await prompted.Task.WaitAsync(Poll.Patience);
        if (!saidFirst) ending.Say(Observation.TimedOut(90));
        // Said once: a second cause is not the one that ended it.
        ending.Say(Observation.Stopped);
        agent.Close();

        var ended = await Assert.ThrowsAsync<AcpEnded>(() => run.WaitAsync(Poll.Patience));
        Assert.Equal("ended.timeout", Assert.Single(ended.Why.Parts).Code);
    }

    /// <summary>
    /// A stream that ended before the timeout came keeps its own diagnosis: the agent exited, and a timeout said once the
    /// stream is over changes nothing it said. The other order of the same two events as the case above.
    /// </summary>
    [Fact]
    public async Task A_stream_that_ended_before_the_timeout_keeps_its_own_diagnosis()
    {
        var prompted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, Queues);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    prompted.TrySetResult();
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var session = Session(agent, new Record(), Never);

        var run = session.RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None);
        await prompted.Task.WaitAsync(Poll.Patience);
        agent.Close();
        await session.Ended.WaitAsync(Poll.Patience);
        session.Ending(Observation.TimedOut(90));

        var failed = await Assert.ThrowsAsync<DriverException>(() => run.WaitAsync(Poll.Patience));
        Assert.Contains("stream ended before it answered", failed.Message);
    }

    /// <summary>
    /// 🔴 A real question still parks: a turn that ends with no background work running, and none whose ending its agent was
    /// never told, ends the run as before, one prompt and the close, and nothing asks whether to wait. A task that ended inside
    /// the turn, before a step its agent took after it, is neither running nor owed a wake: the harness handed the ending to
    /// that step (BGWAIT3).
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
                    LooksAtSomethingElse(self);
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
            .WaitAsync(Poll.Patience);

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
            .WaitAsync(Poll.Patience);

        Assert.Equal(["initialize", "session/new", "session/prompt", "session/close"], agent.Methods);
        Assert.Equal(1, asked);
    }

    /// <summary>
    /// The agent of BGWAIT3's case: its first prompt backgrounds the census, takes another step, and the census ends after that
    /// step, while the agent writes its last words, which say it still runs; any later prompt is never answered.
    /// </summary>
    private static Agent EndsUnseen(TaskCompletionSource<int> goOn) => new((frame, self) =>
    {
        switch (frame.GetProperty("method").GetString())
        {
            case "initialize":
                return Ok(frame, Queues);
            case "session/new":
                return Ok(frame, """{"sessionId":"s-1"}""");
            case "session/prompt" when self.Prompts.Count == 1:
                BackgroundsTheCensus(self);
                GatesEnd(self, "b71");
                self.Push(Said(ClosesWhenItEnds));
                return Ok(frame, """{"stopReason":"end_turn"}""");
            case "session/prompt":
                return Unanswered(frame, goOn);
            default:
                return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
        }
    });

    /// <summary>
    /// 🔴 BGWAIT3, the owner's report (2026-10-21): a driven session on the install backgrounded a test class, and the command
    /// ended about three seconds before its turn did, after the agent's last step, while it wrote that the command still ran and
    /// that it would close its quest when it finished. Nothing was running at the turn's end, so the session closed and parked
    /// on the person (等你处理), five seconds before the harness would have woken its agent with the ending it was never handed.
    /// An ending said after the turn's last step is owed a wake: the session waits, working, for the turn its harness wakes, and
    /// that turn's end ends the run, with no go-on and no second ask of the quest.
    /// </summary>
    [Fact]
    public async Task A_turn_that_ends_after_its_own_work_ended_unseen_waits_for_the_turn_its_harness_wakes()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = EndsUnseen(goOn);
        var record = new Record();
        var asked = 0;

        var run = Session(agent, record, Never)
            .RunAsync(
                "D:/fam/Game", "close quest #12", CancellationToken.None,
                waitsOnBackground: _ =>
                {
                    Interlocked.Increment(ref asked);
                    return Task.FromResult(true);
                });

        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal) && line.Contains(Census)),
            () => string.Join(" | ", record.Lines), Poll.Patience);
        await Task.Delay(300);
        Assert.False(run.IsCompleted);
        Assert.DoesNotContain("session/close", agent.Methods);
        Assert.Equal(1, Volatile.Read(ref asked));

        // The harness wakes its agent with the ending, as it does for work that ends after the turn (CONSOLE2a), and its turn ends.
        agent.Push(Said("The census passed. Closing the quest."));
        WokenEnds(agent);
        var outcome = await EndsWithoutGoOn(run, goOn.Task);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/close"], agent.Methods);
        Assert.Equal(1, Volatile.Read(ref asked));
        Assert.Equal(
            [
                $"message:{ClosesWhenItEnds}",
                "turn:end_turn",
                $"note:— its background work ended: `{Census}` (completed). The session goes on.",
                "message:The census passed. Closing the quest.",
                "turn:end_turn",
            ],
            record.Lines);
    }

    /// <summary>
    /// BGWAIT3's fallback is BGWAIT2's: where no turn wakes within the bound after an ending the agent was never handed, Daoris's
    /// go-on tells it, as a fresh turn the adapter answers. That tells it the ending: the go-on's answer ends the run, and no
    /// second go-on follows it.
    /// </summary>
    [Fact]
    public async Task Where_no_turn_wakes_for_an_ending_its_agent_was_never_handed_the_go_on_tells_it_once()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = EndsUnseen(goOn);
        var record = new Record();
        var asked = 0;

        var run = Session(agent, record, Soon)
            .RunAsync(
                "D:/fam/Game", "close quest #12", CancellationToken.None,
                waitsOnBackground: _ =>
                {
                    Interlocked.Increment(ref asked);
                    return Task.FromResult(true);
                });

        var id = await goOn.Task.WaitAsync(Poll.Patience);
        Assert.Contains($"`{Census}` (completed)", agent.Prompts[1]);
        Assert.False(run.IsCompleted);

        agent.Push(Answer(id, "end_turn"));
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/prompt", "session/close"], agent.Methods);
        Assert.Equal(1, Volatile.Read(ref asked));
    }

    /// <summary>
    /// D105 stands for an ending the agent was never handed as it does for work still running: where the quest does not wait on
    /// it, the session ends at its turn's end, the driver asked once.
    /// </summary>
    [Fact]
    public async Task An_ending_its_agent_was_never_handed_ends_with_the_session_where_the_quest_does_not_wait()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = EndsUnseen(goOn);
        var record = new Record();
        var asked = 0;

        await Session(agent, record, Never)
            .RunAsync(
                "D:/fam/Game", "close quest #12", CancellationToken.None,
                waitsOnBackground: _ =>
                {
                    Interlocked.Increment(ref asked);
                    return Task.FromResult(false);
                })
            .WaitAsync(Poll.Patience);

        Assert.Equal(["initialize", "session/new", "session/prompt", "session/close"], agent.Methods);
        Assert.Equal(1, asked);
        Assert.DoesNotContain(record.Lines, line => line.StartsWith("note:", StringComparison.Ordinal));
    }

    /// <summary>How often the DONEEND1 tests' sessions read their quest again while they wait: many times in a breath.</summary>
    private static readonly TimeSpan Look = TimeSpan.FromMilliseconds(30);

    /// <summary>A session that reads its quest again every <see cref="Look"/> while it waits on its background work (DONEEND1).</summary>
    private static AcpSession Looking(Agent agent, Record record, TimeSpan wake) =>
        new(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event, wakeQuiet: wake) { BackgroundLook = Look };

    /// <summary>The agent of the owner's case: its first prompt backgrounds the gates, and any later prompt is never answered.</summary>
    private static Agent Gates(TaskCompletionSource<int> goOn, Action<Agent>? more = null) => new((frame, self) =>
    {
        switch (frame.GetProperty("method").GetString())
        {
            case "initialize":
                return Ok(frame, Queues);
            case "session/new":
                return Ok(frame, """{"sessionId":"s-1"}""");
            case "session/prompt" when self.Prompts.Count == 1:
                BackgroundsTheGates(self);
                more?.Invoke(self);
                return Ok(frame, """{"stopReason":"end_turn"}""");
            case "session/prompt":
                return Unanswered(frame, goOn);
            default:
                return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
        }
    });

    /// <summary>
    /// 🔴 DONEEND1 (2026-10-20): a session that waits on its own long test run while its quest closes done went on waiting until the
    /// timeout killed it, holding a slot and every count, because the quest was read once, at the turn's end. It is read again while
    /// the session waits, and once it no longer waits on the work (BGWAIT1's own condition, <c>Observation.Parks</c>), the run ends:
    /// the session closes, the work still running ends with it and is named, and nothing of Daoris's goes on.
    /// </summary>
    [Fact]
    public async Task A_quest_that_closes_while_the_session_waits_ends_the_run_and_its_work_with_it()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = Gates(goOn);
        var record = new Record();
        var closed = 0;
        var asked = 0;
        var session = Looking(agent, record, Never);

        var run = session.RunAsync(
            "X:/daoris/game", "close quest #12", CancellationToken.None,
            waitsOnBackground: _ =>
            {
                Interlocked.Increment(ref asked);
                return Task.FromResult(Volatile.Read(ref closed) == 0);
            });

        // The turn ended on the work, and the quest still waits on it: read again and again, the session still waits.
        await Poll.Until(() => Volatile.Read(ref asked) >= 4, () => string.Join(" | ", record.Lines), Poll.Patience);
        Assert.False(run.IsCompleted);
        Assert.DoesNotContain("session/close", agent.Methods);

        // The quest closes done while the work runs: the next look ends the wait, and the run.
        Volatile.Write(ref closed, 1);
        var outcome = await EndsWithoutGoOn(run, goOn.Task);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/close"], agent.Methods);
        Assert.Equal(["Run the gates"], session.LeftRunning);
        Assert.Equal(
            [
                $"message:{Waiting}",
                "turn:end_turn",
                "note:— its turn ended while its own background work runs: `Run the gates`. The session waits for that work, "
                + "and goes on when it ends.",
                "note:— its quest no longer waits on its background work: the session ends, and the work still running ends with "
                + "it: `Run the gates`.",
            ],
            record.Lines);
    }

    /// <summary>
    /// DONEEND1: a turn its harness woke for one piece of work, which closes the quest while another piece still runs, is the
    /// agent's own turn, never cut: the run ends at that turn's said end, the work still running ending with it. The looks end
    /// with the wait.
    /// </summary>
    [Fact]
    public async Task A_woken_turn_running_as_the_quest_closes_is_never_cut_and_its_end_ends_the_run()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = Gates(goOn, self => self.Push(Update(
            """{"sessionUpdate":"async_task_spawned","asyncTaskId":"b2","name":"Build","taskType":"shell","canStop":true}""")));
        var record = new Record();
        var closed = 0;
        var asked = 0;
        var session = Looking(agent, record, Never);

        var run = session.RunAsync(
            "X:/daoris/game", "close quest #12", CancellationToken.None,
            waitsOnBackground: _ =>
            {
                Interlocked.Increment(ref asked);
                return Task.FromResult(Volatile.Read(ref closed) == 0);
            });
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), Poll.Patience);

        // The gates end while the build runs; the harness wakes its agent, which closes its quest in that turn.
        GatesEnd(agent);
        agent.Push(Said("The gates passed; the quest is done."));
        // The woken turn read before the quest reads closed: a look between the push and the read saw no turn running and ended the
        // run (the full set on 5e8caed81).
        await Poll.Until(
            () => record.Lines.Any(line => line.Contains("The gates passed", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), Poll.Patience);
        var looked = Volatile.Read(ref asked);
        Volatile.Write(ref closed, 1);
        // A look reads it closed; the session then waits on the turn alone, many looks' time.
        await Poll.Until(() => Volatile.Read(ref asked) > looked, () => string.Join(" | ", record.Lines), Poll.Patience);
        await Task.Delay(Look * 10);
        Assert.False(run.IsCompleted);
        Assert.DoesNotContain("session/close", agent.Methods);

        // The woken turn says its end: the run ends there, the build still running.
        WokenEnds(agent);
        await EndsWithoutGoOn(run, goOn.Task);

        Assert.Equal(["initialize", "session/new", "session/prompt", "session/close"], agent.Methods);
        Assert.Equal(["Build"], session.LeftRunning);
        Assert.Equal(
            "note:— its quest no longer waits on its background work: the session ends, and the work still running ends with it: "
            + "`Build`.",
            record.Lines[^1]);
        var after = Volatile.Read(ref asked);
        await Task.Delay(Look * 10);
        Assert.Equal(after, Volatile.Read(ref asked));
    }

    /// <summary>
    /// DONEEND1: a look that cannot be read says nothing, so the session goes on waiting and looks again; silence is never read
    /// as the quest's close. The look that reads it closed ends the run.
    /// </summary>
    [Fact]
    public async Task A_look_that_cannot_be_read_keeps_the_session_waiting()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = Gates(goOn);
        var record = new Record();
        var closed = 0;
        var asked = 0;

        var run = Looking(agent, record, Never).RunAsync(
            "X:/daoris/game", "close quest #12", CancellationToken.None,
            waitsOnBackground: _ =>
            {
                if (Interlocked.Increment(ref asked) == 1) return Task.FromResult(true);
                return Volatile.Read(ref closed) == 0
                    ? Task.FromException<bool>(new HttpRequestException("the service did not answer"))
                    : Task.FromResult(false);
            });

        await Poll.Until(() => Volatile.Read(ref asked) >= 5, () => string.Join(" | ", record.Lines), Poll.Patience);
        Assert.False(run.IsCompleted);
        Assert.Single(record.Lines, line => line.StartsWith("note:", StringComparison.Ordinal));

        Volatile.Write(ref closed, 1);
        await EndsWithoutGoOn(run, goOn.Task);
        Assert.StartsWith("note:— its quest no longer waits on its background work", record.Lines[^1], StringComparison.Ordinal);
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
            () => string.Join(" | ", record.Lines), Poll.Patience);
        agent.Close();

        var failed = await Assert.ThrowsAsync<DriverException>(() => run.WaitAsync(Poll.Patience));
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

        // Nothing wakes the agent here, so the go-on goes once the bound has passed (BGWAIT2).
        var run = Session(agent, record, Soon)
            .RunAsync(
                "D:/fam/Game", "close quest #12", CancellationToken.None, inbox: inbox, waitsOnBackground: _ => Task.FromResult(true));
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), Poll.Patience);

        Assert.True(inbox.Hold(new ChatMessage("skip the Angular Jest run", [])));
        await words.Task.WaitAsync(Poll.Patience);
        await Task.Delay(300);
        Assert.False(run.IsCompleted);
        Assert.False(ended.Task.IsCompleted);

        GatesEnd(agent);
        await ended.Task.WaitAsync(Poll.Patience);
        await run.WaitAsync(Poll.Patience);

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

        var run = Session(agent, new Record(), Soon)
            .RunAsync(
                "D:/fam/Game", "close quest #12", CancellationToken.None, inbox: inbox, waitsOnBackground: _ => Task.FromResult(true));
        var id = await first.Task.WaitAsync(Poll.Patience);
        Assert.True(inbox.Hold(new ChatMessage("skip the Angular Jest run", [])));
        agent.Push(Answer(id, "end_turn"));

        await Poll.Until(() => agent.Prompts.Count == 2, () => string.Join(" | ", agent.Prompts), Poll.Patience);
        Assert.Equal("skip the Angular Jest run", agent.Prompts[1]);
        await Task.Delay(300);
        Assert.False(run.IsCompleted);

        GatesEnd(agent);
        await run.WaitAsync(Poll.Patience);

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

        var run = Session(agent, record, Soon)
            .RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None, waitsOnBackground: _ => Task.FromResult(true));
        await Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal)),
            () => string.Join(" | ", record.Lines), Poll.Patience);

        // Woken by the first ending, the agent starts the build in the background before the first is said.
        agent.Push(Update("""{"sessionUpdate":"async_task_spawned","asyncTaskId":"b2","name":"Build","taskType":"shell","canStop":true}"""));
        GatesEnd(agent, state: "failed");
        await Task.Delay(500);
        Assert.False(ended.Task.IsCompleted);

        GatesEnd(agent, task: "b2");
        await ended.Task.WaitAsync(Poll.Patience);
        await run.WaitAsync(Poll.Patience);

        var resumed = agent.Prompts[1];
        Assert.Contains("`Run the gates` (failed)", resumed);
        Assert.Contains("`Build` (completed)", resumed);
    }
}
