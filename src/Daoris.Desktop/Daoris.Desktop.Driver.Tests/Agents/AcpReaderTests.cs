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
