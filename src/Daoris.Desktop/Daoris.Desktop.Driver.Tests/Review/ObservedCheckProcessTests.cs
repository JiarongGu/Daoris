using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class ObservedCheckProcessTests : IDisposable
{
    private readonly HeavyFixture _fixture = new("observed-check");

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_observed_gate_runs_its_declared_command_and_cwd_regardless_of_its_heavy_mark(bool? heavy)
    {
        Declare(heavy);

        var state = await RunAsync(CheckAsk.ByDaoris);

        Assert.Equal((CheckStates.Reached, 0), (state.State, state.Exit));
        Assert.Equal(["ran", "ran"], File.ReadAllLines(Marker));
        Assert.Contains("comments", state.Tail);
    }

    [Fact]
    public async Task An_agents_named_light_gate_is_still_refused()
    {
        Declare(false);

        var state = await RunAsync(CheckAsk.ByAgent);

        Assert.Equal((CheckStates.Failed, 1), (state.State, state.Exit));
        Assert.Contains("not marked heavy", state.Tail);
        Assert.False(File.Exists(Marker));
    }

    [Fact]
    public async Task An_observed_gate_uses_the_repositorys_declaration_instead_of_the_machines_heavy_mapping()
    {
        Declare(false);
        DriverConfig.Parse("""{"heavyGates":{"engine":[{"name":"comments","run":"node -e process.exit(9)"}]}}""")
            .Save(DriverConfig.ResolvePath(_fixture.Home));

        var state = await RunAsync(CheckAsk.ByDaoris);

        Assert.Equal((CheckStates.Reached, 0), (state.State, state.Exit));
        Assert.True(File.Exists(Marker));
    }

    [Fact]
    public async Task An_observed_gate_of_a_light_sessions_tree_runs_after_the_workers_turn()
    {
        Declare(false);
        var git = Path.Combine(_fixture.Checkout, ".git", "worktrees", "engine-tree");
        File.WriteAllText(Path.Combine(git, "locked"), SessionLocks.Reason("s1"));
        SessionChecks.Hold(_fixture.Home, "s1", SessionChecks.Light);

        var state = await RunAsync(CheckAsk.ByDaoris);

        Assert.Equal((CheckStates.Reached, 0), (state.State, state.Exit));
        Assert.True(File.Exists(Marker));
    }

    [Fact]
    public async Task An_undeclared_observed_gate_fails_without_running_another_gate()
    {
        Declare(true);

        var state = await RunAsync(CheckAsk.ByDaoris, "gone");

        Assert.Equal((CheckStates.Failed, 2), (state.State, state.Exit));
        Assert.Contains("declares no gate `gone`", state.Tail);
        Assert.False(File.Exists(Marker));
    }

    private string Marker => Path.Combine(_fixture.Tree, "child folder", "ran.txt");

    private void Declare(bool? heavy)
    {
        var folder = Path.GetDirectoryName(Marker)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "probe.mjs"), "import {appendFileSync} from 'node:fs'; appendFileSync('ran.txt', 'ran\\n');");
        var gate = new Dictionary<string, object>
        {
            ["name"] = "comments", ["run"] = "node probe.mjs && node probe.mjs", ["cwd"] = "child folder",
        };
        if (heavy is { } mark) gate["heavy"] = mark;
        File.WriteAllText(Path.Combine(_fixture.Tree, DeclarationNames.Gates), JsonSerializer.Serialize(new { gates = new[] { gate } }));
    }

    private async Task<CheckState> RunAsync(string by, string gate = "comments")
    {
        var files = new CheckFiles(_fixture.Home);
        files.WriteAsk(new CheckAsk("a1", "s1", gate, null, by, DateTimeOffset.UtcNow));
        var broker = new CheckBroker(_fixture.Home, _ => _fixture.Tree,
            (ask, world, output, ct) => HeavyRun.RunAsync(ask, world with { Variable = _ => null }, output, ct));
        return (await broker.RunNextAsync(CancellationToken.None).WaitAsync(Poll.Patience))!;
    }
}
