using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// Reads what <see cref="LineCounts.Of"/> answers from, once for a line (CONTEXTDRIVE1a, the context-counters design §2.2): the service's
/// records, read once and handed to the readers that count them today (<see cref="ServiceClient.ReadStrikes"/>,
/// <see cref="ServiceClient.ReadFailedRuns"/>), the line's events, its moves from <see cref="StaffMoves"/>, and its answers alone from its
/// admission (<see cref="ContinuationAdmissions"/>, COMMS1g) once one is kept. It keeps nothing: a count is read from its record each time,
/// so a restart reads it equal.
/// </summary>
public sealed class LineCountsRead(
    ServiceClient service, SessionEvents events, StaffMoves moves, ContinuationAdmissions admissions, string home,
    Func<IReadOnlyDictionary<string, int>> forgiven)
{
    /// <summary>Every count of the line <paramref name="session"/> is on; none for a teammate's record.</summary>
    public async Task<IReadOnlyList<LineCount>> OfAsync(string session, CancellationToken ct)
    {
        var json = await RecordsJsonAsync(service, ct).ConfigureAwait(false);
        return LineCounts.Of(session, Gather(session, json, events, moves, admissions, home, forgiven()));
    }

    /// <summary>The service's records as it answered them, closed ones included; null where it did not, so their counts read unknown.</summary>
    public static async Task<string?> RecordsJsonAsync(ServiceClient service, CancellationToken ct)
    {
        try
        {
            return await service.SessionRecordsJsonAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }
    }

    /// <summary>
    /// The line <paramref name="session"/> is on as facts below the course (CONTEXTDRIVE1b, CONTEXTDRIVE1i; the context-counters design §3,
    /// §5): its counts, each with the number Daoris acts on it at today, its parts and its last go-ons, read from this home's files over the
    /// records' text as the service answered it (null where it did not).
    /// </summary>
    public static LineFacts FactsOf(string home, string session, string? recordsJson)
    {
        var config = Config(home);
        var evidence = Gather(
            session, recordsJson, new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions)), new StaffMoves(home), new ContinuationAdmissions(home), home,
            config.Forgiven);
        return new LineFacts(
            LineCounts.Facts(LineCounts.Of(session, evidence), config.Strikes), LineCounts.Parts(session, evidence), LineCounts.GoOns(session, evidence));
    }

    /// <summary>The same, the records read from <paramref name="service"/> now.</summary>
    public static async Task<LineFacts> FactsAsync(ServiceClient service, string home, string session, CancellationToken ct) =>
        FactsOf(home, session, await RecordsJsonAsync(service, ct).ConfigureAwait(false));

    // The home's own configuration, its failed sessions and the marks Try again left; one that does not read is the defaults'.
    private static DriverConfig Config(string home)
    {
        try
        {
            return DriverConfig.Load(DriverConfig.ResolvePath(home));
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return DriverConfig.Empty;
        }
    }

    private static LineEvidence Gather(
        string session, string? json, SessionEvents events, StaffMoves moves, ContinuationAdmissions admissions, string home,
        IReadOnlyDictionary<string, int> forgiven) =>
        Gather(
            session, json, id => EventsOf(events, id), line => MovesOf(home, line, moves), forgiven,
            history => admissions.Count(history) is { From: ContinuationAdmissions.Admission } kept ? kept : null);

    /// <summary>
    /// The evidence for the line <paramref name="session"/> is on, from the records' text as the service answered it (null where it did not) and
    /// the readers of its parts' events, of its moves by line key and of its admission's count (none kept, null).
    /// </summary>
    public static LineEvidence Gather(
        string session, string? recordsJson, Func<string, PartEvents?> eventsOf, Func<string, MovesAnswer> movesOf,
        IReadOnlyDictionary<string, int> forgiven, Func<LineHistory, AloneCount?>? aloneOf = null)
    {
        if (recordsJson is null)
        {
            var alone = LineKeys.Of(session, new Dictionary<string, PriorSession>(StringComparer.Ordinal));
            return new LineEvidence(
                null, EventsFor(alone.Parts, eventsOf), new Dictionary<string, int>(), new Dictionary<string, int>(), forgiven, movesOf(alone.Key));
        }

        var records = ServiceClient.ReadRecords(recordsJson);
        var key = LineKeys.Of(session, records);
        var kept = key.Parts.Count == 0 || aloneOf is null ? null : aloneOf(ContinuationAdmissions.History(session, records, eventsOf));
        return new LineEvidence(
            records, EventsFor(LineKeys.Members(key, records).Select(member => member.Session), eventsOf),
            ServiceClient.ReadStrikes(recordsJson), ServiceClient.ReadFailedRuns(recordsJson), forgiven, movesOf(key.Key), kept);
    }

    /// <summary>The line's moves in 24 hours; a home with no store or one before the table reads at least, and a store that does not read, unreadable.</summary>
    public static MovesAnswer MovesOf(string home, string line, StaffMoves moves)
    {
        try
        {
            using var store = DesktopStoreReader.Open(DesktopStore.LocationIn(home));
            if (store is null || store.Version < StaffMoves.Step) return new MovesAnswer(0, MovesAnswer.BeforeStep);
            return new MovesAnswer(moves.Of(line).Moves, MovesAnswer.Read);
        }
        catch (Exception error) when (error is SqliteException or IOException or UnauthorizedAccessException)
        {
            return new MovesAnswer(0, MovesAnswer.Unreadable);
        }
    }

    private static Dictionary<string, PartEvents?> EventsFor(IEnumerable<string> parts, Func<string, PartEvents?> eventsOf) =>
        parts.Distinct(StringComparer.Ordinal).ToDictionary(part => part, eventsOf, StringComparer.Ordinal);

    /// <summary>A part's events as this machine keeps them; null where the log does not read, a teammate's or another machine's.</summary>
    public static PartEvents? EventsOf(SessionEvents events, string id)
    {
        if (!SessionEventText.IsLocalId(id)) return null;
        try
        {
            var all = events.After(id, 0).Events;
            // The log's first event is seq 1 until D195's retention drops the oldest: a log that starts later has lost its start.
            return new PartEvents(all, all.Count > 0 && all[0].Seq > 1);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return null;
        }
    }
}
