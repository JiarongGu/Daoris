using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR3 (D88): a session branch goes once git proves its work is on a branch of the person's — by
/// the person's press, or by a tidy rule they set, and never otherwise. Real git throughout.
/// </summary>
public sealed class SweepTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;

    public SweepTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "sweep", Guid.NewGuid().ToString("N")[..8]);
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

    [Fact]
    public async Task Work_is_landed_when_a_branch_of_the_persons_holds_every_commit()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);

        var merged = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "merge", "--no-ff", "--no-edit", merged.Branch);
        Assert.Equal(0, await SessionTrees.UnlandedAsync(root, merged.Branch));

        var featured = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "branch", "feature/x", featured.Branch);
        Assert.Equal(0, await SessionTrees.UnlandedAsync(root, featured.Branch));

        var alone = await TreeWithWorkAsync(trees, root);
        Assert.Equal(1, await SessionTrees.UnlandedAsync(root, alone.Branch));
    }

    /// <summary>A chain's next step grows from the step before's branch, which is Daoris's too — so that work is not landed either.</summary>
    [Fact]
    public async Task Work_only_another_session_branch_holds_is_not_landed()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var parent = await TreeWithWorkAsync(trees, root);
        var child = await trees.OpenAsync(root, "engine", "aurora", from: parent.Branch);

        Assert.Equal(1, await SessionTrees.UnlandedAsync(root, child.Branch));
    }

    [Fact]
    public async Task Work_a_remote_holds_is_landed()
    {
        var upstream = await RepositoryAsync("upstream");
        var root = Path.Combine(_scratch, "engine");
        await GitAsync(_scratch, "clone", "--quiet", upstream, root);
        await IdentityAsync(root);
        var trees = new SessionTrees(_home);
        var pushed = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "push", "--quiet", "origin", $"{pushed.Branch}:refs/heads/review/x");
        await GitAsync(root, "fetch", "--quiet", "origin");

        Assert.Equal(0, await SessionTrees.UnlandedAsync(root, pushed.Branch));
    }

    /// <summary>The branch form's work is landed, so its tree goes without --force now — where the line alone kept it forever.</summary>
    [Fact]
    public async Task A_tree_whose_work_is_on_a_feature_branch_is_removed_without_force()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "branch", "feature/x", tree.Branch);

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.Empty((await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
        Assert.Contains("the work", await GitAsync(root, "log", "feature/x", "--oneline"));
    }

    [Fact]
    public async Task The_plan_lists_every_session_branch_with_what_it_holds()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var empty = await trees.OpenAsync(root, "engine", "aurora");
        var landed = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "branch", "feature/x", landed.Branch);
        var unlanded = await TreeWithWorkAsync(trees, root);
        var dirty = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(dirty.Path, "loose.txt"), "not committed\n");
        var inUse = await trees.OpenAsync(root, "engine", "aurora");

        var plan = await trees.SweepPlanAsync([("engine", "aurora", root)], new HashSet<string> { inUse.Path });

        Assert.Equal(SweepKind.Empty, KindOf(plan, empty.Branch));
        Assert.Equal(SweepKind.Landed, KindOf(plan, landed.Branch));
        Assert.Equal(SweepKind.Unlanded, KindOf(plan, unlanded.Branch));
        Assert.Equal(1, plan.Single(item => item.Branch == unlanded.Branch).Commits);
        Assert.Equal(SweepKind.Dirty, KindOf(plan, dirty.Branch));
        Assert.Equal(SweepKind.InUse, KindOf(plan, inUse.Branch));
        Assert.All(plan, item => Assert.Equal("engine", item.Repository));
    }

    [Fact]
    public async Task The_clean_up_removes_what_the_proof_clears_and_keeps_the_rest_named()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var empty = await trees.OpenAsync(root, "engine", "aurora");
        var landed = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "merge", "--no-ff", "--no-edit", landed.Branch);
        var unlanded = await TreeWithWorkAsync(trees, root);
        // A branch whose tree was removed by hand still counts.
        await GitAsync(root, "branch", "daoris/s-treeless");

        var swept = await trees.SweepAsync([("engine", "aurora", root)], new HashSet<string>());

        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.DoesNotContain(empty.Branch, branches);
        Assert.DoesNotContain(landed.Branch, branches);
        Assert.DoesNotContain("daoris/s-treeless", branches);
        Assert.Contains(unlanded.Branch, branches);
        Assert.False(Directory.Exists(empty.Path));
        Assert.False(Directory.Exists(landed.Path));
        Assert.True(Directory.Exists(unlanded.Path));
        Assert.Equal(3, swept.Count(result => result.Removed));
        Assert.Contains(swept, result => !result.Removed && result.Item.Branch == unlanded.Branch);
    }

    /// <summary>🔴 Checked again right before each goes: work committed after the plan keeps its branch.</summary>
    [Fact]
    public async Task Work_committed_after_the_plan_keeps_its_branch()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        var plan = await trees.SweepPlanAsync([("engine", "aurora", root)], new HashSet<string>());
        Assert.Equal(SweepKind.Empty, KindOf(plan, tree.Branch));

        await CommitAsync(tree.Path, "late.txt", "arrived after the plan", "late work");
        var swept = await trees.SweepAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.False(swept.Single(result => result.Item.Branch == tree.Branch).Removed);
        Assert.Contains(tree.Branch, await GitAsync(root, "branch", "--list", "daoris/*"));
    }

    /// <summary>
    /// 🔴 A tree whose files' paths pass Windows' 260 characters is removed, as it was opened: the first
    /// real workspace's discarded tree failed with "Filename too long", after git had let go of it.
    /// </summary>
    [Fact]
    public async Task A_tree_with_paths_past_260_characters_is_removed()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        var deep = Path.Combine([tree.Path, .. Enumerable.Repeat("a-folder-name-long-enough-to-matter", 8)]);
        Directory.CreateDirectory(deep);
        await File.WriteAllTextAsync(Path.Combine(deep, "work-in-a-deep-place.txt"), "deep\n");
        Assert.True(Path.Combine(deep, "work-in-a-deep-place.txt").Length > 260);

        var removal = await trees.RemoveAsync(tree.Path, force: true);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(tree.Path));
        Assert.Empty((await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
    }

    /// <summary>
    /// 🔴 A committed file past 260 characters is not uncommitted work: without long paths git cannot
    /// read it and reports it changed, and the first real workspace's clean-up kept two trees for it.
    /// </summary>
    [Fact]
    public async Task A_committed_file_past_260_characters_is_not_taken_for_uncommitted_work()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        var deep = Path.Combine([tree.Path, .. Enumerable.Repeat("a-folder-name-long-enough-to-matter", 8)]);
        Directory.CreateDirectory(deep);
        await File.WriteAllTextAsync(Path.Combine(deep, "committed-in-a-deep-place.txt"), "deep\n");
        await GitAsync(tree.Path, "-c", "core.longpaths=true", "add", "-A");
        await GitAsync(tree.Path, "-c", "core.longpaths=true", "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "deep");
        await GitAsync(root, "branch", "keep", tree.Branch);

        var plan = await trees.SweepPlanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Equal(SweepKind.Landed, KindOf(plan, tree.Branch));
    }

    /// <summary>The tidy rule: once a press lands the work, its tree and branch go — and the landed branch stays.</summary>
    [Fact]
    public async Task A_tidy_rule_removes_the_tree_and_branch_once_the_work_lands()
    {
        var root = await RepositoryAsync("engine");
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}", Tidy: true))
            .Save(Path.Combine(_home, "driver.json"));
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);

        var landed = await trees.LandAsync(tree.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));

        Assert.True(landed.Landed, landed.Message);
        Assert.False(Directory.Exists(tree.Path));
        Assert.Empty((await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
        Assert.Contains("the work", await GitAsync(root, "log", "feature/0fda18", "--oneline"));
        Assert.Contains("removed", landed.Message);
    }

    [Fact]
    public void A_tidy_rule_is_kept_in_the_file_and_written_only_when_on()
    {
        var tidy = DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Merge, Tidy: true));
        Assert.True(DriverConfig.Parse(tidy.ToJson()).Landings["engine"].Tidy);
        Assert.DoesNotContain("tidy", DriverConfig.Empty.WithLanding("engine", LandingRule.Merge).ToJson(), StringComparison.Ordinal);
    }

    private static string KindOf(IReadOnlyList<SweepItem> plan, string branch) => plan.Single(item => item.Branch == branch).Kind;

    private async Task<TreeOpened> TreeWithWorkAsync(SessionTrees trees, string root)
    {
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, $"{Guid.NewGuid():N}.txt", "the session's work", "the work");
        return tree;
    }

    private async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await IdentityAsync(root);
        await CommitAsync(root, "README.md", $"# {name}", "first");
        return root;
    }

    private static async Task IdentityAsync(string root)
    {
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
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
