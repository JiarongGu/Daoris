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

    private async Task<DriverModule> UpAsync()
    {
        var loop = Loop();
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
}
