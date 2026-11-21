namespace Daoris.Driver;

/// <summary>
/// The bus log's store: one port, so the store can change without its writers. Today's is
/// <see cref="DesktopBusLogStore"/>, two tables of the desktop store under the home's lease.
/// </summary>
/// <remarks>A write is one transaction: the batch's entries and the measures with them, all or none.</remarks>
public interface IBusLogStore : IDisposable
{
    /// <summary>The highest order the log keeps, 0 where it keeps none: the next append's order is one more.</summary>
    long Newest();

    /// <summary>Write one batch in one transaction, retention's cut with it; what it wrote.</summary>
    BusWritten Write(BusBatch batch);

    /// <summary>The entries after <paramref name="after"/>, in order, at most <paramref name="limit"/>: a replay.</summary>
    IReadOnlyList<BusEnvelope> Read(long after, int limit);

    /// <summary>Every measure row from <paramref name="fromDay"/> (UTC, <c>yyyy-MM-dd</c>) on, by day.</summary>
    IReadOnlyList<BusMeasureRow> Measures(string fromDay);
}

/// <summary>One flush: its entries in order, the measures owed since the last write, and whether a commit-class entry asked for it.</summary>
public sealed record BusBatch(IReadOnlyList<BusEnvelope> Entries, IReadOnlyList<BusMeasureRow> Measures, bool Commit);

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
}

/// <summary>
/// One measure of one day, added into the day's row at each write: a count, a total and the most of one (milliseconds, microseconds,
/// bytes or entries, by the measure).
/// </summary>
public sealed record BusMeasureRow(string Day, string Measure, string Name, long Count, long Total, long Most);
