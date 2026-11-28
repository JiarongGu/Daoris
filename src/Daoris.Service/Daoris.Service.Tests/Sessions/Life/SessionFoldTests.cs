using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The fold as a table: a session's last fact and the leases proved alive for it, to the state, the holder, since when, and the
/// invariant's breach by its code; and the vocabulary it is spelled in, read back by its one reader.
/// </summary>
public sealed class SessionFoldTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-11-28T08:00:00Z");
    private static readonly DateTimeOffset Now = T0.AddHours(3);

    private static SessionFact Fact(SessionFactKind kind, int minutes, SessionHolder? holder = null, SessionOutcome? outcome = null) =>
        new("s1", kind, new FactWitness(WitnessDoor.Advance), 2, T0.AddMinutes(minutes)) { Holder = holder ?? SessionHolder.None, Outcome = outcome };

    private static readonly SessionHolder Run = new(HolderKind.Step, "r1");
    private static readonly SessionHolder Check = new(HolderKind.CheckSlot);

    // A process whose driver runs, of this start or of an earlier one (a terminal's driver beside the holder), and one whose driver went.
    private static readonly SessionLease Live = new("s1", 2, OwnerRuns: true);
    private static readonly SessionLease Beside = new("s1", 1, OwnerRuns: true);
    private static readonly SessionLease Survived = new("s1", 1, OwnerRuns: false);

    /// <summary>Each row: its name, the last fact, the leases, then the state, the holder, the minute it is since, and the breach.</summary>
    public static TheoryData<string, SessionFact, SessionLease[], FoldedState, SessionHolder?, int, BreachCode?> Rows => new()
    {
        { "opened, no process: waits on the machine room", Fact(SessionFactKind.Opened, 0, SessionHolder.MachineRoom), [], FoldedState.Waiting, SessionHolder.MachineRoom, 0, null },
        { "starting with no holder named: still the machine room's", Fact(SessionFactKind.Starting, 1), [], FoldedState.Waiting, SessionHolder.MachineRoom, 1, null },
        { "starting with its process alive: working", Fact(SessionFactKind.Starting, 1, SessionHolder.MachineRoom), [Live], FoldedState.Working, null, 1, null },
        { "a turn with a live lease: working", Fact(SessionFactKind.TurnBegan, 2), [Live], FoldedState.Working, null, 2, null },
        { "a turn whose driver runs beside the holder: working", Fact(SessionFactKind.TurnBegan, 2), [Beside], FoldedState.Working, null, 2, null },
        { "a turn with no process: needs you", Fact(SessionFactKind.TurnBegan, 2), [], FoldedState.NeedsYou, null, 2, BreachCode.TurnWithoutLease },
        { "a turn whose process outlived its driver: needs you", Fact(SessionFactKind.TurnBegan, 2), [Survived], FoldedState.NeedsYou, null, 2, BreachCode.TurnOnVoidLease },
        { "a turn with several void leases at once: needs you", Fact(SessionFactKind.TurnBegan, 2), [Survived, Survived with { Generation = 0 }], FoldedState.NeedsYou, null, 2, BreachCode.TurnOnVoidLease },
        { "a question open: needs you, the person holds it", Fact(SessionFactKind.QuestionOpened, 9, SessionHolder.Person), [], FoldedState.NeedsYou, SessionHolder.Person, 9, null },
        { "a question for the steward: the steward holds it", Fact(SessionFactKind.QuestionOpened, 9, new(HolderKind.Steward)), [Survived], FoldedState.NeedsYou, new SessionHolder(HolderKind.Steward), 9, null },
        { "a question open beside a live process: a breach", Fact(SessionFactKind.QuestionOpened, 9, SessionHolder.Person), [Live], FoldedState.NeedsYou, SessionHolder.Person, 9, BreachCode.QuestionWithLease },
        { "awaiting its run's step: waiting on the step", Fact(SessionFactKind.StepAwaited, 30, Run), [], FoldedState.Waiting, Run, 30, null },
        { "awaiting its own check: waiting on a check slot", Fact(SessionFactKind.StepAwaited, 30, Check), [], FoldedState.Waiting, Check, 30, null },
        { "awaiting a step that names nothing: needs you", Fact(SessionFactKind.StepAwaited, 30), [], FoldedState.NeedsYou, null, 30, BreachCode.WaitWithoutHolder },
        { "awaiting a step beside a live process: a breach", Fact(SessionFactKind.StepAwaited, 30, Run), [Live], FoldedState.NeedsYou, Run, 30, BreachCode.WaitWithLease },
        { "ended: ended whatever runs", Fact(SessionFactKind.Ended, 40, outcome: SessionOutcome.Failed), [Live], FoldedState.Ended, null, 40, null },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void A_last_fact_folds_to_one_state_its_holder_since_and_breach(
        string name, SessionFact last, SessionLease[] leases, FoldedState state, SessionHolder? holder, int since, BreachCode? breach)
    {
        var folded = SessionFold.Of(last, Now, leases);

        Assert.NotNull(folded);
        Assert.True(state == folded.State, $"{name}: {folded.State}");
        Assert.Equal(holder, folded.Holder);
        Assert.Equal(T0.AddMinutes(since), folded.Since);
        Assert.True(breach == folded.Breach, $"{name}: {folded.Breach}");
        Assert.True(folded.Breach is null || folded.State == FoldedState.NeedsYou, $"{name}: a breach is never {folded.State}");
    }

    [Fact]
    public void No_fact_witnessed_nothing_and_folds_to_none()
    {
        Assert.Null(SessionFold.Of((SessionFact?)null, Now, [Live]));
        Assert.Null(SessionFold.Of([], Now, [Live]));
    }

    [Fact]
    public void An_end_carries_its_outcome_and_a_fact_from_a_clock_ahead_is_since_now()
    {
        Assert.Equal(SessionOutcome.Stopped, SessionFold.Of(Fact(SessionFactKind.Ended, 5, outcome: SessionOutcome.Stopped), Now, [])!.Outcome);
        Assert.Equal(Now, SessionFold.Of(Fact(SessionFactKind.Opened, 600, SessionHolder.MachineRoom), Now, [])!.Since);
    }

    [Fact]
    public void A_stored_start_whose_process_runs_is_no_difference_but_a_stored_turn_with_none_is()
    {
        var started = SessionFold.Of(Fact(SessionFactKind.Starting, 1, SessionHolder.MachineRoom), Now, [Live])!;
        var unleased = SessionFold.Of(Fact(SessionFactKind.TurnBegan, 2), Now, [])!;

        Assert.False(SessionFold.Differs(started, SessionState.Starting));
        Assert.True(SessionFold.Differs(started, SessionState.Queued));
        Assert.True(SessionFold.Differs(unleased, SessionState.Working));
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
    public void The_vocabulary_is_spelled_once_and_read_back_by_its_one_reader_which_knows_no_other_spelling()
    {
        AssertRoundTrip<SessionFactKind>();
        AssertRoundTrip<SessionOutcome>();
        AssertRoundTrip<HolderKind>();
        AssertRoundTrip<WitnessDoor>();
        AssertRoundTrip<BreachCode>();
        AssertRoundTrip<FoldedState>();
        AssertRoundTrip<TimeBucket>();
        Assert.Equal(("turn-began", "machine-room", "needs-you", "turn-on-void-lease"), (
            Vocabulary<SessionFactKind>.Spell(SessionFactKind.TurnBegan), Vocabulary<HolderKind>.Spell(HolderKind.MachineRoom),
            Vocabulary<FoldedState>.Spell(FoldedState.NeedsYou), Vocabulary<BreachCode>.Spell(BreachCode.TurnOnVoidLease)));
        Assert.Null(Vocabulary<SessionFactKind>.Read("TurnBegan"));
        Assert.Null(Vocabulary<SessionFactKind>.Read("a-later-kind"));
    }

    private static void AssertRoundTrip<T>() where T : struct, Enum
    {
        var spellings = Enum.GetValues<T>().Select(Vocabulary<T>.Spell).ToList();
        Assert.Equal(spellings.Count, spellings.Distinct(StringComparer.Ordinal).Count());
        Assert.All(Enum.GetValues<T>(), value => Assert.Equal(value, Vocabulary<T>.Read(Vocabulary<T>.Spell(value))));
    }
}
