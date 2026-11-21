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
}

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
