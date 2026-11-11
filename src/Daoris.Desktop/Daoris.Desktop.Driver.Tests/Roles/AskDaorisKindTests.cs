using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The kind <c>ask-daoris</c>: Ask Daoris's acts, read after each turn by a supervisor at the machine alone, whose one act is to report.
/// The seed holds <i>Second check</i> deciding it, held by the person's rules, so the check is off until the person turns it on.
/// </summary>
public sealed class AskDaorisKindTests
{
    private static RoleRecord Checker(RoleScope scope, string id = "checker") =>
        new(id, AuthoredText.Of("Checker"), RoleRecord.Appointed, [], scope,
            new RoleHold(AppointedRoles.Supervisor, [SupervisorKinds.Qualified(SupervisorKinds.AskDaoris, SupervisorKinds.Report)], RoleHolders.Terminal)
            {
                Decides = [SupervisorKinds.AskDaoris],
            });

    [Fact]
    public void The_kind_is_of_the_vocabulary_with_its_one_act_and_is_the_machine_s_alone()
    {
        Assert.Contains(SupervisorKinds.AskDaoris, SupervisorKinds.All);
        Assert.Equal(["ask-daoris.report"], SupervisorKinds.ActsOf(SupervisorKinds.AskDaoris));
        Assert.Equal(SupervisorKinds.AskDaoris, SupervisorKinds.KindOf("ask-daoris.report"));
        Assert.True(SupervisorKinds.MachineOnly(SupervisorKinds.AskDaoris));
        Assert.All(SupervisorKinds.All.Where(kind => kind != SupervisorKinds.AskDaoris), kind => Assert.False(SupervisorKinds.MachineOnly(kind)));
        Assert.Equal("range", new SupervisorSubject(SupervisorKinds.AskDaoris, "s1", "r1").IdName);
        Assert.Contains(SupervisorKinds.AskDaoris, SupervisorSubject.Kinds);
    }

    [Fact]
    public void A_supervisor_deciding_it_below_the_machine_is_refused_machine_only_and_at_the_machine_taken()
    {
        Assert.Equal("machine-only", RoleHold.Judge(Checker(new RoleScope(RoleScope.Workspace, "w1")))?.Code);
        Assert.Equal("machine-only", RoleHold.Judge(Checker(new RoleScope(RoleScope.Repository, "atlas")))?.Code);
        Assert.Null(RoleHold.Judge(Checker(RoleScope.Everywhere)));

        var catalogue = RoleCatalog.Of([]);
        Assert.Equal("machine-only", catalogue.Judge(RoleChange.Set(Checker(new RoleScope(RoleScope.Workspace, "w1"))))?.Code);
    }

    [Fact]
    public void The_seed_holds_second_check_deciding_the_kind_at_the_machine_held_by_rules_so_it_is_off()
    {
        var role = RoleCatalog.Seed.Single(each => each.Id == SecondCheck.Role);

        Assert.Equal(("Second check", "复核"), (role.Name.Find("en"), role.Name.Find("zh")));
        Assert.Equal(RoleScope.Machine, role.Scope.Kind);
        Assert.Equal(RoleHolders.Rules, role.Hold!.Holder);
        Assert.Equal([SupervisorKinds.AskDaoris], role.Hold.Decides);
        Assert.Equal(["ask-daoris.report"], role.Hold.Acts);
        Assert.Null(RoleHold.Judge(role));
        Assert.False(SecondCheck.On(RoleCatalog.Of([]).Slots));
    }

    [Fact]
    public void The_machine_s_line_names_second_check_and_no_place_below_it_has_a_line_for_the_kind()
    {
        var catalogue = RoleCatalog.Of([]);

        var deciders = SupervisorDeciders.Of(catalogue.Slots, RuleWiderPlaces.From([("engine", "games")]));

        var machine = deciders.Places.Single(place => place.Place == "machine").Kinds.Single(line => line.Kind == SupervisorKinds.AskDaoris);
        Assert.Equal((SecondCheck.Role, RoleHolders.Rules), (machine.Role, machine.Holder));
        Assert.All(deciders.Places.Where(place => place.Place != "machine"), place => Assert.DoesNotContain(place.Kinds, line => line.Kind == SupervisorKinds.AskDaoris));
        Assert.DoesNotContain(deciders.Elsewhere, line => line.Kind == SupervisorKinds.AskDaoris);
    }

    [Fact]
    public void Held_by_an_agent_the_check_is_on()
    {
        var seeded = RoleCatalog.Seed.Single(each => each.Id == SecondCheck.Role);
        var on = seeded with { Hold = seeded.Hold! with { Holder = RoleHolders.Agent } };

        var slots = RoleCatalog.Of([new StoredRole(SecondCheck.Role, "set", on.Json(), 1)]).Slots;

        Assert.True(SecondCheck.On(slots));
    }
}
