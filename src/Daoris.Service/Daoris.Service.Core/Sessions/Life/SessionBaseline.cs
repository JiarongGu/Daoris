using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// What a store says before the fold's trial: its records, those not ended and their ages, those shown in progress with no
/// process, those not moved for over two hours, those the fold says other than the status, the fold's time for a list, and the time
/// buckets of the journalled. Counts and times only: no id, note or name.
/// </summary>
/// <param name="Records">This machine's records (a teammate's mirrored ones left out).</param>
/// <param name="NotEnded">Those whose stored status is live.</param>
/// <param name="Ages">The age of each not ended, since its making.</param>
/// <param name="InProgressNoProcess">Those stored starting or working with no live lease of this generation.</param>
/// <param name="StillOverTwoHours">Those not ended whose status has not moved for over two hours.</param>
/// <param name="Journalled">Those with a journal.</param>
/// <param name="Seeded">Those folded with no journal, from the one fact their stored status says.</param>
/// <param name="Differ">Those folded whose fold says other than their status.</param>
/// <param name="DifferSeeded">Of <see cref="Differ"/>, those folded from their status alone.</param>
/// <param name="Pairs">The differences by stored state and fold, as <c>stored → fold</c>.</param>
/// <param name="Folded">How many sessions the timed list folded.</param>
/// <param name="FoldTook">How long the list took: its journals read and each folded.</param>
/// <param name="Buckets">The time buckets summed over every journalled record.</param>
public sealed record SessionBaselineReading(
    int Records, int NotEnded, IReadOnlyList<TimeSpan> Ages, int InProgressNoProcess, int StillOverTwoHours, int Journalled, int Seeded,
    int Differ, int DifferSeeded, IReadOnlyDictionary<string, int> Pairs, int Folded, TimeSpan FoldTook,
    IReadOnlyDictionary<TimeBucket, TimeSpan> Buckets)
{
    /// <summary>The reading as the console prints it.</summary>
    public IReadOnlyList<string> Lines()
    {
        var ages = Ages.Order().ToList();
        string Age(double share) => ages.Count == 0 ? "-" : Span(ages[Math.Min(ages.Count - 1, (int)Math.Floor(share * ages.Count))]);
        return
        [
            $"sessions: {Records} of this machine's, {NotEnded} not ended",
            $"ages of those not ended: median {Age(0.5)}, 90th percentile {Age(0.9)}, oldest {(ages.Count == 0 ? "-" : Span(ages[^1]))}; "
            + $"{ages.Count(age => age > TimeSpan.FromDays(1))} over a day, {ages.Count(age => age > TimeSpan.FromDays(7))} over a week",
            $"in progress with no process: {InProgressNoProcess}",
            $"not moved for over two hours: {StillOverTwoHours}",
            $"journals: {Journalled} records have one, {Seeded} folded from their status alone",
            $"differ: {Differ} ({DifferSeeded} of them folded from their status alone)"
            + (Pairs.Count == 0 ? "" : $": {string.Join(", ", Pairs.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} {pair.Value}"))}"),
            $"the fold of a list of {Folded}: {FoldTook.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)} ms",
            $"buckets of the journalled: {string.Join(", ", Buckets.Where(pair => pair.Value > TimeSpan.Zero).Select(pair => $"{SessionBuckets.Spell(pair.Key)} {Span(pair.Value)}").DefaultIfEmpty("none"))}",
        ];
    }

    private static string Span(TimeSpan span) => span.TotalDays >= 1
        ? $"{(int)span.TotalDays}d {span.Hours}h"
        : span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m" : $"{(int)span.TotalMinutes}m";
}

/// <summary>
/// The baseline of a store, read only (<see cref="SessionBaselineReading"/>): it writes nothing and makes no table, so it reads a copy
/// of an installed store, and one older than the journal, whose every record then folds from the fact its status says.
/// </summary>
public static class SessionBaseline
{
    /// <summary>How long a live session may go unmoved before it counts as still here.</summary>
    public static readonly TimeSpan StillAfter = TimeSpan.FromHours(2);

    /// <summary>Read <paramref name="store"/> (open) at <paramref name="now"/>, against the processes proved alive, timing a list of <paramref name="count"/>.</summary>
    public static async Task<SessionBaselineReading> ReadAsync(
        SqliteConnection store, IReadOnlyList<ShadowLease> leases, DateTimeOffset now, int count = 50, CancellationToken ct = default)
    {
        var records = await RecordsAsync(store, ct).ConfigureAwait(false);
        var journalled = await HasAsync(store, "session_facts", ct).ConfigureAwait(false);
        var generations = await HasAsync(store, "app_generations", ct).ConfigureAwait(false) ? await GenerationsAsync(store, ct).ConfigureAwait(false) : [];
        var generation = generations.Count == 0 ? 0 : generations[^1].Number;
        var leased = leases
            .Select(lease => new SessionLease(lease.Session, AppGenerations.Of(lease.Started, generations)))
            .GroupBy(lease => lease.Session, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<SessionLease>)[.. group], StringComparer.Ordinal);
        IReadOnlyList<SessionLease> LeasesOf(string session) => leased.GetValueOrDefault(session) ?? [];

        var journals = journalled ? await FactsAsync(store, null, ct).ConfigureAwait(false) : new Dictionary<string, IReadOnlyList<SessionFact>>();
        IReadOnlyList<SessionFact> JournalOf(Record record) =>
            journals.GetValueOrDefault(record.Id) is { Count: > 0 } facts ? facts : [SessionFact.OfStatus(record.Id, record.State, record.Updated, record.Parts, "baseline", 0)];

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
            if (SessionFold.Of(JournalOf(record), now, LeasesOf(record.Id), generation) is not { } folded || !SessionFold.Differs(folded, record.State)) continue;
            differ++;
            if (own is not { Count: > 0 }) differSeeded++;
            var pair = $"{Session.Spell(record.State)} → {SessionFold.Spell(folded.State)}";
            pairs[pair] = pairs.GetValueOrDefault(pair) + 1;
        }

        // The list's fold timed as a read asks it: its journals read in one query, then each folded.
        var listed = records.OrderByDescending(record => record.Updated).Take(count).ToList();
        var began = Stopwatch.GetTimestamp();
        var read = journalled ? await FactsAsync(store, [.. listed.Select(record => record.Id)], ct).ConfigureAwait(false) : journals;
        foreach (var record in listed)
        {
            var facts = read.GetValueOrDefault(record.Id) is { Count: > 0 } own ? own : JournalOf(record);
            _ = SessionFold.Of(facts, now, LeasesOf(record.Id), generation);
        }

        var took = Stopwatch.GetElapsedTime(began);

        var lastSeen = journals.Values.SelectMany(facts => facts).GroupBy(fact => fact.Generation).ToDictionary(group => group.Key, group => group.Max(fact => fact.At));
        var down = SessionBuckets.Down(generations, lastSeen);
        var buckets = Enum.GetValues<TimeBucket>().ToDictionary(bucket => bucket, _ => TimeSpan.Zero);
        foreach (var facts in journals.Values)
        {
            foreach (var (bucket, spent) in SessionBuckets.Of(facts, now, down).Buckets) buckets[bucket] += spent;
        }

        return new(
            records.Count, live.Count, [.. live.Select(record => now - record.Created)],
            live.Count(record => record.State is SessionState.Starting or SessionState.Working
                                 && !LeasesOf(record.Id).Any(lease => lease.Generation == generation)),
            live.Count(record => now - record.Updated > StillAfter),
            journals.Count, seeded, differ, differSeeded, pairs, listed.Count, took, buckets);
    }

    private sealed record Record(string Id, SessionState State, DateTimeOffset Created, DateTimeOffset Updated, string? Parts);

    private static async Task<List<Record>> RecordsAsync(SqliteConnection store, CancellationToken ct)
    {
        await using var command = store.CreateCommand();
        command.CommandText = "SELECT id, state, created, updated, note_parts FROM sessions WHERE origin IS NULL";
        var records = new List<Record>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (!Session.TryParse(reader.GetString(1), out var state)) continue;
            records.Add(new(
                reader.GetString(0), state, Moments.Read(reader.GetString(2)) ?? DateTimeOffset.MinValue,
                Moments.Read(reader.GetString(3)) ?? DateTimeOffset.MinValue, reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return records;
    }

    private static async Task<bool> HasAsync(SqliteConnection store, string table, CancellationToken ct)
    {
        await using var command = store.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", table);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not null;
    }

    private static async Task<IReadOnlyList<AppGeneration>> GenerationsAsync(SqliteConnection store, CancellationToken ct)
    {
        await using var command = store.CreateCommand();
        command.CommandText = "SELECT generation, started FROM app_generations ORDER BY generation";
        var all = new List<AppGeneration>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            all.Add(new(reader.GetInt64(0), Moments.Read(reader.GetString(1)) ?? DateTimeOffset.MinValue));
        }

        return all;
    }

    /// <summary>The journals of <paramref name="sessions"/>, or of every record of this machine's where none are named.</summary>
    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<SessionFact>>> FactsAsync(
        SqliteConnection store, IReadOnlyList<string>? sessions, CancellationToken ct)
    {
        await using var command = store.CreateCommand();
        command.CommandText = sessions is null
            ? $"SELECT {SessionJournal.FactColumns} FROM session_facts WHERE session IN (SELECT id FROM sessions WHERE origin IS NULL) ORDER BY position"
            : $"SELECT {SessionJournal.FactColumns} FROM session_facts WHERE session IN (SELECT value FROM json_each($sessions)) ORDER BY position";
        if (sessions is not null) command.Parameters.AddWithValue("$sessions", System.Text.Json.JsonSerializer.Serialize(sessions));
        return await SessionJournal.ReadAsync(command, ct).ConfigureAwait(false);
    }
}
