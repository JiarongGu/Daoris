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

        public Task StartAsync(EnvironmentInstance instance, EnvironmentSpec spec, CancellationToken cancel)
        {
            if (FailNextStart)
            {
                FailNextStart = false;
                throw new InvalidOperationException("the server did not come up");
            }

            Started.Add($"{instance.Id} {instance.Version} {string.Join(",", instance.Ports.Select(each => $"{each.Key}={each.Value}"))}");
            return Task.CompletedTask;
        }

        public Task StopAsync(EnvironmentInstance instance, CancellationToken cancel)
        {
            Stopped.Add(instance.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static EnvironmentUse Uses(string session, string branch, string own = "v1") => new(session, Server, branch, "v1", own);

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
}
