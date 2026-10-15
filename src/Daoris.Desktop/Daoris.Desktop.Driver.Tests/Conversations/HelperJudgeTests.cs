using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT2a-ii (D163 point 2; the mixed agents design §5): the driver's judgement of each ask for a helper, in process. It
/// judges what only the driver can: the person's rule (`off`, `ask` held `waiting`, `on`), the two at once per asker and the
/// cap, and again what the connector judged, since a file may be old. A refusal has a code the asker reads; a wait is no refusal.
/// </summary>
public sealed class HelperJudgeTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-12T10:00:00Z");

    private static HelperAsk Ask(string by = HelperAsk.ByAgent, string? agent = null) => new("a1b2c3d4", "s1", "Why?", agent, by, At);

    private static PriorSession Asker() => new("s1", "trees/default/reports/s-1", "completed", Repository: "reports") { Workspace = "default" };

    private static HelperRule Rule(string mode) => new(mode, []);

    [Fact]
    public void An_ask_with_the_rule_on_goes()
    {
        Assert.Equal(HelperVerdict.Go, HelperJudge.Judge(Ask(), null, Asker(), Rule(HelperRules.On), started: 0, slot: true));
    }

    [Theory]
    [InlineData(HelperRules.Off, HelperWhy.Off)]
    public void An_agents_ask_with_the_rule_off_is_refused_with_its_code(string mode, string code)
    {
        Assert.Equal(HelperVerdict.Refuse(code), HelperJudge.Judge(Ask(), null, Asker(), Rule(mode), 0, true));
    }

    /// <summary>`ask` holds it `waiting` for the person's yes: nothing starts until it is approved, whatever the cap.</summary>
    [Fact]
    public void An_agents_ask_with_the_rule_ask_waits_for_the_persons_yes_until_approved()
    {
        Assert.Equal(HelperVerdict.Wait(null), HelperJudge.Judge(Ask(), null, Asker(), Rule(HelperRules.Ask), 0, true));
        var waiting = new HelperState("a1b2c3d4", HelperStates.Waiting, null, null, At);
        Assert.Equal(HelperVerdict.Wait(null), HelperJudge.Judge(Ask(), waiting, Asker(), Rule(HelperRules.Ask), 0, true));

        var approved = waiting with { State = HelperStates.Approved };
        Assert.Equal(HelperVerdict.Go, HelperJudge.Judge(Ask(), approved, Asker(), Rule(HelperRules.Ask), 0, true));
    }

    /// <summary>The person's own ask is theirs: neither `ask` nor `off` holds a verb they typed, and they say yes by typing it.</summary>
    [Theory]
    [InlineData(HelperRules.Ask)]
    [InlineData(HelperRules.Off)]
    [InlineData(HelperRules.On)]
    public void The_persons_own_ask_goes_whatever_the_rule(string mode)
    {
        Assert.Equal(HelperVerdict.Go, HelperJudge.Judge(Ask(HelperAsk.ByPerson), null, Asker(), Rule(mode), 0, true));
    }

    [Fact]
    public void An_asker_that_is_gone_or_is_a_helper_a_reviewer_or_an_intake_is_refused_again_since_the_file_may_be_old()
    {
        Assert.Equal(HelperVerdict.Refuse(HelperWhy.AskerGone), HelperJudge.Judge(Ask(), null, null, Rule(HelperRules.On), 0, true));
        foreach (var unfit in new[] { Asker() with { Serves = "s0" }, Asker() with { Opinion = "o1" }, Asker() with { Ask = "a9" } })
        {
            Assert.Equal(HelperVerdict.Refuse(HelperWhy.AskerUnfit), HelperJudge.Judge(Ask(), null, unfit, Rule(HelperRules.On), 0, true));
        }
    }

    [Fact]
    public void At_most_two_helpers_at_once_per_asker_a_third_is_refused_and_a_full_cap_only_waits()
    {
        Assert.Equal(HelperVerdict.Go, HelperJudge.Judge(Ask(), null, Asker(), Rule(HelperRules.On), started: 1, slot: true));
        Assert.Equal(HelperVerdict.Refuse(HelperWhy.TooMany), HelperJudge.Judge(Ask(), null, Asker(), Rule(HelperRules.On), started: 2, slot: true));
        Assert.Equal(HelperVerdict.Wait(HelperVerdict.Cap), HelperJudge.Judge(Ask(), null, Asker(), Rule(HelperRules.On), started: 0, slot: false));
    }

    /// <summary>
    /// HELPERNO1 (D163 point 3): the person's no to an agent's ask is a refusal with its own code, `declined`, which the judge reads
    /// as past judging whatever the rule says now, so a rule turned `on` after the no never starts it.
    /// </summary>
    [Theory]
    [InlineData(HelperRules.Ask)]
    [InlineData(HelperRules.On)]
    [InlineData(HelperRules.Off)]
    public void The_persons_no_is_past_judging_whatever_the_rule(string mode)
    {
        var declined = new HelperState("a1b2c3d4", HelperStates.Refused, HelperWhy.Declined, null, At);
        Assert.Equal(HelperVerdict.Done, HelperJudge.Judge(Ask(), declined, Asker(), Rule(mode), 0, true));
        Assert.Equal("the person said no to it", HelperWhy.Said(HelperWhy.Declined));
    }

    /// <summary>A refused or answered ask is over: the judge says nothing more of it, so a look never restarts it.</summary>
    [Theory]
    [InlineData(HelperStates.Refused)]
    [InlineData(HelperStates.Answered)]
    [InlineData(HelperStates.Started)]
    public void An_ask_already_past_its_start_is_not_judged_again(string state)
    {
        var kept = new HelperState("a1b2c3d4", state, null, "h1", At);
        Assert.Equal(HelperVerdict.Done, HelperJudge.Judge(Ask(), kept, Asker(), Rule(HelperRules.On), 0, true));
    }
}
