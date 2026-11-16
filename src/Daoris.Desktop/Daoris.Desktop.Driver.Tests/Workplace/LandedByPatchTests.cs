using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDEDBYPATCH1: every reading of "landed" is by patch, as the tree's work reads it, so a landing that replayed a tree's
/// commit (same patch, new id) leaves the session list saying landed, never "not landed" for ever.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandedByPatchTests : LandedFixture
{
    [Fact]
    public async Task A_tree_whose_commit_the_line_holds_as_a_replay_offers_no_landing()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(Scratch, "engine"), "engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the session's work\n", "the session's work");

        var before = await trees.WorkAsync(tree.Path);
        Assert.NotNull(LandOffer.Of(tree.Path, before!));

        await GitAsync(root, "checkout", "--quiet", "-b", "quest/work");
        await GitAsync(root, "cherry-pick", "-x", $"main..{tree.Branch}");
        await GitAsync(root, "checkout", "--quiet", "main");
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "Merge quest/work", "quest/work");
        await GitAsync(root, "branch", "-D", "quest/work");

        var work = await trees.WorkAsync(tree.Path);
        Assert.Null(LandOffer.Of(tree.Path, work!));
        Assert.False(work!.Holds);
    }

    [Fact]
    public async Task A_tree_whose_commit_the_line_holds_as_a_replay_it_has_changed_since_offers_no_landing()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(Scratch, "engine"), "engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the session's work\n", "the session's work");

        // The line moved before the landing, so the replay is not the tree's own tree; then it goes on
        await CommitAsync(root, "other.txt", "other\n", "the line moves");
        await GitAsync(root, "checkout", "--quiet", "-b", "quest/work");
        await GitAsync(root, "cherry-pick", "-x", $"main..{tree.Branch}");
        await GitAsync(root, "checkout", "--quiet", "main");
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "Merge quest/work", "quest/work");
        await GitAsync(root, "branch", "-D", "quest/work");
        // The line goes on: later work touches the file the replay brought.
        await CommitAsync(root, "work.txt", "the line rewrote the work\n", "the line goes on");

        var work = await trees.WorkAsync(tree.Path);
        Assert.Null(LandOffer.Of(tree.Path, work!));
    }
}
