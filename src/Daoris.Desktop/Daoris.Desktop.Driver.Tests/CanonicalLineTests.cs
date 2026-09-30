using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR2: a repository's line is the repository's, and a person can set it. What the person set for the
/// repository, then for its workspace, then the checkout's guess — read from one place by every door
/// that grows or lands work. Real git for the doors, as the other tree tests use it.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class CanonicalLineTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;

    public CanonicalLineTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "canonical-line", Guid.NewGuid().ToString("N")[..8]);
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

    /// <summary>The branch-name rule. The CLI's <c>driverconfig.test.ts</c> holds these cases, answer for answer.</summary>
    [Theory]
    [InlineData("main", true)]
    [InlineData("develop", true)]
    [InlineData("feature/team-x", true)]
    [InlineData("release/2026.09", true)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    [InlineData("-dash", false)]
    [InlineData("a..b", false)]
    [InlineData("ends/", false)]
    [InlineData("x.lock", false)]
    [InlineData("a:b", false)]
    [InlineData("@", false)]
    [InlineData("a@{1}", false)]
    [InlineData(".hidden", false)]
    [InlineData("feat/.x", false)]
    [InlineData("a//b", false)]
    [InlineData("a b", false)]
    [InlineData("a\u0085b", false)]
    public void A_line_is_a_name_git_would_take(string name, bool valid) =>
        Assert.Equal(valid, BranchName.IsValid(name));

    [Fact]
    public void The_repositorys_own_setting_wins_then_its_workspaces_then_the_guess()
    {
        var config = DriverConfig.Empty.WithWorkspaceLine("aurora", "develop").WithLine("engine", "release");

        Assert.Equal(new Line("release", LineSource.Repository), CanonicalLine.Choose(config, "engine", "aurora", "main"));
        Assert.Equal(new Line("develop", LineSource.Workspace), CanonicalLine.Choose(config, "game", "aurora", "main"));
        Assert.Equal(new Line("main", LineSource.Checkout), CanonicalLine.Choose(config, "game", "forge", "main"));
        Assert.Equal(new Line(null, LineSource.None), CanonicalLine.Choose(config, "game", "forge", null));
    }

    /// <summary>A repository in no workspace is in the `default` one (D48 §2), and its line is found under that name.</summary>
    [Fact]
    public void A_repository_in_no_workspace_takes_the_default_workspaces_line()
    {
        var config = DriverConfig.Empty.WithWorkspaceLine("default", "develop");

        Assert.Equal(new Line("develop", LineSource.Workspace), CanonicalLine.Choose(config, "game", null, "main"));
    }

    /// <summary>The screen's answer: every repository here with its line, one with no checkout answered from what is set alone.</summary>
    [Fact]
    public async Task The_screen_is_answered_for_every_repository_with_what_said_so()
    {
        var root = await RepositoryAsync("engine");
        var config = DriverConfig.Empty.WithLine("remote-only", "develop");

        var lines = await CanonicalLine.OfAsync(config, [("engine", "aurora", root), ("remote-only", "aurora", null), ("elsewhere", null, null)]);

        Assert.Equal(
            [
                new RepositoryLine("engine", "aurora", "main", LineSource.Checkout),
                new RepositoryLine("remote-only", "aurora", "develop", LineSource.Repository),
                new RepositoryLine("elsewhere", "default", null, LineSource.None),
            ],
            lines);
    }

    [Fact]
    public void The_lines_are_kept_in_driver_json_and_one_git_would_refuse_is_not()
    {
        var config = DriverConfig.Parse("""
            { "lines": { "engine": "release", "odd": "has space" }, "workspaceLines": { "aurora": "develop" } }
            """);

        Assert.Equal("release", config.Lines["engine"]);
        Assert.False(config.Lines.ContainsKey("odd"));
        Assert.Equal("develop", DriverConfig.Parse(config.ToJson()).WorkspaceLines["aurora"]);

        var cleared = config.WithLine("engine", null).WithWorkspaceLine("aurora", null);
        Assert.DoesNotContain("lines", cleared.ToJson(), StringComparison.Ordinal);
        Assert.Throws<DriverException>(() => config.WithLine("engine", "a..b"));
    }

    [Fact]
    public async Task A_session_tree_grows_from_the_line_set_for_its_repository()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "develop");
        await GitAsync(root, "checkout", "--quiet", "develop");
        await CommitAsync(root, "develop.txt", "the develop line", "on develop");
        await GitAsync(root, "checkout", "--quiet", "main");
        DriverConfig.Empty.WithLine("engine", "develop").Save(Path.Combine(_home, "driver.json"));

        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");

        Assert.Contains("`develop`, the line set for this repository", opened.BasedOn);
        Assert.True(File.Exists(Path.Combine(opened.Path, "develop.txt")), "the tree grew from main, not develop");
    }

    [Fact]
    public async Task A_workspaces_line_is_the_default_for_its_repositories()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "develop");
        DriverConfig.Empty.WithWorkspaceLine("aurora", "develop").Save(Path.Combine(_home, "driver.json"));

        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");

        Assert.Contains("the line set for its workspace", opened.BasedOn);
    }

    /// <summary>A line only the remote has is grown from there, rather than refused or silently skipped.</summary>
    [Fact]
    public async Task A_line_this_checkout_has_only_on_origin_is_grown_from_origin()
    {
        var upstream = await RepositoryAsync("upstream");
        await GitAsync(upstream, "branch", "develop");
        await GitAsync(upstream, "checkout", "--quiet", "develop");
        await CommitAsync(upstream, "develop.txt", "the develop line", "on develop");
        await GitAsync(upstream, "checkout", "--quiet", "main");
        var root = Path.Combine(_scratch, "engine");
        await GitAsync(_scratch, "clone", "--quiet", upstream, root);
        DriverConfig.Empty.WithLine("engine", "develop").Save(Path.Combine(_home, "driver.json"));

        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");

        Assert.True(File.Exists(Path.Combine(opened.Path, "develop.txt")), "the tree did not grow from origin/develop");
    }

    /// <summary>
    /// 🔴 A line only on origin is compared as origin has it. `git log develop..` fails where there is
    /// no local `develop`, and its empty output read as "nothing unmerged": the tree went as if its
    /// work had landed, and the merge said there was nothing to merge.
    /// </summary>
    [Fact]
    public async Task Work_on_a_line_only_origin_has_is_not_taken_for_landed()
    {
        var root = await CloneWithDevelopOnlyOnOriginAsync();
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var removal = await trees.RemoveAsync(opened.Path);
        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("has not taken", removal.Message);
        Assert.True(Directory.Exists(opened.Path));

        var merge = await trees.MergeAsync(opened.Path);
        Assert.False(merge.Merged);
        Assert.DoesNotContain("nothing to merge", merge.Message);
        Assert.Contains("put it on `develop`", merge.Message);
    }

    /// <summary>A line git has nowhere cannot say whether work landed on it, so the tree stays.</summary>
    [Fact]
    public async Task A_tree_is_not_removed_against_a_line_git_cannot_find()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");
        DriverConfig.Empty.WithLine("engine", "gone").Save(Path.Combine(_home, "driver.json"));

        var removal = await trees.RemoveAsync(opened.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("`gone`", removal.Message);
        Assert.True(Directory.Exists(opened.Path));
    }

    private async Task<string> CloneWithDevelopOnlyOnOriginAsync()
    {
        var upstream = await RepositoryAsync("upstream");
        await GitAsync(upstream, "branch", "develop");
        var root = Path.Combine(_scratch, "engine");
        await GitAsync(_scratch, "clone", "--quiet", upstream, root);
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        DriverConfig.Empty.WithLine("engine", "develop").Save(Path.Combine(_home, "driver.json"));
        return root;
    }

    [Fact]
    public async Task A_line_set_to_a_branch_nowhere_is_refused_with_how_to_fix_it()
    {
        var root = await RepositoryAsync("engine");
        DriverConfig.Empty.WithLine("engine", "develop").Save(Path.Combine(_home, "driver.json"));

        var refused = await Assert.ThrowsAsync<DriverException>(() => new SessionTrees(_home).OpenAsync(root, "engine", "aurora"));

        Assert.Contains("daoris driver line engine", refused.Message);
        Assert.Empty((await GitAsync(root, "branch", "--list", "daoris/*")).Trim());
    }

    /// <summary>The merge lands on the set line, and a checkout on the guessed one is refused, naming the set one.</summary>
    [Fact]
    public async Task A_merge_lands_on_the_line_that_is_set_and_refuses_a_checkout_on_another()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "develop");
        DriverConfig.Empty.WithLine("engine", "develop").Save(Path.Combine(_home, "driver.json"));
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var refused = await trees.MergeAsync(opened.Path);
        Assert.False(refused.Merged);
        Assert.Contains("put it on `develop`", refused.Message);

        await GitAsync(root, "checkout", "--quiet", "develop");
        var merged = await trees.MergeAsync(opened.Path);
        Assert.True(merged.Merged, merged.Message);
        Assert.Contains("the work", await GitAsync(root, "log", "develop", "--oneline"));
        Assert.DoesNotContain("the work", await GitAsync(root, "log", "main", "--oneline"));
    }

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
