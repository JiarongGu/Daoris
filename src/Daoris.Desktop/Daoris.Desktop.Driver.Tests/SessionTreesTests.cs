using System.Diagnostics;
using Daoris.Driver;

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
/// makes its own repository, so nothing here is order-dependent.
/// </remarks>
[Trait(Category.Name, Category.Process)]
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

        Assert.StartsWith(Path.Combine(_home, "trees", "aurora", "engine"), opened.Path);
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
        var (_, worktrees, _) = await GitTupleAsync(outer, "worktree", "list");
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
            var added = await GitTupleAsync(root, "add", ".");
            Assert.True(added.Code == 0, $"the fixture must fit under its own root: {added.Stderr}");
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

    private static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var (_, stdout, _) = await GitTupleAsync(cwd, arguments);
        return stdout;
    }

    private static async Task<(int Code, string Stdout, string Stderr)> GitTupleAsync(
        string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout, await stderr);
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
