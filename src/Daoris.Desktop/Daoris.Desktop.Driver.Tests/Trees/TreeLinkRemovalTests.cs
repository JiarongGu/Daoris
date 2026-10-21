using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 TREELINK1 (D51's note): a tree's removal never follows a link out of it. TREEDEPS1 probed git 2.53.0.windows.3 on the owner's
/// machine: <c>git worktree remove --force</c> followed a junction in a tree to a folder outside it and deleted the file there. A
/// session that links outside its tree (<c>npm link</c>, a workspace's junction, a link of its own) would lose the target when its
/// tree went. Each removal path is driven here over a tree holding a junction to a folder outside the trees home: the folder's file
/// still stands after it, and the junction is gone with its tree. Elsewhere the link is a symbolic link, which git never followed,
/// so there every test is green with or without the guard.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeLinkRemovalTests : BranchTidyingFixture
{
    private const string Persons = "the person's own, outside every tree\n";

    private static readonly Func<CancellationToken, Task<IReadOnlySet<string>>> NoneRunning =
        _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    /// <summary>The person's removal, forced: an untracked junction at the tree's top goes as a link.</summary>
    [Fact]
    public async Task A_forced_removal_leaves_what_a_link_in_the_tree_points_at()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        var file = await LinkOutAsync(tree.Path, "linked");

        var removal = await trees.RemoveAsync(tree.Path, force: true);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(tree.Path));
        Assert.Equal(Persons, await File.ReadAllTextAsync(file));
    }

    /// <summary>
    /// The unforced removal, behind the clean-up's proof: a junction in an ignored build folder the checkout also holds, as npm's
    /// workspaces make under <c>node_modules</c>, is no keep (SQUASHTIDY1c), and goes as a link.
    /// </summary>
    [Fact]
    public async Task An_unforced_removal_leaves_what_a_link_in_a_build_folder_points_at()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await LandedTreeAsync(trees, root);
        var file = await LinkOutAsync(tree.Path, Path.Combine("build", "linked"));

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(tree.Path));
        Assert.Equal(Persons, await File.ReadAllTextAsync(file));
    }

    /// <summary>
    /// A landing's tidy (LAND3): the pressed session's tree goes through the removal, and another session's tree the landed work
    /// holds through the tidy's own; each holds a junction in its build folder, and neither target loses its file.
    /// </summary>
    [Fact]
    public async Task A_landings_tidy_leaves_what_links_in_its_trees_point_at()
    {
        var root = await RepositoryAsync("engine");
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true))
            .Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var trees = new SessionTrees(_home);
        var held = await trees.OpenAsync(root, "engine", "default");
        var heldFile = await LinkOutAsync(held.Path, Path.Combine("build", "linked"));
        var pressed = await TreeWithWorkAsync(trees, root);
        var pressedFile = await LinkOutAsync(pressed.Path, Path.Combine("build", "linked"));

        var landed = await trees.LandAsync(pressed.Path, new LandingSubject("s2", "0fda18", "Fix the gap"), inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(new TidiedBranch(held.Branch, Removed: true, Tree: true), Assert.Single(landed.Tidied!));
        Assert.False(Directory.Exists(held.Path));
        Assert.False(Directory.Exists(pressed.Path), landed.Message);
        Assert.Equal(Persons, await File.ReadAllTextAsync(heldFile));
        Assert.Equal(Persons, await File.ReadAllTextAsync(pressedFile));
    }

    /// <summary>The person's press of the clean-up (D88), which deletes the tree with git.</summary>
    [Fact]
    public async Task The_clean_ups_press_leaves_what_a_link_in_the_tree_points_at()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await LandedTreeAsync(trees, root);
        var file = await LinkOutAsync(tree.Path, Path.Combine("build", "linked"));

        var pressed = await trees.SweepAsync([("engine", "default", root)], new HashSet<string>());

        Assert.True(pressed.Single(result => result.Item.Branch == tree.Branch).Removed, string.Join(" | ", pressed.Select(each => each.Message)));
        Assert.False(Directory.Exists(tree.Path));
        Assert.Equal(Persons, await File.ReadAllTextAsync(file));
    }

    /// <summary>
    /// The look's own tidy (AUTOTIDY1): the tree's folder is moved aside whole, never deleted, and fourteen days later the purge
    /// takes the link under its build folder as a link (TIDIEDLINK1). Neither step reaches the target.
    /// </summary>
    [Fact]
    public async Task The_looks_set_aside_and_its_purge_leave_what_a_link_in_the_tree_points_at()
    {
        var root = await RepositoryAsync("engine");
        var tree = await LandedTreeAsync(new SessionTrees(_home), root);
        var file = await LinkOutAsync(tree.Path, Path.Combine("build", "linked"));
        _ledger.Register("engine", root);

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith(Removed(tree), Assert.Single(said), StringComparison.Ordinal);
        Assert.False(Directory.Exists(tree.Path));
        Assert.Equal(Persons, await File.ReadAllTextAsync(file));
        await PurgedAsync(file);
    }

    /// <summary>
    /// An open that did not finish, undone (OPENCUT1): git lets go of its tree with <c>--force</c> twice, the hardest removal of all,
    /// and what is left is deleted here. A junction in it, as TREEDEPS1's copy of the dependencies makes, goes as a link.
    /// </summary>
    [Fact]
    public async Task An_undone_opening_leaves_what_a_link_in_its_tree_points_at()
    {
        var root = await RepositoryAsync("engine");
        var tree = await new SessionTrees(_home).OpenAsync(root, "engine", "default");
        var file = await LinkOutAsync(tree.Path, Path.Combine("node_modules", "@fixture", "web"));

        await TreeOpenings.UndoAsync(root, tree.Path, tree.Branch, ours: true);

        Assert.False(Directory.Exists(tree.Path));
        Assert.DoesNotContain(tree.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.Equal(Persons, await File.ReadAllTextAsync(file));
    }

    /// <summary>
    /// A tree whose opening a kill cut off (OPENCUT1, TreeStrays): the look moves its folder aside whole and lets git forget it, and the
    /// purge takes the link under its build folder as a link.
    /// </summary>
    [Fact]
    public async Task A_cut_off_openings_tidy_leaves_what_a_link_in_its_tree_points_at()
    {
        var root = await RepositoryAsync("engine");
        var cut = DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "s-0e0ded00");
        await GitAsync(root, "worktree", "add", "--quiet", "--lock", "--reason", TreeOpenings.LockReason(await EndedProcessAsync()), "-b", "daoris/s-0e0ded00", cut, "main");
        var file = await LinkOutAsync(cut, Path.Combine("build", "linked"));
        _ledger.Register("engine", root);

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith("tidy  engine: removed `daoris/s-0e0ded00`, whose tree's opening was cut off", Assert.Single(said), StringComparison.Ordinal);
        Assert.False(Directory.Exists(cut));
        Assert.Equal(Persons, await File.ReadAllTextAsync(file));
        await PurgedAsync(file);
    }

    /// <summary>
    /// A tree whose own folder is a link is somewhere else: the removal would reach through it, so it is refused, forced or not, and
    /// what the link points at stands as it was.
    /// </summary>
    [Fact]
    public async Task A_tree_whose_own_folder_is_a_link_is_never_removed_through_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        var elsewhere = Path.Combine(_scratch, "elsewhere-tree");
        Directory.Move(tree.Path, elsewhere);
        await JunctionAsync(tree.Path, elsewhere);

        var removal = await trees.RemoveAsync(tree.Path, force: true);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("its tree's folder is itself a link", removal.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(elsewhere, "README.md")));
        Directory.Delete(tree.Path);
    }

    /// <summary>
    /// A folder outside the trees home holding a file, and a junction to it at <paramref name="relative"/> in <paramref name="tree"/>:
    /// the file's path.
    /// </summary>
    private async Task<string> LinkOutAsync(string tree, string relative)
    {
        var outside = Path.Combine(_scratch, "outside", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outside);
        var file = Path.Combine(outside, "notes.md");
        await File.WriteAllTextAsync(file, Persons);
        await JunctionAsync(Path.Combine(tree, relative), outside);
        return file;
    }

    /// <summary>The one folder the look moved aside, purged once its wait is over: gone, and <paramref name="file"/> as it was.</summary>
    private async Task PurgedAsync(string file)
    {
        var aside = Assert.Single(Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine")));
        var moved = TreeSetAside.MovedAt(Path.GetFileName(aside))!.Value;

        var purged = new SessionTrees(_home).PurgeSetAside(moved.AddDays(15), KeepKinds.SetAside.Days);

        Assert.True(Assert.Single(purged).Purged, purged[0].Kept);
        Assert.False(Directory.Exists(aside));
        Assert.Equal(Persons, await File.ReadAllTextAsync(file));
    }
}
