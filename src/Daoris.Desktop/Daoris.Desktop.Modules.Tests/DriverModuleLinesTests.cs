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

    /// <summary>
    /// LAND2a (D145): *Accept automatically* rides the rule as the tidy does — written only when on, read back for the
    /// switch, and a merge carrying it refused in the driver's words with nothing written.
    /// </summary>
    [Fact]
    public async Task An_automatic_acceptance_rides_the_rule_and_a_merge_with_it_is_refused()
    {
        var state = await AnswerAsync(Module(), "SET_LANDING",
            new { workspace = "aurora", form = "branch", pattern = "feature/{quest}-{slug}", autoAccept = true });

        Assert.True(DriverConfig.Load(DriverConfigPath).WorkspaceLandings["aurora"].AutoAccept);
        Assert.True(state.GetProperty("workspaceLandings")[0].GetProperty("autoAccept").GetBoolean());

        var merge = await RefusalAsync(Module(), "SET_LANDING", new { repository = "engine", form = "merge", autoAccept = true });
        Assert.Contains("only a branch rule accepts automatically", merge);
        Assert.False(DriverConfig.Load(DriverConfigPath).Landings.ContainsKey("engine"));
    }

    /// <summary>The clean-up reads the registry's checkouts and the sessions in use, so before the driver is up it is the cold-start sentence.</summary>
    [Fact]
    public async Task The_clean_up_before_the_driver_is_up_is_a_sentence()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "SWEEP_PLAN"));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "SWEEP", new { only = new[] { "engine:daoris/s-x" } }));
    }

    /// <summary>
    /// LAND3b (D102's LAND3 note): a clean-up row says whether its discard is offered beside it, by the driver's own rule
    /// (<see cref="SessionTrees.RemovalOffered"/>, the line `trees clean` prints beside such a row), so the page and the
    /// terminal offer it beside the same rows: a failed or superseded attempt's commits, which no clean-up takes.
    /// </summary>
    [Fact]
    public void A_clean_up_row_says_whether_its_discard_is_offered_beside_it_as_the_terminal_s_list_does()
    {
        var camel = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        SweepItem[] items =
        [
            new("engine", "aurora", "daoris/s-1a2b3c4d", null, SweepKind.Unlanded, 2, null, "a1b2c3d the work"),
            new("engine", "aurora", "daoris/s-2b3c4d5e", "tree", SweepKind.Unlanded, 1, null, "e4f5a6b more work"),
            new("engine", "aurora", "daoris/s-3c4d5e6f", null, SweepKind.Unlanded, 0, null, "git could not tell"),
            new("engine", "aurora", "daoris/s-4d5e6f7a", null, SweepKind.Empty, 0, "main", null),
            new("engine", "aurora", "daoris/s-5e6f7a8b", null, SweepKind.Landed, 3, "feature/x", null),
            new("engine", "aurora", "daoris/s-6f7a8b9c", "tree", SweepKind.Dirty, 0, null, "2 path(s)"),
            new("engine", "aurora", "daoris/s-7a8b9c0d", "tree", SweepKind.InUse, 0, null, null),
        ];

        var offered = items.Select(item => JsonSerializer.SerializeToElement(DriverModule.SweepRow(item), camel).GetProperty("discardable").GetBoolean()).ToArray();

        Assert.Equal([true, true, false, false, false, false, false], offered);
        Assert.Equal(items.Select(item => SessionTrees.RemovalOffered(item) is not null), offered);
    }

    /// <summary>
    /// A failed or superseded attempt's branch discarded from the page (LAND3b, D102's LAND3 note) is the terminal's
    /// `trees remove &lt;branch&gt; --repository &lt;name&gt; --force`: it reads the registry's checkout and the sessions in
    /// use, so before the driver is up it is the cold-start sentence, and a discard that names no branch or no repository
    /// is a malformed call.
    /// </summary>
    [Fact]
    public async Task Discarding_a_session_branch_before_the_driver_is_up_is_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "DISCARD_SESSION_BRANCH",
            new { repository = "engine", branch = "daoris/s-1a2b3c4d", force = true });

        Assert.Contains(Refusals.DriverNotReady, refusal);
        Assert.Contains("still coming up", refusal);
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "DISCARD_SESSION_BRANCH", new { repository = "engine" }));
        await Assert.ThrowsAnyAsync<Exception>(() => AnswerAsync(Module(), "DISCARD_SESSION_BRANCH", new { branch = "daoris/s-1a2b3c4d" }));
    }

    /// <summary>No sessions in use, as the service's ledger answers when nothing runs.</summary>
    private static Task<IReadOnlySet<string>> NoneInUse(CancellationToken _) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    /// <summary>
    /// The branch is removed in its repository's own checkout, which the registry names (LAND3b): with none on this
    /// machine, the terminal's sentence, and nothing is asked of git.
    /// </summary>
    [Fact]
    public async Task A_session_branch_is_discarded_only_where_its_repository_has_a_checkout_here()
    {
        var removal = await DriverModule.DiscardBranchAsync(
            new SessionTrees(Home), "engine", "daoris/s-1a2b3c4d", force: true,
            checkout: _ => Task.FromResult<string?>(null), inUse: NoneInUse, CancellationToken.None);

        Assert.False(removal.Removed);
        Assert.Equal("`engine` has no checkout here, so its branch `daoris/s-1a2b3c4d` cannot be removed from this machine.", removal.Message);
    }

    /// <summary>
    /// A branch whose tree a session still running or waiting names is kept, whatever the press says (D88's keep): forced,
    /// the driver's removal would take that tree with it. The checkout is not asked for.
    /// </summary>
    [Fact]
    public async Task A_branch_whose_tree_a_session_still_holds_is_kept_even_forced()
    {
        var tree = Path.Combine(Home, "trees", "aurora", "Engine", "s-1a2b3c4d");
        var asked = false;

        var removal = await DriverModule.DiscardBranchAsync(
            new SessionTrees(Home), "engine", "daoris/s-1a2b3c4d", force: true,
            checkout: _ =>
            {
                asked = true;
                return Task.FromResult<string?>(Home);
            },
            inUse: _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { tree }),
            CancellationToken.None);

        Assert.False(removal.Removed);
        Assert.Contains("still running or waiting", removal.Message);
        Assert.Contains("`daoris/s-1a2b3c4d`", removal.Message);
        Assert.False(asked);
    }

    /// <summary>
    /// Only Daoris's own branches are discarded here, refused in the driver's own words before git is asked: a branch of
    /// the person's is theirs to delete.
    /// </summary>
    [Fact]
    public async Task Only_a_session_branch_is_discarded_and_its_refusal_is_the_drivers()
    {
        var removal = await DriverModule.DiscardBranchAsync(
            new SessionTrees(Home), "engine", "feature/0fda18-fix", force: true,
            checkout: _ => Task.FromResult<string?>(Home), inUse: NoneInUse, CancellationToken.None);

        Assert.False(removal.Removed);
        Assert.Contains("is not a session branch", removal.Message);
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

    /// <summary>
    /// LOOK2a: right after a start, Settings → Workspace said no repository here had a line, and kept saying it. The
    /// lines refuse *still coming up* until the loop's service is up, never answering an empty list for it; the moment it
    /// is, the state says so, the page is told once (<c>DRIVER_READY</c>), which is when it asks again what the driver
    /// refused, and the lines answer every repository the registry holds.
    /// </summary>
    [Fact]
    public async Task The_lines_refuse_until_the_drivers_service_is_up_and_the_page_is_told_when_it_is()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(module, "LINES"));
        Assert.False((await AnswerAsync(module, "STATE")).GetProperty("ready").GetBoolean());
        Assert.DoesNotContain(Raised, message => message.Type == "DRIVER_READY");

        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandInService())));

        Assert.True((await AnswerAsync(module, "STATE")).GetProperty("ready").GetBoolean());
        Assert.Single(Raised, message => message.Type == "DRIVER_READY");
        var lines = (await AnswerAsync(module, "LINES")).GetProperty("lines");
        Assert.Equal(["engine", "game"], lines.EnumerateArray().Select(line => line.GetProperty("repository").GetString()));
    }

    /// <summary>The service a driver reads its snapshot from, standing in: two registered repositories, no quest, no session.</summary>
    private sealed class StandInService : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.AbsolutePath == "/api/registry"
                ? """[{"repository":"game","workspace":"aurora"},{"repository":"engine","workspace":"aurora"}]"""
                : "[]";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
