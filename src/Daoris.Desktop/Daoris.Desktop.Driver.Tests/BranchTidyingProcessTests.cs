using System.Text.Json;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTOTIDY1 (D88's note) over real git: the look removes a session branch that holds nothing beyond the line, with its tree,
/// by the clean-up's own path, and keeps every other branch exactly as the press finds it. The sessions are the stand-in
/// ledger's, read through the real client.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class BranchTidyingProcessTests : IDisposable
{
    // A look judges each branch the line holds with several git calls, twice: a dozen took over a minute while other worktrees
    // built (FLAKE1), and 22 seconds alone.
    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(4);

    private readonly string _scratch;
    private readonly string _home;
    private readonly StandInLedger _ledger = new();
    private readonly CancellationTokenSource _closing = new(TimeSpan.FromMinutes(10));
    private DateTimeOffset _now = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    public BranchTidyingProcessTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "autotidy", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        // Git leaves read-only objects and tree links on Windows, as GitTree's cleanup says; a failed cleanup is not a failed test.
        try
        {
            // The links a test made go first, alone, so the delete below never meets one.
            var walk = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var link in Directory.EnumerateDirectories(_scratch, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 })
                         .Where(folder => File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint)).ToList())
            {
                Directory.Delete(link);
            }

            foreach (var file in Directory.EnumerateFiles(_scratch, "*", walk))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle */ }
        catch (UnauthorizedAccessException) { /* a file something still holds */ }
    }

    /// <summary>
    /// 🔴 The row's own case: a session branch whose work a merge took onto the line goes with its tree, and a branch whose tree
    /// was removed by hand goes too, each said once in the report and the machine log. Every other branch stays, as the press
    /// finds it, and says nothing: commits beyond the line, on a branch of the person's, or on the line by content after a
    /// squash; a tree whose branch never moved, which words to its session go on in; and, each on a branch the line took, an
    /// uncommitted change, an untracked file, an ignored file only the tree holds, a build's output the checkout holds too, a
    /// session working in its tree, and one waiting on the person.
    /// </summary>
    [Fact]
    public async Task The_look_removes_an_empty_branch_and_keeps_every_other()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);

        var merged = await LandedTreeAsync(trees, root);
        await GitAsync(root, "branch", "daoris/s-treeless");

        var beyond = await TreeWithWorkAsync(trees, root);
        var featured = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "branch", "feature/x", featured.Branch);
        var squashed = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "merge", "--squash", squashed.Branch);
        await GitAsync(root, "commit", "--quiet", "-m", "the work, squashed");
        var unmoved = await trees.OpenAsync(root, "engine", "default");
        var changed = await LandedTreeAsync(trees, root);
        await File.WriteAllTextAsync(Path.Combine(changed.Path, "README.md"), "# engine, edited\n");
        var untracked = await LandedTreeAsync(trees, root);
        await File.WriteAllTextAsync(Path.Combine(untracked.Path, "loose.txt"), "not committed\n");
        var ignored = await LandedTreeAsync(trees, root);
        await File.WriteAllTextAsync(Path.Combine(ignored.Path, "local.db"), "made here alone\n");
        var built = await LandedTreeAsync(trees, root);
        Directory.CreateDirectory(Path.Combine(built.Path, "build"));
        await File.WriteAllTextAsync(Path.Combine(built.Path, "build", "out.txt"), "a build's output\n");
        var running = await LandedTreeAsync(trees, root);
        var waiting = await LandedTreeAsync(trees, root);

        _ledger.Register("engine", root);
        _ledger.Working("s-running", "engine", running.Path);
        _ledger.Publish("q1", "engine");
        _ledger.Park("s-waiting", "q1", waiting.Path);
        using var log = Log();
        var tidying = new BranchTidying(_home);

        var said = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.DoesNotContain(merged.Branch, branches, StringComparison.Ordinal);
        Assert.DoesNotContain("daoris/s-treeless", branches, StringComparison.Ordinal);
        Assert.False(Directory.Exists(merged.Path));
        foreach (var kept in new[] { beyond, featured, squashed, unmoved, changed, untracked, ignored, built, running, waiting })
        {
            Assert.Contains(kept.Branch, branches, StringComparison.Ordinal);
            Assert.True(Directory.Exists(kept.Path), kept.Path);
        }

        Assert.Equal(2, said.Count);
        Assert.Contains(said, line => line.StartsWith(Removed(merged), StringComparison.Ordinal));
        Assert.Contains("tidy  engine: removed `daoris/s-treeless`, which held nothing beyond `main`.", said);
        // Its folder was moved aside, never deleted: what it held is all there.
        var aside = Assert.Single(Directory.GetDirectories(Path.Combine(_home, "trees", ".tidied", "default", "engine")));
        Assert.StartsWith(Path.GetFileName(merged.Path) + "-", Path.GetFileName(aside), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(aside, "README.md")));
        Assert.DoesNotContain(merged.Path.Replace('\\', '/'), (await GitAsync(root, "worktree", "list", "--porcelain")).Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, Events("branch.tidied").Length);
        Assert.Empty(Events("branch.kept"));
        // The record of where session branches grew from forgets it, as the press's does.
        Assert.DoesNotContain(trees.Grown.All(), entry => entry.Branch == merged.Branch);
        Assert.Contains(trees.Grown.All(), entry => entry.Branch == unmoved.Branch);
    }

    /// <summary>
    /// What git cannot read, and a removal git refuses (a locked tree), keep the branch, and the look says why once: the next
    /// look meets the same and says nothing, in the report or the log.
    /// </summary>
    [Fact]
    public async Task A_branch_git_cannot_read_or_remove_stays_and_is_said_once()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var unreadable = await trees.OpenAsync(root, "engine", "default");
        // Its link to the repository broken: git cannot say what the tree holds. git leaves the link read-only.
        var link = Path.Combine(unreadable.Path, ".git");
        File.SetAttributes(link, FileAttributes.Normal);
        await File.WriteAllTextAsync(link, $"gitdir: {Path.Combine(_scratch, "nowhere")}\n");
        var locked = await LandedTreeAsync(trees, root);
        await GitAsync(root, "worktree", "lock", "--reason", "a person's", locked.Path);
        _ledger.Register("engine", root);
        using var log = Log();
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };

        var first = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);
        var second = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.Equal(2, first.Count);
        Assert.Contains($"tidy  engine: `{unreadable.Branch}` stays, since git could not say what its tree holds.", first);
        Assert.Contains($"tidy  engine: `{locked.Branch}` stays, since its tree is locked (a person's); `git worktree unlock` lets it go.", first);
        Assert.Empty(second);
        var kept = Events("branch.kept");
        Assert.Equal(2, kept.Length);
        Assert.Contains(kept, data => data.GetProperty("branch").GetString() == unreadable.Branch && data.GetProperty("why").GetString() == TidyKept.Unread);
        Assert.Contains(kept, data => data.GetProperty("branch").GetString() == locked.Branch && data.GetProperty("why").GetString() == TidyKept.Refused);
        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.Contains(unreadable.Branch, branches, StringComparison.Ordinal);
        Assert.Contains(locked.Branch, branches, StringComparison.Ordinal);
        Assert.True(Directory.Exists(locked.Path));
    }

    /// <summary>
    /// 🔴 A tree a start is choosing, a fresh one or one it resumes in, is held from that choice until its record opens: while a
    /// start holds the repository's trees, the look leaves every branch for another look, says so once, and takes what it may
    /// at the next look once nothing does.
    /// </summary>
    [Fact]
    public async Task A_start_holding_the_repository_leaves_its_branches_for_another_look()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        _ledger.Register("engine", root);
        using var log = Log();
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };

        IReadOnlyList<string> held;
        using (TreeLock.TryStarting(_home, "default", "engine"))
        {
            held = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);
        }

        Assert.Contains(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.Equal(["tidy  engine: a session was starting in one of `engine`'s trees, so its branches were left as they were for another look."], held);

        var next = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith(Removed(landed), Assert.Single(next), StringComparison.Ordinal);
        Assert.False(Directory.Exists(landed.Path));
    }

    /// <summary>
    /// A review step in progress (REVIEWENV1d) keeps the tree it shows from, as a session in use keeps its own: a tree the line
    /// took whose build is shown stays, and goes at the look after nothing shows from it.
    /// </summary>
    [Fact]
    public async Task A_tree_a_review_shows_from_stays_while_it_is_shown()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        _ledger.Register("engine", root);
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };
        IReadOnlyCollection<string> shown = [Path.Combine(landed.Path, "app", "dist")];

        var held = await tidying.LookAsync(_ledger.Client(), null, _closing.Token, _ => Task.FromResult(shown)).WaitAsync(Bound);

        Assert.Empty(held);
        Assert.Contains(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.True(Directory.Exists(landed.Path));

        shown = [];
        var next = await tidying.LookAsync(_ledger.Client(), null, _closing.Token, _ => Task.FromResult(shown)).WaitAsync(Bound);

        Assert.StartsWith(Removed(landed), Assert.Single(next), StringComparison.Ordinal);
    }

    /// <summary>
    /// A registered root that is a folder inside a repository is never looked into: git walks up from it, and would answer for
    /// the repository above (FIX-LOG), whose own empty session branch stays.
    /// </summary>
    [Fact]
    public async Task A_root_that_is_not_a_repository_of_its_own_is_never_looked_into()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "daoris/s-above");
        var inside = Path.Combine(root, "inside");
        Directory.CreateDirectory(inside);
        _ledger.Register("inside", inside);

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.Empty(said);
        Assert.Contains("daoris/s-above", await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
    }

    /// <summary>The watch's look runs the tidy beside it, and a later look's report says what went.</summary>
    [Fact]
    public async Task The_watch_s_look_removes_an_empty_branch_and_says_so_in_a_report()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        _ledger.Register("engine", root);
        var config = Path.Combine(_home, "driver.json");
        await File.WriteAllTextAsync(config, """{ "drivable": [], "pollSeconds": 1 }""");
        using var log = Log();
        var watch = new DriverWatch(
            _ledger.Client(), config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")))
        {
            Log = log,
            Tidying = new BranchTidying(_home) { Pace = TimeSpan.FromMinutes(5) },
        };
        var said = new List<string>();
        var watching = watch.RunAsync(
            (report, _) =>
            {
                lock (said) said.AddRange(report.Events.Where(line => line.StartsWith("tidy", StringComparison.Ordinal)));
                return Task.CompletedTask;
            },
            onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => { lock (said) return said.Count > 0; }, () => "no look said the tidy", Bound);

            lock (said) Assert.StartsWith(Removed(landed), Assert.Single(said), StringComparison.Ordinal);
            Assert.DoesNotContain(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
            Assert.Single(Events("branch.tidied"));
        }
        finally
        {
            await _closing.CancelAsync();
            try { await watching.WaitAsync(Bound); } catch (OperationCanceledException) { }
        }
    }

    /// <summary>
    /// A tracked file marked <c>assume-unchanged</c> or <c>skip-worktree</c> and then edited reads as clean to git's status: the
    /// look keeps such a tree for the press, saying why, and the edit stays where it was.
    /// </summary>
    [Fact]
    public async Task A_tree_whose_index_hides_a_change_stays_for_the_press()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var assumed = await LandedTreeAsync(trees, root);
        await GitAsync(assumed.Path, "update-index", "--assume-unchanged", "README.md");
        await File.WriteAllTextAsync(Path.Combine(assumed.Path, "README.md"), "# engine, edited where git does not look\n");
        var skipped = await LandedTreeAsync(trees, root);
        await GitAsync(skipped.Path, "update-index", "--skip-worktree", "README.md");
        await File.WriteAllTextAsync(Path.Combine(skipped.Path, "README.md"), "# engine, edited where git does not look\n");
        _ledger.Register("engine", root);
        using var log = Log();

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        foreach (var kept in new[] { assumed, skipped })
        {
            Assert.Contains(kept.Branch, branches, StringComparison.Ordinal);
            Assert.Equal("# engine, edited where git does not look\n", await File.ReadAllTextAsync(Path.Combine(kept.Path, "README.md")));
            Assert.Contains(said, line => line.StartsWith($"tidy  engine: `{kept.Branch}` stays, since 1 tracked path(s) in its tree are marked", StringComparison.Ordinal));
        }

        Assert.Equal(2, Events("branch.kept").Count(data => data.GetProperty("why").GetString() == TidyKept.Hidden));
    }

    /// <summary>
    /// A repository nested in the tree, marked by a <c>.git</c> file inside a tracked folder, is invisible to the tree's own
    /// status: the look finds it by reading the tree's folders itself, and keeps the tree for the press.
    /// </summary>
    [Fact]
    public async Task A_repository_nested_in_a_tree_keeps_it_for_the_press()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var nested = await trees.OpenAsync(root, "engine", "default");
        Directory.CreateDirectory(Path.Combine(nested.Path, "docs"));
        await CommitAsync(nested.Path, Path.Combine("docs", "guide.md"), "the session's guide\n", "the guide");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", nested.Branch);
        Directory.CreateDirectory(Path.Combine(nested.Path, "docs", "inner"));
        await File.WriteAllTextAsync(Path.Combine(nested.Path, "docs", "inner", ".git"), $"gitdir: {Path.Combine(_scratch, "elsewhere.git")}\n");
        Assert.Empty((await GitAsync(nested.Path, "status", "--porcelain", "--untracked-files=all", "--ignored=matching")).Trim());
        _ledger.Register("engine", root);
        using var log = Log();

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.Contains(nested.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(nested.Path, "docs", "inner", ".git")));
        Assert.Equal([$"tidy  engine: `{nested.Branch}` stays, since its tree holds another repository at docs/inner."], said);
        Assert.Equal(TidyKept.Nested, Assert.Single(Events("branch.kept")).GetProperty("why").GetString());
    }

    /// <summary>
    /// The line is read by its full name: a tag named as the line, which git reads before the branch, never stands for it. A
    /// session branch a person's feature branch took, which such a tag holds, is landed elsewhere and stays the press's.
    /// </summary>
    [Fact]
    public async Task A_tag_named_as_the_line_never_stands_for_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var featured = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "branch", "feature/x", featured.Branch);
        await GitAsync(root, "tag", "main", "feature/x");
        _ledger.Register("engine", root);

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.Empty(said);
        Assert.Contains(featured.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.True(Directory.Exists(featured.Path));
        var plan = await trees.SweepPlanAsync([("engine", "default", root)], new HashSet<string>());
        Assert.Equal(SweepKind.Landed, plan.Single(item => item.Branch == featured.Branch).Kind);
    }

    /// <summary>
    /// A tree reached through a link under the trees home is somewhere else: here its repository's folder was moved away after
    /// the tree was opened, and a link left in its place, so git still lists the tree under the trees home. The look keeps it,
    /// saying so, and its folder stays where the link points.
    /// </summary>
    [Fact]
    public async Task A_tree_reached_through_a_link_under_the_trees_home_stays()
    {
        var root = await RepositoryAsync("engine");
        var linked = await LandedTreeAsync(new SessionTrees(_home), root);
        var elsewhere = Path.Combine(_scratch, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        Directory.Move(Path.Combine(_home, "trees", "default", "engine"), Path.Combine(elsewhere, "engine"));
        await JunctionAsync(Path.Combine(_home, "trees", "default", "engine"), Path.Combine(elsewhere, "engine"));
        _ledger.Register("engine", root);

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.Contains(linked.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.True(Directory.Exists(Path.Combine(elsewhere, "engine", Path.GetFileName(linked.Path))));
        Assert.Equal([$"tidy  engine: `{linked.Branch}` stays, since its tree is reached through a link under the trees home."], said);
    }

    /// <summary>
    /// A session names its tree by the path its start opened, through the link, where git lists where the tree really is: it
    /// holds the tree all the same, since the busy match compares resolved paths, at the press's list and the look's alike.
    /// </summary>
    [Fact]
    public async Task A_session_naming_its_tree_through_a_link_holds_it()
    {
        var root = await RepositoryAsync("engine");
        var elsewhere = Path.Combine(_scratch, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        await JunctionAsync(Path.Combine(_home, "trees", "default", "engine"), elsewhere);
        var trees = new SessionTrees(_home);
        var linked = await LandedTreeAsync(trees, root);

        var plan = await trees.SweepPlanAsync([("engine", "default", root)], new HashSet<string> { linked.Path });

        Assert.Equal(SweepKind.InUse, plan.Single(item => item.Branch == linked.Branch).Kind);
    }

    /// <summary>
    /// 🔴 The look never deletes a tree's folder: it moves it aside whole, so a write that lands after the last look is kept.
    /// Here everything the tree held is in the folder moved aside, and a file written there afterwards stays with it.
    /// </summary>
    [Fact]
    public async Task A_tree_moved_aside_keeps_everything_and_what_is_written_after()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        var held = Directory.GetFiles(landed.Path, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(landed.Path, file)).ToList();
        _ledger.Register("engine", root);

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        var aside = Assert.Single(Directory.GetDirectories(Path.Combine(_home, "trees", ".tidied", "default", "engine")));
        Assert.Equal([$"{Removed(landed)}{aside}, and goes once it is left untouched for 14 days."], said);
        Assert.False(Directory.Exists(landed.Path));
        Assert.All(held, file => Assert.True(File.Exists(Path.Combine(aside, file)), file));
        await File.WriteAllTextAsync(Path.Combine(aside, "late.db"), "a write that came after the last look\n");
        Assert.True(File.Exists(Path.Combine(aside, "late.db")));
        Assert.DoesNotContain(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 On Windows a folder whose files something holds open cannot be renamed: the tree is in use, so the branch, its tree and
    /// git's record of it all stay, the look says so once, and a later look takes it once nothing holds it. Elsewhere the rename
    /// goes, and what the holder reads or writes is in the folder moved aside.
    /// </summary>
    [Fact]
    public async Task A_tree_something_holds_open_stays_until_nothing_does()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        _ledger.Register("engine", root);
        using var log = Log();
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };

        IReadOnlyList<string> said;
        IReadOnlyList<string> again;
        using (new FileStream(Path.Combine(landed.Path, "README.md"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            said = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);
            again = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);
        }

        if (!OperatingSystem.IsWindows())
        {
            Assert.StartsWith(Removed(landed), Assert.Single(said), StringComparison.Ordinal);
            return;
        }

        Assert.StartsWith(
            $"tidy  engine: `{landed.Branch}` stays, since something on this machine is using its tree, so its folder could not be moved aside",
            Assert.Single(said), StringComparison.Ordinal);
        Assert.Empty(again);
        Assert.Contains(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(landed.Path, "README.md")));
        Assert.Contains(Path.GetFileName(landed.Path), await GitAsync(root, "worktree", "list", "--porcelain"), StringComparison.Ordinal);
        Assert.Equal(TidyKept.Busy, Assert.Single(Events("branch.kept")).GetProperty("why").GetString());

        var free = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith(Removed(landed), Assert.Single(free), StringComparison.Ordinal);
    }

    /// <summary>
    /// A folder moved aside goes once it has waited fourteen days with nothing written in it since its move; one written in after
    /// its move stays, said once, for the person to look at; and none goes before its wait.
    /// </summary>
    [Fact]
    public async Task The_purge_deletes_a_folder_left_untouched_and_keeps_one_written_after_its_move()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var untouched = await LandedTreeAsync(trees, root);
        var touched = await LandedTreeAsync(trees, root);
        _ledger.Register("engine", root);
        await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);
        var asides = Directory.GetDirectories(Path.Combine(_home, "trees", ".tidied", "default", "engine"));
        var quiet = asides.Single(folder => Path.GetFileName(folder).StartsWith(Path.GetFileName(untouched.Path) + "-", StringComparison.Ordinal));
        var written = asides.Single(folder => Path.GetFileName(folder).StartsWith(Path.GetFileName(touched.Path) + "-", StringComparison.Ordinal));
        await File.WriteAllTextAsync(Path.Combine(written, "late.db"), "a write that came after the move\n");
        using var log = Log();

        var early = await new BranchTidying(_home) { Clock = () => DateTimeOffset.UtcNow.AddDays(13) }
            .LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);
        var later = new BranchTidying(_home) { Pace = TimeSpan.Zero, Clock = () => DateTimeOffset.UtcNow.AddDays(15) };
        var said = await later.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);
        var again = await later.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.Empty(early);
        Assert.Equal(2, said.Count);
        Assert.Contains(said, line => line.StartsWith($"tidy  deleted the folder moved aside to {quiet} on ", StringComparison.Ordinal));
        Assert.Contains(said, line => line.StartsWith($"tidy  the folder moved aside to {written} on ", StringComparison.Ordinal)
                                      && line.Contains("stays, since something in it was written after it was moved aside: late.db.", StringComparison.Ordinal));
        Assert.Empty(again);
        Assert.False(Directory.Exists(quiet));
        Assert.True(File.Exists(Path.Combine(written, "late.db")));
        Assert.Single(Events("tidied.purged"));
        Assert.Equal(SetAsideKept.Written, Assert.Single(Events("tidied.kept")).GetProperty("why").GetString());
    }

    /// <summary>
    /// 🔴 A look closed in the middle of a removal finishes it before it lets go of the repository: the token is cancelled as the
    /// branch is about to be deleted, the repository is still held then, and the delete still runs to its end.
    /// </summary>
    [Fact]
    public async Task A_look_closed_mid_removal_finishes_it_before_letting_go()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        using var closing = new CancellationTokenSource();
        bool? heldThen = null;
        var trees = new SessionTrees(_home)
        {
            BeforeDeleting = async _ =>
            {
                await closing.CancelAsync();
                using var starting = TreeLock.TryStarting(_home, "default", "engine");
                heldThen = starting is null;
            },
        };

        var pass = await trees.TidyEmptyAsync(root, "engine", "default", _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>()), closing.Token)
            .WaitAsync(Bound);

        Assert.True(heldThen);
        Assert.True(Assert.Single(pass.Results).Removed);
        Assert.DoesNotContain(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFileName(landed.Path), await GitAsync(root, "worktree", "list", "--porcelain"), StringComparison.Ordinal);
    }

    /// <summary>
    /// A list of the working trees git could not give is never "no tree here": the look judges nothing in the repository and
    /// says why, the press's list says each branch is unread, and the press removes nothing.
    /// </summary>
    [Fact]
    public async Task A_worktree_list_git_could_not_give_keeps_every_branch()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        await GitAsync(root, "branch", "daoris/s-treeless");
        var trees = new SessionTrees(_home) { ListFails = _ => true };
        var nobody = new HashSet<string>();

        var pass = await trees.TidyEmptyAsync(root, "engine", "default", _ => Task.FromResult<IReadOnlySet<string>>(nobody), _closing.Token)
            .WaitAsync(Bound);
        var plan = await trees.SweepPlanAsync([("engine", "default", root)], nobody);
        var swept = await trees.SweepAsync([("engine", "default", root)], nobody);

        Assert.Equal(TidyKept.Unread, pass.Held);
        Assert.Empty(pass.Results);
        Assert.Equal(2, plan.Count);
        Assert.All(plan, item => Assert.True(item.Unread, item.Branch));
        Assert.DoesNotContain(swept, result => result.Removed);
        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.Contains(landed.Branch, branches, StringComparison.Ordinal);
        Assert.Contains("daoris/s-treeless", branches, StringComparison.Ordinal);
        Assert.True(Directory.Exists(landed.Path));
    }

    /// <summary>A link at <paramref name="link"/> to <paramref name="target"/>: a junction on Windows, which needs no privilege.</summary>
    private static async Task JunctionAsync(string link, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        var info = new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"mklink failed: {await output} {await error}");
    }

    /// <summary>How a removal by the look opens: the folder moved aside follows, named for the moment it moved.</summary>
    private static string Removed(TreeOpened tree) =>
        $"tidy  engine: removed `{tree.Branch}`, which held nothing beyond `main`; its tree's folder was moved aside to ";

    private MachineLog Log() => new(_home, "driver", () => _now);

    /// <summary>The data of every line of <paramref name="event"/> the machine log holds.</summary>
    private JsonElement[] Events(string @event)
    {
        var folder = Path.Combine(_home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.EnumerateFiles(folder, "*.jsonl").SelectMany(StubFile.Lines)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone())
                .Where(line => line.GetProperty("event").GetString() == @event)
                .Select(line => line.GetProperty("data")),
        ];
    }

    private async Task<TreeOpened> TreeWithWorkAsync(SessionTrees trees, string root)
    {
        var tree = await trees.OpenAsync(root, "engine", "default");
        await CommitAsync(tree.Path, $"{Guid.NewGuid():N}.txt", "the session's work", "the work");
        return tree;
    }

    /// <summary>A session tree whose work a merge took onto the line, as a pull request completed with a merge commit leaves one.</summary>
    private async Task<TreeOpened> LandedTreeAsync(SessionTrees trees, string root)
    {
        var tree = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "merge", "--no-ff", "--no-edit", tree.Branch);
        return tree;
    }

    /// <summary>A repository on <c>main</c> that ignores a database and a build's output, and holds a build's output itself.</summary>
    private async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "*.db\nbuild/\n");
        await CommitAsync(root, "README.md", $"# {name}\n", "first");
        Directory.CreateDirectory(Path.Combine(root, "build"));
        await File.WriteAllTextAsync(Path.Combine(root, "build", "out.txt"), "the checkout's own build\n");
        return root;
    }

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content);
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--quiet", "-m", message);
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
