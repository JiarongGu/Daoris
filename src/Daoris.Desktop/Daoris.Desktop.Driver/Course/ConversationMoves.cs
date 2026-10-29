using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The conversations' move record (FOUNDATION1f-ii part a, D212; the foundation design §3.3, §13.4, §19.4): the store a shell's conversation
/// moves are written in, the door to their destinations' <c>said</c>, and the take-up of a dead shell's conversation moves, at the shell's
/// come-up and at each look. The live limit move writes its move here (<see cref="BeginAsync"/>); the curve and the hand-over are rows f-iii
/// and f-v's.
/// </summary>
/// <remarks>
/// <para><b>Nothing here sends a word.</b> A move this holder ends on its executor's failure, or takes up from a dead run, is read by its
/// words' <c>sent</c> and its destination's <c>said</c> alone (<see cref="MoveDelivery"/>), and ended so its records go on by their own
/// come-up as an older build's would: before its open was asked it falls back on its old part (<see cref="NotOpened"/>); with its open asked
/// and no answer recorded it is held as an effect never recorded; opened and not delivered it is held as undelivered, naming only what the
/// reading found <i>left</i> or uncertain. A word it moved that waits there never attempted goes on by its destination's own go-on, which
/// sends only words never attempted, so no word is sent twice by a move.</para>
/// <para><b>Read again.</b> A move whose destination could not be read is kept for the next look of any holder in this process, its claim
/// this run's; a move in flight is never read here, since its write may still be made.</para>
/// </remarks>
public sealed class ConversationMoves
{
    /// <summary>The fall-back's code of a move whose executor stopped before its open was asked: its line stays on its old part.</summary>
    public const string NotOpened = "not-opened";

    /// <summary>The outcome of a prompt its executor knows it never wrote: its hand was refused before the write.</summary>
    public const string Unsent = "unsent";

    /// <summary>The moves to read again at the next look, by the store's file and the move: their destinations could not be read.</summary>
    private static readonly ConcurrentDictionary<(string Store, long Move), byte> Again = new();

    private readonly ServiceClient _service;

    private readonly SessionEvents _events;

    private readonly SessionOutput? _output;

    private readonly MoveTakeUp _takeUp;

    /// <param name="home">The home whose move record this is.</param>
    /// <param name="service">The service whose ledger holds the records.</param>
    /// <param name="events">Where a record's notes are kept: a quiet move's, and a settled word's <c>lost</c> note.</param>
    /// <param name="output">The conversations' console, where a note is said beside its record; none says none there.</param>
    /// <param name="store">The move record; null for the home's, under this process's run.</param>
    /// <param name="clock">What time it is, for a phase's quiet; null for the machine's.</param>
    /// <param name="said">The destinations' <c>said</c>; null for the service's.</param>
    public ConversationMoves(
        string home, ServiceClient service, SessionEvents events, SessionOutput? output = null, MoveStore? store = null, TimeProvider? clock = null,
        IMoveSaid? said = null)
    {
        _service = service;
        _events = events;
        _output = output;
        Store = store ?? new MoveStore(home, clock: clock);
        Said = said ?? new MoveSaidDoor(service, events);
        Delivery = new MoveDelivery(Store, Said);
        Resolve = new MoveResolve(Store, Said);
        _takeUp = new MoveTakeUp(home, Store, MoveTakeUpScope.Conversations, imports: null, note: Note, clock: clock, said: Said);
    }

    /// <summary>The move record, under this process's run.</summary>
    public MoveStore Store { get; }

    /// <summary>The destinations' <c>said</c>.</summary>
    public IMoveSaid Said { get; }

    /// <summary>The moved words' delivery and its one reading.</summary>
    public MoveDelivery Delivery { get; }

    /// <summary>A hold's one rule.</summary>
    public MoveResolve Resolve { get; }

    /// <summary>
    /// A conversation's move decided (§13.4 step 1): the record's standing move read first as the course reads it (§3.1's <c>Never</c>), a hold
    /// under this run's claim closed <c>resolved</c> by the one rule where nothing it names waits, and this run's move past its write, its
    /// prompt's outcome recorded as the write returned, read by its delivery's one reading, which closes it <c>delivered</c> once every word it
    /// moved has <c>sent</c>; then the decision, claimed by this run. Where a move still stands for the record, the one it left or the one whose
    /// open made it, nothing is written and that move is answered.
    /// </summary>
    internal async Task<ConversationMoveBegun> BeginAsync(MoveDecision decision, CancellationToken ct)
    {
        try
        {
            var standing = await Resolve.StandingAsync(decision.Left, ct).ConfigureAwait(false);

            // A new part its limit refuses at its first turn is judged as that turn ends, which can be before its move's own reading.
            if (standing is { Phase: MoveRecordPhase.Delivering, Effect.Outcome: not null } past && string.Equals(past.ClaimRun, Store.Run, StringComparison.Ordinal))
            {
                await ReadAsync(new MoveClaim(past.Id, past.ClaimRun, past.ClaimGeneration), ct).ConfigureAwait(false);
                standing = await Resolve.StandingAsync(decision.Left, ct).ConfigureAwait(false);
            }

            if (standing is not null) return new(null, standing, null);
            var decided = Store.Decide(decision);
            return decided.Claim is { } claim ? new(new ConversationMove(this, claim), null, null) : new(null, decided.Move, null);
        }
        catch (Exception error) when (error is DriverException or SqliteException or IOException or UnauthorizedAccessException)
        {
            return new(null, null, error.Message);
        }
    }

    /// <summary>
    /// The shell's come-up (§3.3): its run recorded, another live shell on its home said once (refusing nothing), then the look. Never faults:
    /// a store that will not answer now is read again at the next look.
    /// </summary>
    /// <param name="say">Where another shell on the home is said; none logs it alone.</param>
    public async Task<MoveLook?> ComeUpAsync(Action<string>? say, CancellationToken ct)
    {
        try
        {
            return await _takeUp.ComeUpAsync(await StandsAsync(ct).ConfigureAwait(false), SweepAsync, say, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error, ct))
        {
            return null;
        }
    }

    /// <summary>
    /// One look (§3.3, §19.1 point 4): the moves this process keeps to read again; then every dead run's conversation move taken up once and
    /// ended by its phase; every hold of this run's read by the one rule; and a live run's move quiet ten minutes said once. Never faults.
    /// </summary>
    public async Task<MoveLook?> LookAsync(CancellationToken ct)
    {
        try
        {
            return await _takeUp.LookAsync(await StandsAsync(ct).ConfigureAwait(false), SweepAsync, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error, ct))
        {
            return null;
        }
    }

    /// <summary>
    /// What the take-up left to its caller, ended by phase: the moves kept to read again, and each dead run's move it took and did not end.
    /// </summary>
    private async Task SweepAsync(MoveLook look, CancellationToken ct)
    {
        foreach (var (store, id) in Again.Keys)
        {
            if (!string.Equals(store, Store.Location, StringComparison.OrdinalIgnoreCase)) continue;
            Again.TryRemove((store, id), out _);
            if (Store.Read(id) is { Stands: true } move && string.Equals(move.ClaimRun, Store.Run, StringComparison.Ordinal))
            {
                await Settle(() => ReadAsync(new MoveClaim(id, move.ClaimRun, move.ClaimGeneration), ct)).ConfigureAwait(false);
            }
        }

        // A dead run's move: whether its prompt was written is not known here, so its delivery is read by its evidence alone.
        foreach (var taken in look.Taken)
        {
            if (taken is { Claim: { } claim, Move: { } move }) await Settle(() => EndAsync(claim, move.Phase, unwritten: false, ct)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A move its executor goes no further with, or a dead run's the take-up left to its caller, ended by its phase (the remarks).
    /// <paramref name="unwritten"/> is its own executor's knowledge that its prompt was never written, its hand refused before the write: the
    /// prompt's outcome is then recorded so, and the hold names no word, since none it moved reached the agent, its attempted words left to the
    /// come-up's settlement as before. Otherwise its delivery is read by its words' <c>sent</c> and its destination's <c>said</c> alone.
    /// </summary>
    internal async Task EndAsync(MoveClaim claim, MoveRecordPhase? phase, bool unwritten, CancellationToken ct)
    {
        switch (phase)
        {
            case MoveRecordPhase.Decided or MoveRecordPhase.Quiescing or MoveRecordPhase.OldEnded:
                Store.FallBack(claim, NotOpened);
                return;
            case MoveRecordPhase.Opening:
                // The open asked and its answer not recorded: an effect never recorded, which names no word and so closes at its read.
                Store.Hold(claim, MoveHoldWhy.Unrecorded);
                await Resolve.ReadAsync(claim, ct).ConfigureAwait(false);
                return;
            case MoveRecordPhase.Held:
                await Resolve.ReadAsync(claim, ct).ConfigureAwait(false);
                return;
            case { } reads when MoveDelivery.Reads.Contains(reads) && unwritten:
                if (Store.Read(claim.Move)?.Effect is { Outcome: null }) Store.EffectEnded(claim, Unsent);
                Store.Hold(claim, MoveHoldWhy.Undelivered);
                await Resolve.ReadAsync(claim, ct).ConfigureAwait(false);
                return;
            case { } reads when MoveDelivery.Reads.Contains(reads):
                await ReadAsync(claim, ct).ConfigureAwait(false);
                return;
        }
    }

    /// <summary>
    /// The one reading of a move's delivery, finished (§13.4 step 4): delivered, held or ended as the reading says; a move whose words wait on
    /// its destination never attempted, nothing begun, held as undelivered naming none, so it closes and they go on by the destination's own
    /// go-on; one whose destination could not be read kept to read again.
    /// </summary>
    internal async Task<MoveDelivered> ReadAsync(MoveClaim claim, CancellationToken ct)
    {
        var read = await Delivery.ReadAsync(claim, ct).ConfigureAwait(false);
        switch (read.Outcome)
        {
            case MoveDeliveryOutcome.GoesOn when read.Move.Phase is { } phase && MoveDelivery.Reads.Contains(phase):
                Store.Hold(claim, MoveHoldWhy.Undelivered);
                return read with { Move = await Resolve.ReadAsync(claim, ct).ConfigureAwait(false), Outcome = MoveDeliveryOutcome.Held };
            case MoveDeliveryOutcome.Unread:
                Again[(Store.Location, claim.Move)] = 0;
                return read;
            default:
                return read;
        }
    }

    /// <summary>A move's settling, which a store that will not take it now, or a claim that moved, leaves for the next look.</summary>
    private static async Task Settle(Func<Task> settle)
    {
        try
        {
            await settle().ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or SqliteException)
        {
            // The store would not take it now, or another run holds it: the next look reads it again.
        }
    }

    /// <summary>Whether each record stands in the ledger, read once for the look; nothing can be told where the service does not answer.</summary>
    private async Task<Func<string, bool?>> StandsAsync(CancellationToken ct)
    {
        try
        {
            var ids = SessionRecords.Parse(await _service.SessionRecordsJsonAsync(ct).ConfigureAwait(false))
                .Select(record => record.Id).ToHashSet(StringComparer.Ordinal);
            return record => ids.Contains(record);
        }
        catch (Exception error) when (Unanswered(error, ct))
        {
            return _ => null;
        }
    }

    /// <summary>A line on a record, as a conversation's driver's line is kept: said on its console and kept in its record's events.</summary>
    private void Note(string session, Noted line)
    {
        _output?.Append(session, line.Note);
        _events.Keep(
            session,
            new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts.Count > 0 ? line.Parts : null },
            say: null);
    }

    private static bool Unanswered(Exception error, CancellationToken ct) =>
        error is HttpRequestException or DriverException or SqliteException or IOException or UnauthorizedAccessException
            or System.Text.Json.JsonException or ObjectDisposedException
        || (error is OperationCanceledException && !ct.IsCancellationRequested);
}

/// <summary>What a decision came to: the move this run claimed, the move standing for the record, or why the store would not write it.</summary>
internal sealed record ConversationMoveBegun(ConversationMove? Move, MoveRecord? Standing, string? Refused);

/// <summary>
/// One live conversation move under its executor's claim (FOUNDATION1f-ii part a): each phase written under the claim's check before what it
/// fences, answering the store's refusal where it would not take it, which stops the executor before that effect; null where it was written.
/// </summary>
internal sealed class ConversationMove(ConversationMoves moves, MoveClaim claim)
{
    private int _wired;

    /// <summary>The executor's claim.</summary>
    public MoveClaim Claim => claim;

    /// <summary>The old part ended, or there was none to end.</summary>
    public string? OldEnded() => Write(() => moves.Store.Advance(claim, MoveRecordPhase.OldEnded));

    /// <summary>The open asked under <paramref name="key"/>, the key its keyed open is sent with, so a retried open whose answer was lost reuses it.</summary>
    public string? Opening(string key) => Write(() => moves.Store.Opening(claim, key));

    /// <summary>The open's answer: the record it made and every word it moved, each by its id there, in the transfer's order.</summary>
    public string? Opened(string destination, IReadOnlyList<MovedWord> moved) =>
        Write(() => moves.Store.Opened(claim, destination, [.. moved.Select(word => new MoveRecordWord(word.Id, word.Key, word.Transfer, word.Order))]));

    /// <summary>The words going across: <c>delivering</c>, then the prompt's mark, before the hand.</summary>
    public string? Delivering() =>
        Write(() => moves.Store.Advance(claim, MoveRecordPhase.Delivering)) ?? Write(() => moves.Store.MarkEffect(claim, MoveEffects.Prompt));

    /// <summary>Whether the write's return was recorded (<see cref="Wire"/>).</summary>
    public bool Wired => Volatile.Read(ref _wired) == 1;

    /// <summary>
    /// As the door's write returns, once: each written word's <c>sent</c> and the prompt's outcome in one transaction (<see cref="MoveDelivery.Wrote"/>),
    /// before the take; a word whose <c>sent</c> failed reads <i>left</i>, never sent again.
    /// </summary>
    public void Wire(IReadOnlyCollection<string> transfers)
    {
        if (Interlocked.Exchange(ref _wired, 1) == 0) moves.Delivery.Wrote(claim, transfers);
    }

    /// <summary>After the take: the one reading, finished (<see cref="ConversationMoves.ReadAsync"/>). Never faults.</summary>
    public async Task ReadAsync()
    {
        try
        {
            await moves.ReadAsync(claim, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or SqliteException)
        {
            // The store would not take it now: a move standing past its write is said quiet, and taken up once this run ends.
        }
    }

    /// <summary>The executor goes no further: the move ended by its phase, its prompt known unwritten. Never faults.</summary>
    public async Task EndAsync()
    {
        try
        {
            await moves.EndAsync(claim, moves.Store.Read(claim.Move)?.Phase, unwritten: true, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or SqliteException)
        {
            // The store would not take it now: the move stands under this run, said quiet, and is taken up once this run ends.
        }
    }

    /// <summary>The move ended <c>superseded</c>: the old part went on before anything began. Never faults.</summary>
    public void Superseded() => Write(() => moves.Store.Close(claim, MoveCloseWhy.Superseded));

    private static string? Write(Func<MoveRecord> write)
    {
        try
        {
            write();
            return null;
        }
        catch (Exception error) when (error is DriverException or SqliteException)
        {
            return error.Message;
        }
    }
}
