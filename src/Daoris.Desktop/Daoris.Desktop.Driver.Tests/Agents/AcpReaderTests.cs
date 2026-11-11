using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What a session's reader does as its stream ends, whatever the ending of what ran beside it does.</summary>
public sealed class AcpReaderTests
{
    /// <summary>A streams sink whose every write throws, as a store that refuses it does.</summary>
    private sealed class Refusing : ISessionStreams
    {
        public void Open(SessionStream stream) { }

        public void Line(string streamId, string text) => throw new IOException("the store refused the line");

        public void End(string streamId, string? state) => throw new IOException("the store refused the end");
    }

    [Fact]
    public async Task A_line_the_transcript_cannot_keep_does_not_end_the_reader_while_the_agent_lives()
    {
        var agent = new AcpBackgroundTests.Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return AcpBackgroundTests.Ok(frame, """{"protocolVersion":1}""");
                case "session/new":
                    return AcpBackgroundTests.Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("a plain line the agent wrote to stdout");
                    return AcpBackgroundTests.Ok(frame, """{"stopReason":"end_turn"}""");
                default:
                    return null;
            }
        });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, line =>
        {
            if (line.Contains("a plain line", StringComparison.Ordinal)) throw new IOException("the transcript is gone");
        });

        var outcome = await session.RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
    }

    [Fact]
    public async Task A_held_permission_whose_line_cannot_be_kept_is_still_answered_when_the_person_answers()
    {
        var agent = new AcpBackgroundTests.Agent((frame, self) =>
        {
            switch (frame.TryGetProperty("method", out var method) ? method.GetString() : null)
            {
                case "initialize":
                    return AcpBackgroundTests.Ok(frame, """{"protocolVersion":1}""");
                case "session/new":
                    return AcpBackgroundTests.Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    self.Push("""{"jsonrpc":"2.0","id":900,"method":"session/request_permission","params":{"sessionId":"s-1","toolCall":{"toolCallId":"c9","title":"docker restart","kind":"execute"},"options":[{"optionId":"yes","name":"Allow","kind":"allow_once"},{"optionId":"no","name":"Reject","kind":"reject_once"}]}}""");
                    return null;
                default:
                    return null;
            }
        });
        var registry = new HeldPermissions();
        var session = new AcpSession(agent.Incoming, agent.Outgoing, line =>
        {
            if (line.Contains("permission held", StringComparison.Ordinal)) throw new IOException("the transcript is gone");
        }, posture: "?default") { Permissions = registry.For("d1") };

        var run = session.RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None);
        await Poll.Until(() => registry.Held("d1").Count == 1, () => "nothing was held for the person");
        Assert.True(registry.Answer("d1", "900", "no").Answered);

        await Poll.Until(
            () => { lock (agent.Sent) return agent.Sent.Any(line => line.Contains("\"id\":900", StringComparison.Ordinal) && line.Contains("result", StringComparison.Ordinal)); },
            () => "the person's answer never reached the agent");
        agent.Close();
        await Assert.ThrowsAsync<DriverException>(() => run.WaitAsync(Poll.Patience));
    }

    [Fact]
    public async Task A_stream_that_ends_faults_what_is_awaited_even_where_ending_what_ran_beside_it_throws()
    {
        var agent = new AcpBackgroundTests.Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return AcpBackgroundTests.Ok(frame, """{"protocolVersion":1}""");
                case "session/new":
                    return AcpBackgroundTests.Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    // A child is open when the stream ends, so its ending is written to the sink that refuses it.
                    self.Push(AcpBackgroundTests.Update("""{"sessionUpdate":"subagent_spawned","subagentSessionId":"a1","name":"One","task":"read"}"""));
                    self.Close();
                    return null;
                default:
                    return null;
            }
        });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, streams: new Refusing());

        var run = session.RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None);

        var failed = await Assert.ThrowsAsync<DriverException>(() => run.WaitAsync(Poll.Patience));
        Assert.Contains("stream ended before it answered", failed.Message);
    }

    [Fact]
    public async Task A_run_that_ended_its_turn_returns_its_outcome_even_where_ending_what_ran_beside_it_throws()
    {
        var agent = new AcpBackgroundTests.Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return AcpBackgroundTests.Ok(frame, """{"protocolVersion":1}""");
                case "session/new":
                    return AcpBackgroundTests.Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    // A child is still open when the turn ends, so the run's own ending writes it to the sink that refuses it.
                    self.Push(AcpBackgroundTests.Update("""{"sessionUpdate":"subagent_spawned","subagentSessionId":"a1","name":"One","task":"read"}"""));
                    return AcpBackgroundTests.Ok(frame, """{"stopReason":"end_turn"}""");
                default:
                    return null;
            }
        });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, streams: new Refusing());

        var outcome = await session.RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
    }
}
