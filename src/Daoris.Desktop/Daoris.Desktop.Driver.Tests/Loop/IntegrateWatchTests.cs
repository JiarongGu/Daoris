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
public sealed class IntegrateWatchTests(ITestOutputHelper output) : IDisposable
{
    private const string T1 = "1111111111111111111111111111111111111111";

    private const string Rules = """
        { "drivable": ["engine", "shop"], "adapter": "stub", "cap": 3, "pollSeconds": 600, "trees": ["engine", "shop"],
          "landings": {
            "engine": { "form": "branch", "pattern": "feature/{quest}-{slug}", "integrate": { "request": "owner" } },
            "shop": { "form": "branch", "pattern": "feature/{quest}-{slug}" } } }
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

    private void Land(string repository, string branch, string tip = T1) =>
        new LandedBranches(_home).Record(new LandedBranch(repository, "default", branch, "main", tip, "s-" + branch, "q1", null, Then)
        {
            AcceptedBy = AcceptedBy.Person,
            Integrate = new LandedIntegrate(LandingIntegrate.Owner, LandedIntegrate.Rule),
        });

    private static DriverConfig Plain(params string[] repositories) =>
        repositories.Aggregate(DriverConfig.Empty, (config, repository) => config.WithLanding(repository, new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}")));

    private static DriverConfig Integrating(string repository) =>
        Plain().WithLanding(repository, new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"))
            .WithLandingIntegrate(repository, new LandingIntegrate(Request: LandingIntegrate.Owner));

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
        Land("engine", "feature/q1-ship");
        Land("shop", "feature/q2-ship");

        // The runtime writes its records only where this process holds the home, as the desktop does while it runs.
        using var desktop = HomeLease.TryAcquire(_home, LeaseKind.Desktop, out _);
        Assert.NotNull(desktop);
        var passes = 0;
        var said = new List<string>();
        await using var hooks = new HookSet(_home, say: (_, _) => { });
        using var log = new MachineLog(_home, "driver");
        var watch = new DriverWatch(_ledger.Client(), config, _home, new SessionProcesses(), sync: null, hooks: hooks,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
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
            Assert.Equal(("engine", "feature/q1-ship", T1, IntegrationRequester.NodeStart), (ask.Repository, ask.Branch, ask.Tip, ask.Requester));
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
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses))) { Log = log };
        var watching = watch.RunAsync((_, _) => Task.CompletedTask, onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => watch.Nodes is not null, () => "the watch built no runtime", Poll.Patience);
            Assert.IsType<HomeIntegrationDoor>(watch.Nodes!.Integration);
        }
        finally
        {
            await _closing.CancelAsync();
            try { await watching.WaitAsync(Poll.Patience); }
            catch (OperationCanceledException) { }
        }
    }

    /// <summary>Only a choice naming nothing but <c>integrate</c> reads narrowed: a plugin's node, or a cycle that acts, needs every repository's units.</summary>
    [Fact]
    public void The_read_is_narrowed_only_where_integrate_alone_arms_the_runtime()
    {
        var runtime = new NodeRuntime(new NodeRecordFiles(_home), new ObservedPort(), () => Then, () => true);
        Assert.True(NodeRuntime.IntegratesAlone(Integrating("engine")));
        Assert.True(runtime.ReadsIntegratingOnly(Integrating("engine")));
        Assert.False(NodeRuntime.IntegratesAlone(Plain("engine")));
        Assert.False(runtime.ReadsIntegratingOnly(Plain("engine")));

        var cycle = DriverConfig.Parse("""
            { "landings": { "engine": { "form": "branch", "pattern": "feature/{quest}", "integrate": { "request": "owner" } } },
              "workCycles": { "shop": { "quest": { "schemaVersion": 2, "entries": ["deploy"], "end": "deploy",
                "nodes": [{ "id": "deploy", "kind": "acme.deploy/deploy", "version": 1, "reach": "performed" }], "wires": [] } } } }
            """);
        Assert.False(NodeRuntime.IntegratesAlone(cycle));
        Assert.False(runtime.ReadsIntegratingOnly(cycle));

        Assert.True(NodeRuntime.Integrating(Integrating("engine"), "engine", "default"));
        Assert.False(NodeRuntime.Integrating(Integrating("engine"), "shop", "default"));
    }

    /// <summary>
    /// F51, measured: over forty repositories of three landed branches each, one carrying <c>integrate</c>, the look's inventory read names only
    /// that repository's three branch units and asks the service for no quest or ask; the read with no narrowing names every unit and asks.
    /// </summary>
    [Fact]
    public async Task The_look_reads_only_the_integrating_repositories_branch_units_and_the_cost_is_measured()
    {
        Directory.CreateDirectory(_home);
        var repositories = Enumerable.Range(0, 40).Select(index => index == 0 ? "engine" : $"repo-{index}").ToList();
        foreach (var repository in repositories)
        {
            _ledger.Register(repository, Path.Combine(_home, repository));
            for (var branch = 0; branch < 3; branch++) Land(repository, $"feature/q{branch}-ship");
        }

        var config = repositories.Aggregate(Integrating("engine"), (all, repository) =>
            repository == "engine" ? all : all.WithLanding(repository, new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}")));
        var client = _ledger.Client();
        var asked = 0;
        var quests = 0;
        _ledger.Before = (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath is "/api/quests" or "/api/asks" or "/api/sessions") Interlocked.Increment(ref quests);
            return Task.CompletedTask;
        };

        var clock = Stopwatch.StartNew();
        var narrowed = await DriverWatch.ReadNodeUnitsAsync(_home, client, CancellationToken.None, narrowedTo: config);
        var narrowedIn = clock.Elapsed;
        asked = Volatile.Read(ref quests);
        clock.Restart();
        var full = await DriverWatch.ReadNodeUnitsAsync(_home, client, CancellationToken.None);
        var fullIn = clock.Elapsed;

        Assert.Equal(3, narrowed.Units.Count);
        Assert.All(narrowed.Units, unit => Assert.Equal(("engine", WorkUnitKind.Branch), (unit.Owner.Repository, unit.Kind)));
        Assert.Equal(0, asked);
        Assert.Equal(120, full.Units.Count(unit => unit.Kind == WorkUnitKind.Branch));
        Assert.True(Volatile.Read(ref quests) > 0);
        Assert.Equal(40, narrowed.Roots.Count);
        output.WriteLine($"inventory read over 40 repositories: narrowed {narrowedIn.TotalMilliseconds:F0} ms for {narrowed.Units.Count} units, full {fullIn.TotalMilliseconds:F0} ms for {full.Units.Count} units");
    }
}
