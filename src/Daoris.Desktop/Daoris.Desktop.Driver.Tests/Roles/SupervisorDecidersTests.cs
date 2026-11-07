using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>ROLES</c>' <c>deciders</c> answer, for every place a supervisor or the repositories' map names, who decides each kind there by the
/// one resolver, with the acts of the kind its grants cover, so the page's *Who decides here* reads the resolution and judges nothing.
/// </summary>
public sealed class SupervisorDecidersTests
{
    private const string Games = "games";
    private const string Engine = "engine";

    private static readonly RuleWiderPlaces Placed = RuleWiderPlaces.From([(Engine, Games)]);

    private static RoleSlot Supervisor(string id, RoleScope scope, IReadOnlyList<string> decides, IReadOnlyList<string> acts, string holder = RoleHolders.Terminal)
    {
        var role = new RoleRecord(id, AuthoredText.Of(id), RoleRecord.Appointed, [], scope,
            new RoleHold(AppointedRoles.Supervisor, acts, holder) { Decides = decides });
        return new(id, RoleState.Set, role, null, 1, scope, Appointed: true);
    }

    private static readonly RoleSlot Machine = Supervisor(AppointedRoles.Supervisor, RoleScope.Everywhere, [], SupervisorKinds.Seeded, RoleHolders.Rules);

    private static DeciderLine Line(Deciders deciders, string place, string kind) =>
        deciders.Places.Single(each => each.Place == place).Kinds.Single(each => each.Kind == kind);

    [Fact]
    public void Each_place_a_supervisor_or_the_map_names_reads_each_kind_by_the_resolver_with_the_kind_s_granted_acts()
    {
        RoleSlot[] slots =
        [
            Machine,
            Supervisor("gatekeeper", new RoleScope(RoleScope.Workspace, Games), [SupervisorKinds.Allow], ["allow.once"]),
            Supervisor("lander", new RoleScope(RoleScope.Repository, "atlas"), [SupervisorKinds.Landing], []),
        ];

        var deciders = SupervisorDeciders.Of(slots, Placed);

        Assert.Equal(["machine", "workspace:games", "repository:atlas", "repository:engine"], deciders.Places.Select(each => each.Place));
        Assert.All(deciders.Places, each => Assert.Equal(SupervisorKinds.All, each.Kinds.Select(line => line.Kind)));
        Assert.Equal(
            new DeciderLine(SupervisorKinds.Allow, "gatekeeper", "workspace:games", RoleHolders.Terminal, ["allow.once"], null),
            Line(deciders, "repository:engine", SupervisorKinds.Allow), Comparer);
        Assert.Equal(
            new DeciderLine(SupervisorKinds.Question, AppointedRoles.Supervisor, "machine", RoleHolders.Rules, ["question.answer"], null),
            Line(deciders, "repository:engine", SupervisorKinds.Question), Comparer);
        Assert.Equal(
            new DeciderLine(SupervisorKinds.Landing, "lander", "repository:atlas", RoleHolders.Terminal, [], null),
            Line(deciders, "repository:atlas", SupervisorKinds.Landing), Comparer);
        Assert.Equal(AppointedRoles.Supervisor, Line(deciders, "machine", SupervisorKinds.Allow).Role);
        Assert.All(deciders.Elsewhere, line => Assert.Equal(AppointedRoles.Supervisor, line.Role));
    }

    [Fact]
    public void A_map_that_does_not_read_makes_a_repository_no_place_names_the_person_s_said_and_the_machine_still_its_own()
    {
        var deciders = SupervisorDeciders.Of([Machine], RuleWiderPlaces.Unread("the store is locked"));

        Assert.Equal(["machine"], deciders.Places.Select(each => each.Place));
        Assert.All(deciders.Elsewhere, line =>
        {
            Assert.Null(line.Role);
            Assert.Contains("the store is locked", line.Held, StringComparison.Ordinal);
        });
        Assert.Equal(AppointedRoles.Supervisor, Line(deciders, "machine", SupervisorKinds.Question).Role);
    }

    private static readonly IEqualityComparer<DeciderLine> Comparer = EqualityComparer<DeciderLine>.Create(
        (a, b) => a!.Kind == b!.Kind && a.Role == b.Role && a.Scope == b.Scope && a.Holder == b.Holder && a.Held == b.Held && a.Granted.SequenceEqual(b.Granted),
        line => line.Kind.GetHashCode(StringComparison.Ordinal));
}
