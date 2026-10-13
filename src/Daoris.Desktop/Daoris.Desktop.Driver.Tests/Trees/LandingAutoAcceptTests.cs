using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>autoAccept</c> on a landing rule in <c>driver.json</c> (LAND2a, D145): a branch rule may accept done work
/// automatically, its plugin pushing and opening the pull request with no press. Off until set; only a branch rule takes
/// it, with a plugin or without one; a repository's own rule replaces its workspace's whole, the switch with it. The
/// driver's half of a TWIN with the CLI's <c>driverconfig.ts</c>, whose <c>driverconfig.test.ts</c> holds the same two
/// tables and parses these theories to hold them to its own, cell for cell.
/// </summary>
/// <remarks>🔴 <b>Keep each row on one line, its cells literals</b>: the CLI's test reads them.</remarks>
public sealed class LandingAutoAcceptTests
{
    /// <summary>The shape: only a branch rule accepts automatically, and a merge naming a plugin is told that first.</summary>
    [Theory]
    [InlineData("branch", "feature/{quest}-{slug}", "example.github-pull-request", true, null)]
    [InlineData("branch", "feature/{quest}-{slug}", null, true, null)]
    [InlineData("branch", "feature/{quest}-{slug}", null, false, null)]
    [InlineData("merge", null, null, true, "only a branch rule accepts automatically")]
    [InlineData("merge", null, null, false, null)]
    [InlineData("merge", null, "example.github-pull-request", true, "only a branch rule hands its work to a plugin")]
    public void Only_a_branch_rule_accepts_automatically(string form, string? pattern, string? plugin, bool autoAccept, string? problem)
    {
        var said = LandingRules.Problem(new LandingRule(form, pattern, Plugin: plugin, AutoAccept: autoAccept));

        if (problem is null) Assert.Null(said);
        else Assert.Contains(problem, said);
    }

    /// <summary>What the file says, read: absent is off, only JSON <c>true</c> is on, and a merge carrying it is not read.</summary>
    [Theory]
    [InlineData("absent is off", """{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}"}}}""", "landings", "engine", "off")]
    [InlineData("true on a branch rule is on", """{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}","autoAccept":true}}}""", "landings", "engine", "on")]
    [InlineData("without a plugin it is still on", """{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}","tidy":true,"autoAccept":true}}}""", "landings", "engine", "on")]
    [InlineData("false is off", """{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}","autoAccept":false}}}""", "landings", "engine", "off")]
    [InlineData("text is not true", """{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}","autoAccept":"true"}}}""", "landings", "engine", "off")]
    [InlineData("a number is not true", """{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}","autoAccept":1}}}""", "landings", "engine", "off")]
    [InlineData("a merge carrying it is not read", """{"landings":{"engine":{"form":"merge","autoAccept":true}}}""", "landings", "engine", "none")]
    [InlineData("a merge without it is read", """{"landings":{"engine":{"form":"merge","autoAccept":false}}}""", "landings", "engine", "off")]
    [InlineData("a workspace's rule carries it", """{"workspaceLandings":{"aurora":{"form":"branch","pattern":"review/{session}","autoAccept":true}}}""", "workspaceLandings", "aurora", "on")]
    public void AutoAccept_reads_as_the_cli_reads_it(string name, string file, string map, string key, string expected)
    {
        var config = DriverConfig.Parse(file);
        var rules = map == "workspaceLandings" ? config.WorkspaceLandings : config.Landings;
        var read = rules.TryGetValue(key, out var rule) ? rule.AutoAccept ? "on" : "off" : "none";

        Assert.True(expected == read, $"{name}: {read}");
    }

    /// <summary>Written only when on, after the tidy, and kept through the file: the CLI writes the same key in the same place.</summary>
    [Fact]
    public void The_switch_is_written_only_when_on_and_survives_the_file()
    {
        var on = DriverConfig.Empty.WithWorkspaceLanding(
            "aurora", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true, Plugin: "example.github-pull-request", AutoAccept: true));

        Assert.True(DriverConfig.Parse(on.ToJson()).WorkspaceLandings["aurora"].AutoAccept);
        Assert.Matches("\"tidy\": true,\\s*\"autoAccept\": true", on.ToJson());
        Assert.DoesNotContain("autoAccept",
            DriverConfig.Empty.WithWorkspaceLanding("aurora", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}")).ToJson(),
            StringComparison.Ordinal);
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Merge, AutoAccept: true)));
    }

    /// <summary>
    /// The scope is the rule's (D87, D145 point 1): a repository's own rule replaces its workspace's whole, so one that
    /// leaves the switch off is accepted by hand in a workspace that accepts automatically, and the reverse; cleared, the
    /// workspace's rule stands again.
    /// </summary>
    [Fact]
    public void A_repositorys_own_rule_replaces_the_workspaces_switch_whole()
    {
        var automatic = new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true);
        var byHand = new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}");
        var config = DriverConfig.Empty.WithWorkspaceLanding("aurora", automatic).WithLanding("engine", byHand);

        Assert.False(LandingRules.Choose(config, "engine", "aurora").Rule.AutoAccept);
        Assert.True(LandingRules.Choose(config, "game", "aurora").Rule.AutoAccept);
        Assert.True(LandingRules.Choose(config.WithLanding("engine", null), "engine", "aurora").Rule.AutoAccept);
        Assert.True(LandingRules.Choose(
            DriverConfig.Empty.WithWorkspaceLanding("aurora", byHand).WithLanding("engine", automatic), "engine", "aurora").Rule.AutoAccept);
    }

    /// <summary>
    /// The sentence each door says as the switch is set (D145 point 5, design §6): with a plugin, that it pushes and opens
    /// pull requests without a press; with none, that nothing leaves the machine.
    /// </summary>
    [Fact]
    public void Each_door_says_what_the_switch_does_and_warns_where_no_plugin_pushes()
    {
        var pushes = LandingRules.AutoAcceptSays("example.github-pull-request");
        Assert.Contains("`example.github-pull-request` pushes it and opens a pull request without asking you each time", pushes);
        Assert.Contains("Switch it off to accept each one yourself", pushes);

        var stays = LandingRules.AutoAcceptSays(null);
        Assert.Contains("no plugin opens a pull request, so each done's branch waits here for you to push it", stays);
        Assert.Contains("nothing leaves this machine", stays);
    }
}
