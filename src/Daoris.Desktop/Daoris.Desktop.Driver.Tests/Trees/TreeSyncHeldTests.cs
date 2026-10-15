using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDSYNC3 (D87's 2026-10-15 amendment point 1): <i>Bring up to date</i> judges a session branch's commits as a landing does. WSR6's
/// plan said <i>replays 3 commit(s)</i> for a branch whose first was squash-merged, and <i>replays 1</i> for one whose only commit was:
/// now what the line holds is neither counted nor replayed, and a branch it holds entirely moves onto the line as holding nothing of
/// its own. Beside <see cref="TreeSyncTests"/>, in a class of its own so the git-only classes still run four at once (PROC1b).
/// </summary>
/// <remarks>
/// 🔴 <b>Nothing here reaches a network</b>: `origin` is a bare repository under the scratch folder, and the platform's merge is a
/// second clone of it, committing the same change under a new id as a squash of one commit does.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeSyncHeldTests : TreeSyncFixture
{
    private const string Changelog = "# Changes\n\n- first\n";

    /// <summary>
    /// The install's case: the session's first commit squash-merged on the platform under a new id, the line's changelog changed beside
    /// it since, and two more commits on the old base. The plan counts two, the press replays those two onto the line, and the branch
    /// holds no second copy of the merged work.
    /// </summary>
    [Fact]
    public async Task A_squash_merged_first_commit_is_not_counted_and_only_the_rest_is_replayed()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        await CommitAsync(root, "CHANGELOG.md", Changelog, "a changelog");
        await GitAsync(root, "push", "--quiet", "origin", "main");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "CHANGELOG.md", Changelog + "- the fix\n", "the fix");
        await PlatformAsync(origin, "the fix (#7)", ("CHANGELOG.md", Changelog + "- the fix\n"));
        await PlatformAsync(origin, "the line's own entry", ("CHANGELOG.md", Changelog + "- the fix\n- the line's own\n"));
        await CommitAsync(tree.Path, "more.txt", "more\n", "more of the work");
        await CommitAsync(tree.Path, "notes.txt", "notes\n", "the notes");

        var plan = await trees.SyncPlanAsync(Repositories(root), Nobody);

        var item = plan.Rebases.Single(each => each.Branch == tree.Branch);
        Assert.Equal((RebaseKind.Replay, 2, 1), (item.Kind, item.Commits, item.LeftOut));
        Assert.Contains("replays 2 commit(s) of its own onto `main`, leaving out 1 the line holds", SyncWords.Describe(item));

        var done = await trees.SyncAsync(Repositories(root), Nobody);

        var replayed = done.Rebases.Single(each => each.Item.Branch == tree.Branch);
        Assert.True(replayed.Replayed, replayed.Message);
        Assert.Equal($"replayed `{tree.Branch}` onto `main`: 2 commit(s) of its own, leaving out 1 the line holds.", replayed.Message);
        Assert.Equal(["the notes", "more of the work"], await SubjectsAsync(root, $"main..{tree.Branch}"));
        Assert.Equal(await RevAsync(root, "main"), await RevAsync(root, $"{tree.Branch}~2"));
        Assert.Equal(Changelog + "- the fix\n- the line's own\n", (await GitAsync(root, "show", $"{tree.Branch}:CHANGELOG.md")).ReplaceLineEndings("\n"));
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(tree.Path, "status", "--porcelain")).Trim());
    }

    /// <summary>
    /// The install's second branch: its only commit squash-merged. The plan moves it onto the line holding nothing of its own, and the
    /// press puts the branch and its tree on the line's tip.
    /// </summary>
    [Fact]
    public async Task A_branch_the_line_holds_entirely_moves_onto_the_line_holding_nothing_of_its_own()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "fix.txt", "the fix\n", "the fix");
        await PlatformAsync(origin, "the fix (#8)", ("fix.txt", "the fix\n"));
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#9)");

        var plan = await trees.SyncPlanAsync(Repositories(root), Nobody);

        var item = plan.Rebases.Single(each => each.Branch == tree.Branch);
        Assert.Equal((RebaseKind.Replay, 0, 1), (item.Kind, item.Commits, item.LeftOut));
        Assert.EndsWith("moves onto `main`: it holds nothing of its own beyond work that reached the line (1 commit(s) the line holds left out)",
            SyncWords.Describe(item));

        var done = await trees.SyncAsync(Repositories(root), Nobody);

        var moved = done.Rebases.Single(each => each.Item.Branch == tree.Branch);
        Assert.True(moved.Replayed, moved.Message);
        Assert.Equal($"moved `{tree.Branch}` onto `main`: it holds nothing of its own beyond work that reached the line (1 commit(s) the line holds left out).",
            moved.Message);
        Assert.Equal(await RevAsync(root, "main"), await RevAsync(root, tree.Branch));
        Assert.Equal(await RevAsync(root, "main"), await RevAsync(tree.Path, "HEAD"));
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
    }

    /// <summary>
    /// A squash of several of a session's commits, which no one commit's patch matches: a branch grown from the line has the walk by
    /// content asked too, since the line changed every file its first commit touched, and only the commit after the squash is counted.
    /// </summary>
    [Fact]
    public async Task A_squash_of_several_is_found_by_content_for_a_branch_grown_from_the_line()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CommitAsync(tree.Path, "shared.txt", "one\ntwo\n", "more of the work");
        await PlatformAsync(origin, "the work (#10)", ("work.txt", "the work\n"), ("shared.txt", "one\ntwo\n"));
        await PlatformCommitAsync(origin, "later.txt", "the line moves on\n", "the line moves on");
        await CommitAsync(tree.Path, "notes.txt", "notes\n", "the notes");

        var plan = await trees.SyncPlanAsync(Repositories(root), Nobody);

        var item = plan.Rebases.Single(each => each.Branch == tree.Branch);
        Assert.Equal((RebaseKind.Replay, 1, CutBy.Content), (item.Kind, item.Commits, item.CutBy));

        var done = await trees.SyncAsync(Repositories(root), Nobody);

        Assert.True(done.Rebases.Single(each => each.Item.Branch == tree.Branch).Replayed);
        Assert.Equal(["the notes"], await SubjectsAsync(root, $"main..{tree.Branch}"));
    }

    /// <summary>
    /// A commit the line holds between two it lacks (a cherry-pick of the middle one, its file changed on the line since): skipped, the
    /// runs either side replayed in the session's own tree, and nothing of the branch's own lost.
    /// </summary>
    [Fact]
    public async Task A_commit_the_line_holds_between_two_it_lacks_is_skipped_by_the_replay()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "a.txt", "a\n", "the first part");
        await CommitAsync(tree.Path, "b.txt", "b\n", "the second part");
        await CommitAsync(tree.Path, "c.txt", "c\n", "the third part");
        await PlatformAsync(origin, "the second part (picked)", ("b.txt", "b\n"));
        await PlatformAsync(origin, "the line goes on with it", ("b.txt", "b\nand the line's own\n"));

        var plan = await trees.SyncPlanAsync(Repositories(root), Nobody);

        Assert.Equal(2, plan.Rebases.Single(each => each.Branch == tree.Branch).Commits);

        var done = await trees.SyncAsync(Repositories(root), Nobody);

        var replayed = done.Rebases.Single(each => each.Item.Branch == tree.Branch);
        Assert.True(replayed.Replayed, replayed.Message);
        Assert.Equal(["the third part", "the first part"], await SubjectsAsync(root, $"main..{tree.Branch}"));
        Assert.Equal("b\nand the line's own\n", (await GitAsync(root, "show", $"{tree.Branch}:b.txt")).ReplaceLineEndings("\n"));
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(tree.Path, "status", "--porcelain")).Trim());
    }

    /// <summary>The platform's side: a clone of the bare origin commits <paramref name="files"/> as one commit on `main`, and pushes.</summary>
    private async Task PlatformAsync(string origin, string message, params (string File, string Content)[] files)
    {
        var platform = Path.Combine(Scratch, $"platform-{Guid.NewGuid():N}"[..20]);
        await GitAsync(Scratch, "clone", "--quiet", origin, platform);
        foreach (var (file, content) in files) await File.WriteAllTextAsync(Path.Combine(platform, file), content);
        await GitAsync(platform, "add", "-A");
        await GitAsync(platform, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
        await GitAsync(platform, "push", "--quiet", "origin", "main");
    }
}
