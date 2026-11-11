using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The supervisor's decision drawn on a run's review (<see cref="RunReviewSupervisor"/>): the newest one kept on the attempt read, as memory
/// keeps it, held with the moment it goes on, then released, then applied; whether it names the record read; and none drawn on another attempt.
/// </summary>
public sealed class RunReviewSupervisorTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "review-supervisor", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-12T09:00:00Z");

    private const string Revision = "aaaaaaa1bbbbbbb2";

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private sealed class Admits : IDecisionAdmission
    {
        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) => new("g-1", null) { Window = TimeSpan.FromMinutes(10) };
    }

    private QuestionSupervisorDecisions Decisions() => new(_home, new Admits()) { LandingApplied = LandingApplied.Of(_home) };

    private ReviewRecord Kept(int attempt = 1)
    {
        var record = new ReviewRecord("q7", "review", attempt, ReviewKinds.Folder, Revision, At) { Place = "reports" };
        ReviewRecords.Keep(_home, record);
        return record;
    }

    private static SupervisorLanding Landing(string? revision = Revision, int attempt = 1) =>
        new("s2", "q7", "review", attempt, "g-1", SupervisorLandingActs.Accept, [new SupervisorReason("The files are the brief's.", "the record")],
            new SupervisorHolder(RoleHolders.Terminal, null)) { Revision = revision };

    [Fact]
    public async Task A_held_decision_is_drawn_on_the_record_it_names_then_released_then_gone_on()
    {
        var record = Kept();
        var decisions = Decisions();
        var kept = await decisions.LandingAsync(Landing(), "reports", null, At);

        var held = RunReviewSupervisor.Of(decisions.LandingOn("q7", "review", 1), record)!;

        Assert.Equal((kept.Decision, "s2", "accept", "held", 1, Revision, true, ReviewKinds.Folder),
            (held.Id, held.Session, held.Do, held.State, held.Attempt, held.Revision, held.OnRecord, held.Work));
        Assert.Equal(At.AddMinutes(10), held.GoesOnAt);
        Assert.Equal(RoleHolders.Terminal, held.Holder);

        await decisions.ReleaseLandingsAsync(new LandingRelease(_ => Task.FromResult<string?>(null), _ => null), At.AddMinutes(11));
        Assert.Equal("released", RunReviewSupervisor.Of(decisions.LandingOn("q7", "review", 1), record)!.State);

        decisions.LandingWords.Settled(kept.Decision!, SupervisorState.WentOn, null);
        Assert.Equal("went-on", RunReviewSupervisor.Of(decisions.LandingOn("q7", "review", 1), record)!.State);
    }

    [Theory]
    [InlineData(ReviewKinds.Branch)]
    [InlineData(ReviewKinds.Checkout)]
    [InlineData(ReviewKinds.Folder)]
    [InlineData(ReviewKinds.Copy)]
    [InlineData(ReviewKinds.Plan)]
    [InlineData(ReviewKinds.Answer)]
    public async Task The_heads_card_reads_an_accept_by_the_kind_of_work_its_attempts_record_holds(string kind)
    {
        ReviewRecords.Keep(_home, new ReviewRecord("q7", "review", 1, kind, Revision, At) { Place = "reports" });
        var decisions = Decisions();
        await decisions.LandingAsync(Landing(), "reports", null, At);
        await decisions.LandingAsync(Landing() with { Node = "approval", Do = SupervisorLandingActs.Approve }, "reports", null, At);

        var landings = decisions.LandingsOf("s2");

        Assert.Equal(kind, LandingWork.Of(_home, Assert.Single(landings, view => view.Node == "review")));
        Assert.Null(LandingWork.Of(_home, Assert.Single(landings, view => view.Node == "approval")));
    }

    [Fact]
    public async Task One_naming_another_revision_is_drawn_off_the_record_and_none_is_drawn_on_another_attempt()
    {
        var record = Kept();
        var decisions = Decisions();
        await decisions.LandingAsync(Landing("ccccccc3"), "reports", null, At);

        Assert.False(RunReviewSupervisor.Of(decisions.LandingOn("q7", "review", 1), record)!.OnRecord);
        Assert.Null(decisions.LandingOn("q7", "review", 2));
        Assert.Null(RunReviewSupervisor.Of(null, record));
    }

    [Fact]
    public async Task One_naming_no_revision_stands_on_no_record_but_a_branchs()
    {
        var record = Kept();
        var decisions = Decisions();
        await decisions.LandingAsync(Landing(revision: null), "reports", null, At);

        Assert.False(RunReviewSupervisor.Of(decisions.LandingOn("q7", "review", 1), record)!.OnRecord);
        Assert.True(RunReviewSupervisor.Of(decisions.LandingOn("q7", "review", 1), record with { Kind = ReviewKinds.Branch })!.OnRecord);
    }
}
