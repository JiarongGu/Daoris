namespace Daoris.Driver;

/// <summary>A word waiting on a record's <c>said</c> as a move reads it: its id there, and whether a dispatcher marked it attempted.</summary>
/// <param name="Word">Its id on the record, which a moved word keeps from the record it left (row d-ii part 3).</param>
/// <param name="Attempted">Whether a dispatcher marked it attempted before a write: it may have reached its agent.</param>
/// <param name="Person">Whether it is the person's own word: another author's (a helper's answer, an agent's findings, Daoris's) is a claim, never offered as theirs to say (§19.3).</param>
public sealed record MoveWaiting(string Word, bool Attempted, bool Person = true);

/// <summary>
/// A move's destination's <c>said</c>, as the move reads and settles it (FOUNDATION1f-i part 3, D212; the foundation design §3.3, §13.4 step 4,
/// §19.4): what waits there, the take of words its agent was written, and the settlement of uncertain ones. Never a session event, which is
/// the display's and no delivery's evidence. Rows f-ii to f-vi hand the service's, <see cref="MoveSaidDoor"/>.
/// </summary>
public interface IMoveSaid
{
    /// <summary>The words waiting on a record's <c>said</c>, in their order: none for a record the ledger no longer holds; null where it cannot be told now. Never faults.</summary>
    Task<IReadOnlyList<MoveWaiting>?> WaitingAsync(string record, CancellationToken ct);

    /// <summary>Take these words off a record as taken, its session having taken them. A reader judges what the record holds after, never this. Never faults.</summary>
    Task TakeAsync(string record, IReadOnlyList<string> words, CancellationToken ct);

    /// <summary>
    /// Settle these uncertain words on a record as any attempted word is: taken back, never sent again by itself, with the <c>lost</c> note on the
    /// record naming those that left, written by the move itself whatever its conversation shows (round 8, <i>O7-F4</i>). Never faults.
    /// </summary>
    Task SettleAsync(string record, IReadOnlyList<string> words, CancellationToken ct);

    /// <summary>
    /// The head of the line a record is on, where it is past the record: the newest record that continues it, which only an older build makes
    /// while a move stands (§13.4 step 5, §19.3). Null where the record is the head, or where it cannot be told now. Never faults. A reader
    /// handed none reads every record as the head.
    /// </summary>
    Task<string?> HeadPastAsync(string record, CancellationToken ct) => Task.FromResult<string?>(null);
}

/// <summary>
/// The service's doors to a destination's <c>said</c> (FOUNDATION1f-i part 3): the record's waiting words and their attempt marks as this
/// machine's records answer them, the taken door, and the take-back door with the <c>lost</c> note kept in the record's events. Rows f-ii to
/// f-vi hand it the move's readings, the conversations' holder first (FOUNDATION1f-ii part a, <see cref="ConversationMoves"/>).
/// </summary>
/// <param name="service">The service whose ledger holds the records.</param>
/// <param name="events">Where a record's <c>lost</c> note is kept; none keeps none.</param>
public sealed class MoveSaidDoor(ServiceClient service, SessionEvents? events) : IMoveSaid
{
    public async Task<IReadOnlyList<MoveWaiting>?> WaitingAsync(string record, CancellationToken ct)
    {
        try
        {
            // No record of this machine's by that id holds no word; a host from before `said` answers none, which cannot be told.
            if (await service.RecordAsync(record, ct).ConfigureAwait(false) is not { } read) return [];
            return read.Said is { } said
                ? [.. said.Where(word => word.Id is not null).Select(word =>
                    new MoveWaiting(word.Id!, word.Attempt is not null, word.By is null && word.Helper is null && word.From is null))]
                : null;
        }
        catch (Exception error) when (Unanswered(error, ct))
        {
            return null;
        }
    }

    public async Task<string?> HeadPastAsync(string record, CancellationToken ct)
    {
        try
        {
            return WordsNever.ContinuedIn(await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false), record);
        }
        catch (Exception error) when (Unanswered(error, ct))
        {
            return null;
        }
    }

    public async Task TakeAsync(string record, IReadOnlyList<string> words, CancellationToken ct)
    {
        if (words.Count == 0) return;
        try
        {
            await service.TakenAsync(record, words, by: null, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (Unanswered(error, ct))
        {
            // The words wait for the next reading, which takes them off again; nothing reads this answer.
        }
    }

    public async Task SettleAsync(string record, IReadOnlyList<string> words, CancellationToken ct)
    {
        var gone = new List<string>();
        foreach (var word in words)
        {
            try
            {
                // Taken back, as the come-up's uncertain settlement takes an attempted word: no session is known to have taken it.
                if (await service.TakeBackAsync(record, word, text: null, ct).ConfigureAwait(false) is { Done: true } or { Why: "taken" }) gone.Add(word);
            }
            catch (Exception error) when (Unanswered(error, ct))
            {
                // It waits, attempted, for the move's next reading.
            }
        }

        // The move writes its own note whatever the conversation shows: its words' events are recorded before the write (O7-F4).
        if (gone.Count > 0)
        {
            events?.Keep(record, new SessionEvent { Kind = SessionEventKind.Note, Text = SessionNotes.LostLine, Words = gone, Code = SessionEventCodes.Lost }, say: null);
        }
    }

    private static bool Unanswered(Exception error, CancellationToken ct) =>
        error is HttpRequestException or DriverException or System.Text.Json.JsonException or ObjectDisposedException
        || (error is OperationCanceledException && !ct.IsCancellationRequested);
}

/// <summary>No destination's <c>said</c> to read: every record cannot be told, and nothing is taken or settled. A take-up handed none reads with it.</summary>
internal sealed class MoveSaidUntold : IMoveSaid
{
    public static readonly MoveSaidUntold Instance = new();

    public Task<IReadOnlyList<MoveWaiting>?> WaitingAsync(string record, CancellationToken ct) => Task.FromResult<IReadOnlyList<MoveWaiting>?>(null);

    public Task TakeAsync(string record, IReadOnlyList<string> words, CancellationToken ct) => Task.CompletedTask;

    public Task SettleAsync(string record, IReadOnlyList<string> words, CancellationToken ct) => Task.CompletedTask;
}
