using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// What a store says before the fold's trial: its records, those not ended and their ages, those shown in progress with no
/// process, those not moved for over two hours, those the fold says other than the status, the fold's time for a list, the time
/// buckets of the journalled, the census of state moves no fact witnesses, and an append's cost against the span between moves.
/// Counts and times only: no id, note or name.
/// </summary>
/// <param name="Records">This machine's records (a teammate's mirrored ones left out).</param>
/// <param name="NotEnded">Those whose stored status is live.</param>
/// <param name="Ages">The age of each not ended, since its making.</param>
/// <param name="InProgressNoProcess">Those stored starting or working with no live lease.</param>
/// <param name="StillOverTwoHours">Those not ended whose status has not moved for over two hours.</param>
/// <param name="Journalled">Those with a journal.</param>
/// <param name="Seeded">Those folded with no journal, from the one fact their stored status says.</param>
/// <param name="Differ">Those folded whose fold says other than their status.</param>
/// <param name="DifferSeeded">Of <see cref="Differ"/>, those folded from their status alone.</param>
/// <param name="Pairs">The differences by stored state and fold, as <c>stored → fold</c>.</param>
/// <param name="Folded">How many sessions the timed list folded.</param>
/// <param name="FoldTook">How long the list took: its last facts read and each folded.</param>
/// <param name="Buckets">The time buckets summed over every journalled record.</param>
public sealed record SessionBaselineReading(
    int Records, int NotEnded, IReadOnlyList<TimeSpan> Ages, int InProgressNoProcess, int StillOverTwoHours, int Journalled, int Seeded,
    int Differ, int DifferSeeded, IReadOnlyDictionary<string, int> Pairs, int Folded, TimeSpan FoldTook,
    IReadOnlyDictionary<TimeBucket, TimeSpan> Buckets)
{
    /// <summary>Those not ended, by their stored state's spelling.</summary>
    public IReadOnlyDictionary<string, int> ByState { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>How many leases the markers proved alive, and how many of them are live (their owner runs).</summary>
    public (int Proved, int Live) Leases { get; init; }

    /// <summary>The census: <c>event_journal</c>'s state moves no fact witnesses, from the first a fact does.</summary>
    public int Unwitnessed { get; init; }

    /// <summary>The journal's rows this build cannot read.</summary>
    public int Unreadable { get; init; }

    /// <summary>The median time of one append, timed on an in-memory copy of the store; null where it holds no record to append to.</summary>
    public TimeSpan? Append { get; init; }

    /// <summary>How many appends <see cref="Append"/> is the median of.</summary>
    public int Appended { get; init; }

    /// <summary>The median span between two state moves of a session, as <c>event_journal</c> holds them: the step an append serves.</summary>
    public TimeSpan? MoveSpan { get; init; }

    /// <summary>The reading as the console prints it.</summary>
    public IReadOnlyList<string> Lines()
    {
        var ages = Ages.Order().ToList();
        string Age(double share) => ages.Count == 0 ? "-" : Span(ages[Math.Min(ages.Count - 1, (int)Math.Floor(share * ages.Count))]);
        string Millis(TimeSpan span) => span.TotalMilliseconds.ToString("0.000", CultureInfo.InvariantCulture);
        return
        [
            $"sessions: {Records} of this machine's, {NotEnded} not ended"
            + (ByState.Count == 0 ? "" : $" ({string.Join(", ", ByState.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key} {pair.Value}"))})"),
            $"leases: {Leases.Proved} proved alive, {Leases.Live} live (their owner runs)",
            $"ages of those not ended: median {Age(0.5)}, 90th percentile {Age(0.9)}, oldest {(ages.Count == 0 ? "-" : Span(ages[^1]))}; "
            + $"{ages.Count(age => age > TimeSpan.FromDays(1))} over a day, {ages.Count(age => age > TimeSpan.FromDays(7))} over a week",
            $"in progress with no process: {InProgressNoProcess}",
            $"not moved for over two hours: {StillOverTwoHours}",
            $"journals: {Journalled} records have one, {Seeded} folded from their status alone; {Unreadable} rows this build cannot read",
            $"state moves no fact witnesses: {Unwitnessed}",
            $"differ: {Differ} ({DifferSeeded} of them folded from their status alone)"
            + (Pairs.Count == 0 ? "" : $": {string.Join(", ", Pairs.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} {pair.Value}"))}"),
            $"the fold of a list of {Folded}: {FoldTook.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)} ms",
            Append is not { } append
                ? "an append: not timed, no record to append to"
                : $"an append: median {Millis(append)} ms over {Appended} on an in-memory copy"
                  + (MoveSpan is { TotalMilliseconds: > 0 } span
                      ? $"; the median span between a session's moves {Span(span)}, so an append adds {(append / span * 100).ToString("0.0000", CultureInfo.InvariantCulture)}% to it"
                      : "; no span between moves to weigh it against"),
            $"buckets of the journalled: {string.Join(", ", Buckets.Where(pair => pair.Value > TimeSpan.Zero).Select(pair => $"{Vocabulary<TimeBucket>.Spell(pair.Key)} {Span(pair.Value)}").DefaultIfEmpty("none"))}",
        ];
    }

    private static string Span(TimeSpan span) => span.TotalDays >= 1
        ? $"{(int)span.TotalDays}d {span.Hours}h"
        : span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m"
        : span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes}m" : $"{span.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)}s";
}

/// <summary>
/// The baseline of a store, read only (<see cref="SessionBaselineReading"/>): it writes nothing and makes no table, so it reads a copy
/// of an installed store, and one older than the journal, whose every record then folds from the fact its status says. Its append is
/// timed on an in-memory copy, never on the store.
/// </summary>
/// <remarks>
/// A record with no journal is folded from its stored status here, where the shadow folds it to none: the baseline measures a store from
/// before the journal, which the shadow never reads. That seeding ends with the switch to the fold, after which every record has a
/// journal from its open.
/// </remarks>
public static class SessionBaseline
{
    /// <summary>How long a live session may go unmoved before it counts as still here.</summary>
    public static readonly TimeSpan StillAfter = TimeSpan.FromHours(2);

    /// <summary>How many appends the copy times.</summary>
    public const int Appends = 200;

    /// <summary>A store opened read only, as the baseline reads it: no statement through it can write the file.</summary>
    public static async Task<SqliteConnection> OpenReadOnlyAsync(string store, CancellationToken ct = default)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = store, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await connection.OpenAsync(ct).ConfigureAwait(false);
        return connection;
    }

    /// <summary>Read <paramref name="store"/> (open) at <paramref name="now"/>, against the leases proved alive, timing a list of <paramref name="count"/>.</summary>
    public static async Task<SessionBaselineReading> ReadAsync(
        SqliteConnection store, IReadOnlyList<SessionLease> leases, DateTimeOffset now, int count = 50, CancellationToken ct = default)
    {
        var gate = ConnectionGate.For(store);
        var reading = await gate.RunAsync(() => ReadAsync(gate, leases, now, count, ct), ct).ConfigureAwait(false);
        var (append, appended) = await AppendCostAsync(store, ct).ConfigureAwait(false);
        return reading with { Append = append, Appended = appended };
    }

    private static async Task<SessionBaselineReading> ReadAsync(
        ConnectionGate store, IReadOnlyList<SessionLease> leases, DateTimeOffset now, int count, CancellationToken ct)
    {
        var records = await RecordsAsync(store, ct).ConfigureAwait(false);
        var journalled = await HasAsync(store, "session_facts", ct).ConfigureAwait(false);
        var generations = await new AppGenerations(store).AllAsync(ct).ConfigureAwait(false);
        var leased = leases
            .GroupBy(lease => lease.Session, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<SessionLease>)[.. group], StringComparer.Ordinal);
        IReadOnlyList<SessionLease> LeasesOf(string session) => leased.GetValueOrDefault(session) ?? [];

        var read = journalled ? await FactsAsync(store, null, ct).ConfigureAwait(false) : new JournalRead(new Dictionary<string, IReadOnlyList<SessionFact>>(), []);
        var journals = read.Journals;
        SessionFact LastOf(Record record) =>
            journals.GetValueOrDefault(record.Id) is { Count: > 0 } facts
                ? facts[^1]
                : SessionFact.OfStatus(record.Id, record.State, record.Updated, record.Parts, new FactWitness(WitnessDoor.Store), 0);

        var live = records.Where(record => SessionFold.OfStored(record.State).State != FoldedState.Ended).ToList();
        var differ = 0;
        var differSeeded = 0;
        var seeded = 0;
        var pairs = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            var own = journals.GetValueOrDefault(record.Id);
            var shadowed = SessionFold.OfStored(record.State).State != FoldedState.Ended || own is { Count: > 0 } && own[^1].Kind != SessionFactKind.Ended;
            if (!shadowed) continue;
            if (own is not { Count: > 0 }) seeded++;
            if (SessionFold.Of(LastOf(record), now, LeasesOf(record.Id)) is not { } folded || !SessionFold.Differs(folded, record.State)) continue;
            differ++;
            if (own is not { Count: > 0 }) differSeeded++;
            var pair = $"{Session.Spell(record.State)} → {Vocabulary<FoldedState>.Spell(folded.State)}";
            pairs[pair] = pairs.GetValueOrDefault(pair) + 1;
        }

        // The list's fold timed as an audit asks it: its last facts read in one query, then each folded.
        var listed = records.OrderByDescending(record => record.Updated).Take(count).ToList();
        var began = Stopwatch.GetTimestamp();
        var lasts = journalled ? await LastFactsAsync(store, [.. listed.Select(record => record.Id)], ct).ConfigureAwait(false) : [];
        foreach (var record in listed) _ = SessionFold.Of(lasts.GetValueOrDefault(record.Id) ?? LastOf(record), now, LeasesOf(record.Id));
        var took = Stopwatch.GetElapsedTime(began);

        var lastFact = journals.Values.SelectMany(facts => facts).Where(fact => fact.Generation > 0)
            .GroupBy(fact => fact.Generation).ToDictionary(group => group.Key, group => group.Max(fact => fact.At));
        var down = SessionBuckets.Down(generations, lastFact);
        var starts = generations.Select(generation => generation.Started).ToList();
        var buckets = Enum.GetValues<TimeBucket>().ToDictionary(bucket => bucket, _ => TimeSpan.Zero);
        foreach (var facts in journals.Values)
        {
            foreach (var (bucket, spent) in SessionBuckets.Of(facts, now, down, starts).Buckets) buckets[bucket] += spent;
        }

        return new(
            records.Count, live.Count, [.. live.Select(record => now - record.Created)],
            live.Count(record => record.State is SessionState.Starting or SessionState.Working
                                 && SessionFold.LeasingOf(LeasesOf(record.Id)) != Leasing.Live),
            live.Count(record => now - record.Updated > StillAfter),
            journals.Count, seeded, differ, differSeeded, pairs, listed.Count, took, buckets)
        {
            ByState = live.GroupBy(record => Session.Spell(record.State), StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            Leases = (leases.Count, leases.Count(lease => lease.OwnerRuns)),
            Unwitnessed = (await SessionJournal.UnwitnessedAsync(store, ct).ConfigureAwait(false)).Count,
            Unreadable = read.Unreadable.Count,
            MoveSpan = await MoveSpanAsync(store, ct).ConfigureAwait(false),
        };
    }

    /// <summary>
    /// The median time of one append (<see cref="SessionJournal.AppendAsync"/>, the fact's insert and the census's lookup), timed on an
    /// in-memory copy of <paramref name="store"/>, so the store is never written.
    /// </summary>
    private static async Task<(TimeSpan? Median, int Count)> AppendCostAsync(SqliteConnection store, CancellationToken ct)
    {
        await using var copy = new SqliteConnection("Data Source=:memory:");
        await copy.OpenAsync(ct).ConfigureAwait(false);
        store.BackupDatabase(copy);
        var sessions = await SessionStore.OpenAsync(copy, ct).ConfigureAwait(false);
        var gate = ConnectionGate.For(copy);
        var id = await gate.RunAsync(async () =>
        {
            await using var command = gate.Command();
            command.CommandText = "SELECT id FROM sessions WHERE origin IS NULL ORDER BY updated DESC LIMIT 1";
            return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
        }, ct).ConfigureAwait(false);
        if (id is null || await sessions.FindAsync(id, ct).ConfigureAwait(false) is not { } record) return (null, 0);

        var times = new List<TimeSpan>(Appends);
        for (var each = 0; each < Appends; each++)
        {
            var began = Stopwatch.GetTimestamp();
            await gate.InTransactionAsync((_, inside) => sessions.Journal.AppendAsync(record, WitnessDoor.Store, minted: false, inside), ct).ConfigureAwait(false);
            times.Add(Stopwatch.GetElapsedTime(began));
        }

        return (times.Order().ElementAt(times.Count / 2), times.Count);
    }

    private sealed record Record(string Id, SessionState State, DateTimeOffset Created, DateTimeOffset Updated, string? Parts);

    /// <exception cref="InvalidDataException">A record whose time this build cannot read: refused, never read as another moment.</exception>
    private static async Task<List<Record>> RecordsAsync(ConnectionGate store, CancellationToken ct)
    {
        await using var command = store.Command();
        command.CommandText = "SELECT id, state, created, updated, note_parts FROM sessions WHERE origin IS NULL";
        var records = new List<Record>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (!Session.TryParse(reader.GetString(1), out var state)) continue;
            DateTimeOffset Moment(int column) => Moments.Read(reader.GetString(column))
                ?? throw new InvalidDataException($"session {reader.GetString(0)} holds '{reader.GetString(column)}' as a time, which this build does not read");
            records.Add(new(reader.GetString(0), state, Moment(2), Moment(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return records;
    }

    private static async Task<bool> HasAsync(ConnectionGate store, string table, CancellationToken ct)
    {
        await using var command = store.Command();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", table);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not null;
    }

    /// <summary>The median span between two state moves of one session, from <c>event_journal</c>; null where it holds none.</summary>
    private static async Task<TimeSpan?> MoveSpanAsync(ConnectionGate store, CancellationToken ct)
    {
        if (!await HasAsync(store, "event_journal", ct).ConfigureAwait(false)) return null;
        await using var command = store.Command();
        command.CommandText = """
            SELECT at - LAG(at) OVER (PARTITION BY record ORDER BY position) FROM event_journal
            WHERE family = 'sessions' AND change IN ('opened', 'state')
            """;
        var spans = new List<long>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (!reader.IsDBNull(0) && reader.GetInt64(0) >= 0) spans.Add(reader.GetInt64(0));
        }

        return spans.Count == 0 ? null : TimeSpan.FromMilliseconds(spans.Order().ElementAt(spans.Count / 2));
    }

    /// <summary>Each of <paramref name="sessions"/>' last facts, as an audit reads them.</summary>
    private static async Task<Dictionary<string, SessionFact>> LastFactsAsync(ConnectionGate store, IReadOnlyList<string> sessions, CancellationToken ct)
    {
        await using var command = store.Command();
        command.CommandText = $"""
            SELECT {SessionJournal.FactColumns} FROM session_facts WHERE position IN (
              SELECT MAX(position) FROM session_facts WHERE session IN (SELECT value FROM json_each($sessions)) GROUP BY session)
            """;
        command.Parameters.AddWithValue("$sessions", System.Text.Json.JsonSerializer.Serialize(sessions));
        var read = await SessionJournal.ReadAsync(command, ct).ConfigureAwait(false);
        return read.Journals.ToDictionary(pair => pair.Key, pair => pair.Value[^1], StringComparer.Ordinal);
    }

    /// <summary>The journals of <paramref name="sessions"/>, or of every record of this machine's where none are named.</summary>
    private static async Task<JournalRead> FactsAsync(ConnectionGate store, IReadOnlyList<string>? sessions, CancellationToken ct)
    {
        await using var command = store.Command();
        command.CommandText = sessions is null
            ? $"SELECT {SessionJournal.FactColumns} FROM session_facts WHERE session IN (SELECT id FROM sessions WHERE origin IS NULL) ORDER BY position"
            : $"SELECT {SessionJournal.FactColumns} FROM session_facts WHERE session IN (SELECT value FROM json_each($sessions)) ORDER BY position";
        if (sessions is not null) command.Parameters.AddWithValue("$sessions", System.Text.Json.JsonSerializer.Serialize(sessions));
        return await SessionJournal.ReadAsync(command, ct).ConfigureAwait(false);
    }
}
