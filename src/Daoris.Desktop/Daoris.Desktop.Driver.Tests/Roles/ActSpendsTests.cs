using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every supervisor kind's act and every steward act declares what it spends and its pin; an act is admitted only where
/// every grant on its chain covers both its name and its spend, a refusal naming the grant missing; an act lapses with its subject.
/// </summary>
public sealed class ActSpendsTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 16, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Every_supervisor_act_and_every_steward_act_declares_its_spend_and_its_pin()
    {
        var unnamed = SupervisorKinds.Acts.Concat(AppointedRoles.StewardActs).Where(act => ActSpends.Of(act) is null).ToList();
        Assert.Empty(unnamed);
        Assert.All(ActSpends.Named, act => Assert.False(string.IsNullOrWhiteSpace(ActSpends.Of(act)!.Pin)));
        Assert.All(ActSpends.Named, act => Assert.True(SupervisorKinds.Acts.Contains(act) || AppointedRoles.StewardActs.Contains(act), act));
    }

    private static Func<RiskAct, string> Judged(string outcome) => _ => outcome;

    [Fact]
    public void Each_spend_is_an_act_kind_of_the_risk_policy_and_an_act_that_spends_nothing_none()
    {
        Assert.Equal(RiskActs.SpendMoney, ActSpends.KindOf("vacancy.prepaid")?.Kind);
        Assert.Equal(RiskActs.ReleaseStart, ActSpends.KindOf("landing.approve")?.Kind);
        Assert.Equal(RiskActs.PublishOutside, ActSpends.KindOf("files.allow-share")?.Kind);
        Assert.Equal(RiskActs.ReachWiden, ActSpends.KindOf("allow.once")?.Kind);
        Assert.Equal(RiskActs.ReachWiden, ActSpends.KindOf("allow.standing")?.Kind);
        Assert.Null(ActSpends.KindOf("allow.reject"));
        Assert.Null(ActSpends.KindOf("question.answer"));
        Assert.All(ActSpends.Named.Where(act => ActSpends.Of(act)!.Spends != ActSpending.None), act => Assert.NotNull(ActSpends.KindOf(act)));
    }

    [Fact]
    public void A_spend_the_policy_passes_needs_no_grant_beyond_the_act_one_it_gates_on_a_role_needs_its_covering_grant_and_one_the_owner_holds_is_refused()
    {
        var recast = SupervisorKinds.Qualified(SupervisorKinds.Vacancy, SupervisorKinds.Recast);
        var spends = OpinionCasting.Spends(SupervisorKinds.Recast, OpinionCasting.PrepaidAgent);
        Assert.True(spends);

        Assert.Null(ActSpends.Missing([[recast]], recast, spends, Judged(RiskVerdict.Pass)));
        Assert.Equal("vacancy.prepaid", ActSpends.Missing([[recast]], recast, spends, Judged(RiskVerdict.Gate)));
        Assert.Null(ActSpends.Missing([[recast, "vacancy.prepaid"]], recast, spends, Judged(RiskVerdict.Gate)));
        Assert.Equal(ActSpending.Money, ActSpends.Missing([[recast, "vacancy.prepaid"]], recast, spends, Judged(RiskVerdict.OwnerOutcome)));
        // Naming another reviewer spends nothing beyond the act, whatever the policy says of money.
        Assert.Null(ActSpends.Missing([[recast]], recast, OpinionCasting.Spends(SupervisorKinds.Recast, "codex-acp"), Judged(RiskVerdict.OwnerOutcome)));

        // A spend no grant covers is the role's where the policy gates it on a role, the owner role's where it classes it the owner's.
        Assert.Null(ActSpends.Missing([["landing.approve"]], "landing.approve", spends: true, Judged(RiskVerdict.Gate)));
        Assert.Equal(ActSpending.Destination, ActSpends.Missing([["landing.approve"]], "landing.approve", spends: true, Judged(RiskVerdict.OwnerOutcome)));
    }

    [Fact]
    public void Each_spend_is_judged_as_its_own_kind_at_the_place()
    {
        var asked = new List<string>();
        ActSpends.Missing([["files.allow-share"]], "files.allow-share", spends: true, act => { asked.Add(act.ToString()); return RiskVerdict.Pass; });
        ActSpends.Missing([["allow.once"]], "allow.once", spends: true, act => { asked.Add(act.ToString()); return RiskVerdict.Pass; });
        Assert.Equal([RiskActs.PublishOutside, RiskActs.ReachWiden], asked);
    }

    [Fact]
    public void A_delegate_never_holds_more_than_its_delegator()
    {
        var recast = SupervisorKinds.Qualified(SupervisorKinds.Vacancy, SupervisorKinds.Recast);
        Assert.Equal("vacancy.prepaid", ActSpends.Missing([[recast, "vacancy.prepaid"], [recast]], recast, spends: true, Judged(RiskVerdict.Gate)));
        Assert.Equal(recast, ActSpends.Missing([[recast], []], recast, spends: false, Judged(RiskVerdict.Pass)));
    }

    [Fact]
    public void A_second_vacancy_never_reads_the_first_s_act()
    {
        List<OpinionCast> casts =
        [
            new(At, "s1", LineLevels.Machine, "supervisor", OpinionCasting.Vacant, null, "nobody free"),
            new(At.AddMinutes(1), "s1", LineLevels.Machine, "supervisor", SupervisorKinds.Prepaid, null, "spend the balance"),
            new(At.AddMinutes(2), "s1", OpinionCasting.Step, "dsh", OpinionCasting.Filled, null, "dsh holds the step"),
            new(At.AddMinutes(30), "s2", LineLevels.Machine, "supervisor", OpinionCasting.Vacant, null, "nobody free again"),
        ];
        Assert.Null(OpinionCasting.Newest(casts));
        Assert.Empty(OpinionCasting.Left(casts));
    }
}
