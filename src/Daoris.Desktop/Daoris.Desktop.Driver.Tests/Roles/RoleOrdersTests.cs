using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one reader of a role's order for every line but a start's own walk: the role a
/// line is bound to, its order as the policy arranges it, and an arrangement kept to one agent's alternatives where a rule names the agents.
/// </summary>
public sealed class RoleOrdersTests : IDisposable
{
    private static readonly ActorChoice One = new("cool", "m1", null);

    private static readonly ActorChoice Two = new("fine", "m2", null);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "roleorders-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly RoleStaffing _staffing;

    private readonly StaffLedger _ledger;

    public RoleOrdersTests()
    {
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{ "adapter": "cool", "actors": { "build": [ { "agent": "cool", "model": "m1" }, { "agent": "fine", "model": "m2" } ] } }""");
        _staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.Equal(StaffingImported.Done, _staffing.ImportFile().How);
        _ledger = new StaffLedger(_home);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private DriverConfig Config() => StaffingProjection.Config(_staffing.Read()!);

    private void Policy(string json) =>
        RoleDoor.Apply(_staffing, RoleChange.Set(RoleCatalog.Seed[0]), null, null, new RolePolicyPress(StaffPolicy.Parse(json).Policy!));

    [Fact]
    public void A_role_under_no_policy_is_its_stored_order_and_a_weighted_one_is_arranged_by_weight()
    {
        var stored = RoleOrders.Arranged(_ledger, "build", "build", Config());
        Assert.Equal(["1", "2"], stored.Select(each => each.From));

        Policy("""{"rule":"weight","choices":[{"agent":"cool","model":"m1","weight":1},{"agent":"fine","model":"m2","weight":3}]}""");

        Assert.Equal(["2", "1"], RoleOrders.Arranged(_ledger, "build", "build", Config()).Select(each => each.From));
        Assert.Equal(["1", "2"], RoleOrders.Arranged(null, "build", "build", Config()).Select(each => each.From));
    }

    [Fact]
    public void A_choice_not_taking_the_work_is_left_out_of_the_order_a_line_goes_on_in()
    {
        Policy("""{"rule":"order","choices":[{"agent":"cool","model":"m1","takes":["design"]},{"agent":"fine","model":"m2","takes":["build"]}]}""");

        Assert.Equal(["2"], RoleOrders.Arranged(_ledger, "build", "build", Config()).Select(each => each.From));
    }

    [Fact]
    public void The_role_a_line_is_bound_to_is_read_while_the_catalogue_holds_it_else_the_role_taking_the_work_now()
    {
        var catalogue = RoleCatalog.ReadHome(_home);

        Assert.Equal("build", RoleOrders.Bound("build", "build", catalogue, new RolePlace()));
        Assert.Equal("chat", RoleOrders.Bound(null, "chat", catalogue, new RolePlace()));
        Assert.Equal("chat", RoleOrders.Bound("a-removed-role", "chat", catalogue, new RolePlace()));
    }

    [Fact]
    public void A_hand_overs_approved_choice_is_held_while_the_role_still_lists_it_and_never_swapped_for_another()
    {
        var config = Config();

        Assert.True(RoleOrders.Holds("build", config, "fine", "m2", null));
        Assert.False(RoleOrders.Holds("build", config, "fine", "m3", null));
        Assert.False(RoleOrders.Holds("build", config, "fine", "m2", "high"));
    }

    [Fact]
    public void A_weight_only_orders_one_agents_alternatives_and_the_agents_stay_the_rules()
    {
        ActorEffective A1 = new("a", "m1", null, "1"), B1 = new("b", "m1", null, "2"), A2 = new("a", "m2", null, "3");
        IReadOnlyList<ActorEffective> asked = [A1, A2, B1];

        // The policy would put A2 first and B1 last: a's two entries still come before b's, in the arranged order between them.
        Assert.Equal([A2, A1, B1], RoleOrders.WithinAgents(asked, [A2, B1, A1]));
        // An entry the arrangement left out stays out; an unchanged arrangement is the stored order exactly.
        Assert.Equal([A1, B1], RoleOrders.WithinAgents(asked, [A1, B1]));
        IReadOnlyList<ActorEffective> interleaved = [A1, B1, A2];
        Assert.Same(interleaved, RoleOrders.WithinAgents(interleaved, interleaved));
    }
}
