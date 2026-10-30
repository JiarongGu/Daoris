using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-i part 3 (D212; the foundation design §3.3, §13.4 step 4, §13.5's downgrade table, §19.4): the moved words' <c>sent</c>, read by
/// nothing. Each word's delivery evidence is written in one transaction as its door's write returns and before its take, and the move closes
/// <c>delivered</c> after the take and only once every word it moved has <c>sent</c>. A crash at <c>delivering</c> is read by <c>sent</c> alone:
/// a word with it is taken, taken off where it still waits; one gone without it is <i>left</i>; one waiting, attempted and without it, is
/// uncertain, settled and said; and the move is held naming its <i>left</i> and uncertain words and its begun effect, never delivered again.
/// </summary>
/// <remarks>
/// Every case has its own scratch home under <c>_fixtures/</c>; the destination's <c>said</c> is <see cref="MoveSaidStandIn"/>, which keeps no
/// session event at all, so no reading here can be made from one: a <c>User</c> event is the display's, never delivery's evidence. One process
/// is one run to the lease's test, so a run's death is the liveness each case hands in. Nothing here starts a process.
/// </remarks>
[Collection("agent operation ports")]
public sealed class MoveDeliveryTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "move-delivery", Guid.NewGuid().ToString("N")[..8]);

    private readonly HashSet<string> _dead = new(StringComparer.Ordinal);

    private readonly MoveSaidStandIn _said = new();

    public MoveDeliveryTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private MoveStore Store(string run) => new(_home, () => StoreSettled.Yes, each => !_dead.Contains(each.Run), run, StoreProcess.Shell);

    private MoveDelivery Delivery(MoveStore store) => new(store, _said);

    private static string Transfer(int n) => $"opens-s1/w{n}";

    /// <summary>
    /// A move of <c>s1</c> opened onto <c>s2</c> with <paramref name="words"/> words, each waiting there as the open moved it, at
    /// <c>delivering</c>, and its prompt marked where <paramref name="marked"/>.
    /// </summary>
    private MoveClaim Delivering(MoveStore store, int words = 2, bool marked = true, MoveRecordPhase at = MoveRecordPhase.Delivering)
    {
        var claim = store.Decide(new MoveDecision("s1", MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris) { FromEnded = true }).Claim!;
        store.Opening(claim, "opens-s1");
        store.Opened(claim, "s2", [.. Enumerable.Range(1, words).Select(n => new MoveRecordWord($"w{n}", null, Transfer(n), n - 1))]);
        for (var n = 1; n <= words; n++) _said.Wait("s2", $"w{n}");
        if (at != MoveRecordPhase.Delivering) return claim;
        store.Advance(claim, MoveRecordPhase.Delivering);
        if (marked) store.MarkEffect(claim, MoveEffects.Prompt);
        return claim;
    }

    /// <summary>The executor's run died; a shell of its own run takes the move up, as the come-up's take-up does.</summary>
    private (MoveStore Store, MoveClaim Claim) TakenUp(MoveClaim claim)
    {
        _dead.Add(claim.Run);
        var taker = Store("run-taker");
        var taken = taker.Take(claim.Move, claim.Generation);
        return (taker, taken.Claim!);
    }

    private static IReadOnlyList<string> Words(IReadOnlyList<MoveRecordWord> words) => [.. words.Select(word => word.Word)];

    // ——— The live order: `sent` as the write returns, then the take, then `delivered` (§13.4 step 4, §19.4).

    [Fact]
    public async Task Sent_is_written_as_the_door_s_write_returns_and_before_the_take_and_delivered_only_after_the_take()
    {
        var store = Store("run-a");
        var claim = Delivering(store);
        _said.Attempt("s2", "w1", "w2");
        var atTheTake = new List<string>();
        _said.OnTake = _ => atTheTake.Add(
            $"{string.Join(",", store.WordsOf(claim.Move).Select(word => word.Disposition))} at {store.Read(claim.Move)!.PhaseWord}");

        var delivered = await Delivery(store).WroteAsync(claim, [Transfer(1), Transfer(2)]);

        Assert.Equal(["sent,sent at delivering"], atTheTake);
        Assert.Equal(MoveDeliveryOutcome.Delivered, delivered.Outcome);
        Assert.Equal(MoveRecordPhase.Delivered, store.Read(claim.Move)!.Phase);
        Assert.Equal(["w1", "w2"], Words(delivered.Taken));

        // At `delivered` no moved word waits on the destination, since the take precedes it (§13.5's `delivered` row).
        Assert.Empty(_said.Of("s2"));
        Assert.Equal("sent", store.Read(claim.Move)!.Effect!.Outcome);
    }

    [Fact]
    public async Task Delivered_is_written_only_once_every_word_the_move_moved_has_sent()
    {
        var store = Store("run-a");
        var claim = Delivering(store, words: 3);

        // The record named two of the three attempted: the write carried those two, and the third waits, never attempted.
        _said.Attempt("s2", "w1", "w2");
        var first = await Delivery(store).WroteAsync(claim, [Transfer(1), Transfer(2)]);

        Assert.Equal(MoveDeliveryOutcome.GoesOn, first.Outcome);
        Assert.Equal(MoveRecordPhase.Delivering, store.Read(claim.Move)!.Phase);
        Assert.Equal(["w3"], Words(first.Waiting));
        Assert.Throws<MoveRefusedException>(() => store.Advance(claim, MoveRecordPhase.Delivered));
        Assert.Equal(["w3"], _said.Of("s2"));

        store.MarkEffect(claim, MoveEffects.Prompt);
        _said.Attempt("s2", "w3");
        var second = await Delivery(store).WroteAsync(claim, [Transfer(3)]);

        Assert.Equal(MoveDeliveryOutcome.Delivered, second.Outcome);
        Assert.Equal(["w1", "w2", "w3"], Words(second.Taken));
    }

    [Fact]
    public async Task A_sent_whose_transaction_failed_reads_its_word_left_once_it_is_taken_and_the_move_is_held_never_closed_delivered()
    {
        var store = Store("run-a");
        var claim = Delivering(store);
        _said.Attempt("s2", "w1", "w2");
        using (var planted = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes }))
        {
            planted.Write("probe", write => write.Execute(
                "CREATE TRIGGER refuse_sent BEFORE UPDATE OF disposition ON move_words WHEN NEW.disposition = 'sent' BEGIN SELECT RAISE(ABORT, 'the disk refused'); END"));
        }

        var read = await Delivery(store).WroteAsync(claim, [Transfer(1), Transfer(2)]);

        // The write returned and the turn took the words; the move holds no `sent` for them, so they are left, the conservative side.
        Assert.Empty(_said.Of("s2"));
        Assert.Equal(MoveDeliveryOutcome.Held, read.Outcome);
        Assert.Equal(["w1", "w2"], Words(read.Left));
        Assert.Equal(MoveEffects.Prompt, read.Begun!.What);
        var move = store.Read(claim.Move)!;
        Assert.Equal(MoveHoldWhy.Undelivered, move.HeldWhy);
        Assert.False(move.Moments.ContainsKey(MoveRecordPhase.Delivered));
        Assert.Equal([MoveWordDispositions.Left, MoveWordDispositions.Left], store.WordsOf(claim.Move).Select(word => word.Disposition));
    }

    // ——— A crash at `delivering`, read by `sent` alone (§13.4 step 4, §13.5's `delivering` row, §19.4).

    [Fact]
    public async Task A_crash_at_delivering_is_read_by_sent_alone_each_word_taken_left_or_uncertain_and_the_move_held_naming_them_and_its_begun_effect()
    {
        var store = Store("run-dead");
        var claim = Delivering(store, words: 4);

        // The first write carried w1 and returned: `sent`, and the crash came before its take. The second carried w2 to w4, attempted,
        // and the crash came before it returned.
        _said.Attempt("s2", "w1");
        Assert.True(Delivery(store).Wrote(claim, [Transfer(1)]));
        store.MarkEffect(claim, MoveEffects.Prompt);
        _said.Attempt("s2", "w2", "w3", "w4");

        // Meanwhile the person took w3 back, and an older build's come-up settled w4 as uncertain: each left `said` by the same statement.
        _said.Gone("s2", "w3");
        _said.Gone("s2", "w4");
        var (taker, taken) = TakenUp(claim);

        var read = await Delivery(taker).ReadAsync(taken);

        Assert.Equal(MoveDeliveryOutcome.Held, read.Outcome);
        Assert.Equal(["w1"], Words(read.Taken));
        Assert.Equal(["w2"], Words(read.Uncertain));
        Assert.Equal(["w3", "w4"], Words(read.Left));
        Assert.Equal((MoveEffects.Prompt, (string?)null), (read.Begun!.What, read.Begun.Outcome));
        Assert.Equal(
            [MoveWordDispositions.Sent, MoveWordDispositions.Uncertain, MoveWordDispositions.Left, MoveWordDispositions.Left],
            taker.WordsOf(claim.Move).Select(word => word.Disposition));
        Assert.Equal(MoveHoldWhy.Undelivered, read.Held!.HeldWhy);

        // The sent word taken off as taken, the uncertain one settled; nothing else asked of the destination and nothing delivered again.
        Assert.Equal(["take s2 w1", "settle s2 w2"], _said.Calls);
        Assert.Empty(_said.Of("s2"));
        Assert.False(taker.Read(claim.Move)!.Moments.ContainsKey(MoveRecordPhase.Delivered));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task After_sent_and_before_the_take_the_come_up_takes_the_words_off_as_taken_and_closes_the_move_delivered_an_older_take_back_between_or_not(bool olderComeUp)
    {
        var store = Store("run-dead");
        var claim = Delivering(store);
        _said.Attempt("s2", "w1", "w2");
        Delivery(store).Wrote(claim, [Transfer(1), Transfer(2)]);

        // An older build's come-up between: it settles an attempted word as uncertain, taking it back.
        if (olderComeUp) _said.Gone("s2", "w1");
        var (taker, taken) = TakenUp(claim);

        var read = await Delivery(taker).ReadAsync(taken);

        Assert.Equal(MoveDeliveryOutcome.Delivered, read.Outcome);
        Assert.Equal(["w1", "w2"], Words(read.Taken));
        Assert.Equal([olderComeUp ? "take s2 w2" : "take s2 w1,w2"], _said.Calls);
        Assert.Empty(_said.Of("s2"));
    }

    [Fact]
    public async Task After_the_take_and_before_delivered_the_move_closes_delivered_by_sent_and_nothing_is_asked_of_the_destination()
    {
        var store = Store("run-dead");
        var claim = Delivering(store);
        _said.Attempt("s2", "w1", "w2");
        Delivery(store).Wrote(claim, [Transfer(1), Transfer(2)]);
        _said.Gone("s2", "w1");
        _said.Gone("s2", "w2");
        var (taker, taken) = TakenUp(claim);

        var read = await Delivery(taker).ReadAsync(taken);

        Assert.Equal(MoveDeliveryOutcome.Delivered, read.Outcome);
        Assert.Empty(_said.Calls);
        Assert.Equal(MoveRecordPhase.Delivered, taker.Read(claim.Move)!.Phase);
    }

    [Theory]
    [InlineData("the person's take-back")]
    [InlineData("an older come-up's uncertain take-back")]
    public async Task A_crash_at_delivering_before_the_write_its_word_then_taken_off_by_another_door_is_left_at_the_return_and_never_delivered_again(string door)
    {
        var store = Store("run-dead");
        var claim = Delivering(store, words: 1);
        _said.Attempt("s2", "w1");
        _said.Gone("s2", "w1");
        var (taker, taken) = TakenUp(claim);

        var read = await Delivery(taker).ReadAsync(taken);

        Assert.True(read.Outcome == MoveDeliveryOutcome.Held, door);
        Assert.Equal(["w1"], Words(read.Left));
        Assert.Empty(read.Taken);
        Assert.Empty(_said.Calls);
        Assert.Equal(MoveWordDispositions.Left, taker.WordsOf(claim.Move)[0].Disposition);
    }

    // ——— §13.5's `opened` row, a destination that cannot be read, a move of no word.

    [Fact]
    public async Task At_opened_a_moved_word_an_older_build_s_turn_took_is_left_and_the_move_held_and_a_move_whose_words_all_wait_goes_on()
    {
        var store = Store("run-dead");
        var claim = Delivering(store, at: MoveRecordPhase.Opened);
        _said.Gone("s2", "w1");
        var (taker, taken) = TakenUp(claim);

        var read = await Delivery(taker).ReadAsync(taken);

        Assert.Equal(MoveDeliveryOutcome.Held, read.Outcome);
        Assert.Equal(["w1"], Words(read.Left));
        Assert.Equal(["w2"], Words(read.Waiting));
        Assert.Null(read.Begun);

        var other = Store("run-other");
        var waiting = other.Decide(new MoveDecision("s3", MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris) { FromEnded = true }).Claim!;
        other.Opening(waiting, "opens-s3");
        other.Opened(waiting, "s4", [new("v1", null, "opens-s3/v1", 0)]);
        _said.Wait("s4", "v1");

        var goes = await Delivery(other).ReadAsync(waiting);

        Assert.Equal(MoveDeliveryOutcome.GoesOn, goes.Outcome);
        Assert.Equal(["v1"], Words(goes.Waiting));
        Assert.Equal(MoveRecordPhase.Opened, other.Read(waiting.Move)!.Phase);
    }

    [Fact]
    public async Task A_move_whose_destination_s_said_cannot_be_read_is_read_again_later_with_nothing_written()
    {
        var store = Store("run-dead");
        var claim = Delivering(store);
        _said.Attempt("s2", "w1");
        _said.Unreadable.Add("s2");
        var (taker, taken) = TakenUp(claim);

        var read = await Delivery(taker).ReadAsync(taken);

        Assert.Equal(MoveDeliveryOutcome.Unread, read.Outcome);
        Assert.Equal(MoveRecordPhase.Delivering, taker.Read(claim.Move)!.Phase);
        Assert.All(taker.WordsOf(claim.Move), word => Assert.Null(word.Disposition));
    }

    [Fact]
    public async Task A_move_of_no_word_is_delivered_by_its_write_s_return_held_where_its_effect_was_begun_and_never_recorded_and_goes_on_where_none_was()
    {
        var store = Store("run-a");
        var written = Delivering(store, words: 0);
        Assert.Equal(MoveDeliveryOutcome.Delivered, (await Delivery(store).WroteAsync(written, [])).Outcome);

        var cut = Store("run-dead");
        var begun = cut.Decide(new MoveDecision("s3", MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris) { FromEnded = true }).Claim!;
        cut.Opening(begun, "opens-s3");
        cut.Opened(begun, "s4", []);
        cut.Advance(begun, MoveRecordPhase.Delivering);
        cut.MarkEffect(begun, MoveEffects.Prompt);
        var (taker, taken) = TakenUp(begun);

        var held = await Delivery(taker).ReadAsync(taken);

        // Its begun effect alone: held, the hold's note the caller's to say, and closed at once by §3.3's one rule, since it waits on no `said`.
        Assert.Equal(MoveDeliveryOutcome.Held, held.Outcome);
        Assert.Equal(MoveHoldWhy.Unrecorded, held.Held!.HeldWhy);
        Assert.Equal(((MoveRecordPhase?)MoveRecordPhase.Closed, (string?)MoveCloseWhy.Resolved), (held.Move.Phase, held.Move.ClosedWhy));

        var fresh = Store("run-fresh");
        var unmarked = fresh.Decide(new MoveDecision("s5", MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris) { FromEnded = true }).Claim!;
        fresh.Opening(unmarked, "opens-s5");
        fresh.Opened(unmarked, "s6", []);
        fresh.Advance(unmarked, MoveRecordPhase.Delivering);
        Assert.Equal(MoveDeliveryOutcome.GoesOn, (await Delivery(fresh).ReadAsync(unmarked)).Outcome);
    }

    // ——— The line's head, read before anything is delivered (FOUNDATION1f-ii part b; §13.4 step 5, §19.3).

    /// <summary>
    /// 🔴 Where an older build has continued the destination, nothing is delivered there: the move is held <c>superseded</c>, naming the person's
    /// own waiting words for the person to take back and say at the head, the attempted one uncertain, settled and said, and another author's
    /// word kept on the destination as a claim, named by nothing.
    /// </summary>
    [Fact]
    public async Task A_head_past_the_destination_holds_the_move_superseded_and_names_only_the_persons_own_waiting_words()
    {
        var store = Store("run-a");
        var claim = Delivering(store, words: 3);
        _said.Attempt("s2", "w2");
        _said.Claim("s2", "w3");
        _said.Heads["s2"] = "s3";

        var read = await Delivery(store).ReadAsync(claim);

        Assert.Equal(MoveDeliveryOutcome.Held, read.Outcome);
        Assert.Equal(MoveHoldWhy.Superseded, read.Held!.HeldWhy);
        var move = store.Read(claim.Move)!;
        Assert.Equal(MoveRecordPhase.Held, move.Phase);
        Assert.Equal(
            [("w1", MoveWordDispositions.Held), ("w2", MoveWordDispositions.Uncertain), ("w3", null)],
            store.WordsOf(claim.Move).Select(word => (word.Word, word.Disposition)));
        Assert.Equal(["w1"], Words(read.Waiting.Where(word => word.Word != "w3").ToList()));

        // Nothing was written to the destination or taken off it as delivered; the attempted word was settled, and the person's and the claim wait.
        Assert.Equal(["settle s2 w2"], _said.Calls);
        Assert.Equal(["w1", "w3"], _said.Of("s2"));
        Assert.Null(store.Read(claim.Move)!.Effect!.Outcome);
    }

    /// <summary>
    /// 🔴 The hold stands while a word it names waits and closes <c>resolved</c> by §3.3's one rule once the person takes it back, the claim
    /// left on the destination keeping it open no longer.
    /// </summary>
    [Fact]
    public async Task The_superseded_hold_closes_resolved_once_the_persons_words_are_taken_back()
    {
        var store = Store("run-a");
        var claim = Delivering(store, words: 2);
        _said.Claim("s2", "w2");
        _said.Heads["s2"] = "s3";
        await Delivery(store).ReadAsync(claim);
        Assert.Equal(MoveRecordPhase.Held, store.Read(claim.Move)!.Phase);

        _said.Gone("s2", "w1");
        var read = await new MoveResolve(store, _said).ReadAsync(claim);

        Assert.Equal(("closed", MoveCloseWhy.Resolved), (read.PhaseWord, read.ClosedWhy));
        Assert.Equal(["w2"], _said.Of("s2"));
    }

    /// <summary>
    /// 🔴 A word the move wrote (<c>sent</c>) that still waits is taken off as taken before the hold, since its agent was written; the word not
    /// yet written is the one the hold names.
    /// </summary>
    [Fact]
    public async Task A_head_past_the_destination_takes_a_sent_word_off_as_taken_and_holds_the_rest()
    {
        var store = Store("run-a");
        var claim = Delivering(store, words: 2);
        _said.Attempt("s2", "w1");
        store.Sent(claim, [Transfer(1)]);
        _said.Heads["s2"] = "s3";

        var read = await Delivery(store).ReadAsync(claim);

        Assert.Equal(MoveDeliveryOutcome.Held, read.Outcome);
        Assert.Equal(["take s2 w1"], _said.Calls);
        Assert.Equal(["w2"], _said.Of("s2"));
        Assert.Equal(
            [("w1", MoveWordDispositions.Sent), ("w2", MoveWordDispositions.Held)],
            store.WordsOf(claim.Move).Select(word => (word.Word, word.Disposition)));
    }

    /// <summary>
    /// Where every word the move moved has <c>sent</c>, nothing is left to deliver, so a head past the destination holds nothing: the move
    /// closes <c>delivered</c> as ever. A destination that is the head reads as before (a guard: the case already holds).
    /// </summary>
    [Fact]
    public async Task A_head_past_a_destination_whose_words_all_sent_still_closes_delivered_and_the_head_itself_reads_as_before()
    {
        var store = Store("run-a");
        var claim = Delivering(store, words: 1);
        _said.Attempt("s2", "w1");
        store.Sent(claim, [Transfer(1)]);
        _said.Heads["s2"] = "s3";

        Assert.Equal(MoveDeliveryOutcome.Delivered, (await Delivery(store).ReadAsync(claim)).Outcome);

        var other = Store("run-b");
        var fresh = other.Decide(new MoveDecision("s5", MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris) { FromEnded = true }).Claim!;
        other.Opening(fresh, "opens-s5");
        other.Opened(fresh, "s6", [new MoveRecordWord("v1", null, "opens-s5/v1", 0)]);
        _said.Wait("s6", "v1");
        other.Advance(fresh, MoveRecordPhase.Delivering);
        Assert.Equal(MoveDeliveryOutcome.GoesOn, (await Delivery(other).ReadAsync(fresh)).Outcome);
    }
}

/// <summary>
/// A move's destination's <c>said</c>, standing in (FOUNDATION1f-i part 3): each record's waiting words in order with their attempt marks,
/// the take of words a session took and the settlement of uncertain ones, each call kept in order. It keeps no session event: the move's
/// readings read none. A record set unreadable answers that it cannot be told; one set keeping leaves its words through a take and a settle.
/// </summary>
internal sealed class MoveSaidStandIn : IMoveSaid
{
    private readonly object _gate = new();

    private readonly Dictionary<string, List<MoveWaiting>> _records = new(StringComparer.Ordinal);

    /// <summary>Each take and settle asked, as <c>take s2 w1,w2</c>.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>The records whose <c>said</c> cannot be read now.</summary>
    public HashSet<string> Unreadable { get; } = new(StringComparer.Ordinal);

    /// <summary>The records whose take and settle answer and leave their words, as a host that did not take them.</summary>
    public HashSet<string> Keeps { get; } = new(StringComparer.Ordinal);

    /// <summary>Called with the record before each take, still holding its words: what a case reads at that moment.</summary>
    public Action<string>? OnTake { get; set; }

    /// <summary>The head past a record, where an older build continued it (FOUNDATION1f-ii part b); a record named here is read so, others as the head.</summary>
    public Dictionary<string, string> Heads { get; } = new(StringComparer.Ordinal);

    Task<string?> IMoveSaid.HeadPastAsync(string record, CancellationToken ct) => Task.FromResult(Heads.GetValueOrDefault(record));

    /// <summary>A waiting word that is another author's, a claim and never the person's to say (§19.3).</summary>
    public void Claim(string record, string word)
    {
        lock (_gate)
        {
            var held = _records[record];
            var at = held.FindIndex(each => each.Word == word);
            held[at] = held[at] with { Person = false };
        }
    }

    /// <summary>A word waiting on a record, after those already there.</summary>
    public MoveSaidStandIn Wait(string record, string word)
    {
        lock (_gate)
        {
            if (!_records.TryGetValue(record, out var words)) _records[record] = words = [];
            words.Add(new MoveWaiting(word, false));
        }

        return this;
    }

    /// <summary>These words marked attempted on a record, as a dispatcher marks them before the write.</summary>
    public void Attempt(string record, params string[] words)
    {
        lock (_gate)
        {
            var held = _records[record];
            for (var at = 0; at < held.Count; at++)
            {
                if (words.Contains(held[at].Word)) held[at] = held[at] with { Attempted = true };
            }
        }
    }

    /// <summary>A word taken off a record by another door than the move's: the person's take-back, an older come-up's, an older build's turn.</summary>
    public void Gone(string record, string word)
    {
        lock (_gate) _records[record].RemoveAll(each => each.Word == word);
    }

    /// <summary>The words waiting on a record now, by id.</summary>
    public IReadOnlyList<string> Of(string record)
    {
        lock (_gate) return [.. (_records.GetValueOrDefault(record) ?? []).Select(word => word.Word)];
    }

    public Task<IReadOnlyList<MoveWaiting>?> WaitingAsync(string record, CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<MoveWaiting>?>(
                Unreadable.Contains(record) ? null : [.. _records.GetValueOrDefault(record) ?? []]);
        }
    }

    public Task TakeAsync(string record, IReadOnlyList<string> words, CancellationToken ct)
    {
        OnTake?.Invoke(record);
        Leave("take", record, words);
        return Task.CompletedTask;
    }

    public Task SettleAsync(string record, IReadOnlyList<string> words, CancellationToken ct)
    {
        Leave("settle", record, words);
        return Task.CompletedTask;
    }

    private void Leave(string how, string record, IReadOnlyList<string> words)
    {
        lock (_gate)
        {
            Calls.Add($"{how} {record} {string.Join(",", words)}");
            if (!Keeps.Contains(record) && _records.TryGetValue(record, out var held)) held.RemoveAll(each => words.Contains(each.Word));
        }
    }
}
