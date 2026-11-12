using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git, a tree's files record and its install logs go wherever the tree goes: the look's tidy moving it aside and the removal
/// of it by hand; a tree that stays keeps both.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeFilesForgottenGitTests : BranchTidyingFixture
{
    private string LogOf(string tree) => DaorisHome.Entry(_home, HomeEntry.TreeFiles, $"{TreeFilesRecord.IdOf(tree)}.0a1b2c3d.log");

    private void Record(string tree)
    {
        TreeFilesRecord.Mark(_home, tree);
        File.WriteAllText(LogOf(tree), "an install's output\n");
    }

    [Fact]
    public async Task A_tree_the_look_moves_aside_takes_its_record_and_logs_with_it()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        _ledger.Register("engine", root);
        Record(landed.Path);

        await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.False(Directory.Exists(landed.Path));
        Assert.False(File.Exists(TreeFilesRecord.PathOf(_home, landed.Path)));
        Assert.False(File.Exists(LogOf(landed.Path)));
    }

    [Fact]
    public async Task A_tree_removed_by_hand_takes_its_record_and_logs_with_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var landed = await LandedTreeAsync(trees, root);
        Record(landed.Path);

        var removal = await trees.RemoveAsync(landed.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(File.Exists(TreeFilesRecord.PathOf(_home, landed.Path)));
        Assert.False(File.Exists(LogOf(landed.Path)));
    }

    [Fact]
    public async Task A_tree_that_stays_keeps_its_record_and_logs()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");
        Record(opened.Path);

        var removal = await trees.RemoveAsync(opened.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.True(File.Exists(TreeFilesRecord.PathOf(_home, opened.Path)));
        Assert.True(File.Exists(LogOf(opened.Path)));
    }
}
