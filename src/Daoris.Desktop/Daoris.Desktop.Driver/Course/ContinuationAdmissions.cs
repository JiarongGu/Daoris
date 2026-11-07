using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>A part of a line as this machine reads it: its session, and its events, null where its log does not read.</summary>
public sealed record HistoryPart(string Session, PartEvents? Events);

/// <summary>A line's parts oldest first under its one key (<see cref="LineKeys.Of"/>), and why its walk stopped short, if it did.</summary>
public sealed record LineHistory(string Line, IReadOnlyList<HistoryPart> Parts, string? Cut);

/// <summary>A line's answers-alone count: exact, or at least where its seed's history is cut or trimmed.</summary>
/// <param name="Since">When its epoch began: the seed's reading, or the word that reset it.</param>
/// <param name="From"><see cref="ContinuationAdmissions.Admission"/> where the store keeps the line; <see cref="ContinuationAdmissions.Seed"/> where none is kept yet.</param>
public sealed record AloneCount(string Line, int N, string Known, string? Why, DateTimeOffset Since, string From);

/// <summary>A go-on judged inside its line's admission: admitted, or held for the person, and the count it is judged at.</summary>
public sealed record AloneAdmission(bool Admitted, AloneCount Count);

/// <summary>One epoch of a line's count: its seed or the reset that opened it, and the count it opened at.</summary>
/// <param name="Source">A reset's word, <c>part:seq</c>; none for the seed.</param>
public sealed record AloneEpoch(int Epoch, string Cause, string? Source, int N, string Known, string? Why, DateTimeOffset Since);

/// <summary>
/// One go-on of a line by answers alone and its disposition: reserved when admitted, attempted once a prompt took
/// its answers, then taken once its agent acted on them, or cancelled where they went beside the person's word. Uncertain is for a
/// delivery that cannot tell whether its attempt reached the agent. Every disposition but cancelled counts.
/// </summary>
/// <param name="Id">Its row; 0 for one not kept yet.</param>
/// <param name="Source">What it goes on by: <c>answers:</c> and its answers' ids, <c>quest:</c> and a waited quest's, or <c>seen:part:seq</c> for one the events showed.</param>
/// <param name="Words">The ids of the answers it took.</param>
public sealed record AloneAdmitted(long Id, int Epoch, string Source, IReadOnlyList<string> Words, string? Session, DateTimeOffset Reserved)
{
    public DateTimeOffset? Attempted { get; init; }

    public DateTimeOffset? Taken { get; init; }

    public DateTimeOffset? Cancelled { get; init; }

    public DateTimeOffset? Uncertain { get; init; }
}

/// <summary>Everything a line's admission keeps: its epochs oldest first, its go-ons, the last event it read of each part, and the prompt it read last.</summary>
public sealed record AloneRows(
    string Line, IReadOnlyList<AloneEpoch> Epochs, IReadOnlyList<AloneAdmitted> Admitted, IReadOnlyDictionary<string, long> Read, AloneScan Carry);

/// <summary>
/// The answers-alone count kept as one admission per line in the desktop store, under the line's one key. A go-on is judged and reserved inside one write, so two answers queued together are never both the second; the <c>at</c> it is
/// judged against is handed in. A store short of the step, or one that does not read or take the write, answers null.
/// </summary>
public sealed class ContinuationAdmissions(string home, Func<StoreSettled>? settled = null, TimeProvider? clock = null)
{
    /// <summary>The family a write moves.</summary>
    public const string Family = "continuation_admissions";

    /// <summary>The store's version the tables arrive at.</summary>
    public const int Step = 20;

    /// <summary>The count is the store's.</summary>
    public const string Admission = "admission";

    /// <summary>An epoch begun at the line's seed; and the count a line with no admission would be seeded at.</summary>
    public const string Seed = "seed";

    /// <summary>An epoch begun at a word of the person's or Daoris's.</summary>
    public const string Reset = "reset";

    // One lock for the process; the store's write lock serializes the processes.
    private static readonly Lock _gate = new();

    private string Location => DesktopStore.LocationIn(home);

    private DateTimeOffset Now => (clock ?? TimeProvider.System).GetUtcNow();

    /// <summary>
    /// The history of the line <paramref name="session"/> is on: the parts its walk reached that no record names, then every record on its
    /// key, oldest first, each part's events as <paramref name="eventsOf"/> reads them.
    /// </summary>
    public static LineHistory History(string session, IReadOnlyDictionary<string, PriorSession> records, Func<string, PartEvents?> eventsOf)
    {
        var key = LineKeys.Of(session, records);
        var parts = key.Parts.Where(part => !records.ContainsKey(part))
            .Concat(LineKeys.Members(key, records).Select(member => member.Session))
            .Distinct(StringComparer.Ordinal);
        return new LineHistory(key.Key, [.. parts.Select(part => new HistoryPart(part, eventsOf(part)))], key.Cut);
    }

    /// <summary>What the store keeps of <paramref name="line"/>; null where it keeps none.</summary>
    public AloneRows? Of(string line)
    {
        try
        {
            using var store = DesktopStoreReader.Open(Location);
            return store is null || store.Version < Step ? null : Read(store, line);
        }
        catch (Exception error) when (Unread(error))
        {
            return null;
        }
    }

    /// <summary>The line's count now, read on over its history in memory, or the seed it would take where none is kept; null without the step.</summary>
    public AloneCount? Count(LineHistory history)
    {
        try
        {
            using var store = DesktopStoreReader.Open(Location);
            if (store is null || store.Version < Step) return null;
            var kept = Read(store, history.Line);
            var count = CountOf(ReadOn(kept, history, Now));
            return kept is null ? count with { From = Seed } : count;
        }
        catch (Exception error) when (Unread(error))
        {
            return null;
        }
    }

    /// <summary>
    /// Judge a go-on of the line by <paramref name="source"/>, taking <paramref name="words"/>, inside its admission: seeded or read on, then
    /// admitted again where it is the same go-on, admitted and reserved where the count is short of <paramref name="at"/> (whatever the count
    /// where none is handed), else held. Written only where something changed. Null where the store does not hold the step or take the write.
    /// </summary>
    public AloneAdmission? Admit(LineHistory history, string source, IReadOnlyList<string> words, string? session, int? at)
    {
        lock (_gate)
        {
            try
            {
                var now = Now;
                using (var reader = DesktopStoreReader.Open(Location))
                {
                    if (reader is not null && reader.Version >= Step && Read(reader, history.Line) is { } kept)
                    {
                        var (answer, judged) = Judge(ReadOn(kept, history, now), source, words, session, at, now);
                        if (!Changed(kept, judged)) return answer;
                    }
                }

                using var store = DesktopStore.Open(Location, StoreProcess.Options(settled ?? StartConfirmation.Read));
                if (store.Stamp.Version < Step) return null;
                return store.Write(Family, write =>
                {
                    var before = Read(write, history.Line);
                    var (answer, after) = Judge(ReadOn(before, history, now), source, words, session, at, now);
                    Keep(write, before, after);
                    return answer;
                });
            }
            catch (Exception error) when (error is DesktopStoreException || Unread(error))
            {
                return null;
            }
        }
    }

    // ——— Pure: the rows read on, judged and counted.

    /// <summary>
    /// The line's rows read on over <paramref name="history"/>: seeded where there are none, else each part's events after the last one read
    /// fed on from the prompt read last. A part not read before is fed only what it kept after the seed: what came before belongs to the seed.
    /// </summary>
    public static AloneRows ReadOn(AloneRows? rows, LineHistory history, DateTimeOffset now)
    {
        if (rows is null) return Seeded(history, now);

        var seeded = rows.Epochs[0].Since;
        var read = new Dictionary<string, long>(rows.Read, StringComparer.Ordinal);
        var fed = new List<(string Part, SessionEvent Event)>();
        foreach (var part in history.Parts)
        {
            if (part.Events is not { } events) continue;
            var known = rows.Read.TryGetValue(part.Session, out var last);
            var fresh = events.Events.Where(e => known ? e.Seq > last : e.At > seeded).ToList();
            if (fresh.Count == 0) continue;
            fed.AddRange(fresh.Select(e => (part.Session, e)));
            read[part.Session] = fresh.Max(e => e.Seq);
        }

        if (fed.Count == 0) return rows;

        var (scan, steps) = AnswersAlone.Read(rows.Carry, fed);
        var epochs = rows.Epochs.ToList();
        var admitted = rows.Admitted.ToList();
        foreach (var step in steps)
        {
            var epoch = epochs[^1].Epoch;
            if (step.Kind == AloneStep.Reset)
            {
                epochs.Add(new AloneEpoch(epoch + 1, Reset, $"{step.Part}:{step.Seq}", 0, LineCounts.Exact, null, step.At));
            }
            else if (step.Kind == AloneStep.GoOn)
            {
                Took(admitted, epoch, step);
            }
            else
            {
                // Beside the person's word: those answers went on with them, so no go-on of theirs went on alone.
                for (var each = 0; each < admitted.Count; each++)
                {
                    if (admitted[each] is { Cancelled: null, Taken: null } row && Overlaps(row.Words, step.Words))
                    {
                        admitted[each] = row with { Attempted = row.Attempted ?? step.At, Cancelled = step.At };
                    }
                }
            }
        }

        // The prompt read last took these answers: on their way to the agent.
        var open = epochs[^1].Epoch;
        for (var each = 0; each < admitted.Count; each++)
        {
            if (admitted[each] is { Attempted: null, Cancelled: null } row && row.Epoch == open && Overlaps(row.Words, scan.Answered))
            {
                admitted[each] = row with { Attempted = now };
            }
        }

        return new AloneRows(rows.Line, epochs, admitted, read, scan);
    }

    /// <summary>The count the rows answer: the epoch's opening count and each go-on in it not cancelled.</summary>
    public static AloneCount CountOf(AloneRows rows)
    {
        var epoch = rows.Epochs[^1];
        var n = epoch.N + rows.Admitted.Count(row => row.Epoch == epoch.Epoch && row.Cancelled is null);
        return new AloneCount(rows.Line, n, epoch.Known, epoch.Why, epoch.Since, Admission);
    }

    /// <summary>
    /// A go-on judged against the rows: the same go-on (its source, a pending one its answers overlap, or a kept one holding all of them, as a
    /// retry does) is admitted again and nothing is counted twice; another is admitted and reserved where the count is short of
    /// <paramref name="at"/>, or whatever the count where none is handed, and held otherwise.
    /// </summary>
    public static (AloneAdmission Answer, AloneRows Rows) Judge(
        AloneRows rows, string source, IReadOnlyList<string> words, string? session, int? at, DateTimeOffset now)
    {
        var epoch = rows.Epochs[^1].Epoch;
        var admitted = rows.Admitted.ToList();
        var same = admitted.FindIndex(row => row.Epoch == epoch && row.Cancelled is null
            && (row.Source == source || (row.Taken is null && Overlaps(row.Words, words)) || (words.Count > 0 && words.All(row.Words.Contains))));
        if (same >= 0)
        {
            var merged = admitted[same].Words.Union(words, StringComparer.Ordinal).ToList();
            if (merged.Count != admitted[same].Words.Count) admitted[same] = admitted[same] with { Words = merged };
            var again = rows with { Admitted = admitted };
            return (new AloneAdmission(true, CountOf(again)), again);
        }

        var count = CountOf(rows);
        if (at is { } bound && count.N >= bound) return (new AloneAdmission(false, count), rows);

        admitted.Add(new AloneAdmitted(0, epoch, source, [.. words], session, now));
        var reserved = rows with { Admitted = admitted };
        return (new AloneAdmission(true, CountOf(reserved)), reserved);
    }

    /// <summary>The line's first rows: its count read whole from the events, and where that reading stopped.</summary>
    private static AloneRows Seeded(LineHistory history, DateTimeOffset now)
    {
        var line = new List<(string Part, SessionEvent Event)>();
        var read = new Dictionary<string, long>(StringComparer.Ordinal);
        var trimmed = false;
        var missing = false;
        foreach (var part in history.Parts)
        {
            // A part whose log does not read ends the line before it, as the count read from the events alone left it.
            if (part.Events is not { } events)
            {
                line.Clear();
                missing = true;
                continue;
            }

            trimmed |= events.Trimmed;
            line.AddRange(events.Events.Select(e => (part.Session, e)));
            if (events.Events.Count > 0) read[part.Session] = events.Events.Max(e => e.Seq);
        }

        var why = history.Cut is not null ? LineCounts.WalkCut : trimmed ? LineCounts.LogTrimmed : missing ? LineCounts.EventsMissing : null;
        var n = AnswersAlone.Count(line.Select(each => each.Event));
        var epoch = new AloneEpoch(0, Seed, null, n, why is null ? LineCounts.Exact : LineCounts.AtLeast, why, now);
        return new AloneRows(history.Line, [epoch], [], read, AnswersAlone.Read(AloneScan.Start, line).Scan);
    }

    /// <summary>
    /// A go-on the events show: it takes the pending admission its answers overlap; a kept one holding all its answers is a retry of it and
    /// counts nothing more; else it takes the oldest pending one, whose answers the prompt named otherwise; else it is counted as shown.
    /// </summary>
    private static void Took(List<AloneAdmitted> admitted, int epoch, AloneStep step)
    {
        var at = admitted.FindIndex(row => row is { Cancelled: null, Taken: null } && row.Epoch == epoch && Overlaps(row.Words, step.Words));
        if (at < 0 && step.Words.Count > 0
            && admitted.Exists(row => row is { Cancelled: null, Taken: not null } && row.Epoch == epoch && step.Words.All(row.Words.Contains)))
        {
            return;
        }

        if (at < 0) at = admitted.FindIndex(row => row is { Cancelled: null, Taken: null } && row.Epoch == epoch);
        if (at >= 0)
        {
            admitted[at] = admitted[at] with { Attempted = admitted[at].Attempted ?? step.At, Taken = step.At };
            return;
        }

        admitted.Add(new AloneAdmitted(0, epoch, $"seen:{step.Part}:{step.Seq}", step.Words, step.Part, step.At) { Attempted = step.At, Taken = step.At });
    }

    private static bool Overlaps(IReadOnlyList<string> some, IReadOnlyList<string> other) => some.Any(id => other.Contains(id, StringComparer.Ordinal));

    // ——— The store's rows.

    private const string LinesSql = "SELECT read, carry FROM continuation_lines WHERE line = $line";

    private const string EpochsSql = "SELECT epoch, cause, source, n, known, why, since FROM continuation_epochs WHERE line = $line ORDER BY epoch";

    private const string AdmittedSql =
        "SELECT id, epoch, source, words, session, reserved, attempted, taken, cancelled, uncertain FROM continuation_admissions WHERE line = $line ORDER BY id";

    private static AloneRows? Read(DesktopStoreReader store, string line) => Rows(
        line, store.Query(LinesSql, LineOf, ("$line", line)), store.Query(EpochsSql, EpochOf, ("$line", line)), store.Query(AdmittedSql, AdmittedOf, ("$line", line)));

    private static AloneRows? Read(DesktopWrite write, string line) => Rows(
        line, write.Query(LinesSql, LineOf, ("$line", line)), write.Query(EpochsSql, EpochOf, ("$line", line)), write.Query(AdmittedSql, AdmittedOf, ("$line", line)));

    private static AloneRows? Rows(
        string line, IReadOnlyList<(IReadOnlyDictionary<string, long> Read, AloneScan Carry)> lines, IReadOnlyList<AloneEpoch> epochs, IReadOnlyList<AloneAdmitted> admitted) =>
        lines.Count == 0 || epochs.Count == 0 ? null : new AloneRows(line, epochs, admitted, lines[0].Read, lines[0].Carry);

    private static (IReadOnlyDictionary<string, long> Read, AloneScan Carry) LineOf(SqliteDataReader row)
    {
        var read = JsonSerializer.Deserialize<Dictionary<string, long>>(row.GetString(0));
        using var carry = JsonDocument.Parse(row.GetString(1));
        var root = carry.RootElement;
        return (
            read is null ? new Dictionary<string, long>(StringComparer.Ordinal) : new Dictionary<string, long>(read, StringComparer.Ordinal),
            new AloneScan(
                root.GetProperty("prompting").GetBoolean(), root.GetProperty("said").GetBoolean(), root.GetProperty("answers").GetBoolean(),
                [.. root.GetProperty("answered").EnumerateArray().Select(each => each.GetString()!)]));
    }

    private static AloneEpoch EpochOf(SqliteDataReader row) => new(
        row.GetInt32(0), row.GetString(1), row.IsDBNull(2) ? null : row.GetString(2), row.GetInt32(3), row.GetString(4),
        row.IsDBNull(5) ? null : row.GetString(5), Moment(row.GetString(6)));

    private static AloneAdmitted AdmittedOf(SqliteDataReader row) =>
        new(row.GetInt64(0), row.GetInt32(1), row.GetString(2), JsonSerializer.Deserialize<string[]>(row.GetString(3)) ?? [],
            row.IsDBNull(4) ? null : row.GetString(4), Moment(row.GetString(5)))
        {
            Attempted = MomentOrNull(row, 6), Taken = MomentOrNull(row, 7), Cancelled = MomentOrNull(row, 8), Uncertain = MomentOrNull(row, 9),
        };

    /// <summary>Write what <paramref name="after"/> holds beyond <paramref name="before"/>: the reading, new epochs, new go-ons and moved dispositions.</summary>
    private static void Keep(DesktopWrite write, AloneRows? before, AloneRows after)
    {
        if (before is not null && !Changed(before, after)) return;

        write.Execute(
            """
            INSERT INTO continuation_lines (line, read, carry) VALUES ($line, $read, $carry)
            ON CONFLICT (line) DO UPDATE SET read = excluded.read, carry = excluded.carry
            """,
            ("$line", after.Line), ("$read", JsonSerializer.Serialize(after.Read)),
            ("$carry", JsonSerializer.Serialize(new { prompting = after.Carry.Prompting, said = after.Carry.Said, answers = after.Carry.Answers, answered = after.Carry.Answered })));

        foreach (var epoch in after.Epochs.Skip(before?.Epochs.Count ?? 0))
        {
            write.Execute(
                "INSERT INTO continuation_epochs (line, epoch, cause, source, n, known, why, since) VALUES ($line, $epoch, $cause, $source, $n, $known, $why, $since)",
                ("$line", after.Line), ("$epoch", epoch.Epoch), ("$cause", epoch.Cause), ("$source", epoch.Source), ("$n", epoch.N),
                ("$known", epoch.Known), ("$why", epoch.Why), ("$since", Moment(epoch.Since)));
        }

        for (var each = 0; each < after.Admitted.Count; each++)
        {
            var row = after.Admitted[each];
            if (row.Id == 0)
            {
                write.Execute(
                    """
                    INSERT INTO continuation_admissions (line, epoch, source, words, session, reserved, attempted, taken, cancelled, uncertain)
                    VALUES ($line, $epoch, $source, $words, $session, $reserved, $attempted, $taken, $cancelled, $uncertain)
                    """,
                    ("$line", after.Line), ("$epoch", row.Epoch), ("$source", row.Source), ("$words", JsonSerializer.Serialize(row.Words)),
                    ("$session", row.Session), ("$reserved", Moment(row.Reserved)), ("$attempted", Moment(row.Attempted)), ("$taken", Moment(row.Taken)),
                    ("$cancelled", Moment(row.Cancelled)), ("$uncertain", Moment(row.Uncertain)));
            }
            else if (!SameRow(row, before!.Admitted[each]))
            {
                write.Execute(
                    "UPDATE continuation_admissions SET words = $words, attempted = $attempted, taken = $taken, cancelled = $cancelled, uncertain = $uncertain WHERE id = $id",
                    ("$id", row.Id), ("$words", JsonSerializer.Serialize(row.Words)), ("$attempted", Moment(row.Attempted)), ("$taken", Moment(row.Taken)),
                    ("$cancelled", Moment(row.Cancelled)), ("$uncertain", Moment(row.Uncertain)));
            }
        }
    }

    private static bool Changed(AloneRows before, AloneRows after) =>
        after.Epochs.Count != before.Epochs.Count
        || after.Admitted.Count != before.Admitted.Count
        || after.Admitted.Where((row, each) => !SameRow(row, before.Admitted[each])).Any()
        || !after.Carry.Same(before.Carry)
        || after.Read.Count != before.Read.Count
        || after.Read.Any(pair => !before.Read.TryGetValue(pair.Key, out var seq) || seq != pair.Value);

    private static bool SameRow(AloneAdmitted one, AloneAdmitted other) =>
        one.Id == other.Id && one.Words.SequenceEqual(other.Words, StringComparer.Ordinal)
        && one.Attempted == other.Attempted && one.Taken == other.Taken && one.Cancelled == other.Cancelled && one.Uncertain == other.Uncertain;

    private static bool Unread(Exception error) => error is SqliteException or IOException or UnauthorizedAccessException or JsonException or FormatException;

    private static string Moment(DateTimeOffset at) => at.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    private static string? Moment(DateTimeOffset? at) => at is { } some ? Moment(some) : null;

    private static DateTimeOffset Moment(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static DateTimeOffset? MomentOrNull(SqliteDataReader row, int column) => row.IsDBNull(column) ? null : Moment(row.GetString(column));
}
