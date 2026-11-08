using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An agent supervisor's attempts: each written before its turn starts and its outcome beside it at its end, one per choice of the role's
/// order, an attempt with no outcome closed failed at a start or past its bound, and a subject whose attempts ended the person's.
/// </summary>
public sealed class SupervisorAttemptsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 14, 0, 0, TimeSpan.Zero);

    private static readonly SupervisorSubject Question = SupervisorSubject.Question("s-1a2b3c4d", "q-7");

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-attempts", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    [Fact]
    public void An_attempt_is_written_before_its_start_and_its_outcome_at_its_end_one_per_choice()
    {
        var attempts = new SupervisorAttempts(_home);

        var first = attempts.Begin(Question, choice: 1, Now)!;
        Assert.Equal([1], attempts.Of(Question).Select(each => each.Choice));
        Assert.Null(attempts.Of(Question).Single().Outcome);
        Assert.True(attempts.Running(Question));
        Assert.Null(attempts.Begin(Question, choice: 2, Now));

        attempts.End(first, SupervisorAttemptOutcomes.Limited, Now.AddMinutes(1));
        Assert.False(attempts.Running(Question));
        Assert.False(attempts.Ended(Question));
        Assert.Null(attempts.Begin(Question, choice: 1, Now));

        var second = attempts.Begin(Question, choice: 2, Now.AddMinutes(2))!;
        attempts.End(second, SupervisorAttemptOutcomes.Failed, Now.AddMinutes(3));
        Assert.True(attempts.Ended(Question));
        Assert.Equal([1, 2], attempts.Spent(Question));
    }

    [Fact]
    public void An_attempt_with_no_outcome_is_closed_failed_at_a_start_and_its_subject_is_the_person_s()
    {
        new SupervisorAttempts(_home).Begin(Question, choice: 1, Now);

        var restarted = new SupervisorAttempts(_home);
        Assert.Equal([Question], restarted.CloseUnended(Now.AddMinutes(1)));

        Assert.Equal(SupervisorAttemptOutcomes.Failed, restarted.Of(Question).Single().Outcome);
        Assert.True(restarted.Ended(Question));
        Assert.Empty(restarted.CloseUnended(Now.AddMinutes(2)));
    }

    [Fact]
    public void The_start_s_sweep_closes_an_interrupted_attempt_and_keeps_every_subject_whose_attempts_ended_the_person_s()
    {
        var before = new QuestionSupervisorDecisions(_home, new NoAdmission());
        before.Attempts.Begin(Question, choice: 1, Now);
        var point = SupervisorSubject.Point("s-1a2b3c4d", "p-3");
        before.Attempts.End(before.Attempts.Begin(point, choice: 1, Now)!, SupervisorAttemptOutcomes.Failed, Now);

        var after = new QuestionSupervisorDecisions(_home, new NoAdmission());
        after.SweepAtStart(Now.AddMinutes(1));

        Assert.True(after.Closed(Question));
        Assert.True(after.Closed(point));
        Assert.Null(after.Attempts.Begin(Question, choice: 2, Now.AddMinutes(2)));
    }

    private sealed class NoAdmission : IDecisionAdmission
    {
        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) => DecisionAdmitted.Refused(SupervisorWhy.NotStaffed);
    }

    [Fact]
    public void One_past_its_bound_is_closed_failed_and_one_within_it_runs_on()
    {
        var attempts = new SupervisorAttempts(_home);
        attempts.Begin(Question, choice: 1, Now);

        Assert.Empty(attempts.CloseOverdue(Now.AddMinutes(10), TimeSpan.FromMinutes(10)));
        Assert.Equal([Question], attempts.CloseOverdue(Now.AddMinutes(11), TimeSpan.FromMinutes(10)));
        Assert.True(attempts.Ended(Question));
    }

    [Fact]
    public void A_decided_or_left_outcome_ends_the_subject_s_attempts_and_a_file_that_does_not_read_is_none()
    {
        var attempts = new SupervisorAttempts(_home);
        var point = SupervisorSubject.Point("s-1a2b3c4d", "p-3");
        attempts.End(attempts.Begin(point, choice: 1, Now)!, SupervisorAttemptOutcomes.Left, Now);
        Assert.True(attempts.Ended(point));

        File.WriteAllText(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions, "s-1a2b3c4d.decisions"), "a-broken.attempt"), "{ not json");
        Assert.Single(attempts.Of(point));
        Assert.Empty(attempts.Of(Question));
    }
}
