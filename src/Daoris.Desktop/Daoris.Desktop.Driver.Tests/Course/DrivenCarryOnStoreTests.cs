using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-iv part a (D212; the foundation design §3.3, §13.4 steps 1 to 4, §19.4): the driven carry-on keeps its move record, as the live
/// limit move does since row f-ii. Decided at <c>old-ended</c>, the session it goes on from having ended already; <c>opening</c> under the key
/// its keyed open is sent with; <c>opened</c> with every word the open moved, by its id on the new part; <c>delivering</c> and the prompt's mark
/// before the take; each handed word's <c>sent</c> where today's take returns, then the reading that closes it <c>delivered</c>. A carry-on the
/// quest's hand-over made keeps none (row f-v's); a standing move on the record holds the carry-on, which writes no second move; a carry-on that
/// goes no further than its open is ended by its phase, its record closed.
/// </summary>
/// <remarks>
/// The destination's <c>said</c> is <see cref="MoveSaidStandIn"/>, and the take the carry-on makes is its <c>TakeAsync</c>; nothing here starts a
/// process. The tick that runs the real carry-on is <c>SessionMessagesTickTests</c>'. Every case has its own scratch home under <c>_fixtures/</c>.
/// </remarks>
[Collection("agent operation ports")]
public sealed class DrivenCarryOnStoreTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "driven-carry-on-store", Guid.NewGuid().ToString("N")[..8]);

    private readonly MoveSaidStandIn _said = new();

    public DrivenCarryOnStoreTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private readonly HashSet<string> _dead = new(StringComparer.Ordinal);

    private MoveStore Store(string run) => new(_home, () => StoreSettled.Yes, each => !_dead.Contains(each.Run), run, StoreProcess.Host);

    /// <summary>The driven holder of one run: the conversations' holder over the same store, handed the lease holder's scope.</summary>
    private ConversationMoves Holder(string run = "run-loop") =>
        new(_home, new ServiceClient("http://carry-on.test", null), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)),
            store: Store(run), said: _said, scope: MoveTakeUpScope.LeaseHolder);

    private static MoveDecision Carry(string why = HandOverWhy.CarryOn) =>
        CarryOnRecord.Decision(new ContinuedFrom("s1", why, HandOverBy.Daoris), agent: "claude-code", account: "account-2")!;

    /// <summary>The quest's hand-over as its ask decides it (<c>QuestHandOver.AskAsync</c>): driven, the agent and account named, at <c>decided</c>.</summary>
    private static MoveDecision HandOver(string agent = "codex-acp") =>
        new("s1", MoveExecutors.Driven, MoveHows.Brief, HandOverWhy.Press, HandOverBy.Person) { Agent = agent, Account = "work" };

    private MoveRecord? MoveFrom(string left) => Store("run-probe").Of(left).LastOrDefault();

    private static MovedWord Moved(int n) => new($"w{n}", $"w{n}", null, $"opens-s1/w{n}", n - 1);

    /// <summary>
    /// 🔴 One carry-on, written in the design's order (§13.4): decided at <c>old-ended</c> under this run's claim, since the session it goes on
    /// from has ended; <c>opening</c> under the key its open is sent with; <c>opened</c> with the moved word by its id on the new part;
    /// <c>delivering</c> with the prompt marked before the take; then the word's <c>sent</c> where the take returns, and <c>delivered</c>.
    /// </summary>
    [Fact]
    public async Task A_carry_on_is_decided_old_ended_then_opening_opened_delivering_and_delivered_in_that_order()
    {
        var holder = Holder();
        var begun = await holder.BeginAsync(Carry(), default);
        await using var record = new CarryOnRecord(begun.Move!);

        var decided = MoveFrom("s1")!;
        Assert.Equal(("old-ended", "run-loop", "driven", "brief", "carry-on", "daoris"), (decided.PhaseWord, decided.ClaimRun, decided.Executor, decided.How, decided.Why, decided.By));
        Assert.Equal(("account-2", "claude-code", "carry-on"), (decided.Account, decided.Agent, decided.RecipeWhy));
        Assert.False(decided.Moments.ContainsKey(MoveRecordPhase.Quiescing));

        var key = MoveWords.DrivenKey();
        Assert.Null(record.Opening(key));
        var opening = MoveFrom("s1")!;
        Assert.Equal(("opening", key), (opening.PhaseWord, opening.Opens));
        Assert.StartsWith("drive-move/", key, StringComparison.Ordinal);

        _said.Wait("s2", "w1");
        Assert.Null(await record.FenceAsync("s2", [Moved(1)]));
        var delivering = MoveFrom("s1")!;
        Assert.Equal(("delivering", "s2"), (delivering.PhaseWord, delivering.Destination));
        Assert.Equal((MoveEffects.Prompt, (string?)null), (delivering.Effect!.What, delivering.Effect.Outcome));
        var word = Assert.Single(Store("run-probe").WordsOf(delivering.Id));
        Assert.Equal(("w1", (string?)null), (word.Word, word.Disposition));

        // FOUNDATION1f-iv part b: the door's write return writes the word's `sent`, then the take is made, then the reading closes the move.
        List<string?> seen = [];
        record.Handed(["opens-s1/w1"], async () =>
        {
            seen.Add($"{MoveFrom("s1")!.Effect!.Outcome}/{Store("run-probe").WordsOf(delivering.Id).Single().Disposition}/{string.Join(",", _said.Of("s2"))}");
            await _said.TakeAsync("s2", ["w1"], default);
        });
        Assert.Null(Store("run-probe").WordsOf(delivering.Id).Single().Disposition);
        record.Written();
        await record.DisposeAsync();

        Assert.Equal(["sent/sent/w1"], seen);
        var delivered = MoveFrom("s1")!;
        Assert.Equal(("delivered", MoveWordDispositions.Sent), (delivered.PhaseWord, delivered.Effect!.Outcome));
        Assert.Equal(MoveWordDispositions.Sent, Assert.Single(Store("run-probe").WordsOf(delivered.Id)).Disposition);
        Assert.Empty(_said.Of("s2"));
    }

    /// <summary>
    /// 🔴 FOUNDATION1f-iv part b: the write's return is told once however many doors tell it (the protocol door's first prompt and a transient
    /// try's write both do): <c>sent</c> is written once and the take is made once.
    /// </summary>
    [Fact]
    public async Task A_write_told_again_by_a_second_door_makes_the_take_once()
    {
        var holder = Holder();
        var begun = await holder.BeginAsync(Carry(), default);
        await using var record = new CarryOnRecord(begun.Move!);
        Assert.Null(record.Opening(MoveWords.DrivenKey()));
        _said.Wait("s2", "w1");
        Assert.Null(await record.FenceAsync("s2", [Moved(1)]));
        var takes = 0;
        record.Handed(["opens-s1/w1"], () =>
        {
            Interlocked.Increment(ref takes);
            return _said.TakeAsync("s2", ["w1"], default);
        });

        record.Written();
        record.Written();
        await record.DisposeAsync();

        Assert.Equal(1, takes);
        Assert.Equal("delivered", MoveFrom("s1")!.PhaseWord);
    }

    /// <summary>
    /// 🔴 FOUNDATION1f-iv part b: a record whose prompt was never written is ended by its phase and makes no take, however the take was handed:
    /// the word was not sent, so it waits on the new part for its own go-on.
    /// </summary>
    [Fact]
    public async Task A_prompt_never_written_makes_no_take_and_leaves_the_word_unsent()
    {
        var holder = Holder();
        var begun = await holder.BeginAsync(Carry(), default);
        var takes = 0;
        _said.Wait("s2", "w1");
        await using (var record = new CarryOnRecord(begun.Move!))
        {
            Assert.Null(record.Opening(MoveWords.DrivenKey()));
            Assert.Null(await record.FenceAsync("s2", [Moved(1)]));
            record.Handed(["opens-s1/w1"], () =>
            {
                Interlocked.Increment(ref takes);
                return Task.CompletedTask;
            });
        }

        Assert.Equal(0, takes);
        var move = MoveFrom("s1")!;
        Assert.Equal(("closed", MoveHoldWhy.Undelivered, ConversationMoves.Unsent), (move.PhaseWord, move.HeldWhy, move.Effect!.Outcome));
        Assert.Null(Store("run-probe").WordsOf(move.Id).Single().Disposition);
        Assert.Equal(["w1"], _said.Of("s2"));
    }

    /// <summary>
    /// 🔴 FOUNDATION1f-iv part b: a loop that died between the door's write and the take left the word <c>sent</c> and still waiting on the new
    /// part; the next loop's first look takes the dead run's move up and reads it, taking the word off as taken and closing the move
    /// <c>delivered</c>, so it is never sent twice and the line's next carry-on is not held.
    /// </summary>
    [Fact]
    public async Task A_dead_loop_s_move_past_the_write_and_before_the_take_is_taken_up_delivered_and_frees_the_line()
    {
        var dying = Holder("run-dying");
        var begun = await dying.BeginAsync(Carry(), default);
        _said.Wait("s2", "w1");
        var record = new CarryOnRecord(begun.Move!);
        Assert.Null(record.Opening(MoveWords.DrivenKey()));
        Assert.Null(await record.FenceAsync("s2", [Moved(1)]));
        // The loop dies as the take would begin: `sent` is written, the take never made.
        var stopped = new TaskCompletionSource();
        record.Handed(["opens-s1/w1"], () => stopped.Task);
        record.Written();
        Assert.Equal("sent", Store("run-probe").WordsOf(MoveFrom("s1")!.Id).Single().Disposition);
        Assert.Equal("delivering", MoveFrom("s1")!.PhaseWord);
        _dead.Add("run-dying");

        var next = Holder("run-next");
        Assert.NotNull(await next.LookAsync(default));

        var move = MoveFrom("s1")!;
        Assert.Equal("delivered", move.PhaseWord);
        Assert.Empty(_said.Of("s2"));
        Assert.NotNull((await next.BeginAsync(Carry(), default)).Move);
        stopped.SetResult();
        await record.DisposeAsync();
    }

    /// <summary>
    /// 🔴 FOUNDATION1f-iv part b: a dead loop's driven move is taken up at each phase it can stand in, and ended by it: before the open, the line
    /// stays on its old part (<c>not-opened</c>); the open asked and no answer recorded, held <c>unrecorded</c> and closed; opened or delivering with
    /// no word <c>sent</c>, held <c>undelivered</c> and closed, its word left waiting on the new part for its own go-on. None stands to hold the
    /// line's next carry-on.
    /// </summary>
    [Theory]
    [InlineData("old-ended")]
    [InlineData("opening")]
    [InlineData("opened")]
    [InlineData("delivering")]
    public async Task A_dead_loop_s_driven_move_is_taken_up_at_the_phase_it_stood_in_and_frees_the_line(string phase)
    {
        var dying = Store("run-dying");
        var decided = dying.Decide(Carry());
        var claim = decided.Claim!;
        _said.Wait("s2", "w1");
        if (phase != "old-ended") dying.Opening(claim, MoveWords.DrivenKey());
        if (phase is "opened" or "delivering") dying.Opened(claim, "s2", [new MoveRecordWord("w1", "w1", "opens-s1/w1", 0)]);
        if (phase == "delivering")
        {
            dying.Advance(claim, MoveRecordPhase.Delivering);
            dying.MarkEffect(claim, MoveEffects.Prompt);
        }

        _dead.Add("run-dying");
        var next = Holder("run-next");
        Assert.NotNull(await next.LookAsync(default));

        var move = MoveFrom("s1")!;
        Assert.False(move.Stands, move.PhaseWord);
        Assert.Equal(phase == "old-ended" ? ConversationMoves.NotOpened : null, move.FellBackCode);
        if (phase is "opened" or "delivering") Assert.Equal(["w1"], _said.Of("s2"));
        Assert.NotNull((await next.BeginAsync(Carry(), default)).Move);
    }

    /// <summary>🔴 A carry-on that hands no word closes <c>delivered</c> on its prompt's outcome: a move of no word.</summary>
    [Fact]
    public async Task A_carry_on_of_no_word_closes_delivered_on_its_effects_outcome()
    {
        var holder = Holder();
        var begun = await holder.BeginAsync(Carry(HandOverWhy.Retry), default);
        await using var record = new CarryOnRecord(begun.Move!);
        Assert.Null(record.Opening(MoveWords.DrivenKey()));
        Assert.Null(await record.FenceAsync("s2", []));

        record.Written();
        await record.DisposeAsync();

        var move = MoveFrom("s1")!;
        Assert.Equal(("delivered", "retry", "s2", MoveWordDispositions.Sent), (move.PhaseWord, move.Why, move.Destination, move.Effect!.Outcome));
        Assert.Empty(Store("run-probe").WordsOf(move.Id));
    }

    /// <summary>
    /// 🔴 A start that continues nothing keeps no move; every why a start continues by does, by its own code, the quest's hand-over's among them
    /// (FOUNDATION1f-v part c: a handed start whose hand-over left no move, an agent's ask taken at its turn's end or an older build's take, is
    /// decided as any carry-on is).
    /// </summary>
    [Fact]
    public void A_start_that_continues_nothing_decides_no_move_and_every_other_why_decides_one()
    {
        Assert.Null(CarryOnRecord.Decision(null, "claude-code", null));

        foreach (var why in new[] { HandOverWhy.CarryOn, HandOverWhy.Retry, HandOverWhy.Limit, HandOverWhy.Curve, HandOverWhy.Press, HandOverWhy.Agent })
        {
            var decision = CarryOnRecord.Decision(new ContinuedFrom("s1", why, HandOverBy.Daoris), "claude-code", null)!;
            Assert.Equal(("s1", MoveExecutors.Driven, why, true), (decision.Left, decision.Executor, decision.Why, decision.FromEnded));
        }
    }

    /// <summary>
    /// 🔴 FOUNDATION1f-v part c (§3.3: a hand-over is one move from ask to delivery): the carry-on the quest's hand-over starts goes on from the
    /// hand-over's own move, decided at the ask and left at <c>old-ended</c> by the look's take, never from a second one; it is written in the
    /// carry-on's order from there and closes <c>delivered</c>, the one row, its why, asker, agent and account the ask's.
    /// </summary>
    [Fact]
    public async Task The_carry_on_a_hand_over_starts_goes_on_from_the_hand_overs_own_move_and_writes_no_second()
    {
        var holder = Holder();
        var asked = await holder.BeginAsync(HandOver(), default);
        Assert.Equal("decided", MoveFrom("s1")!.PhaseWord);
        Assert.Null(asked.Move!.Quiescing());
        Assert.Null(asked.Move.OldEnded());

        // The carry-on's own decision finds the move standing, and goes on from it, claimed by this run.
        var again = await holder.BeginAsync(Carry(HandOverWhy.Press), default);
        Assert.Null(again.Move);
        await using var record = CarryOnRecord.Of(holder, again, handedFrom: true, "codex-acp")!;
        Assert.Null(record.OldEnded());
        Assert.Null(record.Opening(MoveWords.DrivenKey()));
        Assert.Null(await record.FenceAsync("s2", []));
        record.Written();
        await record.DisposeAsync();

        var move = Assert.Single(Store("run-probe").Of("s1"));
        Assert.Equal(asked.Move.Claim.Move, move.Id);
        Assert.Equal(("delivered", "run-loop", HandOverWhy.Press, HandOverBy.Person, "codex-acp", "work"), (move.PhaseWord, move.ClaimRun, move.Why, move.By, move.Agent, move.Account));
        Assert.Equal("s2", move.Destination);
    }

    /// <summary>
    /// 🔴 A standing move that is not the hand-over's own is not continued: a start that did not come of the hand-over, a move another run holds, one
    /// naming another agent, or one past its open holds the carry-on as before, so no two carry-ons share a move.
    /// </summary>
    [Fact]
    public async Task A_standing_move_that_is_not_the_hand_overs_own_is_not_continued()
    {
        var holder = Holder();
        await holder.BeginAsync(HandOver(), default);
        var standing = await holder.BeginAsync(Carry(HandOverWhy.Press), default);

        Assert.Null(CarryOnRecord.Of(holder, standing, handedFrom: false, "codex-acp"));
        Assert.Null(CarryOnRecord.Of(holder, standing, handedFrom: true, "claude-code"));
        Assert.Null(CarryOnRecord.Of(Holder("run-other"), standing, handedFrom: true, "codex-acp"));

        var opened = Store("run-loop");
        var claim = new MoveClaim(standing.Standing!.Id, "run-loop", standing.Standing.ClaimGeneration);
        opened.Advance(claim, MoveRecordPhase.OldEnded);
        opened.Opening(claim, "drive-move/x");
        var past = await holder.BeginAsync(Carry(HandOverWhy.Press), default);
        Assert.Null(CarryOnRecord.Of(holder, past, handedFrom: true, "codex-acp"));
    }

    /// <summary>
    /// 🔴 A carry-on whose open is refused goes no further: its record is ended by its phase, the open asked and no record answered, held
    /// <c>unrecorded</c> naming no word and closed, so the line's next carry-on is not held by it.
    /// </summary>
    [Fact]
    public async Task A_refused_open_closes_the_record_by_its_phase()
    {
        var holder = Holder();
        var begun = await holder.BeginAsync(Carry(), default);
        await using (var record = new CarryOnRecord(begun.Move!))
        {
            Assert.Null(record.Opening(MoveWords.DrivenKey()));
            await record.EndAsync();
        }

        var move = MoveFrom("s1")!;
        Assert.Equal(("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved), (move.PhaseWord, move.HeldWhy, move.ClosedWhy));
        var next = await Holder().BeginAsync(Carry(), default);
        Assert.NotNull(next.Move);
    }

    /// <summary>
    /// 🔴 A carry-on a run leaves by any way out is ended by its phase as it is let go, never left standing: before its open it falls back
    /// <c>not-opened</c> with the session still its line's head; past its fence, its hand refused, its prompt is <c>unsent</c> and the move
    /// held <c>undelivered</c> naming no word and closed, the word that waits on the new part never attempted.
    /// </summary>
    [Fact]
    public async Task A_carry_on_let_go_before_its_open_or_after_its_fence_is_ended_by_its_phase()
    {
        var holder = Holder();
        var begun = await holder.BeginAsync(Carry(), default);
        await using (new CarryOnRecord(begun.Move!)) { }
        var early = MoveFrom("s1")!;
        Assert.Equal(("fell-back", ConversationMoves.NotOpened), (early.PhaseWord, early.FellBackCode));

        var again = await holder.BeginAsync(Carry(), default);
        _said.Wait("s2", "w1");
        await using (var record = new CarryOnRecord(again.Move!))
        {
            Assert.Null(record.Opening(MoveWords.DrivenKey()));
            Assert.Null(await record.FenceAsync("s2", [Moved(1)]));
        }

        var late = MoveFrom("s1")!;
        Assert.Equal(("closed", MoveHoldWhy.Undelivered, ConversationMoves.Unsent),(late.PhaseWord, late.HeldWhy, late.Effect!.Outcome));        Assert.Equal(["w1"], _said.Of("s2"));
    }

    /// <summary>
    /// 🔴 FOUNDATION1f-iv part b: the take of the moved words follows the write of the prompt that carries them, and the take is what keeps the
    /// person's that reopened the work on the ask the instruction reads. The instruction is therefore handed those words as the take will keep them:
    /// the person's reopening words only, once each, in the ask's order; never Daoris's own or an answer, which the service's take keeps nowhere,
    /// and nothing where the ask could not be read.
    /// </summary>
    [Fact]
    public void The_instruction_is_handed_the_persons_moved_words_as_the_take_will_keep_them_on_the_ask()
    {
        var at = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var ask = new AskWords("a1", [new AskWordView(AskWordView.Asked, "Build it.", at.AddHours(-1))]);
        var person = new SaidWordView("w1", "Also log the port.", at, [], true);
        var daoris = person with { Id = "w2", From = DaorisWords.From };
        var answer = person with { Id = "w3", Reopens = false };

        var handed = ask.WithMoved([person, daoris, answer], "s2", "q1");

        Assert.Equal(["Build it.", "Also log the port."], handed.Said!.Select(word => word.Text));
        Assert.Equal((AskWordView.Reopened, "s2", "q1"), (handed.Said![1].Kind, handed.Said[1].Session, handed.Said[1].Quest));
        Assert.Equal(2, handed.WithMoved([person], "s2", "q1").Said!.Count);
        Assert.Null(AskWords.Unread("a1").WithMoved([person], "s2", "q1").Said);
    }

    /// <summary>
    /// 🔴 A move already standing for the record holds the carry-on: the store answers that move, no second is written and no record is made
    /// for the carry-on, whose caller holds its start; a record that is the destination of a standing move is held alike.
    /// </summary>
    [Fact]
    public async Task A_standing_move_on_the_record_holds_the_carry_on_and_no_second_move_is_written()
    {
        var other = Store("run-other");
        var standing = other.Decide(new MoveDecision("s1", MoveExecutors.Driven, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris) { FromEnded = true });
        other.Opening(standing.Claim!, "drive-move/other");

        var begun = await Holder().BeginAsync(Carry(), default);

        Assert.Null(begun.Move);
        Assert.Equal(standing.Move.Id, begun.Standing!.Id);
        Assert.Single(Store("run-probe").Of("s1"));
        Assert.Equal("opening", Store("run-probe").Read(standing.Move.Id)!.PhaseWord);

        // The record that is a standing move's destination takes no move of its own.
        var into = other.Decide(new MoveDecision("s0", MoveExecutors.Driven, MoveHows.Brief, HandOverWhy.CarryOn, HandOverBy.Daoris) { FromEnded = true });
        other.Opening(into.Claim!, "drive-move/into");
        other.Opened(into.Claim!, "s5", []);
        var held = await Holder().BeginAsync(Carry() with { Left = "s5" }, default);
        Assert.Null(held.Move);
        Assert.Empty(Store("run-probe").Of("s5"));
    }
}
