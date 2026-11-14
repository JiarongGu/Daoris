using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Why `daoris tree clean --yes` removed none of the landed trees it listed, one test for each cause, over real git: a judged delete waits behind a
/// session's start, and Daoris's own copy of a machine-local file is never the session's work.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeCleanCausesTests : LandedFixture
{
    [Fact]
    public async Task A_delete_the_press_judged_waits_for_a_start_holding_the_repository_and_then_goes()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        await GitAsync(root, "branch", "daoris/s-1a2b3c4d");
        var starting = TreeLock.TryStarting(Home, root, StartIn.Fresh);
        Assert.NotNull(starting);
        var release = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(600));
            starting!.Dispose();
        });

        var done = await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());
        await release;

        var result = Assert.Single(done.Sessions);
        Assert.True(result.Removed, result.Message);
        Assert.Empty((await GitAsync(root, "branch", "--list", "daoris/s-1a2b3c4d")).Trim());
    }

    /// <summary>A hold that outlasts the wait still keeps the branch, said with the holder's words, as the delete did before it waited.</summary>
    [Fact]
    public async Task A_delete_whose_wait_ends_with_the_repository_still_held_keeps_the_branch_and_says_what_held_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        await GitAsync(root, "branch", "daoris/s-1a2b3c4d");
        using var starting = TreeLock.TryStarting(Home, root, StartIn.Fresh);
        Assert.NotNull(starting);
        JournaledDeletes.WaitSeam.Value = TimeSpan.FromMilliseconds(300);

        var done = await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());

        var result = Assert.Single(done.Sessions);
        Assert.False(result.Removed);
        Assert.Contains("a session was starting in `engine`", result.Message, StringComparison.Ordinal);
        Assert.NotEmpty((await GitAsync(root, "branch", "--list", "daoris/s-1a2b3c4d")).Trim());
    }

    /// <summary>
    /// The tree holds `local/` ignored whole, in it a copy Daoris made of a machine-local file that the checkout's own file has moved on from
    /// since, and the half-made copy of a cut-off open: neither is the session's work, so the landed tree goes.
    /// </summary>
    [Fact]
    public async Task A_stale_copy_of_a_machine_local_file_Daoris_made_does_not_keep_a_landed_tree()
    {
        var (root, tree, branch) = await LandedTreeWithLocalCopyAsync();

        var done = await new SessionTrees(Home).CleanAsync([("engine", "aurora", root)], new HashSet<string>());

        var result = Assert.Single(done.Sessions, each => each.Item.Branch == branch);
        Assert.True(result.Removed, result.Message);
        Assert.False(Directory.Exists(tree));
    }

    /// <summary>The same tree where the session changed the copy: its bytes are no longer the ones Daoris wrote, so they are work and keep it.</summary>
    [Fact]
    public async Task A_copy_the_session_changed_after_Daoris_made_it_still_keeps_the_tree()
    {
        var (root, tree, branch) = await LandedTreeWithLocalCopyAsync();
        var copy = Path.Combine(tree, "local", "machine.props");
        File.SetAttributes(copy, FileAttributes.Normal);
        await File.WriteAllTextAsync(copy, "the session's own edit\n");

        var done = await new SessionTrees(Home).CleanAsync([("engine", "aurora", root)], new HashSet<string>());

        var result = Assert.Single(done.Sessions, each => each.Item.Branch == branch);
        Assert.False(result.Removed);
        Assert.True(Directory.Exists(tree));
    }

    [Fact]
    public void A_read_git_said_nothing_of_says_so_where_it_once_said_nothing_at_all()
    {
        Assert.Equal("git could not tell: no commit stands at the branch's name, and git said nothing", TreeSweeps.TipUnread(""));
        Assert.Equal("git could not tell: no commit stands at the branch's name, and git said nothing", TreeSweeps.TipUnread("  \n"));
        Assert.Equal("git could not tell: fatal: bad object\n".Split('\n')[0], TreeSweeps.TipUnread("fatal: bad object\nmore"));
    }

    /// <summary>A landed tree (its commit merged onto the line) whose ignored `local/` holds Daoris's copy, recorded, and a half-made one.</summary>
    private async Task<(string Root, string Tree, string Branch)> LandedTreeWithLocalCopyAsync()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, ".gitignore", "local/\n", "ignore the machine-local folder");
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the work\n", "work");
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "merge the work", opened.Branch);

        Directory.CreateDirectory(Path.Combine(opened.Path, "local"));
        var copy = Path.Combine(opened.Path, "local", "machine.props");
        await File.WriteAllTextAsync(copy, "as Daoris copied it\n");
        File.SetAttributes(copy, FileAttributes.ReadOnly);
        await File.WriteAllTextAsync(Path.Combine(opened.Path, "local", DependencyStore.MakingPrefix + "1a2b3c4d"), "half\n");
        // The checkout's own file moved on after the copy: the copy is stale.
        Directory.CreateDirectory(Path.Combine(root, "local"));
        await File.WriteAllTextAsync(Path.Combine(root, "local", "machine.props"), "the checkout's file now\n");
        TreeFilesRecord.Change(Home, opened.Path, state => state with
        {
            Copies = new Dictionary<string, string> { ["local/machine.props"] = TreeCopies.HashOf(copy)! },
        });
        return (root, opened.Path, opened.Branch);
    }
}
