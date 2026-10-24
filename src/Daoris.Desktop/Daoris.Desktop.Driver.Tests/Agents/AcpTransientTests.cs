using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTERRORS1a (D210, the agent-errors design §5.1): a transient failure tried again inside <see cref="AcpSession.RunAsync"/>, on the
/// same session, after 1, 2, 4, 8 and 16 minutes on a clock the test moves (<c>.claude/knowledge/held-clocks.md</c>), each try one prompt
/// of the person's held words and then Daoris's go-on, never a word on the wire again. Against a fake agent over in-memory streams that
/// answers its prompts with <c>claude-agent-acp</c>'s 529 frame, source-derived (§3.3, <c>acp-agent.js:4178-4180</c>): no process and no
/// model, so the fast half.
/// </summary>
public sealed class AcpTransientTests
{
    private const string Overloaded =
        "Internal error: API Error: 529 Overloaded. This is a server-side issue, usually temporary — try again in a moment. If it persists, check https://status.claude.com.";

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

        /// <summary>Each prompt's text blocks, in the order sent.</summary>
        public IReadOnlyList<string[]> Prompts
        {
            get
            {
                lock (Sent)
                {
                    return [.. Sent.Select(line => JsonDocument.Parse(line).RootElement)
                        .Where(frame => frame.TryGetProperty("method", out var m) && m.GetString() == "session/prompt")
                        .Select(frame => frame.GetProperty("params").GetProperty("prompt").EnumerateArray()
                            .Where(block => block.GetProperty("type").GetString() == "text")
                            .Select(block => block.GetProperty("text").GetString()!).ToArray())];
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

    private static string Answer(JsonElement request, string stopReason) => Ok(request, $$"""{"stopReason":"{{stopReason}}"}""");

    /// <summary>The 529 as <c>claude-agent-acp</c> 0.84.0 answers a prompt with it: <c>-32603</c>, <c>data.errorKind</c> (source-derived).</summary>
    private static string Failed(JsonElement request, string kind = "overloaded", string message = Overloaded) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = request.GetProperty("id").GetInt32(),
            error = new { code = -32603, message, data = new { errorKind = kind } },
        });

    private static string Said(string text) =>
        """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"""
        + JsonSerializer.Serialize(text) + "}}}}";

    private const string Silent = """{"protocolVersion":1,"agentCapabilities":{}}""";

    /// <summary>The agent: it opens <c>s-1</c>, and answers its n-th prompt by <paramref name="prompt"/>, counted from one.</summary>
    private static Agent Scripted(Func<int, JsonElement, Agent, string?> prompt)
    {
        var count = 0;
        return new Agent((frame, self) =>
        {
            switch (frame.TryGetProperty("method", out var m) ? m.GetString() : null)
            {
                case "initialize":
                    return Ok(frame, Silent);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    return prompt(Interlocked.Increment(ref count), frame, self);
                default:
                    return frame.TryGetProperty("id", out _) && frame.TryGetProperty("method", out _) ? Ok(frame, "{}") : null;
            }
        });
    }

    /// <summary>What the retry told, in order: the line once, each try with its next, each wait's start and end.</summary>
    private sealed class Told
    {
        private readonly List<string> _lines = [];

        public IReadOnlyList<string> Lines
        {
            get { lock (_lines) return [.. _lines]; }
        }

        public void Add(string line)
        {
            lock (_lines) _lines.Add(line);
        }

        public int Count(string line)
        {
            lock (_lines) return _lines.Count(each => each == line);
        }
    }

    private static readonly AdapterSet Built = AdapterSet.Built();

    private static TransientRetry Retry(MovedClock clock, Told told, Func<CancellationToken, Task<bool>>? wanted = null) => new()
    {
        Read = refused => AgentErrors.Read(
            Built, Built.Resolve("claude-code-acp"), DoorFailure.OfPrompt(refused), clock.GetUtcNow(), TimeZoneInfo.Utc),
        Clock = clock,
        Wanted = wanted,
        Began = (_, next) => told.Add($"began:{(next - clock.GetUtcNow()).TotalMinutes:0}"),
        Tried = (_, number, next) => told.Add($"try:{number}:{(next is { } at ? (at - clock.GetUtcNow()).TotalMinutes.ToString("0") : "last")}"),
        Waits = waiting => told.Add(waiting ? "wait" : "waited"),
    };

    /// <summary>Moves the held clock past each wait the run makes, once it waits: <paramref name="minutes"/> one wait at a time.</summary>
    private static async Task PassAsync(Told told, MovedClock clock, Task run, params int[] minutes)
    {
        for (var at = 0; at < minutes.Length; at++)
        {
            var waits = at + 1;
            await Poll.Until(() => told.Count("wait") >= waits || run.IsCompleted, () => "the run never waited to try again", Poll.Patience);
            Assert.False(run.IsCompleted, "the run ended where it should have waited");
            clock.Advance(TimeSpan.FromMinutes(minutes[at]));
            await Poll.Until(() => told.Count("waited") >= waits || run.IsCompleted, () => "the wait never ended", Poll.Patience);
        }
    }

    /// <summary>
    /// 🔴 Shape 1 (the design §0): the prompt fails with the 529 twice, then the agent works. The run tries again in the SAME session,
    /// after 1 and then 2 minutes on the held clock, each try the go-on alone, and ends as its work does; the line said once.
    /// </summary>
    [Fact]
    public async Task A_prompt_failing_transient_twice_is_tried_again_in_the_same_session_after_one_then_two_minutes()
    {
        var clock = new MovedClock();
        var told = new Told();
        var agent = Scripted((n, frame, self) => n <= 2 ? Failed(frame) : Answer(frame, "end_turn"));
        var retry = Retry(clock, told);
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });

        var run = session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None, retry: retry);
        await PassAsync(told, clock, run, 1, 2);
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/prompt", "session/prompt", "session/close"], agent.Methods);
        var goOn = TransientRetry.GoOnText(Overloaded);
        Assert.Equal([["serve the report"], [goOn], [goOn]], agent.Prompts);
        Assert.Equal(["began:1", "wait", "waited", "try:1:2", "wait", "waited", "try:2:4"], told.Lines);
        Assert.Equal((2, TimeSpan.FromMinutes(3), false), (retry.Episode!.Tries, retry.Episode.Waited, retry.Episode.Exhausted));
    }

    /// <summary>A run handed no retry keeps today's end: the refusal ends it (§5.1, round 3, R2-O6).</summary>
    [Fact]
    public async Task A_run_handed_no_retry_keeps_today_s_end()
    {
        var agent = Scripted((_, frame, _) => Failed(frame));
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });

        var refused = await Assert.ThrowsAsync<AcpRefusal>(() => session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None));
        Assert.Equal("overloaded", refused.ErrorKind);
    }

    /// <summary>The same agent erring every try: past the bound after five tries and 31 minutes of waits, the run ends exhausted.</summary>
    [Fact]
    public async Task Erring_every_try_ends_past_the_bound_after_five_tries_and_31_minutes_of_waits()
    {
        var clock = new MovedClock();
        var told = new Told();
        var agent = Scripted((_, frame, _) => Failed(frame));
        var retry = Retry(clock, told);
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });

        var run = session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None, retry: retry);
        await PassAsync(told, clock, run, 1, 2, 4, 8, 16);
        var exhausted = await Assert.ThrowsAsync<AcpTransientExhausted>(() => run.WaitAsync(Poll.Patience));

        Assert.Equal((5, TimeSpan.FromMinutes(31), true), (exhausted.Episode.Tries, exhausted.Episode.Waited, exhausted.Episode.Exhausted));
        Assert.Equal(6, agent.Prompts.Count);
        Assert.Equal("try:5:last", told.Lines[^1]);
        Assert.Single(told.Lines, line => line.StartsWith("began:", StringComparison.Ordinal));
    }

    /// <summary>
    /// A word said during a wait ends it at once and goes as the next try, first, in one prompt with the go-on after it, and counts as
    /// one; the wait after it is the schedule's next, and the minutes waited are those actually waited.
    /// </summary>
    [Fact]
    public async Task A_word_said_during_a_wait_goes_at_once_in_one_prompt_with_the_go_on_after_it()
    {
        var clock = new MovedClock();
        var told = new Told();
        var agent = Scripted((n, frame, self) =>
        {
            if (n == 1) return Failed(frame);
            self.Push(Said("Using 9090."));
            return Answer(frame, "end_turn");
        });
        var retry = Retry(clock, told);
        var inbox = new DrivenInbox(_ => { });
        var taken = new List<string>();
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });

        var run = session.RunAsync(
            "D:/fam/Game", "serve the report", CancellationToken.None, inbox: inbox, asked: words => { lock (taken) taken.Add(words.Text); }, retry: retry);
        await Poll.Until(() => session.WaitsOnTransient, () => "the run never waited", Poll.Patience);
        Assert.True(inbox.Hold(new ChatMessage("use port 9090", [])));
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal([["serve the report"], ["use port 9090", TransientRetry.GoOnText(Overloaded)]], agent.Prompts);
        Assert.Equal(["use port 9090"], taken);
        Assert.Equal((1, TimeSpan.Zero), (retry.Episode!.Tries, retry.Episode.Waited));
    }

    /// <summary>The owner's check (§5.5): a quest done before the try sends no try, and the run closes as at a turn's end.</summary>
    [Fact]
    public async Task No_try_goes_where_the_owner_s_check_says_no_more_turn_is_wanted()
    {
        var clock = new MovedClock();
        var agent = Scripted((_, frame, _) => Failed(frame));
        var told = new Told();
        var retry = Retry(clock, told, wanted: _ => Task.FromResult(false));
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });

        var run = session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None, retry: retry);
        await PassAsync(told, clock, run, 1);
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Single(agent.Prompts);
        Assert.Equal(0, retry.Episode!.Tries);
    }

    /// <summary>An unreadable check answers yes (§5.1, round 3, R2-O6): a try costs a turn, and an unread look must not end the episode.</summary>
    [Fact]
    public async Task An_unreadable_check_answers_yes()
    {
        var clock = new MovedClock();
        var agent = Scripted((n, frame, _) => n == 1 ? Failed(frame) : Answer(frame, "end_turn"));
        var told = new Told();
        var retry = Retry(clock, told, wanted: _ => throw new HttpRequestException("the service did not answer"));
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });

        var run = session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None, retry: retry);
        await PassAsync(told, clock, run, 1);
        await run.WaitAsync(Poll.Patience);

        Assert.Equal(2, agent.Prompts.Count);
    }

    /// <summary>A limit on a try leaves the episode as a limit, once (§5.1, §6.1): its refusal ends the run, the reading kept for the conclusion.</summary>
    [Fact]
    public async Task A_limit_on_a_try_leaves_the_episode_as_a_limit()
    {
        var clock = new MovedClock();
        var agent = Scripted((n, frame, _) => n == 1 ? Failed(frame) : Failed(frame, "rate_limit", "Internal error"));
        var told = new Told();
        var retry = Retry(clock, told);
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });

        var run = session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None, retry: retry);
        await PassAsync(told, clock, run, 1);
        var refused = await Assert.ThrowsAsync<AcpRefusal>(() => run.WaitAsync(Poll.Patience));

        Assert.Equal("rate_limit", refused.ErrorKind);
        Assert.Equal((AgentErrorKind.Limit, "owner:kind:rate_limit"), (retry.Last!.Kind, retry.Last.By));
        Assert.Equal(2, agent.Prompts.Count);
    }

    /// <summary>A weekly limit's sentence under <c>rate_limit</c> is limited and never retried: no episode opens.</summary>
    [Fact]
    public async Task A_weekly_limit_under_rate_limit_is_never_retried()
    {
        var agent = Scripted((_, frame, _) => Failed(frame, "rate_limit", "Internal error: You've hit your weekly limit · resets Oct 26, 10pm (UTC)"));
        var retry = Retry(new MovedClock(), new Told());
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });

        await Assert.ThrowsAsync<AcpRefusal>(() => session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None, retry: retry));

        Assert.False(retry.Opened);
        Assert.Equal(AgentErrorKind.Limit, retry.Last!.Kind);
        Assert.Single(agent.Prompts);
    }

    /// <summary>
    /// 🔴 The wake gate (§5.1, round 3's read, R3-O1): a turn its harness wakes during a wait ends the wait, never the episode. Nothing is
    /// prompted into it, and the go-on goes once, after it has ended.
    /// </summary>
    [Fact]
    public async Task A_woken_turn_during_a_wait_ends_the_wait_and_the_go_on_follows_once_after_it_ends()
    {
        var clock = new MovedClock();
        var agent = Scripted((n, frame, _) => n == 1 ? Failed(frame) : Answer(frame, "end_turn"));
        var retry = Retry(clock, new Told());
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });

        var run = session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None, retry: retry);
        await Poll.Until(() => session.WaitsOnTransient, () => "the run never waited", Poll.Patience);
        // The harness wakes its agent with no prompt open: its turn runs, and no prompt goes into it.
        agent.Push(Said("The build you started has finished."));
        await Poll.Until(() => !session.WaitsOnTransient, () => "the woken turn never ended the wait", Poll.Patience);
        await Task.Delay(200);
        Assert.Single(agent.Prompts);

        // Its end, as claude-agent-acp says one: the autonomous result's usage_update.
        agent.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"usage_update","used":10,"size":100,"_meta":{"_claude/origin":{"kind":"task-notification"}}}}}""");
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal([["serve the report"], [TransientRetry.GoOnText(Overloaded)]], agent.Prompts);
        Assert.Equal(1, retry.Episode!.Tries);
    }

    /// <summary>
    /// 🔴 Nonacceptance (§5.1, round 3's read, R3-O2): a held word whose prompt meets the error before any update of its turn may or may
    /// not be in the agent's conversation. It is never sent again, and the <c>lost</c> note is said once under its id.
    /// </summary>
    [Fact]
    public async Task A_word_whose_prompt_met_the_error_before_any_update_is_never_sent_again_and_said_lost_once()
    {
        var clock = new MovedClock();
        var events = new List<SessionEvent>();
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = Scripted((n, frame, self) =>
        {
            if (n == 1)
            {
                _ = Task.Run(async () =>
                {
                    await go.Task;
                    self.Push(Answer(frame, "end_turn"));
                });
                return null;
            }

            return n == 2 ? Failed(frame) : Answer(frame, "end_turn");
        });
        var told = new Told();
        var retry = Retry(clock, told);
        var inbox = new DrivenInbox(_ => { });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: e => { lock (events) events.Add(e); });

        var run = session.RunAsync("D:/fam/Game", "serve the report", CancellationToken.None, inbox: inbox, retry: retry);
        await Poll.Until(() => agent.Prompts.Count == 1, () => "the first prompt never went", Poll.Patience);
        Assert.True(inbox.Hold(new ChatMessage("also log the port", []) { Level = MessageLevel.Ordinary }));
        go.SetResult();
        await PassAsync(told, clock, run, 1);
        await run.WaitAsync(Poll.Patience);

        Assert.Equal([["serve the report"], ["also log the port"], [TransientRetry.GoOnText(Overloaded)]], agent.Prompts);
        List<SessionEvent> lost;
        lock (events) lost = [.. events.Where(e => e.Code == SessionEventCodes.Lost)];
        Assert.Equal(["said-1"], Assert.Single(lost).Words);
        Assert.Equal(["also log the port"], retry.Uncertain.Select(word => word.Text));
    }

    /// <summary>The person's stop or Daoris's shutdown during a wait ends the run as that stop does today: the wait is cancelled with it.</summary>
    [Fact]
    public async Task A_shutdown_during_a_wait_cancels_it_with_the_run()
    {
        var clock = new MovedClock();
        var agent = Scripted((_, frame, _) => Failed(frame));
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });
        using var shutdown = new CancellationTokenSource();

        var run = session.RunAsync("D:/fam/Game", "serve the report", shutdown.Token, retry: Retry(clock, new Told()));
        await Poll.Until(() => session.WaitsOnTransient, () => "the run never waited", Poll.Patience);
        await shutdown.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(Poll.Patience));
        Assert.Single(agent.Prompts);
        Assert.False(session.WaitsOnTransient);
    }
}
