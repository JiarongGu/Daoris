namespace Daoris.Driver;

/// <summary>
/// The plugin steps in flight under a home: each attempt a plugin performs (its step <c>&lt;plugin&gt;/&lt;node&gt;</c>) that is
/// recorded, sent or unknown, so sent or perhaps sent and its outcome not settled, on a record whose run goes on. What an update's drain
/// waits on beside the driven sessions and the turns: a swap then cuts the call off, and its outcome is not known until the restart
/// asks after it.
/// </summary>
/// <remarks>
/// None of these is in flight: a settled attempt; a held one, which waits on the person's word rather than its plugin; Daoris's own
/// kinds (a check, a close, a request), which a restart asks after or sends again itself; an ended run's, which nothing moves any more;
/// and a record that does not read, on which nothing is judged or sent until it is mended. Read from the records as the runtime keeps
/// them (<see cref="NodeRecordFiles.All"/>), never written.
/// </remarks>
public static class PluginStepsInFlight
{
    /// <summary>Whether an attempt is a plugin's step sent, or perhaps sent, and not settled.</summary>
    public static bool InFlight(NodeAttempt attempt) =>
        attempt.State is AttemptStates.Recorded or AttemptStates.Sent or AttemptStates.Unknown && NodeKit.IsPlugin(attempt.Call.Step);

    /// <summary>The plugin steps in flight on <paramref name="records"/>, an ended run's none.</summary>
    public static int Count(IEnumerable<NodeRecord> records) =>
        records.Where(record => record.Ended is null).Sum(record => record.Attempts.Count(InFlight));

    /// <summary>The plugin steps in flight on the home's records, read and never written.</summary>
    public static int Count(string home) => Count(new NodeRecordFiles(home, () => false).All().Records);
}
