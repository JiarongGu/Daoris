using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1e part e3a over real git (D215's note; the integration design §5.2 steps 2 and 3): a candidate formed from a line's queued
/// requests and each member replayed onto it in the line's own warm integration tree, containment judged first by the frozen tip; a
/// conflict aborted and handed back with the tree clean for the next member; a crash inside a replay recovered by putting the tree back at
/// the step's recorded start. Called from no production path. Never moves the line: promotion is row e4's.
/// </summary>
/// <remarks>The case names follow the merge tool's candidate tests (<c>merge-queue.test.ts</c>, INTEGRATE1c).</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationFormGitTests : LandedFixture
{
    private sealed class Killed() : Exception("the process died here");

    private IntegrationStore Store(string run, Func<StoreRun, bool>? alive = null) => new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    /// <summary>The second process's store: run-b, which reads run-a as dead.</summary>
    private IntegrationStore Taker() => Store("run-b", run => run.Run != "run-a");

    private string TreesHome => DaorisHome.Entry(Home, HomeEntry.Trees);

    private IntegrationReplays Replays(IntegrationStore store) => new(store, TreesHome);

    private static IntegrationReplayAsk Ask(string root, Door? door = null, DriverConfig? config = null) =>
        new("engine", "aurora", root, "main") { Say = door is null ? null : door.SayAsync, Config = config };

    /// <summary>
    /// The say door a session is told through, as the real one keeps a word: under its key, once. A key said again answers kept and says nothing
    /// more, so <see cref="Told"/> is what reached the session and <see cref="Calls"/> every time the driver asked.
    /// </summary>
    private sealed class Door(bool keeps = true)
    {
        private readonly HashSet<string> _keys = [];

        public List<(string Session, string Text, string Key)> Told { get; } = [];

        public int Calls { get; private set; }

        public Task<SayAnswer?> SayAsync(string session, string text, string key, CancellationToken ct)
        {
            Calls++;
            if (!keeps) return Task.FromResult<SayAnswer?>(new SayAnswer(false, "its conversation is gone"));
            if (_keys.Add(key)) Told.Add((session, text, key));
            return Task.FromResult<SayAnswer?>(new SayAnswer(true, "kept"));
        }
    }

    private const string Rows = "# Tasks\n\n- [ ] **A1** one.\n- [ ] **A2** two.\n- [ ] **A3** three.\n";

    private const string Archive = "# Archive\n\n## OLD - old (2026-10-01)\nOld.\n";

    private const string Writer = "git ls-files -- *.txt > index/map.md";

    private static string Declaration(string writer) =>
        $"{{ \"gates\": [], \"generated\": [{{ \"paths\": \"index\", \"run\": \"{writer}\" }}] }}\n";

    /// <summary>The line's generated folder, marked so, with its first copy and its declaration; git's blob id of that declaration.</summary>
    private static async Task<string> GeneratedDeclaredAsync(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "index"));
        await File.WriteAllTextAsync(Path.Combine(root, ".gitattributes"), "index/** linguist-generated\n");
        await File.WriteAllTextAsync(Path.Combine(root, "index", "map.md"), "shared.txt\n");
        await File.WriteAllTextAsync(Path.Combine(root, "shared.txt"), "shared\n");
        await CommitAsync(root, "daoris.gates.json", Declaration(Writer), "the index and its declaration");
        return (await GitAsync(root, "rev-parse", "HEAD:daoris.gates.json")).Trim();
    }

    private static async Task<string> ShowAsync(string root, string commit, string path) => await GitAsync(root, "show", $"{commit}:{path}");

    /// <summary>No merge is left in progress in the warm tree: git keeps a linked tree's MERGE_HEAD in its own git directory.</summary>
    private static async Task AssertNoMergeAsync(string tree)
    {
        var mergeHead = (await GitAsync(tree, "rev-parse", "--path-format=absolute", "--git-path", "MERGE_HEAD")).Trim();
        Assert.False(File.Exists(mergeHead), mergeHead);
    }

    private static async Task<string> CommitOfAsync(string cwd, string reference) => (await GitAsync(cwd, "rev-parse", reference)).Trim();

    /// <summary>A branch from <c>main</c> with one commit for each file given, the checkout left on <c>main</c>; the branch's tip.</summary>
    private static async Task<string> BranchAsync(string root, string name, params (string File, string Content)[] commits)
    {
        await GitAsync(root, "checkout", "--quiet", "-b", name, "main");
        foreach (var (file, content) in commits) await CommitAsync(root, file, content, $"work on {name}: {file}");
        var tip = await CommitOfAsync(root, name);
        await GitAsync(root, "checkout", "--quiet", "main");
        return tip;
    }

    /// <summary>The request of a branch at its frozen tip, the <paramref name="order"/>th to arrive; a rule that takes up to eight members.</summary>
    private static void Queue(IntegrationStore store, string id, string branch, string tip, int order, string? session = null, string? conflicts = null) =>
        Assert.Null(store.Add(new IntegrationRequest(
            id, "engine", "aurora", branch, tip, IntegrationRoutes.Recorded, "main", IntegrationRequester.Card,
            new LandingIntegrate(Batch: 8, Conflicts: conflicts).Resolved(), new DateTimeOffset(2026, 10, 30, 9, 0, order, TimeSpan.Zero)) { Session = session }));

    private static async Task<string[]> ParentsOfAsync(string cwd, string commit) =>
        (await GitAsync(cwd, "rev-list", "--parents", "-n", "1", commit)).Trim().Split(' ').Skip(1).ToArray();

    private static async Task<string> StatusAsync(string tree) => (await GitAsync(tree, "status", "--porcelain")).Trim();

    private static IntegrationMember Member(IntegrationStore store, IntegrationCandidate candidate, string request) =>
        store.Members(candidate.Id).Single(each => each.Request == request);

    /// <summary>
    /// 🔴 Two queued requests form one candidate on the line's commit and are replayed in order, each merged <c>--no-ff</c> onto the last in a
    /// warm tree of Daoris's own under the trees home: the merge commit's second parent is the frozen tip, the line never moves, and the
    /// person's checkout is left as it was.
    /// </summary>
    [Fact]
    public async Task Two_members_are_formed_and_replayed_in_order_in_a_tree_of_the_lines_own()
    {
        var root = await RepositoryAsync("engine");
        var @base = await CommitOfAsync(root, "main");
        var a = await BranchAsync(root, "feature/a", ("a.txt", "a\n"));
        var b = await BranchAsync(root, "feature/b", ("b.txt", "b\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", a, 1);
        Queue(store, "rb", "feature/b", b, 2);

        var run = await Replays(store).RunAsync(Ask(root));

        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        var candidate = store.Candidate(run.Candidate!.Id)!;
        Assert.Equal(@base, candidate.Base);
        var first = Member(store, candidate, "ra");
        var second = Member(store, candidate, "rb");
        Assert.Equal((IntegrationMemberStates.Replayed, IntegrationMemberStates.Replayed), (first.State, second.State));
        Assert.Equal(second.Replayed, candidate.Tip);
        Assert.Equal([@base, a], await ParentsOfAsync(root, first.Replayed!));
        Assert.Equal([first.Replayed!, b], await ParentsOfAsync(root, second.Replayed!));
        Assert.Equal("Merge branch 'feature/b'", (await GitAsync(root, "log", "-1", "--format=%s", second.Replayed!)).Trim());

        // The tree is the line's own, under the trees home, detached at the candidate's tip, clean and locked; no one's checkout was used.
        var tree = run.Tree!;
        Assert.StartsWith(Path.GetFullPath(TreesHome), Path.GetFullPath(tree), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetFullPath(root), Path.GetFullPath(tree), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(second.Replayed, await CommitOfAsync(tree, "HEAD"));
        Assert.Equal("", await StatusAsync(tree));
        Assert.Equal("HEAD", (await GitAsync(tree, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Contains("locked", await GitAsync(root, "worktree", "list", "--porcelain"), StringComparison.Ordinal);

        Assert.Equal(@base, await CommitOfAsync(root, "main"));
        Assert.Equal("main", (await GitAsync(root, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal("", await StatusAsync(root));

        // Each end wrote its step: the form, then a replay for each member, in the order given.
        Assert.Equal(
            [(IntegrationStepNames.Form, null, 1), (IntegrationStepNames.Replay, "ra", 1), (IntegrationStepNames.Replay, "rb", 1)],
            store.Steps(candidate.Id).Select(each => (each.Step, each.Request, each.Attempt)));
        Assert.All(store.Steps(candidate.Id), each => Assert.Equal((IntegrationStepOutcomes.Done, @base), (each.Outcome, each.LineCommit)));
    }

    /// <summary>
    /// 🔴 A member's tip is the one its request froze, never the branch's live one: a commit made on the branch after the request is not in
    /// the merge, and the merge commit's second parent is the frozen tip.
    /// </summary>
    [Fact]
    public async Task A_members_frozen_tip_is_replayed_and_never_the_branchs_live_one()
    {
        var root = await RepositoryAsync("engine");
        var frozen = await BranchAsync(root, "feature/a", ("a.txt", "a\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", frozen, 1);
        await GitAsync(root, "checkout", "--quiet", "feature/a");
        await CommitAsync(root, "later.txt", "made after the request froze\n", "after the freeze");
        var live = await CommitOfAsync(root, "feature/a");
        await GitAsync(root, "checkout", "--quiet", "main");

        var run = await Replays(store).RunAsync(Ask(root));

        var replayed = Member(store, run.Candidate!, "ra").Replayed!;
        Assert.Equal(frozen, (await ParentsOfAsync(root, replayed))[1]);
        Assert.DoesNotContain(live, await GitAsync(root, "rev-list", replayed), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(run.Tree!, "later.txt")));
    }

    /// <summary>
    /// 🔴 A member the line holds is contained and settled at once (nothing replayed, its request closed); one an earlier member's replay holds
    /// stays in the candidate contained by it, provisional, its request open, and is queued again where its candidate ends unsettled.
    /// </summary>
    [Fact]
    public async Task A_member_the_line_holds_is_settled_at_once_and_one_an_earlier_member_holds_is_provisional()
    {
        var root = await RepositoryAsync("engine");
        var held = await CommitOfAsync(root, "main");
        await CommitAsync(root, "line.txt", "the line moves\n", "the line moves");
        var x = await BranchAsync(root, "feature/x", ("x1.txt", "1\n"), ("x2.txt", "2\n"));
        var inside = await CommitOfAsync(root, "feature/x~1");
        var store = Store("run-a");
        Queue(store, "rz", "feature/z", held, 1);
        Queue(store, "rx", "feature/x", x, 2);
        Queue(store, "ry", "feature/y", inside, 3);

        var run = await Replays(store).RunAsync(Ask(root));

        var candidate = store.Candidate(run.Candidate!.Id)!;
        var z = Member(store, candidate, "rz");
        Assert.Equal((IntegrationMemberStates.Contained, null, false, null), (z.State, z.ContainedBy, z.Stands, z.Replayed));
        Assert.Equal(IntegrationRequestCloses.Settled, store.Request("rz")!.ClosedWhy);
        var replayed = Member(store, candidate, "rx");
        Assert.Equal(IntegrationMemberStates.Replayed, replayed.State);
        var y = Member(store, candidate, "ry");
        Assert.Equal((IntegrationMemberStates.Contained, "rx", true, null), (y.State, y.ContainedBy, y.Stands, y.Replayed));
        Assert.Null(store.Request("ry")!.ClosedAt);
        Assert.Equal(replayed.Replayed, candidate.Tip);
        Assert.Equal([await CommitOfAsync(root, "main"), x], await ParentsOfAsync(root, candidate.Tip!));

        // Said, and each its own step: z and y ended their replay without moving the tree.
        Assert.Equal(
            [IntegrationStepNames.Form, IntegrationStepNames.Replay, IntegrationStepNames.Replay, IntegrationStepNames.Replay],
            store.Steps(candidate.Id).Select(each => each.Step));

        // The candidate ends unsettled: the provisional member goes back to the queue beside its container, in arrival order.
        await new IntegrationSteps(store).RunAsync(
            run.Claim!, IntegrationStepNames.Settle,
            (_, _) => Task.FromResult(new IntegrationStepEnd(IntegrationStepOutcomes.Held) { Ends = IntegrationCandidateEnds.FormedAgain }));
        Assert.Equal(["rx", "ry"], store.Queued("engine", "main").Select(each => each.Id));
    }

    /// <summary>
    /// 🔴 A member whose work the line holds by patch (a cherry-pick, so no ancestry shows it) is judged contained by D87 point 1's judgement and
    /// never merged, and never read as a conflict.
    /// </summary>
    [Fact]
    public async Task A_member_the_line_holds_by_patch_is_contained_and_not_a_conflict()
    {
        var root = await RepositoryAsync("engine");
        var x = await BranchAsync(root, "feature/x", ("x.txt", "x\n"));
        await GitAsync(root, "cherry-pick", "-x", x);
        var @base = await CommitOfAsync(root, "main");
        var store = Store("run-a");
        Queue(store, "rx", "feature/x", x, 1);

        var run = await Replays(store).RunAsync(Ask(root));

        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        var candidate = store.Candidate(run.Candidate!.Id)!;
        var member = Member(store, candidate, "rx");
        Assert.Equal((IntegrationMemberStates.Contained, null, false), (member.State, member.ContainedBy, member.Stands));
        Assert.Equal(IntegrationRequestCloses.Settled, store.Request("rx")!.ClosedWhy);
        // Nothing left to promote: the candidate ended, its line free, its tree where it was formed.
        Assert.Equal((IntegrationCandidateEnds.Empty, null), (candidate.EndedWhy, candidate.Tip));
        Assert.Null(store.StandingFor("engine", "main"));
        Assert.Equal(@base, await CommitOfAsync(run.Tree!, "HEAD"));
    }

    /// <summary>
    /// 🔴 Two members that conflict: the second is aborted, the tree put back at the candidate so far, and it is <c>conflict</c> then handed
    /// back at its own step with its request closed; the third goes on onto the first, so the warm tree was clean for it, and the line stays.
    /// </summary>
    [Fact]
    public async Task One_member_conflicting_is_handed_back_and_the_next_goes_on_in_a_clean_tree()
    {
        var root = await RepositoryAsync("engine");
        var @base = await CommitOfAsync(root, "main");
        var a = await BranchAsync(root, "feature/a", ("shared.txt", "from a\n"));
        var b = await BranchAsync(root, "feature/b", ("shared.txt", "from b\n"));
        var c = await BranchAsync(root, "feature/c", ("c.txt", "c\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", a, 1, session: "s-a");
        Queue(store, "rb", "feature/b", b, 2, session: "s-b");
        Queue(store, "rc", "feature/c", c, 3, session: "s-c");
        var door = new Door();

        var run = await Replays(store).RunAsync(Ask(root, door));

        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        var candidate = store.Candidate(run.Candidate!.Id)!;
        Assert.True(candidate.Stands);
        var first = Member(store, candidate, "ra");
        var conflicted = Member(store, candidate, "rb");
        var third = Member(store, candidate, "rc");
        Assert.Equal((IntegrationMemberStates.HandedBack, false, null), (conflicted.State, conflicted.Stands, conflicted.Replayed));
        Assert.Contains("shared.txt", conflicted.Said, StringComparison.Ordinal);
        Assert.Equal(IntegrationRequestCloses.HandedBack, store.Request("rb")!.ClosedWhy);
        Assert.Null(store.Request("ra")!.ClosedAt);

        // §5.4's `session`, the default: only the conflicting member's session is told, in the rebase words, keyed by the session and the
        // frozen tip, with the file and the member it met (an earlier member of the same candidate).
        var told = Assert.Single(door.Told);
        Assert.Equal(("s-b", RebaseKeys.Of("s-b", b)), (told.Session, told.Key));
        Assert.StartsWith("Rebase your work onto `main`: the line moved since your work began, and landing it conflicts with what the line changed:", told.Text);
        Assert.Contains("`shared.txt`", told.Text, StringComparison.Ordinal);
        Assert.Contains("`feature/a`", told.Text, StringComparison.Ordinal);

        // The third was merged onto the first's replay, not onto the conflicted attempt, and the tree holds only theirs.
        Assert.Equal([first.Replayed!, c], await ParentsOfAsync(root, third.Replayed!));
        Assert.Equal(third.Replayed, candidate.Tip);
        var tree = run.Tree!;
        Assert.Equal(third.Replayed, await CommitOfAsync(tree, "HEAD"));
        Assert.Equal("", await StatusAsync(tree));
        Assert.Equal("from a", (await File.ReadAllTextAsync(Path.Combine(tree, "shared.txt"))).Trim());
        Assert.True(File.Exists(Path.Combine(tree, "c.txt")));
        // No merge is left in progress in the warm tree: git keeps a linked tree's MERGE_HEAD in its own git directory.
        var mergeHead = (await GitAsync(tree, "rev-parse", "--path-format=absolute", "--git-path", "MERGE_HEAD")).Trim();
        Assert.False(File.Exists(mergeHead), mergeHead);
        Assert.Equal(@base, await CommitOfAsync(root, "main"));

        // The conflict was a replay that ended, then a step of its own that handed it back, before the next member's replay.
        Assert.Equal(
            [
                (IntegrationStepNames.Form, null, IntegrationStepOutcomes.Done), (IntegrationStepNames.Replay, "ra", IntegrationStepOutcomes.Done),
                (IntegrationStepNames.Replay, "rb", IntegrationStepOutcomes.Done), (IntegrationStepNames.Conflict, "rb", IntegrationStepOutcomes.HandedBack),
                (IntegrationStepNames.Replay, "rc", IntegrationStepOutcomes.Done),
            ],
            store.Steps(candidate.Id).Select(each => (each.Step, each.Request, each.Outcome)));
    }

    /// <summary>
    /// 🔴 INTEGRATE1e part e3b, step 4 (the twin of the merge tool's "the backlog's line conflict is resolved by the merge base", MERGETASKS1): the
    /// second member and the first change the backlog's neighbouring lines, which git reads as one conflict; the backlog the line declares is
    /// merged by its base, the merge made with the member's frozen tip as its second parent, and the third member goes on onto it in a clean tree.
    /// </summary>
    [Fact]
    public async Task The_backlogs_line_conflict_is_resolved_by_the_merge_base_and_the_member_goes_on_in_a_clean_tree()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "TASKS.md", Rows, "the backlog");
        var a = await BranchAsync(root, "feature/a", ("TASKS.md", Rows.Replace("- [ ] **A2** two.\n", "")));
        var b = await BranchAsync(root, "feature/b", ("TASKS.md", Rows.Replace("**A1** one.\n", "**A1** one.\n- [ ] **B1** the second's.\n")));
        var c = await BranchAsync(root, "feature/c", ("c.txt", "c\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", a, 1);
        Queue(store, "rb", "feature/b", b, 2);
        Queue(store, "rc", "feature/c", c, 3);
        var door = new Door();

        var run = await Replays(store).RunAsync(Ask(root, door));

        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        var candidate = store.Candidate(run.Candidate!.Id)!;
        var first = Member(store, candidate, "ra");
        var second = Member(store, candidate, "rb");
        var third = Member(store, candidate, "rc");
        Assert.Equal((IntegrationMemberStates.Replayed, IntegrationMemberStates.Replayed, IntegrationMemberStates.Replayed), (first.State, second.State, third.State));
        Assert.Equal([first.Replayed!, b], await ParentsOfAsync(root, second.Replayed!));
        Assert.Equal("# Tasks\n\n- [ ] **A1** one.\n- [ ] **B1** the second's.\n- [ ] **A3** three.\n", await ShowAsync(root, second.Replayed!, "TASKS.md"));
        Assert.Equal("Merge branch 'feature/b'", (await GitAsync(root, "log", "-1", "--format=%s", second.Replayed!)).Trim());
        Assert.Equal([second.Replayed!, c], await ParentsOfAsync(root, third.Replayed!));
        Assert.Equal(third.Replayed, candidate.Tip);
        Assert.Equal("", await StatusAsync(run.Tree!));
        await AssertNoMergeAsync(run.Tree!);
        Assert.Empty(door.Told);
        var step = store.Steps(candidate.Id).Single(each => each is { Step: IntegrationStepNames.Replay, Request: "rb" });
        Assert.Contains("`TASKS.md` conflicted and was settled by the merge base: 1 line(s) a side added kept, 1 line(s) a side removed dropped", step.Said, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The twin of the same test's "one row both sides edited is left (MERGETASKS1)", and of its "backlog beside code": a row both sides edited
    /// is the session's conflict, and so is a settled backlog beside a conflict in code. Nothing the resolve staged stays: the tree goes back to the
    /// candidate so far, and the next member goes on from it.
    /// </summary>
    [Fact]
    public async Task A_backlog_row_both_sides_edited_and_a_backlog_beside_code_are_conflicts_and_the_tree_is_clean_after_each()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "TASKS.md", Rows, "the backlog");
        await CommitAsync(root, "shared.txt", "shared\n", "a file both change");
        var a = await BranchAsync(root, "feature/a", ("TASKS.md", Rows.Replace("**A2** two.", "**A2** two, a's.")));
        var b = await BranchAsync(root, "feature/b", ("TASKS.md", Rows.Replace("**A2** two.", "**A2** two, b's.")));
        var c = await BranchAsync(root, "feature/c", ("TASKS.md", Rows.Replace("- [ ] **A3** three.\n", "")), ("shared.txt", "from c\n"));
        var d = await BranchAsync(root, "feature/d", ("TASKS.md", Rows.Replace("**A1** one.\n", "**A1** one.\n- [ ] **D1** d's.\n")), ("shared.txt", "from d\n"));
        var e = await BranchAsync(root, "feature/e", ("e.txt", "e\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", a, 1, session: "s-a");
        Queue(store, "rb", "feature/b", b, 2, session: "s-b");
        Queue(store, "rc", "feature/c", c, 3, session: "s-c");
        Queue(store, "rd", "feature/d", d, 4, session: "s-d");
        Queue(store, "re", "feature/e", e, 5, session: "s-e");
        var door = new Door();

        var run = await Replays(store).RunAsync(Ask(root, door));

        var candidate = store.Candidate(run.Candidate!.Id)!;
        Assert.Equal(
            [IntegrationMemberStates.Replayed, IntegrationMemberStates.HandedBack, IntegrationMemberStates.Replayed, IntegrationMemberStates.HandedBack, IntegrationMemberStates.Replayed],
            store.Members(candidate.Id).Select(each => each.State));
        Assert.Contains("TASKS.md", Member(store, candidate, "rb").Said, StringComparison.Ordinal);
        Assert.Contains("shared.txt", Member(store, candidate, "rd").Said, StringComparison.Ordinal);
        Assert.Equal(["s-b", "s-d"], door.Told.Select(each => each.Session));
        // Member d's settled backlog is not in the tree: the candidate is a, c's merges and e's.
        var tree = run.Tree!;
        Assert.Equal(Member(store, candidate, "re").Replayed, await CommitOfAsync(tree, "HEAD"));
        Assert.Equal("", await StatusAsync(tree));
        await AssertNoMergeAsync(tree);
        Assert.DoesNotContain("D1", await File.ReadAllTextAsync(Path.Combine(tree, "TASKS.md")), StringComparison.Ordinal);
        Assert.Equal("from c", (await File.ReadAllTextAsync(Path.Combine(tree, "shared.txt"))).Trim());
    }

    /// <summary>
    /// 🔴 The twin of the merge tool's "two archive entries a union merge leaves touching are spaced in the merge": each member appends an entry to
    /// a union-merged record, git keeps the blank line they both open with once, so the second's heading lands under the first's last line; the
    /// resolve sets one blank line before it, inside the member's one merge commit, and never leaves it glued. A join the line already held stays.
    /// </summary>
    [Fact]
    public async Task Two_archive_entries_a_union_merge_leaves_touching_are_spaced_in_the_merge_and_a_join_the_line_held_is_left()
    {
        var root = await RepositoryAsync("engine");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        // Named apart from this repository's own archive: a path it tracks reads to the merge tool's reach census as a read of that file.
        await File.WriteAllTextAsync(Path.Combine(root, ".gitattributes"), "docs/archive.md merge=union\n");
        // The line's own copy holds a join already, which the merge never made.
        var held = Archive + "## HELD - held (2026-10-02)\nHeld.\n";
        await CommitAsync(root, "docs/archive.md", held, "the archive");
        var a = await BranchAsync(root, "feature/a", ("docs/archive.md", held + "\n## A - a (2026-10-27)\nOutcome A.\n"));
        var b = await BranchAsync(root, "feature/b", ("docs/archive.md", held + "\n## B - b (2026-10-28)\nOutcome B.\n"));
        var c = await BranchAsync(root, "feature/c", ("c.txt", "c\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", a, 1);
        Queue(store, "rb", "feature/b", b, 2);
        Queue(store, "rc", "feature/c", c, 3);

        var run = await Replays(store).RunAsync(Ask(root));

        var candidate = store.Candidate(run.Candidate!.Id)!;
        var second = Member(store, candidate, "rb");
        Assert.Equal(IntegrationMemberStates.Replayed, second.State);
        Assert.Equal(
            held + "\n## A - a (2026-10-27)\nOutcome A.\n\n## B - b (2026-10-28)\nOutcome B.\n",
            await ShowAsync(root, second.Replayed!, "docs/archive.md"));
        // One merge commit for the member, its second parent the frozen tip: nothing was committed on top.
        Assert.Equal([Member(store, candidate, "ra").Replayed!, b], await ParentsOfAsync(root, second.Replayed!));
        var step = store.Steps(candidate.Id).Single(each => each is { Step: IntegrationStepNames.Replay, Request: "rb" });
        Assert.Contains("set apart", step.Said, StringComparison.Ordinal);
        Assert.Contains("docs/archive.md:", step.Said, StringComparison.Ordinal);
        Assert.Equal("", await StatusAsync(run.Tree!));
        Assert.Equal(Member(store, candidate, "rc").Replayed, candidate.Tip);
    }

    /// <summary>
    /// 🔴 The twin of the merge tool's "the orientation index is written into each merge from the merged tree, and a conflict in it is resolved by
    /// that writing": a conflict only in a generated file takes the candidate's copy and goes on; where the person accepted the declaration for
    /// integration, the writer writes it again from the merged tree, inside the member's merge commit; a writer is never read from anything else.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_conflict_only_in_a_generated_file_takes_the_candidates_copy_and_is_written_again_where_its_writer_is_accepted(bool accepted)
    {
        var root = await RepositoryAsync("engine");
        var declared = await GeneratedDeclaredAsync(root);
        var a = await BranchAsync(root, "feature/a", ("a.txt", "a\n"), ("index/map.md", "shared.txt\na.txt\n"));
        var b = await BranchAsync(root, "feature/b", ("b.txt", "b\n"), ("index/map.md", "shared.txt\nb.txt\n"));
        var c = await BranchAsync(root, "feature/c", ("c.txt", "c\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", a, 1);
        Queue(store, "rb", "feature/b", b, 2);
        Queue(store, "rc", "feature/c", c, 3);
        var config = accepted ? DriverConfig.Empty.WithIntegrationGates("engine", declared) : null;

        var run = await Replays(store).RunAsync(Ask(root, config: config));

        var candidate = store.Candidate(run.Candidate!.Id)!;
        var second = Member(store, candidate, "rb");
        Assert.Equal(IntegrationMemberStates.Replayed, second.State);
        Assert.Equal([Member(store, candidate, "ra").Replayed!, b], await ParentsOfAsync(root, second.Replayed!));
        Assert.Equal(accepted ? "a.txt\nb.txt\nshared.txt\n" : "shared.txt\na.txt\n", await ShowAsync(root, second.Replayed!, "index/map.md"));
        Assert.Equal("b\n", await ShowAsync(root, second.Replayed!, "b.txt"));
        var step = store.Steps(candidate.Id).Single(each => each is { Step: IntegrationStepNames.Replay, Request: "rb" });
        Assert.Contains("`index/map.md` took the candidate's copy", step.Said, StringComparison.Ordinal);
        Assert.Equal(accepted, step.Said!.Contains($"written again from the merged tree by `{Writer}`", StringComparison.Ordinal));
        Assert.Equal(Member(store, candidate, "rc").Replayed, candidate.Tip);
        Assert.Equal("", await StatusAsync(run.Tree!));
        await AssertNoMergeAsync(run.Tree!);
    }

    /// <summary>
    /// 🔴 A declared line rule (INTEGRATE1d, §5.2 step 4's <c>run</c> writer): both members change only the line its pattern matches, which the
    /// merge by the base settles for its writer to write again from the merged tree, inside the member's merge commit; the rule is read from the
    /// declaration the person accepted for integration, at the line, or it settles nothing and the file is a conflict.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_conflict_in_the_line_a_declared_rule_matches_is_settled_and_written_again_only_where_its_declaration_is_accepted(bool accepted)
    {
        var root = await RepositoryAsync("engine");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        await File.WriteAllTextAsync(Path.Combine(root, "docs", "STATUS.md"), "Status: base\n");
        await CommitAsync(
            root, "daoris.gates.json",
            """{ "integration": { "merge": [ { "paths": "docs/STATUS.md", "line": "^Status: ", "run": "git ls-files -- *.txt > docs/STATUS.md" } ] } }""" + "\n",
            "the declaration");
        var declared = (await GitAsync(root, "rev-parse", "HEAD:daoris.gates.json")).Trim();
        var a = await BranchAsync(root, "feature/a", ("a.txt", "a\n"), ("docs/STATUS.md", "Status: a\n"));
        var b = await BranchAsync(root, "feature/b", ("b.txt", "b\n"), ("docs/STATUS.md", "Status: b\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", a, 1);
        Queue(store, "rb", "feature/b", b, 2);
        var config = accepted ? DriverConfig.Empty.WithIntegrationGates("engine", declared) : null;

        var run = await Replays(store).RunAsync(Ask(root, config: config));

        var candidate = store.Candidate(run.Candidate!.Id)!;
        var second = Member(store, candidate, "rb");
        if (accepted)
        {
            Assert.Equal(IntegrationMemberStates.Replayed, second.State);
            Assert.Equal([Member(store, candidate, "ra").Replayed!, b], await ParentsOfAsync(root, second.Replayed!));
            Assert.Equal("a.txt\nb.txt\nshared.txt\n", await ShowAsync(root, second.Replayed!, "docs/STATUS.md"));
            var step = store.Steps(candidate.Id).Single(each => each is { Step: IntegrationStepNames.Replay, Request: "rb" });
            Assert.Contains("The declared line rule settled `docs/STATUS.md`", step.Said, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(IntegrationMemberStates.Held, second.State);
            Assert.Contains("docs/STATUS.md", second.Said, StringComparison.Ordinal);
        }

        Assert.Equal("", await StatusAsync(run.Tree!));
        await AssertNoMergeAsync(run.Tree!);
    }

    /// <summary>
    /// 🔴 A real conflict under <c>conflicts: hold</c> (§5.4): nobody is told, the member is set aside in its own state with the conflict said, its
    /// request stays open and standing on the line, and the members after it go on in a clean tree. Its request is not queued again, so the next
    /// candidate does not form it to conflict and hold it for ever.
    /// </summary>
    [Fact]
    public async Task A_conflict_under_hold_sets_the_member_aside_with_its_request_standing_and_the_rest_go_on()
    {
        var root = await RepositoryAsync("engine");
        var a = await BranchAsync(root, "feature/a", ("shared.txt", "from a\n"));
        var b = await BranchAsync(root, "feature/b", ("shared.txt", "from b\n"));
        var c = await BranchAsync(root, "feature/c", ("c.txt", "c\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", a, 1, session: "s-a", conflicts: LandingIntegrate.Hold);
        Queue(store, "rb", "feature/b", b, 2, session: "s-b", conflicts: LandingIntegrate.Hold);
        Queue(store, "rc", "feature/c", c, 3, session: "s-c", conflicts: LandingIntegrate.Hold);
        var door = new Door();

        var run = await Replays(store).RunAsync(Ask(root, door));

        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        var candidate = store.Candidate(run.Candidate!.Id)!;
        Assert.True(candidate.Stands);
        var held = Member(store, candidate, "rb");
        var third = Member(store, candidate, "rc");
        Assert.Equal((IntegrationMemberStates.Held, true, null), (held.State, held.Stands, held.Replayed));
        Assert.Contains("shared.txt", held.Said, StringComparison.Ordinal);
        Assert.Contains("held", held.Said, StringComparison.OrdinalIgnoreCase);
        Assert.Null(store.Request("rb")!.ClosedAt);
        Assert.Empty(door.Told);
        Assert.Equal(0, door.Calls);
        Assert.Equal([Member(store, candidate, "ra").Replayed!, c], await ParentsOfAsync(root, third.Replayed!));
        Assert.Equal(third.Replayed, candidate.Tip);
        Assert.Equal("", await StatusAsync(run.Tree!));
        await AssertNoMergeAsync(run.Tree!);
        Assert.Equal(
            [(IntegrationStepNames.Replay, "rb", IntegrationStepOutcomes.Done), (IntegrationStepNames.Conflict, "rb", IntegrationStepOutcomes.Done)],
            store.Steps(candidate.Id).Where(each => each.Request == "rb").Select(each => (each.Step, each.Request, each.Outcome)));
        Assert.Empty(store.Queued("engine", "main").Where(each => each.Id == "rb"));
    }

    /// <summary>
    /// 🔴 A session the door could not reach, or a request with no session to tell (a records branch), cannot rebase: the member is held for the
    /// steward's or the owner's act as under <c>hold</c>, shown with the conflict and the door's reason, never handed back to nobody.
    /// </summary>
    [Theory]
    [InlineData("door-refuses")]
    [InlineData("no-session")]
    [InlineData("no-door")]
    public async Task A_conflict_whose_session_cannot_be_told_holds_the_member_for_an_act(string why)
    {
        var root = await RepositoryAsync("engine");
        var a = await BranchAsync(root, "feature/a", ("shared.txt", "from a\n"));
        var b = await BranchAsync(root, "feature/b", ("shared.txt", "from b\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", a, 1, session: "s-a");
        Queue(store, "rb", "feature/b", b, 2, session: why == "no-session" ? null : "s-b");
        var door = new Door(keeps: why != "door-refuses");

        var run = await Replays(store).RunAsync(Ask(root, why == "no-door" ? null : door));

        var candidate = store.Candidate(run.Candidate!.Id)!;
        var held = Member(store, candidate, "rb");
        Assert.Equal((IntegrationMemberStates.Held, true), (held.State, held.Stands));
        Assert.Contains("shared.txt", held.Said, StringComparison.Ordinal);
        if (why == "door-refuses") Assert.Contains("its conversation is gone", held.Said, StringComparison.Ordinal);
        Assert.Null(store.Request("rb")!.ClosedAt);
        Assert.Empty(door.Told);
        Assert.Equal("", await StatusAsync(run.Tree!));
    }

    /// <summary>
    /// 🔴 A session is told once per tip: the words are keyed by the session and the frozen tip (the live branch's tip, moved since, is not the key),
    /// and the same tip conflicting in a later candidate, its request made again, says the same key, which the door keeps once.
    /// </summary>
    [Fact]
    public async Task A_session_is_told_once_per_frozen_tip_and_the_same_tip_conflicting_again_keeps_the_same_key()
    {
        var root = await RepositoryAsync("engine");
        var a = await BranchAsync(root, "feature/a", ("shared.txt", "from a\n"));
        var b = await BranchAsync(root, "feature/b", ("shared.txt", "from b\n"));
        await GitAsync(root, "checkout", "--quiet", "feature/b");
        await CommitAsync(root, "later.txt", "made after the request froze\n", "after the freeze");
        await GitAsync(root, "checkout", "--quiet", "main");
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", a, 1, session: "s-a");
        Queue(store, "rb", "feature/b", b, 2, session: "s-b");
        var door = new Door();

        var first = await Replays(store).RunAsync(Ask(root, door));

        Assert.Equal(IntegrationRequestCloses.HandedBack, store.Request("rb")!.ClosedWhy);
        Assert.Equal(RebaseKeys.Of("s-b", b), Assert.Single(door.Told).Key);

        // The candidate promotes nothing here; end it so the line is free (its first member is queued again), then the session's next landing
        // asks again at the same tip, and the same tip conflicts again.
        var ending = store.Begin(first.Claim!, IntegrationStepNames.Settle);
        store.End(first.Claim!, ending.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Held) { Ends = IntegrationCandidateEnds.Dropped });
        Queue(store, "rb2", "feature/b", b, 3, session: "s-b");
        var second = await Replays(store).RunAsync(Ask(root, door));

        Assert.Equal(IntegrationReplayRuns.Replayed, second.Kind);
        Assert.Equal(IntegrationMemberStates.HandedBack, Member(store, second.Candidate!, "rb2").State);
        Assert.Equal(2, door.Calls);
        Assert.Single(door.Told);
    }

    /// <summary>
    /// 🔴 A crash inside the resolve (the merge made and settled, its commit not yet): another process takes the candidate, the tree goes back to
    /// the step's recorded start and the member is merged and settled again as the step's next attempt, one merge commit for it. A crash in the
    /// conflict step after the session was told tells again at the same key, which the door keeps once, and the member is handed back.
    /// </summary>
    [Theory]
    [InlineData("after-resolve")]
    [InlineData("after-tell")]
    public async Task A_crash_in_the_resolve_or_after_the_telling_is_run_again_from_the_recorded_start(string crash)
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "TASKS.md", Rows, "the backlog");
        var conflicting = crash == "after-tell";
        var a = await BranchAsync(root, "feature/a", conflicting ? ("shared.txt", "from a\n") : ("TASKS.md", Rows.Replace("- [ ] **A2** two.\n", "")));
        var b = await BranchAsync(root, "feature/b", conflicting ? ("shared.txt", "from b\n") : ("TASKS.md", Rows.Replace("**A1** one.\n", "**A1** one.\n- [ ] **B1** b's.\n")));
        var first = Store("run-a");
        Queue(first, "ra", "feature/a", a, 1, session: "s-a");
        Queue(first, "rb", "feature/b", b, 2, session: "s-b");
        var door = new Door();
        var dying = Replays(first);
        // The resolve's point is passed by each member's replay: the second's is the one that dies.
        var seen = 0;
        dying.Seam = point => point == crash && ++seen == (conflicting ? 1 : 2) ? throw new Killed() : Task.CompletedTask;
        await Assert.ThrowsAsync<Killed>(() => dying.RunAsync(Ask(root, door)));

        var old = first.Standing().Single();
        var open = first.OpenStep(old.Id)!;
        Assert.Equal((conflicting ? IntegrationStepNames.Conflict : IntegrationStepNames.Replay, "rb", 1), (open.Step, open.Request, open.Attempt));

        var store = Taker();
        var claim = store.Take(old.Id, old.ClaimGeneration).Claim!;
        var run = await Replays(store).ContinueAsync(claim, Ask(root, door));

        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        var candidate = store.Candidate(old.Id)!;
        var second = Member(store, candidate, "rb");
        if (conflicting)
        {
            Assert.Equal(IntegrationMemberStates.HandedBack, second.State);
            Assert.Equal(2, door.Calls);
            Assert.Single(door.Told);
        }
        else
        {
            Assert.Equal(IntegrationMemberStates.Replayed, second.State);
            Assert.Equal([Member(store, candidate, "ra").Replayed!, b], await ParentsOfAsync(root, second.Replayed!));
            Assert.Equal("# Tasks\n\n- [ ] **A1** one.\n- [ ] **B1** b's.\n- [ ] **A3** three.\n", await ShowAsync(root, second.Replayed!, "TASKS.md"));
            Assert.Equal(second.Replayed, await CommitOfAsync(run.Tree!, "HEAD"));
        }

        Assert.Equal("", await StatusAsync(run.Tree!));
        await AssertNoMergeAsync(run.Tree!);
        Assert.Equal(2, store.Steps(old.Id).Count(each => each.Request == "rb" && each.Step == open.Step && each.Outcome is IntegrationStepOutcomes.Done or IntegrationStepOutcomes.HandedBack or IntegrationStepOutcomes.Interrupted));
    }

    /// <summary>
    /// 🔴 A crash inside the second member's replay, once its merge is made (the tree mid-merge) or its commit is (the tree one commit on),
    /// before its end is written: another process takes the candidate and resumes it, the tree goes back to the step's recorded start, and the
    /// step is run again as its next attempt, so the candidate's chain holds one merge for the member and the first member's replay stands.
    /// </summary>
    [Theory]
    [InlineData("after-merge")]
    [InlineData("after-commit")]
    public async Task A_crash_after_the_replays_start_and_before_its_end_is_run_again_from_the_recorded_start(string crash)
    {
        var root = await RepositoryAsync("engine");
        var @base = await CommitOfAsync(root, "main");
        var a = await BranchAsync(root, "feature/a", ("a.txt", "a\n"));
        var b = await BranchAsync(root, "feature/b", ("b.txt", "b\n"));
        var first = Store("run-a");
        Queue(first, "ra", "feature/a", a, 1);
        Queue(first, "rb", "feature/b", b, 2);
        var seen = 0;
        var dying = Replays(first);
        dying.Seam = point => point == crash && ++seen == 2 ? throw new Killed() : Task.CompletedTask;
        await Assert.ThrowsAsync<Killed>(() => dying.RunAsync(Ask(root)));

        var old = first.Standing().Single();
        var recorded = first.Members(old.Id).Single(each => each.Request == "ra").Replayed!;
        Assert.Equal(recorded, old.Tip);
        var open = first.OpenStep(old.Id)!;
        Assert.Equal((IntegrationStepNames.Replay, "rb", 1), (open.Step, open.Request, open.Attempt));

        // The come-up: the taker reads run-a as dead, takes the candidate and goes on from the resume.
        var store = Taker();
        var claim = store.Take(old.Id, old.ClaimGeneration).Claim!;
        var run = await Replays(store).ContinueAsync(claim, Ask(root));

        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        var candidate = store.Candidate(old.Id)!;
        var second = Member(store, candidate, "rb");
        Assert.Equal(IntegrationMemberStates.Replayed, second.State);
        Assert.Equal(second.Replayed, candidate.Tip);
        Assert.Equal([recorded, b], await ParentsOfAsync(root, second.Replayed!));
        Assert.Equal(second.Replayed, await CommitOfAsync(run.Tree!, "HEAD"));
        Assert.Equal("", await StatusAsync(run.Tree!));
        Assert.Equal(@base, await CommitOfAsync(root, "main"));
        Assert.Equal(
            [(IntegrationStepNames.Replay, "ra", 1, IntegrationStepOutcomes.Done), (IntegrationStepNames.Replay, "rb", 1, IntegrationStepOutcomes.Interrupted),
                (IntegrationStepNames.Replay, "rb", 2, IntegrationStepOutcomes.Done)],
            store.Steps(old.Id).Where(each => each.Step == IntegrationStepNames.Replay).Select(each => (each.Step, each.Request, each.Attempt, each.Outcome)));
        Assert.Equal(1, store.Steps(old.Id).Count(each => each.Step == IntegrationStepNames.Form));
    }

    /// <summary>
    /// A line with nothing queued forms nothing; a candidate already standing is answered, with no claim, and nothing is written; the tree
    /// stays warm for the next candidate, which finds it at the same place and makes none beside it.
    /// </summary>
    [Fact]
    public async Task A_standing_candidate_is_answered_and_the_next_candidate_finds_the_tree_warm()
    {
        var root = await RepositoryAsync("engine");
        var a = await BranchAsync(root, "feature/a", ("a.txt", "a\n"));
        var b = await BranchAsync(root, "feature/b", ("b.txt", "b\n"));
        var store = Store("run-a");
        var replays = Replays(store);
        Assert.Equal(IntegrationReplayRuns.Idle, (await replays.RunAsync(Ask(root))).Kind);
        Assert.Empty(store.Standing());

        Queue(store, "ra", "feature/a", a, 1);
        var first = await replays.RunAsync(Ask(root));
        Queue(store, "rb", "feature/b", b, 2);
        var standing = await replays.RunAsync(Ask(root));
        Assert.Equal(IntegrationReplayRuns.Standing, standing.Kind);
        Assert.Null(standing.Claim);
        Assert.Equal(first.Candidate!.Id, standing.Candidate!.Id);
        Assert.Single(store.Standing());

        // The first candidate formed again (its request is queued again beside the new one); the second finds the warm tree.
        await new IntegrationSteps(store).RunAsync(
            first.Claim!, IntegrationStepNames.Settle,
            (_, _) => Task.FromResult(new IntegrationStepEnd(IntegrationStepOutcomes.Held) { Ends = IntegrationCandidateEnds.FormedAgain }));
        var second = await replays.RunAsync(Ask(root));

        Assert.Equal(IntegrationReplayRuns.Replayed, second.Kind);
        Assert.NotEqual(first.Candidate.Id, second.Candidate!.Id);
        Assert.Equal(first.Tree, second.Tree);
        Assert.Equal(1, (await GitAsync(root, "worktree", "list", "--porcelain")).Split('\n').Count(each => each.Contains($"/{IntegrationReplays.Prefix}", StringComparison.Ordinal)));
        Assert.Equal(second.Candidate.Tip, await CommitOfAsync(second.Tree!, "HEAD"));
    }

    /// <summary>A folder where the tree goes that git does not list is never taken over: the form step is held, said why, and nothing is merged in it.</summary>
    [Fact]
    public async Task A_folder_git_does_not_list_is_left_alone_and_the_form_is_held()
    {
        var root = await RepositoryAsync("engine");
        var a = await BranchAsync(root, "feature/a", ("a.txt", "a\n"));
        var store = Store("run-a");
        Queue(store, "ra", "feature/a", a, 1);
        var replays = Replays(store);
        var tree = replays.TreeOf("aurora", "engine", "main");
        Directory.CreateDirectory(tree);
        await File.WriteAllTextAsync(Path.Combine(tree, "someones.txt"), "not Daoris's\n");

        var run = await replays.RunAsync(Ask(root));

        Assert.Equal(IntegrationReplayRuns.Held, run.Kind);
        Assert.Contains("git does not list it", run.Said, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(tree, "someones.txt")));
        var step = Assert.Single(store.Steps(run.Candidate!.Id));
        Assert.Equal((IntegrationStepNames.Form, IntegrationStepOutcomes.Held), (step.Step, step.Outcome));
        Assert.Equal(IntegrationMemberStates.Formed, Member(store, run.Candidate, "ra").State);
    }
}
