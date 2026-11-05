using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR1 (D87): how a session's work lands is a workspace's rule, with a repository's override — merged
/// into the line, or put on a branch named by a pattern for the person to push. Daoris never pushes.
/// Real git for the door, as the other tree tests use it.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
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
        GitSeed.Discard(_scratch);
    }

    /// <summary>The pattern rule.</summary>
    [Theory]
    [InlineData("feature/{quest}-{slug}", true)]
    [InlineData("review/{session}", true)]
    [InlineData("team/{repository}/{quest}", true)]
    [InlineData("feature/{ticket}-{slug}", true)]
    [InlineData("feature/{ticket}", true)]
    [InlineData("", false)]
    [InlineData("feature/fixed", false)]
    [InlineData("feature/{slug}", false)]
    [InlineData("feature/{nope}-{quest}", false)]
    [InlineData("feature/{quest", false)]
    [InlineData("feature/{quest}..x", false)]
    [InlineData("feature {quest}", false)]
    public void A_pattern_names_a_branch_git_would_take_and_one_per_session(string pattern, bool valid) =>
        Assert.Equal(valid, BranchPatterns.Problem(pattern) is null);

    [Theory]
    [InlineData("Verify the streaming budget on the device", "verify-the-streaming-budget-on-the")]
    [InlineData("Fix: API gap (#12)", "fix-api-gap-12")]
    [InlineData("修复 登录", "work")]
    [InlineData(null, "work")]
    public void A_slug_is_the_title_in_words_git_takes(string? title, string slug) =>
        Assert.Equal(slug, BranchPatterns.Slug(title));

    /// <summary>
    /// WSR5, found landing TK-2202: a chain lands from its last step, and its branch was named for that
    /// step (`feature/verify-in-prod-…-381807d1f7bd`). It is named for the chain's first quest — the one
    /// the ask became — walking up each step's parent, and a quest with none is its own.
    /// </summary>
    [Fact]
    public async Task A_chain_lands_named_after_its_first_quest()
    {
        var quests = new Dictionary<string, QuestView>
        {
            ["34a9d57b8fd5"] = new("34a9d57b8fd5", "ask #53e0f1", "portal-ui", "TK-2202: the OEE dashboard shows Empty Shackles 0", "", "Done"),
            ["381807d1f7bd"] = new("381807d1f7bd", "ask #53e0f1", "portal-ui", "Verify in prod that TK-2202's reading is real", "", "Done") { Parent = "34a9d57b8fd5" },
            ["cafe01"] = new("cafe01", "ask #53e0f1", "portal-ui", "a third step", "", "Open") { Parent = "381807d1f7bd" },
        };
        Task<QuestView?> Find(string id) => Task.FromResult(quests.GetValueOrDefault(id));

        var subject = await BranchPatterns.SubjectAsync("fd12f1bc", "cafe01", Find, opening: null);

        Assert.Equal(new LandingSubject("fd12f1bc", "34a9d57b8fd5", "TK-2202: the OEE dashboard shows Empty Shackles 0"), subject);
        Assert.Equal(new LandingSubject("s1", "34a9d57b8fd5", "TK-2202: the OEE dashboard shows Empty Shackles 0"),
            await BranchPatterns.SubjectAsync("s1", "34a9d57b8fd5", Find, opening: null));
        // A parent the service no longer answers for ends the walk where it is; a chat has its opening.
        Assert.Equal("cafe01", (await BranchPatterns.SubjectAsync("s2", "cafe01", id => Task.FromResult(id == "cafe01" ? quests[id] : null), null)).Quest);
        Assert.Equal(new LandingSubject("c1", null, "tidy the docs"), await BranchPatterns.SubjectAsync("c1", null, Find, "tidy the docs"));
    }

    [Fact]
    public void A_pattern_is_expanded_from_the_session_it_lands()
    {
        var names = new LandingNames("0fda18", "fix-api-gap", "engine", "s1a2b3c4");

        Assert.Equal("feature/0fda18-fix-api-gap", BranchPatterns.Expand("feature/{quest}-{slug}", names));
        Assert.Equal("team/engine/s1a2b3c4", BranchPatterns.Expand("team/{repository}/{session}", names));
    }

    /// <summary>
    /// A plugin the rule names must be on this machine, switched on, sound, and speak on the landing's
    /// point — each refused in its own sentence, which both doors give when the rule is set and the
    /// press gives again before anything is made.
    /// </summary>
    [Fact]
    public void A_plugin_that_is_missing_off_refused_or_lands_nothing_is_each_named()
    {
        Plugin("example.lands", """{ "id": "example.lands", "hooks": { "command": ["node", "land.mjs"], "points": ["work/perform"] } }""");
        Plugin("example.watches", """{ "id": "example.watches", "hooks": { "command": ["node", "hooks.mjs"], "points": ["session/ended"] } }""");
        Plugin("example.broken", """{ "id": "example.broken", "apiVersion": 99 }""");
        Plugin("example.off", """{ "id": "example.off", "hooks": { "command": ["node", "land.mjs"], "points": ["work/perform"] } }""");
        PluginState.Disable(_home, "example.off");
        var catalog = PluginCatalog.Load(_home);

        Assert.Null(LandingPlugins.ProblemOf("example.lands", catalog));
        Assert.Contains("not installed", LandingPlugins.ProblemOf("example.nowhere", catalog));
        Assert.Contains("daoris plugin enable example.off", LandingPlugins.ProblemOf("example.off", catalog));
        Assert.Contains("needs plugin API 99", LandingPlugins.ProblemOf("example.broken", catalog));
        Assert.Contains("`work/perform`", LandingPlugins.ProblemOf("example.watches", catalog));
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
        Rule(GraphLand.Branch("feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var landed = await LandAsync(trees, opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix the API gap"));

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal("feature/0fda18-fix-the-api-gap", landed.Branch);
        Assert.Contains("git push", landed.Message);
        Assert.Contains("the work", await GitAsync(root, "log", "feature/0fda18-fix-the-api-gap", "--oneline"));
        Assert.Equal("elsewhere", (await GitAsync(root, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Contains("in-flight.txt", await GitAsync(root, "status", "--porcelain"));
        Assert.DoesNotContain("the work", await GitAsync(root, "log", "main", "--oneline"));
        Assert.True(Directory.Exists(opened.Path), "the tree is the person's to discard");
    }

    /// <summary>
    /// ACCEPTFOOT2 (D164's 2026-10-16 note): a conversation's plan and press name its branch for its work's oldest commit subject
    /// beyond the line, never for its opening, and the two name one branch.
    /// </summary>
    [Fact]
    public async Task A_conversations_branch_is_named_for_its_oldest_commit_never_its_opening()
    {
        var root = await RepositoryAsync("engine");
        Rule(GraphLand.Branch("feature/{slug}-{session}"));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the conversation's work", "Cap the chunk budget");
        await CommitAsync(opened.Path, "more.txt", "and its test", "Test the cap");
        var subject = new LandingSubject("s1a2b3c4", null, "ok so that is merged now, what about the rest");

        var plan = await PlanAsync(trees,opened.Path, subject);
        var landed = await LandAsync(trees, opened.Path, subject);

        Assert.Equal("feature/cap-the-chunk-budget-s1a2b3c4", plan.Target);
        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(plan.Target, landed.Branch);
    }

    /// <summary>An oldest commit with an empty message names the work `work`, never the commit after it (ACCEPTFOOT2).</summary>
    [Fact]
    public async Task A_conversations_oldest_commit_with_no_message_names_it_work()
    {
        var root = await RepositoryAsync("engine");
        Rule(GraphLand.Branch("feature/{slug}-{session}"));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(opened.Path, "work.txt"), "the conversation's work\n");
        await GitAsync(opened.Path, "add", "work.txt");
        await GitAsync(opened.Path, "commit", "--quiet", "--allow-empty-message", "-m", "");
        await CommitAsync(opened.Path, "more.txt", "and its test", "Add tests");

        var plan = await PlanAsync(trees,opened.Path, new LandingSubject("s1a2b3c4", null, "ok so that is merged now"));

        Assert.Equal("feature/work-s1a2b3c4", plan.Target);
    }

    [Fact]
    public async Task A_branch_that_exists_is_refused_and_never_moved()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "feature/0fda18-fix");
        var before = (await GitAsync(root, "rev-parse", "feature/0fda18-fix")).Trim();
        Rule(GraphLand.Branch("feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var landed = await LandAsync(trees, opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));

        Assert.False(landed.Landed);
        Assert.Contains("Daoris does not move a branch it did not make", landed.Message);
        Assert.Equal(before, (await GitAsync(root, "rev-parse", "feature/0fda18-fix")).Trim());
    }

    [Fact]
    public async Task Nothing_to_land_and_work_not_committed_are_each_refused_in_their_own_words()
    {
        var root = await RepositoryAsync("engine");
        Rule(GraphLand.Branch("review/{session}"));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");

        var empty = await LandAsync(trees, opened.Path, new LandingSubject("s1a2b3c4", null, null));
        Assert.False(empty.Landed);
        Assert.Contains("nothing to land", empty.Message);

        await File.WriteAllTextAsync(Path.Combine(opened.Path, "loose.txt"), "not committed\n");
        var dirty = await LandAsync(trees, opened.Path, new LandingSubject("s1a2b3c4", null, null));
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

        var plan = await PlanAsync(trees,opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));
        Assert.Equal(new LandingPlan(LandingForm.Merge, "main", LandingSource.Default), plan);

        var landed = await LandAsync(trees, opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));
        Assert.True(landed.Landed, landed.Message);
        Assert.Contains("the work", await GitAsync(root, "log", "main", "--oneline"));
    }

    [Fact]
    public async Task The_plan_names_the_branch_a_press_would_make()
    {
        var root = await RepositoryAsync("engine");
        Rule(GraphLand.Branch("feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");

        var plan = await PlanAsync(trees,opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix the API gap"));

        Assert.Equal((LandingForm.Branch, "feature/0fda18-fix-the-api-gap"), (plan.Form, plan.Target));
    }

    // ——— LANDNAME1a: an entered branch name replaces the rule's wholesale

    [Fact]
    public async Task An_entered_name_replaces_the_rules_whole_name_and_the_record_keeps_it()
    {
        var root = await RepositoryAsync("engine");
        Rule(GraphLand.Branch("feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");
        var subject = new LandingSubject("s1a2b3c4", "0fda18", "Fix the API gap") { Entered = "  my/own-name  " };

        var plan = await PlanAsync(trees,opened.Path, subject);
        Assert.Equal("my/own-name", plan.Target);
        Assert.Null(plan.Problem);

        var landed = await LandAsync(trees, opened.Path, subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal("my/own-name", landed.Branch);
        Assert.Contains("the work", await GitAsync(root, "log", "my/own-name", "--oneline"));
        Assert.Empty((await GitAsync(root, "branch", "--list", "feature/*")).Trim());
        Assert.Equal("my/own-name", new LandedBranches(_home).All().Single().Branch);
    }

    [Fact]
    public async Task A_blank_entered_name_keeps_the_rules()
    {
        var root = await RepositoryAsync("engine");
        Rule(GraphLand.Branch("feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var landed = await LandAsync(trees, opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix") { Entered = "   " });

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal("feature/0fda18-fix", landed.Branch);
    }

    [Fact]
    public async Task An_entered_name_git_would_not_take_is_refused_in_words_about_the_name()
    {
        var root = await RepositoryAsync("engine");
        Rule(GraphLand.Branch("feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");
        var subject = new LandingSubject("s1a2b3c4", "0fda18", "Fix") { Entered = "no good..name" };

        Assert.NotNull((await PlanAsync(trees,opened.Path, subject)).Problem);
        var landed = await LandAsync(trees, opened.Path, subject);

        Assert.False(landed.Landed);
        Assert.Contains("`no good..name`", landed.Message);
        Assert.Contains("entered", landed.Message);
        Assert.DoesNotContain("pattern", landed.Message);
    }

    [Fact]
    public async Task An_entered_name_that_is_a_branch_Daoris_did_not_make_is_refused_and_never_moved()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "theirs");
        var before = (await GitAsync(root, "rev-parse", "theirs")).Trim();
        Rule(GraphLand.Branch("feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var landed = await LandAsync(trees, opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix") { Entered = "theirs" });

        Assert.False(landed.Landed);
        Assert.Contains("`theirs`", landed.Message);
        Assert.Contains("entered", landed.Message);
        Assert.DoesNotContain("pattern", landed.Message);
        Assert.Equal(before, (await GitAsync(root, "rev-parse", "theirs")).Trim());
    }

    [Fact]
    public async Task A_merge_rule_refuses_an_entered_name_in_words_and_merges_nothing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");
        var subject = new LandingSubject("s1a2b3c4", "0fda18", "Fix") { Entered = "my/own-name" };

        Assert.Contains("merge", (await PlanAsync(trees,opened.Path, subject)).Problem);
        var landed = await LandAsync(trees, opened.Path, subject);

        Assert.False(landed.Landed);
        Assert.Contains("makes no branch", landed.Message);
        Assert.DoesNotContain("the work", await GitAsync(root, "log", "main", "--oneline"));
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
    /// at: the owner's TK-2203 shape, where a follow-up ask's drill-down follows the first ask's quest. It is named for the
    /// chain's first quest whichever ask published it, the record keeps the advance, the later session finds the landing as its
    /// own, and the checkout is not touched.
    /// </summary>
    [Fact]
    public async Task A_quest_following_the_chain_from_another_ask_moves_the_chains_branch_on()
    {
        var root = await RepositoryAsync("engine");
        Rule(GraphLand.Branch("feature/{quest}-{slug}"));
        var quests = new Dictionary<string, QuestView>
        {
            ["q1"] = new("q1", "ask #a1", "engine", "Fix the gap", "", "Done"),
            ["q3"] = new("q3", "ask #a2", "engine", "Drill down into the gap", "", "Done") { Parent = "q1" },
        };
        Task<QuestView?> Find(string id) => Task.FromResult(quests.GetValueOrDefault(id));
        var trees = new SessionTrees(_home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "work.txt", "the first session's work", "the first work");
        var landed = await LandAsync(trees, first.Path, await BranchPatterns.SubjectAsync("s1", "q1", Find, null));
        Assert.True(landed.Landed, landed.Message);
        var tip = (await GitAsync(root, "rev-parse", "feature/q1-fix-the-gap")).Trim();

        var drill = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(drill.Path, "drill.txt", "the drill-down", "the drill-down");
        var head = (await GitAsync(drill.Path, "rev-parse", "HEAD")).Trim();
        var advanced = await LandAsync(trees, drill.Path, await BranchPatterns.SubjectAsync("s3", "q3", Find, null));

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
        Rule(GraphLand.Branch("feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "work.txt", "the first session's work", "the first work");
        Assert.True((await LandAsync(trees, first.Path, new LandingSubject("s1", "q1", "Fix the gap"))).Landed);
        const string chain = "feature/q1-fix-the-gap";
        var tip = (await GitAsync(root, "rev-parse", chain)).Trim();

        // Grown from the line, not from the chain's work: no fast-forward.
        var apart = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(apart.Path, "apart.txt", "work beside the chain", "work beside");
        var grewApart = await LandAsync(trees, apart.Path, new LandingSubject("s2", "q1", "Fix the gap"));
        Assert.False(grewApart.Landed);
        Assert.Equal(LandingCode.Exists, grewApart.Refusal);
        Assert.Contains("does not grow from `feature/q1-fix-the-gap`", grewApart.Message);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", chain)).Trim());

        var next = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(next.Path, "next.txt", "the next step", "the next step");

        // Somebody moved it since its landing.
        var tree = (await GitAsync(root, "rev-parse", $"{tip}^{{tree}}")).Trim();
        var theirs = (await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture",
            "commit-tree", tree, "-p", tip, "-m", "somebody's commit")).Trim();
        await GitAsync(root, "update-ref", $"refs/heads/{chain}", theirs);
        var moved = await LandAsync(trees, next.Path, new LandingSubject("s3", "q1", "Fix the gap"));
        Assert.False(moved.Landed);
        Assert.Contains("moved since Daoris landed on it", moved.Message);
        Assert.Equal(theirs, (await GitAsync(root, "rev-parse", chain)).Trim());

        // Checked out in a working tree of the person's.
        await GitAsync(root, "update-ref", $"refs/heads/{chain}", tip);
        var elsewhere = Path.Combine(_scratch, "elsewhere");
        await GitAsync(root, "worktree", "add", "--quiet", elsewhere, chain);
        var standing = await LandAsync(trees, next.Path, new LandingSubject("s3", "q1", "Fix the gap"));
        Assert.False(standing.Landed);
        Assert.Contains("is checked out at", standing.Message);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", chain)).Trim());
        Assert.Empty(trees.Recorded.Of("engine", chain)!.Advances);
    }

    /// <summary>
    /// STANDSREFUSE1 (D167's ACCEPTALL1g note, D145 §3): the plan says the refusal a standing branch would meet, in the press's own
    /// sentence, before the press: one Daoris did not make, one moved since it landed on it, and one a working tree has checked out.
    /// One it may move on says none, and still stands.
    /// </summary>
    [Fact]
    public async Task The_plan_says_the_refusal_a_standing_branch_would_meet_before_the_press()
    {
        var root = await RepositoryAsync("engine");
        Rule(GraphLand.Branch("feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        const string chain = "feature/q1-fix-the-gap";

        // Somebody's branch of the pattern's name: the press refuses it, and the plan says so first.
        await GitAsync(root, "branch", "feature/q0-fix-the-gap");
        var theirsTree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(theirsTree.Path, "theirs.txt", "work for a branch somebody made", "work for theirs");
        var notOurs = await PlanAsync(trees,theirsTree.Path, new LandingSubject("s0", "q0", "Fix the gap"));
        Assert.True(notOurs.Stands);
        Assert.Contains("Daoris does not move a branch it did not make", notOurs.Problem);
        Assert.StartsWith(notOurs.Problem!, (await LandAsync(trees, theirsTree.Path, new LandingSubject("s0", "q0", "Fix the gap"))).Message);

        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "work.txt", "the first session's work", "the first work");
        Assert.True((await LandAsync(trees, first.Path, new LandingSubject("s1", "q1", "Fix the gap"))).Landed);
        var tip = (await GitAsync(root, "rev-parse", chain)).Trim();
        var next = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(next.Path, "next.txt", "the next step", "the next step");
        var subject = new LandingSubject("s3", "q1", "Fix the gap");

        // Daoris's own, at its recorded tip and checked out nowhere: it moves on, and nothing is said against it.
        var mayMove = await PlanAsync(trees,next.Path, subject);
        Assert.Equal((chain, true, (string?)null), (mayMove.Target, mayMove.Stands, mayMove.Problem));

        // Somebody moved it since its landing.
        var tree = (await GitAsync(root, "rev-parse", $"{tip}^{{tree}}")).Trim();
        var theirs = (await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture",
            "commit-tree", tree, "-p", tip, "-m", "somebody's commit")).Trim();
        await GitAsync(root, "update-ref", $"refs/heads/{chain}", theirs);
        Assert.Contains("moved since Daoris landed on it", (await PlanAsync(trees,next.Path, subject)).Problem);

        // Checked out in a working tree of the person's.
        await GitAsync(root, "update-ref", $"refs/heads/{chain}", tip);
        var elsewhere = Path.Combine(_scratch, "elsewhere");
        await GitAsync(root, "worktree", "add", "--quiet", elsewhere, chain);
        var checkedOut = await PlanAsync(trees,next.Path, subject);
        Assert.Contains("is checked out at", checkedOut.Problem);
        Assert.StartsWith(checkedOut.Problem!, (await LandAsync(trees, next.Path, subject)).Message);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", chain)).Trim());
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
        Rule(GraphLand.Branch("feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "work.txt", "the first session's work", "the first work");
        Assert.True((await LandAsync(trees, first.Path, new LandingSubject("s1", "q1", "Fix the gap"))).Landed);
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

        var refused = await LandAsync(trees, next.Path, new LandingSubject("s2", "q1", "Fix the gap"));

        Assert.False(refused.Landed);
        Assert.Equal(LandingCode.Completed, refused.Refusal);
        Assert.Contains("already reads on `main` — its pull request was merged", refused.Message);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", chain)).Trim());
    }

    /// <summary>SESSONE1d: where no branch holds that work, the part grows from the line, and says that the tree it would have gone into is gone.</summary>
    [Fact]
    public async Task A_further_part_whose_sessions_tree_is_gone_with_nothing_landed_grows_from_the_line_and_says_so()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);

        var next = await trees.OpenAsync(root, "engine", "aurora", landed: "feature/q1-never-landed", partOf: "s1");

        Assert.Null(next.GrewFrom);
        Assert.Contains("the tree session `s1` worked in is gone, and `feature/q1-never-landed`, the branch its work landed on, is gone too", next.BasedOn);
        Assert.Equal((await GitAsync(root, "rev-parse", "main")).Trim(), (await GitAsync(next.Path, "rev-parse", "HEAD")).Trim());

        var bare = await trees.OpenAsync(root, "engine", "aurora", partOf: "s1");
        Assert.Contains("the tree session `s1` worked in is gone, and no branch holds its work here", bare.BasedOn);
    }

    private void Plugin(string id, string manifest)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins,id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), manifest);
    }

    private IWorkplaceGate? _gate;

    private void Rule(string land) => _gate = GraphLand.Gate(_home, land);

    /// <summary>The plan of <paramref name="path"/>, read by the run's bound process where a test bound one, as the press reads it.</summary>
    private Task<LandingPlan> PlanAsync(SessionTrees trees, string path, LandingSubject subject) => trees.PlanAsync(path, subject, process: _gate?.Process);

    /// <summary>The press's landing of <paramref name="path"/>, by the run's bound <c>land</c> where a test bound one.</summary>
    private Task<TreeLanding> LandAsync(SessionTrees trees, string path, LandingSubject subject, string acceptedBy = AcceptedBy.Person) =>
        trees.LandAsync(path, subject, acceptedBy: acceptedBy, gate: _gate);

    /// <summary>
    /// A repository on `main` with one commit, its README: a copy of the run's plain seed (<see cref="GitSeed.PlainAsync"/>, PROC1h;
    /// PROCSPEED1, PROCSPEED1b), five git starts a test fewer. No test here reads the first commit's id or time.
    /// </summary>
    private Task<string> RepositoryAsync(string name) => GitSeed.PlainAsync(Path.Combine(_scratch, name), name);

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
        using var process = Process.Start(TestGit.Past(info))!;
        // 🔴 Both streams are read at once, before the wait: read one after the other, a git that writes more than a pipe's
        // worth of warnings to the second blocks on it while the first is still being read (FIX-LOG 2026-10-04).
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        return await stdout;
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot() => GitSeed.RepoRoot();
}
