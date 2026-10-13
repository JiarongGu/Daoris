using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SQUASHTIDY1 (D51 rule 7, D88, D102's SQUASHTIDY1 note): a session branch whose commits no branch of the person's holds, but
/// whose work a squash merge or a cherry-pick put elsewhere by content, is discarded unforced and says how its work was found
/// held; the clean-up lists it as landed and removes it. Work held nowhere still refuses, and force stays its door. Real git, in
/// a scratch repository, and nothing reaches a network.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ContentHeldTests : LandedFixture
{
    private static (string, string?, string?)[] Engine(string root) => [("engine", "aurora", root)];

    /// <summary>The owner's first branch: squash-merged as a pull request, its tree now the line's, and ancestry sees nothing.</summary>
    [Fact]
    public async Task A_squash_merged_tree_whose_tree_is_the_lines_is_discarded_unforced_and_says_so()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CommitAsync(tree.Path, "shared.txt", "one\ntwo\n", "more of the work");
        await SquashAsync(root, tree.Branch);
        // The squash leaves no ancestry: D88 alone would refuse it.
        Assert.Equal(2, await SessionTrees.UnlandedAsync(root, tree.Branch));

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.Contains("its work is on `main` by content (a squash merge)", removal.Message);
        Assert.False(Directory.Exists(tree.Path));
        Assert.Equal("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
    }

    /// <summary>The line moved on after the squash: the squash commit is still on it, since the branch left it.</summary>
    [Fact]
    public async Task A_squash_the_line_moved_on_from_is_found_by_its_commits_tree()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        await CommitAsync(root, "work.txt", "the work, changed on the line since\n", "the line moves on");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.Contains("its work is on `main` by content (a squash merge)", removal.Message);
    }

    /// <summary>The owner's second branch: its one change cherry-picked onto a person's branch, its file the same there.</summary>
    [Fact]
    public async Task A_cherry_picked_tree_whose_files_read_the_same_on_a_persons_branch_is_discarded_unforced_naming_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CherryPickOntoAsync(root, tree.Branch, "feature/x");
        Assert.Equal(1, await SessionTrees.UnlandedAsync(root, tree.Branch));

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.Contains("its work is on `feature/x` by content", removal.Message);
        Assert.DoesNotContain("squash", removal.Message);
        Assert.False(Directory.Exists(tree.Path));
        Assert.Contains("feature/x", await GitAsync(root, "branch", "--list"));
    }

    /// <summary>One file it changed reads otherwise on every branch: nothing holds the whole of it, so the refusal stands.</summary>
    [Fact]
    public async Task A_tree_with_one_file_differing_still_refuses()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CommitAsync(tree.Path, "notes.txt", "the notes\n", "the notes");
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/x");
        await CommitAsync(root, "work.txt", "the work\n", "the work, taken");
        await CommitAsync(root, "notes.txt", "other notes\n", "the notes, taken otherwise");
        await GitAsync(root, "checkout", "--quiet", "main");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("has not taken", removal.Message);
        Assert.Contains("--force", removal.Message);
        Assert.True(Directory.Exists(tree.Path));
        Assert.Contains(tree.Branch, await GitAsync(root, "branch", "--list", "daoris/*"));
    }

    /// <summary>Commits that change no file leave nothing to find anywhere: not proven, as D102 has it, so the refusal stands.</summary>
    [Fact]
    public async Task A_tree_whose_commits_change_no_file_still_refuses()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await GitAsync(tree.Path, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--allow-empty", "-m", "nothing");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("has not taken", removal.Message);
    }

    /// <summary>A branch whose tree is gone goes through its own door unforced on the same proof, saying the same.</summary>
    [Fact]
    public async Task A_treeless_branch_held_by_content_goes_through_its_door_unforced()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await GitAsync(root, "worktree", "remove", tree.Path);
        await SquashAsync(root, tree.Branch);

        var removal = await trees.RemoveBranchAsync(root, "engine", tree.Branch);

        Assert.True(removal.Removed, removal.Message);
        Assert.Contains("its work is on `main` by content (a squash merge)", removal.Message);
        Assert.Equal("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
    }

    /// <summary>
    /// The Branches tab's clean-up reads the same proof: a squash-merged branch and a cherry-picked one are listed as landed,
    /// where their work is, and go at the press; one whose file differs is kept, unlanded, with its discard offered.
    /// </summary>
    [Fact]
    public async Task The_clean_up_lists_work_held_by_content_as_landed_and_removes_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var squashed = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(squashed.Path, "squashed.txt", "squashed\n", "squashed work");
        var picked = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(picked.Path, "picked.txt", "picked\n", "picked work");
        var differs = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(differs.Path, "differs.txt", "mine\n", "work that differs");
        await GitAsync(root, "worktree", "remove", squashed.Path);
        await SquashAsync(root, squashed.Branch);
        await CherryPickOntoAsync(root, picked.Branch, "feature/x");
        await CommitOnAsync(root, "feature/x", "differs.txt", "otherwise\n", "the same file, otherwise");

        var plan = await trees.SweepPlanAsync(Engine(root), new HashSet<string>());

        var squashedRow = plan.Single(item => item.Branch == squashed.Branch);
        Assert.Equal((SweepKind.Landed, "main", true), (squashedRow.Kind, squashedRow.Where, squashedRow.Removable));
        Assert.Equal(new ContentHold("main", Squash: true), squashedRow.HeldBy);
        var pickedRow = plan.Single(item => item.Branch == picked.Branch);
        Assert.Equal((SweepKind.Landed, "feature/x", true), (pickedRow.Kind, pickedRow.Where, pickedRow.Removable));
        Assert.Equal(new ContentHold("feature/x", Squash: false), pickedRow.HeldBy);
        var differsRow = plan.Single(item => item.Branch == differs.Branch);
        Assert.Equal((SweepKind.Unlanded, 1), (differsRow.Kind, differsRow.Commits));
        Assert.Null(differsRow.HeldBy);
        Assert.NotNull(SessionTrees.RemovalOffered(differsRow));
        Assert.Null(SessionTrees.RemovalOffered(squashedRow));

        var swept = await trees.SweepAsync(Engine(root), new HashSet<string>());

        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.DoesNotContain(squashed.Branch, branches);
        Assert.DoesNotContain(picked.Branch, branches);
        Assert.Contains(differs.Branch, branches);
        Assert.False(Directory.Exists(picked.Path), "its tree went with it");
        Assert.True(Directory.Exists(differs.Path));
        Assert.Equal(2, swept.Count(result => result.Removed));
    }

    /// <summary>
    /// A person's branch, moved on from the line by a change of its own, that takes <paramref name="branch"/>'s commits by
    /// cherry-pick: new commits, so no ancestry reaches the session branch's.
    /// </summary>
    private static async Task CherryPickOntoAsync(string root, string branch, string onto)
    {
        await GitAsync(root, "checkout", "--quiet", "-b", onto);
        await CommitAsync(root, "feature.txt", "the person's own\n", "the person's own");
        await GitAsync(root, "cherry-pick", $"main..{branch}");
        await GitAsync(root, "checkout", "--quiet", "main");
    }
}
