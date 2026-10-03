using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The protocol door's resume (ANSWER1a, D131 §1), against a fake agent over in-memory streams: <c>session/resume</c>
/// where the agent advertises it, <c>session/load</c> with its replay not kept again where it advertises only that, and
/// a refusal by code where it offers neither or refuses. Read from <c>claude-agent-acp</c> 0.84.0 and the ACP SDK 1.5.1 it
/// pins (design §0). No process and no model, so the fast half.
/// </summary>
public sealed class AcpResumeTests
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

        public JsonElement Params(string method)
        {
            lock (Sent)
            {
                return Sent.Select(line => JsonDocument.Parse(line).RootElement)
                    .First(frame => frame.TryGetProperty("method", out var m) && m.GetString() == method)
                    .GetProperty("params").Clone();
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

    private static string Error(JsonElement request, int code, string message) =>
        $$$"""{"jsonrpc":"2.0","id":{{{request.GetProperty("id").GetRawText()}}},"error":{"code":{{{code}}},"message":"{{{message}}}"}}""";

    private static string Said(string session, string text) =>
        """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":""" + JsonSerializer.Serialize(session)
        + ""","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":""" + JsonSerializer.Serialize(text)
        + "}}}}";

    /// <summary>What the adapter advertises at 0.84.0: <c>loadSession</c>, and <c>resume</c> among its session capabilities.</summary>
    private const string Both = """{"protocolVersion":1,"agentCapabilities":{"loadSession":true,"sessionCapabilities":{"resume":{},"close":{}}}}""";

    private const string LoadOnly = """{"protocolVersion":1,"agentCapabilities":{"loadSession":true}}""";

    private const string Neither = """{"protocolVersion":1,"agentCapabilities":{"sessionCapabilities":{"close":{}}}}""";

    private static Agent Answering(string initialize, Func<JsonElement, Agent, string?>? resume = null) => new((frame, self) =>
    {
        switch (frame.TryGetProperty("method", out var m) ? m.GetString() : null)
        {
            case "initialize":
                return Ok(frame, initialize);
            case "session/resume" or "session/load":
                return resume?.Invoke(frame, self) ?? Ok(frame, "{}");
            case "session/prompt":
                var on = frame.GetProperty("params").GetProperty("sessionId").GetString()!;
                self.Push(Said(on, "carrying on with port 8080"));
                return Ok(frame, """{"stopReason":"end_turn"}""");
            default:
                return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
        }
    });

    private static readonly IReadOnlyList<AcpMcpServer> Servers =
        [new("daoris", "D:/bin/daoris-mcp.exe", ["--stdio"], new Dictionary<string, string> { ["DAORIS_HOME"] = "D:/daoris-data" })];

    /// <summary>
    /// 🔴 The resume call: <c>session/resume</c> on the kept conversation, in the same tree, handed the servers and the rules
    /// a new session would be, and the answer as the next prompt of that same conversation. No <c>session/new</c>.
    /// </summary>
    [Fact]
    public async Task An_agent_that_resumes_is_sent_session_resume_and_the_answer_as_its_next_prompt()
    {
        var agent = Answering(Both);
        var lines = new List<string>();

        var outcome = await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add, meta: new { claudeCode = new { options = new { settings = "D:/daoris-data/s.json" } } })
            .RunAsync("D:/trees/s-1", "Port 8080.", CancellationToken.None, Servers, resume: "0b5e7c1a");

        Assert.Equal(["initialize", "session/resume", "session/prompt", "session/close"], agent.Methods);
        var resumed = agent.Params("session/resume");
        Assert.Equal("0b5e7c1a", resumed.GetProperty("sessionId").GetString());
        Assert.Equal("D:/trees/s-1", resumed.GetProperty("cwd").GetString());
        Assert.Equal("daoris", resumed.GetProperty("mcpServers")[0].GetProperty("name").GetString());
        Assert.Equal("D:/daoris-data/s.json", resumed.GetProperty("_meta").GetProperty("claudeCode").GetProperty("options").GetProperty("settings").GetString());
        var prompt = agent.Params("session/prompt");
        Assert.Equal("0b5e7c1a", prompt.GetProperty("sessionId").GetString());
        Assert.Equal("Port 8080.", prompt.GetProperty("prompt")[0].GetProperty("text").GetString());
        Assert.Equal("0b5e7c1a", outcome.SessionId);
        Assert.Contains(lines, line => line.Contains("carrying on with port 8080"));
    }

    /// <summary>
    /// <c>session/load</c> where the agent advertises only that: its replay of the history, sent before it answers, is not
    /// kept a second time — the record already holds it — and what the agent says after is.
    /// </summary>
    [Fact]
    public async Task An_agent_that_only_loads_is_sent_session_load_and_its_replay_is_not_kept_again()
    {
        var agent = Answering(LoadOnly, (frame, self) =>
        {
            self.Push(Said("0b5e7c1a", "an old line from the replay"));
            return Ok(frame, "{}");
        });
        var events = new List<SessionEvent>();
        var lines = new List<string>();

        await new AcpSession(agent.Incoming, agent.Outgoing, lines.Add, onEvent: events.Add)
            .RunAsync("D:/trees/s-1", "Port 8080.", CancellationToken.None, resume: "0b5e7c1a");

        Assert.Equal(["initialize", "session/load", "session/prompt", "session/close"], agent.Methods);
        Assert.DoesNotContain(events, e => e.Text?.Contains("an old line") == true);
        Assert.DoesNotContain(lines, line => line.Contains("an old line"));
        Assert.Contains(events, e => e.Kind == SessionEventKind.Message && e.Text == "carrying on with port 8080");
    }

    /// <summary>An agent that advertises neither is not asked to resume, nor prompted: the answer falls back, saying so.</summary>
    [Fact]
    public async Task An_agent_that_offers_no_resume_is_refused_before_anything_is_sent()
    {
        var agent = Answering(Neither);

        var refused = await Assert.ThrowsAsync<AcpResumeRefused>(() => new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/trees/s-1", "Port 8080.", CancellationToken.None, resume: "0b5e7c1a"));

        Assert.Equal(ContinueWhy.Offered, refused.Why.Code);
        Assert.Equal(["initialize"], agent.Methods);
    }

    /// <summary>
    /// The adapter refuses a conversation it no longer has with <c>resource_not_found</c> (-32002, design §0): it is gone,
    /// and the agent's own words are kept beside the code, never in the line a note carries.
    /// </summary>
    [Fact]
    public async Task A_conversation_the_agent_no_longer_has_is_refused_as_gone_with_its_words_kept()
    {
        var agent = Answering(Both, (frame, _) => Error(frame, -32002, "Resource not found: 0b5e7c1a"));

        var refused = await Assert.ThrowsAsync<AcpResumeRefused>(() => new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/trees/s-1", "Port 8080.", CancellationToken.None, resume: "0b5e7c1a"));

        Assert.Equal(ContinueWhy.Gone, refused.Why.Code);
        Assert.Equal("Resource not found: 0b5e7c1a", refused.Words);
        Assert.DoesNotContain("session/prompt", agent.Methods);
    }

    [Fact]
    public async Task Any_other_refusal_of_the_resume_is_refused_as_refused()
    {
        var agent = Answering(Both, (frame, _) => Error(frame, -32603, "Internal error: query closed"));

        var refused = await Assert.ThrowsAsync<AcpResumeRefused>(() => new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/trees/s-1", "Port 8080.", CancellationToken.None, resume: "0b5e7c1a"));

        Assert.Equal(ContinueWhy.Refused, refused.Why.Code);
        Assert.Equal("Internal error: query closed", refused.Words);
    }

    /// <summary>
    /// 🔴 <c>codex-acp</c> refuses a thread another Codex client holds with <c>invalidRequest</c> and
    /// <c>data.reason: "thread_active_writer"</c> (D137 §1.1): read from that structured data, it is <c>elsewhere</c>, and the
    /// agent's own sentence stays beside it, never in the line.
    /// </summary>
    [Fact]
    public async Task A_conversation_another_client_holds_is_refused_as_elsewhere()
    {
        var agent = Answering(Both, (frame, _) =>
            """{"jsonrpc":"2.0","id":""" + frame.GetProperty("id").GetRawText()
            + ""","error":{"code":-32600,"message":"This Codex session is in use by another Codex client (the Codex app, the CLI or an IDE extension).","data":{"reason":"thread_active_writer"}}}""");

        var refused = await Assert.ThrowsAsync<AcpResumeRefused>(() => new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/trees/s-1", "Also log the port.", CancellationToken.None, resume: "thread-1"));

        Assert.Equal(ContinueWhy.Elsewhere, refused.Why.Code);
        Assert.Contains("in use by another Codex client", refused.Words);
        Assert.DoesNotContain("session/prompt", agent.Methods);
    }

    /// <summary>The same sentence with no structured reason is a refusal like any other: the agent's words are never read for a code.</summary>
    [Fact]
    public async Task A_refusal_that_says_another_client_only_in_words_is_refused()
    {
        var agent = Answering(Both, (frame, _) => Error(frame, -32600, "This Codex session is in use by another Codex client."));

        var refused = await Assert.ThrowsAsync<AcpResumeRefused>(() => new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/trees/s-1", "Also log the port.", CancellationToken.None, resume: "thread-1"));

        Assert.Equal(ContinueWhy.Refused, refused.Why.Code);
    }

    /// <summary>
    /// Words to an ended or parked record go on as one prompt, each its own text block, in the order said (D137 §2.2): the
    /// conversation reads a correction as a correction.
    /// </summary>
    [Fact]
    public async Task Words_waiting_are_one_prompt_each_its_own_text_block()
    {
        var agent = Answering(Both);

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { })
            .RunAsync("D:/trees/s-1", "Port 8080.\n\nNo, 9090.", CancellationToken.None, resume: "0b5e7c1a", blocks: ["Port 8080.", "No, 9090."]);

        var prompt = agent.Params("session/prompt").GetProperty("prompt");
        Assert.Equal(["Port 8080.", "No, 9090."], prompt.EnumerateArray().Select(block => block.GetProperty("text").GetString()));
    }

    /// <summary>
    /// 🔴 A conversation that goes on (MSG1c, D137 §2.2): resumed on the id it kept, never opened anew, and its first turn
    /// the person's words waiting, each its own text block in the order said, told once the prompt is on the wire.
    /// </summary>
    [Fact]
    public async Task A_conversation_that_goes_on_is_resumed_and_its_first_turn_is_each_word_as_its_own_block()
    {
        var agent = Answering(Both);
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });
        var sent = false;

        await session.ResumeAsync("D:/trees/chat-1", "0b5e7c1a", CancellationToken.None, Servers);
        var stop = await session.PromptAsync(["Port 8080.", "No, 9090."], CancellationToken.None, () => sent = true);

        Assert.Equal(["initialize", "session/resume", "session/prompt"], agent.Methods);
        var prompt = agent.Params("session/prompt");
        Assert.Equal("0b5e7c1a", prompt.GetProperty("sessionId").GetString());
        Assert.Equal(
            ["Port 8080.", "No, 9090."],
            prompt.GetProperty("prompt").EnumerateArray().Select(block => block.GetProperty("text").GetString()));
        Assert.True(sent);
        Assert.Equal("end_turn", stop);
        session.Release();
    }

    /// <summary>The posture is set on the resume's answer as on a new session's (D81): the adapter's first offered mode.</summary>
    [Fact]
    public async Task The_posture_is_set_on_the_resumed_conversation()
    {
        var agent = Answering(Both, (frame, _) =>
            Ok(frame, """{"modes":{"currentModeId":"default","availableModes":[{"id":"default"},{"id":"auto"},{"id":"acceptEdits"}]}}"""));

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, posture: "auto|acceptEdits")
            .RunAsync("D:/trees/s-1", "Port 8080.", CancellationToken.None, resume: "0b5e7c1a");

        Assert.Equal(["initialize", "session/resume", "session/set_mode", "session/prompt", "session/close"], agent.Methods);
        Assert.Equal("auto", agent.Params("session/set_mode").GetProperty("modeId").GetString());
        Assert.Equal("0b5e7c1a", agent.Params("session/set_mode").GetProperty("sessionId").GetString());
    }

    /// <summary>
    /// The id a new session's answer names is told the moment it arrives (D131 §1), so it is kept before the first turn can
    /// fail or park.
    /// </summary>
    [Fact]
    public async Task A_new_sessions_id_is_told_as_soon_as_the_agent_names_it()
    {
        string? told = null;
        var agent = new Agent((frame, self) => (frame.TryGetProperty("method", out var m) ? m.GetString() : null) switch
        {
            "initialize" => Ok(frame, Both),
            "session/new" => Ok(frame, """{"sessionId":"fresh-1"}"""),
            "session/prompt" => Ok(frame, """{"stopReason":"end_turn"}"""),
            _ => frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null,
        });

        await new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onConversation: id => told ??= id)
            .RunAsync("D:/trees/s-1", "take quest #q1", CancellationToken.None);

        Assert.Equal("fresh-1", told);
    }
}
