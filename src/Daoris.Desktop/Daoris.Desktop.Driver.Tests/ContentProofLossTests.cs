using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SQUASHTIDY1c (D51 rule 7, D102's SQUASHTIDY1 and SQUASHTIDY1c notes): the content proof loses nothing it deletes. A second
/// agent's read-only review named six ways it could; each is a test here, seen failing first in a real scratch repository, and
/// each holds what the fix guarantees. Real git, and nothing reaches a network: an <c>origin</c> is a bare repository under the
/// scratch folder.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ContentProofLossTests : LandedFixture
{
    private static (string, string?, string?)[] Engine(string root) => [("engine", "aurora", root)];

    // ---- (1) the tip deleted is the tip judged ----

    /// <summary>
    /// The review's Discard: a commit made on the branch between the judgement and the delete is never deleted with it. The
    /// seam is the moment before the delete, where a commit lands on the branch as a session's or a person's would.
    /// </summary>
    [Fact]
    public async Task A_commit_made_after_the_discard_judged_the_branch_is_never_deleted_with_it()
    {
        var root = await RepositoryAsync("engine");
        string? late = null;
        var trees = new SessionTrees(Home) { BeforeDeleting = Once(async branch => late = await MoveOnAsync(root, branch)) };
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.NotNull(late);
        Assert.Equal(late, await TipAsync(root, tree.Branch));
        Assert.Contains("moved since it was judged", removal.Message);
    }

    /// <summary>The clean-up's press: the same, for a branch whose tree is gone.</summary>
    [Fact]
    public async Task A_commit_made_after_the_clean_up_judged_the_branch_is_never_deleted_with_it()
    {
        var root = await RepositoryAsync("engine");
        string? late = null;
        var trees = new SessionTrees(Home) { BeforeDeleting = Once(async branch => late = await MoveOnAsync(root, branch)) };
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await GitAsync(root, "worktree", "remove", tree.Path);
        await SquashAsync(root, tree.Branch);

        var swept = await trees.SweepAsync(Engine(root), new HashSet<string>());

        Assert.NotNull(late);
        Assert.Equal(late, await TipAsync(root, tree.Branch));
        var result = Assert.Single(swept);
        Assert.False(result.Removed, result.Message);
        Assert.Contains("moved since it was judged", result.Message);
    }

    /// <summary>The terminal's <c>trees remove &lt;branch&gt;</c> for a branch whose tree is gone: the same.</summary>
    [Fact]
    public async Task A_commit_made_after_the_branch_door_judged_the_branch_is_never_deleted_with_it()
    {
        var root = await RepositoryAsync("engine");
        string? late = null;
        var trees = new SessionTrees(Home) { BeforeDeleting = Once(async branch => late = await MoveOnAsync(root, branch)) };
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await GitAsync(root, "worktree", "remove", tree.Path);
        await SquashAsync(root, tree.Branch);

        var removal = await trees.RemoveBranchAsync(root, "engine", tree.Branch);

        Assert.NotNull(late);
        Assert.Equal(late, await TipAsync(root, tree.Branch));
        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("moved since it was judged", removal.Message);
    }

    // ---- (2) a holder the same clean-up removes never vouches ----

    /// <summary>
    /// A branch a landing made, merged into the line, then the line reverted its change; a session redoes it. The landed
    /// branch's file reads as the session's, but the clean-up removes that branch too, as merged: the session branch must not
    /// have gone on its word, or the redo is on no ref at all.
    /// </summary>
    [Fact]
    public async Task A_branch_the_clean_up_itself_removes_never_vouches_for_a_session_branch()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "two\n", new LandingSubject("s1a2b3c4", "0fda18", "Two"));
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "merge the landing", landed.Branch!);
        await CommitAsync(root, "shared.txt", "one\n", "revert it on the line");
        var redo = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(redo.Path, "shared.txt", "two\n", "the redo");
        var redone = await TipAsync(root, redo.Branch);

        var done = await trees.CleanAsync(Engine(root), new HashSet<string>());

        Assert.Contains(done.Landed, result => result.Item.Branch == landed.Branch && result.Removed);
        Assert.Contains(redo.Branch, await GitAsync(root, "branch", "--list", "daoris/*"));
        Assert.Equal(redone, await TipAsync(root, redo.Branch));
        Assert.Contains(done.Sessions, result => result.Item.Branch == redo.Branch && !result.Removed && result.Item.Kind == SweepKind.Unlanded);
    }

    // ---- (3) a gitlink's change is a change ----

    /// <summary>
    /// <c>diff.ignoreSubmodules=all</c> hides a gitlink's change from porcelain <c>git diff</c>: the branch changed a file and
    /// a submodule's commit, a person's branch holds the file alone, and the proof must still see the gitlink differ.
    /// </summary>
    [Fact]
    public async Task A_submodules_change_hidden_by_config_still_differs()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "config", "diff.ignoreSubmodules", "all");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        var pinned = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "work.txt"), "the work\n");
        await GitAsync(tree.Path, "add", "work.txt");
        await GitAsync(tree.Path, "update-index", "--add", "--cacheinfo", $"160000,{pinned},lib");
        Directory.CreateDirectory(Path.Combine(tree.Path, "lib"));
        await GitAsync(tree.Path, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "the work and a submodule");
        await GitAsync(root, "branch", "feature/x", "main");
        await CommitOnAsync(root, "feature/x", "work.txt", "the work\n", "the work alone");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("has not taken", removal.Message);
        Assert.Contains(tree.Branch, await GitAsync(root, "branch", "--list", "daoris/*"));
    }

    // ---- (4) what the tree holds beyond its commits ----

    /// <summary>An ignored file only the session's tree holds (a local database) keeps the tree, named.</summary>
    [Fact]
    public async Task An_ignored_file_only_the_tree_holds_keeps_it_named()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, ".gitignore", "*.db\nbuild/\n", "ignore databases and builds");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "local.db"), "rows only this tree has\n");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("local.db", removal.Message);
        Assert.True(File.Exists(Path.Combine(tree.Path, "local.db")));
        Assert.Contains(tree.Branch, await GitAsync(root, "branch", "--list", "daoris/*"));
    }

    /// <summary>
    /// The narrowest rule: an ignored path the registered checkout holds too (a build's output, installed dependencies) is
    /// what the repository leaves in any checkout it is built in, so it goes with the tree and blocks nothing.
    /// </summary>
    [Fact]
    public async Task An_ignored_output_the_checkout_holds_too_goes_with_the_tree()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, ".gitignore", "*.db\nbuild/\n", "ignore databases and builds");
        Directory.CreateDirectory(Path.Combine(root, "build"));
        await File.WriteAllTextAsync(Path.Combine(root, "build", "out.bin"), "the checkout's own build\n");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        Directory.CreateDirectory(Path.Combine(tree.Path, "build"));
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "build", "out.bin"), "the session's build\n");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(tree.Path));
    }

    /// <summary>An untracked file is uncommitted work though <c>status.showUntrackedFiles=no</c> hides it from a plain status.</summary>
    [Fact]
    public async Task An_untracked_file_hidden_by_config_keeps_the_tree()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "config", "status.showUntrackedFiles", "no");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "notes.txt"), "never added\n");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("uncommitted", removal.Message);
        Assert.True(File.Exists(Path.Combine(tree.Path, "notes.txt")));
    }

    /// <summary>The clean-up's list keeps the same tree, so its press never takes the file either.</summary>
    [Fact]
    public async Task The_clean_up_keeps_a_tree_with_an_ignored_file_only_it_holds()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, ".gitignore", "*.db\n", "ignore databases");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await SquashAsync(root, tree.Branch);
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "local.db"), "rows only this tree has\n");

        var swept = await trees.SweepAsync(Engine(root), new HashSet<string>());

        var result = Assert.Single(swept);
        Assert.False(result.Removed, result.Message);
        Assert.Equal(SweepKind.Dirty, result.Item.Kind);
        Assert.Contains("local.db", result.Item.Detail);
        Assert.True(File.Exists(Path.Combine(tree.Path, "local.db")));
    }

    // ---- (5) what the final tree does not show: empty commits, history ----

    /// <summary>
    /// An empty commit carrying a note, and a file added then deleted, are in no tree a squash leaves: every commit of the
    /// branch stays on a ref of its own, which the sentence names.
    /// </summary>
    [Fact]
    public async Task Every_commit_a_content_held_discard_deletes_stays_on_a_recovery_ref_it_names()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "draft.txt", "a draft\n", "a draft");
        await GitAsync(tree.Path, "rm", "--quiet", "draft.txt");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the draft becomes the work");
        await GitAsync(tree.Path, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--allow-empty", "-m", "a note only");
        var commits = (await GitAsync(root, "rev-list", $"main..{tree.Branch}")).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, commits.Length);
        var judged = await TipAsync(root, tree.Branch);
        await SquashAsync(root, tree.Branch);

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        var kept = $"refs/daoris/discarded/{tree.Branch}";
        Assert.Contains($"`{kept}`", removal.Message);
        Assert.Equal(judged, (await GitAsync(root, "rev-parse", kept)).Trim());
        foreach (var commit in commits) Assert.NotEqual("", (await GitAsync(root, "for-each-ref", "--contains", commit)).Trim());
        Assert.Equal("", (await GitAsync(root, "branch", "--list", tree.Branch)).Trim());
    }

    /// <summary>The branch door and the clean-up keep the same ref for a branch whose tree is gone.</summary>
    [Fact]
    public async Task The_branch_door_and_the_clean_up_keep_a_recovery_ref_for_what_they_delete_on_content()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var door = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(door.Path, "door.txt", "by the door\n", "by the door");
        var swept = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(swept.Path, "swept.txt", "by the clean-up\n", "by the clean-up");
        var doorTip = await TipAsync(root, door.Branch);
        var sweptTip = await TipAsync(root, swept.Branch);
        await GitAsync(root, "worktree", "remove", door.Path);
        await GitAsync(root, "worktree", "remove", swept.Path);
        await SquashAsync(root, door.Branch);
        await SquashAsync(root, swept.Branch);

        var removal = await trees.RemoveBranchAsync(root, "engine", door.Branch);
        var done = await trees.SweepAsync(Engine(root), new HashSet<string>());

        Assert.True(removal.Removed, removal.Message);
        Assert.Contains($"`refs/daoris/discarded/{door.Branch}`", removal.Message);
        Assert.Equal(doorTip, (await GitAsync(root, "rev-parse", $"refs/daoris/discarded/{door.Branch}")).Trim());
        var result = Assert.Single(done);
        Assert.True(result.Removed, result.Message);
        Assert.Contains($"`refs/daoris/discarded/{swept.Branch}`", result.Message);
        Assert.Equal(sweptTip, (await GitAsync(root, "rev-parse", $"refs/daoris/discarded/{swept.Branch}")).Trim());
    }

    // ---- (6) origin's copy of the line is not the person's ----

    /// <summary>
    /// Held only on <c>origin/main</c>, which the next fetch may move: the platform rewrites the line after the discard, and
    /// the work must still be on a ref here.
    /// </summary>
    [Fact]
    public async Task Work_held_only_on_origins_line_survives_the_line_being_rewritten_after_the_discard()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        var judged = await TipAsync(root, tree.Branch);
        await GitAsync(root, "push", "--quiet", "origin", tree.Branch);
        await SquashOnPlatformAsync(origin, tree.Branch);
        await GitAsync(root, "fetch", "--quiet", "--prune", "origin");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.Contains("`origin/main`", removal.Message);
        // The platform drops the squash from its line; the next fetch moves origin/main back.
        var platform = Path.Combine(Scratch, "rewriter");
        await GitAsync(Scratch, "clone", "--quiet", origin, platform);
        await GitAsync(platform, "reset", "--quiet", "--hard", "HEAD~1");
        await GitAsync(platform, "push", "--quiet", "--force", "origin", "main");
        await GitAsync(root, "fetch", "--quiet", "--prune", "origin");
        Assert.Equal("", (await GitAsync(root, "branch", "--all", "--contains", judged)).Trim());

        Assert.NotEqual("", (await GitAsync(root, "for-each-ref", "--contains", judged)).Trim());
    }

    /// <summary>Where both forms of the line hold it, the person's own line is the one named.</summary>
    [Fact]
    public async Task Where_both_forms_hold_it_the_local_line_is_named()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await GitAsync(root, "push", "--quiet", "origin", tree.Branch);
        await SquashOnPlatformAsync(origin, tree.Branch);
        await GitAsync(root, "pull", "--quiet", "--ff-only", "origin", "main");

        var removal = await trees.RemoveAsync(tree.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.Contains("its work is on `main` by content", removal.Message);
    }

    /// <summary>A commit on <paramref name="branch"/> made through a worktree of its own, as a session or a person would; its id.</summary>
    private async Task<string> MoveOnAsync(string root, string branch)
    {
        await CommitOnAsync(root, branch, "late.txt", "made after the judgement\n", "a commit after the judgement");
        return await TipAsync(root, branch);
    }

    private static async Task<string> TipAsync(string root, string branch) =>
        (await GitAsync(root, "rev-parse", "--verify", "--quiet", $"refs/heads/{branch}")).Trim();

    /// <summary>The seam runs once: the first delete it precedes is the one the test races.</summary>
    private static Func<string, Task> Once(Func<string, Task> act)
    {
        var done = false;
        return async branch =>
        {
            if (done) return;
            done = true;
            await act(branch);
        };
    }
}
