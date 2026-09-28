using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR1 (D87): how a session's work lands is a workspace's rule, with a repository's override — merged
/// into the line, or put on a branch named by a pattern for the person to push. Daoris never pushes.
/// Real git for the door, as the other tree tests use it.
/// </summary>
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

        Assert.Equal(TargetPrompt.Compose(target), TargetPrompt.Compose(target with { LandsOn = new LandingPlan(LandingForm.Merge, "main", LandingSource.Default) }));
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
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return stdout;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
