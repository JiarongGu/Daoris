using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Who holds which running instance of an environment. A session whose inputs equal the line's leases the one shared
/// instance; a session whose branch changes them leases an instance of its own, keyed by the branch, which no other branch is moved onto;
/// an instance no lease holds goes after its grace; and the admission counts the instances, never the leases.
/// </summary>
public sealed class EnvironmentLedgerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
    private static readonly EnvironmentSpec Server = new("game-server", ["server/**"], "run-server", ["http", "admin"], TimeSpan.FromMinutes(5));

    private static EnvironmentLedger Empty() => EnvironmentLedger.Empty(new PortPool(9000, 8));

    private static EnvironmentUse Uses(string session, string branch = "main", string line = "v1", string? own = null, string fork = "v1") =>
        new(session, Server, branch, line, own ?? fork, fork);

    [Fact]
    public void Two_sessions_whose_inputs_are_the_lines_lease_one_shared_instance_and_it_starts_once()
    {
        var first = Empty().Lease(Uses("a", "daoris/a"), T0, capacity: 4);
        var second = first.Ledger.Lease(Uses("b", "daoris/b"), T0, capacity: 4);

        Assert.True(first.Starts);
        Assert.False(second.Starts);
        Assert.Equal(first.Instance, second.Instance);
        Assert.True(first.Instance!.Shared);
        Assert.Equal("v1", first.Instance.Version);
        Assert.Equal(["a", "b"], second.Ledger.Holders(first.Instance.Id));
        Assert.Equal(1, second.Ledger.Count);
    }

    [Fact]
    public void A_session_whose_branch_changes_the_inputs_leases_an_instance_of_its_own_on_other_ports_and_the_shared_one_is_untouched()
    {
        var shared = Empty().Lease(Uses("a"), T0, 4).Ledger.Lease(Uses("b"), T0, 4).Ledger;

        var changing = shared.Lease(Uses("c", "daoris/server-v2", own: "v2"), T0, 4);

        Assert.True(changing.Starts);
        Assert.False(changing.Instance!.Shared);
        Assert.Equal("v2", changing.Instance.Version);
        Assert.Equal("daoris/server-v2", changing.Instance.Scope);
        Assert.NotEqual(shared.Lease(Uses("a"), T0, 4).Instance!.Id, changing.Instance.Id);
        Assert.Empty(changing.Instance.Ports.Values.Intersect(shared.Instances.Single().Ports.Values));
        Assert.Equal(["a", "b"], changing.Ledger.Holders(shared.Instances.Single().Id));
        Assert.Equal(["c"], changing.Ledger.Holders(changing.Instance.Id));
        Assert.Equal(2, changing.Ledger.Count);
    }

    [Fact]
    public void A_second_session_of_the_changing_branch_joins_its_instance_and_a_session_of_another_branch_never_does()
    {
        var own = Empty().Lease(Uses("c", "daoris/server-v2", own: "v2"), T0, 4).Ledger;

        var mate = own.Lease(Uses("d", "daoris/server-v2", own: "v2"), T0, 4);
        var other = mate.Ledger.Lease(Uses("e", "daoris/server-v3", own: "v3"), T0, 4);

        Assert.False(mate.Starts);
        Assert.Equal(["c", "d"], mate.Ledger.Holders(mate.Instance!.Id));
        Assert.True(other.Starts);
        Assert.NotEqual(mate.Instance.Id, other.Instance!.Id);
        Assert.Equal(["e"], other.Ledger.Holders(other.Instance.Id));
    }

    [Fact]
    public void A_branch_that_moves_its_own_inputs_again_restarts_its_instance_at_the_new_version_and_its_sessions_follow()
    {
        var own = Empty().Lease(Uses("c", "daoris/server-v2", own: "v2"), T0, 4).Ledger;

        var again = own.Lease(Uses("c", "daoris/server-v2", own: "v2b"), T0, 4);

        Assert.True(again.Starts);
        Assert.Equal("v2b", again.Instance!.Version);
        Assert.Single(again.Ledger.Instances);
        Assert.Equal(["c"], again.Ledger.Holders(again.Instance.Id));
    }

    [Fact]
    public void A_session_that_leases_again_holds_one_lease_for_the_environment_the_one_it_asked_last()
    {
        var shared = Empty().Lease(Uses("a"), T0, 4).Ledger;
        var changed = shared.Lease(Uses("a", "daoris/x", own: "vx"), T0, 4);

        Assert.Equal(["a"], changed.Ledger.Holders(changed.Instance!.Id));
        Assert.Empty(changed.Ledger.Holders(shared.Instances.Single().Id));

        var back = changed.Ledger.Lease(Uses("a", "daoris/x", own: "v1"), T0, 4);

        Assert.True(back.Instance!.Shared);
        Assert.Equal(["a"], back.Ledger.Holders(back.Instance.Id));
    }

    [Fact]
    public void An_instance_nobody_holds_is_stopped_after_its_grace_and_not_before_and_a_lease_in_the_grace_keeps_it()
    {
        var held = Empty().Lease(Uses("a"), T0, 4).Ledger.Lease(Uses("b"), T0, 4).Ledger;
        var released = held.Release("a", T0.AddMinutes(1)).Release("b", T0.AddMinutes(2));

        Assert.Empty(released.Sweep(T0.AddMinutes(2) + TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1)).Stopped);

        var back = released.Lease(Uses("c"), T0.AddMinutes(5), 4);
        Assert.False(back.Starts);
        Assert.Empty(back.Ledger.Sweep(T0.AddHours(3)).Stopped);

        var late = released.Sweep(T0.AddMinutes(7));
        Assert.Equal([held.Instances.Single().Id], late.Stopped.Select(each => each.Id));
        Assert.Empty(late.Ledger.Instances);
        Assert.Equal(0, late.Ledger.Count);
    }

    [Fact]
    public void The_admission_counts_instances_never_leases_and_a_new_instance_past_the_capacity_is_refused_while_a_lease_of_a_running_one_is_not()
    {
        var two = Empty().Lease(Uses("a"), T0, 2).Ledger.Lease(Uses("b"), T0, 2).Ledger.Lease(Uses("c", "x", own: "v2"), T0, 2).Ledger;

        Assert.Equal(2, two.Count);
        Assert.Equal(3, two.Leases.Count);

        var refused = two.Lease(Uses("d", "y", own: "v3"), T0, capacity: 2);
        var joined = two.Lease(Uses("e"), T0, capacity: 2);

        Assert.Null(refused.Instance);
        Assert.Equal(EnvironmentRefusal.Instances, refused.Refusal);
        Assert.Equal(two, refused.Ledger);
        Assert.Null(joined.Refusal);
        Assert.False(joined.Starts);
    }

    [Fact]
    public void A_pool_with_too_few_ports_for_the_environment_refuses_it_and_a_stopped_instance_gives_its_ports_back()
    {
        var small = EnvironmentLedger.Empty(new PortPool(9000, 3));
        var one = small.Lease(Uses("a"), T0, 4);

        Assert.Equal(EnvironmentRefusal.Ports, one.Ledger.Lease(Uses("b", "x", own: "v2"), T0, 4).Refusal);

        var gone = one.Ledger.Release("a", T0).Sweep(T0.AddHours(1)).Ledger;
        var again = gone.Lease(Uses("b", "x", own: "v2"), T0.AddHours(1), 4);

        Assert.Null(again.Refusal);
        Assert.Equal(new Dictionary<string, int> { ["http"] = 9000, ["admin"] = 9001 }, again.Instance!.Ports);
    }

    [Fact]
    public void An_idle_shared_instance_leased_at_a_newer_line_version_keeps_the_version_it_runs_and_is_not_restarted()
    {
        var idle = Empty().Lease(Uses("a"), T0, 4).Ledger.Release("a", T0);

        var again = idle.Lease(new EnvironmentUse("b", Server, "main", "v2", "v1", "v1"), T0.AddMinutes(1), 4);

        Assert.False(again.Starts);
        Assert.Equal("v1", again.Instance!.Version);
        Assert.Null(again.Instance.IdleSince);
        Assert.Equal(["b"], again.Ledger.Holders(again.Instance.Id));
    }

    [Fact]
    public void A_branch_that_only_lags_a_landing_that_moved_the_inputs_leases_the_shared_instance_while_one_that_changed_them_itself_does_not()
    {
        var lagging = new EnvironmentUse("a", Server, "daoris/client", LineVersion: "v2", BranchVersion: "v1", ForkVersion: "v1");
        var changed = new EnvironmentUse("b", Server, "daoris/server", LineVersion: "v2", BranchVersion: "v3", ForkVersion: "v1");
        var caughtUp = new EnvironmentUse("c", Server, "daoris/same", LineVersion: "v2", BranchVersion: "v2", ForkVersion: "v1");

        Assert.False(lagging.Changes);
        Assert.True(changed.Changes);
        Assert.False(caughtUp.Changes);
        Assert.True(Empty().Lease(lagging, T0, 4).Instance!.Shared);
    }

    [Fact]
    public void A_version_that_adds_or_renames_a_port_is_another_version()
    {
        var ids = new Dictionary<string, string> { ["server/**"] = "t" };

        Assert.NotEqual(EnvironmentVersion.Of(Server, ids), EnvironmentVersion.Of(Server with { Ports = ["http", "metrics"] }, ids));
    }

    [Fact]
    public void A_full_admission_takes_out_the_instance_idle_longest_to_make_room_and_never_one_a_lease_holds()
    {
        var full = Empty().Lease(Uses("a"), T0, 2).Ledger.Lease(Uses("b", "x", own: "v2"), T0, 2).Ledger.Release("a", T0.AddMinutes(1)).Release("b", T0.AddMinutes(2));

        var made = full.Lease(Uses("c", "y", own: "v3"), T0.AddMinutes(3), 2);

        Assert.Null(made.Refusal);
        Assert.Equal(["game-server"], made.Evicted.Select(each => each.Id));
        Assert.Equal(2, made.Ledger.Count);

        var held = Empty().Lease(Uses("a"), T0, 1).Ledger;
        Assert.Equal(EnvironmentRefusal.Instances, held.Lease(Uses("c", "y", own: "v3"), T0, 1).Refusal);
    }
}
