using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-i part 2 (D212; the foundation design §3.3, §13.4, §13.5, §19.1): the take-up of dead runs' moves, one unit in the design's
/// order at each come-up and look: the imports of any box an older build wrote, once the start's confirmation is settled; then every
/// standing move of the unit's kind whose claim's run is dead, taken once under the generation it was read at, held where its effect was
/// marked and never recorded and closed <c>gone</c> where a record it names no longer exists; a live run's move quiet past ten minutes said
/// <c>move.quiet</c> once, never taken over; and then the caller's own sweep. A shell's come-up says once that another Daoris runs on its home.
/// Part 3 adds a taken move at <c>delivering</c> read by its words' <c>sent</c>, and every hold of the unit's scope read by §3.3's one rule.
/// </summary>
/// <remarks>
/// Every case has its own scratch home under <c>_fixtures/</c>. One process is one run to the lease's test, so a run's death is the liveness
/// each case hands in; a really killed executor is <see cref="MoveClaimProcessTests"/>'. Nothing here starts a process.
/// </remarks>
[Collection("agent operation ports")]
public sealed class MoveTakeUpTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "move-take-up", Guid.NewGuid().ToString("N")[..8]);

    private readonly MovedClock _clock = new();

    private readonly HashSet<string> _dead = new(StringComparer.Ordinal);

    private readonly List<(string Session, Noted Note)> _notes = [];

    /// <summary>The destinations' <c>said</c> part 3's readings read (FOUNDATION1f-i part 3).</summary>
    private readonly MoveSaidStandIn _said = new();

    private StoreSettled _settled = StoreSettled.Yes;

    public MoveTakeUpTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    /// <summary>A move store of one run, dead where the case says so and alive otherwise.</summary>
    private MoveStore Store(string run, string kind = StoreProcess.Shell) =>
        new(_home, () => _settled, each => !_dead.Contains(each.Run), run, kind, _clock);

    private MoveTakeUp TakeUp(MoveStore store, MoveTakeUpScope? scope = null, params IMoveBoxImport[] imports) =>
        new(_home, store, scope ?? MoveTakeUpScope.Conversations, imports, (session, note) => _notes.Add((session, note)), _clock, _said);

    private static MoveDecision Decision(string left, string executor = MoveExecutors.Conversation) =>
        new(left, executor, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris) { FromEnded = true };

    /// <summary>Every record stands.</summary>
    private static bool? Standing(string _) => true;

    /// <summary>A move of a run that then died, at <paramref name="phase"/>.</summary>
    private MoveRecord DeadMove(string left, string run = "run-dead", string executor = MoveExecutors.Conversation, MoveRecordPhase? phase = null)
    {
        var store = Store(run);
        var decided = store.Decide(Decision(left, executor));
        var claim = decided.Claim!;
        var move = decided.Move;
        if (phase is MoveRecordPhase.Opening or MoveRecordPhase.Opened) move = store.Opening(claim, $"opens-{left}");
        if (phase is MoveRecordPhase.Opened) move = store.Opened(claim, $"{left}-next", []);
        _dead.Add(run);
        return move;
    }

    private int LogLines(string @event)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Logs);
        return Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*.jsonl").SelectMany(File.ReadAllLines).Count(line => line.Contains($"\"event\":\"{@event}\"", StringComparison.Ordinal))
            : 0;
    }

    // ——— The take-up, per kind (§3.3, §13.4).

    [Fact]
    public async Task A_shell_takes_up_every_dead_run_s_conversation_move_once_and_leaves_a_live_run_s_its_own_and_every_driven_one()
    {
        var dead = DeadMove("s1");
        var driven = DeadMove("q1", executor: MoveExecutors.Driven);
        var live = Store("run-live").Decide(Decision("s2")).Move;
        var shell = Store("run-shell");
        var own = shell.Decide(Decision("s3")).Move;

        var look = await TakeUp(shell).LookAsync(Standing);

        var taken = Assert.Single(look.Taken);
        Assert.Equal((dead.Id, "run-shell", 2L), (taken.Move!.Id, taken.Claim!.Run, taken.Claim.Generation));
        Assert.Equal(("run-dead", 1L), (shell.Read(driven.Id)!.ClaimRun, shell.Read(driven.Id)!.ClaimGeneration));
        Assert.Equal("run-live", shell.Read(live.Id)!.ClaimRun);
        Assert.Equal("run-shell", shell.Read(own.Id)!.ClaimRun);
        Assert.Empty(look.Held);
        Assert.Empty(look.Gone);

        // Taken once: the next look finds it this run's own.
        Assert.Empty((await TakeUp(shell).LookAsync(Standing)).Taken);
        Assert.Equal(2L, shell.Read(dead.Id)!.ClaimGeneration);
    }

    [Fact]
    public async Task The_lease_holder_s_look_takes_up_every_dead_run_s_driven_move_and_a_share_loop_only_its_own_lines()
    {
        var mine = DeadMove("q1", executor: MoveExecutors.Driven);
        var theirs = DeadMove("q2", executor: MoveExecutors.Driven);
        var chat = DeadMove("s1");

        var share = await TakeUp(Store("run-share", StoreProcess.Host), MoveTakeUpScope.Shared(line => line == "q1")).LookAsync(Standing);

        Assert.Equal([mine.Id], share.Taken.Select(each => each.Move!.Id));
        var holder = await TakeUp(Store("run-holder", StoreProcess.Host), MoveTakeUpScope.LeaseHolder).LookAsync(Standing);
        Assert.Equal([theirs.Id], holder.Taken.Select(each => each.Move!.Id));
        Assert.Equal("run-dead", Store("run-probe").Read(chat.Id)!.ClaimRun);
    }

    [Fact]
    public async Task A_move_whose_phase_a_newer_build_wrote_is_never_taken_up()
    {
        var newer = DeadMove("s1");
        using (var store = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes }))
        {
            store.Write("probe", write => write.Execute("UPDATE moves SET phase = 'carrying' WHERE id = $id", ("$id", newer.Id)));
        }

        var look = await TakeUp(Store("run-shell")).LookAsync(Standing);

        Assert.Empty(look.Taken);
        Assert.Equal("run-dead", Store("run-probe").Read(newer.Id)!.ClaimRun);
    }

    // ——— What a taker finds (§3.3, §13.4 step 2; §3.3's `gone`).

    [Fact]
    public async Task A_dead_run_s_move_whose_effect_was_marked_and_has_no_outcome_is_held_by_its_taker_and_never_made_again()
    {
        var store = Store("run-dead");
        var decided = store.Decide(Decision("s1"));
        store.Opening(decided.Claim!, "opens-1");
        store.MarkEffect(decided.Claim!, MoveEffects.Spawn);
        var recorded = Store("run-other").Decide(Decision("s2"));
        Store("run-other").MarkEffect(recorded.Claim!, MoveEffects.Spawn);
        Store("run-other").EffectEnded(recorded.Claim!, "opened s2-next");
        _dead.UnionWith(["run-dead", "run-other"]);
        var shell = Store("run-shell");

        var look = await TakeUp(shell).LookAsync(Standing);

        var held = Assert.Single(look.Held);
        Assert.Equal((decided.Move.Id, (MoveRecordPhase?)MoveRecordPhase.Held, (string?)MoveHoldWhy.Unrecorded, "run-shell"), (held.Id, held.Phase, held.HeldWhy, held.ClaimRun));
        Assert.Equal(MoveEffects.Spawn, held.Effect!.What);
        Assert.Null(held.Effect.Outcome);
        Assert.Equal([recorded.Move.Id], look.Taken.Select(each => each.Move!.Id));
        Assert.Throws<MoveRefusedException>(() => shell.MarkEffect(new MoveClaim(held.Id, "run-shell", held.ClaimGeneration), MoveEffects.Spawn));
    }

    [Fact]
    public async Task A_dead_run_s_move_whose_record_left_or_destination_no_longer_exists_closes_gone_and_one_that_cannot_be_told_is_only_taken()
    {
        var left = DeadMove("s1");
        var destination = DeadMove("s2", phase: MoveRecordPhase.Opened);
        var unknown = DeadMove("s3");
        var shell = Store("run-shell");

        var look = await TakeUp(shell).LookAsync(record => record switch
        {
            "s1" or "s2-next" => false,
            "s3" => null,
            _ => true,
        });

        Assert.Equal([left.Id, destination.Id], look.Gone.Select(move => move.Id).Order());
        Assert.All(look.Gone, move => Assert.Equal(((MoveRecordPhase?)MoveRecordPhase.Closed, (string?)MoveCloseWhy.Gone), (move.Phase, move.ClosedWhy)));
        Assert.Equal([unknown.Id], look.Taken.Select(each => each.Move!.Id));
        Assert.True(shell.Read(unknown.Id)!.Stands);
        // Gone frees the record: its next move claims.
        Assert.True(shell.Decide(Decision("s1")).Claimed);
    }

    // ——— Two takers (§13.4, §19.1).

    [Fact]
    public async Task Of_two_takers_who_read_one_dead_run_s_move_the_second_is_refused_by_the_generation_and_nothing_is_taken_twice()
    {
        var dead = DeadMove("s1");
        var first = TakeUp(Store("run-a"));
        var second = TakeUp(Store("run-b"));
        MoveLook? firstLook = null;
        second.BeforeTake = move =>
        {
            // The second read the move at generation 1; the first takes it before the second's take.
            firstLook ??= first.LookAsync(Standing).GetAwaiter().GetResult();
        };

        var secondLook = await second.LookAsync(Standing);

        Assert.Equal([dead.Id], firstLook!.Taken.Select(each => each.Move!.Id));
        Assert.Empty(secondLook.Taken);
        var refused = Assert.Single(secondLook.Refused);
        Assert.Equal(MoveTakeRefusals.Moved, refused.Refused);
        Assert.Equal(("run-a", 2L), (Store("run-probe").Read(dead.Id)!.ClaimRun, Store("run-probe").Read(dead.Id)!.ClaimGeneration));
    }

    [Fact]
    public async Task Two_shells_looking_at_once_take_each_dead_move_once()
    {
        for (var round = 0; round < 25; round++)
        {
            var dead = DeadMove($"s{round}", run: $"run-dead-{round}");
            var looks = await Task.WhenAll(
                Task.Run(() => TakeUp(Store($"run-a-{round}")).LookAsync(Standing)),
                Task.Run(() => TakeUp(Store($"run-b-{round}")).LookAsync(Standing)));

            Assert.Single(looks.SelectMany(look => look.Taken).Where(each => each.Move!.Id == dead.Id));
            Assert.Equal(2L, Store("run-probe").Read(dead.Id)!.ClaimGeneration);
        }
    }

    // ——— The order: the imports, then the take-up, then the caller's sweep (§3.3, §13.5).

    private sealed class Recording(List<string> order, MoveBoxPass pass, params string[] pending) : IMoveBoxImport
    {
        public string Box => "recorded";

        public MoveBoxPass Import()
        {
            order.Add("import");
            return pass;
        }

        public IReadOnlyList<string> Pending() => pending;
    }

    [Fact]
    public async Task The_imports_run_first_then_the_take_up_then_the_caller_s_sweep_which_reads_what_the_look_found()
    {
        DeadMove("s2");
        var order = new List<string>();
        var shell = Store("run-shell");
        var imported = shell.Decide(Decision("s1")).Move;
        var unit = TakeUp(shell, null, new Recording(order, new MoveBoxPass([imported], [], [], 0, null)));
        unit.BeforeTake = move => order.Add($"take {move.Left}");
        MoveLook? swept = null;

        var look = await unit.LookAsync(Standing, (found, _) =>
        {
            order.Add("sweep");
            swept = found;
            return Task.CompletedTask;
        });

        Assert.Equal(["import", "take s2", "sweep"], order);
        Assert.Same(look, swept);
        Assert.Equal([imported.Id], look.Imported.Select(move => move.Id));
        Assert.Equal(["s2"], look.Taken.Select(each => each.Move!.Left));
        Assert.Equal("run-shell", shell.Read(imported.Id)!.ClaimRun);
    }

    [Fact]
    public async Task While_the_imports_wait_on_the_confirmation_a_record_whose_box_waits_is_never_taken_up_and_the_sweep_reads_it_as_never()
    {
        new HandOverBox(_home).Write("s1", new HandOverRequest("codex", null, HandOverWhy.Press, HandOverBy.Person, _clock.GetUtcNow()));
        DeadMove("s1-next");
        var older = DeadMove("s1");
        _settled = StoreSettled.Not("the install's swap journal is at `swapping`");
        var shell = Store("run-shell");
        var unit = TakeUp(shell, null, new HandOverBoxImport(_home, shell, _ => true));
        MoveLook? swept = null;

        var held = await unit.LookAsync(Standing, (found, _) =>
        {
            swept = found;
            return Task.CompletedTask;
        });

        Assert.Contains("`swapping`", held.ImportsHeld);
        Assert.Equal(["s1"], swept!.Never);
        Assert.Equal(["s1-next"], held.Taken.Select(each => each.Move!.Left));
        Assert.Equal("run-dead", shell.Read(older.Id)!.ClaimRun);

        // Asked again at the next look: settled now, the box is behind the standing move, so the dead run's move is taken up and the box
        // waits until that move ends.
        _settled = StoreSettled.Yes;
        var settled = await unit.LookAsync(Standing);

        Assert.Null(settled.ImportsHeld);
        Assert.Equal([older.Id], settled.Taken.Select(each => each.Move!.Id));
        Assert.Equal(["s1"], settled.Never);
        Assert.True(File.Exists(new HandOverBox(_home).PathOf("s1")));
    }

    // ——— A hung executor (§19.1 point 4).

    [Fact]
    public async Task A_live_run_s_move_quiet_past_ten_minutes_after_old_ended_is_said_once_on_its_line_and_in_the_log_and_never_taken_over()
    {
        var hung = Store("run-hung");
        var decided = hung.Decide(Decision("s1"));
        hung.Opening(decided.Claim!, "opens-1");
        var shell = Store("run-shell");
        var unit = TakeUp(shell);

        _clock.Advance(TimeSpan.FromMinutes(9));
        Assert.Empty((await unit.LookAsync(Standing)).Quiet);
        Assert.Empty(_notes);

        _clock.Advance(TimeSpan.FromMinutes(2));
        var look = await unit.LookAsync(Standing);

        Assert.Equal([decided.Move.Id], look.Quiet.Select(move => move.Id));
        var (session, note) = Assert.Single(_notes);
        Assert.Equal("s1", session);
        var part = Assert.Single(note.Parts!);
        Assert.Equal("move.quiet", part.Code);
        Assert.Equal(11, Convert.ToInt32(part.Values.Single(value => value.Key == "minutes").Value, System.Globalization.CultureInfo.InvariantCulture));
        NoteAssert.Holds(note);
        Assert.Equal(1, LogLines("move.quiet"));
        Assert.Empty(look.Taken);

        // Said once, whoever looks next: this shell again, or another process on the home.
        _clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Empty((await unit.LookAsync(Standing)).Quiet);
        Assert.Empty((await TakeUp(Store("run-other")).LookAsync(Standing)).Quiet);
        Assert.Single(_notes);
        Assert.Equal(1, LogLines("move.quiet"));
        Assert.Equal(("run-hung", 1L), (shell.Read(decided.Move.Id)!.ClaimRun, shell.Read(decided.Move.Id)!.ClaimGeneration));

        // A later phase that stands still as long is a stretch of its own, said once more.
        hung.Opened(decided.Claim!, "s1-next", []);
        _clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Single((await unit.LookAsync(Standing)).Quiet);
        Assert.Equal(2, _notes.Count);
    }

    [Fact]
    public void Of_two_lookers_who_read_a_quiet_move_before_either_said_it_exactly_one_says_it_and_the_other_writes_nothing()
    {
        var hung = Store("run-hung");
        var decided = hung.Decide(Decision("s1"));
        var read = hung.Opening(decided.Claim!, "opens-1");
        var since = read.Moments[MoveRecordPhase.Opening];
        long Revision()
        {
            using var reader = DesktopStoreReader.Open(DesktopStore.LocationIn(_home))!;
            return reader.Revisions()[MoveStore.Family];
        }

        Assert.True(Store("run-a").SayQuiet(read, since));
        var after = Revision();
        Assert.False(Store("run-b").SayQuiet(read, since));
        Assert.Equal(after, Revision());
        Assert.Equal(since, hung.Read(read.Id)!.QuietAt);

        // The move moved on since it was read: nothing is said of the phase it left.
        _clock.Advance(TimeSpan.FromMinutes(1));
        var opened = hung.Opened(decided.Claim!, "s1-next", []);
        Assert.False(Store("run-a").SayQuiet(read, "2026-10-20T09:30:00.0000000+00:00"));
        Assert.True(Store("run-a").SayQuiet(opened, opened.Moments[MoveRecordPhase.Opened]));
    }

    [Fact]
    public async Task A_move_waiting_on_its_old_part_or_at_old_ended_or_held_is_never_said_quiet()
    {
        var store = Store("run-live");
        var quiescing = store.Decide(Decision("s1") with { FromEnded = false });
        store.Advance(quiescing.Claim!, MoveRecordPhase.Quiescing);
        store.Decide(Decision("s2"));
        var held = store.Decide(Decision("s3"));
        store.Hold(held.Claim!, MoveHoldWhy.Unrecorded);

        _clock.Advance(TimeSpan.FromHours(2));
        var look = await TakeUp(Store("run-shell")).LookAsync(Standing);

        Assert.Empty(look.Quiet);
        Assert.Empty(_notes);
    }

    // ——— Two shells on one home (§19.1 point 3).

    [Fact]
    public async Task A_shell_coming_up_beside_another_live_shell_says_once_that_another_Daoris_runs_on_its_home_and_refuses_nothing()
    {
        var other = Store("run-first");
        other.Enter();
        Store("run-host", StoreProcess.Host).Enter();
        Store("run-gone").Enter();
        _dead.Add("run-gone");
        var said = new List<string>();
        var shell = Store("run-second");
        var unit = TakeUp(shell);

        await unit.ComeUpAsync(Standing, say: said.Add);
        await unit.ComeUpAsync(Standing, say: said.Add);
        await TakeUp(shell).ComeUpAsync(Standing, say: said.Add);

        var sentence = Assert.Single(said);
        Assert.Contains($"process {Environment.ProcessId}", sentence);
        Assert.StartsWith("Another Daoris runs on this home", sentence);
        Assert.Equal(1, LogLines("home.another-shell"));
        Assert.Contains(shell.Runs(), run => run.Run == "run-second");
        Assert.True(shell.Decide(Decision("s1")).Claimed);
    }

    [Fact]
    public async Task A_shell_alone_on_its_home_or_beside_a_host_alone_says_nothing_and_a_host_never_says_it()
    {
        Store("run-host", StoreProcess.Host).Enter();
        var said = new List<string>();

        await TakeUp(Store("run-shell")).ComeUpAsync(Standing, say: said.Add);
        await TakeUp(Store("run-other-host", StoreProcess.Host), MoveTakeUpScope.LeaseHolder).ComeUpAsync(Standing, say: said.Add);

        Assert.Empty(said);
        Assert.Equal(0, LogLines("home.another-shell"));
    }

    // ——— Part 3: a taken move at `delivering` read by `sent`, and every hold read by §3.3's one rule at each come-up and look.

    /// <summary>A move of <paramref name="left"/> opened onto <c>{left}-next</c> with the words named waiting there, at <c>delivering</c>, its prompt marked and every word attempted.</summary>
    private MoveClaim DeliveringMove(MoveStore store, string left, params string[] words)
    {
        var claim = store.Decide(Decision(left)).Claim!;
        store.Opening(claim, $"opens-{left}");
        store.Opened(claim, $"{left}-next", [.. words.Select((word, at) => new MoveRecordWord(word, null, $"opens-{left}/{word}", at))]);
        foreach (var word in words) _said.Wait($"{left}-next", word);
        store.Advance(claim, MoveRecordPhase.Delivering);
        store.MarkEffect(claim, MoveEffects.Prompt);
        _said.Attempt($"{left}-next", words);
        return claim;
    }

    /// <summary>A move of <paramref name="left"/> opened onto <c>{left}-next</c> with the words named waiting there, held for the person naming each (§19.3).</summary>
    private MoveClaim HeldMove(MoveStore store, string left, params string[] words)
    {
        var claim = store.Decide(Decision(left)).Claim!;
        store.Opening(claim, $"opens-{left}");
        store.Opened(claim, $"{left}-next", [.. words.Select((word, at) => new MoveRecordWord(word, null, $"opens-{left}/{word}", at))]);
        foreach (var word in words) _said.Wait($"{left}-next", word);
        store.Hold(claim, MoveHoldWhy.Superseded, words.ToDictionary(word => $"opens-{left}/{word}", _ => MoveWordDispositions.Held, StringComparer.Ordinal));
        return claim;
    }

    [Fact]
    public async Task A_dead_run_s_move_at_delivering_is_read_by_sent_after_its_take_closed_delivered_where_every_word_has_it_and_held_where_one_does_not()
    {
        var dead = Store("run-dead");
        var written = DeliveringMove(dead, "s1", "w1");
        dead.Sent(written, ["opens-s1/w1"]);
        var cut = DeliveringMove(dead, "s2", "v1");
        _said.Gone("s2-next", "v1");
        _dead.Add("run-dead");

        var look = await TakeUp(Store("run-shell")).ComeUpAsync(Standing);

        Assert.Equal([written.Move], look.Delivered.Select(move => move.Id));
        Assert.Equal(MoveRecordPhase.Delivered, Store("run-probe").Read(written.Move)!.Phase);
        Assert.Empty(_said.Of("s1-next"));
        var held = Assert.Single(look.Held);
        Assert.Equal((cut.Move, (string?)MoveHoldWhy.Undelivered), (held.Id, held.HeldWhy));
        Assert.Equal(MoveWordDispositions.Left, Store("run-probe").WordsOf(cut.Move).Single().Disposition);

        // Its left word waits nowhere and its begun effect on no `said`: closed resolved in the same look.
        Assert.Equal([cut.Move], look.Resolved.Select(move => move.Id));
        Assert.Empty(look.Taken);
    }

    [Fact]
    public async Task Every_hold_of_the_unit_s_scope_is_read_by_the_one_rule_at_each_come_up_and_look_its_own_and_a_dead_run_s_and_never_a_live_run_s()
    {
        var shell = Store("run-shell");
        var own = HeldMove(shell, "s1", "w1");
        var dead = HeldMove(Store("run-dead"), "s2", "v1");
        var live = HeldMove(Store("run-live"), "s3", "u1");
        _dead.Add("run-dead");

        // While no one ran, the person took v1 back at the dead run's destination: its hold closes at the come-up, once taken.
        _said.Gone("s2-next", "v1");
        var comeUp = await TakeUp(shell).ComeUpAsync(Standing);

        Assert.Equal([dead.Move], comeUp.Resolved.Select(move => move.Id));
        Assert.Equal(("run-shell", MoveCloseWhy.Resolved), (shell.Read(dead.Move)!.ClaimRun, shell.Read(dead.Move)!.ClosedWhy!));
        Assert.Equal(MoveRecordPhase.Held, shell.Read(own.Move)!.Phase);

        // Then the person takes w1 and u1 back: this shell's own hold closes at its next look; the live run's is its own executor's to read.
        _said.Gone("s1-next", "w1");
        _said.Gone("s3-next", "u1");
        var look = await TakeUp(shell).LookAsync(Standing);

        Assert.Equal([own.Move], look.Resolved.Select(move => move.Id));
        Assert.Equal((MoveRecordPhase.Held, "run-live"), (shell.Read(live.Move)!.Phase!.Value, shell.Read(live.Move)!.ClaimRun));
        Assert.True(shell.Decide(Decision("s1")).Claimed);
        Assert.False(shell.Decide(Decision("s3")).Claimed);
    }
}
