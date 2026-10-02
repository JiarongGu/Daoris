using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Abandon's proof on real git (PAUSE1d, D132 point 7, design §3.3): a tree and its branch are discarded only when no commit
/// after the session's base is on any ref but this machine's local <c>daoris/*</c> branches. D88's proof turned around: the
/// line, a branch of the person's, a tag and a pushed <c>origin/daoris/*</c> each count as elsewhere, and partly elsewhere is
/// kept whole. The discard behind it takes the tree with its uncommitted work and its branch, or the branch alone.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class SessionTreeOnlyHereTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;

    public SessionTreeOnlyHereTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "only-here", Guid.NewGuid().ToString("N")[..8]);
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

    /// <summary>Commits only on its own branch, and uncommitted changes in its tree: only here, each counted, the files named.</summary>
    [Fact]
    public async Task Work_only_on_its_own_branch_is_only_here_with_its_counts_and_files()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root, commits: 2);
        File.WriteAllText(Path.Combine(tree.Path, "dirty.txt"), "uncommitted\n");
        File.AppendAllText(Path.Combine(tree.Path, "README.md"), "changed\n");

        var judged = await trees.OnlyHereAsync(tree.Path, tree.Branch, tree.Base, root);

        Assert.Equal(OnlyHereKind.OnlyHere, judged.Kind);
        Assert.True(judged.TreeHere);
        Assert.Equal((2, 2), (judged.Commits, judged.Uncommitted));
        Assert.Equal(["README.md", "dirty.txt"], judged.Files.Order(StringComparer.Ordinal));
        Assert.Equal((await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim(), judged.Tip);
    }

    /// <summary>The line, a branch of the person's and a tag each hold work elsewhere, named, so the tree is kept.</summary>
    [Theory]
    [InlineData("merge")]
    [InlineData("branch")]
    [InlineData("tag")]
    public async Task Work_on_the_line_a_branch_of_yours_or_a_tag_is_elsewhere(string where)
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        switch (where)
        {
            case "merge": await GitAsync(root, "merge", "--no-ff", "--no-edit", tree.Branch); break;
            case "branch": await GitAsync(root, "branch", "feature/x", tree.Branch); break;
            default: await GitAsync(root, "tag", "kept", tree.Branch); break;
        }

        var judged = await trees.OnlyHereAsync(tree.Path, tree.Branch, tree.Base, root);

        Assert.Equal(OnlyHereKind.Elsewhere, judged.Kind);
        Assert.Equal(where switch { "merge" => "main", "branch" => "feature/x", _ => "kept" }, judged.Where);
    }

    /// <summary>🔴 A pushed session branch counts as elsewhere, where D88's proof leaves it out: pushed means somebody may have it.</summary>
    [Fact]
    public async Task A_pushed_session_branch_is_elsewhere_though_the_clean_up_calls_it_unlanded()
    {
        var upstream = await RepositoryAsync("upstream");
        var root = Path.Combine(_scratch, "engine");
        await GitAsync(_scratch, "clone", "--quiet", upstream, root);
        await IdentityAsync(root);
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "push", "--quiet", "origin", tree.Branch);

        var judged = await trees.OnlyHereAsync(tree.Path, tree.Branch, tree.Base, root);

        Assert.Equal(OnlyHereKind.Elsewhere, judged.Kind);
        Assert.Equal($"origin/{tree.Branch}", judged.Where);
        Assert.Equal(1, await SessionTrees.UnlandedAsync(root, tree.Branch));
    }

    /// <summary>Another session branch holding the same commits (a chain's next step) is still Daoris's own: only here.</summary>
    [Fact]
    public async Task Work_another_session_branch_holds_is_still_only_here()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var parent = await TreeWithWorkAsync(trees, root);
        await trees.OpenAsync(root, "engine", "aurora", from: parent.Branch);

        Assert.Equal(OnlyHereKind.OnlyHere, (await trees.OnlyHereAsync(parent.Path, parent.Branch, parent.Base, root)).Kind);
    }

    /// <summary>Two commits on a branch of the person's and one only here: partly elsewhere is kept whole.</summary>
    [Fact]
    public async Task Partly_elsewhere_is_kept_whole()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root, commits: 3);
        await GitAsync(root, "branch", "feature/half", $"{tree.Branch}~1");

        var judged = await trees.OnlyHereAsync(tree.Path, tree.Branch, tree.Base, root);

        Assert.Equal((OnlyHereKind.Elsewhere, 3, "feature/half"), (judged.Kind, judged.Commits, judged.Where));
    }

    /// <summary>With no base on the record, the merge-base with the line stands in, and the line's own later commits are not the tree's.</summary>
    [Fact]
    public async Task With_no_base_the_merge_base_with_the_line_stands_in()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root, commits: 2);
        await CommitAsync(root, "later.md", "the line moved on", "later");

        var judged = await trees.OnlyHereAsync(tree.Path, tree.Branch, baseCommit: null, root);

        Assert.Equal((OnlyHereKind.OnlyHere, 2), (judged.Kind, judged.Commits));
    }

    /// <summary>A base git does not know is a count git cannot give: kept.</summary>
    [Fact]
    public async Task A_base_git_does_not_know_keeps_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);

        var judged = await trees.OnlyHereAsync(tree.Path, tree.Branch, new string('0', 40), root);

        Assert.Equal(OnlyHereKind.Unknown, judged.Kind);
        Assert.False(judged.Discards);
    }

    /// <summary>
    /// The discard behind the proof takes the tree, its uncommitted work and its branch; the commits stay in the repository
    /// until git collects them, so the tip the record keeps brings the branch back.
    /// </summary>
    [Fact]
    public async Task The_discard_takes_the_tree_its_work_and_its_branch_and_the_tip_brings_it_back()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        File.WriteAllText(Path.Combine(tree.Path, "dirty.txt"), "uncommitted\n");
        var judged = await trees.OnlyHereAsync(tree.Path, tree.Branch, tree.Base, root);

        var removed = await trees.DiscardAsync(tree.Path, tree.Branch, judged);

        Assert.True(removed.Removed, removed.Message);
        Assert.False(Directory.Exists(tree.Path));
        Assert.Equal("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
        await GitAsync(root, "branch", tree.Branch, judged.Tip!);
        Assert.Equal(judged.Tip, (await GitAsync(root, "rev-parse", tree.Branch)).Trim());
    }

    /// <summary>A branch whose tree went without git being told is judged from the checkout, and deleted alone.</summary>
    [Fact]
    public async Task A_branch_whose_tree_is_gone_is_judged_from_the_checkout_and_deleted_alone()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        ForceDelete(tree.Path);

        var judged = await trees.OnlyHereAsync(tree.Path, tree.Branch, tree.Base, root);
        var removed = await trees.DiscardAsync(tree.Path, tree.Branch, judged);

        Assert.Equal((OnlyHereKind.OnlyHere, false, 1), (judged.Kind, judged.TreeHere, judged.Commits));
        Assert.True(removed.Removed, removed.Message);
        Assert.Equal("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
        Assert.Equal(OnlyHereKind.Gone, (await trees.OnlyHereAsync(tree.Path, tree.Branch, tree.Base, root)).Kind);
    }

    /// <summary>A session branch checked out in the person's checkout is somebody's work: kept, and the discard refuses it.</summary>
    [Fact]
    public async Task A_branch_checked_out_elsewhere_is_kept_and_never_discarded()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        ForceDelete(tree.Path);
        await GitAsync(root, "worktree", "prune");
        await GitAsync(root, "checkout", "--quiet", tree.Branch);

        var judged = await trees.OnlyHereAsync(tree.Path, tree.Branch, tree.Base, root);
        var removed = await trees.DiscardAsync(tree.Path, tree.Branch, judged);

        Assert.Equal(OnlyHereKind.CheckedOut, judged.Kind);
        Assert.False(removed.Removed);
        Assert.Contains(tree.Branch, await GitAsync(root, "branch", "--list", tree.Branch));
    }

    /// <summary>🔴 A path outside this home's trees, or a branch that is not Daoris's, is never judged for a discard.</summary>
    [Fact]
    public async Task A_checkout_or_a_branch_of_the_persons_is_never_judged_for_a_discard()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await GitAsync(root, "branch", "feature/mine");

        Assert.Equal(OnlyHereKind.Unknown, (await trees.OnlyHereAsync(root, "main", null, root)).Kind);
        var tree = await TreeWithWorkAsync(trees, root);
        Assert.Equal(OnlyHereKind.Unknown, (await trees.OnlyHereAsync(tree.Path, "feature/mine", null, root)).Kind);
    }

    /// <summary>A session's tree, its branch, and its base as the record keeps it (SURF6): the commit it stood at when it opened.</summary>
    private sealed record Opened(string Path, string Branch, string Base);

    private async Task<Opened> TreeWithWorkAsync(SessionTrees trees, string root, int commits = 1)
    {
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        var opened = new Opened(tree.Path, tree.Branch, (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
        for (var at = 0; at < commits; at++) await CommitAsync(tree.Path, $"{Guid.NewGuid():N}.txt", "the session's work", $"the work {at}");
        return opened;
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

    private static void ForceDelete(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(folder, recursive: true);
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
        await GitAsync(tree, "commit", "--quiet", "-m", message);
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
