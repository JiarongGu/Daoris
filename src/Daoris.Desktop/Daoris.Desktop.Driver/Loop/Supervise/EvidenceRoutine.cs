using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Where the routine reads a day's failures: runs ended by a stop, waits with no holder past their deadline, the merge queue's handed-back reds, review HIGH findings, refused acts.</summary>
public interface IEvidenceRecords
{
    Task<IReadOnlyList<EvidenceFailure>> ReadAsync(DateOnly day, CancellationToken ct);
}

/// <summary>The records door an accepted class row reaches the backlog through; the routine never writes the backlog's file itself.</summary>
public interface IEvidenceBacklog
{
    /// <summary>Whether the row was taken.</summary>
    Task<bool> AppendAsync(EvidenceRow row, CancellationToken ct);
}

/// <summary>A day's list as the routine keeps it: the day, the rows proposed, and each class's state (<c>proposed</c>, <c>accepted</c>).</summary>
public sealed record EvidenceDay(DateOnly Day, IReadOnlyList<EvidenceRow> Rows, IReadOnlyDictionary<string, string> States);

/// <summary>
/// The evidence routine on the look (<see cref="EvidenceDays"/>): once a day after its hour, or at once on the steward's word, it reads the
/// day's failures from the records, proposes one row per class not declined, and keeps them under the home for the steward role to accept
/// or decline. An accepted row goes to the backlog through the records door alone; a declined class is kept so no later day proposes it.
/// </summary>
public sealed class EvidenceRoutine(string home, IEvidenceRecords records)
{
    public const string Proposed = "proposed";
    public const string Accepted = "accepted";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private string Folder => DaorisHome.Entry(home, HomeEntry.Evidence);

    private string DayFile(DateOnly day) => Path.Combine(Folder, $"{day:yyyy-MM-dd}.json");

    private string DeclinedFile => Path.Combine(Folder, "declined.json");

    /// <summary>The day's list where it is due, kept; null where it is not due.</summary>
    public async Task<EvidenceDay?> RunAsync(DateTimeOffset now, bool word, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (!EvidenceDays.Due(now, Read(today) is null ? null : today, word)) return null;
        var failures = await records.ReadAsync(today, ct).ConfigureAwait(false);
        var rows = EvidenceDays.Propose(now, failures, Declined());
        var kept = Read(today)?.States ?? new Dictionary<string, string>();
        var states = rows.ToDictionary(row => row.Class, row => kept.GetValueOrDefault(row.Class) ?? Proposed, StringComparer.Ordinal);
        var day = new EvidenceDay(today, rows, states);
        Write(DayFile(today), day);
        return day;
    }

    /// <summary>The steward role's accept: the row appended through the records door once; false where it is not proposed or the door refused it.</summary>
    public async Task<bool> AcceptAsync(DateOnly day, string @class, IEvidenceBacklog backlog, CancellationToken ct)
    {
        if (Read(day) is not { } kept || kept.States.GetValueOrDefault(@class) != Proposed || kept.Rows.FirstOrDefault(row => row.Class == @class) is not { } row) return false;
        if (!await backlog.AppendAsync(row, ct).ConfigureAwait(false)) return false;
        Write(DayFile(day), kept with { States = new Dictionary<string, string>(kept.States, StringComparer.Ordinal) { [@class] = Accepted } });
        return true;
    }

    /// <summary>The steward role's decline: the class kept as declined, so no later list proposes it.</summary>
    public void Decline(DateOnly day, string @class)
    {
        Write(DeclinedFile, Declined().Append(@class).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList());
        if (Read(day) is { } kept)
        {
            Write(DayFile(day), kept with { Rows = [.. kept.Rows.Where(row => row.Class != @class)], States = kept.States.Where(each => each.Key != @class).ToDictionary(StringComparer.Ordinal) });
        }
    }

    /// <summary>A day's kept list; null where none is kept or it does not read.</summary>
    public EvidenceDay? Read(DateOnly day) => Load<EvidenceDay>(DayFile(day));

    private HashSet<string> Declined() => [.. Load<List<string>>(DeclinedFile) ?? []];

    private static T? Load<T>(string file) where T : class
    {
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<T>(File.ReadAllText(file), Options) : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private static void Write<T>(string file, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temporary = file + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options));
        File.Move(temporary, file, overwrite: true);
    }
}
