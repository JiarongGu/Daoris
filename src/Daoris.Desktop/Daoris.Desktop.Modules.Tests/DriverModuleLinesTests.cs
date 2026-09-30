using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Each repository's line, its landing rule and the clean-up over the bridge (`DriverModule.Lines.cs`, MOD5).
/// </summary>
public sealed class DriverModuleLinesTests : DriverModuleBridge
{
    /// <summary>
    /// A repository's line (WSR2): the screen's half of `daoris driver line`, over the same file. The
    /// names ride as rows, not as an object's keys, so no key policy on the bridge can respell one.
    /// </summary>
    [Fact]
    public async Task Setting_a_line_writes_the_same_file_the_terminal_edits()
    {
        var module = Module();
        await AnswerAsync(module, "SET_LINE", new { workspace = "aurora", branch = "develop" });
        var state = await AnswerAsync(module, "SET_LINE", new { repository = "Engine", branch = "release/2026.09" });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal("release/2026.09", config.Lines["Engine"]);
        Assert.Equal("develop", config.WorkspaceLines["aurora"]);
        Assert.Equal("Engine", state.GetProperty("lines")[0].GetProperty("repository").GetString());
        Assert.Equal("aurora", state.GetProperty("workspaceLines")[0].GetProperty("workspace").GetString());

        var cleared = await AnswerAsync(module, "SET_LINE", new { repository = "engine" });
        Assert.Equal(0, cleared.GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public async Task A_line_git_would_not_take_or_one_with_no_owner_is_refused_in_a_sentence()
    {
        var bad = await RefusalAsync(Module(), "SET_LINE", new { repository = "engine", branch = "a..b" });
        Assert.Contains("not a branch name git would take", bad);

        var neither = await RefusalAsync(Module(), "SET_LINE", new { branch = "develop" });
        Assert.Contains("a `repository` or a `workspace`", neither);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).Lines.Count > 0);
    }

    /// <summary>How work lands (WSR1, D87): the screen's half of `daoris driver landing`, over the same file.</summary>
    [Fact]
    public async Task Setting_a_landing_rule_writes_the_same_file_the_terminal_edits()
    {
        var module = Module();
        await AnswerAsync(module, "SET_LANDING", new { workspace = "aurora", form = "branch", pattern = "feature/{quest}-{slug}" });
        var state = await AnswerAsync(module, "SET_LANDING", new { repository = "engine", form = "merge" });

        var config = DriverConfig.Load(DriverConfigPath);
        Assert.Equal("feature/{quest}-{slug}", config.WorkspaceLandings["aurora"].Pattern);
        Assert.Equal(LandingRule.Merge, config.Landings["engine"]);
        Assert.Equal("branch", state.GetProperty("workspaceLandings")[0].GetProperty("form").GetString());
        Assert.Equal("engine", state.GetProperty("landings")[0].GetProperty("repository").GetString());

        var cleared = await AnswerAsync(module, "SET_LANDING", new { workspace = "aurora" });
        Assert.Equal(0, cleared.GetProperty("workspaceLandings").GetArrayLength());
    }

    [Fact]
    public async Task A_landing_rule_that_could_not_land_work_is_refused_in_the_drivers_words()
    {
        var fixedName = await RefusalAsync(Module(), "SET_LANDING", new { repository = "engine", form = "branch", pattern = "feature/fixed" });
        Assert.Contains("`{quest}` or `{session}`", fixedName);

        var push = await RefusalAsync(Module(), "SET_LANDING", new { repository = "engine", form = "push" });
        Assert.Contains("a plugin's to do", push);
    }

    /// <summary>
    /// WSR4 (D100): a branch rule may name the plugin that pushes it and opens the pull request — one
    /// installed here, switched on, and speaking on `work/land`. Refused in a sentence otherwise, as the
    /// terminal refuses it, and nothing is written.
    /// </summary>
    [Fact]
    public async Task A_branch_rule_names_its_plugin_only_where_one_here_lands_work()
    {
        var missing = await RefusalAsync(Module(), "SET_LANDING",
            new { workspace = "aurora", form = "branch", pattern = "feature/{quest}-{slug}", plugin = "example.github-pull-request" });
        Assert.Contains("not installed", missing);
        Assert.False(File.Exists(DriverConfigPath) && DriverConfig.Load(DriverConfigPath).WorkspaceLandings.Count > 0);

        var folder = Path.Combine(Home, "plugins", "example.github-pull-request");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"),
            """{ "id": "example.github-pull-request", "hooks": { "command": ["node", "${plugin}/land.mjs"], "points": ["work/land"] } }""");
        PluginState.Disable(Home, "example.github-pull-request");
        var off = await RefusalAsync(Module(), "SET_LANDING",
            new { workspace = "aurora", form = "branch", pattern = "feature/{quest}-{slug}", plugin = "example.github-pull-request" });
        Assert.Contains("daoris plugin enable example.github-pull-request", off);

        PluginState.Enable(Home, "example.github-pull-request");
        var state = await AnswerAsync(Module(), "SET_LANDING",
            new { workspace = "aurora", form = "branch", pattern = "feature/{quest}-{slug}", plugin = "example.github-pull-request" });
        Assert.Equal("example.github-pull-request", DriverConfig.Load(DriverConfigPath).WorkspaceLandings["aurora"].Plugin);
        Assert.Equal("example.github-pull-request", state.GetProperty("workspaceLandings")[0].GetProperty("plugin").GetString());

        var merge = await RefusalAsync(Module(), "SET_LANDING", new { repository = "engine", form = "merge", plugin = "example.github-pull-request" });
        Assert.Contains("only a branch rule", merge);
    }

    /// <summary>The tidy rides the rule (D88), and is written only when on.</summary>
    [Fact]
    public async Task A_tidy_landing_rule_writes_the_same_file()
    {
        var state = await AnswerAsync(Module(), "SET_LANDING", new { repository = "engine", form = "merge", tidy = true });

        Assert.True(DriverConfig.Load(DriverConfigPath).Landings["engine"].Tidy);
        Assert.True(state.GetProperty("landings")[0].GetProperty("tidy").GetBoolean());
    }

    /// <summary>The clean-up reads the registry's checkouts and the sessions in use, so before the driver is up it is the cold-start sentence.</summary>
    [Fact]
    public async Task The_clean_up_before_the_driver_is_up_is_a_sentence()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "SWEEP_PLAN"));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "SWEEP", new { only = new[] { "engine:daoris/s-x" } }));
    }

    /// <summary>
    /// Bringing repositories up to date (WSR6) reads the registry's checkouts and the sessions in use, as the clean-up
    /// does, so before the driver is up both its list and its press are the cold-start sentence — and nothing is fetched.
    /// </summary>
    [Fact]
    public async Task Bringing_up_to_date_before_the_driver_is_up_is_a_sentence()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "TREES_SYNC_PLAN"));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "TREES_SYNC_PLAN", new { also = new[] { "game" } }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "TREES_SYNC_PLAN", new { all = true }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "TREES_SYNC", new { only = new[] { "engine:main" } }));
        // Which repositories a look would take (WSR7, D112) is read off the registry's checkouts too.
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "TREES_SYNC_SCOPE"));
    }

    /// <summary>
    /// Every door's press asks the sessions in use again inside each repository's hold (LEFT2), so a session a driver
    /// opened since the look is seen: Settings' *Bring up to date* and Ask Daoris's sync card alike (WSR7 d). Read from
    /// the modules' sources, as a reviewer would, since only a live driver can open a session between the two.
    /// </summary>
    [Fact]
    public void Every_press_of_bringing_up_to_date_asks_the_sessions_in_use_again()
    {
        var modules = Path.Combine(WorkspaceRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Modules");
        var presses = Directory.EnumerateFiles(modules, "DriverModule*.cs")
            .SelectMany(path =>
            {
                var source = File.ReadAllText(path);
                return System.Text.RegularExpressions.Regex.Matches(source, @"\.SyncAsync\(repositories\b")
                    .Select(press => (File: Path.GetFileName(path), Call: source[press.Index..source.IndexOf(';', press.Index)]));
            })
            .ToList();

        Assert.Contains(presses, press => press.File == "DriverModule.Lines.cs");
        Assert.Contains(presses, press => press.File == "DriverModule.Help.cs");
        Assert.All(presses, press => Assert.True(press.Call.Contains("inUseNow:", StringComparison.Ordinal),
            $"{press.File} presses bringing up to date without asking the sessions in use again: {press.Call}"));
    }

    private static string WorkspaceRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    /// <summary>What each repository's line is needs the registry's checkouts, so before the driver is up it is the cold-start sentence.</summary>
    [Fact]
    public async Task Asking_the_lines_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "LINES");

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }
}
