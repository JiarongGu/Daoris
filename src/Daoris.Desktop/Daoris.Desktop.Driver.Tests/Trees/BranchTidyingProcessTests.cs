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
    /// 🔴 The row's own case, AUTOTIDY1's and AUTOTIDY2a's: every branch the press clears whose work is on the line goes by
    /// itself, each said once in the report and the machine log. A session branch whose work a merge took onto the line goes with
    /// its tree, and one whose tree was removed by hand; one on the line by content after a squash, its commits kept on a ref of
    /// their own; and, each on a branch the line took, a tree holding an ignored file only it holds and one holding a build's
    /// output the checkout holds too, each folder moved aside whole. Every other branch stays, as the press finds it, and says
    /// nothing: commits beyond the line; commits on a branch of the person's the line does not hold yet, whose session may still
    /// be asked to change them; a tree whose branch never moved, which words to its session go on in; and, each on a branch the
    /// line took, an uncommitted change, an untracked file, a session working in its tree, and one waiting on the person.
    /// </summary>
    [Fact]
    public async Task The_look_removes_every_branch_the_press_clears_and_keeps_every_other()
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
        Assert.DoesNotContain("daoris/s-treeless", branches, StringComparison.Ordinal);
        var gone = new[] { merged, squashed, ignored, built };
        foreach (var each in gone)
        {
            Assert.DoesNotContain(each.Branch, branches, StringComparison.Ordinal);
            Assert.False(Directory.Exists(each.Path), each.Path);
        }

        foreach (var kept in new[] { beyond, featured, unmoved, changed, untracked, running, waiting })
        {
            Assert.Contains(kept.Branch, branches, StringComparison.Ordinal);
            Assert.True(Directory.Exists(kept.Path), kept.Path);
        }

        Assert.Equal(5, said.Count);
        Assert.Contains("tidy  engine: removed `daoris/s-treeless`, which held nothing beyond `main`.", said);
        foreach (var each in new[] { merged, ignored, built })
        {
            Assert.Contains(said, line => line.StartsWith(Removed(each), StringComparison.Ordinal));
        }

        var discarded = (await GitAsync(root, "for-each-ref", "--format=%(refname)", "refs/daoris/discarded/")).Trim();
        Assert.Contains(squashed.Branch, discarded, StringComparison.Ordinal);
        Assert.Contains(said, line => line.StartsWith(
                $"tidy  engine: removed `{squashed.Branch}`, since its work is on `main` by content (a squash merge); its tree's folder was moved aside to ",
                StringComparison.Ordinal)
            && line.EndsWith($"Its commits stay at `{discarded}` until you delete that ref; `git branch {squashed.Branch} {discarded}` brings the branch back.",
                StringComparison.Ordinal));
        // Each folder was moved aside, never deleted: what it held is all there, an ignored file only it held and a build's output.
        var asides = Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees,".tidied", "default", "engine"));
        Assert.Equal(gone.Length, asides.Length);
        string Aside(TreeOpened tree) => asides.Single(folder => Path.GetFileName(folder).StartsWith(Path.GetFileName(tree.Path) + "-", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(Aside(merged), "README.md")));
        Assert.Equal("made here alone\n", await File.ReadAllTextAsync(Path.Combine(Aside(ignored), "local.db")));
        Assert.True(File.Exists(Path.Combine(Aside(built), "build", "out.txt")));
        var listed = (await GitAsync(root, "worktree", "list", "--porcelain")).Replace('\\', '/');
        Assert.All(gone, each => Assert.DoesNotContain(each.Path.Replace('\\', '/'), listed, StringComparison.OrdinalIgnoreCase));
        var tidied = Events("branch.tidied");
        Assert.Equal(5, tidied.Length);
        Assert.Equal(
            [SweepKind.Empty, SweepKind.Empty, SweepKind.Empty, SweepKind.Empty, SweepKind.Landed],
            tidied.Select(data => data.GetProperty("kind").GetString()).Order(StringComparer.Ordinal));
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
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        await File.WriteAllTextAsync(config, """{ "drivable": [], "pollSeconds": 1 }""");
        using var log = Log();
        var watch = new DriverWatch(
            _ledger.Client(), config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
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
    /// session branch a person's feature branch took, which such a tag holds, is landed elsewhere, not on the line, and stays
    /// the press's (AUTOTIDY2a's wait for the line).
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
        Directory.Move(DaorisHome.Entry(_home, HomeEntry.Trees,"default", "engine"), Path.Combine(elsewhere, "engine"));
        await JunctionAsync(DaorisHome.Entry(_home, HomeEntry.Trees,"default", "engine"), Path.Combine(elsewhere, "engine"));
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
        await JunctionAsync(DaorisHome.Entry(_home, HomeEntry.Trees,"default", "engine"), elsewhere);
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

        var aside = Assert.Single(Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees,".tidied", "default", "engine")));
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
        var asides = Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees,".tidied", "default", "engine"));
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
    /// 🔴 TIDIEDLINK1, D88's note: a folder moved aside whose build folder holds links, as npm's workspaces and pnpm leave under
    /// <c>node_modules</c>, is deleted after its wait with each link taken as a link: what one points at outside the folder stays,
    /// every file in it as it was, and one pointing inside goes with the folder. A link anywhere else keeps its folder, said as
    /// before, and what it points at is untouched.
    /// </summary>
    [Fact]
    public async Task The_purge_takes_a_link_under_a_build_folder_as_a_link_and_keeps_a_folder_with_one_elsewhere()
    {
        var engine = DaorisHome.Entry(_home, HomeEntry.Trees,".tidied", "default", "engine");
        var moved = DateTimeOffset.UtcNow.AddMinutes(1);
        var stamp = moved.ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var outside = Path.Combine(_scratch, "elsewhere", "web");
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "index.js"), "module.exports = 'the target';\n");
        File.SetAttributes(Path.Combine(outside, "index.js"), FileAttributes.ReadOnly);
        var built = Path.Combine(engine, $"s-0b0117a0-{stamp}");
        Directory.CreateDirectory(Path.Combine(built, "packages", "ui"));
        await File.WriteAllTextAsync(Path.Combine(built, "packages", "ui", "index.js"), "export {};\n");
        await File.WriteAllTextAsync(Path.Combine(built, "README.md"), "# engine\n");
        // A read-only file, as git's objects are: cleared before its delete, as before.
        File.SetAttributes(Path.Combine(built, "README.md"), FileAttributes.ReadOnly);
        await JunctionAsync(Path.Combine(built, "node_modules", "@fixture", "web"), outside);
        await JunctionAsync(Path.Combine(built, "node_modules", "ui"), Path.Combine(built, "packages", "ui"));
        var data = Path.Combine(_scratch, "elsewhere", "data");
        Directory.CreateDirectory(data);
        await File.WriteAllTextAsync(Path.Combine(data, "notes.md"), "the person's\n");
        var linked = Path.Combine(engine, $"s-0d47a000-{stamp}");
        Directory.CreateDirectory(linked);
        await JunctionAsync(Path.Combine(linked, "data"), data);

        var purged = new SessionTrees(_home).PurgeSetAside(moved.AddDays(15), BranchTidying.SetAsideFor);

        Assert.Equal(2, purged.Count);
        Assert.True(purged.Single(each => each.Folder == built).Purged, purged.Single(each => each.Folder == built).Kept);
        Assert.False(Directory.Exists(built));
        Assert.Equal("module.exports = 'the target';\n", await File.ReadAllTextAsync(Path.Combine(outside, "index.js")));
        Assert.True(File.GetAttributes(Path.Combine(outside, "index.js")).HasFlag(FileAttributes.ReadOnly));
        var kept = purged.Single(each => each.Folder == linked);
        Assert.False(kept.Purged);
        Assert.Equal(SetAsideKept.Linked, kept.Code);
        Assert.Equal("it holds a link, at data", kept.Kept);
        Assert.True(Directory.Exists(Path.Combine(linked, "data")));
        Assert.Equal("the person's\n", await File.ReadAllTextAsync(Path.Combine(data, "notes.md")));
        File.SetAttributes(Path.Combine(outside, "index.js"), FileAttributes.Normal);
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

    /// <summary>
    /// TREEFOLLOW1 (D164 point 4): an agent that ran <c>git checkout -b feature/x</c> in its tree and committed there, as its
    /// repository's rules ask, is followed. The look learns the branch (the ledger keeps it, so it outlives the tree), takes the
    /// session branch no tree has out any more, says where the work is, and warns of nothing; the tree stays on its branch, whose
    /// commits the head counts, the plan names and Accept lands.
    /// </summary>
    [Fact]
    public async Task A_tree_its_agent_moved_onto_a_branch_of_its_own_is_followed_and_its_empty_session_branch_goes()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var moved = await trees.OpenAsync(root, "engine", "default");
        await GitAsync(moved.Path, "checkout", "--quiet", "-b", "feature/x");
        await CommitAsync(moved.Path, "work.txt", "the session's work", "the work");
        _ledger.Register("engine", root);
        using var log = Log();
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };

        var first = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);
        var second = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.Equal([$"tidy  engine: removed `{moved.Branch}`, which held nothing beyond `main`; the session's work is on `feature/x`."], first);
        Assert.Empty(second);
        Assert.Empty(Events("branch.kept"));
        Assert.DoesNotContain(moved.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.Equal("feature/x", (await GitAsync(moved.Path, "branch", "--show-current")).Trim());
        Assert.True(File.Exists(Path.Combine(moved.Path, "work.txt")));
        // Learned, and kept while the tree or its branch stands, so it is known after the tree goes.
        Assert.Equal("feature/x", trees.Grown.Of("engine", moved.Branch)!.Now);

        var work = await trees.WorkAsync(moved.Path);
        Assert.Equal(1, work!.Commits);
        Assert.Equal("feature/x", work.Branch);
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: false))
            .Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var plan = await trees.PlanAsync(moved.Path, new LandingSubject("s1", null, "Fix the gap"));
        Assert.Equal(("branch", "feature/x", true), (plan.Form, plan.Target, plan.Own));
    }

    /// <summary>
    /// TREEFOLLOW1: a session branch that holds commits of its own stays when its tree moved on, silently: its commits are on
    /// the tree's branch now, so it is landed by ancestry, which the look leaves until the line has it (AUTOTIDY2a), and the
    /// look warns of no unread tree. And the ledger forgets an entry whose tree and both branches are gone at the look, not only
    /// after a removal.
    /// </summary>
    [Fact]
    public async Task A_session_branch_with_work_of_its_own_stays_when_its_tree_moved_on_and_a_gone_entry_is_forgotten()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var moved = await TreeWithWorkAsync(trees, root);
        await GitAsync(moved.Path, "checkout", "--quiet", "-b", "feature/x");
        // An entry no branch and no tree stands behind: a branch deleted by hand, on the install.
        trees.Grown.Record(new GrownBranch("engine", "default", "daoris/s-742b2f42", "main", "abc123", null, _now));
        _ledger.Register("engine", root);
        using var log = Log();
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };

        var said = await tidying.LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.Empty(said);
        Assert.Empty(Events("branch.kept"));
        Assert.Contains(moved.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.Equal("feature/x", trees.Grown.Of("engine", moved.Branch)!.Now);
        Assert.Null(trees.Grown.Of("engine", "daoris/s-742b2f42"));
    }

    /// <summary>
    /// 🔴 AUTOTIDY2a: a tree whose only holdings are ignored paths goes by the look, the tree's own scratch included: an ignored
    /// file only it holds, and a file it wrote in an ignored folder the checkout also has (a session's <c>.tmp/*.log</c>), each
    /// kept in the folder moved aside. The press, which deletes the folder, keeps refusing the tree's own scratch as it did, and an
    /// untracked path that is not ignored keeps the tree at both doors.
    /// </summary>
    [Fact]
    public async Task A_tree_holding_only_ignored_paths_goes_by_the_look_and_stays_at_the_press()
    {
        var root = await RepositoryAsync("engine");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "*.db\nbuild/\n.tmp/\n");
        await GitAsync(root, "commit", "--quiet", "-am", "ignore the scratch folder");
        Directory.CreateDirectory(Path.Combine(root, ".tmp"));
        await File.WriteAllTextAsync(Path.Combine(root, ".tmp", "checkout.log"), "the checkout's own scratch\n");
        var trees = new SessionTrees(_home);
        var scratch = await LandedTreeAsync(trees, root);
        await File.WriteAllTextAsync(Path.Combine(scratch.Path, "session.db"), "the session's own scratch\n");
        Directory.CreateDirectory(Path.Combine(scratch.Path, ".tmp"));
        await File.WriteAllTextAsync(Path.Combine(scratch.Path, ".tmp", "run.log"), "a session's log\n");
        var loose = await LandedTreeAsync(trees, root);
        await File.WriteAllTextAsync(Path.Combine(loose.Path, "session.db"), "the session's own scratch\n");
        await File.WriteAllTextAsync(Path.Combine(loose.Path, "notes.txt"), "neither committed nor ignored\n");
        _ledger.Register("engine", root);
        var nobody = new HashSet<string>();

        var plan = await trees.SweepPlanAsync([("engine", "default", root)], nobody);
        var pressed = await trees.SweepAsync([("engine", "default", root)], nobody);

        var row = plan.Single(item => item.Branch == scratch.Branch);
        Assert.Equal(SweepKind.Dirty, row.Kind);
        Assert.Contains("1 ignored path(s) your checkout does not have: session.db", row.Detail, StringComparison.Ordinal);
        Assert.Contains(".tmp/run.log", row.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(pressed, result => result.Removed);
        Assert.True(Directory.Exists(scratch.Path));

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith(Removed(scratch), Assert.Single(said), StringComparison.Ordinal);
        var aside = Assert.Single(Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees,".tidied", "default", "engine")));
        Assert.Equal("the session's own scratch\n", await File.ReadAllTextAsync(Path.Combine(aside, "session.db")));
        Assert.Equal("a session's log\n", await File.ReadAllTextAsync(Path.Combine(aside, ".tmp", "run.log")));
        Assert.Contains(loose.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(loose.Path, "notes.txt")));
    }

    /// <summary>
    /// AUTOTIDY2a: the look judges every session branch before it holds the repository, so a start choosing a tree waits on the
    /// hold only while what may go is judged again and removed. With nothing to take (commits beyond the line, an uncommitted
    /// change on a branch the line took), a start holding the trees meets no look at all.
    /// </summary>
    [Fact]
    public async Task A_look_with_nothing_to_take_never_holds_the_repository()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        await TreeWithWorkAsync(trees, root);
        var changed = await LandedTreeAsync(trees, root);
        await File.WriteAllTextAsync(Path.Combine(changed.Path, "README.md"), "# engine, edited\n");
        _ledger.Register("engine", root);

        IReadOnlyList<string> said;
        using (TreeLock.TryStarting(_home, "default", "engine"))
        {
            said = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);
        }

        Assert.Empty(said);
    }

    /// <summary>
    /// AUTOTIDY2a: a branch on the line by content keeps its commits on a ref of their own before its tree's folder moves; where
    /// something holds the folder open the branch stays, and that ref is let go with the attempt, as the press's refused removal
    /// lets it go, so no ref is left for a branch that still stands. Once nothing holds the folder, the look takes it, kept.
    /// </summary>
    [Fact]
    public async Task A_content_held_tree_something_holds_open_leaves_no_ref_of_its_commits()
    {
        var root = await RepositoryAsync("engine");
        var squashed = await TreeWithWorkAsync(new SessionTrees(_home), root);
        await GitAsync(root, "merge", "--squash", squashed.Branch);
        await GitAsync(root, "commit", "--quiet", "-m", "the work, squashed");
        _ledger.Register("engine", root);
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };
        var removed = $"tidy  engine: removed `{squashed.Branch}`, since its work is on `main` by content (a squash merge); its tree's folder was moved aside to ";

        IReadOnlyList<string> said;
        using (new FileStream(Path.Combine(squashed.Path, "README.md"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            said = await tidying.LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);
        }

        if (!OperatingSystem.IsWindows())
        {
            Assert.StartsWith(removed, Assert.Single(said), StringComparison.Ordinal);
            return;
        }

        Assert.StartsWith($"tidy  engine: `{squashed.Branch}` stays, since something on this machine is using its tree", Assert.Single(said), StringComparison.Ordinal);
        Assert.Contains(squashed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.Empty((await GitAsync(root, "for-each-ref", "--format=%(refname)", "refs/daoris/discarded/")).Trim());

        var free = await tidying.LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith(removed, Assert.Single(free), StringComparison.Ordinal);
        Assert.Contains(squashed.Branch, await GitAsync(root, "for-each-ref", "--format=%(refname)", "refs/daoris/discarded/"), StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 AUTOTIDY2a, the look waits for the line: Accept's landing put each session's commits on a branch of the person's, its
    /// pull request still open, so the line does not hold them and the person may still ask that session to change them in its
    /// own tree. The look keeps each tree and branch, saying nothing, though the press would take them. Once the line has the
    /// commits, merged into its own branch, or only into origin's copy as a fetch leaves a platform's merge nobody pulled yet,
    /// the next look takes each.
    /// </summary>
    [Fact]
    public async Task A_branch_landed_on_a_branch_of_the_person_s_waits_for_the_line()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var merged = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "branch", "feature/q1-merged", merged.Branch);
        var fetched = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "branch", "feature/q2-fetched", fetched.Branch);
        _ledger.Register("engine", root);
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };
        var plan = await trees.SweepPlanAsync([("engine", "default", root)], new HashSet<string>());
        Assert.All(plan, item => Assert.True(item is { Kind: SweepKind.Landed, Removable: true }, item.Branch));

        var early = await tidying.LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.Empty(early);
        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        foreach (var each in new[] { merged, fetched })
        {
            Assert.Contains(each.Branch, branches, StringComparison.Ordinal);
            Assert.True(Directory.Exists(each.Path), each.Path);
        }

        // The first is merged into the line's own branch; the second reaches only origin's copy, as a platform's fast-forward a
        // fetch brought here before anyone pulled it.
        await GitAsync(root, "update-ref", "refs/remotes/origin/main", "refs/heads/feature/q2-fetched");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", "feature/q1-merged");

        var next = await tidying.LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.Equal(2, next.Count);
        Assert.Contains(next, line => line.StartsWith(Removed(merged), StringComparison.Ordinal));
        Assert.Contains(next, line => line.StartsWith(
            $"tidy  engine: removed `{fetched.Branch}`, since its work is on `origin/main`; its tree's folder was moved aside to ", StringComparison.Ordinal));
        var after = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.DoesNotContain(merged.Branch, after, StringComparison.Ordinal);
        Assert.DoesNotContain(fetched.Branch, after, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 AUTOTIDY2d: a tree whose session ended with the person's words still waiting on its record stays from the look, as a
    /// session in use keeps its own: the next loop goes on with those words in that tree (MSG1b, D137 §2.2). A tree whose ended
    /// session holds no words goes as ever.
    /// </summary>
    [Fact]
    public async Task A_tree_whose_ended_session_has_words_waiting_stays_from_the_look()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var spoken = await LandedTreeAsync(trees, root);
        var quiet = await LandedTreeAsync(trees, root);
        _ledger.Register("engine", root);
        _ledger.Working("s-spoken", "engine", spoken.Path, state: "completed");
        _ledger.Working("s-quiet", "engine", quiet.Path, state: "completed");
        Assert.NotNull((await _ledger.Client().SayAsync("s-spoken", "fix the review comment")).Word);

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith(Removed(quiet), Assert.Single(said), StringComparison.Ordinal);
        Assert.Contains(spoken.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
        Assert.True(Directory.Exists(spoken.Path));
    }

    /// <summary>
    /// 🔴 AUTOTIDY2b: a tree whose own ignored <c>node_modules</c> holds paths past Windows' 260 characters, as an install leaves
    /// one, goes by the look as any other tree: its folder moved aside whole, the deep file in it, git's record of the tree let
    /// go, and fourteen days later the folder deleted, the deep file with it. On the owner's install such a tree's removal met
    /// "Filename too long", and a purge that cannot delete keeps the folder for good.
    /// </summary>
    [Fact]
    public async Task A_tree_whose_paths_pass_260_characters_is_moved_aside_and_purged()
    {
        var root = await RepositoryAsync("engine");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "*.db\nbuild/\nnode_modules/\n");
        await GitAsync(root, "commit", "--quiet", "-am", "ignore the installed packages");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        var deep = Path.Combine([landed.Path, "node_modules", .. Enumerable.Repeat("a-package-folder-long-enough-to-matter", 7)]);
        Directory.CreateDirectory(deep);
        var file = Path.Combine(deep, "index.js");
        await File.WriteAllTextAsync(file, "module.exports = {};\n");
        Assert.True(file.Length > 260, $"{file.Length} characters");
        _ledger.Register("engine", root);
        using var log = Log();

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith(Removed(landed), Assert.Single(said), StringComparison.Ordinal);
        var aside = Assert.Single(Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees,".tidied", "default", "engine")));
        Assert.False(Directory.Exists(landed.Path));
        Assert.True(File.Exists(Path.Combine(aside, Path.GetRelativePath(landed.Path, file))));
        Assert.DoesNotContain(Path.GetFileName(landed.Path), await GitAsync(root, "worktree", "list", "--porcelain"), StringComparison.Ordinal);
        Assert.DoesNotContain(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);

        var later = await new BranchTidying(_home) { Clock = () => DateTimeOffset.UtcNow.AddDays(15) }
            .LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith($"tidy  deleted the folder moved aside to {aside} on ", Assert.Single(later), StringComparison.Ordinal);
        Assert.False(Directory.Exists(aside));
        Assert.Single(Events("tidied.purged"));
        Assert.Empty(Events("tidied.kept"));
    }

    /// <summary>
    /// 🔴 AUTOTIDY2b, the row's own case: the branches Accept's landings made (D102's group) go by the look once the line holds
    /// their work, after their sessions' branches, as the press takes them: one merged into the line, and one squash-merged, its
    /// commits kept on a ref of their own. Each is said in the report by its name, and in the machine log by its session alone,
    /// since its name carries the title's words. One whose pull request is still open stays, with its session's branch, saying
    /// nothing. The session branch of each the line holds goes first, its work on the line through the landing's branch.
    /// </summary>
    [Fact]
    public async Task The_look_takes_the_landing_branches_the_line_holds_after_their_session_branches()
    {
        var root = await RepositoryAsync("engine");
        var trees = LandingTrees();
        var merged = await LandingAsync(trees, root, new LandingSubject("s1", "q1", "Merged"));
        await GitAsync(root, "merge", "--no-ff", "--no-edit", merged.Branch);
        var squashed = await LandingAsync(trees, root, new LandingSubject("s2", "q2", "Squashed"));
        await SquashAsync(root, squashed.Branch);
        var open = await LandingAsync(trees, root, new LandingSubject("s3", "q3", "Open"));
        _ledger.Register("engine", root);
        using var log = Log();

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        var branches = await GitAsync(root, "branch", "--list");
        foreach (var gone in new[] { merged, squashed })
        {
            Assert.DoesNotContain(gone.Branch, branches, StringComparison.Ordinal);
            Assert.DoesNotContain(gone.Tree.Branch, branches, StringComparison.Ordinal);
        }

        Assert.Contains(open.Branch, branches, StringComparison.Ordinal);
        Assert.Contains(open.Tree.Branch, branches, StringComparison.Ordinal);
        var discarded = (await GitAsync(root, "for-each-ref", "--format=%(refname)", "refs/daoris/discarded/")).Trim();
        Assert.Equal($"refs/daoris/discarded/{squashed.Branch}", discarded);
        Assert.Equal(4, said.Count);
        Assert.Equal(2, said.Count(line => line.Contains("a branch a landing made", StringComparison.Ordinal)));
        Assert.Contains($"tidy  engine: removed `{merged.Branch}`, a branch a landing made, since its work is on `main`.", said);
        Assert.Contains(
            $"tidy  engine: removed `{squashed.Branch}`, a branch a landing made, since its work is on `main` by content (each file it changed reads "
            + $"the same there). {ContentHold.KeptAt(squashed.Branch, discarded)}",
            said);
        Assert.DoesNotContain(said, line => line.Contains(open.Branch, StringComparison.Ordinal) || line.Contains(open.Tree.Branch, StringComparison.Ordinal));

        var tidied = Events("branch.tidied");
        Assert.Equal(4, tidied.Length);
        var landings = tidied.Where(data => data.TryGetProperty("session", out _)).ToList();
        Assert.Equal(
            [("s1", LandedKind.Merged), ("s2", LandedKind.OnLine)],
            landings.Select(data => (data.GetProperty("session").GetString(), data.GetProperty("kind").GetString())).Order());
        Assert.All(landings, data => Assert.Equal(JsonValueKind.Null, data.GetProperty("branch").ValueKind));
        Assert.Empty(Events("branch.kept"));
        Assert.DoesNotContain("feature/", string.Join('\n', Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl").SelectMany(StubFile.Lines)),
            StringComparison.Ordinal);
        var record = new LandedBranches(_home);
        Assert.Equal(LandedKind.Merged, record.Landing("s1")!.RemovedAs);
        Assert.Equal(LandedKind.OnLine, record.Landing("s2")!.RemovedAs);
        Assert.Null(record.Landing("s3")!.GoneAt);
    }

    /// <summary>
    /// AUTOTIDY2b: a landing's branch whose work the line holds stays from the look, as the press keeps it, saying nothing, where it
    /// is checked out in a tree of the person's, holds a commit its remote lacks (pushed, then moved), or a session branch a session
    /// still works in leans on it. The session branch of each of the first two goes, its work on the line through that branch.
    /// </summary>
    [Fact]
    public async Task A_landing_branch_checked_out_moved_since_its_push_or_leaned_on_stays_from_the_look()
    {
        var root = await RepositoryAsync("engine");
        var origin = Path.Combine(_scratch, "engine-origin.git");
        Directory.CreateDirectory(origin);
        await GitAsync(origin, "init", "--quiet", "--bare", "-b", "main");
        await GitAsync(root, "remote", "add", "origin", origin);
        await GitAsync(root, "push", "--quiet", "-u", "origin", "main");
        var trees = LandingTrees();
        var checkedOut = await LandingAsync(trees, root, new LandingSubject("s4", "q4", "Checked out"));
        await SquashAsync(root, checkedOut.Branch);
        await GitAsync(root, "worktree", "add", "--quiet", Path.Combine(_scratch, "elsewhere"), checkedOut.Branch);
        var pushed = await LandingAsync(trees, root, new LandingSubject("s5", "q5", "Pushed"));
        await GitAsync(root, "push", "--quiet", "-u", "origin", pushed.Branch);
        await CommitOnAsync(root, pushed.Branch);
        await SquashAsync(root, pushed.Branch);
        var leaned = await LandingAsync(trees, root, new LandingSubject("s6", "q6", "Leaned on"));
        await SquashAsync(root, leaned.Branch);
        _ledger.Register("engine", root);
        _ledger.Working("s-leaned", "engine", leaned.Tree.Path);
        using var log = Log();

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        var branches = await GitAsync(root, "branch", "--list");
        foreach (var kept in new[] { checkedOut, pushed, leaned }) Assert.Contains(kept.Branch, branches, StringComparison.Ordinal);
        Assert.Contains(leaned.Tree.Branch, branches, StringComparison.Ordinal);
        Assert.Equal(2, said.Count);
        foreach (var gone in new[] { checkedOut, pushed })
        {
            Assert.DoesNotContain(gone.Tree.Branch, branches, StringComparison.Ordinal);
            Assert.Contains(said, line => line.StartsWith($"tidy  engine: removed `{gone.Tree.Branch}`, since its work is on `main`; ", StringComparison.Ordinal));
        }

        Assert.DoesNotContain(said, line => line.Contains("feature/", StringComparison.Ordinal));
        Assert.Empty((await GitAsync(root, "for-each-ref", "--format=%(refname)", "refs/daoris/discarded/")).Trim());
        Assert.Empty(Events("branch.kept"));
        Assert.DoesNotContain(Events("branch.tidied"), data => data.TryGetProperty("session", out _));
    }

    /// <summary>
    /// 🔴 AUTOTIDY2c, the owner's install of 2026-10-15: folders under a repository's trees that git lists no tree for (an empty
    /// one a removed tree left, with its branch still here, and folders a session wrote beside its tree) are moved aside whole
    /// once left quiet, said and logged, and the trees list names them moved aside. The branch whose folder that was goes in the
    /// same look, never kept as unreadable. A tree stays, and so does a folder a running session names.
    /// </summary>
    [Fact]
    public async Task Folders_under_the_trees_that_are_no_tree_are_moved_aside_and_listed_so()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var working = await TreeWithWorkAsync(trees, root);
        var engine = DaorisHome.Entry(_home, HomeEntry.Trees,"default", "engine");
        var leftover = Path.Combine(engine, "s-1f315a22");
        Directory.CreateDirectory(leftover);
        await GitAsync(root, "branch", "daoris/s-1f315a22");
        Directory.CreateDirectory(Path.Combine(engine, "devtools"));
        await File.WriteAllTextAsync(Path.Combine(engine, "devtools", "tool.mjs"), "export {};\n");
        Directory.CreateDirectory(Path.Combine(engine, ".tmp"));
        var named = Path.Combine(engine, "scratch");
        Directory.CreateDirectory(named);
        _ledger.Register("engine", root);
        _ledger.Working("s-running", "engine", named);
        using var log = Log();

        var said = await new BranchTidying(_home) { Clock = () => DateTimeOffset.UtcNow.AddHours(1) }
            .LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        var asides = Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees,".tidied", "default", "engine"));
        string Aside(string name) => asides.Single(folder => Path.GetFileName(folder).StartsWith(name + "-", StringComparison.Ordinal));
        Assert.Equal(3, asides.Length);
        Assert.Equal("export {};\n", await File.ReadAllTextAsync(Path.Combine(Aside("devtools"), "tool.mjs")));
        Assert.Equal(
            new[] { "s-1f315a22", "devtools", ".tmp" }.Select(name =>
                $"tidy  engine: `{name}`, a folder under its trees that git knows no tree in, was moved aside to {Aside(name)}, and goes once it is left untouched for 14 days.")
            .Append("tidy  engine: removed `daoris/s-1f315a22`, which held nothing beyond `main`.")
            .Order(StringComparer.Ordinal),
            said.Order(StringComparer.Ordinal));
        Assert.Equal([working.Path, named], Directory.GetDirectories(engine).Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.DoesNotContain("daoris/s-1f315a22", branches, StringComparison.Ordinal);
        Assert.Contains(working.Branch, branches, StringComparison.Ordinal);
        var tidied = Events("branch.tidied");
        Assert.Equal(4, tidied.Length);
        var strays = tidied.Where(data => data.GetProperty("kind").GetString() == StrayKind.Folder).ToList();
        Assert.Equal(3, strays.Count);
        Assert.All(strays, data =>
        {
            Assert.Equal(JsonValueKind.Null, data.GetProperty("branch").ValueKind);
            Assert.True(data.GetProperty("tree").GetBoolean());
            Assert.StartsWith("trees/.tidied/default/engine/", data.GetProperty("folder").GetString(), StringComparison.Ordinal);
        });
        Assert.Empty(Events("branch.kept"));

        var listed = await trees.ListAsync();

        Assert.Equal(working.Branch, listed.Single(tree => tree.Path == working.Path).Branch);
        Assert.Equal("(not a tree)", listed.Single(tree => tree.Path == named).Branch);
        var moved = listed.Where(tree => tree.MovedAside is not null).ToList();
        Assert.Equal(asides.Order(StringComparer.Ordinal), moved.Select(tree => tree.Path).Order(StringComparer.Ordinal));
        Assert.All(moved, tree => Assert.Equal(("default", "engine", "(moved aside)"), (tree.Workspace, tree.Repository, tree.Branch)));
    }

    /// <summary>
    /// A folder something wrote in lately may be one a start is making or a session is using: it waits, saying nothing, until it
    /// has been left quiet (<see cref="TreeStrays.Quiet"/>), and is then moved aside.
    /// </summary>
    [Fact]
    public async Task A_folder_written_in_lately_waits_for_a_later_look()
    {
        var root = await RepositoryAsync("engine");
        var devtools = DaorisHome.Entry(_home, HomeEntry.Trees,"default", "engine", "devtools");
        Directory.CreateDirectory(devtools);
        await File.WriteAllTextAsync(Path.Combine(devtools, "tool.mjs"), "export {};\n");
        _ledger.Register("engine", root);

        var now = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.Empty(now);
        Assert.True(File.Exists(Path.Combine(devtools, "tool.mjs")));

        var later = await new BranchTidying(_home) { Clock = () => DateTimeOffset.UtcNow + TreeStrays.Quiet + TimeSpan.FromMinutes(1) }
            .LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.StartsWith("tidy  engine: `devtools`, a folder under its trees that git knows no tree in, was moved aside to ", Assert.Single(later), StringComparison.Ordinal);
        Assert.False(Directory.Exists(devtools));
    }

    /// <summary>
    /// 🔴 OPENCUT1, the owner's install of 2026-10-15 (<c>s-0031408d</c>): a tree whose opening a kill cut off is locked, half
    /// checked out, on a branch no record names. Where its lock is git's own from an add that never finished, or Daoris's naming a
    /// process that no longer runs, the look lets go of the lock, moves its folder aside, lets git forget it and removes its
    /// branch, which holds nothing of its own. A lock a person made, or one a Daoris process still running holds, keeps its tree.
    /// </summary>
    [Fact]
    public async Task A_tree_whose_opening_a_kill_cut_off_is_moved_aside_with_its_branch()
    {
        var root = await RepositoryAsync("engine");
        var engine = DaorisHome.Entry(_home, HomeEntry.Trees,"default", "engine");
        var cut = Path.Combine(engine, "s-0031408d");
        await GitAsync(root, "worktree", "add", "--quiet", "-b", "daoris/s-0031408d", cut, "main");
        await GitAsync(root, "worktree", "lock", "--reason", TreeOpenings.GitsOwn, cut);
        await GitAsync(cut, "rm", "--cached", "-r", "--quiet", ".");
        var ended = Path.Combine(engine, "s-0e0ded00");
        await GitAsync(root, "worktree", "add", "--quiet", "--lock", "--reason", TreeOpenings.LockReason(await EndedProcessAsync()), "-b", "daoris/s-0e0ded00", ended, "main");
        var opening = Path.Combine(engine, "s-0be11000");
        await GitAsync(root, "worktree", "add", "--quiet", "--lock", "--reason", TreeOpenings.LockReason(Environment.ProcessId), "-b", "daoris/s-0be11000", opening, "main");
        var persons = Path.Combine(engine, "s-9e450000");
        await GitAsync(root, "worktree", "add", "--quiet", "--lock", "--reason", "a person's", "-b", "daoris/s-9e450000", persons, "main");
        _ledger.Register("engine", root);
        using var log = Log();

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        var asides = Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees,".tidied", "default", "engine"));
        string Aside(string name) => asides.Single(folder => Path.GetFileName(folder).StartsWith(name + "-", StringComparison.Ordinal));
        Assert.Equal(2, asides.Length);
        Assert.True(File.Exists(Path.Combine(Aside("s-0031408d"), "README.md")));
        Assert.Equal(
            [
                $"tidy  engine: removed `daoris/s-0031408d`, whose tree's opening was cut off: git's own lock from the start of its add was "
                + $"left on it; its tree's folder was moved aside to {Aside("s-0031408d")}, and goes once it is left untouched for 14 days.",
                $"tidy  engine: removed `daoris/s-0e0ded00`, whose tree's opening was cut off: the Daoris process that opened it no longer "
                + $"runs; its tree's folder was moved aside to {Aside("s-0e0ded00")}, and goes once it is left untouched for 14 days.",
            ],
            said.Order(StringComparer.Ordinal));
        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.DoesNotContain("daoris/s-0031408d", branches, StringComparison.Ordinal);
        Assert.DoesNotContain("daoris/s-0e0ded00", branches, StringComparison.Ordinal);
        Assert.Contains("daoris/s-0be11000", branches, StringComparison.Ordinal);
        Assert.Contains("daoris/s-9e450000", branches, StringComparison.Ordinal);
        var worktrees = (await GitAsync(root, "worktree", "list", "--porcelain")).Replace('\\', '/');
        Assert.DoesNotContain("s-0031408d", worktrees, StringComparison.Ordinal);
        Assert.DoesNotContain("s-0e0ded00", worktrees, StringComparison.Ordinal);
        Assert.Contains($"locked {TreeOpenings.LockReason(Environment.ProcessId)}", worktrees, StringComparison.Ordinal);
        Assert.Contains("locked a person's", worktrees, StringComparison.Ordinal);
        Assert.Equal(
            ["daoris/s-0031408d", "daoris/s-0e0ded00"],
            Events("branch.tidied").Where(data => data.GetProperty("kind").GetString() == StrayKind.Opening)
                .Select(data => data.GetProperty("branch").GetString()).Order(StringComparer.Ordinal));
        Assert.Empty(Events("branch.kept"));

        var listed = await new SessionTrees(_home).ListAsync();

        Assert.Equal(2, listed.Count(tree => tree.MovedAside is not null));
        Assert.Equal(["daoris/s-0be11000", "daoris/s-9e450000"], listed.Where(tree => tree.MovedAside is null).Select(tree => tree.Branch).Order(StringComparer.Ordinal));
    }

    /// <summary>The id of a process that ran and ended: one an opening's lock may name, that no longer runs.</summary>
    private static async Task<int> EndedProcessAsync()
    {
        using var process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("git", "--version") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true })!;
        await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return process.Id;
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
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
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

    /// <summary>This home's trees, with `engine`'s landing rule the branch form: Accept makes a branch of the person's and records it.</summary>
    private SessionTrees LandingTrees()
    {
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"))
            .Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return new SessionTrees(_home);
    }

    /// <summary>A session's tree with one commit, landed by Accept on a branch the record keeps (the branch form's rule).</summary>
    private async Task<(TreeOpened Tree, string Branch)> LandingAsync(SessionTrees trees, string root, LandingSubject subject)
    {
        var tree = await TreeWithWorkAsync(trees, root);
        var landed = await trees.LandAsync(tree.Path, subject);
        Assert.True(landed.Landed, landed.Message);
        return (tree, landed.Branch!);
    }

    /// <summary>A squash merge onto the checkout's own line, as a platform's "squash and merge" leaves it.</summary>
    private static async Task SquashAsync(string root, string branch)
    {
        await GitAsync(root, "merge", "--squash", branch);
        await GitAsync(root, "commit", "--quiet", "-m", $"{branch} (squashed)");
    }

    /// <summary>A commit on a branch nobody has checked out, through a worktree made and removed for it.</summary>
    private async Task CommitOnAsync(string root, string branch)
    {
        var place = Path.Combine(_scratch, $"on-{Guid.NewGuid():N}"[..12]);
        await GitAsync(root, "worktree", "add", "--quiet", place, branch);
        await CommitAsync(place, "after-the-push.txt", "after the push\n", "after the push");
        await GitAsync(root, "worktree", "remove", place);
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
