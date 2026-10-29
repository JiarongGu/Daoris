using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-i part 1 (D212; the foundation design §3.3, §13.3's family <i>The course</i>, §13.4, §13.5, §19.1): the move record in the
/// desktop store, read by nothing. A move's one row in <c>moves</c>, its words in transfer in <c>move_words</c>; one standing move per record
/// left by the partial unique index <c>moves_standing</c>, so of two executors deciding a move of one line the second finds the first's row;
/// the claim by the process run that makes it, taken only from a run dead by the lease's own test and then once, its generation raised;
/// every phase and effect written under the claim's check inside its own transaction; an ended move kept as evidence while the record's next
/// move claims a new row; and the record's moves deleted with it.
/// </summary>
/// <remarks>
/// Every case has its own scratch home under <c>_fixtures/</c> and sets no variable of the process's environment. One process is one run to
/// the lease's test, so "another process" here is a second move store under a run of its own, writing through its own connection; a dead run
/// is either the liveness the case hands in or a run row the case plants with an id no process has, the lease's own test then reading it. A
/// really killed executor is part 2's. Nothing here starts a process, a window or an agent.
/// </remarks>
[Collection("agent operation ports")]
public sealed class MoveStoreTests : IDisposable
{
    /// <summary>A process id no process has: Windows hands out multiples of four, from the bottom.</summary>
    private const int NoProcess = 0x7FFFFFFC;

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "move-store", Guid.NewGuid().ToString("N")[..8]);

    public MoveStoreTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Db => DesktopStore.LocationIn(_home);

    /// <summary>A move store of one run: a shell, by default reading a run's liveness by the lease's own test.</summary>
    private MoveStore Store(string run, Func<StoreRun, bool>? alive = null) => new(_home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    /// <summary>A liveness in which the runs named are dead and every other run lives.</summary>
    private static Func<StoreRun, bool> DeadAre(params string[] runs) => run => !runs.Contains(run.Run);

    private static MoveDecision Decision(string left, string executor = MoveExecutors.Conversation) =>
        new(left, executor, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris);

    /// <summary>A statement of the case's own over the store, as another process or another build writes it.</summary>
    private void Plant(string sql, params (string Name, object? Value)[] values)
    {
        using var store = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        store.Write("probe", write => write.Execute(sql, values));
    }

    private long Revision()
    {
        using var reader = DesktopStoreReader.Open(Db);
        return reader?.Revisions().GetValueOrDefault(MoveStore.Family) ?? 0;
    }

    private long Rows(string table)
    {
        using var reader = DesktopStoreReader.Open(Db)!;
        return reader.Query($"SELECT COUNT(*) FROM {table}", row => row.GetInt64(0))[0];
    }

    private static MoveRecord Deliver(MoveStore store, MoveClaim claim)
    {
        store.Advance(claim, MoveRecordPhase.OldEnded);
        store.Opening(claim, "opens-1");
        store.Opened(claim, "s-new", []);
        store.Advance(claim, MoveRecordPhase.Delivering);
        return store.Advance(claim, MoveRecordPhase.Delivered);
    }

    // ——— The decision and its claim (§3.3, §13.4 step 1).

    [Fact]
    public void A_decided_move_is_claimed_by_the_run_that_makes_it_at_generation_1_and_stands_for_its_record_left()
    {
        var shell = Store("run-a");

        var decided = shell.Decide(Decision("s1") with { Account = "work", RecipeWhy = HandOverWhy.Limit, RecipeBound = 12 });

        Assert.True(decided.Claimed);
        var claim = decided.Claim!;
        var move = decided.Move;
        Assert.Equal((move.Id, "run-a", 1L), (claim.Move, claim.Run, claim.Generation));
        Assert.Equal(("s1", MoveExecutors.Conversation, (MoveRecordPhase?)MoveRecordPhase.Decided, "run-a", 1L), (move.Left, move.Executor, move.Phase, move.ClaimRun, move.ClaimGeneration));
        Assert.Equal(
            (MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris, (string?)"work", (string?)HandOverWhy.Limit, (int?)12),
            (move.How, move.Why, move.By, move.Account, move.RecipeWhy, move.RecipeBound));
        Assert.True(move.Stands);
        Assert.Equal([MoveRecordPhase.Decided], move.Moments.Keys);
        Assert.Null(move.Effect);
        Assert.Equal(move.Id, shell.Standing("s1")!.Id);
        using var reader = DesktopStoreReader.Open(Db)!;
        Assert.Equal(["run-a"], reader.Query("SELECT run FROM store_runs", row => row.GetString(0)));
    }

    [Fact]
    public void A_move_from_an_ended_record_which_has_no_part_to_end_is_written_at_old_ended()
    {
        var decided = Store("run-a").Decide(Decision("s1") with { FromEnded = true });

        Assert.Equal(MoveRecordPhase.OldEnded, decided.Move.Phase);
        Assert.Equal(decided.Move.Moments[MoveRecordPhase.Decided], decided.Move.Moments[MoveRecordPhase.OldEnded]);
    }

    [Fact]
    public void A_decision_names_its_record_its_executor_its_how_and_its_why_and_by_or_is_refused()
    {
        var shell = Store("run-a");

        Assert.Throws<ArgumentException>(() => shell.Decide(Decision(" ")));
        Assert.Throws<ArgumentException>(() => shell.Decide(Decision("s1", "helper")));
        Assert.Throws<ArgumentException>(() => shell.Decide(Decision("s1") with { How = "copy" }));
        Assert.Throws<ArgumentException>(() => shell.Decide(Decision("s1") with { Why = "" }));
        Assert.Throws<ArgumentException>(() => shell.Decide(Decision("s1") with { By = " " }));
        Assert.False(File.Exists(Db));
    }

    // ——— One standing move per record left (§3.3, §19.1 point 1; round 6, O5-F1).

    [Fact]
    public async Task Two_runs_on_two_connections_deciding_a_move_of_one_record_at_once_make_one_standing_row_which_the_second_reads()
    {
        var first = Store("run-a");
        var second = Store("run-b");
        // The store made before the race, so each race is the decision's alone.
        first.Decide(Decision("warm"));

        for (var each = 0; each < 25; each++)
        {
            var left = $"s{each}";
            using var start = new Barrier(2);
            var answers = await Task.WhenAll(new[] { first, second }.Select(store => Task.Run(() =>
            {
                start.SignalAndWait();
                return store.Decide(Decision(left));
            })));

            var claimed = Assert.Single(answers, answer => answer.Claimed);
            var stands = Assert.Single(answers, answer => !answer.Claimed);
            Assert.Equal(claimed.Move.Id, stands.Move.Id);
            Assert.Equal(claimed.Claim!.Run, stands.Move.ClaimRun);
            Assert.Null(stands.Claim);
            Assert.Single(first.Of(left));
        }
    }

    [Fact]
    public void The_second_executor_of_a_line_finds_the_standing_row_writes_nothing_and_another_record_moves_on_its_own()
    {
        var first = Store("run-a").Decide(Decision("s1"));
        var revision = Revision();

        var second = Store("run-b").Decide(Decision("s1", MoveExecutors.Driven));

        Assert.False(second.Claimed);
        Assert.Equal((first.Move.Id, MoveExecutors.Conversation, "run-a"), (second.Move.Id, second.Move.Executor, second.Move.ClaimRun));
        Assert.Equal(revision, Revision());
        Assert.Equal(1, Rows("moves"));
        Assert.True(Store("run-b").Decide(Decision("s2")).Claimed);
    }

    [Theory]
    [InlineData("delivered")]
    [InlineData("fell-back")]
    [InlineData("closed")]
    public void A_move_that_ends_frees_its_record_and_the_next_move_claims_a_new_row_the_ended_one_kept_as_evidence(string end)
    {
        var shell = Store("run-a");
        var first = shell.Decide(Decision("s1")).Claim!;

        var ended = end switch
        {
            "delivered" => Deliver(shell, first),
            "fell-back" => shell.FallBack(first, "refused"),
            _ => shell.Close(first, MoveCloseWhy.Superseded),
        };

        Assert.Equal(end, ended.PhaseWord);
        Assert.False(ended.Stands);
        Assert.Null(shell.Standing("s1"));
        var next = Store("run-b").Decide(Decision("s1"));
        Assert.True(next.Claimed);
        Assert.Equal([first.Move, next.Move.Id], shell.Of("s1").Select(move => move.Id));
        Assert.Equal(end, shell.Read(first.Move)!.PhaseWord);
        Assert.Equal(next.Move.Id, shell.Standing("s1")!.Id);
    }

    [Fact]
    public void Every_phase_before_the_move_ends_keeps_its_record_a_hold_among_them_until_its_close_commits_and_the_next_move_then_claims()
    {
        var shell = Store("run-a");
        var claim = shell.Decide(Decision("s1")).Claim!;
        void Kept(MoveRecord move)
        {
            Assert.True(move.Stands, move.PhaseWord);
            Assert.False(Store("run-b").Decide(Decision("s1")).Claimed, move.PhaseWord);
            Assert.Equal(claim.Move, shell.Standing("s1")!.Id);
        }

        Kept(shell.Read(claim.Move)!);
        Kept(shell.Advance(claim, MoveRecordPhase.Quiescing));
        Kept(shell.Advance(claim, MoveRecordPhase.OldEnded));
        Kept(shell.Opening(claim, "opens-1"));
        Kept(shell.Opened(claim, "s2", []));
        Kept(shell.Advance(claim, MoveRecordPhase.Copied));
        Kept(shell.Advance(claim, MoveRecordPhase.Verified));
        Kept(shell.Advance(claim, MoveRecordPhase.Delivering));
        // Held for the person: it stands, and its record takes no move of its own, until it closes resolved by §3.3's one rule.
        Kept(shell.Hold(claim, "superseded"));

        var resolved = shell.Close(claim, MoveCloseWhy.Resolved);

        Assert.Null(shell.Standing("s1"));
        Assert.Equal(("closed", (string?)"superseded", (string?)MoveCloseWhy.Resolved), (resolved.PhaseWord, resolved.HeldWhy, resolved.ClosedWhy));
        Assert.Equal(
            [MoveRecordPhase.Decided, MoveRecordPhase.Quiescing, MoveRecordPhase.OldEnded, MoveRecordPhase.Opening, MoveRecordPhase.Opened, MoveRecordPhase.Copied,
             MoveRecordPhase.Verified, MoveRecordPhase.Delivering, MoveRecordPhase.Held, MoveRecordPhase.Closed],
            resolved.Moments.Keys.Order());
        Assert.True(Store("run-b").Decide(Decision("s1")).Claimed);
        Assert.Equal(2, shell.Of("s1").Count);
    }

    [Fact]
    public void A_phase_a_newer_build_wrote_that_this_one_does_not_know_stands_and_takes_no_phase_of_this_build_s()
    {
        var shell = Store("run-a");
        var claim = shell.Decide(Decision("s1")).Claim!;
        Plant("UPDATE moves SET phase = 'reconciling' WHERE id = $id", ("$id", claim.Move));

        var read = shell.Standing("s1")!;

        Assert.Equal(("reconciling", (MoveRecordPhase?)null), (read.PhaseWord, read.Phase));
        Assert.True(read.Stands);
        Assert.False(Store("run-b").Decide(Decision("s1")).Claimed);
        Assert.Throws<MoveRefusedException>(() => shell.Advance(claim, MoveRecordPhase.OldEnded));
    }

    // ——— The phases, forward only, under the claim (§3.3; §13.4 steps 2 and 3).

    [Fact]
    public void Phases_go_forward_a_phase_written_again_changes_nothing_a_hold_only_closes_and_an_ended_move_takes_no_write()
    {
        var shell = Store("run-a");
        var claim = shell.Decide(Decision("s1")).Claim!;

        Assert.Throws<MoveRefusedException>(() => shell.Advance(claim, MoveRecordPhase.Delivering));
        Assert.Throws<MoveRefusedException>(() => shell.Advance(claim, MoveRecordPhase.Delivered));
        Assert.Throws<MoveRefusedException>(() => shell.Opened(claim, "s2", []));
        Assert.Throws<MoveRefusedException>(() => shell.Opening(claim, "opens-1"));
        var ended = shell.Advance(claim, MoveRecordPhase.OldEnded);
        Assert.Equal(ended.Moments[MoveRecordPhase.OldEnded], shell.Advance(claim, MoveRecordPhase.OldEnded).Moments[MoveRecordPhase.OldEnded]);
        Assert.Throws<MoveRefusedException>(() => shell.Advance(claim, MoveRecordPhase.Quiescing));
        foreach (var named in new[] { MoveRecordPhase.Opening, MoveRecordPhase.Opened, MoveRecordPhase.FellBack, MoveRecordPhase.Held, MoveRecordPhase.Closed })
        {
            Assert.Throws<ArgumentException>(() => shell.Advance(claim, named));
        }

        shell.Hold(claim, "lost-open");
        Assert.Throws<MoveRefusedException>(() => shell.Opening(claim, "opens-1"));
        Assert.Throws<MoveRefusedException>(() => shell.FallBack(claim, "refused"));
        Assert.Throws<ArgumentException>(() => shell.Close(claim, "abandoned"));
        Assert.Equal(MoveCloseWhy.Resolved, shell.Close(claim, MoveCloseWhy.Resolved).ClosedWhy);
        Assert.Equal(MoveCloseWhy.Resolved, shell.Close(claim, MoveCloseWhy.Gone).ClosedWhy);
        Assert.Throws<MoveRefusedException>(() => shell.Hold(claim, "again"));
        Assert.Throws<MoveRefusedException>(() => shell.MarkEffect(claim, MoveEffects.Prompt));
    }

    [Fact]
    public void The_open_s_answer_writes_the_destination_and_every_word_it_moved_in_one_transaction_and_never_a_word_s_text()
    {
        var shell = Store("run-a");
        var claim = shell.Decide(Decision("s1") with { FromEnded = true }).Claim!;
        var opening = shell.Opening(claim, "opens-1");
        Assert.Equal(((MoveRecordPhase?)MoveRecordPhase.Opening, (string?)"opens-1"), (opening.Phase, opening.Opens));

        // A word the store refuses (two words under one transfer) leaves neither the phase nor any word.
        Assert.Throws<SqliteException>(() => shell.Opened(claim, "s2", [new("w1", "k1", "t1", 0), new("w2", null, "t1", 1)]));
        Assert.Equal(MoveRecordPhase.Opening, shell.Read(claim.Move)!.Phase);
        Assert.Empty(shell.WordsOf(claim.Move));

        var opened = shell.Opened(claim, "s2", [new("w2", null, "t2", 1), new("w1", "k1", "t1", 0)]);

        Assert.Equal(((MoveRecordPhase?)MoveRecordPhase.Opened, (string?)"s2"), (opened.Phase, opened.Destination));
        Assert.Equal(
            [("w1", (string?)"k1", "t1", 0, (string?)null), ("w2", null, "t2", 1, null)],
            shell.WordsOf(claim.Move).Select(word => (word.Word, word.Producer, word.Transfer, word.Position, word.Disposition)));
        using var reader = DesktopStoreReader.Open(Db)!;
        Assert.Equal(
            ["move", "transfer", "word", "producer", "position", "disposition", "disposed_at"],
            reader.Query("PRAGMA table_info(move_words)", row => row.GetString(1)));
    }

    [Fact]
    public void A_claim_writes_only_through_the_run_it_names_and_only_at_its_generation()
    {
        var shell = Store("run-a");
        var claim = shell.Decide(Decision("s1")).Claim!;
        var revision = Revision();

        Assert.Throws<MoveClaimLostException>(() => Store("run-b").Advance(claim, MoveRecordPhase.OldEnded));
        Assert.Throws<MoveClaimLostException>(() => shell.Advance(claim with { Generation = 2 }, MoveRecordPhase.OldEnded));
        Assert.Throws<MoveClaimLostException>(() => shell.Advance(claim with { Move = claim.Move + 99 }, MoveRecordPhase.OldEnded));

        Assert.Equal(MoveRecordPhase.Decided, shell.Read(claim.Move)!.Phase);
        Assert.Equal(revision, Revision());
    }

    // ——— The effect mark (§3.3; §13.4 step 2; X2-3.1).

    [Fact]
    public void An_effect_is_marked_before_it_is_made_its_outcome_after_and_a_begun_one_with_no_outcome_takes_no_second_mark()
    {
        var shell = Store("run-a");
        var claim = shell.Decide(Decision("s1")).Claim!;

        var marked = shell.MarkEffect(claim, MoveEffects.Spawn);

        Assert.Equal((MoveEffects.Spawn, (string?)null, (string?)null), (marked.Effect!.What, marked.Effect.Outcome, marked.Effect.Ended));
        Assert.True(DateTimeOffset.TryParse(marked.Effect.Begun, out _));

        // What a taker reads of a run that died between the mark and the outcome: an effect begun and never recorded, which it holds and
        // never makes again by itself, so it marks no second effect over the first.
        var taker = Store("run-b", DeadAre("run-a"));
        var taken = taker.Take(claim.Move, claim.Generation);
        Assert.Equal((MoveEffects.Spawn, (string?)null), (taken.Move!.Effect!.What, taken.Move.Effect.Outcome));
        Assert.Throws<MoveRefusedException>(() => taker.MarkEffect(taken.Claim!, MoveEffects.Prompt));
        Assert.Throws<ArgumentException>(() => taker.EffectEnded(taken.Claim!, " "));

        var ended = taker.EffectEnded(taken.Claim!, "unknown");

        Assert.Equal("unknown", ended.Effect!.Outcome);
        Assert.True(DateTimeOffset.TryParse(ended.Effect.Ended, out _));
        Assert.Throws<MoveRefusedException>(() => taker.EffectEnded(taken.Claim!, "unknown"));
        Assert.Throws<ArgumentException>(() => taker.MarkEffect(taken.Claim!, "fetch"));
        Assert.Equal(MoveEffects.Prompt, taker.MarkEffect(taken.Claim!, MoveEffects.Prompt).Effect!.What);
    }

    // ——— The claim by process run, passed only from a dead one (§13.4; §19.1; O3-1, X3-1.1, X3-3.1).

    [Fact]
    public void A_dead_run_s_move_is_taken_once_under_the_taker_s_run_and_a_raised_generation_and_the_old_claim_writes_nothing()
    {
        var dead = Store("run-a");
        var claim = dead.Decide(Decision("s1")).Claim!;
        dead.Advance(claim, MoveRecordPhase.OldEnded);
        var taker = Store("run-b", DeadAre("run-a"));
        var other = Store("run-c", DeadAre("run-a"));

        var taken = taker.Take(claim.Move, claim.Generation);

        Assert.Null(taken.Refused);
        Assert.Equal((claim.Move, "run-b", 2L), (taken.Claim!.Move, taken.Claim.Run, taken.Claim.Generation));
        Assert.Equal(("run-b", 2L, (MoveRecordPhase?)MoveRecordPhase.OldEnded), (taken.Move!.ClaimRun, taken.Move.ClaimGeneration, taken.Move.Phase));

        // A second taker that read the move before the take is refused, the move having moved, and nothing is written.
        var revision = Revision();
        Assert.Equal(MoveTakeRefusals.Moved, other.Take(claim.Move, claim.Generation).Refused);
        Assert.Equal(revision, Revision());

        // The dead run's claim, were it to write again, changes nothing: every write checks the claim inside its own transaction.
        Assert.Throws<MoveClaimLostException>(() => dead.Opening(claim, "opens-1"));
        Assert.Throws<MoveClaimLostException>(() => dead.MarkEffect(claim, MoveEffects.Spawn));
        Assert.Equal(revision, Revision());
        Assert.Equal(MoveRecordPhase.Opening, taker.Opening(taken.Claim, "opens-1").Phase);

        // Once the taker's run is dead too, the next take raises the generation again; an ended move is taken by no one.
        var again = Store("run-c", DeadAre("run-a", "run-b")).Take(claim.Move, 2);
        Assert.Equal(("run-c", 3L), (again.Claim!.Run, again.Claim.Generation));
        Store("run-c").Close(again.Claim, MoveCloseWhy.Superseded);
        Assert.Equal(MoveTakeRefusals.Ended, Store("run-d", _ => false).Take(claim.Move, 3).Refused);
        Assert.Equal(MoveTakeRefusals.Gone, Store("run-d", _ => false).Take(claim.Move + 99, 1).Refused);
    }

    [Fact]
    public void A_move_is_never_taken_from_a_live_run_however_long_it_pauses_after_its_mark_nor_from_its_own_run()
    {
        var shell = Store("run-a");
        var claim = shell.Decide(Decision("s1")).Claim!;
        shell.MarkEffect(claim, MoveEffects.Spawn);
        // Paused a day after its mark and before its effect: no moment counts, only whether its process lives.
        Plant("UPDATE moves SET decided_at = $at, effect_begun = $at WHERE id = $id", ("$at", "2026-10-28T00:00:00.0000000+00:00"), ("$id", claim.Move));

        // Both runs are this one live process to the lease's own test.
        Assert.Equal(MoveTakeRefusals.Live, Store("run-b").Take(claim.Move, claim.Generation).Refused);
        // A liveness that calls every run dead takes nothing from the run's own.
        Assert.Equal(MoveTakeRefusals.Own, Store("run-a", _ => false).Take(claim.Move, claim.Generation).Refused);
        var read = shell.Read(claim.Move)!;
        Assert.Equal(("run-a", 1L), (read.ClaimRun, read.ClaimGeneration));

        // The effect, made after however long a pause, is the claimer's alone, and its outcome lands under its claim.
        Assert.Equal("started", shell.EffectEnded(claim, "started").Effect!.Outcome);
    }

    [Fact]
    public void By_the_lease_s_own_test_a_run_whose_process_is_gone_or_another_is_taken_and_one_whose_start_cannot_be_read_never_is()
    {
        using var self = Process.GetCurrentProcess();
        var started = self.StartTime.ToUniversalTime().Ticks;
        var taker = Store("run-taker");
        long Planted(string run, long pid, long? start)
        {
            var move = Store(run).Decide(Decision("s-" + run)).Move.Id;
            Plant("UPDATE store_runs SET pid = $pid, started = $started WHERE run = $run", ("$pid", pid), ("$started", start), ("$run", run));
            return move;
        }

        var gone = Planted("run-gone", NoProcess, started);
        var another = Planted("run-another", Environment.ProcessId, started + TimeSpan.FromHours(1).Ticks);
        var unread = Planted("run-unread", NoProcess, null);
        var idle = Planted("run-idle", 0, started);
        var live = Planted("run-live", Environment.ProcessId, started);

        Assert.Null(taker.Take(gone, 1).Refused);
        Assert.Null(taker.Take(another, 1).Refused);
        // A start the run could not read when it began, and a process whose start no caller may read: the lease's "cannot tell" holds.
        Assert.Equal(MoveTakeRefusals.Live, taker.Take(unread, 1).Refused);
        if (OperatingSystem.IsWindows()) Assert.Equal(MoveTakeRefusals.Live, taker.Take(idle, 1).Refused);
        Assert.Equal(MoveTakeRefusals.Live, taker.Take(live, 1).Refused);
        Assert.Equal(["run-taker", "run-taker"], new[] { gone, another }.Select(move => taker.Read(move)!.ClaimRun));
    }

    [Fact]
    public void Two_shells_on_one_home_each_keep_their_own_conversations_moves()
    {
        var one = Store("shell-1");
        var two = Store("shell-2");
        var first = one.Decide(Decision("s1")).Claim!;
        var second = two.Decide(Decision("s2")).Claim!;

        Assert.Equal(MoveTakeRefusals.Live, two.Take(first.Move, first.Generation).Refused);
        Assert.Equal(MoveTakeRefusals.Live, one.Take(second.Move, second.Generation).Refused);
        Assert.Equal(MoveRecordPhase.OldEnded, one.Advance(first, MoveRecordPhase.OldEnded).Phase);
        Assert.Equal(MoveRecordPhase.OldEnded, two.Advance(second, MoveRecordPhase.OldEnded).Phase);
    }

    // ——— The finds and the process's one run.

    [Fact]
    public void The_one_standing_find_reads_without_making_a_store_and_a_store_from_before_the_step_holds_none()
    {
        Assert.Null(Store("run-a").Standing("s1"));
        Assert.Empty(Store("run-a").Of("s1"));
        Assert.Null(Store("run-a").Read(1));
        Assert.False(File.Exists(Db));

        using (DesktopStore.Open(Db, new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(3)], Settled = () => StoreSettled.Yes }))
        {
        }

        Assert.Null(Store("run-a").Standing("s1"));
        Assert.Empty(Store("run-a").WordsOf(1));
    }

    [Fact]
    public void The_move_store_and_the_bindings_write_under_the_process_s_one_run()
    {
        new HarnessConversations(_home, () => StoreSettled.Yes).Pick("s1", "work");
        var moves = new MoveStore(_home, () => StoreSettled.Yes);

        var decided = moves.Decide(Decision("s1"));

        Assert.Equal((StoreProcess.Run, StoreProcess.Run), (moves.Run, decided.Claim!.Run));
        using var store = DesktopStore.Open(Db, new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        Assert.Equal([(StoreProcess.Run, StoreProcess.Kind)], store.Runs().Select(run => (run.Run, run.Kind)));
    }

    // ——— Deleted with its record (§3.3; §13.6), through the workplace's port.

    [Fact]
    public void A_deleted_record_takes_its_moves_and_their_words_named_as_the_boxes_they_replace_and_another_record_keeps_its_own()
    {
        var shell = Store("run-a");
        var first = shell.Decide(Decision("s1")).Claim!;
        shell.Close(first, MoveCloseWhy.Superseded);
        var second = shell.Decide(Decision("s1") with { FromEnded = true }).Claim!;
        shell.Opening(second, "opens-1");
        shell.Opened(second, "s3", [new("w1", "k1", "t1", 0)]);
        var kept = shell.Decide(Decision("s2")).Claim!;

        var removed = new SessionHomeFiles(_home).Remove(["s1"], events: null);

        Assert.Equal([SessionHomeFiles.HandOver], removed.Went["s1"]);
        Assert.Empty(removed.Failed);
        Assert.Empty(shell.Of("s1"));
        Assert.Empty(shell.WordsOf(second.Move));
        Assert.Equal(kept.Move, shell.Standing("s2")!.Id);
        // An id is never handed out again, so no claim on a deleted move ever names a later one.
        Assert.True(shell.Decide(Decision("s1")).Move.Id > kept.Move);
        Assert.Throws<MoveClaimLostException>(() => shell.Advance(second, MoveRecordPhase.Delivering));
    }

    [Fact]
    public void A_delete_on_a_home_with_no_store_makes_none_and_names_nothing()
    {
        var removed = new SessionHomeFiles(_home).Remove(["s1"], events: null);

        Assert.Empty(removed.Went["s1"]);
        Assert.False(File.Exists(Db));
    }

    [Fact]
    public void A_delete_the_store_will_not_take_leaves_the_moves_named_among_what_stayed_and_the_store_s_path_failed_once()
    {
        Store("run-a").Decide(Decision("s1"));
        // The record's binding lives in the same store, so its delete is refused at the same path.
        new HarnessConversations(_home, () => StoreSettled.Yes).Pick("s1", "work");
        // A newer Daoris raised the stamp past this build's.
        using (DesktopStore.Open(Db, new DesktopStoreOptions
               {
                   Writer = 2, Steps = [.. DesktopStore.Steps, new(DesktopStore.Steps.Count + 1, 2, write => write.Execute("CREATE TABLE raised (x TEXT NOT NULL)"))],
                   Settled = () => StoreSettled.Yes,
               }))
        {
        }

        var removed = new SessionHomeFiles(_home).Remove(["s1", "s2"], events: null);

        Assert.Equal([Db], removed.Failed);
        Assert.Equal([SessionHomeFiles.Harness, SessionHomeFiles.HandOver], removed.Stayed["s1"]);
        Assert.Empty(removed.Stayed["s2"]);
        Assert.Single(Store("run-a").Of("s1"));
    }

    // ——— Across a downgrade (§13.5's table).

    [Fact]
    public void Across_a_downgrade_an_older_build_writes_the_store_as_it_did_finds_no_box_and_the_newer_build_back_reads_its_move_as_it_left_it()
    {
        var shell = Store("run-a");
        var claim = shell.Decide(Decision("s1")).Claim!;
        shell.Advance(claim, MoveRecordPhase.OldEnded);
        var before = shell.Opening(claim, "opens-1");

        // The older build: the store's own code with the three steps it had, and its own statements, the delete of its record among them.
        using (var older = DesktopStore.Open(Db, new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(3)], Settled = () => StoreSettled.Yes }))
        {
            Assert.False(older.Newer);
            older.Write(BindingStore.Family, write => write.Execute(
                "INSERT INTO bindings (session, adapter, conversation, kept) VALUES ('s1', 'claude-code-acp', 'c-1', '2026-10-29T00:00:00.0000000Z')"));
            older.Write(BindingStore.Family, write => write.Execute("DELETE FROM bindings WHERE session = 's1'"));
        }

        // A move the store carries writes no box an older build could execute: it recovers the move from the ledger alone.
        var sessions = DaorisHome.Entry(_home, HomeEntry.Sessions);
        Assert.False(Directory.Exists(sessions) && Directory.EnumerateFileSystemEntries(sessions).Any());

        // The newer build back reads its move as it left it and goes on from its phase under its own claim.
        var after = shell.Standing("s1")!;
        Assert.Equal(
            (before.Id, before.PhaseWord, before.ClaimRun, before.ClaimGeneration, before.Opens),
            (after.Id, after.PhaseWord, after.ClaimRun, after.ClaimGeneration, after.Opens));
        Assert.Equal(before.Moments, after.Moments);
        Assert.Equal(MoveRecordPhase.Opened, shell.Opened(claim, "s2", []).Phase);

        // The record the older build deleted kept its moves, which knew none; the newer build's delete of the id takes them.
        Assert.Equal([SessionHomeFiles.HandOver], new SessionHomeFiles(_home).Remove(["s1"], events: null).Went["s1"]);
        Assert.Empty(shell.Of("s1"));
    }

    // ——— Part 3: the moved words' `sent`, a hold's words, the destination's standing move and the person's press (§3.3, §13.4 step 4, §19.4).

    /// <summary>A move of <paramref name="left"/> opened onto <paramref name="destination"/> with two words, at <c>delivering</c>.</summary>
    private static MoveClaim Delivering(MoveStore store, string left = "s1", string destination = "s2")
    {
        var claim = store.Decide(Decision(left) with { FromEnded = true }).Claim!;
        store.Opening(claim, $"opens-{left}");
        store.Opened(claim, destination, [new("w1", "k1", "t1", 0), new("w2", null, "t2", 1)]);
        store.Advance(claim, MoveRecordPhase.Delivering);
        return claim;
    }

    private static Dictionary<string, string> Named(string transfer, string disposition) => new(StringComparer.Ordinal) { [transfer] = disposition };

    [Fact]
    public void Sent_is_written_at_delivering_under_the_claim_for_words_the_move_moved_with_its_moment_once_and_never_over_another_disposition()
    {
        var shell = Store("run-a");
        var claim = shell.Decide(Decision("s1") with { FromEnded = true }).Claim!;
        shell.Opening(claim, "opens-1");
        shell.Opened(claim, "s2", [new("w1", "k1", "t1", 0), new("w2", null, "t2", 1)]);

        // Before `delivering` nothing has gone across.
        Assert.Throws<MoveRefusedException>(() => shell.Sent(claim, ["t1"]));
        shell.Advance(claim, MoveRecordPhase.Delivering);
        var revision = Revision();

        // A transfer the move did not make, a claim that is not the writer's, or one whose generation moved writes nothing.
        Assert.Throws<MoveRefusedException>(() => shell.Sent(claim, ["t1", "t9"]));
        Assert.Throws<MoveClaimLostException>(() => Store("run-b").Sent(claim, ["t1"]));
        Assert.Throws<MoveClaimLostException>(() => shell.Sent(claim with { Generation = 2 }, ["t1"]));
        Assert.Equal(revision, Revision());
        Assert.All(shell.WordsOf(claim.Move), word => Assert.Null(word.Disposition));

        shell.Sent(claim, ["t1"]);
        var first = shell.WordsOf(claim.Move)[0];
        shell.Sent(claim, ["t1", "t2"]);

        var words = shell.WordsOf(claim.Move);
        Assert.Equal([MoveWordDispositions.Sent, MoveWordDispositions.Sent], words.Select(word => word.Disposition));
        Assert.NotNull(first.DisposedAt);
        Assert.Equal(first.DisposedAt, words[0].DisposedAt);

        // A word another write disposed otherwise is never written `sent` over.
        var other = Delivering(shell, "s3", "s4");
        Plant("UPDATE move_words SET disposition = 'left' WHERE move = $move AND transfer = 't2'", ("$move", other.Move));
        Assert.Throws<MoveRefusedException>(() => shell.Sent(other, ["t1", "t2"]));
        Assert.All(shell.WordsOf(other.Move), word => Assert.NotEqual(MoveWordDispositions.Sent, word.Disposition));
    }

    [Fact]
    public void Delivered_is_written_only_once_every_word_the_move_moved_has_sent()
    {
        var shell = Store("run-a");
        var claim = Delivering(shell);
        shell.Sent(claim, ["t1"]);

        Assert.Throws<MoveRefusedException>(() => shell.Advance(claim, MoveRecordPhase.Delivered));
        Assert.Equal(MoveRecordPhase.Delivering, shell.Read(claim.Move)!.Phase);

        shell.Sent(claim, ["t2"]);
        Assert.Equal(MoveRecordPhase.Delivered, shell.Advance(claim, MoveRecordPhase.Delivered).Phase);
    }

    [Fact]
    public void A_hold_names_its_words_by_their_disposition_in_its_own_transaction_never_a_word_sent_and_held_again_keeps_the_first()
    {
        var shell = Store("run-a");
        var claim = Delivering(shell);
        shell.Sent(claim, ["t1"]);

        // A word sent is never named, a transfer the move did not make is refused, and a disposition no hold names is no hold's: none holds.
        Assert.Throws<MoveRefusedException>(() => shell.Hold(claim, MoveHoldWhy.Undelivered, Named("t1", MoveWordDispositions.Left)));
        Assert.Throws<MoveRefusedException>(() => shell.Hold(claim, MoveHoldWhy.Undelivered, Named("t9", MoveWordDispositions.Left)));
        Assert.Throws<ArgumentException>(() => shell.Hold(claim, MoveHoldWhy.Undelivered, Named("t2", MoveWordDispositions.Sent)));
        Assert.Equal(MoveRecordPhase.Delivering, shell.Read(claim.Move)!.Phase);

        var held = shell.Hold(claim, MoveHoldWhy.Undelivered, Named("t2", MoveWordDispositions.Uncertain));

        Assert.Equal(((MoveRecordPhase?)MoveRecordPhase.Held, (string?)MoveHoldWhy.Undelivered), (held.Phase, held.HeldWhy));
        Assert.Equal([MoveWordDispositions.Sent, MoveWordDispositions.Uncertain], shell.WordsOf(claim.Move).Select(word => word.Disposition));
        shell.Hold(claim, MoveHoldWhy.Undelivered, Named("t2", MoveWordDispositions.Left));
        Assert.Equal(MoveWordDispositions.Uncertain, shell.WordsOf(claim.Move)[1].Disposition);
    }

    [Fact]
    public void The_destination_s_standing_move_is_found_by_its_destination_and_the_destination_takes_no_move_of_its_own_until_it_ends()
    {
        var shell = Store("run-a");
        var claim = Delivering(shell);
        Assert.Equal(claim.Move, shell.StandingTo("s2")!.Id);
        Assert.Null(shell.StandingTo("s1"));
        var revision = Revision();

        // The course's `Never` for the destination, under the write lock as the index's for the record left: answered, nothing written.
        var refused = Store("run-b").Decide(Decision("s2"));

        Assert.False(refused.Claimed);
        Assert.Equal(claim.Move, refused.Move.Id);
        Assert.Equal(revision, Revision());
        shell.Hold(claim, MoveHoldWhy.Undelivered);
        Assert.False(Store("run-b").Decide(Decision("s2")).Claimed);

        shell.Close(claim, MoveCloseWhy.Resolved);

        Assert.Null(shell.StandingTo("s2"));
        Assert.True(Store("run-b").Decide(Decision("s2")).Claimed);
    }

    [Fact]
    public void The_person_s_press_closes_a_hold_resolved_without_its_claim_and_closes_nothing_else()
    {
        var host = Store("run-host");
        var claim = Delivering(host);
        var shell = Store("run-shell");
        var revision = Revision();

        // Not held: the press closes nothing and writes nothing; no such move, nothing.
        Assert.Equal(MoveRecordPhase.Delivering, shell.PressResolved(claim.Move)!.Phase);
        Assert.Null(shell.PressResolved(claim.Move + 99));
        Assert.Equal(revision, Revision());

        host.Hold(claim, MoveHoldWhy.Undelivered);
        var resolved = shell.PressResolved(claim.Move)!;

        // Closed by another run than its claim's, the claim left as it was; the claim's own close after is the press's, a phase written again.
        Assert.Equal(
            ((MoveRecordPhase?)MoveRecordPhase.Closed, (string?)MoveCloseWhy.Resolved, (string?)MoveHoldWhy.Undelivered, "run-host", 1L),
            (resolved.Phase, resolved.ClosedWhy, resolved.HeldWhy, resolved.ClaimRun, resolved.ClaimGeneration));
        Assert.Equal(resolved.Moments[MoveRecordPhase.Closed], host.Close(claim, MoveCloseWhy.Resolved).Moments[MoveRecordPhase.Closed]);
        Assert.Equal(MoveRecordPhase.Closed, shell.PressResolved(claim.Move)!.Phase);
        Assert.True(Store("run-b").Decide(Decision("s1")).Claimed);
    }
}
