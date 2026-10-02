using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// *Try again* over the bridge (`RETRY_QUEST`, RETRY1 as SESSUX1b extends it, D126 §3.4): one act that does whichever applies
/// to the quest the loop's last look considered, and says which. A quest parked on its failed sessions is marked at the strike
/// limit, as `daoris driver retry` marks it; a quest the person's stop holds is released from the session that look named,
/// with no mark, since a stop is not a strike; any other is refused, and nothing is written.
/// </summary>
/// <remarks>The loop's last look is recorded by hand, as <c>DriverModuleSessionsTests</c> records it: no service, no tick.</remarks>
public sealed class DriverModuleRetryTests : DriverModuleBridge
{
    private static readonly QuestView Taken = new("q1", "game", "engine", "Cap the chunk budget", "A body.", "Taken");

    private static readonly PriorSession Stop = new("s1a2b3c4", "X:/trees/s1", "stopped", "the person stopped it.");

    private DriverModule Looked(params Consideration[] considered)
    {
        var loop = Loop();
        loop.Look.Record(considered);
        return new DriverModule(Bus, loop);
    }

    [Fact]
    public async Task A_parked_quest_is_marked_at_the_strike_limit_and_the_answer_says_so()
    {
        var module = Looked(new Consideration(Taken, StartVerdict.Exhausted, "3 session(s) have failed on `#q1`"));

        var answer = await AnswerAsync(module, "RETRY_QUEST", new { quest = "q1" });

        Assert.Equal(3, DriverConfig.Load(DriverConfigPath).ForgivenAt("q1"));
        Assert.Null(DriverConfig.Load(DriverConfigPath).ReleasedFor("q1"));
        Assert.Equal("marked", answer.GetProperty("retried").GetProperty("did").GetString());
        Assert.Equal("q1", answer.GetProperty("retried").GetProperty("quest").GetString());
        // Still the driver's state, which the page's toast reads the strikes from (RETRY1).
        Assert.Equal(3, answer.GetProperty("strikes").GetInt32());
        Assert.Equal(3, answer.GetProperty("forgiven").GetProperty("q1").GetInt32());
    }

    [Fact]
    public async Task A_quest_the_persons_stop_holds_is_released_from_that_stop_and_marked_nothing()
    {
        var module = Looked(new Consideration(Taken, StartVerdict.Stopped, "you stopped session `s1a2b3c4`") { HeldBy = Stop });

        var answer = await AnswerAsync(module, "RETRY_QUEST", new { quest = "#Q1" });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal("s1a2b3c4", config.ReleasedFor("q1"));
        Assert.Equal(0, config.ForgivenAt("q1"));
        var retried = answer.GetProperty("retried");
        Assert.Equal("released", retried.GetProperty("did").GetString());
        Assert.Equal("s1a2b3c4", retried.GetProperty("session").GetString());
    }

    /// <summary>
    /// Anything the look did not park or hold is refused in the catalogue's words, naming the quest: forgiving a quest that is
    /// not parked lets it run past its strikes (D110), and a quest the look never saw has nothing to start again.
    /// </summary>
    [Theory]
    [InlineData(StartVerdict.Start)]
    [InlineData(StartVerdict.Waiting)]
    [InlineData(StartVerdict.Held)]
    public async Task A_quest_neither_parked_nor_held_by_a_stop_is_refused_and_nothing_is_written(StartVerdict verdict)
    {
        var module = Looked(new Consideration(Taken, verdict, "the planner's sentence"));

        var refusal = await RefusalAsync(module, "RETRY_QUEST", new { quest = "q1" });

        Assert.Contains(Refusals.QuestNotHeld, refusal);
        Assert.Contains("id=q1", refusal);
        Assert.False(File.Exists(DriverConfigPath));
    }

    [Fact]
    public async Task A_quest_the_last_look_never_considered_is_refused()
    {
        var refusal = await RefusalAsync(Looked(), "RETRY_QUEST", new { quest = "q9" });

        Assert.Contains(Refusals.QuestNotHeld, refusal);
        Assert.Contains("id=q9", refusal);
    }

    /// <summary>Before any look, and with no service to plan from, the driver is still coming up, as every route that reads it says.</summary>
    [Fact]
    public async Task Before_any_look_with_no_service_it_is_still_coming_up()
    {
        var refusal = await RefusalAsync(Module(), "RETRY_QUEST", new { quest = "q1" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }
}
