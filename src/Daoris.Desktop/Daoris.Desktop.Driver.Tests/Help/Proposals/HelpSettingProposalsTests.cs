using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>Ask Daoris's <c>setting</c> proposal (HELP1c): one of the `daoris driver` verbs, judged by the config's own edits.</summary>
public sealed class HelpSettingProposalsTests : HelpProposalsFixture
{
    [Theory]
    [InlineData("drive", "engine", null, null, "daoris driver drive engine", "Drive `engine`")]
    [InlineData("hold", "game", null, null, "daoris driver hold game", "Hold `game`")]
    [InlineData("trees", "engine", null, "on", "daoris driver trees engine on", "own tree")]
    [InlineData("line", "engine", null, "develop", "daoris driver line engine develop", "`engine`'s line to `develop`")]
    [InlineData("line", null, "work", "--clear", "daoris driver line --workspace work --clear", "Clear workspace `work`'s line")]
    [InlineData("landing", null, "work", "branch feature/{quest}-{slug} --tidy", "daoris driver landing --workspace work branch feature/{quest}-{slug} --tidy", "on a branch `feature/{quest}-{slug}`")]
    [InlineData("intake", null, null, "off", "daoris driver intake off", "Stop answering asks")]
    [InlineData("helper", null, null, "claude-code", "daoris driver helper claude-code", "`claude-code`")]
    [InlineData("strikes", null, null, "5", "daoris driver strikes 5", "5 failed sessions")]
    [InlineData("timeout", null, null, "120", "daoris driver timeout 120", "120 minutes")]
    [InlineData("notify", null, null, "off", "daoris driver notify off", "Stop saying")]
    // HELP9: reading and writing across (D107), and the two dials the terminal alone had.
    [InlineData("across", "engine", null, "read off", "daoris driver across engine read off", "read by no agent outside it")]
    [InlineData("across", "engine", null, "read on", "daoris driver across engine read on", "never a write")]
    [InlineData("across", null, "work", "read off", "daoris driver across --workspace work read off", "Each checkout in workspace `work` that sets none of its own")]
    [InlineData("across", "engine", null, "read --clear", "daoris driver across engine read --clear", "takes its workspace's reading again")]
    [InlineData("across", "game", null, "write-to engine", "daoris driver across game write-to engine", "your standing say-so")]
    [InlineData("across", "game", null, "write-to engine --clear", "daoris driver across game write-to engine --clear", "no longer write into `engine`")]
    [InlineData("cap", null, null, "3", "daoris driver cap 3", "at most 3 sessions at once")]
    [InlineData("adapter", null, null, "claude-code-acp", "daoris driver adapter claude-code-acp", "`claude-code-acp`")]
    // HELP10: a quest its strikes parked, started again as the drawer's Retry and `daoris driver retry` do, `#` or not.
    [InlineData("retry", "q1a2b3c4", null, null, "daoris driver retry q1a2b3c4", "`#q1a2b3c4` may be started again")]
    [InlineData("retry", "#q1a2b3c4", null, null, "daoris driver retry q1a2b3c4", "what already happened is still in the records")]
    // SESSUX1b (D126 §3.4): a quest the person's stop holds, released from that stop as Try again releases it.
    [InlineData("retry", "q2taken0", null, null, "daoris driver retry q2taken0 --session s7a8b9c0", "released from your stop of session `s7a8b9c0`")]
    public void A_setting_is_planned_as_what_it_changes_and_the_command_that_does_the_same(
        string door, string? target, string? workspace, string? value, string terminal, string says)
    {
        var plan = HelpProposals.Plan(Setting(door, target, workspace, value), DriverConfig.Empty, Facts);

        Assert.Null(plan.Refusal);
        Assert.Equal(terminal, plan.Terminal);
        Assert.Contains(says, plan.Describe);
        Assert.NotNull(plan.Apply);
    }

    [Fact]
    public void Applying_a_plan_makes_the_edit_the_screens_route_makes()
    {
        var config = DriverConfig.Empty;
        config = HelpProposals.Plan(Setting("drive", "engine"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("landing", null, "work", "branch feature/{quest}-{slug} --tidy"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("timeout", value: "120"), config, Facts).Apply!(config);

        Assert.Contains("engine", config.Drivable);
        Assert.Equal(new LandingRule("branch", "feature/{quest}-{slug}", Tidy: true), config.WorkspaceLandings["work"]);
        Assert.Equal(120, config.TimeoutMinutes);
    }

    /// <summary>HELP9: across (D107), a cap and an adapter, applied as `SET_READ_ACROSS`, `SET_WRITE_ACROSS` and the terminal make them.</summary>
    [Fact]
    public void Applying_across_a_cap_and_an_adapter_makes_the_edit_the_terminal_makes()
    {
        var config = DriverConfig.Empty;
        config = HelpProposals.Plan(Setting("across", "engine", null, "read off"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("across", null, "work", "read off"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("across", "game", null, "write-to engine"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("cap", value: "3"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("adapter", value: "claude-code-acp"), config, Facts).Apply!(config);

        Assert.False(config.ReadAcross["engine"]);
        Assert.False(config.WorkspaceReadAcross["work"]);
        Assert.Equal(["engine"], config.WriteAcross["game"]);
        Assert.Equal(3, config.Cap);
        Assert.Equal("claude-code-acp", config.Adapter);

        config = HelpProposals.Plan(Setting("across", "engine", null, "read --clear"), config, Facts).Apply!(config);
        config = HelpProposals.Plan(Setting("across", "game", null, "write-to engine --clear"), config, Facts).Apply!(config);

        Assert.False(config.ReadAcross.ContainsKey("engine"));
        Assert.False(config.WriteAcross.ContainsKey("game"));
    }

    /// <summary>
    /// HELP10: a retry is the edit <c>RETRY_QUEST</c> and <c>daoris driver retry</c> make — the quest forgiven at the
    /// strike limit as it stands when applied, not erased, so the next failures park it again.
    /// </summary>
    [Fact]
    public void Applying_a_retry_marks_the_quest_forgiven_at_the_strike_limit_as_the_drawers_route_does()
    {
        var plan = HelpProposals.Plan(Setting("retry", "#q1a2b3c4"), DriverConfig.Empty.WithStrikes(3), Facts);

        var config = plan.Apply!(DriverConfig.Empty.WithStrikes(5));

        Assert.Equal(5, config.ForgivenAt("q1a2b3c4"));
        Assert.Contains("5 more failure(s) will park it again", HelpProposals.Plan(Setting("retry", "q1a2b3c4"), config, Facts).Describe);
        Assert.Null(config.ReleasedFor("q1a2b3c4"));
    }

    /// <summary>
    /// SESSUX1b: a retry of a quest the person's stop holds is the release <c>RETRY_QUEST</c> writes, against the session the
    /// last look named, and never a mark: a stop is not a strike (D58), so nothing about the strikes moves.
    /// </summary>
    [Fact]
    public void Applying_a_retry_of_a_held_quest_releases_its_stop_as_the_route_does()
    {
        var plan = HelpProposals.Plan(Setting("retry", "#q2taken0"), DriverConfig.Empty, Facts);

        var config = plan.Apply!(DriverConfig.Empty.WithStrikes(5));

        Assert.Equal("s7a8b9c0", config.ReleasedFor("q2taken0"));
        Assert.Equal(0, config.ForgivenAt("q2taken0"));
    }

    /// <summary>
    /// HELP9: every door the kind names is one it plans, so the doors Ask Daoris's coverage is held against
    /// (<c>HelpCoverageTests</c>) are doors a proposal can take, and none is named that falls to the refusal.
    /// </summary>
    [Fact]
    public void Every_door_the_kind_names_is_one_it_plans()
    {
        (string Door, string? Target, string? Workspace, string? Value)[] samples =
        [
            ("drive", "engine", null, null), ("undrive", "engine", null, null), ("hold", "engine", null, null),
            ("resume", "engine", null, null), ("trees", "engine", null, "off"), ("line", "engine", null, "main"),
            ("landing", "engine", null, "merge"), ("across", "engine", null, "read on"), ("intake", null, null, "off"),
            ("helper", null, null, "claude-code"), ("strikes", null, null, "0"), ("retry", "q1a2b3c4", null, null),
            ("timeout", null, null, "30"), ("notify", null, null, "on"), ("cap", null, null, "1"), ("adapter", null, null, "claude-code"),
        ];

        Assert.Equal(new HelpSettingProposals().Doors, samples.Select(sample => sample.Door));
        foreach (var (door, target, workspace, value) in samples)
        {
            Assert.Null(HelpProposals.Plan(Setting(door, target, workspace, value), DriverConfig.Empty, Facts).Refusal);
        }
    }

    /// <summary>What the route would refuse is refused here in its own words, and never shown to the person.</summary>
    [Theory]
    [InlineData("drive", "nowhere", null, null, "`nowhere` is not registered")]
    [InlineData("line", null, "elsewhere", "develop", "no workspace `elsewhere`")]
    [InlineData("line", "engine", null, "bad..name", "not a branch name git would take")]
    [InlineData("landing", "engine", null, "branch feature/fixed", "`{quest}` or `{session}`")]
    [InlineData("intake", null, null, "gpt-agent", "no agent `gpt-agent`")]
    // HELP9: what `daoris driver across` refuses, in its words, and the names a helper can invent.
    [InlineData("across", "engine", null, "write-to engine", "`engine` writes in its own tree already")]
    [InlineData("across", "engine", null, "write-to nowhere", "`nowhere` is not registered")]
    [InlineData("across", "engine", null, "write-to", "a relationship names the repository it may write into")]
    [InlineData("across", null, "work", "write-to engine", "a relationship is declared from one repository")]
    [InlineData("across", "engine", "work", "read off", "a repository or a workspace")]
    [InlineData("across", null, null, "read off", "a repository or a workspace")]
    [InlineData("across", "engine", null, "read sometimes", "`read on|off|--clear`")]
    [InlineData("across", "engine", null, "peek", "`across` sets `read on|off|--clear` or `write-to <other> [--clear]`")]
    [InlineData("cap", null, null, "0", "`cap 0` is not a change the driver makes")]
    [InlineData("adapter", null, null, "gpt-agent", "no agent `gpt-agent`")]
    // HELP10: a quest id a helper can invent, or one not parked, which forgiven would run past its strikes (D110).
    // SESSUX1b: or one no stop of the person's holds; the refusal names what the last look parked and what it held.
    [InlineData("retry", "q9none00", null, null, "`#q9none00` is neither parked nor held by your stop on this machine")]
    [InlineData("retry", "q9none00", null, null, "it parked `#q1a2b3c4` and held `#q2taken0`")]
    [InlineData("retry", "engine", null, null, "`#engine` is neither parked nor held by your stop")]
    [InlineData("retry", null, null, null, "`retry` names the quest its failed sessions parked or your stop holds")]
    public void What_the_route_would_refuse_is_refused_in_its_words(
        string door, string? target, string? workspace, string? value, string says)
    {
        var plan = HelpProposals.Plan(Setting(door, target, workspace, value), DriverConfig.Empty, Facts);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Apply);
    }

    /// <summary>HELP8: a landing rule naming a plugin (D100), proposed the way the terminal spells it.</summary>
    [Theory]
    [InlineData("branch feature/{quest}-{slug} --plugin example.lands --tidy")]
    [InlineData("branch feature/{quest}-{slug} --tidy --plugin example.lands")]
    public void A_landing_may_name_a_plugin_that_lands_work_here(string value)
    {
        var facts = WithPlugin("example.lands");

        var plan = HelpProposals.Plan(Setting("landing", null, "work", value), DriverConfig.Empty, facts);

        Assert.Null(plan.Refusal);
        Assert.Equal($"daoris driver landing --workspace work {value}", plan.Terminal);
        Assert.Contains("on a branch `feature/{quest}-{slug}`", plan.Describe);
        Assert.Contains("plugin `example.lands` pushes it and opens the pull request", plan.Describe);
        Assert.Equal(new LandingRule("branch", "feature/{quest}-{slug}", Tidy: true, Plugin: "example.lands"),
            plan.Apply!(DriverConfig.Empty).WorkspaceLandings["work"]);
    }

    /// <summary>HELP8: what `daoris driver landing` refuses about a plugin, refused here in the same words.</summary>
    [Theory]
    [InlineData("branch feature/{quest} --plugin nowhere.lands", "", "not installed on this machine")]
    [InlineData("branch feature/{quest} --plugin example.off", "off", "switched off")]
    [InlineData("branch feature/{quest} --plugin example.quiet", "quiet", "does not land work")]
    [InlineData("merge --plugin example.lands", "", "only a branch rule hands its work to a plugin")]
    [InlineData("branch feature/{quest} --plugin", "", "`--plugin` needs the id of an installed plugin")]
    [InlineData("branch feature/{quest} --plugin Not/An/Id", "", "is not a plugin id")]
    public void A_landing_plugin_the_route_would_refuse_is_refused_in_its_words(string value, string install, string says)
    {
        var facts = WithPlugin("example.lands");
        if (install == "off") facts = WithPlugin("example.off", enabled: false);
        if (install == "quiet") facts = WithPlugin("example.quiet", points: "\"session/ended\"");

        var plan = HelpProposals.Plan(Setting("landing", "engine", null, value), DriverConfig.Empty, facts);

        Assert.Contains(says, plan.Refusal);
        Assert.Null(plan.Apply);
    }

    [Fact]
    public async Task A_setting_is_applied_through_the_edit_the_screens_route_makes()
    {
        var (setting, doors, _) = await ApplyAsync(Setting("drive", "engine"));

        Assert.True(setting.Applied);
        Assert.Equal(["change"], doors.Calls);
        Assert.Contains("engine", doors.Config.Drivable);
    }
}

public sealed partial class HelpStandInDoors
{
    public DriverConfig Config { get; private set; } = DriverConfig.Empty;

    public void Change(Func<DriverConfig, DriverConfig> edit)
    {
        Config = edit(Config);
        Calls.Add("change");
    }
}
