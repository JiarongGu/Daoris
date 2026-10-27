using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWBASE2-i (D164's REVIEWBASE2 note): one session's tree brought up to date at the person's press. The line is fetched once,
/// as the person, then the tree is replayed onto it by WSR6's replay: a tree on a branch has its branch moved last, from the commit
/// judged; a detached tree is rebased in place and HEAD moves only after the content proof. Each refusal is an answer, and leaves the
/// tree as it was. Real git in scratch repositories, <c>origin</c> a bare one beside each, the line moved there by a second clone.
/// </summary>
/// <remarks>
/// The owner, 2026-10-13: "we need to make sure the changes is based of the latest "main" branch". WSR6's look replays only ended
/// sessions' branches, never an open conversation's tree, a dirty one or a detached one.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeUpdateTests : LandedFixture
{
    private static Task<bool> Idle(CancellationToken _) => Task.FromResult(false);

    /// <summary>A file git wrote into the tree, as its text: git converts line endings as it checks out (autocrlf on Windows).</summary>
    private static async Task<string> CheckedOut(string path) => (await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n");

    /// <summary>A repository with origin, a session's tree grown from its line, and the commit it began at.</summary>
    private async Task<(string Root, string Origin, SessionTrees Trees, TreeOpened Tree, string Began)> TreeAsync()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        return (root, origin, trees, tree, await RevAsync(tree.Path, "HEAD"));
    }

    /// <summary>
    /// The owner's case: an idle conversation's clean tree on its session branch, the line moved on at origin since anything here
    /// fetched it. The press fetches it, replays the tree's one commit onto it, moves the branch and leaves the tree on it; the review
    /// then says the work is not behind the line. The person's own line is not moved: only origin's ref is the fetch's.
    /// </summary>
    [Fact]
    public async Task An_idle_clean_tree_on_its_session_branch_is_replayed_onto_the_line_fetched_now()
    {
        var (root, origin, trees, tree, began) = await TreeAsync();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        await PlatformCommitAsync(origin, "line.txt", "theirs\n", "the line moves on");
        var localLine = await RevAsync(root, "main");
        // Review fetches nothing: before the press, nothing here knows the line moved.
        Assert.Equal(0, (await trees.ReviewRangeAsync(tree.Path, began)).Behind!.Commits);

        var done = await trees.UpdateTreeAsync(tree.Path, Idle);

        Assert.True(done.Done, done.Message);
        Assert.Contains($"replayed `{tree.Branch}` onto `main`", done.Message);
        var latest = await RevAsync(root, "origin/main");
        Assert.NotEqual(localLine, latest);
        var moved = Assert.IsType<TreeMoved>(done.Moved);
        Assert.Equal(("main", tree.Branch, 1), (moved.Line, moved.Branch, moved.Commits));
        Assert.Equal(await RevAsync(tree.Path, "HEAD"), moved.To);
        Assert.Equal(latest, await RevAsync(tree.Path, "HEAD~1"));
        Assert.Equal(moved.To, await RevAsync(root, tree.Branch));
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(tree.Path, "status", "--porcelain")).Trim());
        Assert.Equal("mine\n", await CheckedOut(Path.Combine(tree.Path, "own.txt")));
        Assert.Equal(localLine, await RevAsync(root, "main"));
        Assert.Equal(new LineBehind("main", 0, 1), (await trees.ReviewRangeAsync(tree.Path, began)).Behind);
        // Where it grows from now is the line, as WSR6's replay records it.
        Assert.Equal(latest, trees.Grown.Of("engine", tree.Branch)!.From);
    }

    /// <summary>
    /// A detached tree with a commit of its own (point 4): rebased in place from the newest commit it shares with the line, HEAD still
    /// detached and moved after the proof. No ref moves: the session branch it left stays where it was.
    /// </summary>
    [Fact]
    public async Task A_detached_tree_with_commits_of_its_own_is_replayed_in_place()
    {
        var (root, origin, trees, tree, began) = await TreeAsync();
        await GitAsync(tree.Path, "switch", "--quiet", "--detach");
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work, detached");
        var head = await RevAsync(tree.Path, "HEAD");
        await PlatformCommitAsync(origin, "line.txt", "theirs\n", "the line moves on");

        var done = await trees.UpdateTreeAsync(tree.Path, Idle);

        Assert.True(done.Done, done.Message);
        Assert.Equal("replayed the tree onto `main`: 1 commit(s) of its own.", done.Message);
        var moved = Assert.IsType<TreeMoved>(done.Moved);
        Assert.Equal((head, (string?)null, 1), (moved.From, moved.Branch, moved.Commits));
        Assert.Equal("HEAD", (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal(await RevAsync(root, "origin/main"), await RevAsync(tree.Path, "HEAD~1"));
        Assert.Equal("mine\n", await CheckedOut(Path.Combine(tree.Path, "own.txt")));
        Assert.Equal(began, await RevAsync(root, tree.Branch));
        Assert.Equal(new LineBehind("main", 0, 1), (await trees.ReviewRangeAsync(tree.Path, began)).Behind);
    }

    /// <summary>A detached tree holding nothing beyond the line is moved to the line's latest, and says it held nothing of its own.</summary>
    [Fact]
    public async Task A_detached_tree_with_nothing_of_its_own_is_moved_to_the_line()
    {
        var (root, origin, trees, tree, _) = await TreeAsync();
        await GitAsync(tree.Path, "switch", "--quiet", "--detach");
        await PlatformCommitAsync(origin, "line.txt", "theirs\n", "the line moves on");

        var done = await trees.UpdateTreeAsync(tree.Path, Idle);

        Assert.True(done.Done, done.Message);
        Assert.Equal("moved the tree to `main`: it held nothing of its own beyond the line.", done.Message);
        Assert.Equal(0, done.Moved!.Commits);
        Assert.Equal(await RevAsync(root, "origin/main"), await RevAsync(tree.Path, "HEAD"));
        Assert.Equal("theirs\n", await CheckedOut(Path.Combine(tree.Path, "line.txt")));
    }

    /// <summary>
    /// A tree something runs in is refused before anything is fetched (point 3); and the question is asked again once the
    /// repository's trees are held, so a turn that began after the first answer is still seen and nothing moves.
    /// </summary>
    [Fact]
    public async Task A_tree_something_runs_in_is_refused_before_and_after_the_hold()
    {
        var (root, origin, trees, tree, _) = await TreeAsync();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        await PlatformCommitAsync(origin, "line.txt", "theirs\n", "the line moves on");
        var tip = await RevAsync(tree.Path, "HEAD");
        var known = await RevAsync(root, "origin/main");

        var busy = await trees.UpdateTreeAsync(tree.Path, _ => Task.FromResult(true));
        Assert.False(busy.Done);
        Assert.Equal(TreeUpdateWords.Refused(TreeRefusal.Running, new TreeFacts()), busy.Message);
        Assert.Null(busy.Moved);
        Assert.Equal(known, await RevAsync(root, "origin/main"));

        var asked = 0;
        var began = await trees.UpdateTreeAsync(tree.Path, _ => Task.FromResult(asked++ > 0));
        Assert.False(began.Done);
        Assert.Equal(2, asked);
        Assert.Contains("runs in its tree", began.Message);
        Assert.Equal(tip, await RevAsync(tree.Path, "HEAD"));
        Assert.Equal(tip, await RevAsync(root, tree.Branch));
    }

    /// <summary>A tree with uncommitted work is refused, saying how much, and the work is untouched.</summary>
    [Fact]
    public async Task A_tree_with_uncommitted_work_is_refused_and_the_work_kept()
    {
        var (_, origin, trees, tree, _) = await TreeAsync();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "own.txt"), "in flight\n");
        await PlatformCommitAsync(origin, "line.txt", "theirs\n", "the line moves on");
        var tip = await RevAsync(tree.Path, "HEAD");

        var done = await trees.UpdateTreeAsync(tree.Path, Idle);

        Assert.False(done.Done);
        Assert.Contains("uncommitted work (1 path(s))", done.Message);
        Assert.Equal("in flight\n", await File.ReadAllTextAsync(Path.Combine(tree.Path, "own.txt")));
        Assert.Equal(tip, await RevAsync(tree.Path, "HEAD"));
    }

    /// <summary>A branch on its remote would need a force push to replay: refused, and the branch stays.</summary>
    [Fact]
    public async Task A_tree_whose_branch_is_on_its_remote_is_refused()
    {
        var (root, origin, trees, tree, _) = await TreeAsync();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        await GitAsync(root, "push", "--quiet", "origin", tree.Branch);
        await PlatformCommitAsync(origin, "line.txt", "theirs\n", "the line moves on");
        var tip = await RevAsync(root, tree.Branch);

        var done = await trees.UpdateTreeAsync(tree.Path, Idle);

        Assert.False(done.Done);
        Assert.Contains($"`{tree.Branch}` is on its remote", done.Message);
        Assert.Contains("force push", done.Message);
        Assert.Equal(tip, await RevAsync(root, tree.Branch));
        Assert.Equal(tip, await RevAsync(tree.Path, "HEAD"));
    }

    /// <summary>A replay that conflicts is aborted and names the file: the branch, the tree and its files are as they were.</summary>
    [Fact]
    public async Task A_conflict_on_a_branch_is_aborted_and_named_and_the_tree_is_as_it_was()
    {
        var (root, origin, trees, tree, _) = await TreeAsync();
        await CommitAsync(tree.Path, "shared.txt", "mine\n", "the session's");
        await PlatformCommitAsync(origin, "shared.txt", "theirs\n", "someone else's");
        var tip = await RevAsync(root, tree.Branch);

        var done = await trees.UpdateTreeAsync(tree.Path, Idle);

        Assert.False(done.Done);
        Assert.Null(done.Moved);
        Assert.Contains("CONFLICT", done.Message);
        Assert.Contains("shared.txt", done.Message);
        Assert.Contains("aborted", done.Message);
        Assert.Equal(tip, await RevAsync(root, tree.Branch));
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(tree.Path, "status", "--porcelain")).Trim());
        Assert.Equal("mine\n", await CheckedOut(Path.Combine(tree.Path, "shared.txt")));
    }

    /// <summary>A detached tree's conflict is aborted the same way: HEAD back at its commit, still detached, its files as they were.</summary>
    [Fact]
    public async Task A_conflict_on_a_detached_tree_is_aborted_and_named_and_the_tree_is_as_it_was()
    {
        var (_, origin, trees, tree, _) = await TreeAsync();
        await GitAsync(tree.Path, "switch", "--quiet", "--detach");
        await CommitAsync(tree.Path, "shared.txt", "mine\n", "the session's, detached");
        await PlatformCommitAsync(origin, "shared.txt", "theirs\n", "someone else's");
        var head = await RevAsync(tree.Path, "HEAD");

        var done = await trees.UpdateTreeAsync(tree.Path, Idle);

        Assert.False(done.Done);
        Assert.StartsWith("the tree would not replay onto `main`: CONFLICT", done.Message);
        Assert.Contains("shared.txt", done.Message);
        Assert.EndsWith("The replay was aborted, and the tree is as it was.", done.Message);
        Assert.Equal(head, await RevAsync(tree.Path, "HEAD"));
        Assert.Equal("HEAD", (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(tree.Path, "status", "--porcelain")).Trim());
        Assert.Equal("mine\n", await CheckedOut(Path.Combine(tree.Path, "shared.txt")));
    }

    /// <summary>A tree already holding the line's latest, fetched now, is refused in words and nothing moves.</summary>
    [Fact]
    public async Task A_tree_already_on_the_latest_line_is_said_to_be()
    {
        var (_, _, trees, tree, _) = await TreeAsync();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        var tip = await RevAsync(tree.Path, "HEAD");

        var done = await trees.UpdateTreeAsync(tree.Path, Idle);

        Assert.False(done.Done);
        Assert.Equal("already based on `main`", done.Message);
        Assert.Equal(tip, await RevAsync(tree.Path, "HEAD"));
    }

    /// <summary>
    /// A line that cannot be fetched is refused with git's words (point 2), rather than judged against the last fetch; a repository
    /// whose trees another replay holds is refused; and a folder that is no tree of this home's is not read at all.
    /// </summary>
    [Fact]
    public async Task A_failed_fetch_a_held_repository_and_a_folder_not_this_home_s_are_refused()
    {
        var (root, origin, trees, tree, _) = await TreeAsync();
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        await PlatformCommitAsync(origin, "line.txt", "theirs\n", "the line moves on");
        var tip = await RevAsync(tree.Path, "HEAD");

        using (var held = TreeLock.TryReplaying(Home, "aurora", "engine"))
        {
            Assert.NotNull(held);
            var refused = await trees.UpdateTreeAsync(tree.Path, Idle);
            Assert.False(refused.Done);
            Assert.Contains("`engine`'s trees are held", refused.Message);
        }

        Directory.Move(origin, origin + ".gone");
        var offline = await trees.UpdateTreeAsync(tree.Path, Idle);
        Assert.False(offline.Done);
        Assert.StartsWith("could not fetch `main`: ", offline.Message);
        Assert.EndsWith("Nothing was moved.", offline.Message);
        Assert.Equal(tip, await RevAsync(tree.Path, "HEAD"));

        var outside = await trees.UpdateTreeAsync(root, Idle);
        Assert.False(outside.Done);
        Assert.Equal(TreeUpdateWords.Refused(TreeRefusal.NotOurs, new TreeFacts()), outside.Message);
    }

    /// <summary>
    /// A repository with no <c>origin</c> has nothing to fetch, which is no failed fetch: local operation needs no remote, so the
    /// tree is brought up to the line this checkout has, and the answer says so.
    /// </summary>
    [Fact]
    public async Task A_repository_with_no_origin_is_brought_up_to_its_own_line()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "own.txt", "mine\n", "its own work");
        await CommitAsync(root, "line.txt", "theirs\n", "the line moves on");

        var done = await trees.UpdateTreeAsync(tree.Path, Idle);

        Assert.True(done.Done, done.Message);
        Assert.EndsWith(TreeUpdateWords.NoOrigin, done.Message);
        Assert.Equal(await RevAsync(root, "main"), await RevAsync(tree.Path, "HEAD~1"));
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
    }

    /// <summary>
    /// A commit on origin's line, as a platform's merge leaves it: nothing here knows of it until a fetch. Made in the bare origin
    /// itself (PROCSPEED1b).
    /// </summary>
    private static Task PlatformCommitAsync(string origin, string file, string content, string message) =>
        CommitOnOriginAsync(origin, message, (file, content));

    private static async Task<string> RevAsync(string cwd, string revision) => (await GitAsync(cwd, "rev-parse", revision)).Trim();
}
