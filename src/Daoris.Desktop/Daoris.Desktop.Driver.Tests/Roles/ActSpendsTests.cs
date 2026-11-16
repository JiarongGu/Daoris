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

    [Fact]
    public void A_recast_naming_the_prepaid_agent_without_the_prepaid_grant_is_refused_naming_it()
    {
        var recast = SupervisorKinds.Qualified(SupervisorKinds.Vacancy, SupervisorKinds.Recast);
        var spends = OpinionCasting.Spends(SupervisorKinds.Recast, OpinionCasting.PrepaidAgent);
        Assert.True(spends);
        Assert.Equal("vacancy.prepaid", ActSpends.Missing([[recast]], recast, spends));
        Assert.Null(ActSpends.Missing([[recast, "vacancy.prepaid"]], recast, spends));
        // Naming another reviewer spends nothing beyond the act.
        Assert.Null(ActSpends.Missing([[recast]], recast, OpinionCasting.Spends(SupervisorKinds.Recast, "codex-acp")));
    }

    [Fact]
    public void A_delegate_never_holds_more_than_its_delegator()
    {
        var recast = SupervisorKinds.Qualified(SupervisorKinds.Vacancy, SupervisorKinds.Recast);
        Assert.Equal("vacancy.prepaid", ActSpends.Missing([[recast, "vacancy.prepaid"], [recast]], recast, spends: true));
        Assert.Equal(recast, ActSpends.Missing([[recast], []], recast, spends: false));
        // A spend no grant covers stays the owner role's.
        Assert.Equal(ActSpending.Destination, ActSpends.Missing([["landing.approve"]], "landing.approve", spends: true));
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
