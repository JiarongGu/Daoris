using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CODEXWAKE1: a background completion never leaves a Codex continuation waiting. The wire is <c>codex-acp</c> 2.1.1's, read from its
/// bundle: a background command is announced as a call, then <c>async_task_spawned</c> naming that call; after the prompt is answered it
/// goes on streaming the command's output, says <c>async_task_state_update</c>, and completes the call with a <c>tool_call_update</c>.
/// Its <c>usage_update</c> carries no origin and <c>turn/completed</c> sends nothing, so a turn it starts says no end.
/// </summary>
public sealed partial class AcpBackgroundTests
{
    /// <summary>What <c>codex-acp</c> answers at <c>initialize</c>: no Claude Code extension, so no origin on any turn's end.</summary>
    private const string CodexInitialized = """{"protocolVersion":1,"agentCapabilities":{"loadSession":true}}""";

    private const string Measurement = "dotnet test --filter Census";

    /// <summary>The turn as <c>codex-acp</c> 2.1.1 says it: the command's call, its spawn naming that call, the agent's words.</summary>
    private static void CodexBackgroundsTheMeasurement(Agent agent)
    {
        agent.Push(Update($$$"""{"sessionUpdate":"tool_call","toolCallId":"call_m1","title":"{{{Measurement}}}","kind":"execute","status":"in_progress","rawInput":{"command":"{{{Measurement}}}"}}"""));
        agent.Push(Update("""{"sessionUpdate":"tool_call_update","toolCallId":"call_m1","_meta":{"jetbrains":{"air":{"version":1,"asyncTasks":{"backgrounded":true}}}}}"""));
        agent.Push(Update($$$"""{"sessionUpdate":"async_task_spawned","asyncTaskId":"call_m1","name":"{{{Measurement}}}","taskType":"shell","showInTranscript":false,"canStop":true,"toolCallId":"call_m1"}"""));
        agent.Push(Said("The measurement runs in the background; I'll read it when it completes."));
    }

    /// <summary>What it says after the prompt was answered, in its order: output, the task's end, the call's completion.</summary>
    private static void CodexMeasurementEnds(Agent agent)
    {
        agent.Push(Update("""{"sessionUpdate":"tool_call_update","toolCallId":"call_m1","content":[{"type":"content","content":{"type":"text","text":"Passed! 12 tests"}}]}"""));
        agent.Push(Update("""{"sessionUpdate":"async_task_state_update","asyncTaskId":"call_m1","state":"completed","toolCallId":"call_m1"}"""));
        agent.Push(Update("""{"sessionUpdate":"tool_call_update","toolCallId":"call_m1","status":"completed"}"""));
    }

    /// <summary>A turn Codex starts itself once the measurement is done: thinking, words, a meter with no origin, and then nothing.</summary>
    private static void CodexWakes(Agent agent)
    {
        agent.Push(Update("""{"sessionUpdate":"agent_thought_chunk","content":{"type":"text","text":"Reading the output."}}"""));
        agent.Push(Said("The measurement passed: 12 tests."));
        agent.Push(Update("""{"sessionUpdate":"usage_update","used":61234,"size":200000}"""));
    }

    private static Agent CodexAgent(TaskCompletionSource<int> goOn, string? words = null) => new((frame, self) =>
    {
        switch (frame.GetProperty("method").GetString())
        {
            case "initialize":
                return Ok(frame, CodexInitialized);
            case "session/new":
                return Ok(frame, """{"sessionId":"s-1"}""");
            case "session/prompt" when self.Prompts.Count == 1:
                CodexBackgroundsTheMeasurement(self);
                return Ok(frame, """{"stopReason":"end_turn"}""");
            case "session/prompt" when words is not null && self.Prompts[^1] == words:
                return Ok(frame, """{"stopReason":"end_turn"}""");
            case "session/prompt":
                return Unanswered(frame, goOn);
            default:
                return frame.TryGetProperty("id", out _) ? Ok(frame, "{}") : null;
        }
    });

    private static Task<AcpOutcome> RunCodex(Agent agent, Record record, TimeSpan wake, DrivenInbox? inbox = null) =>
        Session(agent, record, wake)
            .RunAsync("D:/fam/Game", "close quest #12", CancellationToken.None, inbox: inbox, waitsOnBackground: _ => Task.FromResult(true));

    private static Task WaitingOnTheMeasurement(Record record) =>
        Poll.Until(
            () => record.Lines.Any(line => line.StartsWith("note:", StringComparison.Ordinal) && line.Contains(Measurement)),
            () => string.Join(" | ", record.Lines), Poll.Patience);

    /// <summary>
    /// 🔴 The measurement's output, end and completed call, said after the turn was answered, are the work's and no turn's: they began
    /// none, so nothing runs when the quiet passes and Daoris's go-on goes, as a fresh turn the adapter answers. They began a turn that
    /// never said its end, and the continuation waited on it until the timeout.
    /// </summary>
    [Fact]
    public async Task A_codex_background_commands_late_updates_start_no_turn_and_the_go_on_goes_after_the_quiet()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = CodexAgent(goOn);
        var record = new Record();

        var run = RunCodex(agent, record, Soon);
        await WaitingOnTheMeasurement(record);

        CodexMeasurementEnds(agent);
        var id = await goOn.Task.WaitAsync(Poll.Patience);
        Assert.Contains("`" + Measurement + "` (completed)", agent.Prompts[1]);

        agent.Push(Answer(id, "end_turn"));
        var outcome = await run.WaitAsync(Poll.Patience);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/prompt", "session/close"], agent.Methods);
    }

    /// <summary>
    /// 🔴 A turn Codex starts when the measurement completes says no end, and its quiet is the end: it ends the wait as a woken turn's
    /// end does, with nothing sent into it. Silence was no end, and the continuation waited on the turn for ever.
    /// </summary>
    [Fact]
    public async Task A_codex_wake_that_says_no_end_ends_when_it_falls_quiet()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = CodexAgent(goOn);
        var record = new Record();

        var run = RunCodex(agent, record, Soon);
        await WaitingOnTheMeasurement(record);

        CodexMeasurementEnds(agent);
        CodexWakes(agent);
        var outcome = await EndsWithoutGoOn(run, goOn.Task);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Equal(["initialize", "session/new", "session/prompt", "session/close"], agent.Methods);
        Assert.Equal(2, record.Lines.Count(line => line == "turn:end_turn"));
        Assert.Contains("message:The measurement passed: 12 tests.", record.Lines);
    }

    /// <summary>A call the wake has open is the turn working, never quiet: the turn is not ended under it, and is ended once it closes.</summary>
    [Fact]
    public async Task A_codex_wake_with_a_call_open_is_not_ended_until_the_call_closes()
    {
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = CodexAgent(goOn);
        var record = new Record();

        var run = RunCodex(agent, record, Soon);
        await WaitingOnTheMeasurement(record);

        CodexMeasurementEnds(agent);
        agent.Push(Update("""{"sessionUpdate":"tool_call","toolCallId":"call_e2e","title":"npm run e2e","kind":"execute","status":"in_progress"}"""));
        await Task.Delay(Soon * 4);
        Assert.False(run.IsCompleted);
        Assert.False(goOn.Task.IsCompleted);

        agent.Push(Update("""{"sessionUpdate":"tool_call_update","toolCallId":"call_e2e","status":"completed"}"""));
        var outcome = await EndsWithoutGoOn(run, goOn.Task);

        Assert.Equal("end_turn", outcome.StopReason);
        Assert.Single(agent.Prompts);
    }

    /// <summary>
    /// 🔴 The person's words said while Codex's wake runs are held for its end, which it never says: they go once the wake falls quiet,
    /// one prompt, answered, and nothing of Daoris's goes beside them. They were held behind a turn that never ended.
    /// </summary>
    [Fact]
    public async Task The_persons_words_held_through_a_codex_wake_go_once_when_it_falls_quiet()
    {
        const string words = "skip the other suites";
        var goOn = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = CodexAgent(goOn, words);
        var record = new Record();
        var inbox = new DrivenInbox(_ => { });

        var run = RunCodex(agent, record, Soon, inbox);
        await WaitingOnTheMeasurement(record);

        CodexMeasurementEnds(agent);
        CodexWakes(agent);
        await Poll.Until(
            () => record.Lines.Contains("message:The measurement passed: 12 tests."), () => string.Join(" | ", record.Lines), Poll.Patience);
        Assert.True(inbox.Hold(new ChatMessage(words, [])));

        await EndsWithoutGoOn(run, goOn.Task);

        Assert.Equal(["close quest #12", words], agent.Prompts);
        Assert.DoesNotContain("session/cancel", agent.Methods);
        Assert.Equal("session/close", agent.Methods[^1]);
    }
}
