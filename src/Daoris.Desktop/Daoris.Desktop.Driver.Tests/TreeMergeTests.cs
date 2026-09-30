using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Merging a session tree into the canonical line (D51 rule 6, design §5, SURF6b).
/// </summary>
/// <remarks>
/// <para>🔴 This is the one place Daoris writes into a checkout it did not create, so these tests are
/// mostly about the merge <b>not</b> happening. Every guard `reaching-in` names has a case here, and
/// each asserts that the refusal left the checkout untouched — a guard that refuses after doing half
/// the work is not a guard.</para>
///
/// <para>Real git throughout, for the reason the other tree tests use it: worktree and merge
/// semantics are git's, and a mock that agrees with a guess about them proves only that the guess is
/// self-consistent. Fixtures live under the repository's gitignored `_fixtures/`.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class TreeMergeTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;

    public TreeMergeTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "tree-merge", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle; the next run's GUID never collides */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
    }

    [Fact]
    public async Task It_lands_the_trees_commits_as_one_revertable_merge()
    {
        var root = await MakeRepositoryAsync();
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitInAsync(opened.Path, "budget.ts", "cap the work per frame", "cap hydration");

        var merged = await trees.MergeAsync(opened.Path);

        Assert.True(merged.Merged, merged.Message);
        Assert.Contains("1 commit", merged.Message);
        Assert.Contains("cap hydration", await GitAsync(root, "log", "--oneline"));
        // `--no-ff`, so what landed is one commit a person can read and revert as a unit.
        Assert.Contains("Merge branch", await GitAsync(root, "log", "-1", "--format=%s"));
        // The tree survives a merge. Discarding it stays a separate act the person asks for.
        Assert.True(Directory.Exists(opened.Path));
    }

    /// <summary>
    /// 🔴 The `reaching-in` guard, and the reason this is a press rather than an automation.
    /// Uncommitted work in the repository's own checkout is exactly what that document was written
    /// from, and merging into it is how you lose it.
    /// </summary>
    [Fact]
    public async Task It_refuses_into_a_checkout_somebody_is_working_in()
    {
        var root = await MakeRepositoryAsync();
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitInAsync(opened.Path, "budget.ts", "cap the work", "cap hydration");

        // Work in flight in the root — uncommitted, exactly as a person leaves it.
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# engine, mid-edit\n");

        var merged = await trees.MergeAsync(opened.Path);

        Assert.False(merged.Merged);
        Assert.Contains("not clean", merged.Message);
        // Untouched: nothing was staged, stashed or committed on anybody's behalf.
        Assert.Contains("mid-edit", await File.ReadAllTextAsync(Path.Combine(root, "README.md")));
        Assert.DoesNotContain("cap hydration", await GitAsync(root, "log", "--oneline"));
    }

    /// <summary>Switching a branch in a checkout Daoris did not create is the same trespass, smaller.</summary>
    [Fact]
    public async Task It_refuses_when_the_checkout_is_not_on_the_canonical_line()
    {
        var root = await MakeRepositoryAsync();
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitInAsync(opened.Path, "budget.ts", "cap the work", "cap hydration");

        await GitAsync(root, "checkout", "--quiet", "-b", "somebody-elses-branch");

        var merged = await trees.MergeAsync(opened.Path);

        Assert.False(merged.Merged);
        // It names both lines, so the person knows what to put it back to.
        Assert.Contains("somebody-elses-branch", merged.Message);
        Assert.Contains("does not switch a branch", merged.Message);
    }

    /// <summary>Uncommitted work in the SESSION tree would not travel, and "merged" would be a lie.</summary>
    [Fact]
    public async Task It_refuses_while_the_session_tree_still_holds_uncommitted_work()
    {
        var root = await MakeRepositoryAsync();
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(opened.Path, "half-done.ts"), "in progress\n");

        var merged = await trees.MergeAsync(opened.Path);

        Assert.False(merged.Merged);
        Assert.Contains("uncommitted work", merged.Message);
    }

    [Fact]
    public async Task A_tree_that_landed_nothing_is_told_so_rather_than_making_an_empty_commit()
    {
        var root = await MakeRepositoryAsync();
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");

        var before = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var merged = await trees.MergeAsync(opened.Path);

        Assert.False(merged.Merged);
        Assert.Contains("nothing to merge", merged.Message);
        Assert.Equal(before, (await GitAsync(root, "rev-parse", "HEAD")).Trim());
    }

    /// <summary>A conflict aborts, and the checkout is exactly as it was found.</summary>
    [Fact]
    public async Task A_conflicting_merge_aborts_and_leaves_the_checkout_alone()
    {
        var root = await MakeRepositoryAsync();
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");

        // The same file, changed differently on both sides.
        await CommitInAsync(opened.Path, "README.md", "# from the session", "session edit");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# from the person\n");
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "commit", "-m", "person edit");

        var head = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var merged = await trees.MergeAsync(opened.Path);

        Assert.False(merged.Merged);
        Assert.Contains("aborted", merged.Message);
        Assert.Equal(head, (await GitAsync(root, "rev-parse", "HEAD")).Trim());
        // Not left mid-merge: a half-merged checkout is worse than an honest refusal.
        var readme = await File.ReadAllTextAsync(Path.Combine(root, "README.md"));
        Assert.Contains("from the person", readme);
        Assert.DoesNotContain("<<<<<<<", readme);
    }

    /// <summary>A checkout Daoris did not open is never merged FROM, whatever git would say.</summary>
    [Fact]
    public async Task It_refuses_anything_that_is_not_a_session_tree()
    {
        var root = await MakeRepositoryAsync();
        var trees = new SessionTrees(_home);

        var merged = await trees.MergeAsync(root);

        Assert.False(merged.Merged);
        Assert.Contains("not a session tree", merged.Message);
    }

    /// <summary>The two acts compose: once merged, removal's own pre-check passes unforced.</summary>
    [Fact]
    public async Task A_merged_tree_removes_without_being_forced()
    {
        var root = await MakeRepositoryAsync();
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitInAsync(opened.Path, "budget.ts", "cap the work", "cap hydration");

        // Before merging, removal refuses and names the unmerged commits (D51 rule 7).
        var refused = await trees.RemoveAsync(opened.Path);
        Assert.False(refused.Removed);
        Assert.Contains("has not taken", refused.Message);

        Assert.True((await trees.MergeAsync(opened.Path)).Merged);

        var removed = await trees.RemoveAsync(opened.Path);
        Assert.True(removed.Removed, removed.Message);
    }

    private async Task CommitInAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "commit", "-m", message);
    }

    private async Task<string> MakeRepositoryAsync()
    {
        var root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# engine\n");
        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "-m", "first");
        return root;
    }

    private static async Task<string> GitAsync(string cwd, params string[] arguments)
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
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return stdout;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
