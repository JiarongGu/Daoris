using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Host policy (the agent-lyntai design §5.3's table, rounds 4 and 5): retry is a table by door, phase and the run that holds it, today's
/// on every row, so a bound kind repeats what today's repeats and nothing else. Daoris's own types, never a proof that the layer can.
/// </summary>
public sealed class HostPolicyRetryTests
{
    [Theory]
    // A driven quest's start or continuation, handed the transient retry: the open repeated alone, a prompt's episode with a go-on.
    [InlineData(Transport.Protocol, RetryRun.QuestTurn, DoorWhere.Open, RetryRule.RepeatOpen)]
    [InlineData(Transport.Protocol, RetryRun.QuestTurn, DoorWhere.Prompt, RetryRule.Episode)]
    // A conversation: its open is never retried, its prompts by its own episode.
    [InlineData(Transport.Protocol, RetryRun.Conversation, DoorWhere.Open, RetryRule.None)]
    [InlineData(Transport.Protocol, RetryRun.Conversation, DoorWhere.Prompt, RetryRule.Episode)]
    // A pass handed no retry (a helper's, an intake's, an opinion's, a partner's, a brainstorm's): ends as it came.
    [InlineData(Transport.Protocol, RetryRun.Pass, DoorWhere.Open, RetryRule.None)]
    [InlineData(Transport.Protocol, RetryRun.Pass, DoorWhere.Prompt, RetryRule.None)]
    // The native door: no episode on any run.
    [InlineData(Transport.Native, RetryRun.QuestTurn, DoorWhere.Result, RetryRule.None)]
    [InlineData(Transport.Native, RetryRun.QuestTurn, DoorWhere.Lines, RetryRule.None)]
    [InlineData(Transport.Native, RetryRun.Conversation, DoorWhere.Result, RetryRule.None)]
    [InlineData(Transport.Native, RetryRun.Pass, DoorWhere.Result, RetryRule.None)]
    public void Host_policy_s_retry_table_is_today_s_by_door_phase_and_run(Transport door, RetryRun run, string where, RetryRule rule)
    {
        Assert.Equal(rule, RetryTable.Of(door, run, where));
    }

    /// <summary>A native resume that ends failing before it names its conversation is the same resume once more, and a second such end is no restart.</summary>
    [Fact]
    public void Host_policy_restarts_a_native_resume_once_where_it_ended_before_naming_its_conversation()
    {
        Assert.True(RetryTable.RestartsResume(Transport.Native, named: false, exitedFailing: true, restarted: false));
        Assert.False(RetryTable.RestartsResume(Transport.Native, named: false, exitedFailing: true, restarted: true));
        Assert.False(RetryTable.RestartsResume(Transport.Native, named: true, exitedFailing: true, restarted: false));
        Assert.False(RetryTable.RestartsResume(Transport.Native, named: false, exitedFailing: false, restarted: false));
        Assert.False(RetryTable.RestartsResume(Transport.Protocol, named: false, exitedFailing: true, restarted: false));
    }
}
