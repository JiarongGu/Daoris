using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1e part e4b over real git (D215's note; the integration design §5.2 steps 8 to 11, §5.6, §5.7, §8): a superseded member's work
/// kept on a recovery ref with its disposition pending in the settle's own end, so the member settles and the candidate no longer stands for
/// it; a crash between the ref and the disposition repaired by the rerun; the come-up's sweep of standing candidates through
/// <c>Take</c> and <c>ResumeAsync</c>; the tidy step through the landing's own clean-up and the close step that ends the candidate; and the
/// requester's authority read again at promotion. Called from no production path. The verdict port is the test's stub.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationSettleGitTests : LandedFixture, IAsyncLifetime
{
    private sealed class Killed() : Exception("the process died here");

    private IIntegrationVerdicts? _installed;

    public Task InitializeAsync()
    {
        try
        {
            _installed = IntegrationVerdicts.Current;
        }
        catch (InvalidOperationException)
        {
            _installed = null;
        }

        return Task.CompletedTask;
    }

    /// <summary>The port Review installed is put back: the stub is this class's alone.</summary>
    public Task DisposeAsync()
    {
        if (_installed is not null) IntegrationVerdicts.Current = _installed;
        return Task.CompletedTask;
    }

    private IntegrationStore Store(string run, Func<StoreRun, bool>? alive = null) => new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    /// <summary>The second process's store: run-b, which reads run-a as dead.</summary>
    private IntegrationStore Taker() => Store("run-b", run => run.Run != "run-a");

    private string TreesHome => DaorisHome.Entry(Home, HomeEntry.Trees);

    private const string Gates = """{ "gates": [ { "name": "unit", "run": "node check.mjs" } ] }""" + "\n";

    private const string Lines20 = "line 1\nline 2\nline 3\nline 4\nline 5\nline 6\nline 7\nline 8\nline 9\nline 10\nline 11\nline 12\nline 13\nline 14\nline 15\nline 16\nline 17\nline 18\nline 19\nline 20\n";

    private static string Lines(params (int Line, string Text)[] changes)
    {
        var lines = Enumerable.Range(1, 20).Select(each => $"line {each}").ToArray();
        foreach (var (line, text) in changes) lines[line - 1] = text;
        return string.Join("\n", lines) + "\n";
    }

    private static async Task<string> CommitOfAsync(string cwd, string reference) => (await GitAsync(cwd, "rev-parse", reference)).Trim();

    private static readonly LandingIntegrate Declared = new(Batch: 8, Gates: LandingIntegrate.Declared);

    private static readonly LandingIntegrate NoGates = new(Batch: 8, Gates: LandingIntegrate.NoGates);

    private static IntegrationRequest Request(
        string id, string branch, string tip, int order, LandingIntegrate integrate, IntegrationRequester? requester = null, string? session = null) =>
        new(id, "engine", "aurora", branch, tip, IntegrationRoutes.Recorded, "main", requester ?? IntegrationRequester.Card, integrate.Resolved(),
            new DateTimeOffset(2026, 10, 30, 9, 0, order, TimeSpan.Zero)) { Session = session };

    /// <summary>A branch from <c>main</c> with one commit for each file given, the checkout left on <c>main</c>; the branch's tip.</summary>
    private static async Task<string> BranchAsync(string root, string name, params (string File, string Content)[] commits)
    {
        await GitAsync(root, "checkout", "--quiet", "-b", name, "main");
        foreach (var (file, content) in commits) await CommitAsync(root, file, content, $"work on {name}: {file}");
        var tip = await CommitOfAsync(root, name);
        await GitAsync(root, "checkout", "--quiet", "main");
        return tip;
    }

    /// <summary>A candidate formed and replayed on the line.</summary>
    private sealed record Candidate(string Root, string Base, IntegrationStore Store, IntegrationClaim Claim, IntegrationPromoteAsk Ask)
    {
        public IntegrationCandidate Now => Store.Candidate(Claim.Candidate)!;

        public IntegrationMember Member(string request) => Store.Members(Claim.Candidate).Single(each => each.Request == request);

        public string Tip => Now.Tip!;

        public IReadOnlyList<(string Step, string? Outcome)> Written => [.. Store.Steps(Claim.Candidate).Select(each => (each.Step, each.Outcome))];

        public IntegrationStep Last(string step) => Store.Steps(Claim.Candidate).Last(each => each.Step == step);
    }

    private async Task<Candidate> FormedAsync(string root, string @base, IntegrationStore store, DriverConfig config, bool detached = true)
    {
        var run = await new IntegrationReplays(store, TreesHome).RunAsync(new IntegrationReplayAsk("engine", "aurora", root, "main") { Config = config });
        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        if (detached) await GitAsync(root, "checkout", "--quiet", "--detach");
        return new Candidate(root, @base, store, run.Claim!, new IntegrationPromoteAsk("engine", "aurora", root, "main", config, Home));
    }

    /// <summary>One member's branch (<c>feature/a</c>) made from <c>main</c> with a file of its own, queued and replayed, under <paramref name="integrate"/>.</summary>
    private async Task<Candidate> SingleAsync(LandingIntegrate? integrate = null, IntegrationRequester? requester = null)
    {
        var root = await RepositoryAsync("engine");
        var @base = await CommitOfAsync(root, "main");
        var store = Store("run-a");
        var tip = await BranchAsync(root, "feature/a", ("a.txt", "a\n"));
        Assert.Null(store.Add(Request("ra", "feature/a", tip, 1, integrate ?? NoGates, requester)));
        return await FormedAsync(root, @base, store, DriverConfig.Empty);
    }

    private sealed class Verdict : IIntegrationVerdicts
    {
        public int Asks { get; private set; }

        public Task<IntegrationVerdict> JudgeAsync(IntegrationVerdictAsk ask)
        {
            Asks++;
            return Task.FromResult(new IntegrationVerdict(VerdictActions.Green));
        }
    }

    private static Verdict Green()
    {
        var verdict = new Verdict();
        IntegrationVerdicts.Current = verdict;
        return verdict;
    }

    /// <summary>The unit's branch cut from the line's twenty lines, frozen after one commit; the line then changes other lines outside the unit's work.</summary>
    private async Task<(Candidate Candidate, string X)> LinedAsync()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "lines.txt", Lines20, "twenty lines");
        await CommitAsync(root, "daoris.gates.json", Gates, "the declaration");
        var blob = (await GitAsync(root, "rev-parse", "HEAD:daoris.gates.json")).Trim();
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/unit");
        await CommitAsync(root, "lines.txt", Lines((2, "two, the unit's")), "the unit's work");
        var frozen = await CommitOfAsync(root, "HEAD");
        await GitAsync(root, "checkout", "--quiet", "main");
        await CommitAsync(root, "lines.txt", Lines((8, "eight, x's")), "x");
        var x = await CommitOfAsync(root, "HEAD");
        await CommitAsync(root, "lines.txt", Lines((8, "eight, x's"), (12, "twelve, y's")), "y");
        var @base = await CommitOfAsync(root, "main");

        var store = Store("run-a");
        Assert.Null(store.Add(Request("ra", "feature/unit", frozen, 1, Declared)));
        var config = DriverConfig.Empty.WithIntegrationGates("engine", blob);
        return (await FormedAsync(root, @base, store, config), x);
    }

    /// <summary>The unit's branch after its request froze: one commit for each change given, in order; the checkout left detached on the line.</summary>
    private static async Task CommitAfterTheFreezeAsync(string root, params (string File, string Content, string Message)[] commits)
    {
        await GitAsync(root, "checkout", "--quiet", "feature/unit");
        foreach (var (file, content, message) in commits) await CommitAsync(root, file, content, message);
        await GitAsync(root, "checkout", "--quiet", "--detach", "main");
    }

    private IntegrationPromotes Promotes(IntegrationStore store) => new(store, TreesHome);

    private static async Task<string[]> RecoveryRefsAsync(string root) =>
        (await GitAsync(root, "for-each-ref", "--format=%(refname) %(objectname)", "refs/daoris/discarded/")).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // ——— Superseded (§5.7).

    /// <summary>
    /// 🔴 A superseded commit's work is kept on a recovery ref under <c>refs/daoris/discarded/</c> and its disposition is pending, in the settle's
    /// own end: the member settles, its request closes, and the candidate no longer stands for it. Nothing is dropped: the branch stays, and the
    /// disposition is still pending once the request is closed.
    /// </summary>
    [Fact]
    public async Task A_superseded_member_s_work_is_kept_on_a_recovery_ref_with_its_disposition_pending_and_the_candidate_no_longer_stands()
    {
        var (candidate, x) = await LinedAsync();
        await CommitAfterTheFreezeAsync(candidate.Root, ("lines.txt", Lines((2, "two, the unit's"), (8, "eight, the unit's")), "line 8, the unit's"));
        var branchTip = await CommitOfAsync(candidate.Root, "feature/unit");
        Green();

        var run = await Promotes(candidate.Store).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        Assert.Equal(candidate.Tip, await CommitOfAsync(candidate.Root, "main"));
        Assert.False(candidate.Now.Stands);
        Assert.Equal(IntegrationCandidateEnds.Settled, candidate.Now.EndedWhy);
        Assert.Null(candidate.Store.StandingFor("engine", "main"));
        Assert.Equal(IntegrationMemberStates.Settled, candidate.Member("ra").State);
        Assert.Equal(IntegrationRequestCloses.Settled, candidate.Store.Request("ra")!.ClosedWhy);
        Assert.Equal([$"refs/daoris/discarded/feature/unit {branchTip}"], await RecoveryRefsAsync(candidate.Root));
        Assert.Equal(branchTip, await CommitOfAsync(candidate.Root, "feature/unit"));

        var pending = Assert.Single(candidate.Store.Pending());
        Assert.Equal(("ra", branchTip, "refs/daoris/discarded/feature/unit", x, true), (pending.Request, pending.Commit, pending.Reference, pending.By, pending.Pending));
        Assert.Equal(["lines.txt"], pending.Files);
        Assert.Equal("line 8, the unit's", pending.Subject);
        Assert.Equal(candidate.Claim.Candidate, pending.Candidate);
        Assert.Contains("superseded", candidate.Last(IntegrationStepNames.Settle).Said, StringComparison.Ordinal);
        Assert.Contains(x[..8], candidate.Last(IntegrationStepNames.Settle).Said, StringComparison.Ordinal);
        Assert.Contains("refs/daoris/discarded/feature/unit", candidate.Last(IntegrationStepNames.Settle).Said, StringComparison.Ordinal);
        Assert.Equal(
            [
                IntegrationStepNames.Form, IntegrationStepNames.Replay, IntegrationStepNames.Promote, IntegrationStepNames.Settle,
                IntegrationStepNames.Tidy, IntegrationStepNames.Close,
            ],
            candidate.Written.Select(each => each.Step));
    }

    /// <summary>
    /// 🔴 A crash between the recovery ref and the settle's end leaves the ref and no disposition; the rerun finds the ref it made, writes the one
    /// disposition, and makes no second ref.
    /// </summary>
    [Fact]
    public async Task A_crash_between_the_recovery_ref_and_the_disposition_leaves_one_ref_which_the_rerun_repairs()
    {
        var (candidate, x) = await LinedAsync();
        await CommitAfterTheFreezeAsync(candidate.Root, ("lines.txt", Lines((2, "two, the unit's"), (8, "eight, the unit's")), "line 8, the unit's"));
        var branchTip = await CommitOfAsync(candidate.Root, "feature/unit");
        Green();
        var first = Promotes(candidate.Store);
        first.Seam = point => point == "after-keep" ? throw new Killed() : Task.CompletedTask;
        await Assert.ThrowsAsync<Killed>(() => first.RunAsync(candidate.Claim, candidate.Ask));

        Assert.Equal([$"refs/daoris/discarded/feature/unit {branchTip}"], await RecoveryRefsAsync(candidate.Root));
        Assert.Empty(candidate.Store.Pending());
        Assert.True(candidate.Store.OpenStep(candidate.Claim.Candidate)!.Open);
        Assert.Equal(IntegrationMemberStates.Promoted, candidate.Member("ra").State);

        var store = Taker();
        var claim = store.Take(candidate.Claim.Candidate, candidate.Claim.Generation).Claim!;
        var run = await new IntegrationPromotes(store, TreesHome).RunAsync(claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        Assert.Equal([$"refs/daoris/discarded/feature/unit {branchTip}"], await RecoveryRefsAsync(candidate.Root));
        var pending = Assert.Single(store.Pending());
        Assert.Equal((branchTip, x), (pending.Commit, pending.By));
        Assert.Equal(IntegrationCandidateEnds.Settled, store.Candidate(claim.Candidate)!.EndedWhy);
        Assert.Equal(
            [(IntegrationStepOutcomes.Interrupted), (IntegrationStepOutcomes.Done)],
            store.Steps(claim.Candidate).Where(each => each.Step == IntegrationStepNames.Settle).Select(each => each.Outcome!));
    }

    /// <summary>
    /// 🔴 A member whose work cannot be kept on a recovery ref is never settled: git refusing every name for the ref leaves the member standing,
    /// its request open, no disposition written and the settle step held, said why; nothing is dropped.
    /// </summary>
    [Fact]
    public async Task A_member_whose_recovery_ref_cannot_be_made_is_not_settled_and_nothing_is_dropped()
    {
        var (candidate, _) = await LinedAsync();
        await CommitAfterTheFreezeAsync(candidate.Root, ("lines.txt", Lines((2, "two, the unit's"), (8, "eight, the unit's")), "line 8, the unit's"));
        // A ref named `refs/daoris/discarded/feature` makes every name under it a directory/file conflict, so no recovery ref can be made.
        await GitAsync(candidate.Root, "update-ref", "refs/daoris/discarded/feature", await CommitOfAsync(candidate.Root, "main"));
        Green();

        var run = await Promotes(candidate.Store).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Unsettled, run.Kind);
        Assert.Equal(candidate.Tip, await CommitOfAsync(candidate.Root, "main"));
        Assert.True(candidate.Now.Stands);
        Assert.Equal(IntegrationMemberStates.Promoted, candidate.Member("ra").State);
        Assert.Null(candidate.Store.Request("ra")!.ClosedAt);
        Assert.Empty(candidate.Store.Pending());
        var settle = candidate.Last(IntegrationStepNames.Settle);
        Assert.Equal(IntegrationStepOutcomes.Held, settle.Outcome);
        Assert.Contains("recovery ref", settle.Said, StringComparison.Ordinal);
        Assert.DoesNotContain(candidate.Written, each => each.Step is IntegrationStepNames.Tidy or IntegrationStepNames.Close);
        Assert.NotEqual("", await CommitOfAsync(candidate.Root, "feature/unit"));
    }

    // ——— The tidy and the close (§5.2 steps 10 and 11).

    /// <summary>
    /// 🔴 The tidy step removes the landed branch only through the landing's own clean-up, named alone: the branch a candidate promoted goes (its
    /// work is on the line), and a landed branch the candidate did not carry, though the clean-up would clear it, stays. The close step ends the
    /// candidate, and what it says is written from the record.
    /// </summary>
    [Fact]
    public async Task The_tidy_removes_the_promoted_landed_branch_through_the_landing_clean_up_alone_and_the_close_ends_the_candidate()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var carried = await LandAsync(trees, root, "a.txt", "a\n", new LandingSubject("s1", "q1", "Fix a"));
        var other = await LandAsync(trees, root, "b.txt", "b\n", new LandingSubject("s2", "q2", "Fix b"));
        await SquashAsync(root, other.Branch!);
        var @base = await CommitOfAsync(root, "main");
        var store = Store("run-a");
        Assert.Null(store.Add(Request("ra", carried.Branch!, await CommitOfAsync(root, carried.Branch!), 1, NoGates, session: "s1")));
        var candidate = await FormedAsync(root, @base, store, DriverConfig.Empty);
        var ask = candidate.Ask with { Tidy = IntegrationTidies.Through(trees) };

        var run = await Promotes(store).RunAsync(candidate.Claim, ask);

        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        Assert.Equal("", (await GitAsync(root, "branch", "--list", carried.Branch!)).Trim());
        Assert.NotEqual("", (await GitAsync(root, "branch", "--list", other.Branch!)).Trim());
        var tidy = candidate.Last(IntegrationStepNames.Tidy);
        Assert.Equal(IntegrationStepOutcomes.Done, tidy.Outcome);
        Assert.Contains(carried.Branch!, tidy.Said, StringComparison.Ordinal);
        Assert.DoesNotContain(other.Branch!, tidy.Said, StringComparison.Ordinal);
        var close = candidate.Last(IntegrationStepNames.Close);
        Assert.Equal(IntegrationStepOutcomes.Done, close.Outcome);
        Assert.Contains("request ra", close.Said, StringComparison.Ordinal);
        Assert.False(candidate.Now.Stands);
        Assert.Equal(IntegrationCandidateEnds.Settled, candidate.Now.EndedWhy);
        Assert.Equal(candidate.Tip, await CommitOfAsync(root, "main"));
    }

    /// <summary>
    /// 🔴 With no landing clean-up handed in, the tidy removes nothing and says so; the tree is left warm and the candidate still closes. A
    /// records branch no landing made is no landed branch, so there is nothing to tidy for it either.
    /// </summary>
    [Fact]
    public async Task With_no_landing_clean_the_tidy_removes_nothing_and_the_candidate_still_closes()
    {
        var candidate = await SingleAsync();
        var tree = Assert.Single(Directory.GetDirectories(Path.Combine(TreesHome, "aurora", "engine"), "i-*"));

        var run = await Promotes(candidate.Store).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        Assert.NotEqual("", (await GitAsync(candidate.Root, "branch", "--list", "feature/a")).Trim());
        Assert.Equal(IntegrationStepOutcomes.Done, candidate.Last(IntegrationStepNames.Tidy).Outcome);
        Assert.Contains("warm", candidate.Last(IntegrationStepNames.Tidy).Said, StringComparison.Ordinal);
        Assert.True(Directory.Exists(tree));
        Assert.Equal(IntegrationCandidateEnds.Settled, candidate.Now.EndedWhy);
    }

    /// <summary>
    /// 🔴 A crash inside the tidy is resumed from the step after the settle: the tidy runs again as its next attempt and the close ends the
    /// candidate; the line moved once and the settle is not made again.
    /// </summary>
    [Fact]
    public async Task A_crash_inside_the_tidy_is_resumed_by_the_sweep_from_the_tidy_with_no_second_settle()
    {
        var candidate = await SingleAsync();
        var first = Promotes(candidate.Store);
        first.Seam = point => point == "in-tidy" ? throw new Killed() : Task.CompletedTask;
        await Assert.ThrowsAsync<Killed>(() => first.RunAsync(candidate.Claim, candidate.Ask));
        Assert.Equal(IntegrationMemberStates.Settled, candidate.Member("ra").State);
        Assert.True(candidate.Now.Stands);

        var store = Taker();
        var swept = await new IntegrationSweeps(store, TreesHome).SweepAsync(_ => Asks(candidate));

        Assert.Equal(candidate.Claim.Candidate, Assert.Single(swept).Candidate);
        Assert.Equal(IntegrationCandidateEnds.Settled, store.Candidate(candidate.Claim.Candidate)!.EndedWhy);
        Assert.Equal(candidate.Tip, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(
            [(IntegrationStepNames.Tidy, 1, IntegrationStepOutcomes.Interrupted), (IntegrationStepNames.Tidy, 2, IntegrationStepOutcomes.Done)],
            store.Steps(candidate.Claim.Candidate).Where(each => each.Step == IntegrationStepNames.Tidy).Select(each => (each.Step, each.Attempt, each.Outcome!)));
        Assert.Single(store.Steps(candidate.Claim.Candidate), each => each.Step == IntegrationStepNames.Settle);
        Assert.Single(store.Steps(candidate.Claim.Candidate), each => each.Step == IntegrationStepNames.Close);
    }

    // ——— The come-up sweep.

    private static IntegrationSweepAsks Asks(Candidate candidate) =>
        new(new IntegrationReplayAsk("engine", "aurora", candidate.Root, "main") { Config = candidate.Ask.Config }, candidate.Ask);

    /// <summary>
    /// 🔴 The sweep takes each standing candidate from its dead run and resumes it: a promote cut after its start runs again (the line not yet
    /// moved) or is recorded by ancestry (the line moved before its record), the line moving once either way and the promote never run again once
    /// the line holds it; the candidate then settles, tidies and closes.
    /// </summary>
    [Theory]
    [InlineData("after-start")]
    [InlineData("after-move")]
    public async Task The_sweep_resumes_a_promote_cut_after_its_start_with_no_second_move(string crash)
    {
        var (cut, _) = await LinedAsync();
        var root = cut.Root;
        var verdict = Green();
        var first = Promotes(cut.Store);
        first.Seam = point => point == crash ? throw new Killed() : Task.CompletedTask;
        await Assert.ThrowsAsync<Killed>(() => first.RunAsync(cut.Claim, cut.Ask));
        var moved = crash == "after-move";
        Assert.Equal(moved ? cut.Tip : cut.Base, await CommitOfAsync(root, "main"));
        var asks = verdict.Asks;

        var store = Taker();
        var swept = await new IntegrationSweeps(store, TreesHome).SweepAsync(_ => Asks(cut));

        Assert.Equal(cut.Claim.Candidate, Assert.Single(swept).Candidate);
        Assert.Equal(cut.Tip, await CommitOfAsync(root, "main"));
        Assert.Equal(cut.Base, await CommitOfAsync(root, "main^"));
        Assert.Equal(moved ? asks : asks + 1, verdict.Asks);
        Assert.Equal(IntegrationCandidateEnds.Settled, store.Candidate(cut.Claim.Candidate)!.EndedWhy);
        Assert.Equal(
            moved ? [(1, IntegrationStepOutcomes.Promoted)] : [(1, IntegrationStepOutcomes.Interrupted), (2, IntegrationStepOutcomes.Promoted)],
            store.Steps(cut.Claim.Candidate).Where(each => each.Step == IntegrationStepNames.Promote).Select(each => (each.Attempt, each.Outcome!)));
        Assert.Equal(
            [IntegrationStepNames.Settle, IntegrationStepNames.Tidy, IntegrationStepNames.Close],
            store.Steps(cut.Claim.Candidate).Select(each => each.Step).Where(step => step is IntegrationStepNames.Settle or IntegrationStepNames.Tidy or IntegrationStepNames.Close));
    }

    /// <summary>
    /// 🔴 The sweep resumes a replay a crash cut mid-step: the member's replay runs again from its recorded start as the next attempt, then the
    /// candidate goes to its verdict, promotes once and closes.
    /// </summary>
    [Fact]
    public async Task The_sweep_resumes_a_replay_cut_mid_step_and_goes_on_to_the_promotion()
    {
        var root = await RepositoryAsync("engine");
        var @base = await CommitOfAsync(root, "main");
        var first = Store("run-a");
        Assert.Null(first.Add(Request("ra", "feature/a", await BranchAsync(root, "feature/a", ("a.txt", "a\n")), 1, NoGates)));
        var replays = new IntegrationReplays(first, TreesHome) { Seam = point => point == "after-merge" ? throw new Killed() : Task.CompletedTask };
        var replayAsk = new IntegrationReplayAsk("engine", "aurora", root, "main") { Config = DriverConfig.Empty };
        await Assert.ThrowsAsync<Killed>(() => replays.RunAsync(replayAsk));
        var standing = Assert.Single(first.Standing());
        Assert.Null(standing.Tip);
        Assert.Equal(@base, await CommitOfAsync(root, "main"));

        var store = Taker();
        var asks = new IntegrationSweepAsks(replayAsk, new IntegrationPromoteAsk("engine", "aurora", root, "main", DriverConfig.Empty, Home));
        var swept = await new IntegrationSweeps(store, TreesHome).SweepAsync(_ => asks);

        Assert.Equal(standing.Id, Assert.Single(swept).Candidate);
        var done = store.Candidate(standing.Id)!;
        Assert.Equal(IntegrationCandidateEnds.Settled, done.EndedWhy);
        Assert.Equal(done.Tip, await CommitOfAsync(root, "main"));
        Assert.Equal(@base, await CommitOfAsync(root, "main^"));
        Assert.Equal(
            [(1, IntegrationStepOutcomes.Interrupted), (2, IntegrationStepOutcomes.Done)],
            store.Steps(standing.Id).Where(each => each.Step == IntegrationStepNames.Replay).Select(each => (each.Attempt, each.Outcome!)));
        Assert.Single(store.Steps(standing.Id), each => each.Step == IntegrationStepNames.Promote);
    }

    /// <summary>
    /// 🔴 A settled candidate is nothing to the sweep: it stands no more, so nothing is taken, written or moved; and a standing candidate whose
    /// run lives is refused by the take, so the sweep writes nothing of it either.
    /// </summary>
    [Fact]
    public async Task The_sweep_does_nothing_for_a_settled_candidate_and_leaves_a_live_run_s_candidate_alone()
    {
        var candidate = await SingleAsync();
        await Promotes(candidate.Store).RunAsync(candidate.Claim, candidate.Ask);
        Assert.False(candidate.Now.Stands);
        var before = candidate.Written;
        var line = await CommitOfAsync(candidate.Root, "main");

        Assert.Empty(await new IntegrationSweeps(Taker(), TreesHome).SweepAsync(_ => Asks(candidate)));
        Assert.Equal(before, candidate.Written);
        Assert.Equal(line, await CommitOfAsync(candidate.Root, "main"));

        // A second candidate, standing under a run that lives: the take is refused and nothing is written.
        var root = candidate.Root;
        Assert.Null(candidate.Store.Add(Request("rb", "feature/b", await BranchAsync(root, "feature/b", ("b.txt", "b\n")), 2, NoGates)));
        var formed = await new IntegrationReplays(candidate.Store, TreesHome).RunAsync(new IntegrationReplayAsk("engine", "aurora", root, "main") { Config = DriverConfig.Empty });
        Assert.Equal(IntegrationReplayRuns.Replayed, formed.Kind);
        var written = Store("run-b").Steps(formed.Claim!.Candidate).Count;

        var swept = Assert.Single(await new IntegrationSweeps(Store("run-b"), TreesHome).SweepAsync(_ => Asks(candidate)));

        Assert.Equal(IntegrationSweptKinds.Refused, swept.Kind);
        Assert.Equal(written, Store("run-b").Steps(formed.Claim.Candidate).Count);
        Assert.Equal(line, await CommitOfAsync(root, "main"));
        Assert.True(Store("run-b").Candidate(formed.Claim.Candidate)!.Stands);
    }

    // ——— The requester's authority, read again at promotion (§5.2 step 8, §8).

    /// <summary>The seam as this class sets it: every request answered by <paramref name="lapsed"/>, which names the requests that lapsed and why.</summary>
    private sealed class Appointments(Func<IntegrationRequest, string?> lapsed) : IIntegrationAppointments
    {
        public List<string> Asked { get; } = [];

        public Task<string?> LapsedAsync(IntegrationRequest request, CancellationToken ct)
        {
            Asked.Add(request.Id);
            return Task.FromResult(lapsed(request));
        }
    }

    /// <summary>The person's choices as they stand when the rule still says <c>request: steward</c>.</summary>
    private static DriverConfig StewardRule() => DriverConfig.Empty
        .WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"))
        .WithLandingIntegrate("engine", new LandingIntegrate(Request: LandingIntegrate.Steward, Batch: 8, Gates: LandingIntegrate.Declared));

    /// <summary>
    /// 🔴 A steward's request whose appointment lapsed between the form and the promotion is held at promotion, said why, with nothing moved: the
    /// candidate is formed again without it, its request stays open and out of the queue, and the other member promotes in the next candidate.
    /// </summary>
    [Fact]
    public async Task A_stewards_request_whose_appointment_lapsed_is_held_at_promotion_and_the_candidate_is_formed_again_without_it()
    {
        var root = await RepositoryAsync("engine");
        var @base = await CommitOfAsync(root, "main");
        var store = Store("run-a");
        var steward = new LandingIntegrate(Request: LandingIntegrate.Steward, Batch: 8, Gates: LandingIntegrate.NoGates);
        Assert.Null(store.Add(Request("ra", "feature/a", await BranchAsync(root, "feature/a", ("a.txt", "a\n")), 1, steward, IntegrationRequester.StewardAct("act-1", 2))));
        Assert.Null(store.Add(Request("rb", "feature/b", await BranchAsync(root, "feature/b", ("b.txt", "b\n")), 2, steward, IntegrationRequester.Card)));
        var candidate = await FormedAsync(root, @base, store, StewardRule());
        var appointments = new Appointments(request => request.Id == "ra" ? "the appointment's generation 2 was revoked" : null);

        var run = await Promotes(store).RunAsync(candidate.Claim, candidate.Ask with { Appointments = appointments });

        Assert.Equal(IntegrationPromoteRuns.Lapsed, run.Kind);
        Assert.Contains("generation 2 was revoked", run.Said, StringComparison.Ordinal);
        Assert.Equal(@base, await CommitOfAsync(root, "main"));
        Assert.Equal(["ra"], appointments.Asked);
        Assert.Equal(IntegrationCandidateEnds.FormedAgain, candidate.Now.EndedWhy);
        var held = candidate.Member("ra");
        Assert.Equal(IntegrationMemberStates.Held, held.State);
        Assert.Contains("generation 2 was revoked", held.Said, StringComparison.Ordinal);
        Assert.Null(store.Request("ra")!.ClosedAt);
        Assert.Equal(["rb"], store.Queued("engine", "main").Select(each => each.Id));
        Assert.Equal(IntegrationStepOutcomes.Done, candidate.Last(IntegrationStepNames.Promote).Outcome);
        Assert.Null(candidate.Now.PromotedAt);

        // The next candidate carries the member whose authority stands, and promotes it.
        await GitAsync(root, "checkout", "--quiet", "main");
        var next = await FormedAsync(root, @base, store, candidate.Ask.Config);
        Assert.Equal(IntegrationPromoteRuns.Settled, (await Promotes(store).RunAsync(next.Claim, next.Ask with { Appointments = appointments })).Kind);
        Assert.Equal("b", (await GitAsync(root, "show", "main:b.txt")).Trim());
        Assert.Equal("", (await GitAsync(root, "ls-tree", "--name-only", "main", "a.txt")).Trim());
    }

    /// <summary>
    /// 🔴 With nothing in the driver that reads an appointment's liveness (the seam's default), a steward's request is held at promotion: an
    /// authority that cannot be read is no authority, and nothing moves.
    /// </summary>
    [Fact]
    public async Task With_no_appointment_to_read_a_stewards_request_is_held_at_promotion()
    {
        var steward = new LandingIntegrate(Request: LandingIntegrate.Steward, Batch: 8, Gates: LandingIntegrate.NoGates);
        var held = await SingleAsync(steward, IntegrationRequester.StewardAct("act-1", 1));

        var run = await Promotes(held.Store).RunAsync(held.Claim, held.Ask with { Config = StewardRule() });

        Assert.Equal(IntegrationPromoteRuns.Lapsed, run.Kind);
        Assert.Equal(held.Base, await CommitOfAsync(held.Root, "main"));
        Assert.Equal(IntegrationMemberStates.Held, held.Member("ra").State);
        Assert.Equal(IntegrationCandidateEnds.Empty, held.Now.EndedWhy);
        Assert.Contains("appointment", run.Said, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 A steward's request whose appointment stands, in a rule that still says <c>request: steward</c>, promotes: the authority is read again
    /// at promotion and found good.
    /// </summary>
    [Fact]
    public async Task A_stewards_request_whose_appointment_stands_promotes()
    {
        var steward = new LandingIntegrate(Request: LandingIntegrate.Steward, Batch: 8, Gates: LandingIntegrate.NoGates);
        var candidate = await SingleAsync(steward, IntegrationRequester.StewardAct("act-1", 1));
        var appointments = new Appointments(_ => null);

        var run = await Promotes(candidate.Store).RunAsync(candidate.Claim, candidate.Ask with { Config = StewardRule(), Appointments = appointments });

        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        Assert.Equal(["ra"], appointments.Asked);
        Assert.Equal(candidate.Tip, await CommitOfAsync(candidate.Root, "main"));
    }
}
