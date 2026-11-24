namespace Daoris.Driver;

/// <summary>
/// The log's measures since its last write, by the day (UTC), measure and name: a count, a total and the most of one, added into the
/// store's row of the day at the next write. Not thread-safe: <see cref="BusLog"/> holds its gate around every use.
/// </summary>
internal sealed class BusMeasures
{
    /// <summary>Entries appended, by kind: the count.</summary>
    public const string Kind = "kind";

    /// <summary>Each kind's handling time where its writer knows it, in milliseconds.</summary>
    public const string Took = "took";

    /// <summary>The flushes: <see cref="Entries"/>, <see cref="Micros"/>, <see cref="CommitClass"/>, <see cref="MeasuresAlone"/>.</summary>
    public const string Flush = "flush";

    /// <summary>The rows' own bytes appended (<see cref="Rows"/>): count the rows, total the bytes, most the largest row.</summary>
    public const string Bytes = "bytes";

    /// <summary>
    /// What the writes cost the disk: <see cref="Pages"/> written (the write-ahead log's frames) and their <see cref="Bytes"/>, each counting
    /// the writes, totalling the pages or bytes, most the largest write's.
    /// </summary>
    public const string Disk = "disk";

    public const string Pages = "pages";

    /// <summary>The rows retention cut.</summary>
    public const string Cut = "cut";

    /// <summary>The entries refused, by reason (<see cref="BusMisses"/>); and <see cref="Drop"/>, the values dropped, as <c>reason|kind|field</c>.</summary>
    public const string Miss = "miss";

    public const string Drop = "drop";

    /// <summary>The batches a busy store sent back (<see cref="Entries"/>): count the batches, total their entries.</summary>
    public const string Retry = "retry";

    /// <summary>A look stage's time, in milliseconds, by the stage.</summary>
    public const string Stage = "stage";

    public const string Git = "git";

    /// <summary>What the queue handed on: its frames, its replays and each kind's hand-ons (<see cref="BusDeliveryMeasures"/>).</summary>
    public const string Delivered = "delivered";

    public const string Frames = "frames";

    public const string Replayed = "replayed";

    public const string Middleware = "middleware";

    public const string Sweep = "sweep", Found = "found", Retried = "retried", Overflow = "overflow", Handled = "handled";

    public const string Began = "began", ByConsumer = "consumer", ByLook = "look", ByOther = "other";

    public const string Step = "step", StepBegan = "began";

    public const string Gate = "gate";

    /// <summary>What each consumer threw, by its name.</summary>
    public const string Consumer = "consumer";

    public const string Entries = "entries";

    public const string Micros = "us";

    public const string CommitClass = "commit";

    public const string MeasuresAlone = "measures";

    public const string Rows = "rows";

    private readonly Dictionary<(string Day, string Measure, string Name), (long Count, long Total, long Most)> _rows = [];

    /// <summary>Whether a measure since the last write owes a write of its own: the looks' stages, a miss, an entry flush's cost.</summary>
    public bool Owed { get; private set; }

    /// <summary>Whether anything is measured since the last write.</summary>
    public bool Any => _rows.Count > 0;

    /// <summary>Add <paramref name="value"/> to a measure's total, <paramref name="count"/> to its count, and keep its most.</summary>
    /// <param name="owing">Whether it owes a write of its own (<see cref="Owed"/>); a write of the measures alone owes none for its own cost.</param>
    public void Add(string day, string measure, string name, long value, long count = 1, long? most = null, bool owing = true)
    {
        var key = (day, measure, name);
        var (had, total, largest) = _rows.GetValueOrDefault(key);
        _rows[key] = (had + count, total + value, Math.Max(largest, most ?? value));
        Owed |= owing;
    }

    /// <summary>Every measure since the last write, let go: what the write carries.</summary>
    public IReadOnlyList<BusMeasureRow> Take()
    {
        var rows = _rows.Select(pair => new BusMeasureRow(pair.Key.Day, pair.Key.Measure, pair.Key.Name, pair.Value.Count, pair.Value.Total, pair.Value.Most))
            .OrderBy(row => row.Day, StringComparer.Ordinal).ToList();
        _rows.Clear();
        Owed = false;
        return rows;
    }

    /// <summary>A write's measures given back where the store refused it, for the next write to carry.</summary>
    public void Return(IReadOnlyList<BusMeasureRow> rows)
    {
        foreach (var row in rows) Add(row.Day, row.Measure, row.Name, row.Total, row.Count, row.Most);
    }
}
