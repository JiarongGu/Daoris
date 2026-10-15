using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDSYNC2 (D87's 2026-10-15 amendment point 7): a session going on in an old tree starts from the line. Before its turn the line
/// is fetched once; where it holds some of the tree's commits, <c>UpdateTreeAsync</c> brings the tree up to date, a failed fetch is
/// said and not refused, and a conflict leaves the tree as it was and is told. A line that merely moved changes nothing. The doors
/// that ask it are <see cref="StartInSyncTickTests"/>'s.
/// </summary>
/// <remarks>
/// 🔴 <b>Nothing here reaches a network</b>: `origin` is a bare repository under the scratch folder, and the platform's merge is a
/// second clone of it, committing the same change under a new id as a squash of one commit does.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeStartSyncTests : TreeSyncFixture
{
    private const string Changelog = "# Changes\n\n- first\n";

    /// <summary>
    /// The install's order: the tree's first commit reached the line by a squash after it landed, and the session goes on. The tree is
    /// brought up to date onto origin's latest, its own commit replayed, its branch moved, and the session told so.
    /// </summary>
    [Fact]
    public async Task A_tree_whose_first_commit_the_line_holds_is_brought_up_to_date_and_told()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "fix.txt", "the fix\n", "the fix");
        await CommitAsync(tree.Path, "more.txt", "more\n", "more of the work");
        await PlatformAsync(origin, "the fix (#7)", ("fix.txt", "the fix\n"));
        var line = await RevAsync(root, "main");

        var started = await trees.StartInSyncAsync(tree.Path);

        var moved = Assert.IsType<TreeMoved>(started.Moved);
        Assert.Equal((tree.Branch, 1), (moved.Branch, moved.Commits));
        Assert.Equal(await RevAsync(root, "origin/main"), await RevAsync(tree.Path, "HEAD~1"));
        Assert.Equal(["more of the work"], await SubjectsAsync(root, $"origin/main..{tree.Branch}"));
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal(
            "Before this turn Daoris brought this tree up to date with `main`: the line already holds 1 of its commit(s), which were left "
            + "out, and its other 1 commit(s) were replayed onto `origin/main`, so its branch holds only what the line lacks. Read the log "
            + "before you go on: the commits you made have new ids.",
            started.Told);
        // The person's own line is not moved: only origin's ref is the fetch's.
        Assert.Equal(line, await RevAsync(root, "main"));
    }

    /// <summary>Not merely because the line moved (point 7): a tree the line holds none of is left as it was, and told nothing.</summary>
    [Fact]
    public async Task A_tree_the_line_holds_none_of_is_left_as_it_was_though_the_line_moved()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        var head = await RevAsync(tree.Path, "HEAD");
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#9)");

        var started = await trees.StartInSyncAsync(tree.Path);

        Assert.Equal(TreeStartSync.Nothing, started);
        Assert.Equal(head, await RevAsync(tree.Path, "HEAD"));
    }

    /// <summary>
    /// A conflict leaves the tree as it was, and the session is told the line holds some of its work, git's lines naming the file, and
    /// to replay its own commits onto the line itself (point 5's words, for a start).
    /// </summary>
    [Fact]
    public async Task A_conflict_leaves_the_tree_as_it_was_and_is_told_in_the_instruction()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        await CommitAsync(root, "CHANGELOG.md", Changelog, "a changelog");
        await GitAsync(root, "push", "--quiet", "origin", "main");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "CHANGELOG.md", Changelog + "- the fix\n", "the fix");
        await CommitAsync(tree.Path, "CHANGELOG.md", Changelog + "- the fix\n- mine\n", "my entry");
        await PlatformAsync(origin, "the fix (#7)", ("CHANGELOG.md", Changelog + "- the fix\n"));
        await PlatformAsync(origin, "their entry", ("CHANGELOG.md", Changelog + "- the fix\n- theirs\n"));
        var head = await RevAsync(tree.Path, "HEAD");

        var started = await trees.StartInSyncAsync(tree.Path);

        Assert.Null(started.Moved);
        Assert.StartsWith(
            "The line `main` already holds 1 of this tree's commit(s), and Daoris could not bring the tree up to date before this turn: ",
            started.Told);
        Assert.Contains("CONFLICT", started.Told);
        Assert.Contains("CHANGELOG.md", started.Told);
        Assert.Contains("Before anything else, replay your own commits onto `origin/main`, keeping only what the line lacks", started.Told);
        Assert.Equal(head, await RevAsync(tree.Path, "HEAD"));
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(tree.Path, "status", "--porcelain")).Trim());
    }

    /// <summary>
    /// A fetch that fails is no refusal at a start (point 7, where the person's own press still refuses it): the tree is judged against
    /// the line as this checkout last had it, brought up to date, and the session told the fetch failed and when origin was last heard.
    /// </summary>
    [Fact]
    public async Task A_failed_fetch_is_said_and_the_tree_is_judged_against_the_last_one()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "fix.txt", "the fix\n", "the fix");
        await SquashAsync(root, tree.Branch);
        await CommitAsync(tree.Path, "more.txt", "more\n", "more of the work");
        await GitAsync(root, "remote", "set-url", "origin", Path.Combine(Scratch, "gone.git"));

        var started = await trees.StartInSyncAsync(tree.Path);

        Assert.Equal(1, Assert.IsType<TreeMoved>(started.Moved).Commits);
        Assert.Equal(await RevAsync(root, "main"), await RevAsync(tree.Path, "HEAD~1"));
        Assert.StartsWith("Before this turn Daoris brought this tree up to date with `main`", started.Told);
        Assert.Contains("`origin` was not fetched (", started.Told);
        Assert.Contains("so this was judged against what it said when it was last fetched here (", started.Told);
    }

    /// <summary>A folder that is no tree of this home's is not read at all: nothing done, nothing told.</summary>
    [Fact]
    public async Task A_folder_that_is_no_tree_of_this_homes_is_not_read()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");

        Assert.Equal(TreeStartSync.Nothing, await new SessionTrees(Home).StartInSyncAsync(root));
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
