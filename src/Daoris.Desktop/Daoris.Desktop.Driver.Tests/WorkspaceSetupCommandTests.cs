using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSSETUP6 (D124 §4.5): the terminal's door to a workspace plan, <c>daoris-driver setup --workspace &lt;name&gt;</c>.
/// <c>--plan</c> prints the list with what touched each and every refusal, and writes nothing; a press writes the plan and
/// says the rule it added; <c>--pause</c>, <c>--resume</c> and <c>--stop</c> steer a plan already made, and print where it
/// stands. Fast: the world is a stand-in.
/// </summary>
public sealed class WorkspaceSetupCommandTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 10, 2);

    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-wsplan-cmd-" + Guid.NewGuid().ToString("N")[..8]);

    public WorkspaceSetupCommandTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static DriverConfig Config => new[] { "atlas", "billing", "cargo" }.Aggregate(
        DriverConfig.Empty with { Adapter = "claude-code-acp", Cap = 2 },
        (config, repository) => config.WithDrivable(repository, true).WithTrees(repository, true));

    private static WorkspaceSetupStandIn World() => new WorkspaceSetupStandIn().With(
        WorkspaceSetupStandIn.Row("atlas"), WorkspaceSetupStandIn.Row("billing"), WorkspaceSetupStandIn.Row("cargo"));

    [Fact]
    public void A_workspace_is_asked_for_by_its_flag_and_one_repository_is_not()
    {
        Assert.True(WorkspaceSetupCommand.Asks(["--workspace", "work", "--plan"]));
        Assert.False(WorkspaceSetupCommand.Asks(["reports", "--plan"]));
    }

    [Theory]
    [InlineData("--workspace")]
    [InlineData("--workspace work --pause --resume")]
    [InlineData("--workspace work --at-once 0")]
    [InlineData("--workspace work --at-once many")]
    [InlineData("--workspace work --pilot -1")]
    [InlineData("--workspace work reports")]
    [InlineData("--workspace work --stop --pilot 1")]
    [InlineData("--workspace work --first")]
    [InlineData("--workspace work --frobnicate")]
    [InlineData("--workspace work --workspace home")]
    public async Task Words_it_does_not_take_are_the_usage(string words)
    {
        var output = new StringWriter();

        var code = await RunAsync(words.Split(' '), output);

        Assert.Equal(2, code);
        Assert.Contains("daoris-driver setup --workspace <name> [--plan]", output.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(WorkspaceSetupFile.PathOf(_home, "work")));
    }

    /// <summary>D124 §4.5: <c>--plan</c> prints the list and each refusal, the rule and the pacing, and publishes and writes nothing.</summary>
    [Fact]
    public async Task The_plan_prints_the_list_with_what_touched_each_and_each_refusal_and_writes_nothing()
    {
        var world = World();
        world.Quest("q1", "cargo", "Say what the tariff is", "Done", from: "billing");
        var output = new StringWriter();

        var code = await RunAsync(["--workspace", "work", "--plan"], output, world, Config.WithDrivable("billing", false));

        var said = output.ToString().ReplaceLineEndings("\n");
        Assert.Equal(0, code);
        Assert.Contains("setup: workspace `work`, 3 repositories with a checkout here, the ones other work touches first", said, StringComparison.Ordinal);
        Assert.Contains("\n  1  cargo          1     0         0  to go\n", said, StringComparison.Ordinal);
        Assert.Contains("\n  2  atlas          0     0         0  to go\n", said, StringComparison.Ordinal);
        Assert.Contains("\n  3  billing        0     0         0  to go\n       refused: `billing` is not driven here", said, StringComparison.Ordinal);
        Assert.Contains("  at once   1 (at most 1 while the cap is 2, so other work keeps a slot)", said, StringComparison.Ordinal);
        Assert.Contains("  pilot     2: once the first 2 have closed, the plan pauses until you resume it", said, StringComparison.Ordinal);
        Assert.Contains("a press adds to workspace `work`'s rules, once, so each set-up's session may run the doctrine tool:", said, StringComparison.Ordinal);
        Assert.Contains("  Bash(daoris --version)", said, StringComparison.Ordinal);
        Assert.EndsWith("--plan: nothing was written, published or added.\n", said, StringComparison.Ordinal);
        Assert.False(File.Exists(WorkspaceSetupFile.PathOf(_home, "work")));
        Assert.Empty(world.Published);
    }

    [Fact]
    public async Task A_press_writes_the_plan_and_says_the_rule_it_added_and_a_second_is_refused()
    {
        var world = World();
        var output = new StringWriter();
        var again = new StringWriter();

        var code = await RunAsync(["--workspace", "work", "--pilot", "1", "--first", "cargo", "--skip", "billing"], output, world);
        var second = await RunAsync(["--workspace", "work"], again, world);

        var said = output.ToString().ReplaceLineEndings("\n");
        Assert.Equal(0, code);
        Assert.Contains("the plan for workspace `work` is written: 2 repositories, one at a time, pausing once the first 1 have closed.", said, StringComparison.Ordinal);
        Assert.Contains("  left out  billing (you unticked it)", said, StringComparison.Ordinal);
        Assert.Contains("added to workspace `work`'s rules, so each set-up's session may run the doctrine tool:", said, StringComparison.Ordinal);
        Assert.Contains("`daoris agent rules remove <rule> --workspace work`", said, StringComparison.Ordinal);
        Assert.Equal(["cargo", "atlas"], WorkspaceSetupFile.Load(_home, "work").Plan!.Order);
        Assert.Empty(world.Published);
        Assert.Equal(1, second);
        Assert.Contains("refused:\n  - a plan for workspace `work` is already working", again.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_press_the_machine_cannot_carry_is_refused_saying_why_and_writes_nothing()
    {
        var world = World();
        world.Tools = new("node", "v22.11.0", null, null, null, null);
        var output = new StringWriter();

        var code = await RunAsync(["--workspace", "work"], output, world);

        Assert.Equal(1, code);
        Assert.Contains("the doctrine tool cannot run here", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("nothing was written", output.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(WorkspaceSetupFile.PathOf(_home, "work")));
    }

    /// <summary>With a plan working, <c>--plan</c> prints where it stands: each repository's state and the head line.</summary>
    [Fact]
    public async Task The_plan_of_a_workspace_with_a_plan_working_prints_where_it_stands()
    {
        var world = World();
        await RunAsync(["--workspace", "work", "--pilot", "0"], new StringWriter(), world);
        await WorkspaceSetup.TickAsync(world, Config, SessionWire.Acp, _home, Day, Now);
        world.Close(world.SetupOf("atlas").Id, "Done");
        var output = new StringWriter();

        var code = await RunAsync(["--workspace", "work", "--plan"], output, world);

        var said = output.ToString().ReplaceLineEndings("\n");
        Assert.Equal(0, code);
        Assert.Contains("setup: workspace `work`, a plan made 2026-10-02: one at a time, with no pilot", said, StringComparison.Ordinal);
        Assert.Contains("  1  atlas    waiting for your review — its set-up #q1 is done and waits for your review", said, StringComparison.Ordinal);
        Assert.Contains("  2  billing  to go", said, StringComparison.Ordinal);
        Assert.Contains("Setting up — 1 waiting for your review · 2 to go", said, StringComparison.Ordinal);
        Assert.Single(world.Published);
    }

    [Fact]
    public async Task Pause_resume_and_stop_steer_the_plan_and_say_where_it_stands()
    {
        var world = World();
        await RunAsync(["--workspace", "work"], new StringWriter(), world);
        await WorkspaceSetup.TickAsync(world, Config, SessionWire.Acp, _home, Day, Now);
        var paused = new StringWriter();
        var resumed = new StringWriter();
        var stopped = new StringWriter();

        Assert.Equal(0, await RunAsync(["--workspace", "work", "--pause"], paused, world));
        Assert.Equal(0, await RunAsync(["--workspace", "work", "--resume"], resumed, world));
        Assert.Equal(0, await RunAsync(["--workspace", "work", "--stop"], stopped, world));

        Assert.Contains("the plan for workspace `work` is paused", paused.ToString(), StringComparison.Ordinal);
        Assert.Contains("— paused by you", paused.ToString(), StringComparison.Ordinal);
        Assert.Contains("the plan for workspace `work` carries on", resumed.ToString(), StringComparison.Ordinal);
        Assert.Contains("the plan for workspace `work` is stopped", stopped.ToString(), StringComparison.Ordinal);
        Assert.Contains("#q1 to `atlas` is open and nobody has started it: `daoris-driver quest delete q1` deletes it.", stopped.ToString(), StringComparison.Ordinal);
        Assert.Contains("— stopped", stopped.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Steering_a_workspace_with_no_plan_is_refused()
    {
        var output = new StringWriter();

        var code = await RunAsync(["--workspace", "work", "--pause"], output, World());

        Assert.Equal(1, code);
        Assert.Contains("there is no plan for workspace `work`", output.ToString(), StringComparison.Ordinal);
    }

    private Task<int> RunAsync(string[] args, TextWriter output, WorkspaceSetupStandIn? world = null, DriverConfig? config = null) =>
        WorkspaceSetupCommand.RunAsync(args, output, world ?? World(), config ?? Config, SessionWire.Acp, _home, Day, Now);
}
