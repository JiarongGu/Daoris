using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The reporting line read off the supervisor resolver: the place's lead, the machine's head, then the owner actor. Help waits on the
/// first level that holds it; a level nobody is appointed to passes it up naming why; an owner-class kind is refused below the owner
/// actor; other help its head leaves ends as gave up with what it tried, never as a question to the owner actor.
/// </summary>
public sealed class ReportingLineTests
{
    private const string Engine = "engine";
    private const string Games = "games";

    private static readonly RuleWiderPlaces Placed = RuleWiderPlaces.From([(Engine, Games)]);
    private static readonly RolePlace AtEngine = new(Repository: Engine);
    private static readonly string Recast = SupervisorKinds.Qualified(SupervisorKinds.Vacancy, SupervisorKinds.Recast);

    private static RoleSlot Supervisor(string id, RoleScope scope, string? holder, params string[] acts)
    {
        var role = new RoleRecord(id, AuthoredText.Of(id), RoleRecord.Appointed, [], scope, new RoleHold(AppointedRoles.Supervisor, acts, holder));
        return new RoleSlot(id, RoleState.Set, role, null, 1, scope, Appointed: true);
    }

    private static RoleSlot Machine(string? holder = RoleHolders.Rules, params string[] acts) =>
        Supervisor(AppointedRoles.Supervisor, RoleScope.Everywhere, holder, acts.Length == 0 ? [.. SupervisorKinds.Seeded] : acts);

    private static ReportingLine Line(params RoleSlot[] slots) => ReportingLine.Of(slots, SupervisorKinds.Vacancy, AtEngine, Placed, SupervisorKinds.Recast);

    [Fact]
    public void An_agent_held_lead_at_the_repository_holds_the_help_and_the_machine_is_its_head()
    {
        var line = Line(Supervisor("engine-lead", new RoleScope(RoleScope.Repository, Engine), RoleHolders.Agent, Recast), Machine());

        Assert.Equal((LineActors.Lead, "engine-lead", LineHolders.Agent), (line.Lead.Actor, line.Lead.Role, line.Lead.Holder));
        Assert.Equal((LineActors.Head, AppointedRoles.Supervisor, LineHolders.Person), (line.Head.Actor, line.Head.Role, line.Head.Holder));

        var waits = line.Climb(ownerClass: false);
        Assert.Equal(LineActors.Lead, waits.At!.Actor);
        Assert.Empty(waits.Passed);
        Assert.Equal("waiting on lead: `engine-lead`", waits.Said());
    }

    [Fact]
    public void A_workspace_lead_answers_a_repository_no_lead_of_its_own_names()
    {
        var line = Line(Supervisor("games-lead", new RoleScope(RoleScope.Workspace, Games), RoleHolders.Terminal, Recast), Machine());

        Assert.Equal(("games-lead", LineHolders.Terminal), (line.Lead.Role, line.Lead.Holder));
    }

    [Fact]
    public void With_no_lead_staffed_the_help_passes_up_to_the_head_naming_why()
    {
        var line = Line(Machine(RoleHolders.Agent, Recast));

        Assert.True(line.Lead.Vacant);
        var waits = line.Climb(ownerClass: false);
        Assert.Equal((LineActors.Head, LineHolders.Agent), (waits.At!.Actor, waits.At.Holder));
        var passed = Assert.Single(waits.Passed);
        Assert.Equal((LineActors.Lead, LinePasses.Vacant), (passed.Actor, passed.Code));
        Assert.Contains("`engine`", passed.Said, StringComparison.Ordinal);
        Assert.StartsWith("waiting on head: `supervisor` · passed up: lead: ", waits.Said(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_level_held_by_the_persons_rules_is_held_by_the_person_there()
    {
        var rules = Line(Machine());
        Assert.Equal((LineActors.Head, LineHolders.Person), (rules.Climb(ownerClass: false).At!.Actor, rules.Climb(ownerClass: false).At!.Holder));
        Assert.Equal("waiting on head: you · passed up: lead: " + rules.Lead.Why, rules.Climb(ownerClass: false).Said());
    }

    [Fact]
    public void A_level_with_the_act_unticked_is_vacant_and_the_help_passes_up()
    {
        var unticked = Line(Supervisor("engine-lead", new RoleScope(RoleScope.Repository, Engine), RoleHolders.Agent, "question.answer"), Machine(RoleHolders.Agent, Recast));

        Assert.True(unticked.Lead.Vacant);
        var waits = unticked.Climb(ownerClass: false);
        Assert.Equal((LineActors.Head, LineHolders.Agent), (waits.At!.Actor, waits.At.Holder));
        var passed = Assert.Single(waits.Passed);
        Assert.Equal((LineActors.Lead, LinePasses.Vacant), (passed.Actor, passed.Code));
        Assert.Contains(Recast, passed.Said, StringComparison.Ordinal);

        // Unticked at the head too: other help ends as gave up, and only an owner-class kind reaches the owner actor.
        var none = Line(
            Supervisor("engine-lead", new RoleScope(RoleScope.Repository, Engine), RoleHolders.Agent, "question.answer"), Machine(RoleHolders.Agent, "question.answer"));
        Assert.True(none.Climb(ownerClass: false).GaveUp);
        Assert.Equal([LinePasses.Vacant, LinePasses.Vacant], none.Climb(ownerClass: false).Passed.Select(each => each.Code));
        Assert.Equal(LineActors.Owner, none.Climb(ownerClass: true).At!.Actor);
    }

    [Fact]
    public void An_owner_class_kind_is_refused_below_the_owner_actor_and_reaches_it_with_the_readings_below()
    {
        var line = Line(Supervisor("engine-lead", new RoleScope(RoleScope.Repository, Engine), RoleHolders.Agent, Recast), Machine(RoleHolders.Agent, Recast));

        var waits = line.Climb(ownerClass: true, new Dictionary<string, string> { [LineActors.Lead] = "the destination is marked and the edits are live data" });

        Assert.Equal((LineActors.Owner, LineHolders.Person), (waits.At!.Actor, waits.At.Holder));
        Assert.Equal([LinePasses.OwnerClass, LinePasses.OwnerClass], waits.Passed.Select(each => each.Code));
        Assert.Contains("the destination is marked", waits.Passed[0].Said, StringComparison.Ordinal);
        Assert.True(OwnerClass.Is(OwnerClass.Money));
        Assert.False(OwnerClass.Is("preference"));
    }

    [Fact]
    public void Non_owner_class_help_the_head_leaves_ends_as_gave_up_with_what_was_tried()
    {
        var line = Line(Supervisor("engine-lead", new RoleScope(RoleScope.Repository, Engine), RoleHolders.Agent, Recast), Machine(RoleHolders.Agent, Recast));
        var left = new Dictionary<string, string>
        {
            [LineActors.Lead] = "no family is free before the reset",
            [LineActors.Head] = "no account of another maker is signed in",
        };

        var waits = line.Climb(ownerClass: false, left);

        Assert.True(waits.GaveUp);
        Assert.Null(waits.At);
        Assert.Equal([(LineActors.Lead, LinePasses.Left), (LineActors.Head, LinePasses.Left)], waits.Passed.Select(each => (each.Actor, each.Code)));
        Assert.Equal("gave up: lead: no family is free before the reset; head: no account of another maker is signed in", waits.Said());
    }

    [Fact]
    public void The_owner_class_tags_are_doctrine_rule_ones_closed_list()
    {
        Assert.Equal(["direction", "money", "sign-in", "release", "history", "data", "publish"], OwnerClass.Kinds);
    }
}
