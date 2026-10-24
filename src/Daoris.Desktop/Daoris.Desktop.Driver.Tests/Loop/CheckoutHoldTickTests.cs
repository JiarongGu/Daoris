using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STEWARDROLE1b (D209 §3.1a, Codex's final read, finding 2) through REAL ticks: a repository whose earlier session worked in its
/// checkout, given trees since and driven by a steward's act, never has its continuation started in the checkout. The start is held
/// <c>held.checkout</c> where its directory is actually chosen, nothing is spawned, and the steward's operations on the quest end; with no
/// role's act behind it the same start carries the quest on as before. A stand-in harness (node) and the stand-in service
/// <see cref="AskAndWaitTickTests.StandIn"/>, as <see cref="StopHoldTickTests"/> uses them; no model and no account.
/// </summary>
/// <remarks>
/// The home is this test's own scratch under the checkout's <c>_fixtures</c>, handed by path: no test sets <c>DAORIS_HOME</c>. The
/// steward's memory is a stand-in <see cref="IRoleStarts"/> set on the driver's context, as the watch sets the judge's at each look.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class CheckoutHoldTickTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "checkout-hold", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    private readonly SessionProcesses _processes = new();

    public CheckoutHoldTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
    }

    public void Dispose() => GitSeed.Remove(_home);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_continuation_a_steward_s_act_caused_is_held_out_of_the_checkout(bool caused)
    {
        await GitSeed.PlainAsync(_repository, "engine");
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var log = Path.Combine(_home, "agent.log");

        // 1. Before trees: the session takes its quest and works in the checkout, and is stopped there with the quest still taken.
        var checkout = Config(log) with { Trees = [] };
        var first = Driver(service, checkout, null).RunOnceAsync();
        await UntilAsync(() => service.SessionCount == 1 && service.Status("q1") == "Taken");
        Assert.True(_processes.Stop("s1"));
        await first.WaitAsync(Poll.Patience);
        Assert.Equal(Path.GetFullPath(_repository), Path.GetFullPath(service.Session("s1")["tree"]!.GetValue<string>()), ignoreCase: true);

        // 2. Trees given since, the quest released, and the repository driven: the next look's start resumes the record's directory.
        var roles = new Roles(caused);
        var given = Config(log).WithReleased("q1", "s1");
        var look = await Driver(service, given, roles).TickAsync().WaitAsync(Poll.Patience);

        if (caused)
        {
            var held = Assert.Single(look.Considerations, c => c.Quest.Id == "q1");
            Assert.Equal(StartVerdict.Blocked, held.Verdict);
            Assert.Equal(Considerations.CheckoutHeld, held.Reason);
            Assert.Equal(SittingFact.Checkout, held.Said?.Code);
            Assert.Equal(1, service.SessionCount);
            Assert.Single(Runs(log));
            Assert.Equal(["q1"], roles.HeldForOwners);
        }
        else
        {
            // The owner's own start runs as before, carrying the quest on where its earlier work is.
            await UntilAsync(() => service.SessionCount == 2 && File.ReadAllLines(log).Length == 2);
            Assert.Equal(Path.GetFullPath(_repository), Path.GetFullPath(Runs(log)[1]["cwd"]!.GetValue<string>()), ignoreCase: true);
            Assert.Empty(roles.HeldForOwners);
            await UntilAsync(() => _processes.Stop("s2"));
        }
    }

    private DriverConfig Config(string log) => DriverConfig.Empty with
    {
        Drivable = ["engine"],
        Trees = ["engine"],
        Adapter = "stub",
        TestBound = 1,
        PollSeconds = 1,
        Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent(), log] },
    };

    // A driver per look, sharing the processes, as the watch builds one per look and hands it the run's memory of a role's acts.
    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, DriverConfig config, IRoleStarts? roles)
    {
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: _processes,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        driver.Context.Roles = roles;
        return driver;
    }

    /// <summary>The run's memory as a start reads it: whether a steward's act caused this start, and the quests it held for the owner.</summary>
    private sealed class Roles(bool caused) : IRoleStarts
    {
        public List<string> HeldForOwners { get; } = [];

        public bool Caused(string repository, string quest) => caused;

        public bool Released(string quest, RoleRelease release) => false;

        public bool Take(string quest, RoleRelease release) => false;

        public void HeldForOwner(string quest) => HeldForOwners.Add(quest);
    }

    /// <summary>The stand-in harness: it writes down where it ran, takes the quest on its first run and works until stopped; later runs wait too.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const [log] = process.argv.slice(2);
            appendFileSync(log, JSON.stringify({ cwd: process.cwd() }) + '\n');
            await fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ action: 'take' }) });
            setInterval(() => {}, 1 << 30);
            """);
        return script;
    }

    private static List<JsonNode> Runs(string log) => [.. File.ReadAllLines(log).Select(line => JsonNode.Parse(line)!)];

    private static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + Poll.Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < patience, "the condition never held");
            await Task.Delay(50);
        }
    }
}
