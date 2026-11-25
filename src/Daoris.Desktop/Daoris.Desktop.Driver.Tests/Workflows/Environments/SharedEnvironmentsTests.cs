using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The first slice of shared environments, proved on a stand-in server: two agents of one repository share one running instance, a third whose branch
/// changes the environment gets its own, and what no lease holds is stopped after its grace. The processes are a fake that records what
/// it is asked to start and stop; the real process host is a later slice.
/// </summary>
public sealed class SharedEnvironmentsTests
{
    private static readonly EnvironmentSpec Server = new("game-server", ["server/**"], "run-server", ["http"], TimeSpan.FromMinutes(5));
    private static readonly PortPool Pool = new(9000, 8);

    private sealed class FakeProcesses : IEnvironmentProcesses
    {
        public List<string> Started { get; } = [];
        public List<string> Stopped { get; } = [];
        public bool FailNextStart { get; set; }
        public string? HangStartOf { get; set; }
        public string? FailStopOf { get; set; }

        public async Task StartAsync(EnvironmentInstance instance, EnvironmentSpec spec, CancellationToken cancel)
        {
            if (FailNextStart)
            {
                FailNextStart = false;
                throw new InvalidOperationException("the server did not come up");
            }

            if (instance.Id == HangStartOf) await Task.Delay(Timeout.Infinite, cancel);
            lock (Started) Started.Add($"{instance.Id} {instance.Version} {string.Join(",", instance.Ports.Select(each => $"{each.Key}={each.Value}"))}");
        }

        public Task StopAsync(EnvironmentInstance instance, CancellationToken cancel)
        {
            if (instance.Id == FailStopOf) throw new InvalidOperationException("would not stop");
            lock (Stopped) Stopped.Add(instance.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static EnvironmentUse Uses(string session, string branch, string own = "v1") => new(session, Server, branch, "v1", own, "v1");

    [Fact]
    public async Task Two_agents_of_one_repository_share_one_instance_and_a_third_whose_branch_changes_it_gets_its_own()
    {
        var processes = new FakeProcesses();
        var environments = new SharedEnvironments(processes, new Clock(new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero)), Pool, capacity: 4);

        var first = await environments.LeaseAsync(Uses("client-1", "daoris/client-1"), default);
        var second = await environments.LeaseAsync(Uses("client-2", "daoris/gameplay"), default);
        var third = await environments.LeaseAsync(Uses("server-1", "daoris/server-v2", own: "v2"), default);

        Assert.Equal(first.Instance, second.Instance);
        Assert.Equal(9000, first.Instance!.Ports["http"]);
        Assert.NotEqual(first.Instance.Id, third.Instance!.Id);
        Assert.Equal(9001, third.Instance.Ports["http"]);
        Assert.Equal(["game-server v1 http=9000", "game-server@daoris/server-v2 v2 http=9001"], processes.Started);
        Assert.Equal(2, environments.Running);
        Assert.Equal(["client-1", "client-2"], environments.Holders(first.Instance.Id));
        Assert.Equal(["server-1"], environments.Holders(third.Instance.Id));
    }

    [Fact]
    public async Task What_no_lease_holds_is_stopped_after_its_grace_and_what_one_still_holds_is_not()
    {
        var processes = new FakeProcesses();
        var clock = new Clock(new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero));
        var environments = new SharedEnvironments(processes, clock, Pool, capacity: 4);
        await environments.LeaseAsync(Uses("client-1", "daoris/client-1"), default);
        await environments.LeaseAsync(Uses("server-1", "daoris/server-v2", own: "v2"), default);

        await environments.ReleaseAsync("client-1", default);
        clock.Now = clock.Now.AddMinutes(4);
        await environments.SweepAsync(default);
        Assert.Empty(processes.Stopped);

        clock.Now = clock.Now.AddMinutes(2);
        await environments.SweepAsync(default);

        Assert.Equal(["game-server"], processes.Stopped);
        Assert.Equal(1, environments.Running);
    }

    [Fact]
    public async Task A_start_that_fails_leaves_no_instance_and_no_lease_behind_and_the_next_lease_starts_it_again()
    {
        var processes = new FakeProcesses { FailNextStart = true };
        var environments = new SharedEnvironments(processes, new Clock(new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero)), Pool, capacity: 4);

        var failed = await environments.LeaseAsync(Uses("client-1", "daoris/client-1"), default);

        Assert.Null(failed.Instance);
        Assert.Equal(EnvironmentOutcome.StartFailed, failed.Outcome);
        Assert.Contains("did not come up", failed.Said, StringComparison.Ordinal);
        Assert.Equal(0, environments.Running);

        var again = await environments.LeaseAsync(Uses("client-1", "daoris/client-1"), default);

        Assert.NotNull(again.Instance);
        Assert.Single(processes.Started);
    }

    [Fact]
    public async Task A_refusal_by_the_admission_is_told_and_starts_nothing()
    {
        var processes = new FakeProcesses();
        var environments = new SharedEnvironments(processes, new Clock(new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero)), Pool, capacity: 1);
        await environments.LeaseAsync(Uses("client-1", "daoris/client-1"), default);

        var refused = await environments.LeaseAsync(Uses("server-1", "daoris/server-v2", own: "v2"), default);

        Assert.Equal(EnvironmentOutcome.Refused, refused.Outcome);
        Assert.Equal(EnvironmentRefusal.Instances, refused.Refusal);
        Assert.Single(processes.Started);
    }

    [Fact]
    public async Task A_start_that_does_not_come_up_by_its_deadline_fails_and_does_not_hold_up_a_lease_of_another_instance()
    {
        var processes = new FakeProcesses { HangStartOf = "game-server@daoris/server-v2" };
        var environments = new SharedEnvironments(processes, new Clock(new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero)), Pool, capacity: 4, startDeadline: TimeSpan.FromMilliseconds(300));

        var slow = environments.LeaseAsync(Uses("server-1", "daoris/server-v2", own: "v2"), default);
        var other = await environments.LeaseAsync(Uses("client-1", "daoris/client-1"), default);
        var failed = await slow;

        Assert.Equal(EnvironmentOutcome.Leased, other.Outcome);
        Assert.Equal(EnvironmentOutcome.StartFailed, failed.Outcome);
        Assert.Equal(1, environments.Running);
    }

    [Fact]
    public async Task A_session_that_joins_an_instance_still_starting_waits_for_it_and_is_told_if_it_fails()
    {
        var processes = new FakeProcesses { HangStartOf = "game-server@daoris/server-v2" };
        var environments = new SharedEnvironments(processes, new Clock(new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero)), Pool, capacity: 4, startDeadline: TimeSpan.FromMilliseconds(300));

        var first = environments.LeaseAsync(Uses("server-1", "daoris/server-v2", own: "v2"), default);
        var mate = environments.LeaseAsync(Uses("server-2", "daoris/server-v2", own: "v2"), default);

        Assert.Equal(EnvironmentOutcome.StartFailed, (await first).Outcome);
        Assert.Equal(EnvironmentOutcome.StartFailed, (await mate).Outcome);
        Assert.Equal(0, environments.Running);
        Assert.Empty(environments.Holders("game-server@daoris/server-v2"));
    }

    [Fact]
    public async Task A_failed_move_of_a_branchs_instance_drops_it_and_its_holders_and_the_next_lease_starts_it_again()
    {
        var processes = new FakeProcesses();
        var environments = new SharedEnvironments(processes, new Clock(new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero)), Pool, capacity: 4);
        await environments.LeaseAsync(Uses("server-1", "daoris/server-v2", own: "v2"), default);
        await environments.LeaseAsync(Uses("server-2", "daoris/server-v2", own: "v2"), default);

        processes.FailNextStart = true;
        var moved = await environments.LeaseAsync(Uses("server-1", "daoris/server-v2", own: "v2b"), default);

        Assert.Equal(EnvironmentOutcome.StartFailed, moved.Outcome);
        Assert.Equal(0, environments.Running);
        Assert.Empty(environments.Holders("game-server@daoris/server-v2"));
        Assert.Equal(EnvironmentOutcome.Leased, (await environments.LeaseAsync(Uses("server-1", "daoris/server-v2", own: "v2b"), default)).Outcome);
    }

    [Fact]
    public async Task A_stop_that_fails_in_a_sweep_is_said_and_the_other_expired_instances_are_still_stopped()
    {
        var processes = new FakeProcesses { FailStopOf = "game-server" };
        var clock = new Clock(new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero));
        var environments = new SharedEnvironments(processes, clock, Pool, capacity: 4);
        await environments.LeaseAsync(Uses("client-1", "daoris/client-1"), default);
        await environments.LeaseAsync(Uses("server-1", "daoris/server-v2", own: "v2"), default);
        await environments.ReleaseAsync("client-1", default);
        await environments.ReleaseAsync("server-1", default);
        clock.Now = clock.Now.AddHours(1);

        var failures = await environments.SweepAsync(default);

        Assert.Equal(["game-server: would not stop"], failures);
        Assert.Equal(["game-server@daoris/server-v2"], processes.Stopped);
        Assert.Equal(0, environments.Running);
    }

    [Fact]
    public async Task A_full_admission_stops_the_instance_idle_longest_to_start_a_new_one()
    {
        var processes = new FakeProcesses();
        var environments = new SharedEnvironments(processes, new Clock(new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero)), Pool, capacity: 1);
        await environments.LeaseAsync(Uses("client-1", "daoris/client-1"), default);
        await environments.ReleaseAsync("client-1", default);

        var made = await environments.LeaseAsync(Uses("server-1", "daoris/server-v2", own: "v2"), default);

        Assert.Equal(EnvironmentOutcome.Leased, made.Outcome);
        Assert.Equal(["game-server"], processes.Stopped);
        Assert.Equal(1, environments.Running);
    }
}
