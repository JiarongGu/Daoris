using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The fold as a table: a journal, the leases proved alive and the generation they are read in, to the state, the holder, since
/// when, and whether the invariant is breached.
/// </summary>
public sealed class SessionFoldTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-11-28T08:00:00Z");
    private static readonly DateTimeOffset Now = T0.AddHours(3);

    private static SessionFact Fact(SessionFactKind kind, int minutes, string? holder = null, SessionOutcome? outcome = null) =>
        new("s1", kind, "test", 2, T0.AddMinutes(minutes)) { Holder = holder, Outcome = outcome };

    private static readonly SessionLease Live = new("s1", 2);
    private static readonly SessionLease Survived = new("s1", 1);

    /// <summary>Each row: its name, the journal, the leases, then the state, the holder, the minute it is since, and whether it is a breach.</summary>
    public static TheoryData<string, SessionFact[], SessionLease[], FoldedState, string?, int, bool> Rows => new()
    {
        { "opened, no process: waits on its start", [Fact(SessionFactKind.Opened, 0, "start")], [], FoldedState.Waiting, "start", 0, false },
        { "starting with its process alive: working", [Fact(SessionFactKind.Opened, 0, "start"), Fact(SessionFactKind.Starting, 1, "start")], [Live], FoldedState.Working, "lease", 1, false },
        { "a turn with a live lease: working", [Fact(SessionFactKind.Opened, 0, "start"), Fact(SessionFactKind.TurnBegan, 2)], [Live], FoldedState.Working, "lease", 2, false },
        { "a turn with no process: needs you", [Fact(SessionFactKind.TurnBegan, 2)], [], FoldedState.NeedsYou, null, 2, true },
        { "a turn whose process survived a restart: needs you", [Fact(SessionFactKind.TurnBegan, 2)], [Survived], FoldedState.NeedsYou, null, 2, true },
        { "a question open: needs you, the person holds it", [Fact(SessionFactKind.TurnBegan, 2), Fact(SessionFactKind.QuestionOpened, 9)], [], FoldedState.NeedsYou, "person", 9, false },
        { "a question open beside a live process: a breach", [Fact(SessionFactKind.QuestionOpened, 9)], [Live], FoldedState.NeedsYou, "person", 9, true },
        { "awaiting its run's step: waiting on the run", [Fact(SessionFactKind.TurnBegan, 2), Fact(SessionFactKind.StepAwaited, 30, "run:r1")], [], FoldedState.Waiting, "run:r1", 30, false },
        { "awaiting its own check: waiting on the check", [Fact(SessionFactKind.StepAwaited, 30, "check")], [], FoldedState.Waiting, "check", 30, false },
        { "awaiting a step that names nothing: needs you", [Fact(SessionFactKind.TurnBegan, 2), Fact(SessionFactKind.StepAwaited, 30)], [], FoldedState.NeedsYou, null, 30, true },
        { "awaiting a step beside a live process: a breach", [Fact(SessionFactKind.StepAwaited, 30, "run:r1")], [Live], FoldedState.NeedsYou, "run:r1", 30, true },
        { "ended: ended whatever runs", [Fact(SessionFactKind.TurnBegan, 2), Fact(SessionFactKind.Ended, 40, outcome: SessionOutcome.Failed)], [Live], FoldedState.Ended, null, 40, false },
        { "ended, then a turn again: working", [Fact(SessionFactKind.Ended, 40, outcome: SessionOutcome.Failed), Fact(SessionFactKind.TurnBegan, 50)], [Live], FoldedState.Working, "lease", 50, false },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void A_journal_folds_to_one_state_its_holder_and_since(
        string name, SessionFact[] journal, SessionLease[] leases, FoldedState state, string? holder, int since, bool breach)
    {
        var folded = SessionFold.Of(journal, Now, leases, generation: 2);

        Assert.NotNull(folded);
        Assert.True(state == folded.State, $"{name}: {folded.State}");
        Assert.Equal(holder, folded.Holder);
        Assert.Equal(T0.AddMinutes(since), folded.Since);
        Assert.True(breach == folded.Breach is not null, $"{name}: {folded.Breach}");
        Assert.True(folded.Breach is null || folded.State == FoldedState.NeedsYou, $"{name}: a breach is never {folded.State}");
    }

    [Fact]
    public void An_empty_journal_witnessed_nothing_and_folds_to_none() =>
        Assert.Null(SessionFold.Of([], Now, [Live], generation: 2));

    [Fact]
    public void An_end_carries_its_outcome_and_a_lease_of_an_earlier_generation_is_named_void()
    {
        var ended = SessionFold.Of([Fact(SessionFactKind.Ended, 5, outcome: SessionOutcome.Stopped)], Now, [], 2)!;
        var survived = SessionFold.Of([Fact(SessionFactKind.TurnBegan, 5)], Now, [Survived], 2)!;

        Assert.Equal(SessionOutcome.Stopped, ended.Outcome);
        Assert.Contains("generation 1", survived.Breach);
    }

    [Fact]
    public void A_fact_written_on_a_clock_ahead_of_the_reader_is_since_now()
    {
        var ahead = SessionFold.Of([Fact(SessionFactKind.Opened, 600, "start")], Now, [], 2)!;

        Assert.Equal(Now, ahead.Since);
    }

    /// <summary>The stored status a fold is compared with while it runs in shadow, each of the ten.</summary>
    [Theory]
    [InlineData(SessionState.Queued, FoldedState.Waiting, null)]
    [InlineData(SessionState.Starting, FoldedState.Waiting, null)]
    [InlineData(SessionState.Working, FoldedState.Working, null)]
    [InlineData(SessionState.AwaitingPerson, FoldedState.NeedsYou, null)]
    [InlineData(SessionState.AwaitingStep, FoldedState.Waiting, null)]
    [InlineData(SessionState.Completed, FoldedState.Ended, SessionOutcome.Done)]
    [InlineData(SessionState.Declined, FoldedState.Ended, SessionOutcome.Declined)]
    [InlineData(SessionState.StoodDown, FoldedState.Ended, SessionOutcome.StoodDown)]
    [InlineData(SessionState.Failed, FoldedState.Ended, SessionOutcome.Failed)]
    [InlineData(SessionState.Stopped, FoldedState.Ended, SessionOutcome.Stopped)]
    public void Each_stored_state_reads_as_one_of_the_four(SessionState stored, FoldedState state, SessionOutcome? outcome) =>
        Assert.Equal((state, outcome), SessionFold.OfStored(stored));

    [Fact]
    public void Generations_name_a_process_by_the_newest_start_no_later_than_it()
    {
        IReadOnlyList<AppGeneration> all = [new(1, T0), new(2, T0.AddHours(1))];

        Assert.Equal(0, AppGenerations.Of(T0.AddMinutes(-1), all));
        Assert.Equal(1, AppGenerations.Of(T0.AddMinutes(30), all));
        Assert.Equal(2, AppGenerations.Of(T0.AddHours(1), all));
    }
}
