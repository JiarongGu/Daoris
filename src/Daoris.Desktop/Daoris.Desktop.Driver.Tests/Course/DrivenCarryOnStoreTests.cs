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

    private MoveStore Store(string run) => new(_home, () => StoreSettled.Yes, _ => true, run, StoreProcess.Host);

    /// <summary>The driven holder of one run: the conversations' holder over the same store, handed the lease holder's scope.</summary>
    private ConversationMoves Holder(string run = "run-loop") =>
        new(_home, new ServiceClient("http://carry-on.test", null), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)),
            store: Store(run), said: _said, scope: MoveTakeUpScope.LeaseHolder);

    private static MoveDecision Carry(string why = HandOverWhy.CarryOn, bool handed = false) =>
        CarryOnRecord.Decision(new ContinuedFrom("s1", why, HandOverBy.Daoris), handedFrom: handed, agent: "claude-code", account: "account-2")!;

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

        // Today's take, then the word's `sent` and the reading that closes the move.
        await _said.TakeAsync("s2", ["w1"], default);
        await record.DeliveredAsync(["opens-s1/w1"]);

        var delivered = MoveFrom("s1")!;
        Assert.Equal(("delivered", MoveWordDispositions.Sent), (delivered.PhaseWord, delivered.Effect!.Outcome));
        Assert.Equal(MoveWordDispositions.Sent, Assert.Single(Store("run-probe").WordsOf(delivered.Id)).Disposition);
        Assert.Empty(_said.Of("s2"));
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

        await record.DeliveredAsync([]);

        var move = MoveFrom("s1")!;
        Assert.Equal(("delivered", "retry", "s2", MoveWordDispositions.Sent), (move.PhaseWord, move.Why, move.Destination, move.Effect!.Outcome));
        Assert.Empty(Store("run-probe").WordsOf(move.Id));
    }

    /// <summary>
    /// 🔴 A carry-on of a quest's hand-over keeps no record here (the hand-over is row f-v's), and a start that continues nothing keeps none; every
    /// other why a start continues by does, by its own code.
    /// </summary>
    [Fact]
    public void A_handed_carry_on_and_a_start_that_continues_nothing_decide_no_move()
    {
        var handed = new ContinuedFrom("s1", HandOverWhy.Press, HandOverBy.Person);
        Assert.Null(CarryOnRecord.Decision(handed, handedFrom: true, "claude-code", null));
        Assert.Null(CarryOnRecord.Decision(null, handedFrom: false, "claude-code", null));

        foreach (var why in new[] { HandOverWhy.CarryOn, HandOverWhy.Retry, HandOverWhy.Limit, HandOverWhy.Curve })
        {
            var decision = CarryOnRecord.Decision(new ContinuedFrom("s1", why, HandOverBy.Daoris), handedFrom: false, "claude-code", null)!;
            Assert.Equal(("s1", MoveExecutors.Driven, why, true), (decision.Left, decision.Executor, decision.Why, decision.FromEnded));
        }
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
