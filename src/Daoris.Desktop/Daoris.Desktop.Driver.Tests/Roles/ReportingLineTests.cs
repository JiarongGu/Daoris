using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The reporting line read off the supervisor resolver, each level named by the supervisor holding it: the repository's
/// or the workspace's supervisor, the machine's, then the owner role. Help waits on the first level that holds it; a level nobody is
/// appointed to passes it up naming why; an owner-class kind is refused below the owner role; other help the machine's supervisor leaves
/// ends as gave up with what it tried, never as a question to the owner role.
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

    private static RoleSlot AtRepository(string holder, params string[] acts) =>
        Supervisor("engine-supervisor", new RoleScope(RoleScope.Repository, Engine), holder, acts);

    private static ReportingLine Line(params RoleSlot[] slots) => ReportingLine.Of(slots, SupervisorKinds.Vacancy, AtEngine, Placed, SupervisorKinds.Recast);

    [Fact]
    public void An_agent_held_repositorys_supervisor_holds_the_help_and_the_machines_is_above_it()
    {
        var line = Line(AtRepository(RoleHolders.Agent, Recast), Machine());

        Assert.Equal((LineLevels.Repository, "engine-supervisor", LineHolders.Agent), (line.Place.Level, line.Place.Role, line.Place.Holder));
        Assert.Equal((LineLevels.Machine, AppointedRoles.Supervisor, LineHolders.Person), (line.Machine.Level, line.Machine.Role, line.Machine.Holder));

        var waits = line.Climb(ownerClass: false);
        Assert.Equal(LineLevels.Repository, waits.At!.Level);
        Assert.Empty(waits.Passed);
        Assert.Equal("waiting on the repository's supervisor: `engine-supervisor`", waits.Said());
    }

    [Fact]
    public void A_workspaces_supervisor_answers_a_repository_with_none_of_its_own()
    {
        var line = Line(Supervisor("games-supervisor", new RoleScope(RoleScope.Workspace, Games), RoleHolders.Terminal, Recast), Machine());

        Assert.Equal((LineLevels.Workspace, "games-supervisor", LineHolders.Terminal), (line.Place.Level, line.Place.Role, line.Place.Holder));
        Assert.Equal("waiting on the workspace's supervisor: `games-supervisor`", line.Climb(ownerClass: false).Said());
    }

    [Fact]
    public void With_no_supervisor_below_the_machine_the_help_passes_up_to_the_machines_naming_why()
    {
        var line = Line(Machine(RoleHolders.Agent, Recast));

        Assert.True(line.Place.Vacant);
        var waits = line.Climb(ownerClass: false);
        Assert.Equal((LineLevels.Machine, LineHolders.Agent), (waits.At!.Level, waits.At.Holder));
        var passed = Assert.Single(waits.Passed);
        Assert.Equal((LineLevels.Repository, LinePasses.Vacant), (passed.Level, passed.Code));
        Assert.Contains("`engine`", passed.Said, StringComparison.Ordinal);
        Assert.StartsWith("waiting on the machine's supervisor: `supervisor` · passed up: the repository's supervisor: ", waits.Said(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_level_held_by_the_persons_rules_is_the_persons_only_where_the_owner_marked_it()
    {
        // No level is the person by default. Unmarked, a level held by rules is vacant and the help passes up.
        var unmarked = ReportingLine.Of([Machine()], SupervisorKinds.Vacancy, AtEngine, Placed, SupervisorKinds.Recast, new PlaceRiskBook(new Dictionary<string, PlaceRisk>()));
        Assert.True(unmarked.Machine.Vacant);
        Assert.True(unmarked.Climb(ownerClass: false).GaveUp);

        // The migration's line mark at the machine keeps today's routing: the person holds the level there.
        var marked = ReportingLine.Of([Machine()], SupervisorKinds.Vacancy, AtEngine, Placed, SupervisorKinds.Recast,
            PlaceRiskBook.Unwritten.Apply(new PlaceRiskEdit { Mark = [PlaceMarks.Line] }));
        Assert.Equal((LineLevels.Machine, LineHolders.Person), (marked.Climb(ownerClass: false).At!.Level, marked.Climb(ownerClass: false).At!.Holder));
        Assert.Equal("waiting on the machine's supervisor: you · passed up: the repository's supervisor: " + marked.Place.Why, marked.Climb(ownerClass: false).Said());

        // A home whose record is not written yet reads the line as before it.
        var older = Line(Machine());
        Assert.Equal(LineHolders.Person, older.Climb(ownerClass: false).At!.Holder);
    }

    [Fact]
    public void A_level_with_the_act_unticked_is_vacant_and_the_help_passes_up()
    {
        var unticked = Line(AtRepository(RoleHolders.Agent, "question.answer"), Machine(RoleHolders.Agent, Recast));

        Assert.True(unticked.Place.Vacant);
        var waits = unticked.Climb(ownerClass: false);
        Assert.Equal((LineLevels.Machine, LineHolders.Agent), (waits.At!.Level, waits.At.Holder));
        var passed = Assert.Single(waits.Passed);
        Assert.Equal((LineLevels.Repository, LinePasses.Vacant), (passed.Level, passed.Code));
        Assert.Contains(Recast, passed.Said, StringComparison.Ordinal);

        // Unticked at the machine too: other help ends as gave up, and only an owner-class kind reaches the owner role.
        var none = Line(AtRepository(RoleHolders.Agent, "question.answer"), Machine(RoleHolders.Agent, "question.answer"));
        Assert.True(none.Climb(ownerClass: false).GaveUp);
        Assert.Equal([LinePasses.Vacant, LinePasses.Vacant], none.Climb(ownerClass: false).Passed.Select(each => each.Code));
        Assert.Equal(LineLevels.Owner, none.Climb(ownerClass: true).At!.Level);
    }

    [Fact]
    public void An_owner_class_kind_is_refused_below_the_owner_role_and_reaches_it_with_the_readings_below()
    {
        var line = Line(AtRepository(RoleHolders.Agent, Recast), Machine(RoleHolders.Agent, Recast));

        var waits = line.Climb(ownerClass: true, new Dictionary<string, string> { [LineLevels.Repository] = "the destination is marked and the edits are live data" });

        Assert.Equal((LineLevels.Owner, LineHolders.Person), (waits.At!.Level, waits.At.Holder));
        Assert.Equal([LinePasses.OwnerClass, LinePasses.OwnerClass], waits.Passed.Select(each => each.Code));
        Assert.Contains("the destination is marked", waits.Passed[0].Said, StringComparison.Ordinal);
        Assert.True(OwnerClass.Is(OwnerClass.Money));
        Assert.False(OwnerClass.Is("preference"));
    }

    [Fact]
    public void Non_owner_class_help_the_machines_supervisor_leaves_ends_as_gave_up_with_what_was_tried()
    {
        var line = Line(AtRepository(RoleHolders.Agent, Recast), Machine(RoleHolders.Agent, Recast));
        var left = new Dictionary<string, string>
        {
            [LineLevels.Repository] = "no family is free before the reset",
            [LineLevels.Machine] = "no account of another maker is signed in",
        };

        var waits = line.Climb(ownerClass: false, left);

        Assert.True(waits.GaveUp);
        Assert.Null(waits.At);
        Assert.Equal([(LineLevels.Repository, LinePasses.Left), (LineLevels.Machine, LinePasses.Left)], waits.Passed.Select(each => (each.Level, each.Code)));
        Assert.Equal(
            "gave up: the repository's supervisor: no family is free before the reset; the machine's supervisor: no account of another maker is signed in",
            waits.Said());
    }

    [Fact]
    public void Each_level_is_named_by_the_supervisor_holding_it_and_none_says_lead_or_head()
    {
        Assert.Equal(
            ["the repository's supervisor", "the workspace's supervisor", "the machine's supervisor", "the owner"],
            new[] { LineLevels.Repository, LineLevels.Workspace, LineLevels.Machine, LineLevels.Owner }.Select(LineLevels.Said));
        Assert.Equal(LineLevels.Workspace, LineLevels.Of(new RoleScope(RoleScope.Workspace, Games)));
        Assert.Equal(LineLevels.Machine, LineLevels.Of(RoleScope.Everywhere));
        Assert.Equal(LineLevels.Workspace, LineLevels.Below(new RolePlace(Workspace: Games)));
    }

    [Fact]
    public void The_owner_class_tags_are_doctrine_rule_ones_closed_list()
    {
        Assert.Equal(["direction", "money", "sign-in", "release", "history", "data", "publish"], OwnerClass.Kinds);
    }
}
