namespace Daoris.Driver;

/// <summary>
/// The bus log's store: one port, so the store can change without its writers. Today's is
/// <see cref="DesktopBusLogStore"/>, two tables of the desktop store under the home's lease.
/// </summary>
/// <remarks>
/// A write is one transaction: the batch's entries and the measures with them, all or none. Inside it the store asks the batch's writer
/// (<see cref="BusBatch.AskWriter"/>) and seats the batch's orders after the newest it keeps, so no other holder's write comes between the
/// lease's answer and the rows, and a log that lost the lease and regained it never collides with the entries written meanwhile.
/// </remarks>
public interface IBusLogStore : IDisposable
{
    /// <summary>Write one batch in one transaction, retention's cut with it; what it wrote.</summary>
    /// <exception cref="BusStoreBusyException">The store could not be written this moment, or the lease not read, and nothing of the batch is.</exception>
    /// <exception cref="BusNotWriterException">This process does not hold the home's lease, asked inside the write, and nothing of the batch is written.</exception>
    BusWritten Write(BusBatch batch);

    /// <summary>The entries after <paramref name="after"/>, in order, at most <paramref name="limit"/>: a replay.</summary>
    IReadOnlyList<BusEnvelope> Read(long after, int limit);

    /// <summary>Every measure row from <paramref name="fromDay"/> (UTC, <c>yyyy-MM-dd</c>) on, by day.</summary>
    IReadOnlyList<BusMeasureRow> Measures(string fromDay);

    /// <summary>The correlation of the entry <paramref name="id"/> where the store keeps it (an older row reads as its own id); null where it keeps no such entry.</summary>
    string? CorrelationOf(string id) => null;

    /// <summary>The armed timers due by <paramref name="by"/> (null: all), earliest first; each write moves them (<see cref="BusTimers.Of"/>).</summary>
    IReadOnlyList<BusTimer> Timers(DateTimeOffset? by, int limit);

    /// <summary>The timers of <paramref name="entries"/> taken away: a kind the registry no longer declares, which no fact can end.</summary>
    void DropTimers(IReadOnlyList<string> entries)
    {
    }
}

/// <summary>
/// The queue's half of the log's store (<see cref="BusQueue"/>): each consumer's row (its types, its cursor, its fenced epoch) and the
/// entries it parked, each write one transaction under the home's lease (<see cref="BusBatch.AskWriter"/>) and the consumer's epoch.
/// </summary>
public interface IBusQueueStore
{
    /// <summary>
    /// The consumer claimed for this process: its epoch raised (an older one's writes are refused from now), its cursor (a new consumer's
    /// the log's newest order, so it starts from now) and its parked entries still in the log, in order.
    /// </summary>
    BusClaim Claim(string consumer, IReadOnlyList<string> kinds, Func<bool?>? writer, DateTimeOffset at);

    /// <summary>One entry acknowledged: the cursor moved up, the timer it ends cleared and its parked row taken, in one transaction.</summary>
    /// <exception cref="BusFencedException">A later claim raised the consumer's epoch.</exception>
    void Acknowledge(BusAck ack);

    /// <summary>Entries parked with their tries, the cursor moved past them, in one transaction.</summary>
    /// <exception cref="BusFencedException">A later claim raised the consumer's epoch.</exception>
    void Park(BusPark park);

    /// <summary>The entries of <paramref name="kinds"/> after <paramref name="after"/>, in order: a consumer's replay.</summary>
    IReadOnlyList<BusEnvelope> Read(long after, IReadOnlyList<string> kinds, int limit);

    /// <summary>A consumer's kept cursor, epoch and parked count; null where it never claimed.</summary>
    (long Cursor, long Epoch, int Parked)? Consumer(string consumer);
}

/// <summary>A consumer's claim: its cursor, its epoch and its parked entries with their tries.</summary>
public sealed record BusClaim(long Cursor, long Epoch, IReadOnlyList<BusParkedEntry> Parked);

/// <summary>An entry a consumer parked, and how many times it threw.</summary>
public sealed record BusParkedEntry(BusEnvelope Entry, int Tries);

/// <summary>A consumer's acknowledgement of <paramref name="Entry"/>, under its epoch, its cursor after it.</summary>
public sealed record BusAck(string Consumer, long Epoch, long Cursor, BusEnvelope Entry)
{
    /// <summary>The obliged handler whose timer on the entry this ends; null ends none.</summary>
    public string? Ends { get; init; }

    /// <summary>Whether the entry sits parked, its row taken with this.</summary>
    public bool Unpark { get; init; }

    /// <summary>The home's lease, asked inside the transaction (<see cref="BusBatch.Writer"/>).</summary>
    public Func<bool?>? Writer { get; init; }
}

/// <summary>A consumer's parked entries, each order with its tries, under its epoch, its cursor after them.</summary>
public sealed record BusPark(string Consumer, long Epoch, long Cursor, IReadOnlyList<(long Seq, int Tries)> Rows)
{
    public Func<bool?>? Writer { get; init; }
}

/// <summary>A later claim raised the consumer's epoch: this one's write is refused, so two processes never both execute it.</summary>
public sealed class BusFencedException(string consumer, long epoch, long current)
    : Exception($"`{consumer}` was claimed again (epoch {current}), so epoch {epoch}'s write is refused");

/// <summary>The store could not be written this moment (its lock held past the wait, its file held): a later try may pass, so its batch is kept.</summary>
public sealed class BusStoreBusyException(Exception inner) : Exception($"the bus log's store is busy ({inner.Message})", inner);

/// <summary>Asked inside the write, this process does not hold the home's lease: its batch is dropped, never written by an old holder.</summary>
public sealed class BusNotWriterException() : Exception("this process does not hold the home's lease, so it does not write the bus's log");

/// <summary>One flush: its entries in order, the measures owed since the last write, and whether a commit-class entry asked for it.</summary>
public sealed record BusBatch(IReadOnlyList<BusEnvelope> Entries, IReadOnlyList<BusMeasureRow> Measures, bool Commit)
{
    /// <summary>Whether this process holds the home's lease (null: its file could not be read this moment); null asks nothing.</summary>
    public Func<bool?>? Writer { get; init; }

    /// <summary>The lease asked, inside the write's transaction: a store's one call before it writes a row.</summary>
    /// <exception cref="BusNotWriterException">Another process holds the lease.</exception>
    /// <exception cref="BusStoreBusyException">The lease could not be read this moment.</exception>
    public void AskWriter()
    {
        if (Writer is null) return;
        switch (Writer())
        {
            case false: throw new BusNotWriterException();
            case null: throw new BusStoreBusyException(new IOException("the home's lease could not be read"));
        }
    }
}

/// <summary>
/// What one write wrote: the rows' own bytes it appended and the largest row's, how many old rows retention cut, and the pages it wrote
/// to disk with their size (null where the store cannot say).
/// </summary>
public sealed record BusWritten(long Bytes, int Cut, long Most = 0)
{
    /// <summary>The pages the write cost the disk: the write-ahead log's frames, a checkpoint's copy into the file aside; null where not known.</summary>
    public long? Pages { get; init; }

    /// <summary>The bytes of one page.</summary>
    public long PageSize { get; init; }

    /// <summary>
    /// How far the batch's orders moved to follow the newest the store kept (0 where they did): the log moves what waits by as much. A
    /// log's first write after it opens moves by the log's length; a later one only where another holder wrote meanwhile.
    /// </summary>
    public long Shift { get; init; }

    /// <summary>Whether the write opened the store first (its steps run there), whose time is not a flush's.</summary>
    public bool Opened { get; init; }
}

/// <summary>
/// One measure of one day, added into the day's row at each write: a count, a total and the most of one (milliseconds, microseconds,
/// bytes or entries, by the measure).
/// </summary>
public sealed record BusMeasureRow(string Day, string Measure, string Name, long Count, long Total, long Most);
