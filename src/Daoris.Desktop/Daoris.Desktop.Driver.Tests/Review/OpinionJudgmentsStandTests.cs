using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// How long an owed opinion's judgment stands (<see cref="LookFacts.Stands"/>): over the same stamp, with its fact kept, until five
/// minutes pass or the earliest moment it turns over still ahead does, a moment already past bounding nothing; never over another
/// stamp, never once let go, never for a judgment marked before a let-go, and never for an opinion no longer owed. A gate turns over
/// at its reset and at a reading pass's minutes (<see cref="OpinionGateLook.Turns"/>).
/// </summary>
public sealed class OpinionJudgmentsStandTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly MovedClock _clock = new() { VirtualTimestamp = true };

    private LookFacts Facts(params string[] owed)
    {
        var facts = new LookFacts(_clock);
        foreach (var due in owed) facts.KeepOpinion(due, new OpinionFact(new Dictionary<string, OpinionSit>(), null, null));
        return facts;
    }

    [Fact]
    public void A_judgment_stands_over_its_stamp_until_five_minutes_pass()
    {
        var facts = Facts("s1/landing");
        facts.Stamped("s1/landing", "a", [], facts.Mark());

        Assert.True(facts.Stands("s1/landing", "a"));
        Assert.False(facts.Stands("s1/landing", "b"));
        Assert.False(facts.Stands("s2/landing", "a"));

        _clock.Advance(LookFacts.Refresh - TimeSpan.FromMilliseconds(1));
        Assert.True(facts.Stands("s1/landing", "a"));
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.False(facts.Stands("s1/landing", "a"));
    }

    [Fact]
    public void A_judgment_is_judged_again_once_the_earliest_moment_ahead_passes_and_a_moment_already_past_bounds_nothing()
    {
        var facts = Facts("s1/landing", "s2/landing");
        var now = _clock.GetUtcNow();
        facts.Stamped("s1/landing", "a", [now.AddMinutes(3), now.AddMinutes(-1), now.AddMinutes(1)], facts.Mark());
        // A reset already past at the judgment: it stands as long as one with no moment does.
        facts.Stamped("s2/landing", "b", [now.AddMinutes(-1)], facts.Mark());

        Assert.True(facts.Stands("s1/landing", "a"));
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.False(facts.Stands("s1/landing", "a"));

        _clock.Advance(LookFacts.Refresh - TimeSpan.FromMinutes(1) - TimeSpan.FromMilliseconds(1));
        Assert.True(facts.Stands("s2/landing", "b"));
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.False(facts.Stands("s2/landing", "b"));
    }

    [Fact]
    public void A_stamp_let_go_or_with_no_fact_or_no_longer_owed_stands_no_more()
    {
        var facts = Facts("s1/landing", "s2/landing");
        facts.Stamped("s1/landing", "a", [], facts.Mark());
        facts.Stamped("s2/landing", "b", [], facts.Mark());
        facts.Stamped("s3/landing", "c", [], facts.Mark());

        facts.Unstamped("s1/landing");
        facts.KeepOpinionsOf(["S3/landing"]);

        Assert.False(facts.Stands("s1/landing", "a"));
        Assert.False(facts.Stands("s2/landing", "b"));
        // No judgment's fact is kept for it, so its stamp vouches for nothing.
        Assert.False(facts.Stands("s3/landing", "c"));
    }

    [Fact]
    public void A_judgment_marked_before_its_stamp_was_let_go_keeps_none_and_one_marked_after_does()
    {
        var facts = Facts("s1/landing", "s2/landing");
        var before = facts.Mark();

        // A pass begun, or ended writing nothing, while the judgment read what it reads.
        facts.Unstamped("s1/landing");
        facts.Stamped("s1/landing", "a", [], before);
        // Another opinion's let-go is not this one's.
        facts.Stamped("s2/landing", "b", [], before);

        Assert.False(facts.Stands("s1/landing", "a"));
        Assert.True(facts.Stands("s2/landing", "b"));

        facts.Stamped("s1/landing", "a", [], facts.Mark());
        Assert.True(facts.Stands("s1/landing", "a"));
    }

    [Fact]
    public void A_gate_turns_over_at_its_reset_and_a_reading_pass_s_minutes()
    {
        var cooling = new OpinionGateState(OpinionGateStates.Unavailable, "reports") { Code = ReviewerUnavailable.Cooling };

        Assert.Equal([Now.AddMinutes(1)], OpinionGateLook.Turns(cooling with { Until = Now.AddMinutes(1) }));
        Assert.Empty(OpinionGateLook.Turns(new OpinionGateState(OpinionGateStates.Settled, "reports") { Opinion = Pass(OpinionViews.GivenState, 2) }));

        var reading = new OpinionGateState(OpinionGateStates.Reading, "reports") { Opinion = Pass(OpinionViews.Reading, 2) };
        Assert.Equal([Now.AddMinutes(2)], OpinionGateLook.Turns(reading));
        Assert.Equal([Now.AddMinutes(5), Now.AddMinutes(2)], OpinionGateLook.Turns(reading with { Until = Now.AddMinutes(5) }));
        Assert.Equal([Now.AddMinutes(1)], OpinionGateLook.Turns(
            new OpinionGateState(OpinionGateStates.ReadAgain, "reports") { Opinion = Pass(OpinionViews.GivenState, -9), Recheck = Pass(OpinionViews.Reading, 1) }));
    }

    private static OpinionView Pass(string state, int minutes) =>
        new("op1", OpinionRules.Landing, OpinionViews.FirstPass, "s1", "r1", "reports", "base", "tip", "codex-acp", "another-maker", state)
        {
            Due = Now.AddMinutes(minutes),
        };
}
