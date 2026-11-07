using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One resolver answers who decides a decision of a kind at a place, kind first: the narrowest scope's supervisor deciding the kind,
/// else the narrowest generic one, the machine's always standing; an unreadable record or workspace map makes the decision the person's.
/// </summary>
public sealed class SupervisorResolutionTests
{
    private const string Games = "games";
    private const string Engine = "engine";

    private static readonly RuleWiderPlaces Placed = RuleWiderPlaces.From([(Engine, Games)]);

    private static RoleRecord Supervisor(string id, RoleScope scope, IReadOnlyList<string>? decides = null, IReadOnlyList<string>? acts = null) =>
        new(id, AuthoredText.Of(id), RoleRecord.Appointed, [], scope,
            new RoleHold(AppointedRoles.Supervisor, acts ?? (decides is null ? SupervisorKinds.Seeded : []), RoleHolders.Terminal) { Decides = decides ?? [] });

    private static RoleSlot Slot(RoleRecord role) => new(role.Id, RoleState.Set, role, null, 1, role.Scope, Appointed: true);

    private static readonly RoleScope Repository = new(RoleScope.Repository, Engine);
    private static readonly RoleScope Workspace = new(RoleScope.Workspace, Games);

    private static List<RoleRecord> Six() =>
    [
        Supervisor("repository-questions", Repository, [SupervisorKinds.Question]),
        Supervisor("workspace-questions", Workspace, [SupervisorKinds.Question]),
        Supervisor("machine-questions", RoleScope.Everywhere, [SupervisorKinds.Question]),
        Supervisor("repository-every", Repository),
        Supervisor("workspace-every", Workspace),
        Supervisor(AppointedRoles.Supervisor, RoleScope.Everywhere),
    ];

    /// <summary>Who answers at <paramref name="place"/>, each found one taken out in turn, until only the machine's generic one stands.</summary>
    private static List<string> Order(RolePlace place)
    {
        var roles = Six();
        var found = new List<string>();
        while (SupervisorResolution.Find([.. roles.Select(Slot)], SupervisorKinds.Question, place, Placed).Role is { } role)
        {
            found.Add(role.Id);
            if (role.Id == AppointedRoles.Supervisor) break;
            roles.RemoveAll(each => each.Id == role.Id);
        }

        return found;
    }

    [Fact]
    public void Each_of_the_six_steps_is_read_kind_first_at_a_repository_a_workspace_and_the_machine()
    {
        Assert.Equal(
            ["repository-questions", "workspace-questions", "machine-questions", "repository-every", "workspace-every", AppointedRoles.Supervisor],
            Order(new RolePlace(Repository: Engine)));
        Assert.Equal(["workspace-questions", "machine-questions", "workspace-every", AppointedRoles.Supervisor], Order(new RolePlace(Games)));
        Assert.Equal(["machine-questions", AppointedRoles.Supervisor], Order(new RolePlace()));
    }

    [Fact]
    public void A_workspace_s_allow_supervisor_answers_allows_beside_a_repository_s_generic_one_which_answers_the_rest()
    {
        RoleSlot[] slots =
        [
            Slot(Supervisor("repository-every", Repository)),
            Slot(Supervisor("gatekeeper", Workspace, [SupervisorKinds.Allow], ["allow.once"])),
            Slot(Supervisor(AppointedRoles.Supervisor, RoleScope.Everywhere)),
        ];
        var place = new RolePlace(Repository: Engine);
        Assert.Equal("gatekeeper", SupervisorResolution.Find(slots, SupervisorKinds.Allow, place, Placed).Role?.Id);
        Assert.Equal("repository-every", SupervisorResolution.Find(slots, SupervisorKinds.Question, place, Placed).Role?.Id);
    }

    [Fact]
    public void A_repository_s_allow_supervisor_with_nothing_ticked_keeps_its_allows_the_person_s_under_a_workspace_s()
    {
        RoleSlot[] slots =
        [
            Slot(Supervisor("keeper", Repository, [SupervisorKinds.Allow], [])),
            Slot(Supervisor("gatekeeper", Workspace, [SupervisorKinds.Allow], ["allow.once", "allow.reject"])),
            Slot(Supervisor(AppointedRoles.Supervisor, RoleScope.Everywhere)),
        ];
        var found = SupervisorResolution.Find(slots, SupervisorKinds.Allow, new RolePlace(Repository: Engine), Placed).Role!;
        Assert.Equal("keeper", found.Id);
        Assert.False(SupervisorGrants.Covers(found, SupervisorKinds.Allow, "once"));
    }

    [Fact]
    public void An_unreadable_repository_supervisor_makes_that_repository_s_decisions_the_person_s_and_no_other_s()
    {
        RoleSlot[] slots =
        [
            new("broken", RoleState.Unreadable, null, "does not read: a field is missing", 1, Repository, Appointed: true),
            Slot(Supervisor(AppointedRoles.Supervisor, RoleScope.Everywhere)),
        ];
        var held = SupervisorResolution.Find(slots, SupervisorKinds.Question, new RolePlace(Repository: Engine), Placed);
        Assert.Null(held.Role);
        Assert.Contains("`broken` does not read", held.Held, StringComparison.Ordinal);
        Assert.Equal(AppointedRoles.Supervisor, SupervisorResolution.Find(slots, SupervisorKinds.Question, new RolePlace(Repository: "atlas"), Placed).Role?.Id);
    }

    [Fact]
    public void A_workspace_map_that_does_not_read_makes_a_repository_s_decisions_the_person_s_where_one_before_its_step_reads_its_own_chain()
    {
        RoleSlot[] slots = [Slot(Supervisor("workspace-every", Workspace)), Slot(Supervisor(AppointedRoles.Supervisor, RoleScope.Everywhere))];
        var place = new RolePlace(Repository: Engine);

        var held = SupervisorResolution.Find(slots, SupervisorKinds.Question, place, RuleWiderPlaces.Unread("the store is locked"));
        Assert.Null(held.Role);
        Assert.Contains("the store is locked", held.Held, StringComparison.Ordinal);
        Assert.Equal(AppointedRoles.Supervisor, SupervisorResolution.Find(slots, SupervisorKinds.Question, place, RuleWiderPlaces.None).Role?.Id);
        Assert.Equal("workspace-every", SupervisorResolution.Find(slots, SupervisorKinds.Question, place, Placed).Role?.Id);
    }

    [Fact]
    public void The_seed_s_three_grants_cover_today_s_acts_and_nothing_else()
    {
        var seeded = RoleCatalog.Seed.Single(role => role.Id == AppointedRoles.Supervisor);
        Assert.True(SupervisorGrants.Covers(seeded, SupervisorKinds.Question, SupervisorKinds.Answer));
        Assert.True(SupervisorGrants.Covers(seeded, SupervisorKinds.Course, SupervisorKinds.GoOn));
        Assert.True(SupervisorGrants.Covers(seeded, SupervisorKinds.Limit, SupervisorKinds.Move));
        Assert.False(SupervisorGrants.Covers(seeded, SupervisorKinds.Allow, "once"));
        Assert.False(SupervisorGrants.Covers(seeded, SupervisorKinds.Landing, "accept"));
    }
}
