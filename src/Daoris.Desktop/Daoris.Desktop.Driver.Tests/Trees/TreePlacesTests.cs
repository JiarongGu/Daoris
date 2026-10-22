using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREEPLACE1: a tree this home opened is one under either place of <see cref="HomeEntry.Trees"/>. D172 point 6 never moves a
/// tree, so after the home's layout moved the trees opened before stay at the old place while new ones open at the new, and
/// the one predicate every check asks (<c>TreeContext.Holds</c>) takes both. Paths only, nothing on the disk; the git half,
/// landing, removing and tidying such a tree, is <see cref="TreePlacesGitTests"/>.
/// </summary>
public sealed class TreePlacesTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;

    public TreePlacesTests()
    {
        _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "tree-places", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    private IReadOnlyList<string> Places(string name) => DaorisHome.Places(_home, HomeEntry.Trees, "aurora", "engine", name);

    [Fact]
    public void A_tree_at_either_place_of_the_trees_is_one_this_home_opened()
    {
        var trees = new SessionTrees(_home);
        var (fresh, old) = (Places("s-new00000")[0], Places("s-old00000")[1]);

        Assert.NotEqual(Path.GetDirectoryName(fresh), Path.GetDirectoryName(old));
        Assert.True(trees.Holds(fresh));
        Assert.True(trees.Holds(old));
        // The layout is read under the place that holds it: the old tree's workspace and repository, not a walk up from the new.
        Assert.Equal(("aurora", "engine"), trees.Owner(fresh));
        Assert.Equal(("aurora", "engine"), trees.Owner(old));
    }

    [Fact]
    public void A_folder_under_neither_place_is_still_refused()
    {
        var trees = new SessionTrees(_home);
        var old = Places("s-old00000")[1];
        // A lookalike beside the old place, and a layout-shaped folder outside the home: neither is a tree this home opened.
        var lookalike = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(old)))! + "-old";

        Assert.False(trees.Holds(Path.Combine(lookalike, "aurora", "engine", "s-old00000")));
        Assert.False(trees.Holds(Path.Combine(_scratch, "elsewhere", "trees", "aurora", "engine", "s-old00000")));
        Assert.False(trees.Holds(_home));
        // The places themselves are no tree.
        foreach (var root in DaorisHome.Places(_home, HomeEntry.Trees)) Assert.False(trees.Holds(root));
    }
}
