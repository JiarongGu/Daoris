using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// How long an owed opinion's judgment stands (<see cref="OpinionJudgments"/>): over the same stamp until five minutes pass or
/// its gate's reset does, never over another stamp, never once forgotten, and never for an opinion no longer owed.
/// </summary>
public sealed class OpinionJudgmentsStandTests
{
    private long _now = 1_000_000;

    [Fact]
    public void A_judgment_stands_over_its_stamp_until_five_minutes_pass()
    {
        var judgments = new OpinionJudgments("home", () => _now);
        judgments.Keep("s1/landing", "a", cools: null);

        Assert.True(judgments.Stands("s1/landing", "a"));
        Assert.False(judgments.Stands("s1/landing", "b"));
        Assert.False(judgments.Stands("s2/landing", "a"));

        _now += (long)OpinionJudgments.Refresh.TotalMilliseconds - 1;
        Assert.True(judgments.Stands("s1/landing", "a"));
        _now += 1;
        Assert.False(judgments.Stands("s1/landing", "a"));
    }

    [Fact]
    public void A_gate_cooling_until_a_reset_is_judged_again_once_it_passes()
    {
        var judgments = new OpinionJudgments("home", () => _now);
        judgments.Keep("s1/landing", "a", cools: TimeSpan.FromMinutes(1));

        Assert.True(judgments.Stands("s1/landing", "a"));
        _now += 60_000;
        Assert.False(judgments.Stands("s1/landing", "a"));
    }

    [Fact]
    public void A_forgotten_judgment_or_one_no_longer_owed_stands_no_more()
    {
        var judgments = new OpinionJudgments("home", () => _now);
        judgments.Keep("s1/landing", "a", cools: null);
        judgments.Keep("s2/landing", "b", cools: null);

        judgments.Forget("s1/landing");
        judgments.KeepOnly(["S3/landing"]);

        Assert.False(judgments.Stands("s1/landing", "a"));
        Assert.False(judgments.Stands("s2/landing", "b"));
    }
}
