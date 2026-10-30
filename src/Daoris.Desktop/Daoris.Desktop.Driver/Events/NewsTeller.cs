namespace Daoris.Driver;

/// <summary>A piece of news a teller claimed and is to show: what happened, to which session, and the journal position that keys it.</summary>
/// <param name="Reason">For an end: the note's <c>ended.*</c> code.</param>
/// <param name="Class">For an end: its class, one closed code (§4.2).</param>
public sealed record NewsTold(
    AttentionKind Kind, string Session, long Position, string Store, string? Workspace, string? State, string? Reason, string? Class, DateTimeOffset At);

/// <summary>
/// One process's teller of the journal's news (FOUNDATION1 m-ii and m-iii, the foundation design §4.2, §4.5, §19.6): the rows it takes, a park
/// or an end, claimed in the said-set by their keys and shown only when claimed, so a restart tells a park made just before it once and no
/// piece twice. The lease's loop takes the driven work's rows and the shell the conversations', each with a cursor of its own.
/// </summary>
/// <param name="news">The one owner of the news.</param>
/// <param name="name">The teller's name, its cursor's key.</param>
/// <param name="takes">Which rows are this teller's; a row it leaves is neither told nor claimed, so the teller that takes it tells it.</param>
/// <param name="clock">What time it is, for the horizon.</param>
/// <param name="show">What the claimed pieces become, called after the claim and never before it.</param>
public sealed class NewsTeller(News news, string name, Func<JournalEntry, bool> takes, Func<DateTimeOffset> clock, Func<IReadOnlyList<NewsTold>, Task> show)
{
    /// <summary>Called with a batch whose history is incomplete since this teller's cursor: the teller tells what the journal still holds and none of the gap (§4.1).</summary>
    public Action<JournalBatch>? Incomplete { get; init; }

    /// <summary>Called with why a claim the store refused showed nothing.</summary>
    public Action<string>? Refused { get; init; }

    /// <summary>The batch's news, claimed, and shown.</summary>
    public async Task TellAsync(JournalBatch batch)
    {
        if (batch.Missed) Incomplete?.Invoke(batch);

        var told = new Dictionary<string, NewsTold>(StringComparer.Ordinal);
        foreach (var row in batch.Rows.Where(takes))
        {
            if (Of(row, batch.Store) is { } piece) told[News.KeyOf(batch.Store, row.Position)] = piece;
        }

        // Nothing to claim: the feed's own keep moves the cursor, and no write is made for a batch with no news.
        if (told.Count == 0) return;

        var last = batch.Rows.Count == 0 ? (long?)null : batch.Rows.Max(row => row.Position);
        var claim = news.Claim(
            name,
            [.. told.Select(each => new NewsOccurrence(each.Key, each.Value.At))],
            last is { } position ? new JournalPosition(batch.Store, position) : null,
            clock());
        if (claim.Refused is { } why)
        {
            Refused?.Invoke(why);
            return;
        }

        var pieces = claim.Told.Select(each => told[each.Key]).ToList();
        if (pieces.Count > 0) await show(pieces).ConfigureAwait(false);
    }

    /// <summary>The news a row is, or null: a move into the state that waits on the person, from one that does not.</summary>
    private static NewsTold? Of(JournalEntry row, string store)
    {
        if (row.Family != "sessions" || row.Change != "state" || row.StateNew is null) return null;
        var at = DateTimeOffset.FromUnixTimeMilliseconds(row.At);
        if (Parked(row.StateNew) && !Parked(row.StateOld))
        {
            return new NewsTold(AttentionKind.Parked, row.Record, row.Position, store, row.Workspace, null, null, null, at);
        }

        // An end by the one table: the classes the watch said before, never a conversation's (§4.2).
        if (PartEnds.Judge(row, store) is { } ended && EndReaders.Takes(EndReader.Notified, ended))
        {
            return new NewsTold(AttentionKind.Ended, ended.Session, ended.Position, store, ended.Workspace, ended.State, ended.Reason, ended.Class, at);
        }

        return null;
    }

    private static bool Parked(string? state) => state is not null && state.Replace("-", "", StringComparison.Ordinal).Equals("awaitingperson", StringComparison.OrdinalIgnoreCase);
}
