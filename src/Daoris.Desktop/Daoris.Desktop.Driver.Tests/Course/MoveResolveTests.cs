using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-i part 3 (D212; the foundation design §3.3's one rule, §13.5's <c>held</c> row, §19.3): a held move closes <c>resolved</c> once
/// nothing it names still waits on its destination's <c>said</c>, read at the person's press through one of its doors, by its executor at
/// each come-up and look, and wherever the course reads the destination's standing move as a precondition; until then it stands and its
/// destination takes no move of its own. A begun effect alone keeps no hold. Read by nothing: the course is called directly here, as the
/// row's proof says (round 8, <i>O8-F6</i>), and the come-up's and the look's reads are <see cref="MoveTakeUpTests"/>'.
/// </summary>
/// <remarks>
/// Every case has its own scratch home under <c>_fixtures/</c>; the destination's <c>said</c> is <see cref="MoveSaidStandIn"/>. One process is one
/// run to the lease's test, so each executor is a move store under a run of its own, alive unless the case says so. Nothing here starts a process.
/// </remarks>
[Collection("agent operation ports")]
public sealed class MoveResolveTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "move-resolve", Guid.NewGuid().ToString("N")[..8]);

    private readonly HashSet<string> _dead = new(StringComparer.Ordinal);

    private readonly MoveSaidStandIn _said = new();

    public MoveResolveTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private MoveStore Store(string run, Func<StoreSettled>? settled = null, string kind = StoreProcess.Shell) =>
        new(_home, settled ?? (() => StoreSettled.Yes), each => !_dead.Contains(each.Run), run, kind);

    private MoveResolve Resolve(MoveStore store) => new(store, _said);

    private static MoveDecision Decision(string left) =>
        new(left, MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris) { FromEnded = true };

    /// <summary>
    /// A move of <paramref name="left"/> opened onto <paramref name="destination"/> with the words named, each waiting there, held for the
    /// person with the head moved past its destination (§19.3), naming every word it moved.
    /// </summary>
    private MoveClaim HeldSuperseded(MoveStore store, string left = "s1", string destination = "s2", params string[] words)
    {
        var claim = store.Decide(Decision(left)).Claim!;
        store.Opening(claim, $"opens-{left}");
        store.Opened(claim, destination, [.. words.Select((word, at) => new MoveRecordWord(word, null, $"opens-{left}/{word}", at))]);
        foreach (var word in words) _said.Wait(destination, word);
        store.Hold(claim, MoveHoldWhy.Superseded, words.ToDictionary(word => $"opens-{left}/{word}", _ => MoveWordDispositions.Held, StringComparer.Ordinal));
        return claim;
    }

    // ——— At the person's press (§3.3; §19.3).

    [Fact]
    public async Task At_the_person_s_press_a_hold_closes_resolved_once_nothing_it_names_waits_without_its_claim_and_a_second_move_of_its_record_claims()
    {
        var host = Store("run-host", kind: StoreProcess.Host);
        var claim = HeldSuperseded(host, words: ["w1", "w2"]);
        var shell = Store("run-shell");

        // Each of the person's take-backs at the destination is one of the hold's doors; the hold stands while a word it names waits.
        _said.Gone("s2", "w1");
        Assert.Equal(MoveRecordPhase.Held, (await Resolve(shell).PressedAsync("s2"))!.Phase);
        _said.Gone("s2", "w2");
        var resolved = (await Resolve(shell).PressedAsync("s2"))!;

        Assert.Equal(((MoveRecordPhase?)MoveRecordPhase.Closed, (string?)MoveCloseWhy.Resolved), (resolved.Phase, resolved.ClosedWhy));
        Assert.Equal(("run-host", 1L), (resolved.ClaimRun, resolved.ClaimGeneration));

        // Resolved, then a second move of the same record, which claims; the destination moves on its own too (round 6, *O5-F1*).
        var second = host.Decide(Decision("s1"));
        Assert.True(second.Claimed);
        Assert.NotEqual(claim.Move, second.Move.Id);
        Assert.True(shell.Decide(Decision("s2")).Claimed);
        Assert.Equal([claim.Move, second.Move.Id], host.Of("s1").Select(move => move.Id));
    }

    [Fact]
    public async Task A_move_closed_superseded_then_a_second_move_of_the_same_record_which_claims_while_no_read_finds_the_first_standing()
    {
        var shell = Store("run-shell");
        var first = shell.Decide(Decision("s1")).Claim!;
        shell.Opening(first, "opens-s1");

        // The open refused: the old part went on before it, so the move closes superseded.
        shell.Close(first, MoveCloseWhy.Superseded);

        Assert.Null(await Resolve(shell).StandingAsync("s1"));
        Assert.Null(await Resolve(shell).PressedAsync("s1"));
        var second = shell.Decide(Decision("s1"));
        Assert.True(second.Claimed);
        Assert.Equal(second.Move.Id, (await Resolve(shell).StandingAsync("s1"))!.Id);
        Assert.Equal(MoveCloseWhy.Superseded, shell.Read(first.Move)!.ClosedWhy);
    }

    [Fact]
    public async Task The_press_closes_nothing_where_the_destination_s_said_cannot_be_read_or_nothing_is_held()
    {
        var host = Store("run-host", kind: StoreProcess.Host);
        HeldSuperseded(host, words: ["w1"]);
        _said.Gone("s2", "w1");
        _said.Unreadable.Add("s2");
        var shell = Store("run-shell");

        Assert.Equal(MoveRecordPhase.Held, (await Resolve(shell).PressedAsync("s2"))!.Phase);
        Assert.Null(await Resolve(shell).PressedAsync("s9"));

        _said.Unreadable.Clear();
        Assert.Equal(MoveRecordPhase.Closed, (await Resolve(shell).PressedAsync("s2"))!.Phase);
    }

    // ——— Wherever the course reads the destination's standing move as a precondition (§3.3; round 8, *O7-F11*).

    [Fact]
    public async Task A_hold_whose_words_the_person_took_back_closes_when_the_course_next_reads_its_destination_with_no_look_and_the_destination_then_moves()
    {
        // A shell beside a headless lease holder runs no look: only the course's own read of the destination closes its hold.
        var shell = Store("run-shell");
        HeldSuperseded(shell, words: ["w1"]);
        Assert.Equal(MoveRecordPhase.Held, (await Resolve(shell).StandingAsync("s2"))!.Phase);
        Assert.False(shell.Decide(Decision("s2")).Claimed);

        _said.Gone("s2", "w1");

        Assert.Null(await Resolve(shell).StandingAsync("s2"));
        Assert.Equal(MoveCloseWhy.Resolved, shell.Of("s1").Single().ClosedWhy);
        Assert.True(shell.Decide(Decision("s2")).Claimed);
    }

    /// <summary>
    /// 🔴 FOUNDATION1i path 1 (the foundation design §10 row 1i; round 8, <i>O7-F11</i>'s driven half, <i>O8-F6</i>): a driven line's limit
    /// resume reads its destination's standing move through the course's one read, which closes a hold whose words the person took back, and
    /// the destination's limit move is then made; while the hold stands, the course holds it, and the limit's own account resumes as ever.
    /// </summary>
    [Fact]
    public async Task A_driven_line_s_held_move_whose_words_were_taken_back_closes_when_its_limit_resume_reads_the_destination_and_the_move_is_made()
    {
        var shell = Store("run-shell");
        HeldSuperseded(shell, words: ["w1"]);
        var limited = new PriorSession("s2", _home, "failed") { Limit = true, Profile = "account-1", Adapter = "claude-code-acp" };
        LineAnswer elsewhere = new LineAnswer.Elsewhere(new HarnessSelection(Refusal: null, Profile: "account-2"), NextHold.Cooling);

        var standing = await DrivenCourse.StandingAsync(Resolve(shell), "s2");
        var held = Assert.IsType<LineCourse.Hold>(DrivenCourse.LimitOwn(limited, elsewhere, standing));
        Assert.Equal(CourseHoldWhy.MoveHeld, held.Why);

        _said.Gone("s2", "w1");
        standing = await DrivenCourse.StandingAsync(Resolve(shell), "s2");

        Assert.Null(standing);
        Assert.Equal(MoveCloseWhy.Resolved, shell.Of("s1").Single().ClosedWhy);
        var made = Assert.IsType<LineCourse.Move>(DrivenCourse.LimitOwn(limited, elsewhere, standing));
        Assert.Equal((CourseFromWhy.Limit, CourseAccountSource.Settled, "account-2"), (made.Why, made.Where.Source, made.Where.Account));
        Assert.IsType<LineCourse.Resume>(DrivenCourse.LimitOwn(limited, new LineAnswer.Resume(), standing));
    }

    [Fact]
    public async Task A_hold_naming_only_a_begun_effect_closes_at_its_first_read_and_its_effect_is_never_made_again()
    {
        var shell = Store("run-shell");
        var claim = shell.Decide(Decision("s1")).Claim!;
        shell.Opening(claim, "opens-s1");
        shell.Opened(claim, "s2", []);
        shell.MarkEffect(claim, MoveEffects.Spawn);
        shell.Hold(claim, MoveHoldWhy.Unrecorded);

        // It waits on no `said`: closed without the destination being read at all.
        _said.Unreadable.Add("s2");
        Assert.Null(await Resolve(shell).StandingAsync("s2"));

        var closed = shell.Read(claim.Move)!;
        Assert.Equal((MoveCloseWhy.Resolved, MoveHoldWhy.Unrecorded), (closed.ClosedWhy!, closed.HeldWhy!));
        Assert.Equal((MoveEffects.Spawn, (string?)null), (closed.Effect!.What, closed.Effect.Outcome));
        Assert.Throws<MoveRefusedException>(() => shell.MarkEffect(claim, MoveEffects.Spawn));
    }

    [Fact]
    public async Task A_begun_effect_keeps_no_hold_beside_a_named_word_and_is_never_made_again_while_the_word_keeps_it()
    {
        var shell = Store("run-shell");
        var claim = shell.Decide(Decision("s1")).Claim!;
        shell.Opening(claim, "opens-s1");
        shell.Opened(claim, "s2", [new("w1", null, "opens-s1/w1", 0)]);
        _said.Wait("s2", "w1");
        shell.Advance(claim, MoveRecordPhase.Delivering);
        shell.MarkEffect(claim, MoveEffects.Prompt);
        shell.Hold(claim, MoveHoldWhy.Superseded, new Dictionary<string, string> { ["opens-s1/w1"] = MoveWordDispositions.Held });

        Assert.Equal(MoveRecordPhase.Held, (await Resolve(shell).ReadAsync(claim)).Phase);
        Assert.Throws<MoveRefusedException>(() => shell.MarkEffect(claim, MoveEffects.Prompt));

        _said.Gone("s2", "w1");

        Assert.Equal(MoveRecordPhase.Closed, (await Resolve(shell).ReadAsync(claim)).Phase);
        Assert.Null(shell.Read(claim.Move)!.Effect!.Outcome);
    }

    [Fact]
    public async Task Another_live_run_s_hold_is_read_as_standing_and_never_closed_by_a_read_that_does_not_hold_its_claim()
    {
        var other = Store("run-other");
        HeldSuperseded(other, words: ["w1"]);
        _said.Gone("s2", "w1");
        var shell = Store("run-shell");

        var standing = await Resolve(shell).StandingAsync("s2");

        Assert.Equal(MoveRecordPhase.Held, standing!.Phase);
        Assert.Equal(MoveRecordPhase.Held, (await Resolve(shell).StandingAsync("s1"))!.Phase);
        Assert.False(shell.Decide(Decision("s2")).Claimed);

        // Its own executor's read closes it.
        Assert.Null(await Resolve(other).StandingAsync("s2"));
    }

    // ——— The executor's read under its claim: an uncertain word settled, then the rule (§13.4 step 4; round 8, *O7-F4*).

    [Fact]
    public async Task A_hold_s_uncertain_word_still_waiting_is_settled_at_its_executor_s_next_read_and_the_hold_then_closes()
    {
        var shell = Store("run-shell");
        var claim = shell.Decide(Decision("s1")).Claim!;
        shell.Opening(claim, "opens-s1");
        shell.Opened(claim, "s2", [new("w1", null, "opens-s1/w1", 0)]);
        _said.Wait("s2", "w1");
        _said.Attempt("s2", "w1");
        shell.Advance(claim, MoveRecordPhase.Delivering);
        shell.Hold(claim, MoveHoldWhy.Undelivered, new Dictionary<string, string> { ["opens-s1/w1"] = MoveWordDispositions.Uncertain });

        // The host did not take the settlement: the word still waits, so the hold stands, and the press closes nothing either.
        _said.Keeps.Add("s2");
        Assert.Equal(MoveRecordPhase.Held, (await Resolve(shell).ReadAsync(claim)).Phase);
        Assert.Equal(MoveRecordPhase.Held, (await Resolve(Store("run-press")).PressedAsync("s2"))!.Phase);

        _said.Keeps.Clear();
        var read = await Resolve(shell).ReadAsync(claim);

        Assert.Equal(MoveRecordPhase.Closed, read.Phase);
        Assert.Equal(["settle s2 w1", "settle s2 w1"], _said.Calls);
        Assert.Empty(_said.Of("s2"));
    }

    // ——— §13.5's `held` row: the newer build back closes or keeps the hold.

    [Fact]
    public async Task Across_a_downgrade_the_newer_build_back_closes_a_hold_whose_words_an_older_build_took_off_and_keeps_one_whose_words_still_wait()
    {
        var shell = Store("run-shell");
        var gone = HeldSuperseded(shell, "s1", "s2", "w1");
        var kept = HeldSuperseded(shell, "s3", "s4", "v1");

        // The older build, run on the home meanwhile, writes the store as it did and knows no move; its person takes w1 back at s2.
        using (var older = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(3)], Settled = () => StoreSettled.Yes }))
        {
            older.Write(BindingStore.Family, write => write.Execute(
                "INSERT INTO bindings (session, adapter, conversation, kept) VALUES ('s2', 'claude-code-acp', 'c-2', '2026-10-30T00:00:00.0000000Z')"));
        }

        _said.Gone("s2", "w1");

        Assert.Equal(MoveRecordPhase.Closed, (await Resolve(shell).ReadAsync(gone)).Phase);
        Assert.Equal(MoveRecordPhase.Held, (await Resolve(shell).ReadAsync(kept)).Phase);
        Assert.Equal(MoveWordDispositions.Held, shell.WordsOf(kept.Move).Single().Disposition);
    }
}
