using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A supervisor an agent holds is staffed through the role door as a working role is: its order kept as its own row of
/// <c>role_staffing</c>, its policy a working role's but for works, its row deleted when its holder moves off the agent; the steward
/// takes no agent and no choices.
/// </summary>
public sealed class AppointedStaffingTests : IDisposable
{
    private static readonly ActorChoice Opus = new("claude", "opus", "high");

    private static readonly ActorChoice Sonnet = new("claude", "sonnet", "medium");

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "appointed-staffing", Guid.NewGuid().ToString("N")[..8]);

    public AppointedStaffingTests()
    {
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{ "adapter": "claude", "actors": { "build": [ { "agent": "claude", "model": "sonnet" } ] } }""");
        Staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.Equal(StaffingImported.Done, Staffing.ImportFile().How);
        Assert.Null(HomeMigration.Run(_home, [new SupervisorStep(() => null)]).Failed);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private RoleStaffing Staffing { get; }

    private static RoleRecord Gatekeeper(string holder) =>
        new("gatekeeper", AuthoredText.Of("Gatekeeper"), RoleRecord.Appointed, [], new RoleScope(RoleScope.Repository, "engine"),
            new RoleHold(AppointedRoles.Supervisor, [SupervisorKinds.Qualified(SupervisorKinds.Question, SupervisorKinds.Answer)], holder) { Decides = [SupervisorKinds.Question] });

    private static StaffPolicy Policy(string json) => StaffPolicy.Parse(json).Policy ?? throw new InvalidOperationException("the test's record does not read");

    private IReadOnlyList<ActorChoice>? Row(string role) => Staffing.Read()!.Actors.GetValueOrDefault(role);

    [Fact]
    public void An_agent_held_supervisor_s_choices_are_its_own_row_and_its_holder_moved_off_the_agent_deletes_it()
    {
        RoleDoor.Apply(Staffing, RoleChange.Set(Gatekeeper(RoleHolders.Agent)), [Opus, Sonnet]);
        Assert.Equal([Opus, Sonnet], Row("gatekeeper"));

        // A later set naming no choices keeps the order it has.
        RoleDoor.Apply(Staffing, RoleChange.Set(Gatekeeper(RoleHolders.Agent) with { Hold = Gatekeeper(RoleHolders.Agent).Hold! with { Window = 9 } }));
        Assert.Equal([Opus, Sonnet], Row("gatekeeper"));

        RoleDoor.Apply(Staffing, RoleChange.Set(Gatekeeper(RoleHolders.Terminal)));
        Assert.Null(Row("gatekeeper"));
    }

    /// <summary>Given its seed again, the seeded supervisor is held by the person's rules, as `default supervisor` leaves it: no order, no policy.</summary>
    [Fact]
    public void The_whole_structure_given_its_seed_leaves_a_seeded_supervisor_no_order_and_no_policy()
    {
        var seed = RoleCatalog.Seed.Single(role => role.Id == AppointedRoles.Supervisor);
        var spread = Policy($$"""{"rule":"weight","choices":[{"agent":"claude","model":"opus","effort":"high","weight":3},{"agent":"claude","model":"sonnet","effort":"medium","weight":1}]}""");
        RoleDoor.Apply(Staffing, RoleChange.Set(seed with { Hold = seed.Hold! with { Holder = RoleHolders.Agent } }), [Opus, Sonnet], policy: new RolePolicyPress(spread));
        Assert.Equal([Opus, Sonnet], Row(AppointedRoles.Supervisor));

        RoleDoor.Apply(Staffing, RoleChange.DefaultAll());

        Assert.Equal(RoleHolders.Rules, RoleCatalog.Read(Staffing.Location).Slots.Single(slot => slot.Id == AppointedRoles.Supervisor).Role!.Hold!.Holder);
        Assert.Null(Row(AppointedRoles.Supervisor));
        Assert.Equal(StaffPolicyRead.Default, Staffing.Policy(AppointedRoles.Supervisor).State);
    }

    [Fact]
    public void An_agent_held_supervisor_with_no_choice_and_choices_on_any_other_holder_are_refused_and_nothing_is_written()
    {
        Assert.Equal("choices", Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(Gatekeeper(RoleHolders.Agent)))).Code);
        Assert.Equal("choices", Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(Gatekeeper(RoleHolders.Agent)), [])).Code);
        Assert.Equal("choices", Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(Gatekeeper(RoleHolders.Agent)), [Opus, Opus])).Code);
        Assert.Equal("choices", Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(Gatekeeper(RoleHolders.Agent)), [.. Enumerable.Repeat(Opus, 5)])).Code);
        Assert.Equal("choices", Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(Gatekeeper(RoleHolders.Rules)), [Opus])).Code);
        Assert.Equal("choices", Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(Gatekeeper(RoleHolders.Terminal)), [Opus])).Code);
        var steward = RoleCatalog.Seed.Single(role => role.Id == AppointedRoles.Steward);
        Assert.Equal("appointed-not-configurable", Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(steward), [Opus])).Code);
        Assert.Equal(
            "appointed-not-configurable",
            Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(steward with { Hold = steward.Hold! with { Holder = RoleHolders.Agent } }))).Code);

        Assert.Null(Row("gatekeeper"));
        Assert.Equal(RoleState.Seed, RoleCatalog.Read(Staffing.Location).Slots.Single(slot => slot.Id == AppointedRoles.Steward).State);
    }

    [Fact]
    public void Its_policy_is_a_working_role_s_but_its_choices_name_no_works_and_its_preset_writes_no_rules()
    {
        var spread = Policy($$"""{"rule":"weight","choices":[{"agent":"claude","model":"opus","effort":"high","weight":3},{"agent":"claude","model":"sonnet","effort":"medium","weight":1}]}""");
        RoleDoor.Apply(Staffing, RoleChange.Set(Gatekeeper(RoleHolders.Agent)), [Opus, Sonnet], policy: new RolePolicyPress(spread));
        Assert.Equal(StaffPolicyRead.Set, Staffing.Policy("gatekeeper").State);

        var works = Policy($$"""{"rule":"order","choices":[{"agent":"claude","model":"opus","effort":"high","takes":["build"]}]}""");
        Assert.Equal(
            StaffPolicy.Code,
            Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(Gatekeeper(RoleHolders.Agent)), [Opus, Sonnet], policy: new RolePolicyPress(works))).Code);
        Assert.Equal(
            StaffPolicy.Code,
            Assert.Throws<RoleRefusedException>(() => RoleDoor.Apply(Staffing, RoleChange.Set(Gatekeeper(RoleHolders.Terminal)), null, policy: new RolePolicyPress(spread))).Code);

        RoleDoor.Apply(Staffing, RoleChange.Set(Gatekeeper(RoleHolders.Agent)), [Opus, Sonnet], policy: new RolePolicyPress(Preset: "even"));
        Assert.Empty(new SupervisorRuleStore(_home, () => StoreSettled.Yes).Of("repository:engine").Rules);
    }
}
