using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An agent that exited after its last turn has closed its stdin, so every write after it throws. The session's closing acts are
/// best-effort, and a finished run keeps its outcome whatever they meet. Over in-memory streams: no process and no model.
/// </summary>
public sealed class AcpClosedStdinTests
{
    private static AcpTransientTests.Agent Agent() => new((frame, self) => frame.GetProperty("method").GetString() switch
    {
        "initialize" => AcpTransientTests.Ok(frame, """{"protocolVersion":1}"""),
        "session/new" => AcpTransientTests.Ok(frame, """{"sessionId":"s-9"}"""),
        "session/prompt" => AcpTransientTests.Ok(frame, """{"stopReason":"end_turn"}"""),
        _ => frame.TryGetProperty("id", out _) ? AcpTransientTests.Ok(frame, "{}") : null,
    });

    /// <summary>A stdin that throws on the frames written once <paramref name="gone"/> says the agent has left.</summary>
    private sealed class Closed(TextWriter inner, Func<string, bool> gone) : TextWriter
    {
        public override Encoding Encoding => inner.Encoding;

        public override Task WriteLineAsync(string? value) =>
            value is not null && gone(value) ? Task.FromException(new IOException("The pipe is being closed.")) : inner.WriteLineAsync(value);
    }

    /// <summary>The cancel, the task stop and the polite close say nothing of a stdin already closed.</summary>
    [Fact]
    public async Task A_closed_stdin_costs_the_cancel_the_task_stop_and_the_close_nothing()
    {
        var left = false;
        var agent = Agent();
        var session = new AcpSession(agent.Incoming, new Closed(agent.Outgoing, _ => left), _ => { });
        await session.OpenAsync("D:/fam/Game", CancellationToken.None);
        Assert.Equal("end_turn", await session.PromptAsync("last", CancellationToken.None));
        left = true;

        await session.CancelTurnAsync();
        Assert.False(await session.StopTaskAsync("t1", CancellationToken.None));
        await session.CloseAsync(CancellationToken.None);
        session.Release();
    }

    /// <summary>A run whose agent is gone by the time it closes still returns the turn it finished.</summary>
    [Fact]
    public async Task A_run_whose_agent_is_gone_at_the_close_still_returns_its_outcome()
    {
        var agent = Agent();
        var outcome = await new AcpSession(agent.Incoming, new Closed(agent.Outgoing, line => line.Contains("session/close")), _ => { })
            .RunAsync("D:/fam/Game", "do the thing", CancellationToken.None);

        Assert.Equal("end_turn", outcome.StopReason);
    }
}
