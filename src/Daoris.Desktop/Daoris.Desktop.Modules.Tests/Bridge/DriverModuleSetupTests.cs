using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A repository's *Set up for agents* over the bridge (`SetupRoutes.cs`, LAYOUT8, D117 §6.1, §6.5): <c>SETUP_PLAN</c>, the twin
/// of <c>daoris-driver setup &lt;repository&gt; --plan</c>, which reads and writes nothing, and <c>SETUP_PRESS</c>, the press,
/// which the terminal's runs: the rule into the repository's scope, then one ask to it. A refusal is an answer, every one at
/// once, as `--plan` prints them. The press's world stands in (<see cref="SetupStandIn"/>), so no git or tool is started.
/// </summary>
public sealed class DriverModuleSetupTests : DriverModuleBridge
{
    private readonly SetupStandIn _world = new();

    /// <summary>`atlas` driven here, its sessions in trees of their own, on an agent that rides the protocol door.</summary>
    private void Driven() =>
        (DriverConfig.Empty with { Adapter = "claude-code-acp" }).WithDrivable("atlas", true).WithTrees("atlas", true).Save(DriverConfigPath);

    /// <summary>The loop <see cref="UpAsync"/> brought up, whose door the terminal's own path reads too (RPC1e).</summary>
    private DriverLoop? _up;

    private async Task<DriverModule> UpAsync()
    {
        var loop = _up = Loop();
        var module = new DriverModule(Bus, loop) { SetupWorlds = _ => _world };
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SetupStandIn.EmptyHost())));
        return module;
    }

    [Theory]
    [InlineData("SETUP_PLAN")]
    [InlineData("SETUP_PRESS")]
    public async Task Before_the_driver_is_up_each_is_a_sentence(string route)
    {
        var refusal = await RefusalAsync(Module(), route, new { repository = "atlas" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
    }

    [Fact]
    public async Task The_plan_says_what_was_read_the_rule_and_the_quest_and_changes_nothing()
    {
        Driven();
        var module = await UpAsync();

        var plan = await AnswerAsync(module, "SETUP_PLAN", new { repository = "ATLAS" });

        Assert.Equal("atlas", plan.GetProperty("repository").GetString());
        Assert.Equal("work", plan.GetProperty("workspace").GetString());
        Assert.Equal("main", plan.GetProperty("line").GetString());
        Assert.Equal("0123456789ab", plan.GetProperty("commit").GetString());
        Assert.Equal("none", plan.GetProperty("layout").GetString());
        Assert.False(plan.GetProperty("adopted").GetBoolean());
        Assert.Equal("whole", plan.GetProperty("asks").GetString());
        Assert.Equal("claude-code-acp", plan.GetProperty("agent").GetString());
        Assert.Equal("merge", plan.GetProperty("lands").GetString());
        Assert.Equal("v24.1.0", plan.GetProperty("node").GetString());
        Assert.Equal("0.9.0", plan.GetProperty("daoris").GetString());
        Assert.Equal(SetupPressRules.Rules, plan.GetProperty("rules").EnumerateArray().Select(rule => rule.GetString()));
        Assert.StartsWith("Set up this repository for every agent (", plan.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(plan.GetProperty("body").GetString()));
        Assert.Empty(plan.GetProperty("refusals").EnumerateArray());
        Assert.True(plan.GetProperty("pressable").GetBoolean());
        // `--plan`'s promise: nothing was published, and no rule was added.
        Assert.Empty(_world.Published);
        Assert.False(File.Exists(DaorisHome.Entry(Home, HomeEntry.Permissions)));
    }

    /// <summary>Every refusal that applies, at once, each with its code and the sentence naming the door that answers it.</summary>
    [Fact]
    public async Task A_plan_the_press_refuses_answers_every_refusal_with_its_code()
    {
        (DriverConfig.Empty with { Adapter = "claude-code-acp" }).Save(DriverConfigPath);
        var module = await UpAsync();

        var plan = await AnswerAsync(module, "SETUP_PLAN", new { repository = "atlas" });

        Assert.False(plan.GetProperty("pressable").GetBoolean());
        var refusals = plan.GetProperty("refusals").EnumerateArray()
            .Select(refusal => (refusal.GetProperty("code").GetString(), refusal.GetProperty("sentence").GetString()!))
            .ToList();
        Assert.Equal([SetupRefusals.NotDriven, SetupRefusals.NoOwnTree], refusals.Select(refusal => refusal.Item1));
        Assert.Contains("`daoris driver drive atlas`", refusals[0].Item2);
    }

    [Fact]
    public async Task The_press_adds_the_rule_then_publishes_one_ask_to_the_repository_as_the_terminal_does()
    {
        Driven();
        var module = await UpAsync();

        var pressed = await AnswerAsync(module, "SETUP_PRESS", new { repository = "atlas" });

        Assert.True(pressed.GetProperty("published").GetBoolean());
        Assert.Equal("Asked as `#a1b2c3d4` in `work`.", pressed.GetProperty("message").GetString());
        Assert.Equal("a1b2c3d4", pressed.GetProperty("askId").GetString());
        Assert.Equal("q1a2b3c4", pressed.GetProperty("questId").GetString());
        Assert.Equal(SetupPressRules.Rules, pressed.GetProperty("added").EnumerateArray().Select(rule => rule.GetString()));
        var (workspace, sentence, to) = Assert.Single(_world.Published);
        Assert.Equal(("work", "atlas"), (workspace, to));
        Assert.StartsWith("Set up this repository for every agent (", sentence);
        var rules = PermissionRules.Load(Home);
        Assert.Equal(SetupPressRules.Rules, rules.Repositories["atlas"].Allow);
    }

    [Fact]
    public async Task A_press_the_plan_refuses_publishes_nothing_and_adds_no_rule()
    {
        (DriverConfig.Empty with { Adapter = "claude-code-acp" }).Save(DriverConfigPath);
        var module = await UpAsync();

        var pressed = await AnswerAsync(module, "SETUP_PRESS", new { repository = "atlas" });

        Assert.False(pressed.GetProperty("published").GetBoolean());
        Assert.Contains("is not driven here", pressed.GetProperty("message").GetString());
        Assert.False(pressed.GetProperty("plan").GetProperty("pressable").GetBoolean());
        Assert.Empty(_world.Published);
        Assert.False(File.Exists(DaorisHome.Entry(Home, HomeEntry.Permissions)));
    }

    /// <summary>The lines and the exit code a route answered as <c>said</c> (RPC1e), which the terminal prints, joined.</summary>
    private static (string Out, int Code) SaidOf(System.Text.Json.JsonElement answer)
    {
        var said = answer.GetProperty("said");
        return (string.Join("\n", said.GetProperty("out").EnumerateArray().Select(line => line.GetString())), said.GetProperty("code").GetInt32());
    }

    /// <summary>The terminal's own path over the same world, file and home, with no instance to look for: what <c>setup</c> prints alone.</summary>
    private async Task<(string Out, int Code)> AloneAsync(params string[] args)
    {
        var config = DriverConfig.Load(DriverConfigPath);
        var output = new StringWriter { NewLine = "\n" };
        var code = await SetupCommand.RunAsync(
            args, output, _world, config, DriverContext.DoorOf(_up!, config), Home, DateOnly.FromDateTime(DateTime.Now));
        return (output.ToString().TrimEnd('\n'), code);
    }

    /// <summary>
    /// RPC1e: asked for its words (<c>said: true</c>) by <c>daoris-driver setup &lt;repository&gt; [--plan]</c> sent over the pipe,
    /// <c>SETUP_PLAN</c> and <c>SETUP_PRESS</c> answer the lines and the exit code the alone path prints over the same world: a plan that
    /// would publish, one the press refuses, and a press refused, which publishes nothing and adds no rule. The page's answers keep their shape.
    /// </summary>
    [Fact]
    public async Task Asked_for_its_words_each_setup_route_answers_the_lines_the_alone_path_prints()
    {
        (DriverConfig.Empty with { Adapter = "claude-code-acp" }).Save(DriverConfigPath);
        var module = await UpAsync();

        Assert.Equal(await AloneAsync("atlas", "--plan"), SaidOf(await AnswerAsync(module, "SETUP_PLAN", new { repository = "atlas", said = true })));
        var refused = SaidOf(await AnswerAsync(module, "SETUP_PRESS", new { repository = "atlas", said = true }));
        Assert.Equal(await AloneAsync("atlas"), refused);
        Assert.Equal(1, refused.Code);
        Assert.Empty(_world.Published);

        Driven();
        var plan = SaidOf(await AnswerAsync(module, "SETUP_PLAN", new { repository = "atlas", said = true }));
        Assert.Equal(await AloneAsync("atlas", "--plan"), plan);
        Assert.Equal(0, plan.Code);
        Assert.EndsWith("--plan: nothing was published, and no rule was added.", plan.Out, StringComparison.Ordinal);
        Assert.Empty(_world.Published);
        Assert.False((await AnswerAsync(module, "SETUP_PLAN", new { repository = "atlas" })).TryGetProperty("said", out _));
    }

    /// <summary>RPC1e: a terminal's press runs here, adds the rule and publishes one ask, and says so in the verb's words.</summary>
    [Fact]
    public async Task A_terminal_s_press_runs_here_and_says_what_it_published_in_the_verb_s_words()
    {
        Driven();
        var module = await UpAsync();

        var (lines, code) = SaidOf(await AnswerAsync(module, "SETUP_PRESS", new { repository = "atlas", said = true }));

        Assert.Equal(0, code);
        Assert.Contains("Asked as `#a1b2c3d4` in `work`.\n  ask    #a1b2c3d4\n  quest  #q1a2b3c4", lines, StringComparison.Ordinal);
        Assert.Single(_world.Published);
        Assert.Equal(SetupPressRules.Rules, PermissionRules.Load(Home).Repositories["atlas"].Allow);
    }
}
