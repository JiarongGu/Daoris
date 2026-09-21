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
}
