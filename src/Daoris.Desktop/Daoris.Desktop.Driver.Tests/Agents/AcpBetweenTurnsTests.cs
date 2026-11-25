using System.Text.Json;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.AcpBackgroundTests;
using Record = Daoris.Desktop.Driver.Tests.AcpBackgroundTests.Record;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A driven session between turns is one that waits on its own background work with no turn running (<see cref="AcpSession.BetweenTurns"/>):
/// what an update's drain does not wait on. A turn its harness wakes when work ends is a turn, though the session still waits on work then.
/// </summary>
public sealed class AcpBetweenTurnsTests
{
    private const string Queues = """{"protocolVersion":1,"agentCapabilities":{"_meta":{"claudeCode":{"promptQueueing":true}}}}""";

    private const string WokenEnd =
        """{"sessionUpdate":"usage_update","used":61234,"size":200000,"cost":{"amount":1.25,"currency":"USD"},"_meta":{"_claude/origin":{"kind":"task-notification"}}}""";

    [Fact]
    public async Task A_session_waiting_on_its_own_work_is_between_turns_until_the_turn_its_harness_woke_runs()
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
                    self.Push(Update("""{"sessionUpdate":"async_task_spawned","asyncTaskId":"t1","name":"Checks","taskType":"shell","canStop":true}"""));
                    self.Push(Said("The checks run in the background; I'll go on when they finish."));
                    return Ok(frame, """{"stopReason":"end_turn"}""");
                case "session/prompt":
                    return Unanswered(frame, goOn);
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var session = Session(agent, new Record(), Soon);

        var run = session.RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None, waitsOnBackground: _ => Task.FromResult(true));
        await Poll.Until(() => session.BetweenTurns, () => "never between turns", Poll.Patience);
        Assert.True(session.WaitsOnOwnWork);

        agent.Push(Update("""{"sessionUpdate":"async_task_state_update","asyncTaskId":"t1","state":"completed"}"""));
        agent.Push(Update("""{"sessionUpdate":"tool_call","toolCallId":"toolu_e2e","title":"npm run e2e","kind":"execute","status":"pending","rawInput":{"command":"npm run e2e"}}"""));
        await Poll.Until(() => !session.BetweenTurns, () => "the woken turn read as no turn", Poll.Patience);
        Assert.True(session.WaitsOnOwnWork);

        agent.Push(Update("""{"sessionUpdate":"tool_call_update","toolCallId":"toolu_e2e","status":"completed"}"""));
        agent.Push(Said("The end-to-end run passed."));
        agent.Push(Update(WokenEnd));
        agent.Push(Update(WokenEnd));
        await EndsWithoutGoOn(run, goOn.Task);
    }
}
