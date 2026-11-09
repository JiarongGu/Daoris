using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's discard of a tree a hand-off reserves, over real git (§23): read under the tree's landing hold, the row first keeps the
/// tree, said, forced or not, and the tree and its branch stand.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeHandOffsGitTests : LandedFixture
{
    [Fact]
    public async Task A_discard_while_a_hand_off_is_open_is_refused_and_the_tree_stands()
    {
        var root = await RepositoryAsync("engine");
        var opened = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        var offs = new TreeHandOffs(Home, () => StoreSettled.Yes);
        var row = offs.Write(new TreeHandOff(
            opened.Path, "engine", opened.Branch, "0123abc", "r-12", "n3", 1, "stop-1", "session", TreeHandOff.AskKey("r-12", "n3", 1, 1),
            TreeHandOffPhases.Reserved, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30)))!;

        var kept = await new SessionTrees(Home).RemoveAsync(opened.Path, force: true);

        Assert.False(kept.Removed);
        Assert.Equal(TreeRemovals.Reserved(opened.Path, row), kept.Message);
        Assert.Contains("stop that step to discard it", kept.Message, StringComparison.Ordinal);
        Assert.True(Directory.Exists(opened.Path));

        offs.Close(row);
        var removed = await new SessionTrees(Home).RemoveAsync(opened.Path, force: true);

        Assert.True(removed.Removed, removed.Message);
    }
}
