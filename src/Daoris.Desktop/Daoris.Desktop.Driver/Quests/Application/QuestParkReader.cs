namespace Daoris.Driver;

/// <summary>
/// The quests' parks as the doors read them (SESSUX1i, D126 §4.6, §4.7): what the watch says a park with, and what the
/// tick hands Overview's row. One reader for both doors, the shell's loop and the headless host, so they say one thing.
/// </summary>
/// <remarks>
/// <para><b>Read only when what the planner parked changed.</b> The records are every session ever run (D126 M9), and a
/// park lasts every look until *Try again*; reading them each look for the same answer would double the look's largest
/// read. A new park always changes the set: its quest left it to run the sessions that failed.</para>
///
/// <para><b>A read that fails is asked again at the next look</b>, and says nothing meanwhile: a park is never lost to one
/// refused read, nor said from a guess.</para>
///
/// <para>Called by one loop, one look at a time, so it holds no lock; <see cref="Latest"/> is read beside it.</para>
/// </remarks>
public sealed class QuestParkReader
{
    private string? _read;
    private volatile IReadOnlyList<QuestPark> _latest = [];

    /// <summary>The parks as last read, in the planner's order; none before any read.</summary>
    public IReadOnlyList<QuestPark> Latest => _latest;

    /// <summary>
    /// This look's parks, read from the records where the quests the planner parked changed since the last read; null where
    /// they did not, or where the records could not be read.
    /// </summary>
    /// <param name="forgivenAt">RETRY1's mark for a quest (<see cref="DriverConfig.ForgivenAt"/>): the planner counts from it.</param>
    /// <param name="records">The records door (<see cref="SessionRecords.ReadAsync"/>); a test hands in its own.</param>
    public async Task<IReadOnlyList<QuestPark>?> LookAsync(
        TickReport report, Func<string, int> forgivenAt, Func<CancellationToken, Task<string>> records,
        CancellationToken ct = default)
    {
        var parked = report.Considerations.Where(c => c.Verdict == StartVerdict.Exhausted).ToList();
        var signature = Considerations.Signature(parked);
        if (signature == _read) return null;

        IReadOnlyList<QuestPark> parks = [];
        if (parked.Count > 0)
        {
            try
            {
                parks = SessionGroups.Parks(SessionLook.From(await records(ct).ConfigureAwait(false), [], parked, forgivenAt));
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                              || (error is TaskCanceledException && !ct.IsCancellationRequested))
            {
                return null;
            }
        }

        _read = signature;
        _latest = parks;
        return parks;
    }
}
