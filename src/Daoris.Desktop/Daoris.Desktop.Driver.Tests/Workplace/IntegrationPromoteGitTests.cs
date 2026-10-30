using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1e part e4a over real git (D215's note; the integration design §5.2 steps 8 and 9, §5.6, §5.7): a verified candidate promoted onto
/// its line only on a passing verdict for its exact tip, base and blob, and only if the line has not moved under it; the fence read from the
/// line's committed declaration inside the effect; the line moved by one <c>update-ref</c> verifying its old value, or by a fast-forward in a
/// clean checkout that has it out; a held member never promoted; each member settled; the candidate's one job closed at each of its ends. Called
/// from no production path. The verdict port is the test's stub (<see cref="IntegrationVerdicts.Current"/>), as Review installs the real one.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationPromoteGitTests : LandedFixture, IAsyncLifetime
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

    /// <summary>A branch from <c>main</c> with one commit for each file given, the checkout left on <c>main</c>; the branch's tip.</summary>
    private static async Task<string> BranchAsync(string root, string name, params (string File, string Content)[] commits)
    {
        await GitAsync(root, "checkout", "--quiet", "-b", name, "main");
        foreach (var (file, content) in commits) await CommitAsync(root, file, content, $"work on {name}: {file}");
        var tip = await CommitOfAsync(root, name);
        await GitAsync(root, "checkout", "--quiet", "main");
        return tip;
    }

    private static void Queue(IntegrationStore store, string id, string branch, string tip, int order, LandingIntegrate integrate, string? session = null) =>
        Assert.Null(store.Add(new IntegrationRequest(
            id, "engine", "aurora", branch, tip, IntegrationRoutes.Recorded, "main", IntegrationRequester.Card, integrate.Resolved(),
            new DateTimeOffset(2026, 10, 30, 9, 0, order, TimeSpan.Zero)) { Session = session }));

    /// <summary>What a candidate formed and replayed on the line stands on: the requests are ra, rb, rc... in the order given.</summary>
    private sealed record Candidate(
        string Root, string Base, IntegrationStore Store, IntegrationClaim Claim, IntegrationPromoteAsk Ask, IntegrationReplayRun Run, string Blob)
    {
        public IntegrationCandidate Now => Store.Candidate(Claim.Candidate)!;

        public IntegrationMember Member(string request) => Store.Members(Claim.Candidate).Single(each => each.Request == request);

        public string Tip => Now.Tip!;
    }

    /// <summary>
    /// <c>engine</c> on <c>main</c>, with <paramref name="declaration"/> committed on it first where one is given (and accepted by the owner, so
    /// the gates may run), a branch for each member, and a candidate formed and replayed from their requests. The checkout is left detached
    /// where <paramref name="detached"/>, so no working tree has the line out, and on <c>main</c> otherwise.
    /// </summary>
    private async Task<Candidate> FormedAsync(
        (string Branch, (string File, string Content)[] Commits)[] members, string? declaration = Gates, LandingIntegrate? integrate = null,
        bool detached = true, bool accepted = true, Func<string, Task>? beforeFormed = null)
    {
        var root = await RepositoryAsync("engine");
        var blob = "";
        if (declaration is not null)
        {
            await CommitAsync(root, "daoris.gates.json", declaration, "the declaration");
            blob = (await GitAsync(root, "rev-parse", "HEAD:daoris.gates.json")).Trim();
        }

        if (beforeFormed is not null) await beforeFormed(root);
        var @base = await CommitOfAsync(root, "main");
        var store = Store("run-a");
        var policy = integrate ?? new LandingIntegrate(Batch: 8, Gates: LandingIntegrate.Declared);
        for (var at = 0; at < members.Length; at++)
        {
            var tip = await BranchAsync(root, members[at].Branch, members[at].Commits);
            Queue(store, $"r{(char)('a' + at)}", members[at].Branch, tip, at + 1, policy);
        }

        var config = declaration is not null && accepted ? DriverConfig.Empty.WithIntegrationGates("engine", blob) : DriverConfig.Empty;
        var replays = new IntegrationReplays(store, TreesHome);
        var run = await replays.RunAsync(new IntegrationReplayAsk("engine", "aurora", root, "main") { Config = config });
        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        if (detached) await GitAsync(root, "checkout", "--quiet", "--detach");
        return new Candidate(root, @base, store, run.Claim!, new IntegrationPromoteAsk("engine", "aurora", root, "main", config, Home), run, blob);
    }

    private static (string Branch, (string File, string Content)[] Commits) Member(string branch, params (string File, string Content)[] commits) => (branch, commits);

    private IntegrationPromotes Promotes(Candidate candidate) => new(candidate.Store, TreesHome);

    /// <summary>The verdict port as this class sets it: every ask kept, each answered by <paramref name="answer"/>.</summary>
    private sealed class Verdict(Func<IntegrationVerdictAsk, Task<IntegrationVerdict>> answer) : IIntegrationVerdicts
    {
        public List<IntegrationVerdictAsk> Asks { get; } = [];

        public async Task<IntegrationVerdict> JudgeAsync(IntegrationVerdictAsk ask)
        {
            Asks.Add(ask);
            return await answer(ask).ConfigureAwait(false);
        }
    }

    private static Verdict Judging(Func<IntegrationVerdictAsk, IntegrationVerdict> answer) => Judging(ask => Task.FromResult(answer(ask)));

    private static Verdict Judging(Func<IntegrationVerdictAsk, Task<IntegrationVerdict>> answer)
    {
        var verdict = new Verdict(answer);
        IntegrationVerdicts.Current = verdict;
        return verdict;
    }

    private static Verdict Green() => Judging(_ => new IntegrationVerdict(VerdictActions.Green));

    private static IReadOnlyList<(string Step, string? Outcome)> Written(Candidate candidate) =>
        [.. candidate.Store.Steps(candidate.Claim.Candidate).Select(each => (each.Step, each.Outcome))];

    private static IntegrationStep Last(Candidate candidate, string step) => candidate.Store.Steps(candidate.Claim.Candidate).Last(each => each.Step == step);

    /// <summary>
    /// 🔴 A green verdict promotes the candidate to its line by one <c>update-ref</c> where no working tree has the line out: the line stands at
    /// the candidate's tip, the person's detached checkout never moved, every member settled and its request closed, the candidate ended
    /// <c>settled</c>. The verdict was asked for the integration tree standing at the tip, on the base, under the accepted blob.
    /// </summary>
    [Fact]
    public async Task A_green_verdict_promotes_the_candidate_by_one_update_ref_and_settles_its_members()
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n")), Member("feature/b", ("b.txt", "b\n"))]);
        var verdict = Green();

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        var tip = candidate.Tip;
        Assert.Equal(tip, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(candidate.Base, await CommitOfAsync(candidate.Root, "HEAD"));
        Assert.Equal(IntegrationCandidateEnds.Settled, candidate.Now.EndedWhy);
        Assert.NotNull(candidate.Now.PromotedAt);
        Assert.All(candidate.Store.Members(candidate.Claim.Candidate), each => Assert.Equal(IntegrationMemberStates.Settled, each.State));
        Assert.All(["ra", "rb"], request => Assert.Equal(IntegrationRequestCloses.Settled, candidate.Store.Request(request)!.ClosedWhy));
        var promote = Last(candidate, IntegrationStepNames.Promote);
        Assert.Equal((IntegrationStepOutcomes.Promoted, candidate.Base), (promote.Outcome, promote.LineCommit));
        Assert.Equal([IntegrationStepNames.Form, IntegrationStepNames.Replay, IntegrationStepNames.Replay, IntegrationStepNames.Promote, IntegrationStepNames.Settle],
            candidate.Store.Steps(candidate.Claim.Candidate).Select(each => each.Step));

        var asked = Assert.Single(verdict.Asks);
        Assert.Equal((tip, candidate.Base, candidate.Blob, "split"), (asked.Candidate.Tip, asked.Base, asked.DeclarationId, asked.Unconfirmed));
        Assert.Equal(Gates, asked.Declaration);
        Assert.Equal(tip, await CommitOfAsync(asked.Tree, "HEAD"));
        Assert.StartsWith(Path.GetFullPath(TreesHome), Path.GetFullPath(asked.Tree), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(Path.GetFullPath(TreesHome), Path.GetFullPath(asked.Scratch), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetFullPath(asked.Tree) + Path.DirectorySeparatorChar, Path.GetFullPath(asked.Scratch) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Home, asked.Home);
        Assert.Equal(["unit"], asked.Gates);
        Assert.Null(asked.Runner);
        Assert.Equal(
            [("feature/a", candidate.Member("ra").Replayed!), ("feature/b", candidate.Member("rb").Replayed!)],
            asked.Members.Select(each => (each.Branch, each.Tip)));
    }

    /// <summary>
    /// 🔴 The ask carries what the accepted declaration says: its runner (D192's <c>verification</c>, with its folder), the gates it names for
    /// integration and the variables it passes; read from the accepted blob alone.
    /// </summary>
    [Fact]
    public async Task The_verdict_is_asked_with_the_declarations_runner_gates_and_passed_names()
    {
        var declaration = """
            { "gates": [ { "name": "unit", "run": "node a.mjs" }, { "name": "lint", "run": "node b.mjs" } ],
              "verification": { "run": "node tools/runner.mjs", "cwd": "tools" },
              "integration": { "gates": [ "lint" ], "env": { "pass": [ "HOMEDRIVE" ] } } }
            """ + "\n";
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))], declaration);
        var verdict = Green();

        await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        var asked = Assert.Single(verdict.Asks);
        Assert.Equal(["lint"], asked.Gates);
        Assert.Equal(["HOMEDRIVE"], asked.Pass);
        Assert.Equal(new VerdictRunner("node tools/runner.mjs", "tools"), asked.Runner);
        Assert.Equal(declaration, asked.Declaration);
    }

    /// <summary>🔴 A rule with <c>gates: none</c> integrates with no gates: the port is never asked, and the promotion says so.</summary>
    [Fact]
    public async Task A_line_integrating_with_no_gates_promotes_without_asking_the_verdict_port()
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))], declaration: null, integrate: new LandingIntegrate(Gates: LandingIntegrate.NoGates));
        var verdict = Green();

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        Assert.Empty(verdict.Asks);
        Assert.Equal(candidate.Tip, await CommitOfAsync(candidate.Root, "main"));
        Assert.Contains("no gates", Last(candidate, IntegrationStepNames.Promote).Said, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 A line moved under the candidate refuses the promotion and moves nothing: moved before the look, the verdict is never asked and the
    /// candidate is formed again, its requests queued; moved while the verdict ran, the line is read again immediately before the effect, and the
    /// line keeps the other's commit.
    /// </summary>
    [Theory]
    [InlineData("before")]
    [InlineData("during-the-verdict")]
    public async Task A_line_moved_under_the_candidate_refuses_the_promotion_and_moves_nothing(string when)
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))], detached: false);
        string theirs = "";

        async Task MoveTheLineAsync()
        {
            await GitAsync(candidate.Root, "checkout", "--quiet", "main");
            await CommitAsync(candidate.Root, "theirs.txt", "someone else's commit\n", "someone else lands first");
            theirs = await CommitOfAsync(candidate.Root, "main");
            await GitAsync(candidate.Root, "checkout", "--quiet", "--detach");
        }

        if (when == "before") await MoveTheLineAsync();
        else await GitAsync(candidate.Root, "checkout", "--quiet", "--detach");
        var verdict = Judging(async _ =>
        {
            if (when == "during-the-verdict") await MoveTheLineAsync();
            return new IntegrationVerdict(VerdictActions.Green);
        });

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Moved, run.Kind);
        Assert.Equal(theirs, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(when == "before" ? 0 : 1, verdict.Asks.Count);
        Assert.Equal(IntegrationCandidateEnds.FormedAgain, candidate.Now.EndedWhy);
        Assert.Null(candidate.Now.PromotedAt);
        Assert.Equal(["ra"], candidate.Store.Queued("engine", "main").Select(each => each.Id));
        Assert.Equal(IntegrationStepOutcomes.Done, Last(candidate, IntegrationStepNames.Promote).Outcome);
        Assert.Contains("moved", Last(candidate, IntegrationStepNames.Promote).Said, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The move verifies the line's old value: a line that moves after the effect's last look and before its <c>update-ref</c> is refused by
    /// git itself, the step held with git's words, and the line keeps the other's commit; the next look forms the candidate again.
    /// </summary>
    [Fact]
    public async Task A_line_that_moves_between_the_last_look_and_the_update_ref_is_refused_by_the_old_value()
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))]);
        Green();
        string theirs = "";
        var promotes = Promotes(candidate);
        promotes.Seam = async point =>
        {
            if (point != "before-move") return;
            await GitAsync(candidate.Root, "checkout", "--quiet", "main");
            await CommitAsync(candidate.Root, "theirs.txt", "someone else's commit\n", "someone else lands first");
            theirs = await CommitOfAsync(candidate.Root, "main");
            await GitAsync(candidate.Root, "checkout", "--quiet", "--detach");
        };

        var run = await promotes.RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Held, run.Kind);
        Assert.Equal(theirs, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(IntegrationStepOutcomes.Held, Last(candidate, IntegrationStepNames.Promote).Outcome);
        Assert.Null(candidate.Now.PromotedAt);
        Assert.True(candidate.Now.Stands);

        promotes.Seam = null;
        var again = await promotes.RunAsync(candidate.Claim, candidate.Ask);
        Assert.Equal(IntegrationPromoteRuns.Moved, again.Kind);
        Assert.Equal(theirs, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(IntegrationCandidateEnds.FormedAgain, candidate.Now.EndedWhy);
    }

    /// <summary>
    /// 🔴 A line a checkout has out is fast-forwarded in that checkout where it is clean, and its files follow; where it is not clean the step
    /// is held, said why, nothing moved and the person's work untouched, never an <c>update-ref</c> under a checkout; tried again at the next
    /// look, it promotes once the work is committed away.
    /// </summary>
    [Fact]
    public async Task A_line_a_checkout_has_out_is_fast_forwarded_when_it_is_clean_and_held_when_it_is_not()
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))], detached: false);
        var verdict = Green();
        await File.WriteAllTextAsync(Path.Combine(candidate.Root, "shared.txt"), "the person's work in flight\n");
        var promotes = Promotes(candidate);

        var held = await promotes.RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Held, held.Kind);
        Assert.Equal(candidate.Base, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal("the person's work in flight\n", await File.ReadAllTextAsync(Path.Combine(candidate.Root, "shared.txt")));
        Assert.Equal(IntegrationStepOutcomes.Held, Last(candidate, IntegrationStepNames.Promote).Outcome);
        Assert.Contains("not clean", Last(candidate, IntegrationStepNames.Promote).Said, StringComparison.Ordinal);
        Assert.True(candidate.Now.Stands);
        Assert.Null(candidate.Now.PromotedAt);

        await GitAsync(candidate.Root, "checkout", "--quiet", "--", "shared.txt");
        var run = await promotes.RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        Assert.Equal(candidate.Tip, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal("a", (await File.ReadAllTextAsync(Path.Combine(candidate.Root, "a.txt"))).Trim());
        Assert.Equal("main", (await GitAsync(candidate.Root, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal(
            [IntegrationStepOutcomes.Held, IntegrationStepOutcomes.Promoted],
            candidate.Store.Steps(candidate.Claim.Candidate).Where(each => each.Step == IntegrationStepNames.Promote).Select(each => each.Outcome));
        Assert.Equal(2, verdict.Asks.Count);
    }

    /// <summary>
    /// 🔴 The fence: <c>integration.by</c> is read inside the promote effect from the declaration committed at the line's commit being moved,
    /// never the working tree. <c>by: tool</c>, and a declaration that does not read, hold the step and move nothing, though the verdict was
    /// green; the working tree's copy, saying the other thing, changes neither answer.
    /// </summary>
    [Theory]
    [InlineData("""{ "gates": [], "integration": { "by": "tool" } }""", "tool")]
    [InlineData("""{ "gates": [], "integration": { "by": "somebody" } }""", "`integration.by` is `daoris` or `tool`")]
    public async Task A_line_another_integrator_owns_or_whose_declaration_does_not_read_is_held_with_nothing_moved(string declaration, string said)
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))], declaration + "\n");
        Green();
        // The working tree says what the committed one does not: a copy by the owner of the line, and it changes nothing.
        await File.WriteAllTextAsync(Path.Combine(candidate.Root, "daoris.gates.json"), """{ "gates": [], "integration": { "by": "daoris" } }""" + "\n");

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Held, run.Kind);
        Assert.Equal(candidate.Base, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(IntegrationStepOutcomes.Held, Last(candidate, IntegrationStepNames.Promote).Outcome);
        Assert.Contains(said, Last(candidate, IntegrationStepNames.Promote).Said, StringComparison.Ordinal);
        Assert.Null(candidate.Now.PromotedAt);
        Assert.True(candidate.Now.Stands);
    }

    /// <summary>🔴 The fence is the line's committed declaration: a working tree that says <c>by: tool</c> and a commit that says <c>daoris</c> promotes.</summary>
    [Fact]
    public async Task The_fence_reads_the_committed_declaration_and_not_the_working_tree()
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))], """{ "gates": [], "integration": { "by": "daoris" } }""" + "\n");
        Green();
        await File.WriteAllTextAsync(Path.Combine(candidate.Root, "daoris.gates.json"), """{ "gates": [], "integration": { "by": "tool" } }""" + "\n");

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        Assert.Equal(candidate.Tip, await CommitOfAsync(candidate.Root, "main"));
    }

    private static IntegrationVerdict HandBack(string branch) => new(VerdictActions.HandBack)
    {
        HandedBack = [new VerdictHandedBack(branch, "pinned", "unit > fails on the member", [], [], null)],
    };

    /// <summary>
    /// 🔴 The owner's rule alone lets <c>land</c> promote: with <c>unconfirmed: land</c> the confirm run's pass is the verdict on the tip, and
    /// the promotion says which gates it passed on; with any other rule, or with no gate named, a <c>land</c> is no pass and nothing is written.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task A_land_verdict_promotes_only_where_the_owners_rule_chose_it_and_names_its_gates(bool rule, bool named)
    {
        var candidate = await FormedAsync(
            [Member("feature/a", ("a.txt", "a\n"))],
            integrate: new LandingIntegrate(Batch: 8, Gates: LandingIntegrate.Declared, Unreached: rule ? LandingIntegrate.Land : null, Reached: rule ? LandingIntegrate.Land : null));
        var verdict = Judging(_ => new IntegrationVerdict(VerdictActions.Land) { PassedOnConfirm = named ? ["unit"] : [] });
        var before = Written(candidate);

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(rule ? "land" : "split", Assert.Single(verdict.Asks).Unconfirmed);
        if (rule && named)
        {
            Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
            Assert.Equal(candidate.Tip, await CommitOfAsync(candidate.Root, "main"));
            Assert.Contains("confirm run", Last(candidate, IntegrationStepNames.Promote).Said, StringComparison.Ordinal);
            Assert.Contains("unit", Last(candidate, IntegrationStepNames.Promote).Said, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(IntegrationPromoteRuns.Waiting, run.Kind);
            Assert.Equal(candidate.Base, await CommitOfAsync(candidate.Root, "main"));
            Assert.Equal(before, Written(candidate));
            Assert.True(candidate.Now.Stands);
        }
    }

    /// <summary>
    /// 🔴 A hold ends the step <c>held</c> and the candidate stands, nothing moved; the next look does not run the gates again, since the
    /// candidate waits for a decide.
    /// </summary>
    [Fact]
    public async Task A_hold_verdict_ends_the_step_held_with_the_candidate_standing_and_is_not_judged_again()
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))]);
        var verdict = Judging(_ => new IntegrationVerdict(VerdictActions.Hold) { On = "decide", Why = "an unconfirmed failure holds the candidate for a decide: unit" });
        var promotes = Promotes(candidate);

        var run = await promotes.RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Held, run.Kind);
        Assert.Equal(candidate.Base, await CommitOfAsync(candidate.Root, "main"));
        var judge = Last(candidate, IntegrationStepNames.Judge);
        Assert.Equal(IntegrationStepOutcomes.Held, judge.Outcome);
        Assert.Contains("decide", judge.Said, StringComparison.Ordinal);
        Assert.True(candidate.Now.Stands);
        Assert.Equal(IntegrationMemberStates.Replayed, candidate.Member("ra").State);
        var written = Written(candidate);

        var again = await promotes.RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Held, again.Kind);
        Assert.Equal(written, Written(candidate));
        Assert.Single(verdict.Asks);
    }

    /// <summary>
    /// 🔴 A split keeps the members it retains, run one at a time: the failing member is handed back (its request closed), the candidate is
    /// formed again, and the next candidate the line forms takes one member though the rule allows eight.
    /// </summary>
    [Fact]
    public async Task A_split_verdict_hands_back_the_pinned_member_and_forms_the_rest_again_one_at_a_time()
    {
        var candidate = await FormedAsync([
            Member("feature/a", ("a.txt", "a\n")), Member("feature/b", ("b.txt", "b\n")), Member("feature/c", ("c.txt", "c\n")), Member("feature/d", ("d.txt", "d\n"))]);
        Judging(_ => new IntegrationVerdict(VerdictActions.Split)
        {
            HandedBack = [new VerdictHandedBack("feature/b", "pinned", "unit > fails on b", [], [], null)],
            Retain = ["feature/a", "feature/c", "feature/d"],
        });

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Split, run.Kind);
        Assert.Equal(candidate.Base, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(IntegrationCandidateEnds.FormedAgain, candidate.Now.EndedWhy);
        Assert.Equal(IntegrationStepOutcomes.Split, Last(candidate, IntegrationStepNames.Judge).Outcome);
        Assert.Equal(IntegrationMemberStates.HandedBack, candidate.Member("rb").State);
        Assert.Equal(IntegrationRequestCloses.HandedBack, candidate.Store.Request("rb")!.ClosedWhy);
        Assert.Contains("unit > fails on b", candidate.Member("rb").Said, StringComparison.Ordinal);
        Assert.All(["ra", "rc", "rd"], request => Assert.Null(candidate.Store.Request(request)!.ClosedAt));
        Assert.Equal(["ra", "rc", "rd"], candidate.Store.Queued("engine", "main").Select(each => each.Id));

        // The next candidate takes one member: the rule's eight are not trusted again at once.
        var next = await new IntegrationReplays(candidate.Store, TreesHome).RunAsync(new IntegrationReplayAsk("engine", "aurora", candidate.Root, "main") { Config = candidate.Ask.Config });
        Assert.Equal(IntegrationReplayRuns.Replayed, next.Kind);
        Assert.Equal(["ra"], candidate.Store.Members(next.Candidate!.Id).Select(each => each.Request));
    }

    /// <summary>
    /// 🔴 A hand back ends each named member <c>handed-back</c> with its request closed; the members kept go back to the queue with the
    /// candidate formed again, and a candidate whose every member is handed back ends <c>empty</c> with nothing to form.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_hand_back_verdict_ends_each_named_member_handed_back_and_forms_the_rest_again(bool all)
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n")), Member("feature/b", ("b.txt", "b\n"))]);
        Judging(_ => new IntegrationVerdict(VerdictActions.HandBack)
        {
            HandedBack = [.. (all ? new[] { "feature/a", "feature/b" } : ["feature/b"]).Select(branch => new VerdictHandedBack(branch, "pinned", "unit > fails", [], [], null))],
            Retain = all ? [] : ["feature/a"],
        });

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.HandedBack, run.Kind);
        Assert.Equal(candidate.Base, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(IntegrationStepOutcomes.HandedBack, Last(candidate, IntegrationStepNames.Judge).Outcome);
        Assert.Equal(all ? IntegrationCandidateEnds.Empty : IntegrationCandidateEnds.FormedAgain, candidate.Now.EndedWhy);
        Assert.Equal(IntegrationRequestCloses.HandedBack, candidate.Store.Request("rb")!.ClosedWhy);
        Assert.Equal(all ? IntegrationRequestCloses.HandedBack : null, candidate.Store.Request("ra")!.ClosedWhy);
        Assert.Equal(all ? Array.Empty<string>() : ["ra"], candidate.Store.Queued("engine", "main").Select(each => each.Id));
    }

    /// <summary>🔴 A stop ends the step <c>stopped</c> and the candidate <c>stopped</c>; the requests stay open and nothing moved.</summary>
    [Fact]
    public async Task A_stop_verdict_stops_the_step_and_the_candidate_and_moves_nothing()
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))]);
        Judging(_ => new IntegrationVerdict(VerdictActions.Stop) { Why = "the base fails a gate that names no test", Failure = new VerdictFailure("unit", null, "unit", "unit") });

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Stopped, run.Kind);
        Assert.Equal(candidate.Base, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(IntegrationStepOutcomes.Stopped, Last(candidate, IntegrationStepNames.Judge).Outcome);
        Assert.Equal(IntegrationCandidateEnds.Stopped, candidate.Now.EndedWhy);
        Assert.Null(candidate.Store.Request("ra")!.ClosedAt);
        Assert.Contains("names no test", Last(candidate, IntegrationStepNames.Judge).Said, StringComparison.Ordinal);
    }

    /// <summary>🔴 A drop closes every request of the candidate <c>dropped</c> and ends the candidate <c>dropped</c>.</summary>
    [Fact]
    public async Task A_dropped_verdict_closes_the_requests_dropped_and_ends_the_candidate_dropped()
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n")), Member("feature/b", ("b.txt", "b\n"))]);
        Judging(_ => new IntegrationVerdict(VerdictActions.Dropped));

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Dropped, run.Kind);
        Assert.Equal(candidate.Base, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(IntegrationCandidateEnds.Dropped, candidate.Now.EndedWhy);
        Assert.All(["ra", "rb"], request => Assert.Equal(IntegrationRequestCloses.Dropped, candidate.Store.Request(request)!.ClosedWhy));
        Assert.All(candidate.Store.Members(candidate.Claim.Candidate), each => Assert.Equal(IntegrationMemberStates.Dropped, each.State));
    }

    /// <summary>🔴 An unknown or a busy verdict is never a pass: nothing is written, the line is untouched and the next look asks again.</summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("busy")]
    [InlineData("a word a newer port speaks")]
    public async Task An_unknown_or_busy_verdict_writes_nothing_and_is_asked_again_at_the_next_look(string action)
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))]);
        var verdict = Judging(_ => new IntegrationVerdict(action) { Why = "the runner wrote no answer" });
        var before = Written(candidate);

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Waiting, run.Kind);
        Assert.Equal(candidate.Base, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(before, Written(candidate));
        Assert.True(candidate.Now.Stands);

        await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);
        Assert.Equal(2, verdict.Asks.Count);
    }

    /// <summary>
    /// 🔴 A verdict is acted on only for this tip: one answered after the integration tree left the tip (the gates were judged on another
    /// commit) is no verdict of this candidate and is not read as a pass; nothing is written, and the next look asks again. The base and the
    /// accepted blob are the candidate's own commit's, read from git at that commit, so they cannot differ from the ask's.
    /// </summary>
    [Fact]
    public async Task A_verdict_given_after_the_tree_left_the_tip_is_not_acted_on()
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))]);
        Judging(async ask =>
        {
            await GitAsync(ask.Tree, "checkout", "--quiet", "--force", "--detach", candidate.Base);
            return new IntegrationVerdict(VerdictActions.Green);
        });
        var before = Written(candidate);

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Waiting, run.Kind);
        Assert.Equal(candidate.Base, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(before, Written(candidate));
        Assert.Null(candidate.Now.PromotedAt);
    }

    /// <summary>
    /// 🔴 A held member is never promoted: the candidate's tip carries the clean members only, the verdict is asked for them alone, and the held
    /// request stays open and out of the queue for the act that releases it.
    /// </summary>
    [Fact]
    public async Task A_held_member_is_never_promoted_and_stays_open_out_of_the_queue()
    {
        var candidate = await FormedAsync(
            [Member("feature/a", ("shared.txt", "a's\n")), Member("feature/b", ("shared.txt", "b's\n")), Member("feature/c", ("c.txt", "c\n"))],
            integrate: new LandingIntegrate(Batch: 8, Gates: LandingIntegrate.Declared, Conflicts: LandingIntegrate.Hold));
        Assert.Equal(IntegrationMemberStates.Held, candidate.Member("rb").State);
        var verdict = Green();

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        Assert.Equal(["feature/a", "feature/c"], Assert.Single(verdict.Asks).Members.Select(each => each.Branch));
        Assert.Equal("a's", (await GitAsync(candidate.Root, "show", "main:shared.txt")).Trim());
        Assert.Equal("c", (await GitAsync(candidate.Root, "show", "main:c.txt")).Trim());
        Assert.Equal(candidate.Tip, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(IntegrationMemberStates.Held, candidate.Member("rb").State);
        Assert.Null(candidate.Store.Request("rb")!.ClosedAt);
        Assert.Empty(candidate.Store.Queued("engine", "main"));
        Assert.Equal(IntegrationRequestCloses.Settled, candidate.Store.Request("ra")!.ClosedWhy);
        Assert.Equal(IntegrationRequestCloses.Settled, candidate.Store.Request("rc")!.ClosedWhy);
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
        Queue(store, "ra", "feature/unit", frozen, 1, new LandingIntegrate(Batch: 8, Gates: LandingIntegrate.Declared));
        var config = DriverConfig.Empty.WithIntegrationGates("engine", blob);
        var run = await new IntegrationReplays(store, TreesHome).RunAsync(new IntegrationReplayAsk("engine", "aurora", root, "main") { Config = config });
        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        await GitAsync(root, "checkout", "--quiet", "--detach");
        return (new Candidate(root, @base, store, run.Claim!, new IntegrationPromoteAsk("engine", "aurora", root, "main", config, Home), run, blob), x);
    }

    /// <summary>The unit's branch after its request froze: one commit for each change given, in order; the checkout left detached on the line.</summary>
    private static async Task CommitAfterTheFreezeAsync(string root, params (string File, string Content, string Message)[] commits)
    {
        await GitAsync(root, "checkout", "--quiet", "feature/unit");
        foreach (var (file, content, message) in commits) await CommitAsync(root, file, content, message);
        await GitAsync(root, "checkout", "--quiet", "--detach", "main");
    }

    /// <summary>
    /// 🔴 The settle judges the commits a unit's branch made after its request froze against the promoted line: one whose content the line
    /// already holds is <c>contained</c> (said, nothing kept) and one that adds work is <c>new</c>; the member is settled, its request closed
    /// and the candidate ended <c>settled</c>.
    /// </summary>
    [Fact]
    public async Task Contained_and_new_commits_after_the_freeze_are_said_and_the_member_is_settled()
    {
        var (candidate, _) = await LinedAsync();
        await CommitAfterTheFreezeAsync(
            candidate.Root, ("lines.txt", Lines((2, "two, the unit's"), (12, "twelve, y's")), "y's change, by hand"), ("more.txt", "more work\n", "more work"));
        Green();

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        Assert.Equal(IntegrationCandidateEnds.Settled, candidate.Now.EndedWhy);
        Assert.Equal(IntegrationRequestCloses.Settled, candidate.Store.Request("ra")!.ClosedWhy);
        Assert.Equal(IntegrationMemberStates.Settled, candidate.Member("ra").State);
        var said = Last(candidate, IntegrationStepNames.Settle).Said!;
        Assert.Contains("contained", said, StringComparison.Ordinal);
        Assert.Contains("1 new", said, StringComparison.Ordinal);
        Assert.Equal(IntegrationStepOutcomes.Done, Last(candidate, IntegrationStepNames.Settle).Outcome);
    }

    /// <summary>
    /// 🔴 A superseded commit is never dropped: its replay conflicts where the line's side was changed after the unit began by a commit outside
    /// its work, so the member is left standing with a sentence that the next part settles it, its request open, the candidate standing, and
    /// the step held; the other members are settled as usual.
    /// </summary>
    [Fact]
    public async Task A_superseded_commit_after_the_freeze_leaves_its_member_standing_and_is_never_dropped()
    {
        var (candidate, x) = await LinedAsync();
        await CommitAfterTheFreezeAsync(candidate.Root, ("lines.txt", Lines((2, "two, the unit's"), (8, "eight, the unit's")), "line 8, the unit's"));
        Green();

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Unsettled, run.Kind);
        Assert.Equal(candidate.Tip, await CommitOfAsync(candidate.Root, "main"));
        Assert.True(candidate.Now.Stands);
        Assert.NotNull(candidate.Now.PromotedAt);
        Assert.Null(candidate.Store.Request("ra")!.ClosedAt);
        var member = candidate.Member("ra");
        Assert.True(member.Stands);
        Assert.Equal(IntegrationMemberStates.Promoted, member.State);
        Assert.Contains("superseded", member.Said, StringComparison.Ordinal);
        var settle = Last(candidate, IntegrationStepNames.Settle);
        Assert.Equal(IntegrationStepOutcomes.Held, settle.Outcome);
        Assert.Contains("superseded", settle.Said, StringComparison.Ordinal);
        Assert.Contains(x[..8], settle.Said, StringComparison.Ordinal);
        Assert.Contains("e4b", settle.Said, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 A line whose rule runs the declared gates but whose declaration the owner has not accepted cannot run them: the verdict port is not
    /// asked, nothing is written, and the line is untouched; the next look asks again.
    /// </summary>
    [Fact]
    public async Task Declared_gates_with_no_accepted_declaration_cannot_run_and_nothing_is_written()
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))], accepted: false);
        var verdict = Green();
        var before = Written(candidate);

        var run = await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Waiting, run.Kind);
        Assert.Empty(verdict.Asks);
        Assert.Equal(candidate.Base, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(before, Written(candidate));
        Assert.Contains("accepted", run.Said, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 A crash after the promote's start, and one after the line moved and before its record, each resumed by another process: the line moves
    /// once whatever the point, a promotion the line already holds read by ancestry (the verdict not asked again) and one it does not run again
    /// from its start as the next attempt.
    /// </summary>
    [Theory]
    [InlineData("after-start")]
    [InlineData("after-move")]
    [InlineData("in-settle")]
    public async Task A_crash_after_the_promotes_start_or_the_move_is_resumed_with_no_second_move(string crash)
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))]);
        var verdict = Green();
        var first = Promotes(candidate);
        first.Seam = point => point == crash ? throw new Killed() : Task.CompletedTask;
        await Assert.ThrowsAsync<Killed>(() => first.RunAsync(candidate.Claim, candidate.Ask));
        var moved = crash != "after-start";
        Assert.Equal(moved ? candidate.Tip : candidate.Base, await CommitOfAsync(candidate.Root, "main"));

        // The come-up: another process takes the dead run's candidate and goes on.
        var store = Taker();
        var claim = store.Take(candidate.Claim.Candidate, candidate.Claim.Generation).Claim!;
        var asks = verdict.Asks.Count;
        var run = await new IntegrationPromotes(store, TreesHome).RunAsync(claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Settled, run.Kind);
        Assert.Equal(candidate.Tip, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(candidate.Base, await CommitOfAsync(candidate.Root, "main^"));
        Assert.Equal(crash == "after-start" ? asks + 1 : asks, verdict.Asks.Count);
        Assert.Equal(IntegrationCandidateEnds.Settled, store.Candidate(claim.Candidate)!.EndedWhy);
        (int, string, string)[] promotes = crash == "after-start"
            ? [(1, IntegrationStepOutcomes.Interrupted, candidate.Base), (2, IntegrationStepOutcomes.Promoted, candidate.Base)]
            : [(1, IntegrationStepOutcomes.Promoted, candidate.Base)];
        Assert.Equal(
            promotes, store.Steps(claim.Candidate).Where(each => each.Step == IntegrationStepNames.Promote).Select(each => (each.Attempt, each.Outcome!, each.LineCommit!)));
        (int, string)[] settles = crash == "in-settle"
            ? [(1, IntegrationStepOutcomes.Interrupted), (2, IntegrationStepOutcomes.Done)]
            : [(1, IntegrationStepOutcomes.Done)];
        Assert.Equal(settles, store.Steps(claim.Candidate).Where(each => each.Step == IntegrationStepNames.Settle).Select(each => (each.Attempt, each.Outcome!)));
    }

    /// <summary>
    /// 🔴 An attempt run again goes from the line's commit its cut attempt recorded, never a fresh read: a line another moved meanwhile is not
    /// moved again, the candidate is formed again and the line keeps the other's commit.
    /// </summary>
    [Fact]
    public async Task A_cut_attempt_run_again_over_a_line_that_moved_moves_nothing()
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))]);
        Green();
        var first = Promotes(candidate);
        first.Seam = point => point == "after-start" ? throw new Killed() : Task.CompletedTask;
        await Assert.ThrowsAsync<Killed>(() => first.RunAsync(candidate.Claim, candidate.Ask));
        await GitAsync(candidate.Root, "checkout", "--quiet", "main");
        await CommitAsync(candidate.Root, "theirs.txt", "someone else's commit\n", "someone else lands first");
        var theirs = await CommitOfAsync(candidate.Root, "main");
        await GitAsync(candidate.Root, "checkout", "--quiet", "--detach");

        var store = Taker();
        var claim = store.Take(candidate.Claim.Candidate, candidate.Claim.Generation).Claim!;
        var run = await new IntegrationPromotes(store, TreesHome).RunAsync(claim, candidate.Ask);

        Assert.Equal(IntegrationPromoteRuns.Moved, run.Kind);
        Assert.Equal(theirs, await CommitOfAsync(candidate.Root, "main"));
        Assert.Equal(IntegrationCandidateEnds.FormedAgain, store.Candidate(claim.Candidate)!.EndedWhy);
    }

    /// <summary>A process in a job of the candidate's name, which the candidate's end must close: null where there is no job (off Windows).</summary>
    private static (ProcessJob Job, Process Process)? InTheJob(string candidate)
    {
        if (ProcessJob.Create(IntegrationJobs.NameOf(candidate)) is not { } job) return null;
        var info = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add("-e");
        info.ArgumentList.Add("setTimeout(() => {}, 120000)");
        var process = Process.Start(info)!;
        Assert.True(job.Join(process.Handle));
        return (job, process);
    }

    /// <summary>
    /// 🔴 The candidate's one job is closed at every end of the candidate (settled, handed back, stopped, dropped, and formed again): what a
    /// gate started in it is ended with the candidate, before the next one forms.
    /// </summary>
    [Theory]
    [InlineData("settled")]
    [InlineData("handed-back")]
    [InlineData("stopped")]
    [InlineData("dropped")]
    [InlineData("moved")]
    public async Task The_candidates_job_is_closed_at_each_end_of_the_candidate(string end)
    {
        var candidate = await FormedAsync([Member("feature/a", ("a.txt", "a\n"))], detached: end != "moved");
        (ProcessJob Job, Process Process)? held = null;
        try
        {
            Judging(_ =>
            {
                held = InTheJob(candidate.Claim.Candidate);
                return end switch
                {
                    "handed-back" => HandBack("feature/a"),
                    "stopped" => new IntegrationVerdict(VerdictActions.Stop) { Why = "the base fails" },
                    "dropped" => new IntegrationVerdict(VerdictActions.Dropped),
                    _ => new IntegrationVerdict(VerdictActions.Green),
                };
            });
            if (end == "moved")
            {
                // A verdict that is never asked: the job is made by the test beforehand, and the early end must close it too.
                held = InTheJob(candidate.Claim.Candidate);
                await CommitAsync(candidate.Root, "theirs.txt", "someone else's commit\n", "someone else lands first");
                await GitAsync(candidate.Root, "checkout", "--quiet", "--detach");
            }

            await Promotes(candidate).RunAsync(candidate.Claim, candidate.Ask);

            Assert.False(candidate.Now.Stands);
            if (held is { } job) Assert.True(job.Process.WaitForExit(10_000), $"the job's process outlived the candidate's end ({end})");
        }
        finally
        {
            if (held is { } job)
            {
                if (!job.Process.HasExited) job.Process.Kill(true);
                job.Process.Dispose();
                job.Job.Dispose();
            }
        }
    }
}
