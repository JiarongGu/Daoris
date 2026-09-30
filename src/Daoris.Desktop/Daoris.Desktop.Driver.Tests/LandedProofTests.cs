using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR5a, the proof by content, case by case: every file a landed branch changed since it left the line —
/// a deletion and both paths of a rename included — must read on the line as the branch left it.
/// </summary>
public sealed class LandedProofTests : LandedFixture
{
    [Fact]
    public async Task A_branch_with_one_file_still_different_on_the_line_is_kept_and_the_file_named()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var b = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"), extra: ("b.txt", "b\n"));
        // The line took b.txt as it was, and shared.txt otherwise.
        await CommitAsync(root, "b.txt", "b\n", "part of it");
        await CommitAsync(root, "shared.txt", "one\nsomething else\n", "not quite");

        var item = Landed(await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>()), b.Branch!);

        Assert.Equal(LandedKind.Differs, item.Kind);
        Assert.False(item.Removable);
        Assert.Equal(["shared.txt"], item.Files);
        var done = await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());
        Assert.False(done.Landed.Single().Removed);
        Assert.Contains(b.Branch!, await GitAsync(root, "branch", "--list"));
    }

    /// <summary>A deletion is a change too: the line must not hold the file the branch deleted.</summary>
    [Fact]
    public async Task A_deletion_is_on_the_line_only_where_the_line_deleted_it_too()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await GitAsync(tree.Path, "rm", "--quiet", "shared.txt");
        await GitAsync(tree.Path, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "drop it");
        var landed = await trees.LandAsync(tree.Path, new LandingSubject("s3", "q3", "Drop"));
        Assert.True(landed.Landed, landed.Message);

        var kept = Landed(await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>()), landed.Branch!);
        Assert.Equal(LandedKind.Differs, kept.Kind);
        Assert.Equal(["shared.txt"], kept.Files);

        await GitAsync(root, "rm", "--quiet", "shared.txt");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "Drop (#9)");
        Assert.Equal(LandedKind.OnLine, Landed(await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>()), landed.Branch!).Kind);
    }

    /// <summary>A rename is both of its paths: the new one must read as the branch left it, and the old one must be gone.</summary>
    [Fact]
    public async Task A_rename_is_on_the_line_only_where_the_old_path_is_gone_and_the_new_one_matches()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await GitAsync(tree.Path, "mv", "shared.txt", "renamed.txt");
        await GitAsync(tree.Path, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "rename it");
        var landed = await trees.LandAsync(tree.Path, new LandingSubject("s4", "q4", "Rename"));
        Assert.True(landed.Landed, landed.Message);

        // The line holds the new name and still the old one.
        await CommitAsync(root, "renamed.txt", "one\n", "a copy, not a rename");
        var kept = Landed(await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>()), landed.Branch!);
        Assert.Equal(LandedKind.Differs, kept.Kind);
        Assert.Equal(["shared.txt"], kept.Files);

        await GitAsync(root, "rm", "--quiet", "shared.txt");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "Rename (#10)");
        Assert.Equal(LandedKind.OnLine, Landed(await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>()), landed.Branch!).Kind);
    }

    [Fact]
    public async Task A_branch_merged_into_the_line_goes()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var b = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s2", "q2", "Second"));
        await GitAsync(root, "merge", "--no-ff", "--no-edit", b.Branch!);

        var item = Landed(await trees.CleanPlanAsync([("engine", "aurora", root)], new HashSet<string>()), b.Branch!);

        Assert.Equal(LandedKind.Merged, item.Kind);
        Assert.True(item.Removable);
    }
}
