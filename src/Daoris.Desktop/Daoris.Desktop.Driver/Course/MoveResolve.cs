using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// A held move's one rule (FOUNDATION1f-i part 3, D212; the foundation design §3.3, §13.5's <c>held</c> row, §19.3): it closes <c>resolved</c>
/// once nothing it names still waits on its destination's <c>said</c>, and until then it stands and its destination takes no move of its own.
/// Rows f-ii to f-vi, h and i adopt it, the conversations' holder first (FOUNDATION1f-ii part a, <see cref="ConversationMoves"/>).
/// </summary>
/// <remarks>
/// <para><b>What a hold names</b> is written with it: the move's words disposed <i>left</i>, uncertain or held for the person
/// (<see cref="MoveWordDispositions.Named"/>), and its begun effect. <b>A begun effect alone keeps no hold</b>: it waits on no <c>said</c>, the
/// hold's note has said it, and nothing makes it again whether the hold stands or not; so a hold naming no word, or one with no destination,
/// closes at its first read, without the destination being read at all. A word <i>left</i> is gone and keeps nothing; a word uncertain
/// keeps it until the settlement takes it back; a word held for the person until the person's take-back.</para>
/// <para><b>Where it is read.</b> By its executor under its claim (<see cref="ReadAsync"/>) at each come-up and each look, which the
/// take-up calls, and wherever the course reads the destination's standing move as a precondition (<see cref="StandingAsync"/>), so a shell
/// that runs no look closes its holds as its course reads them; and at the person's press through one of its doors
/// (<see cref="PressedAsync"/>), which needs no claim, since a held move takes no effect, mark or phase but its close.</para>
/// </remarks>
/// <param name="store">The move record, under this process's run.</param>
/// <param name="said">The destinations' <c>said</c>.</param>
public sealed class MoveResolve(MoveStore store, IMoveSaid said)
{
    /// <summary>The words a hold names: its move's words disposed <i>left</i>, uncertain or held for the person (<see cref="MoveWordDispositions.Named"/>).</summary>
    public static IReadOnlyList<MoveRecordWord> Names(IReadOnlyList<MoveRecordWord> words) =>
        [.. words.Where(word => word.Disposition is { } disposition && MoveWordDispositions.Named.Contains(disposition, StringComparer.Ordinal))];

    /// <summary>
    /// The one rule's question: whether a word the hold names still waits on its destination's <c>said</c>. False where none does, a hold
    /// naming no word or with no destination among them, which is read without asking the destination; null where it cannot be read now.
    /// </summary>
    public async Task<bool?> WaitsAsync(MoveRecord held, CancellationToken ct = default)
    {
        var named = Names(store.WordsOf(held.Id));
        if (named.Count == 0 || held.Destination is not { } destination) return false;
        if (await MoveSaying.WaitingAsync(said, destination, ct).ConfigureAwait(false) is not { } waiting) return null;
        return named.Any(word => waiting.Any(each => string.Equals(each.Word, word.Word, StringComparison.Ordinal)));
    }

    /// <summary>
    /// The executor's read under its claim, at a come-up, a look or a precondition: a held move's uncertain words still waiting settled first,
    /// as the take-up settles any attempted word, with the <c>lost</c> note (round 8, <i>O7-F4</i>); then closed <c>resolved</c> where nothing it
    /// names waits. A move not held is answered as it stands.
    /// </summary>
    /// <exception cref="MoveClaimLostException">The claim no longer holds the move.</exception>
    public async Task<MoveRecord> ReadAsync(MoveClaim claim, CancellationToken ct = default)
    {
        var move = store.Read(claim.Move) ?? throw new MoveClaimLostException(claim, "the move is gone, with its record");
        if (move.Phase != MoveRecordPhase.Held) return move;
        List<string> uncertain = [.. store.WordsOf(move.Id).Where(word => word.Disposition == MoveWordDispositions.Uncertain).Select(word => word.Word)];
        if (uncertain.Count > 0 && move.Destination is { } destination
            && await MoveSaying.WaitingAsync(said, destination, ct).ConfigureAwait(false) is { } waiting)
        {
            List<string> still = [.. uncertain.Where(word => waiting.Any(each => string.Equals(each.Word, word, StringComparison.Ordinal)))];
            if (still.Count > 0) await MoveSaying.SettleAsync(said, destination, still, ct).ConfigureAwait(false);
        }

        return await WaitsAsync(move, ct).ConfigureAwait(false) == false ? store.Close(claim, MoveCloseWhy.Resolved) : move;
    }

    /// <summary>
    /// A record's standing move as the course reads it before it moves the record (§3.1's <c>Never</c>): the one whose open made the record,
    /// the destination's, which the design names, or else the one the record left. Where it is held under this store's run, the one rule is
    /// read first under that claim; another run's hold, live or dead, is answered as it stands, its own executor's or its taker's to read.
    /// </summary>
    /// <returns>The move that still stands, which the course reads as <c>Never</c>; null where none does and the record may move.</returns>
    public async Task<MoveRecord?> StandingAsync(string record, CancellationToken ct = default)
    {
        var standing = store.StandingTo(record) ?? store.Standing(record);
        if (standing is not { Phase: MoveRecordPhase.Held } held || !string.Equals(held.ClaimRun, store.Run, StringComparison.Ordinal)) return standing;
        try
        {
            var read = await ReadAsync(new MoveClaim(held.Id, held.ClaimRun, held.ClaimGeneration), ct).ConfigureAwait(false);
            return read.Stands ? read : store.StandingTo(record) ?? store.Standing(record);
        }
        catch (Exception error) when (error is DriverException or SqliteException)
        {
            // The store would not take the close now, or the claim moved under it: it stands, and the next read reads it again.
            return held;
        }
    }

    /// <summary>
    /// The person's press through one of a hold's doors (§3.3; §19.3: the take-back of a word it names at its destination, or the word said
    /// at the head): the hold standing on <paramref name="record"/>, its destination's or else the one it left, read by the one rule and closed
    /// <c>resolved</c> where nothing it names waits, without its claim (<see cref="MoveStore.PressResolved"/>).
    /// </summary>
    /// <returns>The move as it stands after the press; null where none stands on the record.</returns>
    public async Task<MoveRecord?> PressedAsync(string record, CancellationToken ct = default)
    {
        var standing = store.StandingTo(record) ?? store.Standing(record);
        if (standing is not { Phase: MoveRecordPhase.Held } held) return standing;
        return await WaitsAsync(held, ct).ConfigureAwait(false) == false ? store.PressResolved(held.Id) : held;
    }
}

/// <summary>A destination's <c>said</c> asked through its port, an answer that faults read as one that could not be told.</summary>
internal static class MoveSaying
{
    public static async Task<IReadOnlyList<MoveWaiting>?> WaitingAsync(IMoveSaid said, string record, CancellationToken ct)
    {
        try
        {
            return await said.WaitingAsync(record, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Faulted(error, ct))
        {
            return null;
        }
    }

    public static async Task TakeAsync(IMoveSaid said, string record, IReadOnlyList<string> words, CancellationToken ct)
    {
        if (words.Count == 0) return;
        try
        {
            await said.TakeAsync(record, words, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Faulted(error, ct))
        {
            // What the record holds after is read again; a word still waiting is taken at the next reading.
        }
    }

    public static async Task SettleAsync(IMoveSaid said, string record, IReadOnlyList<string> words, CancellationToken ct)
    {
        try
        {
            await said.SettleAsync(record, words, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Faulted(error, ct))
        {
            // The words wait, attempted, for the next reading under the claim.
        }
    }

    private static bool Faulted(Exception error, CancellationToken ct) =>
        error is HttpRequestException or DriverException or IOException or System.Text.Json.JsonException or ObjectDisposedException
        || (error is OperationCanceledException && !ct.IsCancellationRequested);
}
