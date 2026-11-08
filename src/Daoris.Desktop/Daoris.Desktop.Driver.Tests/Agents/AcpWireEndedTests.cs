using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What a request is told when the stream it would be answered on has already ended, or ends with it pending.</summary>
public sealed class AcpWireEndedTests
{
    private static readonly DriverException Ended = new("the ACP agent's stream ended before it answered");

    [Fact]
    public async Task A_request_made_after_the_stream_ended_is_faulted_at_once_not_left_to_its_token()
    {
        var wire = new AcpWire(TextWriter.Null, _ => { }, () => false);
        wire.Fail(Ended);

        var failed = await Assert.ThrowsAsync<DriverException>(
            () => wire.RequestAsync("initialize", new { }, CancellationToken.None).WaitAsync(Poll.Patience));

        Assert.Same(Ended, failed);
    }

    [Fact]
    public async Task A_task_stop_whose_answer_the_stream_end_faulted_stops_nothing()
    {
        var agent = new AcpBackgroundTests.Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return AcpBackgroundTests.Ok(frame, """{"protocolVersion":1}""");
                case "session/new":
                    return AcpBackgroundTests.Ok(frame, """{"sessionId":"s-1"}""");
                case "_session/async_task/stop":
                    self.Close();
                    return null;
                default:
                    return null;
            }
        });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { });
        await session.OpenAsync("D:/fam/Game", CancellationToken.None);

        var stopped = await session.StopTaskAsync("task-1", CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.False(stopped);
    }
}
