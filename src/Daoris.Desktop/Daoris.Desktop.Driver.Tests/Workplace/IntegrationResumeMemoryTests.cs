using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// On a repository kept in memory: each crash point of a step recovered at the come-up by asking the repository whether the candidate's tip
/// is on the line, never the store alone, a resumed step run again from its start where it is not. The promotion's effect here is the
/// test's own ref move, as the one-ref path makes it.
/// </summary>
/// <remarks>
/// A crash is the process gone between two writes: the case's first store stops where the crash point says, and a second store under another
/// run, which reads the first run as dead, takes the candidate and resumes it, as a shell coming up after one that died does.
/// </remarks>
public sealed class IntegrationResumeMemoryTests : IDisposable
{
    private readonly IntegrationMemoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private sealed class Killed() : Exception("the process died here");

    private IntegrationStore Taker() => _fixture.Store("run-b", run => run.Run != "run-a");

    private Task<string?> CommitOfAsync(string reference) => _fixture.Repo.CommitOfAsync(_fixture.Root, reference, CancellationToken.None);

    /// <summary>
    /// <c>main</c> with nobody's checkout on it (HEAD detached), and a candidate's tip one commit on it, made aside; a request for it, and
    /// the candidate formed on the line's commit with its form step ended at that tip.
    /// </summary>
    private (string Base, string Tip, IntegrationStore Store, IntegrationClaim Claim) Candidate(string repository = "engine", string request = "r1")
    {
        var @base = _fixture.Repo.CommitOfAsync(_fixture.Root, "main", CancellationToken.None).GetAwaiter().GetResult()!;
        _fixture.Switch(null, @base);
        var tip = _fixture.Branch($"candidate-{request}", @base, $"work-{request}.txt", "the candidate's work\n");

        var store = _fixture.Store("run-a");
        Assert.Null(store.Add(new IntegrationRequest(
            request, repository, "aurora", "feature/q1-fix", tip, IntegrationRoutes.Recorded, "main", IntegrationRequester.Card,
            new LandingIntegrate().Resolved(), DateTimeOffset.UtcNow)));
        var formed = store.Form(new IntegrationForming(repository, "aurora", "main", @base, [request]));
        store.End(formed.Claim!, formed.Step!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done)
        {
            Tip = tip, Members = [new IntegrationMemberChange(request, IntegrationMemberStates.Replayed) { Replayed = tip }],
        });
        return (@base, tip, store, formed.Claim!);
    }

    /// <summary>
    /// The promotion's effect where no working tree has the line out: one ref move verifying the line's old value, the commit its step
    /// read at its start; the process killed where <paramref name="crash"/> says.
    /// </summary>
    private Func<IntegrationStep, CancellationToken, Task<IntegrationStepEnd>> Promote(string tip, string? crash = null) =>
        async (step, ct) =>
        {
            if (crash == "after-start") throw new Killed();
            var moved = await _fixture.Repo.UpdateRefAsync(_fixture.Root, "refs/heads/main", tip, step.LineCommit!, ct);
            if (crash == "after-update-ref") throw new Killed();
            return moved.Ok
                ? new IntegrationStepEnd(IntegrationStepOutcomes.Promoted) { Promoted = true }
                : new IntegrationStepEnd(IntegrationStepOutcomes.Held) { Said = $"the line moved under the candidate: {moved.Error.Trim()}" };
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
        using var _ = _fixture.Use();
        var (@base, tip, first, old) = Candidate();
        var steps = new IntegrationSteps(first);
        if (crash is "after-start" or "after-update-ref")
        {
            await Assert.ThrowsAsync<Killed>(() => steps.RunAsync(old, IntegrationStepNames.Promote, Promote(tip, crash), lineCommit: @base));
        }
        else if (crash is "after-end" or "in-settle")
        {
            await steps.RunAsync(old, IntegrationStepNames.Promote, Promote(tip), lineCommit: @base);
            if (crash == "in-settle")
            {
                await Assert.ThrowsAsync<Killed>(() => steps.RunAsync(old, IntegrationStepNames.Settle, (_, _) => throw new Killed()));
            }
        }

        // The come-up: another process takes the dead run's candidate and resumes it.
        var store = Taker();
        var claim = store.Take(old.Candidate, old.Generation).Claim!;
        var resumed = await new IntegrationSteps(store).ResumeAsync(claim, _fixture.Root);
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
            var promoted = await going.RunAsync(claim, IntegrationStepNames.Promote, Promote(tip), lineCommit: @base);
            Assert.Equal(IntegrationStepOutcomes.Promoted, promoted.Outcome);
        }

        await going.RunAsync(claim, IntegrationStepNames.Settle, Settled);

        Assert.Equal(tip, await CommitOfAsync("main"));
        Assert.Equal(@base, await CommitOfAsync("main^"));
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
        using var _ = _fixture.Use();
        var (@base, tip, first, old) = Candidate();
        await Assert.ThrowsAsync<Killed>(() => new IntegrationSteps(first).RunAsync(old, IntegrationStepNames.Promote, Promote(tip, "after-update-ref"), lineCommit: @base));
        _fixture.Switch("main");
        _fixture.Commit("the line moves on", "later.txt", "a later commit\n");
        _fixture.Switch(null, _fixture.Head);

        var store = Taker();
        var claim = store.Take(old.Candidate, old.Generation).Claim!;
        var resumed = await new IntegrationSteps(store).ResumeAsync(claim, _fixture.Root);

        Assert.Equal(IntegrationResumes.Promoted, resumed.Kind);
        Assert.NotNull(store.Candidate(claim.Candidate)!.PromotedAt);

        // Another candidate, whose run dies after its promotion's start, and a line someone else moves meanwhile.
        var (otherBase, otherTip, otherFirst, otherOld) = Candidate("portal", "r2");
        await Assert.ThrowsAsync<Killed>(() => new IntegrationSteps(otherFirst).RunAsync(otherOld, IntegrationStepNames.Promote, Promote(otherTip, "after-start"), lineCommit: otherBase));
        _fixture.Switch("main");
        _fixture.Commit("someone else lands first", "theirs.txt", "someone else's commit\n");
        _fixture.Switch(null, _fixture.Head);
        var theirs = await CommitOfAsync("main");

        var otherStore = Taker();
        var otherClaim = otherStore.Take(otherOld.Candidate, otherOld.Generation).Claim!;
        var again = await new IntegrationSteps(otherStore).ResumeAsync(otherClaim, _fixture.Root);
        Assert.Equal(IntegrationResumes.Again, again.Kind);
        var held = await new IntegrationSteps(otherStore).RunAsync(otherClaim, IntegrationStepNames.Promote, Promote(otherTip), lineCommit: otherBase);

        Assert.Equal(IntegrationStepOutcomes.Held, held.Outcome);
        Assert.Equal(theirs, await CommitOfAsync("main"));
        Assert.Null(otherStore.Candidate(otherClaim.Candidate)!.PromotedAt);
    }

    /// <summary>
    /// 🔴 Never the store alone: where the repository cannot say whether the line holds the tip, nothing is written and the step stays as it
    /// stands, to be asked again; where the store records a promotion the line no longer holds (moved back by hand), nothing is retried and
    /// nothing written.
    /// </summary>
    [Fact]
    public async Task A_resume_the_repository_cannot_answer_or_whose_line_was_moved_back_writes_nothing()
    {
        using var _ = _fixture.Use();
        var (@base, tip, store, claim) = Candidate();
        var steps = new IntegrationSteps(store);

        // The candidate's tip, as a resolve's end writes it, a commit this repository does not hold: the answer is neither yes nor no.
        var resolve = store.Begin(claim, IntegrationStepNames.Resolve);
        store.End(claim, resolve.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Tip = "0123456789abcdef0123456789abcdef01234567" });
        var open = store.Begin(claim, IntegrationStepNames.Gates);

        var unread = await steps.ResumeAsync(claim, _fixture.Root);

        Assert.Equal(IntegrationResumes.Unread, unread.Kind);
        Assert.Equal(open, store.OpenStep(claim.Candidate));

        // Promoted and recorded, then the line put back by hand: the store says promoted, the repository says the line does not hold it.
        store.End(claim, open.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Done) { Tip = tip });
        await steps.RunAsync(claim, IntegrationStepNames.Promote, Promote(tip), lineCommit: @base);
        Assert.True((await _fixture.Repo.UpdateRefAsync(_fixture.Root, "refs/heads/main", @base, tip, CancellationToken.None)).Ok);
        var written = store.Steps(claim.Candidate).Count;

        var unheld = await steps.ResumeAsync(claim, _fixture.Root);

        Assert.Equal(IntegrationResumes.Unheld, unheld.Kind);
        Assert.Contains("does not hold", unheld.Said, StringComparison.Ordinal);
        Assert.Equal(written, store.Steps(claim.Candidate).Count);
        Assert.True(store.Candidate(claim.Candidate)!.Stands);
    }
}
