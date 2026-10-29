using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1e part e2 over real git (D215's note; the integration design §5.6, §5.7): each crash point of a step recovered at the come-up by
/// asking git whether the candidate's tip is on the line, never the store alone, a resumed step run again from its start where it is not;
/// and the commits made on a branch after its request froze judged contained, superseded or new against the promoted line. Called from no
/// production path: the promotion's effect here is the test's own <c>update-ref</c>, as row e4's one-ref path makes it.
/// </summary>
/// <remarks>
/// A crash is the process gone between two writes: the case's first store stops where the crash point says, and a second store under another
/// run, which reads the first run as dead, takes the candidate and resumes it, as a shell coming up after one that died does.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationRunGitTests : LandedFixture
{
    private sealed class Killed() : Exception("the process died here");

    private IntegrationStore Store(string run, Func<StoreRun, bool>? alive = null) => new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    /// <summary>The second process's store: run-b, which reads run-a as dead.</summary>
    private IntegrationStore Taker() => Store("run-b", run => run.Run != "run-a");

    private static async Task<string> CommitOfAsync(string root, string reference) => (await GitAsync(root, "rev-parse", reference)).Trim();

    /// <summary>
    /// <c>engine</c> on <c>main</c>, nobody's checkout on the line (its HEAD detached), and a candidate's tip one commit on it, made aside as
    /// the integration tree makes one; a request for it, and the candidate formed on the line's commit with its form step ended at that tip.
    /// </summary>
    private async Task<(string Root, string Base, string Tip, IntegrationStore Store, IntegrationClaim Claim)> CandidateAsync(
        string repository = "engine", string request = "r1")
    {
        var root = await RepositoryAsync(repository);
        var @base = await CommitOfAsync(root, "main");
        await GitAsync(root, "checkout", "--quiet", "-b", "candidate");
        await CommitAsync(root, "work.txt", "the candidate's work\n", "the candidate");
        var tip = await CommitOfAsync(root, "candidate");
        await GitAsync(root, "checkout", "--quiet", "--detach", @base);

        var store = Store("run-a");
        Assert.Null(store.Add(new IntegrationRequest(
            request, repository, "aurora", "feature/q1-fix", tip, IntegrationRoutes.Recorded, "main", IntegrationRequester.Card,
            new LandingIntegrate().Resolved(), DateTimeOffset.UtcNow)));
        var formed = store.Form(new IntegrationForming(repository, "aurora", "main", @base, [request]));
        store.End(formed.Claim!, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Tip = tip, Members = [new IntegrationMemberChange(request, IntegrationMemberStates.Replayed) { Replayed = tip }],
        });
        return (root, @base, tip, store, formed.Claim!);
    }

    /// <summary>
    /// The promotion's effect where no worktree has the line out: one <c>update-ref</c> verifying the line's old value, the commit its step
    /// read at its start; the process killed where <paramref name="crash"/> says.
    /// </summary>
    private static Func<IntegrationStep, CancellationToken, Task<IntegrationStepEnd>> Promote(string root, string tip, string? crash = null) =>
        async (step, ct) =>
        {
            if (crash == "after-start") throw new Killed();
            var (code, _, err) = await WorkingTree.GitAsync(root, ["update-ref", "refs/heads/main", tip, step.LineCommit!], ct);
            if (crash == "after-update-ref") throw new Killed();
            return code == 0
                ? new IntegrationStepEnd(IntegrationStepOutcomes.Promoted) { Promoted = true }
                : new IntegrationStepEnd(IntegrationStepOutcomes.Held) { Said = $"the line moved under the candidate: {err.Trim()}" };
        };

    private static Task<IntegrationStepEnd> Settled(IntegrationStep step, CancellationToken ct) =>
        Task.FromResult(new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Members = [new IntegrationMemberChange("r1", IntegrationMemberStates.Settled)],
            Closes = [new IntegrationRequestClose("r1", IntegrationRequestCloses.Settled)],
            Ends = IntegrationCandidateEnds.Settled,
        });

    /// <summary>
    /// 🔴 Each crash point of the promotion and the settle after it, recovered at the come-up: the line moved exactly once whatever the
    /// point, a promotion read by ancestry where the line holds the tip (never made again, its step ended <c>promoted</c>), and a step the
    /// line does not show done run again from its start as its next attempt.
    /// </summary>
    [Theory]
    [InlineData("before-start")]
    [InlineData("after-start")]
    [InlineData("after-update-ref")]
    [InlineData("after-end")]
    [InlineData("in-settle")]
    public async Task Each_crash_point_is_recovered_by_ancestry_and_the_line_moves_once(string crash)
    {
        var (root, @base, tip, first, old) = await CandidateAsync();
        var steps = new IntegrationSteps(first);
        if (crash is "after-start" or "after-update-ref")
        {
            await Assert.ThrowsAsync<Killed>(() => steps.RunAsync(old, IntegrationStepNames.Promote, Promote(root, tip, crash), lineCommit: @base));
        }
        else if (crash is "after-end" or "in-settle")
        {
            await steps.RunAsync(old, IntegrationStepNames.Promote, Promote(root, tip), lineCommit: @base);
            if (crash == "in-settle")
            {
                await Assert.ThrowsAsync<Killed>(() => steps.RunAsync(old, IntegrationStepNames.Settle, (_, _) => throw new Killed()));
            }
        }

        // The come-up: another process takes the dead run's candidate and resumes it.
        var store = Taker();
        var claim = store.Take(old.Candidate, old.Generation).Claim!;
        var resumed = await new IntegrationSteps(store).ResumeAsync(claim, root);
        var expected = crash switch
        {
            "before-start" => (IntegrationResumes.Next, IntegrationStepNames.Form, IntegrationStepOutcomes.Done),
            "after-start" => (IntegrationResumes.Again, IntegrationStepNames.Promote, IntegrationStepOutcomes.Interrupted),
            "after-update-ref" => (IntegrationResumes.Promoted, IntegrationStepNames.Promote, IntegrationStepOutcomes.Promoted),
            "after-end" => (IntegrationResumes.Next, IntegrationStepNames.Promote, IntegrationStepOutcomes.Promoted),
            _ => (IntegrationResumes.Again, IntegrationStepNames.Settle, IntegrationStepOutcomes.Interrupted),
        };
        Assert.Equal(expected, (resumed.Kind, resumed.Step!.Step, resumed.Step.Outcome));
        if (crash == "after-update-ref") Assert.Contains("by ancestry", resumed.Step.Said, StringComparison.Ordinal);

        // The caller goes on from what the resume said: the promotion where the line does not hold the tip, then the settle.
        var going = new IntegrationSteps(store);
        if (store.Candidate(claim.Candidate)!.PromotedAt is null)
        {
            var promoted = await going.RunAsync(claim, IntegrationStepNames.Promote, Promote(root, tip), lineCommit: @base);
            Assert.Equal(IntegrationStepOutcomes.Promoted, promoted.Outcome);
        }

        await going.RunAsync(claim, IntegrationStepNames.Settle, Settled);

        Assert.Equal(tip, await CommitOfAsync(root, "main"));
        Assert.Equal(@base, await CommitOfAsync(root, "main^"));
        var candidate = store.Candidate(claim.Candidate)!;
        Assert.Equal(IntegrationCandidateEnds.Settled, candidate.EndedWhy);
        List<(int, string?)> attempts = crash == "after-start"
            ? [(1, IntegrationStepOutcomes.Interrupted), (2, IntegrationStepOutcomes.Promoted)]
            : [(1, IntegrationStepOutcomes.Promoted)];
        Assert.Equal(attempts, store.Steps(claim.Candidate).Where(each => each.Step == IntegrationStepNames.Promote).Select(each => (each.Attempt, each.Outcome)));
        Assert.Equal(IntegrationRequestCloses.Settled, store.Request("r1")!.ClosedWhy);
    }

    /// <summary>
    /// 🔴 A line moved on past the promotion still holds the candidate's tip, so the promotion is read by ancestry; a line moved elsewhere
    /// before it does not, so the step runs again from its start, and its effect, verifying the line's old value, refuses to move a line that
    /// moved: the step is held for the candidate to be formed again, and the line keeps the other's commit.
    /// </summary>
    [Fact]
    public async Task A_line_moved_on_after_the_promotion_holds_it_and_a_line_moved_elsewhere_before_it_is_never_moved_again()
    {
        var (root, @base, tip, first, old) = await CandidateAsync();
        await Assert.ThrowsAsync<Killed>(() => new IntegrationSteps(first).RunAsync(old, IntegrationStepNames.Promote, Promote(root, tip, "after-update-ref"), lineCommit: @base));
        await GitAsync(root, "checkout", "--quiet", "main");
        await CommitAsync(root, "later.txt", "a later commit\n", "the line moves on");
        await GitAsync(root, "checkout", "--quiet", "--detach");

        var store = Taker();
        var claim = store.Take(old.Candidate, old.Generation).Claim!;
        var resumed = await new IntegrationSteps(store).ResumeAsync(claim, root);

        Assert.Equal(IntegrationResumes.Promoted, resumed.Kind);
        Assert.NotNull(store.Candidate(claim.Candidate)!.PromotedAt);

        // Another candidate, whose run dies after its promotion's start, and a line someone else moves meanwhile.
        var (otherRoot, otherBase, otherTip, otherFirst, otherOld) = await CandidateAsync("portal", "r2");
        await Assert.ThrowsAsync<Killed>(() => new IntegrationSteps(otherFirst).RunAsync(otherOld, IntegrationStepNames.Promote, Promote(otherRoot, otherTip, "after-start"), lineCommit: otherBase));
        await GitAsync(otherRoot, "checkout", "--quiet", "main");
        await CommitAsync(otherRoot, "theirs.txt", "someone else's commit\n", "someone else lands first");
        await GitAsync(otherRoot, "checkout", "--quiet", "--detach");
        var theirs = await CommitOfAsync(otherRoot, "main");

        var otherStore = Taker();
        var otherClaim = otherStore.Take(otherOld.Candidate, otherOld.Generation).Claim!;
        var again = await new IntegrationSteps(otherStore).ResumeAsync(otherClaim, otherRoot);
        Assert.Equal(IntegrationResumes.Again, again.Kind);
        var held = await new IntegrationSteps(otherStore).RunAsync(otherClaim, IntegrationStepNames.Promote, Promote(otherRoot, otherTip), lineCommit: otherBase);

        Assert.Equal(IntegrationStepOutcomes.Held, held.Outcome);
        Assert.Equal(theirs, await CommitOfAsync(otherRoot, "main"));
        Assert.Null(otherStore.Candidate(otherClaim.Candidate)!.PromotedAt);
    }

    /// <summary>
    /// 🔴 Never the store alone: where git cannot say whether the line holds the tip, nothing is written and the step stays as it stands, to
    /// be asked again; where the store records a promotion the line no longer holds (moved back by hand), nothing is retried and nothing written.
    /// </summary>
    [Fact]
    public async Task A_resume_git_cannot_answer_or_whose_line_was_moved_back_writes_nothing()
    {
        var (root, @base, tip, store, claim) = await CandidateAsync();
        var steps = new IntegrationSteps(store);

        // The candidate's tip, as a resolve's end writes it, a commit this repository does not hold: git's answer is neither yes nor no.
        var resolve = store.Begin(claim, IntegrationStepNames.Resolve);
        store.End(claim, resolve.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Tip = "0123456789abcdef0123456789abcdef01234567" });
        var open = store.Begin(claim, IntegrationStepNames.Gates);

        var unread = await steps.ResumeAsync(claim, root);

        Assert.Equal(IntegrationResumes.Unread, unread.Kind);
        Assert.Equal(open, store.OpenStep(claim.Candidate));

        // Promoted and recorded, then the line put back by hand: the store says promoted, git says the line does not hold it.
        store.End(claim, open.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Tip = tip });
        await steps.RunAsync(claim, IntegrationStepNames.Promote, Promote(root, tip), lineCommit: @base);
        await GitAsync(root, "update-ref", "refs/heads/main", @base, tip);
        var written = store.Steps(claim.Candidate).Count;

        var unheld = await steps.ResumeAsync(claim, root);

        Assert.Equal(IntegrationResumes.Unheld, unheld.Kind);
        Assert.Contains("does not hold", unheld.Said, StringComparison.Ordinal);
        Assert.Equal(written, store.Steps(claim.Candidate).Count);
        Assert.True(store.Candidate(claim.Candidate)!.Stands);
    }

    // ——— After landing: contained, superseded, new (§5.7).

    private static string Lines(params (int Line, string Text)[] changes)
    {
        var lines = Enumerable.Range(1, 20).Select(each => $"line {each}").ToArray();
        foreach (var (line, text) in changes) lines[line - 1] = text;
        return string.Join("\n", lines) + "\n";
    }

    /// <summary>
    /// 🔴 The commits a unit's branch made after its request froze, judged against the promoted line (D87 point 1, never the branch's
    /// ancestry): one whose patch the line holds, and one whose replay is empty, are contained; one whose every conflicted hunk the line changed
    /// after the frozen base by a commit outside the unit's work is superseded by that commit; one that adds work, and one that conflicts
    /// only with the unit's own landed work, are new.
    /// </summary>
    [Fact]
    public async Task Commits_after_the_frozen_tip_are_judged_contained_superseded_or_new_against_the_promoted_line()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "lines.txt", Lines(), "twenty lines");
        var @base = await CommitOfAsync(root, "main");

        // The unit's branch, frozen at its request after one commit.
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/unit");
        await CommitAsync(root, "lines.txt", Lines((2, "two, the unit's")), "the unit's work");
        var frozen = await CommitOfAsync(root, "HEAD");

        // The line meanwhile: three other units' commits, then the unit's own landed work, resolved to other words than its branch's.
        await GitAsync(root, "checkout", "--quiet", "main");
        await CommitAsync(root, "lines.txt", Lines((15, "fifteen, z's")), "z");
        await CommitAsync(root, "lines.txt", Lines((8, "eight, x's"), (15, "fifteen, z's")), "x");
        var x = await CommitOfAsync(root, "HEAD");
        await CommitAsync(root, "lines.txt", Lines((8, "eight, x's"), (12, "twelve, y's"), (15, "fifteen, z's"), (18, "eighteen, y's")), "y");
        await CommitAsync(root, "lines.txt", Lines((2, "two, resolved"), (8, "eight, x's"), (12, "twelve, y's"), (15, "fifteen, z's"), (18, "eighteen, y's")), "the unit, landed");
        var landed = await CommitOfAsync(root, "HEAD");

        // The branch after its freeze.
        await GitAsync(root, "checkout", "--quiet", "feature/unit");
        await CommitAsync(root, "lines.txt", Lines((2, "two, the unit's"), (15, "fifteen, z's")), "z's change, again");
        var patch = await CommitOfAsync(root, "HEAD");
        await CommitAsync(root, "more.txt", "more work\n", "more work");
        var more = await CommitOfAsync(root, "HEAD");
        await CommitAsync(root, "lines.txt", Lines((2, "two, the unit's"), (8, "eight, the unit's"), (15, "fifteen, z's")), "line 8, the unit's");
        var superseded = await CommitOfAsync(root, "HEAD");
        await CommitAsync(root, "lines.txt", Lines((2, "two, the unit's"), (8, "eight, the unit's"), (12, "twelve, y's"), (15, "fifteen, z's")), "y's change, by hand");
        var empty = await CommitOfAsync(root, "HEAD");
        await CommitAsync(root, "lines.txt", Lines((2, "two, again"), (8, "eight, the unit's"), (12, "twelve, y's"), (15, "fifteen, z's")), "line 2 again");
        var own = await CommitOfAsync(root, "HEAD");
        await GitAsync(root, "checkout", "--quiet", "--detach", "main");

        var judged = await IntegrationAfterLanding.JudgeAsync(root, "main", landed, @base, frozen, own, [landed], CancellationToken.None);

        Assert.NotNull(judged);
        Assert.Equal(
            [
                (patch, AfterLandingDispositions.Contained, (string?)null),
                (more, AfterLandingDispositions.New, null),
                (superseded, AfterLandingDispositions.Superseded, x),
                (empty, AfterLandingDispositions.Contained, null),
                (own, AfterLandingDispositions.New, null),
            ],
            judged.Select(each => (each.Sha, each.Disposition, each.By)));
        Assert.Equal(["lines.txt"], judged.Single(each => each.Sha == superseded).Files);
    }
}
