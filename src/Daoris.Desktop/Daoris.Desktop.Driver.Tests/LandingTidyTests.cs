using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAND3 (D102's note): a landing's tidy takes every session branch its work holds, not the pressed session's alone. On the
/// owner's install a chain's first session branch, whose commit rode into the session accepted after it, and a failed
/// attempt's branch stayed, and <c>session-branches.json</c> still listed branches already gone. Each branch recorded for
/// the repository whose tip the landed ref contains goes, with its tree where it has one and nothing uncommitted, never one
/// a running session holds; what went is said in the landing's sentence, which its note keeps; and the record drops what is
/// gone. A failed attempt's branch is no part of the landing and waits for its own door, which keeps one a live session's
/// tree holds (LAND3c). Real git, as the other tree tests.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class LandingTidyTests : IDisposable
{
    private static readonly Func<CancellationToken, Task<IReadOnlySet<string>>> NoneRunning =
        _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    private readonly string _scratch;
    private readonly string _home;

    public LandingTidyTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "landing-tidy", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle */ }
        catch (UnauthorizedAccessException) { /* read-only pack files under .git */ }
    }

    /// <summary>
    /// TK-2203's shape: the first session's commit rode into the second, grown from its branch (CHAIN2), and the second was
    /// accepted. Both branches and both trees go, the landing says the first went, and the record holds neither.
    /// </summary>
    [Fact]
    public async Task A_chains_earlier_session_branch_the_landing_holds_goes_with_its_tree()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true));
        var trees = new SessionTrees(_home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "first.txt", "the first step", "the first step");
        var second = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(second.Path, "second.txt", "the verify step", "the verify step");

        var landed = await trees.LandAsync(second.Path, new LandingSubject("s2", "0fda18", "Fix the gap"), inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        Assert.False(await BranchAsync(root, second.Branch), "the pressed session's branch goes, as it always did");
        Assert.False(await BranchAsync(root, first.Branch), "the earlier step's branch the landing holds goes too");
        Assert.False(Directory.Exists(first.Path), "with its tree");
        Assert.Contains($"`{first.Branch}` (with its tree)", landed.Message);
        Assert.Contains("`feature/0fda18-fix-the-gap` holds", landed.Message);
        var tidied = Assert.Single(landed.Tidied!);
        Assert.Equal(new TidiedBranch(first.Branch, Removed: true, Tree: true), tidied);
        Assert.Empty(trees.Grown.All());
        Assert.True(await BranchAsync(root, "feature/0fda18-fix-the-gap"), "the landed branch stands");
    }

    /// <summary>
    /// A failed attempt's branch holds commits the landing does not, so the tidy leaves it and says nothing of it; its door is
    /// `trees remove`, which refuses unlanded work until the person says `--force`, and then forgets it in the record.
    /// </summary>
    [Fact]
    public async Task A_failed_attempts_branch_is_kept_until_its_own_door_removes_it()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true));
        var trees = new SessionTrees(_home);
        var failed = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(failed.Path, "attempt.txt", "a failed attempt", "a failed attempt");
        var done = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(done.Path, "work.txt", "the work", "the work");

        var landed = await trees.LandAsync(done.Path, new LandingSubject("s2", "0fda18", "Fix the gap"), inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        Assert.True(await BranchAsync(root, failed.Branch), "a branch whose commits the landing does not hold stays");
        Assert.True(Directory.Exists(failed.Path));
        Assert.Empty(landed.Tidied!);
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

    /// <summary>
    /// A branch the landing holds whose tree a running session holds stays, said; so does one whose tree has uncommitted work,
    /// and one with a tree when nobody asked which sessions run. A branch with no tree goes whatever was asked.
    /// </summary>
    [Fact]
    public async Task A_branch_a_live_session_holds_or_one_with_work_in_flight_is_kept_and_named()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true));
        var trees = new SessionTrees(_home);
        var live = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(live.Path, "live.txt", "a live step", "a live step");
        var dirty = await trees.OpenAsync(root, "engine", "aurora", from: live.Branch);
        await CommitAsync(dirty.Path, "dirty.txt", "a dirty step", "a dirty step");
        await File.WriteAllTextAsync(Path.Combine(dirty.Path, "loose.txt"), "not committed\n");
        var pressed = await trees.OpenAsync(root, "engine", "aurora", from: dirty.Branch);
        await CommitAsync(pressed.Path, "work.txt", "the work", "the work");

        var landed = await trees.LandAsync(pressed.Path, new LandingSubject("s3", "0fda18", "Fix"),
            inUse: _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>([live.Path])));

        Assert.True(landed.Landed, landed.Message);
        Assert.True(await BranchAsync(root, live.Branch));
        Assert.True(Directory.Exists(live.Path));
        Assert.True(await BranchAsync(root, dirty.Branch));
        Assert.Contains($"Kept `{live.Branch}`", landed.Message);
        Assert.Contains("a session still running or waiting holds its tree", landed.Message);
        Assert.Contains("1 path(s) uncommitted", landed.Message);
        Assert.Equal(2, landed.Tidied!.Count(each => !each.Removed));
        // The record keeps the branches that stand.
        Assert.Equal(new[] { live.Branch, dirty.Branch }.Order(StringComparer.Ordinal),
            trees.Grown.All().Select(entry => entry.Branch).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_tree_is_kept_when_nobody_asked_which_sessions_run_and_a_treeless_branch_goes()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true));
        var trees = new SessionTrees(_home);
        var kept = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(kept.Path, "kept.txt", "kept", "kept");
        var treeless = await trees.OpenAsync(root, "engine", "aurora", from: kept.Branch);
        await CommitAsync(treeless.Path, "treeless.txt", "treeless", "treeless");
        await GitAsync(root, "worktree", "remove", treeless.Path);
        var pressed = await trees.OpenAsync(root, "engine", "aurora", from: treeless.Branch);
        await CommitAsync(pressed.Path, "work.txt", "the work", "the work");

        var landed = await trees.LandAsync(pressed.Path, new LandingSubject("s3", "0fda18", "Fix"));

        Assert.True(landed.Landed, landed.Message);
        Assert.True(await BranchAsync(root, kept.Branch), "a tree nobody asked about is kept");
        Assert.Contains("whether a session still holds its tree was not asked", landed.Message);
        Assert.False(await BranchAsync(root, treeless.Branch), "a branch with no tree holds no session");
        Assert.Contains(new TidiedBranch(treeless.Branch, Removed: true, Tree: false), landed.Tidied!);
    }

    /// <summary>
    /// The record drops what is gone: an entry whose branch someone deleted is forgotten at the landing's tidy, and another
    /// repository's entries are untouched, since this landing cannot see that repository's branches.
    /// </summary>
    [Fact]
    public async Task The_landing_prunes_the_record_of_branches_already_gone()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true));
        var trees = new SessionTrees(_home);
        var gone = await trees.OpenAsync(root, "engine", "aurora");
        await GitAsync(root, "worktree", "remove", gone.Path);
        await GitAsync(root, "branch", "-D", gone.Branch);
        trees.Grown.Record(new GrownBranch("game", "aurora", "daoris/s-elsewhere", "main", "abc123", null, DateTimeOffset.UtcNow));
        var pressed = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(pressed.Path, "work.txt", "the work", "the work");

        var landed = await trees.LandAsync(pressed.Path, new LandingSubject("s3", "0fda18", "Fix"), inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        var left = Assert.Single(trees.Grown.All());
        Assert.Equal(("game", "daoris/s-elsewhere"), (left.Repository, left.Branch));
    }

    /// <summary>Without the rule's tidy nothing else goes: the earlier step's branch stays, as the pressed session's does.</summary>
    [Fact]
    public async Task Without_the_tidy_no_other_branch_goes()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        var trees = new SessionTrees(_home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "first.txt", "first", "first");
        var second = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(second.Path, "second.txt", "second", "second");

        var landed = await trees.LandAsync(second.Path, new LandingSubject("s2", "0fda18", "Fix"), inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        Assert.True(await BranchAsync(root, first.Branch));
        Assert.Null(landed.Tidied);
    }

    /// <summary>A merge's tidy holds the same: the line the work was merged into contains the earlier step, which goes.</summary>
    [Fact]
    public async Task A_merges_tidy_takes_the_earlier_step_the_line_now_holds()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Merge, Tidy: true));
        var trees = new SessionTrees(_home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "first.txt", "first", "first");
        var second = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(second.Path, "second.txt", "second", "second");

        var landed = await trees.LandAsync(second.Path, new LandingSubject("s2", "0fda18", "Fix"), inUse: NoneRunning);

        Assert.True(landed.Landed, landed.Message);
        Assert.False(await BranchAsync(root, first.Branch));
        Assert.Contains("`main` holds", landed.Message);
    }

    private void Rule(LandingRule rule) =>
        DriverConfig.Empty.WithLanding("engine", rule).Save(Path.Combine(_home, "driver.json"));

    private static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    private async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await CommitAsync(root, "README.md", $"# {name}", "first");
        return root;
    }

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
        using var process = Process.Start(info)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return stdout;
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git"))
               && !File.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
