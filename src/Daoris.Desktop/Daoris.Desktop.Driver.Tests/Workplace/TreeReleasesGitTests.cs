using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git: a run's tree ended behind the clean-up's proof (<see cref="TreeReleases"/>). A tree whose work the line holds goes with its
/// branch, one holding commits nothing else holds or uncommitted work stays and says why, and the sweep ends the trees of a run whose
/// record ended or is gone while it leaves a live run's.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeReleasesGitTests : LandedFixture
{
    private SessionBranches Grown => new(Home);

    private GrownBranch Entry(string branch) => Grown.Entries().Last(each => each.Branch == branch);

    /// <summary>A session's tree owned by run <paramref name="run"/>, with one commit.</summary>
    private async Task<(string Tree, string Branch)> OwnedAsync(SessionTrees trees, string root, string run, string file)
    {
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, file, $"{file}\n", $"work on {file}");
        Grown.Own("engine", opened.Branch, run);
        return (opened.Path, opened.Branch);
    }

    private async Task MergeAsync(string root, string branch) =>
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", $"merge {branch}", branch);

    private void Record(string run, bool ended)
    {
        var record = new NodeRecord(RunUnits.IdOf(run));
        new NodeRecordFiles(Home, () => true).Write(ended ? record with { Ended = new RunEnd("q1", DateTimeOffset.UtcNow) } : record);
    }

    [Fact]
    public async Task A_tree_whose_work_the_line_holds_goes_with_its_branch_and_leaves_no_reason()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (tree, branch) = await OwnedAsync(trees, root, "q1", "a.txt");
        await MergeAsync(root, branch);

        var released = await new TreeReleases(Home).ReleaseAsync(root, "engine", branch);

        Assert.True(released.Gone, released.Said);
        Assert.False(Directory.Exists(tree));
        Assert.Empty((await GitAsync(root, "branch", "--list", branch)).Trim());
        Assert.Null(Grown.Entries().LastOrDefault(each => each.Branch == branch && each.GoneAt is null)?.Kept);
    }

    [Fact]
    public async Task A_tree_holding_commits_nothing_else_holds_stays_and_the_record_says_why()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (tree, branch) = await OwnedAsync(trees, root, "q1", "a.txt");

        var released = await new TreeReleases(Home).ReleaseAsync(root, "engine", branch);

        Assert.False(released.Gone);
        Assert.True(Directory.Exists(tree));
        Assert.Contains("has not taken", released.Said, StringComparison.Ordinal);
        Assert.Equal(released.Said, Entry(branch).Kept);
    }

    [Fact]
    public async Task A_tree_with_uncommitted_work_stays_even_where_its_commits_are_on_the_line_and_says_so()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (tree, branch) = await OwnedAsync(trees, root, "q1", "a.txt");
        await MergeAsync(root, branch);
        await File.WriteAllTextAsync(Path.Combine(tree, "loose.txt"), "not committed\n");

        var released = await new TreeReleases(Home).ReleaseAsync(root, "engine", branch);

        Assert.False(released.Gone);
        Assert.True(File.Exists(Path.Combine(tree, "loose.txt")));
        Assert.False(string.IsNullOrWhiteSpace(Entry(branch).Kept));
    }

    [Fact]
    public async Task The_sweep_ends_the_trees_of_a_run_whose_record_ended_or_is_gone_and_leaves_a_live_runs()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (endedTree, ended) = await OwnedAsync(trees, root, "q1", "a.txt");
        var (goneTree, gone) = await OwnedAsync(trees, root, "q2", "b.txt");
        var (liveTree, live) = await OwnedAsync(trees, root, "q3", "c.txt");
        await MergeAsync(root, ended);
        await MergeAsync(root, gone);
        await MergeAsync(root, live);
        Record("q1", ended: true);
        Record("q3", ended: false);

        var swept = await new TreeReleases(Home).SweepAsync(root, "engine");

        Assert.Equal(new[] { ended, gone }.Order(), swept.Where(each => each.Gone).Select(each => each.Branch).Order());
        Assert.False(Directory.Exists(endedTree));
        Assert.False(Directory.Exists(goneTree));
        Assert.True(Directory.Exists(liveTree));
    }

    [Fact]
    public async Task The_sweep_keeps_an_ended_runs_unlanded_tree_and_says_why_and_a_tree_no_run_owns_is_not_its_to_end()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (tree, branch) = await OwnedAsync(trees, root, "q1", "a.txt");
        var unowned = await trees.OpenAsync(root, "engine", "aurora");
        Record("q1", ended: true);

        var swept = await new TreeReleases(Home).SweepAsync(root, "engine");

        var kept = Assert.Single(swept);
        Assert.Equal((branch, false), (kept.Branch, kept.Gone));
        Assert.True(Directory.Exists(tree));
        Assert.True(Directory.Exists(unowned.Path));
        Assert.Contains("has not taken", Entry(branch).Kept, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_tree_list_names_each_trees_owner_and_why_it_stays()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var (tree, branch) = await OwnedAsync(trees, root, "q1", "a.txt");
        var unowned = await trees.OpenAsync(root, "engine", "aurora");
        await new TreeReleases(Home).ReleaseAsync(root, "engine", branch);

        var listed = await trees.ListAsync();

        var owned = listed.Single(each => each.Branch == branch);
        Assert.Equal("q1", owned.Run);
        Assert.Contains("has not taken", owned.Reason, StringComparison.Ordinal);
        var loose = listed.Single(each => each.Branch == unowned.Branch);
        Assert.Null(loose.Run);
        Assert.Null(loose.Reason);
        Assert.True(Directory.Exists(tree));
    }
}
