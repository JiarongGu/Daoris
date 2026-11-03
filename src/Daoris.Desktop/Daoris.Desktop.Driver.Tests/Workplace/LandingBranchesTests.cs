using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a landing leaves of the session branches: it takes no branch but the pressed session's own, so a chain's earlier step and a failed
/// attempt stay and wait for their own door, which keeps one a live session's tree holds (LAND3c); a tree its agent moved onto a branch of
/// its own lands as that branch stands. Real git, as the other tree tests.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class LandingBranchesTests : IDisposable
{
    private static readonly Func<CancellationToken, Task<IReadOnlySet<string>>> NoneRunning =
        _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    private readonly string _scratch;
    private readonly string _home;

    public LandingBranchesTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "landing-branches", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    /// <summary>
    /// A failed attempt's branch holds commits the landing does not, so the landing leaves it and says nothing of it; its door is
    /// `trees remove`, which refuses unlanded work until the person says `--force`, and then forgets it in the record.
    /// </summary>
    [Fact]
    public async Task A_failed_attempts_branch_is_kept_until_its_own_door_removes_it()
    {
        var root = await RepositoryAsync("engine");
        var gate = GraphLand.Gate(_home, GraphLand.Branch("feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var failed = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(failed.Path, "attempt.txt", "a failed attempt", "a failed attempt");
        var done = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(done.Path, "work.txt", "the work", "the work");

        var landed = await trees.LandAsync(done.Path, new LandingSubject("s2", "0fda18", "Fix the gap"), inUse: NoneRunning, gate: gate);

        Assert.True(landed.Landed, landed.Message);
        Assert.True(await BranchAsync(root, failed.Branch), "a branch whose commits the landing does not hold stays");
        Assert.True(Directory.Exists(failed.Path));
        Assert.DoesNotContain(failed.Branch, landed.Message);

        // Its tree gone by hand, its branch left: the door finds it by name, refuses unlanded work, then discards it on --force.
        await GitAsync(root, "worktree", "remove", "--force", failed.Path);
        Assert.Equal(failed.Branch, Assert.Single(trees.FindBranches(failed.Branch[("daoris/".Length)..], null)).Branch);
        var refused = await trees.RemoveBranchAsync(root, "engine", failed.Branch);
        Assert.False(refused.Removed);
        Assert.Contains("a failed attempt", refused.Message);
        Assert.Contains("--force", refused.Message);
        Assert.True(await BranchAsync(root, failed.Branch));

        var removed = await trees.RemoveBranchAsync(root, "engine", failed.Branch, force: true);
        Assert.True(removed.Removed, removed.Message);
        Assert.False(await BranchAsync(root, failed.Branch));
        Assert.Empty(trees.Grown.All());
    }

    /// <summary>A failed attempt whose tree is still here goes through the same door: its tree with it, behind `--force`.</summary>
    [Fact]
    public async Task A_failed_attempts_branch_with_its_tree_goes_with_its_tree_on_force()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var failed = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(failed.Path, "attempt.txt", "a failed attempt", "a failed attempt");

        Assert.False((await trees.RemoveBranchAsync(root, "engine", failed.Branch)).Removed);
        var removed = await trees.RemoveBranchAsync(root, "engine", failed.Branch, force: true);

        Assert.True(removed.Removed, removed.Message);
        Assert.False(Directory.Exists(failed.Path));
        Assert.False(await BranchAsync(root, failed.Branch));
        Assert.Empty(trees.Grown.All());
        // Only Daoris's own session branches go through it: a branch of the person's is theirs.
        Assert.Contains("only Daoris's own", (await trees.RemoveBranchAsync(root, "engine", "main", force: true)).Message);
        Assert.True(await BranchAsync(root, "main"));
    }

    /// <summary>
    /// LAND3c: the discard both doors call keeps an attempt's branch whose real tree a session still running holds, forced,
    /// tree and branch alike; once the ledger no longer names that tree, the same call takes both, as LAND3b's screen did.
    /// </summary>
    [Fact]
    public async Task The_shared_discard_keeps_a_branch_whose_tree_a_live_session_holds_until_it_is_let_go()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var attempt = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(attempt.Path, "attempt.txt", "an attempt", "an attempt");
        var ledger = new DiscardLedger { Registry = [("engine", root)], Live = [("s1", "working", attempt.Path)] };
        var discard = new SessionBranchDiscard(trees);

        var kept = await discard.DiscardAsync(ledger.Client(), "engine", attempt.Branch, force: true);

        Assert.False(kept.Removed);
        Assert.Equal(SessionBranchDiscard.Held("engine", attempt.Branch), kept.Message);
        Assert.True(Directory.Exists(attempt.Path));
        Assert.True(await BranchAsync(root, attempt.Branch));

        ledger.Live = [];
        var removed = await discard.DiscardAsync(ledger.Client(), "engine", attempt.Branch, force: true);

        Assert.True(removed.Removed, removed.Message);
        Assert.False(Directory.Exists(attempt.Path));
        Assert.False(await BranchAsync(root, attempt.Branch));
    }

    /// <summary>A landing takes no other branch: the earlier step's branch stays, as the pressed session's does.</summary>
    [Fact]
    public async Task A_landing_takes_no_other_branch()
    {
        var root = await RepositoryAsync("engine");
        var gate = GraphLand.Gate(_home, GraphLand.Branch("feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "first.txt", "first", "first");
        var second = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(second.Path, "second.txt", "second", "second");

        var landed = await trees.LandAsync(second.Path, new LandingSubject("s2", "0fda18", "Fix"), inUse: NoneRunning, gate: gate);

        Assert.True(landed.Landed, landed.Message);
        Assert.True(await BranchAsync(root, first.Branch));
    }

    /// <summary>
    /// TREEFOLLOW1 (D164 point 4): a tree its agent moved onto a branch of its own (<c>feature/…</c>, as its repository's rules ask)
    /// is landed as that branch stands, the one the pull request is from: Accept makes no second branch and says it lands the
    /// session's own, which holds the only copy of the commits.
    /// </summary>
    [Fact]
    public async Task Accept_lands_the_branch_the_agent_made_as_it_is_and_makes_no_other()
    {
        var root = await RepositoryAsync("engine");
        var gate = GraphLand.Gate(_home, GraphLand.Branch("feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await GitAsync(tree.Path, "checkout", "--quiet", "-b", "feature/TK-9-gap");
        await CommitAsync(tree.Path, "work.txt", "the work", "the work");

        var landed = await trees.LandAsync(tree.Path, new LandingSubject("s1", "0fda18", "Fix the gap"), inUse: NoneRunning, gate: gate);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal("feature/TK-9-gap", landed.Branch);
        Assert.Contains("`feature/TK-9-gap`, the session's own branch", landed.Message);
        Assert.False(await BranchAsync(root, "feature/0fda18-fix-the-gap"), "no second branch is made");
        Assert.True(await BranchAsync(root, "feature/TK-9-gap"), "the session's own branch stands: it holds the only copy");
        Assert.Equal("1", (await GitAsync(root, "rev-list", "--count", "main..feature/TK-9-gap")).Trim());
    }

    /// <summary>TREEFOLLOW1: a merge merges the tree's own branch as it would the session branch.</summary>
    [Fact]
    public async Task A_merge_merges_the_branch_the_agent_made()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await GitAsync(tree.Path, "checkout", "--quiet", "-b", "feature/TK-9-gap");
        await CommitAsync(tree.Path, "work.txt", "the work", "the work");

        var landed = await trees.LandAsync(tree.Path, new LandingSubject("s1", "0fda18", "Fix the gap"), inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        Assert.Contains("merged `feature/TK-9-gap` into `main`", landed.Message);
        Assert.Equal("0", (await GitAsync(root, "rev-list", "--count", "main..feature/TK-9-gap")).Trim());
        Assert.True(File.Exists(Path.Combine(root, "work.txt")));
    }

    private static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    /// <summary>A copy of the run's plain seed (<see cref="GitSeed.PlainAsync"/>; PROCSPEED1, PROCSPEED1b): no test here reads the first commit's id or time.</summary>
    private Task<string> RepositoryAsync(string name) => GitSeed.PlainAsync(Path.Combine(_scratch, name), name);

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
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
        using var process = Process.Start(TestGit.Past(info))!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return stdout;
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot() => GitSeed.RepoRoot();
}
