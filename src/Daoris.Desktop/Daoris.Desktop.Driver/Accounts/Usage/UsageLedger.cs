namespace Daoris.Driver;

/// <summary>
/// The usage history's last <see cref="UsageProjection.Horizon"/> held in memory for one home, read on as its files grow
/// (USAGEPROBE1): what a start's projection reads, without parsing a month of turns at every start. Only what a projection
/// reads is held: each reading of a window the walk reads (the whole plan's <c>session</c> and <c>weekly</c>) that gave a
/// number, and each turn a start bound to an account, weighed where its door reports each turn's own tokens.
/// </summary>
/// <remarks>
/// Each month's file is read from where the last read stopped, a line still being written left for the next. A file shorter
/// than was read (pruned, then begun again under its name) has everything read again. Never throws: what could not be read is
/// not there yet. Safe across threads; machine-local, as the history is.
/// </remarks>
public sealed class UsageLedger(string home)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _offsets = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Agent, string Account, ReadPoint Point)> _readings = [];
    private readonly List<(string Agent, string Account, SpentTurn Turn)> _turns = [];

    /// <summary>
    /// One account's readings and turns within <see cref="UsageProjection.Horizon"/> of <paramref name="now"/>, oldest first,
    /// the history read on first.
    /// </summary>
    /// <param name="agent">The account's owner (AGT7).</param>
    /// <param name="account">The account's folder name.</param>
    public (IReadOnlyList<ReadPoint> Readings, IReadOnlyList<SpentTurn> Turns) Of(string agent, string account, DateTimeOffset now)
    {
        var since = now - UsageProjection.Horizon;
        lock (_gate)
        {
            ReadOn(since);
            return (
                [.. _readings.Where(each => Same(each.Agent, agent) && Same(each.Account, account) && each.Point.Seen >= since && each.Point.Seen <= now)
                    .Select(each => each.Point).OrderBy(point => point.Seen)],
                [.. _turns.Where(each => Same(each.Agent, agent) && Same(each.Account, account) && each.Turn.Ended >= since && each.Turn.Ended <= now)
                    .Select(each => each.Turn).OrderBy(turn => turn.Ended)]);
        }
    }

    private void ReadOn(DateTimeOffset since)
    {
        // Every month that can hold a record since then: a record is filed by the month it was written in.
        var first = new DateTimeOffset(since.UtcDateTime.Year, since.UtcDateTime.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var readings = UsageHistory.ReadingMonths(home).Where(month => month.Month >= first).ToList();
        var turns = UsageHistory.TurnMonths(home).Where(month => month.Month >= first).ToList();

        if (readings.Concat(turns).Any(month => _offsets.TryGetValue(month.Name, out var read) && month.Length < read)) Forget();
        foreach (var gone in _offsets.Keys.Where(name => !readings.Concat(turns).Any(month => Same(month.Name, name))).ToList())
        {
            _offsets.Remove(gone);
        }

        foreach (var month in readings)
        {
            var read = new List<KeptReading>();
            var to = UsageHistory.ReadingsOn(home, month, _offsets.GetValueOrDefault(month.Name), read);
            if (to < 0)
            {
                Forget();
                return;
            }

            _offsets[month.Name] = to;
            foreach (var kept in read)
            {
                if (kept.Window is { Walk: { } walk, Scope: null, Used: { } used })
                {
                    _readings.Add((kept.Agent, kept.Account, new ReadPoint(walk, used, kept.Window.Reset, kept.Seen, kept.Epoch)));
                }
            }
        }

        foreach (var month in turns)
        {
            var read = new List<KeptTurn>();
            var to = UsageHistory.TurnsOn(home, month, _offsets.GetValueOrDefault(month.Name), read);
            if (to < 0)
            {
                Forget();
                return;
            }

            _offsets[month.Name] = to;
            foreach (var kept in read)
            {
                // A turn whose report's meaning is not known may be a running total (D179 point 8), and one that said no count
                // said nothing of its size: each is held as spend nothing weighs, never as none.
                if (kept is { Agent: { } agent, Account: { } account })
                {
                    var work = kept.Meaning == UsageHistory.PerTurn ? UsageProjection.Work(kept.Tokens) : null;
                    _turns.Add((agent, account, new SpentTurn(kept.Ended, work, kept.Epoch)));
                }
            }
        }

        _readings.RemoveAll(each => each.Point.Seen < since);
        _turns.RemoveAll(each => each.Turn.Ended < since);
    }

    // Everything read again from the start at the next read: a file was begun again under a name already read.
    private void Forget()
    {
        _offsets.Clear();
        _readings.Clear();
        _turns.Clear();
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
