using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1n part n3 (D215's note; the integration design §17.4, F51): the watch wires the <c>integrate</c> node. A real watch's look, over a
/// stand-in door, asks once for a landed branch under a rule carrying <c>integrate</c> and never for one without it; an older build's runtime
/// (no door set) asks nothing; and the look's inventory read names only the integrating repositories' branch units, with none of the service's
/// quests or asks read for them, its cost measured over many repositories.
/// </summary>
public sealed class IntegrateWatchTests : IDisposable
{
    private const string T1 = "1111111111111111111111111111111111111111";

    private const string Rules = """
        { "drivable": ["engine", "shop"], "adapter": "stub", "cap": 3, "pollSeconds": 600, "trees": ["engine", "shop"] }
        """;

    private static readonly DateTimeOffset Then = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-integrate-watch-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

    private readonly Door _door = new();

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A repository at <paramref name="root"/> with one commit and <paramref name="branch"/> on it: the branch's tip.</summary>
    private static async Task<string> BranchAsync(string root, string branch)
    {
        Directory.CreateDirectory(root);
        foreach (var arguments in new[]
        {
            new[] { "init", "--quiet", "-b", "main" }, new[] { "config", "user.email", "fixture@example.test" }, new[] { "config", "user.name", "Fixture" },
            new[] { "commit", "--quiet", "--allow-empty", "-m", "first" }, new[] { "branch", branch },
        })
        {
            var run = await GitFixture.RunAsync(root, arguments);
            Assert.True(run.ExitCode == 0, run.Stderr);
        }

        return (await GitFixture.GitAsync(root, "rev-parse", branch)).Trim();
    }

    private void Land(string repository, string branch, string quest, string tip = T1) =>
        new LandedBranches(_home).Record(new LandedBranch(repository, "default", branch, "main", tip, "s-" + branch, quest, null, Then)
        {
            AcceptedBy = AcceptedBy.Person,
            Integrate = new LandedIntegrate(LandingIntegrate.Owner, LandedIntegrate.Version),
        });

    /// <summary>The ask a unit's node makes, admitted as the door admits it: a request kept for the unit and the tip, waiting queued.</summary>
    private sealed class Door : IIntegrationDoor
    {
        private readonly object _gate = new();

        private readonly Dictionary<(string Unit, string Tip), IntegrationRequest> _requests = [];

        public List<IntegrationAsk> Asked { get; } = [];

        public int AskedCount
        {
            get { lock (_gate) return Asked.Count; }
        }

        public Task<IntegrationRequest?> FindAsync(string unit, string tip, CancellationToken ct)
        {
            lock (_gate) return Task.FromResult(_requests.GetValueOrDefault((unit, tip)));
        }

        public Task<IntegrationAdmission> AskAsync(IntegrationAsk ask, DriverConfig config, CancellationToken ct)
        {
            lock (_gate)
            {
                Asked.Add(ask);
                var request = new IntegrationRequest($"request-{_requests.Count + 1}", ask.Repository, "default", ask.Branch, ask.Tip!, IntegrationRoutes.Node, "main",
                    ask.Requester, new LandingIntegrate().Resolved(), Then) { Unit = ask.Unit };
                _requests[(ask.Unit!, ask.Tip!)] = request;
                return Task.FromResult(new IntegrationAdmission(true, "requested") { Request = request });
            }
        }

        public Task<bool> PromotedAsync(IntegrationRequest request, CancellationToken ct) => Task.FromResult(false);

        public Task<bool> DropAsync(IntegrationRequest request, CancellationToken ct) => Task.FromResult(false);
    }

    /// <summary>
    /// A watch started over the home runs one look and its pass: the landed branch of the repository whose rule carries <c>integrate</c> is asked
    /// of the door once, however many wakes follow, and the other repository's landed branch is never asked. Seen failing first: the watch set no
    /// door, so nothing asked.
    /// </summary>
    [Fact]
    public async Task A_watchs_look_asks_the_door_once_for_a_landed_branch_under_a_rule_carrying_integrate_and_never_for_one_without_it()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine")).Register("shop", Path.Combine(_home, "shop"));
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, Rules);
        // The graph judges a landing on the branch's tip in the checkout, which the watch reads from git.
        var tip = await BranchAsync(Path.Combine(_home, "engine"), "feature/q1-ship");
        // Quest q1's run is bound to a graph whose integrate asks with the owner; the shop's landing serves a quest with no run.
        _ledger.Publish("q1", "engine");
        _ledger.Move("q1", "Done");
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, IntegratingWorkflow.Graph(LandingIntegrate.Owner, LandingIntegrate.Session)), Then);
        Land("engine", "feature/q1-ship", "q1", tip);
        Land("shop", "feature/q2-ship", "q2");

        // The runtime writes its records only where this process holds the home, as the desktop does while it runs.
        using var desktop = HomeLease.TryAcquire(_home, LeaseKind.Desktop, out _);
        Assert.NotNull(desktop);
        var passes = 0;
        var said = new List<string>();
        await using var hooks = new HookSet(_home, say: (_, _) => { });
        using var log = new MachineLog(_home, "driver");
        var watch = new DriverWatch(_ledger.Client(), config, _home, new SessionProcesses(), sync: null, hooks: hooks,
            harnesses: new HarnessRoster(ProviderRegistry.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Log = log,
            IntegrationDoor = _door,
            Passed = pass =>
            {
                Interlocked.Increment(ref passes);
                lock (said)
                {
                    said.Add($"armed={pass.Armed} evaluated=[{string.Join(",", pass.Evaluated)}] performed=[{string.Join(",", pass.Performed.Select(done => done.Node + ":" + done.What))}]");
                    said.AddRange(pass.Said);
                }
            },
        };
        var watching = watch.RunAsync((_, _) => Task.CompletedTask, onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => _door.AskedCount >= 1, () =>
            {
                lock (said) return $"the look asked the door nothing ({Volatile.Read(ref passes)} passes): {string.Join(" | ", said)}";
            }, Poll.Patience);
            await Poll.Until(() => !watch.Running.Passing, () => "the start's pass never ended", Poll.Patience);
            var before = Volatile.Read(ref passes);
            watch.NodesWoken("due");
            await Poll.Until(() => Volatile.Read(ref passes) > before, () => "the wake ran no pass", Poll.Patience);
            await Poll.Until(() => !watch.Running.Passing, () => "the wake's pass never ended", Poll.Patience);

            var ask = Assert.Single(_door.Asked);
            Assert.Equal(("engine", "feature/q1-ship", tip, IntegrationRequester.NodeStart), (ask.Repository, ask.Branch, ask.Tip, ask.Requester));
            Assert.Equal(RunUnits.IdOf("q1"), ask.Unit);
        }
        finally
        {
            await _closing.CancelAsync();
            try { await watching.WaitAsync(Poll.Patience); }
            catch (OperationCanceledException) { }
        }
    }

    /// <summary>With no door handed in, the watch's runtime is given the home's own, so an install that holds the node asks the real one.</summary>
    [Fact]
    public async Task The_watchs_runtime_is_given_the_homes_door_by_default()
    {
        Directory.CreateDirectory(_home);
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, Rules);
        await using var hooks = new HookSet(_home, say: (_, _) => { });
        using var log = new MachineLog(_home, "driver");
        var watch = new DriverWatch(_ledger.Client(), config, _home, new SessionProcesses(), sync: null, hooks: hooks,
            harnesses: new HarnessRoster(ProviderRegistry.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses))) { Log = log };
        var watching = watch.RunAsync((_, _) => Task.CompletedTask, onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => watch.Nodes is not null, () => "the watch built no runtime", Poll.Patience);
            Assert.IsType<HomeIntegrationDoor>(watch.Nodes!.Doors.Door<IIntegrationDoor>());
        }
        finally
        {
            await _closing.CancelAsync();
            try { await watching.WaitAsync(Poll.Patience); }
            catch (OperationCanceledException) { }
        }
    }
}
