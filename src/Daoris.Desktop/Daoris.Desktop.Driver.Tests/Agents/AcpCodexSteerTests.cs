using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// *Send now* on <c>codex-acp</c>, whose <c>_session/steering</c> reads no <c>idleBehavior</c>: a steer that finds its turn just ended
/// starts one, <c>startedNewTurn</c>, whose words no prompt owns. Against <see cref="AcpSteerTests"/>' scripted agent: no process, so the
/// fast half. The harness's measured answers are <c>docs/2026-10-03-steer-evidence.md</c> §8.
/// </summary>
public sealed class AcpCodexSteerTests
{
    /// <summary>What <c>codex-acp</c> 2.6.0 answers at <c>initialize</c>: the steering extension, no prompt queueing (measured).</summary>
    internal const string CodexSteers = """{"protocolVersion":1,"_meta":{"steering":{"supported":true}},"agentCapabilities":{}}""";

    private static string Ok(JsonElement request, string result) =>
        $$"""{"jsonrpc":"2.0","id":{{request.GetProperty("id").GetRawText()}},"result":{{result}}}""";

    /// <summary>
    /// The words are taken as the next turn's and said so, never noted unreached, and the steer lasts until that turn ends: Codex says no
    /// end, so its quiet ends it, closed in the record as an answered turn, before another prompt may go into it.
    /// </summary>
    [Fact]
    public async Task A_steer_that_started_a_new_turn_is_taken_as_the_next_turn_and_lasts_until_that_turn_is_quiet()
    {
        var first = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new AcpSteerTests.Agent((frame, self) =>
        {
            switch (frame.GetProperty("method").GetString())
            {
                case "initialize":
                    return Ok(frame, CodexSteers);
                case "session/new":
                    return Ok(frame, """{"sessionId":"s-1"}""");
                case "session/prompt":
                    first.SetResult(frame.GetProperty("id").GetInt32());
                    return null;
                case "_session/steering":
                    self.Push($$$"""{"jsonrpc":"2.0","id":{{{first.Task.Result}}},"result":{"stopReason":"end_turn"}}""");
                    self.Push(Ok(frame, """{"outcome":"startedNewTurn"}"""));
                    self.Push("""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"MANGO"}}}}""");
                    return null;
                default:
                    return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
            }
        });
        var record = new AcpSteerTests.Record();
        var session = new AcpSession(agent.Incoming, agent.Outgoing, _ => { }, onEvent: record.Event, wakeQuiet: TimeSpan.FromMilliseconds(300));
        await session.OpenAsync("D:/fam/Game", CancellationToken.None);
        session.OnTaken(record.Asked);
        Assert.True(session.Steers);
        var turn = session.PromptAsync("read the five files", CancellationToken.None);
        await first.Task.WaitAsync(Poll.Patience);

        var steered = await session.SteeredAsync(
            new ChatMessage("put MANGO after the words", []) { Id = "said-c2" }, CancellationToken.None, "conversation").WaitAsync(Poll.Patience);

        Assert.Equal("startedNewTurn", steered);
        Assert.Equal("end_turn", await turn.WaitAsync(Poll.Patience));
        Assert.Equal(
            [
                "turn:end_turn", "taken:said-c2:put MANGO after the words",
                "note:Sent now: its turn ended before it could take this, so this began the next.", "message:MANGO", "turn:end_turn",
            ],
            record.Lines);
        Assert.Equal([ChatNoteCodes.WordsSentNowNextTurn.Code], record.Notes.Select(note => note.Parts?[0].Code));
        session.Release();
    }
}
