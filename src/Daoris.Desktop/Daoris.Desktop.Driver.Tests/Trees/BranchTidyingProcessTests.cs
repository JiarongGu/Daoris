using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTOTIDY1 (D88's note) over real git: the look removes a session branch that holds nothing beyond the line, with its tree,
/// by the clean-up's own path, and keeps every other branch exactly as the press finds it. The sessions are the stand-in
/// ledger's, read through the real client. The folders' cases are <see cref="BranchTidyingFolderTests"/>, the landings'
/// <see cref="BranchTidyingLandingTests"/> and the watch's <see cref="BranchTidyingWatchTests"/> (PROC1g).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class BranchTidyingProcessTests : BranchTidyingFixture
{
    /// <summary>
    /// 🔴 The row's own case, AUTOTIDY1's and AUTOTIDY2a's: every branch the press clears whose work is on the line goes by
    /// itself, each said once in the report and the machine log. A session branch whose work a merge took onto the line goes with
    /// its tree; one on the line by content after a squash, its commits kept on a ref of
    /// their own; and, each on a branch the line took, a tree holding an ignored file only it holds and one holding a build's
    /// output the checkout holds too, each folder moved aside whole. Every other branch stays, as the press finds it, and says
    /// nothing: commits beyond the line; commits on a branch of the person's the line does not hold yet, whose session may still
    /// be asked to change them; a tree whose branch never moved, which words to its session go on in; and, each on a branch the
    /// line took, an uncommitted change, an untracked file, a session working in its tree, and one waiting on the person. A branch
    /// Daoris did not make and record stays too, with nothing beyond the line: its owner is unknown, so it is the press's
    /// (FABRIC-END4a, the design §C3 and §D3).
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
        Assert.Contains("daoris/s-treeless", branches, StringComparison.Ordinal);
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

        Assert.Equal(4, said.Count);
        Assert.DoesNotContain("tidy  engine: removed `daoris/s-treeless`, which held nothing beyond `main`.", said);
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
        var asides = Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine"));
        Assert.Equal(gone.Length, asides.Length);
        string Aside(TreeOpened tree) => asides.Single(folder => Path.GetFileName(folder).StartsWith(Path.GetFileName(tree.Path) + "-", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(Aside(merged), "README.md")));
        Assert.Equal("made here alone\n", await File.ReadAllTextAsync(Path.Combine(Aside(ignored), "local.db")));
        Assert.True(File.Exists(Path.Combine(Aside(built), "build", "out.txt")));
        var listed = (await GitAsync(root, "worktree", "list", "--porcelain")).Replace('\\', '/');
        Assert.All(gone, each => Assert.DoesNotContain(each.Path.Replace('\\', '/'), listed, StringComparison.OrdinalIgnoreCase));
        var tidied = Events("branch.tidied");
        Assert.Equal(4, tidied.Length);
        Assert.Equal(
            [SweepKind.Empty, SweepKind.Empty, SweepKind.Empty, SweepKind.Landed],
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
        var aside = Assert.Single(Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine")));
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
}
