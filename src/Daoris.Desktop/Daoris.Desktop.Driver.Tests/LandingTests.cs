using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR1 (D87): how a session's work lands is a workspace's rule, with a repository's override — merged
/// into the line, or put on a branch named by a pattern for the person to push. Daoris never pushes.
/// Real git for the door, as the other tree tests use it.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class LandingTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;

    public LandingTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "landing", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
    }

    /// <summary>The pattern rule. The CLI's <c>driverconfig.test.ts</c> holds these cases, answer for answer.</summary>
    [Theory]
    [InlineData("feature/{quest}-{slug}", true)]
    [InlineData("review/{session}", true)]
    [InlineData("team/{repository}/{quest}", true)]
    [InlineData("", false)]
    [InlineData("feature/fixed", false)]
    [InlineData("feature/{slug}", false)]
    [InlineData("feature/{nope}-{quest}", false)]
    [InlineData("feature/{quest", false)]
    [InlineData("feature/{quest}..x", false)]
    [InlineData("feature {quest}", false)]
    public void A_pattern_names_a_branch_git_would_take_and_one_per_session(string pattern, bool valid) =>
        Assert.Equal(valid, LandingRules.Problem(pattern) is null);

    [Theory]
    [InlineData("Verify the streaming budget on the device", "verify-the-streaming-budget-on-the")]
    [InlineData("Fix: API gap (#12)", "fix-api-gap-12")]
    [InlineData("修复 登录", "work")]
    [InlineData(null, "work")]
    public void A_slug_is_the_title_in_words_git_takes(string? title, string slug) =>
        Assert.Equal(slug, LandingRules.Slug(title));

    /// <summary>
    /// WSR5, found landing AR-2202: a chain lands from its last step, and its branch was named for that
    /// step (`feature/verify-in-prod-…-381807d1f7bd`). It is named for the chain's first quest — the one
    /// the ask became — walking up each step's parent, and a quest with none is its own.
    /// </summary>
    [Fact]
    public async Task A_chain_lands_named_after_its_first_quest()
    {
        var quests = new Dictionary<string, QuestView>
        {
            ["34a9d57b8fd5"] = new("34a9d57b8fd5", "ask #53e0f1", "report-ui", "AR-2202: the OEE dashboard shows Empty Shackles 0", "", "Done"),
            ["381807d1f7bd"] = new("381807d1f7bd", "ask #53e0f1", "report-ui", "Verify in prod that AR-2202's reading is real", "", "Done") { Parent = "34a9d57b8fd5" },
            ["cafe01"] = new("cafe01", "ask #53e0f1", "report-ui", "a third step", "", "Open") { Parent = "381807d1f7bd" },
        };
        Task<QuestView?> Find(string id) => Task.FromResult(quests.GetValueOrDefault(id));

        var subject = await LandingRules.SubjectAsync("fd12f1bc", "cafe01", Find, opening: null);

        Assert.Equal(new LandingSubject("fd12f1bc", "34a9d57b8fd5", "AR-2202: the OEE dashboard shows Empty Shackles 0"), subject);
        Assert.Equal(new LandingSubject("s1", "34a9d57b8fd5", "AR-2202: the OEE dashboard shows Empty Shackles 0"),
            await LandingRules.SubjectAsync("s1", "34a9d57b8fd5", Find, opening: null));
        // A parent the service no longer answers for ends the walk where it is; a chat has its opening.
        Assert.Equal("cafe01", (await LandingRules.SubjectAsync("s2", "cafe01", id => Task.FromResult(id == "cafe01" ? quests[id] : null), null)).Quest);
        Assert.Equal(new LandingSubject("c1", null, "tidy the docs"), await LandingRules.SubjectAsync("c1", null, Find, "tidy the docs"));
    }

    [Fact]
    public void A_pattern_is_expanded_from_the_session_it_lands()
    {
        var names = new LandingNames("0fda18", "fix-api-gap", "engine", "s1a2b3c4");

        Assert.Equal("feature/0fda18-fix-api-gap", LandingRules.Expand("feature/{quest}-{slug}", names));
        Assert.Equal("team/engine/s1a2b3c4", LandingRules.Expand("team/{repository}/{session}", names));
    }

    [Fact]
    public void The_repositorys_rule_wins_then_its_workspaces_then_merge()
    {
        var branch = new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}");
        var config = DriverConfig.Empty
            .WithWorkspaceLanding("aurora", branch)
            .WithLanding("engine", LandingRule.Merge);

        Assert.Equal(new Landing(LandingRule.Merge, LandingSource.Repository), LandingRules.Choose(config, "engine", "aurora"));
        Assert.Equal(new Landing(branch, LandingSource.Workspace), LandingRules.Choose(config, "game", "aurora"));
        Assert.Equal(new Landing(LandingRule.Merge, LandingSource.Default), LandingRules.Choose(config, "game", "forge"));
        // A repository in no workspace is in `default`'s, as its line is (D86).
        Assert.Equal(LandingSource.Workspace,
            LandingRules.Choose(DriverConfig.Empty.WithWorkspaceLanding("default", branch), "game", null).Source);
    }

    [Fact]
    public void The_rules_are_kept_in_driver_json_and_one_that_could_not_land_is_not()
    {
        var config = DriverConfig.Parse("""
            {
              "landings": { "engine": { "form": "merge" }, "odd": { "form": "push" }, "bad": { "form": "branch", "pattern": "fixed" } },
              "workspaceLandings": { "aurora": { "form": "branch", "pattern": "feature/{quest}-{slug}" } }
            }
            """);

        Assert.Equal(LandingRule.Merge, config.Landings["engine"]);
        Assert.False(config.Landings.ContainsKey("odd"));
        Assert.False(config.Landings.ContainsKey("bad"));
        var again = DriverConfig.Parse(config.ToJson());
        Assert.Equal("feature/{quest}-{slug}", again.WorkspaceLandings["aurora"].Pattern);

        var cleared = config.WithLanding("engine", null).WithWorkspaceLanding("aurora", null);
        Assert.DoesNotContain("landings", cleared.ToJson(), StringComparison.OrdinalIgnoreCase);
        Assert.Throws<DriverException>(() => config.WithLanding("engine", new LandingRule(LandingForm.Branch, "fixed")));
        Assert.Throws<DriverException>(() => config.WithLanding("engine", new LandingRule("push")));
    }

    /// <summary>
    /// WSR4 (D100): a rule may name the plugin that pushes its branch and opens the pull request — only
    /// the branch form, since the plugin starts from the branch Daoris made, and only an id a plugin
    /// could have. The CLI's <c>driverconfig.test.ts</c> holds these cases, answer for answer.
    /// </summary>
    [Theory]
    [InlineData("branch", "feature/{quest}-{slug}", "example.github-pull-request", null)]
    [InlineData("branch", "feature/{quest}-{slug}", null, null)]
    [InlineData("merge", null, "example.github-pull-request", "only a branch")]
    [InlineData("branch", "feature/{quest}-{slug}", "Not An Id", "not a plugin id")]
    [InlineData("branch", "feature/{quest}-{slug}", "../elsewhere", "not a plugin id")]
    [InlineData("push", null, null, "a plugin's to do")]
    public void Only_a_branch_rule_names_a_plugin_and_only_by_an_id(string form, string? pattern, string? plugin, string? problem)
    {
        var said = LandingRules.Problem(new LandingRule(form, pattern, Plugin: plugin));

        if (problem is null) Assert.Null(said);
        else Assert.Contains(problem, said);
    }

    /// <summary>
    /// CASEFOLD1e: a rule's plugin is the catalogue's shape to its very end, as <see cref="PluginCatalog.IsId"/> is. .NET's
    /// <c>$</c> also matches before a final line break, where the CLI's <c>PLUGIN_ID</c> does not, so an id and a line break
    /// was a plugin's id here and none there.
    /// </summary>
    [Fact]
    public void A_rules_plugin_followed_by_a_line_break_is_no_id_as_the_cli_reads_it()
    {
        var said = LandingRules.Problem(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: "example.lands\n"));

        Assert.Contains("not a plugin id", said);
    }

    [Fact]
    public void A_rules_plugin_is_kept_in_driver_json_and_a_merge_naming_one_is_not_read()
    {
        var config = DriverConfig.Parse("""
            {
              "landings": { "odd": { "form": "merge", "plugin": "example.github-pull-request" } },
              "workspaceLandings": { "aurora": { "form": "branch", "pattern": "feature/{quest}-{slug}", "plugin": "example.github-pull-request" } }
            }
            """);

        Assert.False(config.Landings.ContainsKey("odd"));
        Assert.Equal("example.github-pull-request", config.WorkspaceLandings["aurora"].Plugin);
        Assert.Equal("example.github-pull-request", DriverConfig.Parse(config.ToJson()).WorkspaceLandings["aurora"].Plugin);
        // Written only when named: a rule with none reads as it always has.
        Assert.DoesNotContain("plugin", config.WithWorkspaceLanding("aurora", new LandingRule(LandingForm.Branch, "review/{session}")).ToJson());
        Assert.Throws<DriverException>(() => config.WithLanding("engine", new LandingRule(LandingForm.Merge, Plugin: "example.github-pull-request")));
    }

    /// <summary>
    /// A plugin the rule names must be on this machine, switched on, sound, and speak on the landing's
    /// point — each refused in its own sentence, which both doors give when the rule is set and the
    /// press gives again before anything is made.
    /// </summary>
    [Fact]
    public void A_plugin_that_is_missing_off_refused_or_lands_nothing_is_each_named()
    {
        Plugin("example.lands", """{ "id": "example.lands", "hooks": { "command": ["node", "land.mjs"], "points": ["work/land"] } }""");
        Plugin("example.watches", """{ "id": "example.watches", "hooks": { "command": ["node", "hooks.mjs"], "points": ["session/ended"] } }""");
        Plugin("example.broken", """{ "id": "example.broken", "apiVersion": 99 }""");
        Plugin("example.off", """{ "id": "example.off", "hooks": { "command": ["node", "land.mjs"], "points": ["work/land"] } }""");
        PluginState.Disable(_home, "example.off");
        var catalog = PluginCatalog.Load(_home);

        Assert.Null(LandingRules.PluginProblem("example.lands", catalog));
        Assert.Contains("not installed", LandingRules.PluginProblem("example.nowhere", catalog));
        Assert.Contains("daoris plugin enable example.off", LandingRules.PluginProblem("example.off", catalog));
        Assert.Contains("needs plugin API 99", LandingRules.PluginProblem("example.broken", catalog));
        Assert.Contains("`work/land`", LandingRules.PluginProblem("example.watches", catalog));
    }

    /// <summary>
    /// The branch form writes nothing to the checkout: the root may be dirty and on any branch, and it
    /// is left exactly so. The new branch holds the session's work, from the line it grew from.
    /// </summary>
    [Fact]
    public async Task The_branch_form_puts_the_work_on_a_new_branch_and_leaves_the_checkout_as_it_was()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "elsewhere");
        await GitAsync(root, "checkout", "--quiet", "elsewhere");
        await File.WriteAllTextAsync(Path.Combine(root, "in-flight.txt"), "the person's own work\n");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix the API gap"));

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal("feature/0fda18-fix-the-api-gap", landed.Branch);
        Assert.Contains("git push", landed.Message);
        Assert.Contains("the work", await GitAsync(root, "log", "feature/0fda18-fix-the-api-gap", "--oneline"));
        Assert.Equal("elsewhere", (await GitAsync(root, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Contains("in-flight.txt", await GitAsync(root, "status", "--porcelain"));
        Assert.DoesNotContain("the work", await GitAsync(root, "log", "main", "--oneline"));
        Assert.True(Directory.Exists(opened.Path), "the tree is the person's to discard");
    }

    [Fact]
    public async Task A_branch_that_exists_is_refused_and_never_moved()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "feature/0fda18-fix");
        var before = (await GitAsync(root, "rev-parse", "feature/0fda18-fix")).Trim();
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));

        Assert.False(landed.Landed);
        Assert.Contains("Daoris does not move a branch it did not make", landed.Message);
        Assert.Equal(before, (await GitAsync(root, "rev-parse", "feature/0fda18-fix")).Trim());
    }

    [Fact]
    public async Task Nothing_to_land_and_work_not_committed_are_each_refused_in_their_own_words()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "review/{session}"));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");

        var empty = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", null, null));
        Assert.False(empty.Landed);
        Assert.Contains("nothing to land", empty.Message);

        await File.WriteAllTextAsync(Path.Combine(opened.Path, "loose.txt"), "not committed\n");
        var dirty = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", null, null));
        Assert.False(dirty.Landed);
        Assert.Contains("uncommitted", dirty.Message);
        Assert.Empty((await GitAsync(root, "branch", "--list", "review/*")).Trim());
    }

    /// <summary>No rule is the merge it always was, through the same door.</summary>
    [Fact]
    public async Task With_no_rule_the_door_merges_into_the_line()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var plan = await trees.PlanAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));
        Assert.Equal(new LandingPlan(LandingForm.Merge, "main", LandingSource.Default), plan);

        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));
        Assert.True(landed.Landed, landed.Message);
        Assert.Contains("the work", await GitAsync(root, "log", "main", "--oneline"));
    }

    [Fact]
    public async Task The_plan_names_the_branch_a_press_would_make()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), workspace: true);
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");

        var plan = await trees.PlanAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix the API gap"));

        Assert.Equal(new LandingPlan(LandingForm.Branch, "feature/0fda18-fix-the-api-gap", LandingSource.Workspace), plan);
    }

    /// <summary>A session under the branch form is told its work goes through review, and is not to merge or push it.</summary>
    [Fact]
    public void The_instruction_says_how_the_work_will_land_and_says_nothing_under_merge()
    {
        var target = new SessionTarget("0fda18", "Fix", "the body", "game", "engine", "/tree", "http://localhost:0");

        var branch = TargetPrompt.Compose(target with { LandsOn = new LandingPlan(LandingForm.Branch, "feature/0fda18-fix", LandingSource.Workspace) });
        Assert.Contains("`feature/0fda18-fix`", branch);
        Assert.Contains("do not merge it, push it, or open a pull request", branch);

        // A plugin pushes it once accepted (D100) — and the session still pushes nothing itself.
        var handed = TargetPrompt.Compose(target with { LandsOn = new LandingPlan(LandingForm.Branch, "feature/0fda18-fix", LandingSource.Workspace, "example.github-pull-request") });
        Assert.Contains("the person accepts it, this tree's branch is put on `feature/0fda18-fix`, pushed", handed);
        Assert.Contains("do not merge it, push it, or open a pull request yourself", handed);

        Assert.Equal(TargetPrompt.Compose(target), TargetPrompt.Compose(target with { LandsOn = new LandingPlan(LandingForm.Merge, "main", LandingSource.Default) }));
    }

    // ——— LAND2c: a chain lands on one branch (D145 §3, D149)

    /// <summary>
    /// A chain's later step lands by moving the chain's branch on, as a fast-forward, from the commit the first landing made it
    /// at: the owner's AR-2203 shape, where a follow-up ask's drill-down follows the first ask's quest. It is named for the
    /// chain's first quest whichever ask published it, the record keeps the advance, the later session finds the landing as its
    /// own, and the checkout is not touched.
    /// </summary>
    [Fact]
    public async Task A_quest_following_the_chain_from_another_ask_moves_the_chains_branch_on()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        var quests = new Dictionary<string, QuestView>
        {
            ["q1"] = new("q1", "ask #a1", "engine", "Fix the gap", "", "Done"),
            ["q3"] = new("q3", "ask #a2", "engine", "Drill down into the gap", "", "Done") { Parent = "q1" },
        };
        Task<QuestView?> Find(string id) => Task.FromResult(quests.GetValueOrDefault(id));
        var trees = new SessionTrees(_home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "work.txt", "the first session's work", "the first work");
        var landed = await trees.LandAsync(first.Path, await LandingRules.SubjectAsync("s1", "q1", Find, null));
        Assert.True(landed.Landed, landed.Message);
        var tip = (await GitAsync(root, "rev-parse", "feature/q1-fix-the-gap")).Trim();

        var drill = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(drill.Path, "drill.txt", "the drill-down", "the drill-down");
        var head = (await GitAsync(drill.Path, "rev-parse", "HEAD")).Trim();
        var advanced = await trees.LandAsync(drill.Path, await LandingRules.SubjectAsync("s3", "q3", Find, null));

        Assert.True(advanced.Landed, advanced.Message);
        Assert.Equal("feature/q1-fix-the-gap", advanced.Branch);
        Assert.Equal(tip, advanced.AdvancedFrom);
        Assert.Contains("moved `feature/q1-fix-the-gap` on from", advanced.Message);
        Assert.Contains("1 more commit(s), 2 from `main` in all", advanced.Message);
        Assert.Equal(head, (await GitAsync(root, "rev-parse", "feature/q1-fix-the-gap")).Trim());
        Assert.Equal("main", (await GitAsync(root, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Single((await GitAsync(root, "branch", "--list", "feature/*")).Trim().Split('\n'));

        var entry = trees.Recorded.Landing("s3")!;
        Assert.Equal(("s1", head), (entry.Session, entry.Tip));
        var advance = Assert.Single(entry.Advances);
        Assert.Equal((tip, head, "s3", AcceptedBy.Person), (advance.From, advance.To, advance.Session, advance.AcceptedBy));
    }

    /// <summary>
    /// An advance refuses, each in its own words and moving nothing: a session whose work does not grow from the branch, a branch
    /// that moved since its landing, and one a working tree has checked out.
    /// </summary>
    [Fact]
    public async Task An_advance_is_refused_where_the_work_grows_apart_the_branch_moved_or_a_tree_stands_on_it()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "work.txt", "the first session's work", "the first work");
        Assert.True((await trees.LandAsync(first.Path, new LandingSubject("s1", "q1", "Fix the gap"))).Landed);
        const string chain = "feature/q1-fix-the-gap";
        var tip = (await GitAsync(root, "rev-parse", chain)).Trim();

        // Grown from the line, not from the chain's work: no fast-forward.
        var apart = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(apart.Path, "apart.txt", "work beside the chain", "work beside");
        var grewApart = await trees.LandAsync(apart.Path, new LandingSubject("s2", "q1", "Fix the gap"));
        Assert.False(grewApart.Landed);
        Assert.Equal(AutoLandingCode.Exists, grewApart.Refusal);
        Assert.Contains("does not grow from `feature/q1-fix-the-gap`", grewApart.Message);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", chain)).Trim());

        var next = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(next.Path, "next.txt", "the next step", "the next step");

        // Somebody moved it since its landing.
        var tree = (await GitAsync(root, "rev-parse", $"{tip}^{{tree}}")).Trim();
        var theirs = (await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture",
            "commit-tree", tree, "-p", tip, "-m", "somebody's commit")).Trim();
        await GitAsync(root, "update-ref", $"refs/heads/{chain}", theirs);
        var moved = await trees.LandAsync(next.Path, new LandingSubject("s3", "q1", "Fix the gap"));
        Assert.False(moved.Landed);
        Assert.Contains("moved since Daoris landed on it", moved.Message);
        Assert.Equal(theirs, (await GitAsync(root, "rev-parse", chain)).Trim());

        // Checked out in a working tree of the person's.
        await GitAsync(root, "update-ref", $"refs/heads/{chain}", tip);
        var elsewhere = Path.Combine(_scratch, "elsewhere");
        await GitAsync(root, "worktree", "add", "--quiet", elsewhere, chain);
        var standing = await trees.LandAsync(next.Path, new LandingSubject("s3", "q1", "Fix the gap"));
        Assert.False(standing.Landed);
        Assert.Contains("is checked out at", standing.Message);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", chain)).Trim());
        Assert.Empty(trees.Recorded.Of("engine", chain)!.Advances);
    }

    /// <summary>
    /// D149 point 3: a chain's branch whose pull request was merged — every commit on the line, or, after a squash, every file it
    /// changed reading there as it left it — is not moved on, since commits added now would ride no pull request.
    /// </summary>
    [Theory]
    [InlineData("merge")]
    [InlineData("squash")]
    public async Task A_chains_branch_whose_work_already_reads_on_the_line_is_not_moved_on(string how)
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "work.txt", "the first session's work", "the first work");
        Assert.True((await trees.LandAsync(first.Path, new LandingSubject("s1", "q1", "Fix the gap"))).Landed);
        const string chain = "feature/q1-fix-the-gap";
        var tip = (await GitAsync(root, "rev-parse", chain)).Trim();
        var next = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(next.Path, "next.txt", "the next step", "the next step");

        // The platform completed the first pull request into the line.
        var identity = new[] { "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture" };
        if (how == "merge") await GitAsync(root, [.. identity, "merge", "--no-ff", "--no-edit", chain]);
        else
        {
            await GitAsync(root, "merge", "--squash", chain);
            await GitAsync(root, [.. identity, "commit", "-m", "the first work, squashed"]);
        }

        var refused = await trees.LandAsync(next.Path, new LandingSubject("s2", "q1", "Fix the gap"));

        Assert.False(refused.Landed);
        Assert.Equal(AutoLandingCode.Completed, refused.Refusal);
        Assert.Contains("already reads on `main` — its pull request was merged", refused.Message);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", chain)).Trim());
    }

    /// <summary>
    /// D82 as D145 amends it: with the rule's tidy, the step before's session branch goes at its landing, so the next step grows
    /// from the chain's landed branch, says so, and its done moves that branch on.
    /// </summary>
    [Fact]
    public async Task A_next_step_grows_from_the_chains_branch_once_the_tidy_took_the_step_befores()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true));
        var trees = new SessionTrees(_home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "work.txt", "the first session's work", "the first work");
        Assert.True((await trees.LandAsync(first.Path, new LandingSubject("s1", "q1", "Fix the gap"))).Landed);
        Assert.Empty((await GitAsync(root, "branch", "--list", first.Branch)).Trim());
        const string chain = "feature/q1-fix-the-gap";

        var next = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch, landed: chain);

        Assert.Equal(chain, next.GrewFrom);
        Assert.Contains($"`{chain}`, the branch its chain landed on", next.BasedOn);
        Assert.Equal((await GitAsync(root, "rev-parse", chain)).Trim(), (await GitAsync(next.Path, "rev-parse", "HEAD")).Trim());
        await CommitAsync(next.Path, "next.txt", "the next step", "the next step");
        var advanced = await trees.LandAsync(next.Path, new LandingSubject("s2", "q1", "Fix the gap"));
        Assert.True(advanced.Landed, advanced.Message);
        Assert.NotNull(advanced.AdvancedFrom);
        Assert.False(Directory.Exists(next.Path), "the rule's tidy follows an advance as it follows a landing");
    }

    /// <summary>
    /// D149 point 4: at an advance the rule's plugin is told the pull request its first landing opened, and who accepted this
    /// work, so it pushes and opens no second one; an answer naming no pull request keeps the one the record holds.
    /// </summary>
    [Fact]
    public async Task The_plugin_is_told_the_open_pull_request_at_an_advance_and_who_accepted_it()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: "example.lands", AutoAccept: true));
        Plugin("example.lands", """{ "id": "example.lands", "hooks": { "command": ["node", "land.mjs"], "points": ["work/land"] } }""");
        var frames = new List<System.Text.Json.JsonElement>();
        var trees = new SessionTrees(_home, new LandingPlugins(_home, start: (_, _, _) => Task.FromResult<IHookChannel>(new FakeLander(frame =>
        {
            var said = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(frame)).RootElement.Clone();
            frames.Add(said);
            return said.GetProperty("pullRequest").ValueKind == System.Text.Json.JsonValueKind.Null
                ? new PluginLanding("example.lands", true, "https://example.test/pull/7", "pushed it and opened a pull request.")
                : new PluginLanding("example.lands", true, null, "pushed it; its pull request carries the new commits.");
        }))));
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "work.txt", "the first session's work", "the first work");
        Assert.True((await trees.LandAsync(first.Path, new LandingSubject("s1", "q1", "Fix the gap"))).Landed);
        var next = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(next.Path, "next.txt", "the next step", "the next step");

        var advanced = await trees.LandAsync(next.Path, new LandingSubject("s2", "q1", "Fix the gap"), acceptedBy: AcceptedBy.Auto);

        Assert.True(advanced.Landed, advanced.Message);
        Assert.Equal(2, frames.Count);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, frames[0].GetProperty("pullRequest").ValueKind);
        Assert.Equal(AcceptedBy.Person, frames[0].GetProperty("acceptedBy").GetString());
        Assert.Equal("https://example.test/pull/7", frames[1].GetProperty("pullRequest").GetString());
        Assert.Equal(AcceptedBy.Auto, frames[1].GetProperty("acceptedBy").GetString());
        Assert.Equal(2, frames[1].GetProperty("commits").GetArrayLength());
        var entry = trees.Recorded.Of("engine", "feature/q1-fix-the-gap")!;
        Assert.Equal("https://example.test/pull/7", entry.PullRequest);
        Assert.Equal((await GitAsync(root, "rev-parse", "feature/q1-fix-the-gap")).Trim(), entry.PushedTip);
        Assert.Equal(AutoLandingCode.Advanced, AutoLandingRules.CodeOf(advanced));
    }

    /// <summary>A plugin faked on the wire's channel: nothing reaches a network or a platform.</summary>
    private sealed class FakeLander(Func<object, PluginLanding> land) : IHookChannel
    {
        public IReadOnlyList<string> Points => [HookPoints.Land];
        public bool Alive => true;
        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);
        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;
        public Task<PluginLanding> LandAsync(object payload, CancellationToken ct) => Task.FromResult(land(payload));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private void Plugin(string id, string manifest)
    {
        var folder = Path.Combine(_home, "plugins", id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), manifest);
    }

    private void Rule(LandingRule rule, bool workspace = false) =>
        (workspace ? DriverConfig.Empty.WithWorkspaceLanding("aurora", rule) : DriverConfig.Empty.WithLanding("engine", rule))
            .Save(Path.Combine(_home, "driver.json"));

    private async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await CommitAsync(root, "README.md", $"# {name}", "first");
        return root;
    }

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }

    private static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        // 🔴 Both streams are read at once, before the wait: read one after the other, a git that writes more than a pipe's
        // worth of warnings to the second blocks on it while the first is still being read (FIX-LOG 2026-10-04).
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        return await stdout;
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git"))
               && !File.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
