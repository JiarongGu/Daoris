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
    public async Task A_request_whose_caller_stopped_waiting_leaves_no_unobserved_exception_when_the_stream_ends()
    {
        var marker = "wire-" + Guid.NewGuid().ToString("N");
        Assert.False(await ReachesTheFinalizerAsync(marker, () => AskThenStopWaitingThenEndAsync(marker)));
    }

    [Fact]
    public async Task A_session_closed_with_an_ask_in_flight_leaves_no_unobserved_exception()
    {
        const string Said = "stream ended before it answered";
        var reached = false;
        void Seen(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception.InnerExceptions.Any(inner => inner.Message.Contains(Said, StringComparison.Ordinal))) Volatile.Write(ref reached, true);
        }

        TaskScheduler.UnobservedTaskException += Seen;
        try
        {
            await CloseWithoutAnswerAsync();
            await FinalizeAsync(() => Volatile.Read(ref reached));
            Assert.False(Volatile.Read(ref reached));
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Seen;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task CloseWithoutAnswerAsync()
    {
        var agent = new AcpBackgroundTests.Agent((frame, self) => frame.GetProperty("method").GetString() switch
        {
            "initialize" => AcpBackgroundTests.Ok(frame, """{"protocolVersion":1}"""),
            "session/new" => AcpBackgroundTests.Ok(frame, """{"sessionId":"s-1"}"""),
            _ => null,
        });
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, closeTimeout: TimeSpan.FromMilliseconds(50));
        await session.OpenAsync("D:/fam/Game", CancellationToken.None);

        await session.CloseAsync(CancellationToken.None);
        Assert.Contains("session/close", agent.Methods);
        agent.Close();
        await session.Ended.WaitAsync(Poll.Patience);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task AskThenStopWaitingThenEndAsync(string marker)
    {
        var wire = new AcpWire(TextWriter.Null, _ => { }, () => false);
        using var stopped = new CancellationTokenSource();
        var asked = wire.RequestAsync("session/close", new { }, stopped.Token);
        await stopped.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => asked);

        wire.Fail(new DriverException(marker));
    }

    private static async Task<bool> ReachesTheFinalizerAsync(string marker, Func<Task> run)
    {
        var reached = false;
        void Seen(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception.InnerExceptions.Any(inner => inner.Message == marker)) Volatile.Write(ref reached, true);
        }

        TaskScheduler.UnobservedTaskException += Seen;
        try
        {
            await run();
            await FinalizeAsync(() => Volatile.Read(ref reached));
            return Volatile.Read(ref reached);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Seen;
        }
    }

    private static async Task FinalizeAsync(Func<bool> seen)
    {
        for (var attempt = 0; attempt < 10 && !seen(); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Yield();
        }
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
