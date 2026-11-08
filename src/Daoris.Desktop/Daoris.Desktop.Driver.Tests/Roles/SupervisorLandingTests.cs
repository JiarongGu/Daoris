using System.Collections.Immutable;
using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The supervisor's landing decisions: an accept, approve or reject of a run's step held for the role's window with <i>Overrule</i>, then
/// released as the node's word on the run's record, live until the pass applies it; a restart emptying them, the start's sweep recording a
/// word the pass applied as made and closing the rest. The step's own refusals read in the order the keeping reads them. Each seen failing
/// first with its guard taken out.
/// </summary>
public sealed class SupervisorLandingTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-landing", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-08T14:00:00Z");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private sealed class Admits(string? generation) : IDecisionAdmission
    {
        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) =>
            generation is null ? DecisionAdmitted.Refused(SupervisorWhy.NotStaffed) : new(generation, null) { Window = TimeSpan.FromMinutes(10) };
    }

    private QuestionSupervisorDecisions Decisions() => new(_home, new Admits("g-1")) { LandingApplied = LandingApplied.Of(_home) };

    [Fact]
    public async Task A_decision_is_live_when_its_word_is_committed_so_a_pass_between_cannot_drop_it_and_a_refused_word_leaves_it_not_live()
    {
        var decisions = Decisions();
        var kept = await decisions.LandingAsync(Landing(), "reports", null, At);
        var liveAtCommit = new List<bool>();

        Assert.Equal([SupervisorState.Released], await decisions.ReleaseLandingsAsync(
            new LandingRelease(_ => Task.FromResult<string?>(null), _ => { liveAtCommit.Add(decisions.LandingWords.Live(kept.Decision!)); return null; }), At.AddMinutes(11)));
        Assert.Equal([true], liveAtCommit);

        var refused = await decisions.LandingAsync(Landing(attempt: 2), "reports", null, At);
        Assert.Equal([SupervisorState.Stale], await decisions.ReleaseLandingsAsync(
            new LandingRelease(_ => Task.FromResult<string?>(null), _ => RunNodeWords.Stale), At.AddMinutes(11)));
        Assert.False(decisions.LandingWords.Live(refused.Decision!));
    }

    [Theory]
    [InlineData("review", 1)]
    [InlineData("gate/review", 2)]
    [InlineData("a/b/c", 12)]
    public void A_landings_subject_gives_back_its_node_whether_or_not_the_id_holds_a_slash(string node, int attempt)
    {
        Assert.Equal(("q7", node, attempt), SupervisorSubject.Landing("s1", "q7", node, attempt).LandingParts);
        Assert.Null(new SupervisorSubject(SupervisorKinds.Landing, "s1", "q7/review").LandingParts);
        Assert.Null(new SupervisorSubject(SupervisorKinds.Landing, "s1", "q7//1").LandingParts);
    }

    private static SupervisorLanding Landing(string act = SupervisorLandingActs.Accept, int attempt = 1, string generation = "g-1") =>
        new("s1", "q7", "review", attempt, generation, act, [new SupervisorReason("The diff is the quest's brief.", "the diff")], new SupervisorHolder(RoleHolders.Terminal, null));

    private static LandingRelease Line(string? why = null, string? word = null, List<SupervisorLandingDue>? handed = null) => new(
        _ => Task.FromResult(why), due =>
        {
            handed?.Add(due);
            return word;
        });

    private void Applied(int attempt)
    {
        var files = new NodeRecordFiles(_home, () => true);
        var status = NodeLifecycle.Start(RunUnits.IdOf("q7"), "review") with { State = NodeStates.Reached, By = NodeSource.Supervisor, Attempt = attempt };
        files.Write(new NodeRecord(RunUnits.IdOf("q7")) { Statuses = new Dictionary<string, NodeStatus> { ["review"] = status }.ToImmutableSortedDictionary(StringComparer.Ordinal) });
    }

    [Fact]
    public async Task An_accept_is_held_for_the_window_then_released_as_the_nodes_word_and_live_until_the_pass_settles_it()
    {
        var decisions = Decisions();
        var kept = await decisions.LandingAsync(Landing(), "reports", null, At);

        Assert.True(kept.Kept, kept.Why);
        Assert.Equal(At.AddMinutes(10), kept.GoesOnAt);
        var file = File.ReadAllText(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1" + QuestionSupervisorDecisions.DecisionsSuffix), kept.Decision + ".json"));
        Assert.Contains("\"landing\"", file);
        Assert.False(decisions.Refers(SupervisorSubject.Landing("s1", "q7", "review", 1)));

        var handed = new List<SupervisorLandingDue>();
        Assert.Empty(await decisions.ReleaseLandingsAsync(Line(handed: handed), At.AddMinutes(5)));
        Assert.Empty(handed);
        Assert.False(decisions.LandingWords.Live(kept.Decision!));

        Assert.Equal([SupervisorState.Released], await decisions.ReleaseLandingsAsync(Line(handed: handed), At.AddMinutes(11)));
        Assert.Equal((kept.Decision, "review", 1, SupervisorLandingActs.Accept), (handed.Single().Id, handed.Single().Node, handed.Single().Attempt, handed.Single().Do));
        Assert.True(decisions.LandingWords.Live(kept.Decision!));

        decisions.LandingWords.Settled(kept.Decision!, SupervisorState.WentOn, null);
        Assert.False(decisions.LandingWords.Live(kept.Decision!));
        Assert.Equal(SupervisorState.WentOn, decisions.LandingsOf("s1").Single().State);
    }

    [Fact]
    public async Task ROLESREVIEW2b_a_retry_of_a_held_decision_is_refused_where_the_step_now_refuses_it()
    {
        var decisions = Decisions();
        var kept = await decisions.LandingAsync(Landing(), "reports", null, At);

        Assert.Equal(kept.Decision, (await decisions.LandingAsync(Landing(), "reports", null, At.AddMinutes(1))).Decision);
        var retried = await decisions.LandingAsync(Landing(), "reports", SupervisorWhy.OwnerHeld, At.AddMinutes(2));

        Assert.False(retried.Kept);
        Assert.Equal(SupervisorWhy.OwnerHeld, retried.Why);
    }

    [Fact]
    public async Task The_persons_overrule_inside_the_window_keeps_the_step_theirs_and_nothing_is_released_nor_decided_again()
    {
        var decisions = Decisions();
        var kept = await decisions.LandingAsync(Landing(), "reports", null, At);

        Assert.Equal(SupervisorState.Overruled, await decisions.OverruleAsync("s1", kept.Decision!, (_, _, _) => Task.FromResult(true), At.AddMinutes(1)));
        var handed = new List<SupervisorLandingDue>();
        Assert.Empty(await decisions.ReleaseLandingsAsync(Line(handed: handed), At.AddMinutes(11)));
        Assert.Empty(handed);
        Assert.Equal(SupervisorWhy.Answered, (await decisions.LandingAsync(Landing(), "reports", null, At.AddMinutes(12))).Why);
        Assert.False(decisions.Refers(SupervisorSubject.Landing("s1", "q7", "review", 1)));
    }

    [Fact]
    public async Task A_release_the_step_or_the_persons_holds_refuse_goes_stale_and_a_persons_word_waiting_supersedes_it()
    {
        var decisions = Decisions();
        var held = await decisions.LandingAsync(Landing(attempt: 1), "reports", null, At);
        var worded = await decisions.LandingAsync(Landing(attempt: 2), "reports", null, At);
        var line = new LandingRelease(
            due => Task.FromResult(due.Attempt == 1 ? SupervisorWhy.OwnerHeld : null), due => due.Attempt == 2 ? RunNodeWords.Worded : null);
        Assert.Equal(2, (await decisions.ReleaseLandingsAsync(line, At.AddMinutes(11))).Count);

        var seen = decisions.LandingsOf("s1").ToDictionary(view => view.Id);
        Assert.Equal((SupervisorState.Stale, SupervisorWhy.OwnerHeld), (seen[held.Decision!].State, seen[held.Decision!].Why));
        Assert.Equal(SupervisorState.Superseded, seen[worded.Decision!].State);
        Assert.False(decisions.Refers(SupervisorSubject.Landing("s1", "q7", "review", 1)));
        Assert.False(decisions.Refers(SupervisorSubject.Landing("s1", "q7", "review", 2)));
    }

    private sealed class Switched : IDecisionAdmission
    {
        public string Generation { get; set; } = "g-1";

        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) => new(Generation, null) { Window = TimeSpan.FromMinutes(10) };
    }

    [Fact]
    public async Task A_role_changed_before_the_release_goes_stale_and_releases_nothing()
    {
        var admission = new Switched();
        var decisions = new QuestionSupervisorDecisions(_home, admission);
        var kept = await decisions.LandingAsync(Landing(), "reports", null, At);
        admission.Generation = "g-2";

        var handed = new List<SupervisorLandingDue>();
        Assert.Equal([SupervisorState.Stale], await decisions.ReleaseLandingsAsync(Line(handed: handed), At.AddMinutes(11)));
        Assert.Empty(handed);
        Assert.Equal((SupervisorState.Stale, SupervisorWhy.RoleChanged), (decisions.LandingsOf("s1").Single().State, decisions.LandingsOf("s1").Single().Why));
        Assert.False(decisions.LandingWords.Live(kept.Decision!));
    }

    [Fact]
    public async Task A_refused_step_keeps_nothing_and_a_decide_on_a_changed_hold_is_refused()
    {
        var decisions = Decisions();
        Assert.Equal(SupervisorWhy.OwnerHeld, (await decisions.LandingAsync(Landing(), "reports", SupervisorWhy.OwnerHeld, At)).Why);
        Assert.Equal(SupervisorWhy.RoleChanged, (await decisions.LandingAsync(Landing(generation: "g-0"), "reports", null, At)).Why);
        Assert.Empty(decisions.LandingsOf("s1"));
        Assert.True(decisions.Refers(SupervisorSubject.Landing("s1", "q7", "review", 1)));
    }

    [Fact]
    public async Task A_leave_is_final_at_once_and_a_second_decide_on_the_step_is_refused_answered()
    {
        var decisions = Decisions();
        var left = await decisions.LandingAsync(Landing(SupervisorLandingActs.Hold) with { Bound = "go-ahead" }, "reports", null, At);

        Assert.True(left.Kept);
        Assert.Null(left.GoesOnAt);
        Assert.Equal(SupervisorState.Left, decisions.LandingsOf("s1").Single().State);
        Assert.Equal(SupervisorWhy.Answered, (await decisions.LandingAsync(Landing(), "reports", null, At)).Why);
    }

    [Fact]
    public async Task A_released_accept_then_a_restart_before_the_pass_is_closed_stale_and_the_step_the_persons()
    {
        var before = Decisions();
        var kept = await before.LandingAsync(Landing(), "reports", null, At);
        await before.ReleaseLandingsAsync(Line(), At.AddMinutes(11));
        Assert.True(before.LandingWords.Live(kept.Decision!));

        var after = Decisions();
        Assert.False(after.LandingWords.Live(kept.Decision!));
        after.SweepAtStart(At.AddHours(1));

        var view = after.LandingsOf("s1").Single();
        Assert.Equal((SupervisorState.Stale, SupervisorWhy.Restarted), (view.State, view.Why));
        Assert.True(after.Closed(SupervisorSubject.Landing("s1", "q7", "review", 1)));
        Assert.Equal(SupervisorWhy.Overruled, (await after.LandingAsync(Landing(), "reports", null, At.AddHours(2))).Why);
    }

    [Fact]
    public async Task A_word_the_pass_applied_before_a_restart_is_recorded_made_by_the_sweep()
    {
        var before = Decisions();
        var kept = await before.LandingAsync(Landing(), "reports", null, At);
        await before.ReleaseLandingsAsync(Line(), At.AddMinutes(11));
        Applied(attempt: 1);

        var after = Decisions();
        after.SweepAtStart(At.AddHours(1));

        Assert.Equal(SupervisorState.WentOn, after.LandingsOf("s1").Single().State);
        Assert.False(after.Closed(SupervisorSubject.Landing("s1", "q7", "review", 1)));
        Assert.NotNull(kept.Decision);
    }

    [Fact]
    public async Task A_held_decision_at_a_restart_goes_stale_and_a_word_applied_on_another_attempt_is_not_its_own()
    {
        var before = Decisions();
        await before.LandingAsync(Landing(attempt: 2), "reports", null, At);
        await before.ReleaseLandingsAsync(Line(), At.AddMinutes(11));
        Applied(attempt: 1);

        Decisions().SweepAtStart(At.AddHours(1));

        Assert.Equal(SupervisorState.Stale, Decisions().LandingsOf("s1").Single().State);
    }

    [Fact]
    public async Task The_steps_own_refusals_read_in_order_and_a_leave_is_refused_for_the_first_two_alone()
    {
        static LandingStep Step(bool goAhead = false, params string[] acts) =>
            new("q7", "review", 1, "reports", acts.Length == 0 ? [SupervisorLandingActs.Accept] : acts) { GoAheadOpen = goAhead };
        static Func<Task<string?>> Pushes(string? why) => () => Task.FromResult(why);

        Assert.Equal(SupervisorWhy.Answered, await LandingJudge.RefusedAsync(null, "accept", true, Pushes(SupervisorWhy.LandingPushes)));
        Assert.Equal(SupervisorWhy.GoAheadOpen, await LandingJudge.RefusedAsync(Step(goAhead: true), "accept", true, Pushes(SupervisorWhy.LandingPushes)));
        Assert.Equal(SupervisorWhy.GoAheadOpen, await LandingJudge.RefusedAsync(Step(goAhead: true), "hold", false, Pushes(null)));
        Assert.Equal(SupervisorWhy.NotReferred, await LandingJudge.RefusedAsync(Step(), "approve", false, Pushes(null)));
        Assert.Equal(SupervisorWhy.OwnerHeld, await LandingJudge.RefusedAsync(Step(), "accept", true, Pushes(SupervisorWhy.LandingPushes)));
        Assert.Equal(SupervisorWhy.LandingPushes, await LandingJudge.RefusedAsync(Step(), "accept", false, Pushes(SupervisorWhy.LandingPushes)));
        Assert.Null(await LandingJudge.RefusedAsync(Step(), "accept", false, Pushes(null)));
        Assert.Null(await LandingJudge.RefusedAsync(Step(), "hold", true, Pushes(SupervisorWhy.LandingPushes)));
    }
}
