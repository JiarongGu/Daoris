using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Session trees (D51/SURF3): the worktree half of "the tree is the unit of exclusion, and a
/// repository may have more than one". These tests drive REAL git — worktree semantics are git's, and
/// a mock agreeing with a guess about them is the mock-vs-mock trap the modules tests already paid
/// for once.
/// </summary>
/// <remarks>
/// Fixtures live under the repository's gitignored `_fixtures/` — the family's own scratch rule —
/// except the path-length test's, which needs a root shorter than the checkout's (MOD8). Each test
/// makes its own repository, so nothing here is order-dependent, and it starts git alone (a git hook the
/// cut-off open waits in included): it runs four at once with the git-only classes (PROC1f).
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class SessionTreesTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;

    public SessionTreesTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "session-trees", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose() => Remove(_scratch);

    private static void Remove(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
            // A straggling git handle on Windows; the next run's fresh GUID never collides with it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: read-only pack files under .git occasionally refuse the first delete.
        }
    }

    [Fact]
    public async Task A_tree_opens_under_the_home_on_its_own_branch_based_on_the_canonical_line()
    {
        var root = await MakeRepositoryAsync("engine");
        var trees = new SessionTrees(_home);

        var opened = await trees.OpenAsync(root, "engine", "aurora");

        Assert.StartsWith(DaorisHome.Entry(_home, HomeEntry.Trees,"aurora", "engine"), opened.Path);
        Assert.StartsWith("daoris/", opened.Branch);
        // WSP4's resolution: the canonical line where one is declared — and a fresh `git init` fixture
        // HAS one (its own main/master, found by the local-branch fallback) — and it SAYS which it
        // used, because a branch based on the wrong point is invisible until merge time.
        Assert.Contains("canonical line", opened.BasedOn);
        // The price, stated by the act that incurs it: a fresh tree holds nothing git does not track.
        Assert.Contains("nothing git does not track", opened.Sentence);

        Assert.True(Directory.Exists(opened.Path));
        var (clean, _) = await WorkingTree.CleanAsync(opened.Path);
        Assert.True(clean, "a fresh tree is clean by construction");
        // …and it is a real linked worktree of the root, not a copy: one history, two trees.
        Assert.True(File.Exists(Path.Combine(opened.Path, ".git")), ".git is a FILE in a linked worktree");
    }

    /// <summary>Two sessions, two trees, two branches — a name is never reused (D51 rule 2).</summary>
    [Fact]
    public async Task Two_opens_never_share_a_path_or_a_branch()
    {
        var root = await MakeRepositoryAsync("engine");
        var trees = new SessionTrees(_home);

        var first = await trees.OpenAsync(root, "engine", "aurora");
        var second = await trees.OpenAsync(root, "engine", "aurora");

        Assert.NotEqual(first.Path, second.Path);
        Assert.NotEqual(first.Branch, second.Branch);
    }

    /// <summary>
    /// The point of the whole decision, on real git: the root is DIRTY — somebody's work in flight —
    /// and a session tree opens beside it anyway, clean, without touching it.
    /// </summary>
    [Fact]
    public async Task A_dirty_root_does_not_hold_a_session_tree_and_is_not_touched_by_one()
    {
        var root = await MakeRepositoryAsync("engine");
        await File.WriteAllTextAsync(Path.Combine(root, "wip.txt"), "uncommitted work in flight\n");
        var trees = new SessionTrees(_home);

        var opened = await trees.OpenAsync(root, "engine", "aurora");

        var (treeClean, _) = await WorkingTree.CleanAsync(opened.Path);
        Assert.True(treeClean);
        Assert.Equal("uncommitted work in flight\n", await File.ReadAllTextAsync(Path.Combine(root, "wip.txt")));
        Assert.False(File.Exists(Path.Combine(opened.Path, "wip.txt")), "uncommitted work never leaks into a fresh tree");
    }

    [Fact]
    public async Task A_clean_merged_tree_removes_whole_branch_and_all()
    {
        var root = await MakeRepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");

        var removal = await trees.RemoveAsync(opened.Path);

        Assert.True(removal.Removed);
        Assert.False(Directory.Exists(opened.Path));
        var branches = await GitAsync(root, "branch", "--list", opened.Branch);
        Assert.Equal("", branches.Trim());
    }

    /// <summary>Nothing deletes itself (D51 rule 7): uncommitted work refuses, and names itself.</summary>
    [Fact]
    public async Task A_tree_with_uncommitted_work_refuses_to_be_removed()
    {
        var root = await MakeRepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(opened.Path, "half-done.txt"), "not yet committed\n");

        var removal = await trees.RemoveAsync(opened.Path);

        Assert.False(removal.Removed);
        Assert.Contains("uncommitted", removal.Message);
        Assert.Contains("--force", removal.Message);
        Assert.True(File.Exists(Path.Combine(opened.Path, "half-done.txt")), "the refusal preserved the work");
    }

    /// <summary>…and commits the canonical line has not taken refuse the same way, naming them.</summary>
    [Fact]
    public async Task A_tree_with_unmerged_commits_refuses_and_names_them()
    {
        var root = await MakeRepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(opened.Path, "landed.txt"), "session work\n");
        await GitAsync(opened.Path, "add", ".");
        await GitAsync(opened.Path, "commit", "-m", "the session's landing");

        var removal = await trees.RemoveAsync(opened.Path);

        Assert.False(removal.Removed);
        Assert.Contains("the session's landing", removal.Message);
        Assert.True(Directory.Exists(opened.Path));

        // The person saying it again, meaning it: force removes the tree AND its branch, work and all.
        var forced = await trees.RemoveAsync(opened.Path, force: true);
        Assert.True(forced.Removed);
        Assert.False(Directory.Exists(opened.Path));
    }

    /// <summary>
    /// The guard that keeps this from ever being pointed at somebody's checkout: only a tree under
    /// Daoris's own trees home may be removed. Everything else is somebody's, whatever git says.
    /// </summary>
    [Fact]
    public async Task Removal_refuses_anything_outside_the_trees_home()
    {
        var root = await MakeRepositoryAsync("engine");
        var trees = new SessionTrees(_home);

        var removal = await trees.RemoveAsync(root);

        Assert.False(removal.Removed);
        Assert.Contains("not a session tree", removal.Message);
        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public async Task The_trees_of_this_machine_are_listable_with_their_state()
    {
        var root = await MakeRepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");

        var listed = await trees.ListAsync();

        var entry = Assert.Single(listed);
        Assert.Equal(opened.Path, entry.Path);
        Assert.Equal("engine", entry.Repository);
        Assert.Equal("aurora", entry.Workspace);
        Assert.Equal(opened.Branch, entry.Branch);
    }

    /// <summary>
    /// A root that is not the top of its own working tree refuses BEFORE git grows anything. Git
    /// resolves a repository by walking UP, so without this check a folder that merely sits inside
    /// some other checkout would grow worktrees and branches on THAT repository — this suite's own
    /// first run did it to the Daoris repository itself, which is the incident this test now pins.
    /// </summary>
    [Fact]
    public async Task A_folder_inside_another_repository_refuses_rather_than_growing_worktrees_on_it()
    {
        var outer = await MakeRepositoryAsync("outer");
        var inside = Path.Combine(outer, "not-a-repo");
        Directory.CreateDirectory(inside);
        var trees = new SessionTrees(_home);

        var error = await Assert.ThrowsAsync<DriverException>(() => trees.OpenAsync(inside, "x", "default"));

        Assert.Contains("INSIDE", error.Message);
        var worktrees = await GitAsync(outer, "worktree", "list");
        Assert.DoesNotContain("daoris", worktrees);
    }

    /// <summary>A folder in no repository at all refuses with a sentence rather than a git stack.</summary>
    [Fact]
    public async Task A_rootless_folder_refuses_with_a_sentence_rather_than_a_git_stack()
    {
        // Outside the scratch (which lives inside the Daoris repository — where git would walk up and
        // find a repository): the OS temp root belongs to no checkout, which is the very thing under test.
        var bare = Path.Combine(Path.GetTempPath(), $"daoris-norepo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bare);
        try
        {
            var trees = new SessionTrees(_home);

            var error = await Assert.ThrowsAsync<DriverException>(() => trees.OpenAsync(bare, "x", "default"));
            Assert.Contains("not a git repository", error.Message);
        }
        finally
        {
            Directory.Delete(bare, recursive: true);
        }
    }

    /// <summary>
    /// 🔴 Windows' 260-character path limit, found on the first real workspace (2026-09-27): a
    /// repository's deepest tracked file fitted under its own root and not under a session tree, whose
    /// prefix is longer. `worktree add` failed on every tick, and the quest sat.
    /// </summary>
    /// <remarks>
    /// MOD8: the depth was a constant sized for where the main checkout sits. In a git worktree the
    /// repository sits deeper, the fixture's own `git add` crossed the limit, and the test failed in
    /// every worktree. It now makes its repository under a short root of its own and sizes the path
    /// from that root, so it proves the same thing wherever the repository sits.
    /// </remarks>
    [Fact]
    public async Task A_tracked_path_that_fits_the_root_but_not_a_trees_longer_prefix_still_opens()
    {
        // OS temp, as many of this assembly's fixtures are: `_fixtures/` sits as deep as the checkout.
        var shortRoot = Path.Combine(Path.GetTempPath(), "daoris-lp-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var home = Path.Combine(shortRoot, "daoris-home");
            Directory.CreateDirectory(home);
            var root = await MakeRepositoryAsync("engine", under: shortRoot);
            // At most 240 characters under the root: inside Windows' 248 for a folder and 260 for a
            // file. The tree's prefix is 36 longer (`daoris-home/trees/aurora/` and `/s-xxxxxxxx`),
            // so the same path crosses 260 under it.
            const string segment = "component/";
            var room = 240 - (root.Length + 1) - "x.ts".Length;
            Assert.True(room >= 5 * segment.Length, $"the short root is too long to fit a deep path under: {root}");
            var deep = string.Concat(Enumerable.Repeat(segment, room / segment.Length)) + "x.ts";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, deep))!);
            await File.WriteAllTextAsync(Path.Combine(root, deep), "x\n");
            var added = await RunAsync(root, "add", ".");
            Assert.True(added.ExitCode == 0, $"the fixture must fit under its own root: {added.Stderr}");
            await GitAsync(root, "commit", "-m", "a deep file");

            var opened = await new SessionTrees(home).OpenAsync(root, "engine", "aurora");

            Assert.True(Path.Combine(opened.Path, deep).Length > 260, "the fixture must cross the limit to test it");
            Assert.True(File.Exists(Path.Combine(opened.Path, deep)));
        }
        finally
        {
            Remove(shortRoot);
        }
    }

    /// <summary>
    /// A tree that cannot be opened leaves the repository as it found it. `worktree add -b` makes the
    /// branch before it checks anything out, so every failed tick left one: sixteen on the first real
    /// workspace. And the refusal quotes git's error, where it quoted git's progress line
    /// (*"Preparing worktree (new branch …)"*), which said nothing about why.
    /// </summary>
    [Fact]
    public async Task A_tree_that_fails_to_check_out_leaves_no_branch_and_quotes_gits_error()
    {
        var root = await MakeRepositoryAsync("engine");
        // A checkout that fails after the branch exists, on every platform: a required filter that fails.
        await File.WriteAllTextAsync(Path.Combine(root, ".gitattributes"), "*.txt filter=boom\n");
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "x\n");
        await GitAsync(root, "config", "filter.boom.clean", "cat");
        await GitAsync(root, "config", "filter.boom.smudge", "false");
        await GitAsync(root, "config", "filter.boom.required", "true");
        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "-m", "a file only a working filter can check out");

        var error = await Assert.ThrowsAsync<DriverException>(
            () => new SessionTrees(_home).OpenAsync(root, "engine", "aurora"));

        Assert.DoesNotContain("Preparing worktree", error.Message);
        Assert.Contains("boom", error.Message);
        Assert.Equal("", (await GitAsync(root, "branch", "--list", "daoris/*")).Trim());
    }

    /// <summary>
    /// OPENCUT1: an open cut off once git has added its tree, by a cancel as the app's close cuts it, leaves nothing: no branch,
    /// no folder, no record of a tree in git. While it opens, the tree is locked, and the lock's reason says who holds it.
    /// </summary>
    [Fact]
    public async Task An_open_cancelled_once_its_tree_is_added_leaves_nothing_and_its_lock_said_who()
    {
        var root = await MakeRepositoryAsync("engine");
        using var closing = new CancellationTokenSource();
        string? lockSaid = null;
        // The git right after `worktree add`, which reads the branch it made: the open is cut there.
        WorkingTree.Started.Value = arguments =>
        {
            if (closing.IsCancellationRequested || arguments is not ["rev-parse", "--verify", "--quiet", var reference]
                || !reference.StartsWith("refs/heads/daoris/s-", StringComparison.Ordinal))
            {
                return;
            }

            var locked = Path.Combine(root, ".git", "worktrees", reference["refs/heads/daoris/".Length..], "locked");
            lockSaid = File.Exists(locked) ? File.ReadAllText(locked).TrimEnd('\n') : null;
            closing.Cancel();
        };
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SessionTrees(_home).OpenAsync(root, "engine", "aurora", closing.Token));
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }

        Assert.Equal($"Daoris is opening this tree (process {Environment.ProcessId})", lockSaid);
        await AssertNothingLeftAsync(root);
    }

    /// <summary>
    /// OPENCUT1: an open cut off while git is still adding its tree, the branch made and the tree checked out, leaves nothing.
    /// git runs <c>post-checkout</c> before its add returns, so a hook that waits holds git there while the open is cancelled.
    /// </summary>
    [Fact]
    public async Task An_open_cancelled_while_git_is_still_adding_its_tree_leaves_nothing()
    {
        var root = await MakeRepositoryAsync("engine");
        var ran = Path.Combine(_scratch, "post-checkout-ran");
        await File.WriteAllTextAsync(
            Path.Combine(root, ".git", "hooks", "post-checkout"), $"#!/bin/sh\necho ran > '{ran.Replace('\\', '/')}'\nsleep 120\n");
        using var closing = new CancellationTokenSource();

        var opening = new SessionTrees(_home).OpenAsync(root, "engine", "aurora", closing.Token);
        await Poll.Until(() => File.Exists(ran) || opening.IsCompleted, () => "git never ran the hook", TimeSpan.FromMinutes(2));
        Assert.Single((await GitAsync(root, "branch", "--list", "daoris/*")).Split('\n', StringSplitOptions.RemoveEmptyEntries));
        await closing.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        await AssertNothingLeftAsync(root);
    }

    /// <summary>
    /// TREEDEPS1 (D51's note): a tree whose npm lockfile is its checkout's opens with the dependencies the checkout installed at it,
    /// copied in and never linked, and its opening and its agent say so. Git ignores the copy in the tree as in the checkout.
    /// </summary>
    [Fact]
    public async Task A_tree_whose_lockfile_matches_the_checkout_s_opens_with_its_dependencies_copied()
    {
        var root = await MakeNodeRepositoryAsync("engine");

        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");

        var copied = Path.Combine(opened.Path, "node_modules", "left-pad", "index.js");
        Assert.Equal("module.exports = 'left-pad';\n", await File.ReadAllTextAsync(copied));
        Assert.Contains("copied in: `node_modules`.", opened.Sentence);
        Assert.Contains("do not run `npm ci`", opened.Dependencies);
        var (clean, _) = await WorkingTree.CleanAsync(opened.Path);
        Assert.True(clean, "the copy is ignored in the tree as in the checkout");

        await File.WriteAllTextAsync(copied, "changed in the tree\n");
        Assert.Equal("module.exports = 'left-pad';\n", await File.ReadAllTextAsync(Path.Combine(root, "node_modules", "left-pad", "index.js")));
    }

    /// <summary>TREEDEPS1: a checkout whose lockfile moved since it was committed installed another set; the tree copies none, and says why.</summary>
    [Fact]
    public async Task A_tree_whose_lockfile_moved_opens_without_dependencies_and_says_why()
    {
        var root = await MakeNodeRepositoryAsync("engine");
        await File.WriteAllTextAsync(Path.Combine(root, "package-lock.json"), "{\n  \"lockfileVersion\": 3,\n  \"packages\": {}\n}\n");

        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");

        Assert.False(Directory.Exists(Path.Combine(opened.Path, "node_modules")));
        Assert.Contains("nothing git does not track", opened.Sentence);
        Assert.Contains("`node_modules` was not copied from your checkout: this tree's lockfile beside it differs from the checkout's", opened.Sentence);
        Assert.Null(opened.Dependencies);
    }

    /// <summary>TREEDEPS1, OPENCUT1: an open cut off while it copies the checkout's dependencies leaves nothing, no half copy among it.</summary>
    [Fact]
    public async Task An_open_cancelled_while_it_copies_dependencies_leaves_nothing()
    {
        var root = await MakeNodeRepositoryAsync("engine");
        using var closing = new CancellationTokenSource();
        TreeDependencies.Copying.Value = _ => closing.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SessionTrees(_home).OpenAsync(root, "engine", "aurora", closing.Token));
        }
        finally
        {
            TreeDependencies.Copying.Value = null;
        }

        await AssertNothingLeftAsync(root);
        Assert.Equal("module.exports = 'left-pad';\n", await File.ReadAllTextAsync(Path.Combine(root, "node_modules", "left-pad", "index.js")));
    }

    /// <summary>
    /// TREEDEPS1, TIDIEDLINK1: a tree holding a copied workspace link is removed as any other, and git's removal takes the link, never
    /// what the checkout's own link leads to.
    /// </summary>
    [Fact]
    public async Task Removing_a_tree_with_copied_dependencies_removes_their_workspace_link_not_what_it_points_at()
    {
        var root = await MakeNodeRepositoryAsync("engine", workspace: true);
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        Assert.True(new DirectoryInfo(Path.Combine(opened.Path, "node_modules", "ui")).Attributes.HasFlag(FileAttributes.ReparsePoint),
            "the workspace is copied as a link");

        var removal = await trees.RemoveAsync(opened.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(opened.Path));
        Assert.Equal("module.exports = 'ui';\n", await File.ReadAllTextAsync(Path.Combine(root, "packages", "ui", "index.js")));
        Assert.Equal("module.exports = 'ui';\n", await File.ReadAllTextAsync(Path.Combine(root, "node_modules", "ui", "index.js")));
    }

    /// <summary>
    /// CHAIN2, the owner's answer: a chain's next step to the same repository starts ON the branch the
    /// step before it landed on, so a verify step sees the unmerged work it exists to check.
    /// </summary>
    [Fact]
    public async Task A_tree_grown_from_a_named_branch_holds_that_branchs_work()
    {
        var root = await MakeRepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var develop = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(develop.Path, "notes.ts"), "export const notes = true;\n");
        await GitAsync(develop.Path, "add", ".");
        await GitAsync(develop.Path, "commit", "-m", "the develop step's work");

        var verify = await trees.OpenAsync(root, "engine", "aurora", from: develop.Branch);

        Assert.True(File.Exists(Path.Combine(verify.Path, "notes.ts")), "the next step's tree lacks the work before it");
        Assert.Contains(develop.Branch, verify.BasedOn);
        Assert.NotEqual(develop.Branch, verify.Branch);
    }

    /// <summary>A branch that is gone — merged and deleted, or discarded — falls back to the canonical line, and says so.</summary>
    [Fact]
    public async Task A_named_branch_that_is_gone_grows_from_the_canonical_line_and_says_why()
    {
        var root = await MakeRepositoryAsync("engine");

        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora", from: "daoris/s-gone0000");

        Assert.Contains("canonical line", opened.BasedOn);
        Assert.Contains("daoris/s-gone0000", opened.BasedOn);
    }

    // ---------------------------------------------------------------------------------- fixtures

    /// <summary>OPENCUT1: the repository as it was before the open: no session branch, no folder under its trees, no tree in git but the root.</summary>
    private async Task AssertNothingLeftAsync(string root)
    {
        Assert.Equal("", (await GitAsync(root, "branch", "--list", "daoris/*")).Trim());
        var folder = DaorisHome.Entry(_home, HomeEntry.Trees,"aurora", "engine");
        Assert.True(!Directory.Exists(folder) || Directory.GetFileSystemEntries(folder).Length == 0, "a folder is left under the trees");
        Assert.Single((await GitAsync(root, "worktree", "list", "--porcelain")).Split('\n'), line => line.StartsWith("worktree ", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(root, ".git", "worktrees")) && Directory.GetDirectories(Path.Combine(root, ".git", "worktrees")).Length > 0,
            "git keeps a record of a tree");
    }

    private async Task<string> MakeRepositoryAsync(string name, string? under = null)
    {
        var root = Path.Combine(under ?? _scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), $"# {name}\n");
        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "-m", "first");
        return root;
    }

    /// <summary>
    /// A repository with an npm project its checkout installed (TREEDEPS1): the lockfile committed, <c>node_modules</c> ignored and
    /// installed, npm's hidden lockfile written after the lockfile; with <paramref name="workspace"/>, a workspace npm links in.
    /// </summary>
    private async Task<string> MakeNodeRepositoryAsync(string name, bool workspace = false)
    {
        var root = await MakeRepositoryAsync(name);
        // The lockfile's bytes are compared: git writes the tree's as committed, so the checkout's is kept as committed too.
        await GitAsync(root, "config", "core.autocrlf", "false");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "node_modules/\n");
        var lockfile = Path.Combine(root, "package-lock.json");
        await File.WriteAllTextAsync(lockfile, workspace
            ? "{\n  \"lockfileVersion\": 3,\n  \"packages\": {\n    \"\": { \"workspaces\": [\"packages/ui\"] },\n"
              + "    \"node_modules/ui\": { \"resolved\": \"packages/ui\", \"link\": true },\n    \"packages/ui\": { \"name\": \"ui\" }\n  }\n}\n"
            : "{\n  \"lockfileVersion\": 3,\n  \"packages\": {\n    \"\": {},\n    \"node_modules/left-pad\": { \"version\": \"1.3.0\" }\n  }\n}\n");
        if (workspace)
        {
            Directory.CreateDirectory(Path.Combine(root, "packages", "ui"));
            await File.WriteAllTextAsync(Path.Combine(root, "packages", "ui", "index.js"), "module.exports = 'ui';\n");
        }

        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "-m", "an npm project");
        foreach (var package in new[] { "a", "b", "left-pad" })
        {
            Directory.CreateDirectory(Path.Combine(root, "node_modules", package));
            await File.WriteAllTextAsync(Path.Combine(root, "node_modules", package, "index.js"), $"module.exports = '{package}';\n");
        }

        var hidden = Path.Combine(root, "node_modules", ".package-lock.json");
        await File.WriteAllTextAsync(hidden, "{}\n");
        File.SetLastWriteTimeUtc(hidden, File.GetLastWriteTimeUtc(lockfile).AddMinutes(1));
        if (workspace) TreeDependencies.Link(Path.Combine(root, "node_modules", "ui"), Path.Combine(root, "packages", "ui"));
        return root;
    }

    /// <summary>Walk up from the test binary to the workspace manifest — the family's own locator shape.</summary>
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json")))
        {
            directory = directory.Parent!;
        }

        return directory!.FullName;
    }
}
