using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One range of a conversation's turns, as the second check keeps it: its turns, its state (<see cref="Queued"/> while it may grow,
/// <see cref="Running"/> once a reader took it, then read, left, <see cref="Unread"/> with why, or <see cref="Disabled"/> while the check
/// is off) and the limits of what a reading of it is shown. Every state the window shows derives from it.
/// </summary>
public sealed record AskDaorisReport(
    string Range, string Session, string State, DateTimeOffset Opened, IReadOnlyList<ReadTurn> Turns, IReadOnlyList<ReadLimitAt> Limits)
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Read = "read";
    public const string Left = "left";
    public const string Unread = "unread";
    public const string Disabled = "disabled";

    public DateTimeOffset? Closed { get; init; }

    /// <summary>Why it closed unread; null for every other state.</summary>
    public string? Why { get; init; }

    /// <summary>About how many characters its packet takes.</summary>
    public int Size => Turns.Sum(turn => turn.Size);
}

/// <summary>
/// The second check's reports, one file per range in its conversation's decisions folder beside its other decision files, written whole and
/// atomically at each change, and the newest event of each conversation the selection has seen. An observational record: never read as a
/// decision, its name's suffix being its own.
/// </summary>
public sealed class AskDaorisReports(string home)
{
    private const string Suffix = ".second-check";
    private const string SeenFile = "ask-daoris.seen";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private string FolderOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + QuestionSupervisorDecisions.DecisionsSuffix);

    /// <summary>A conversation's reports, oldest first; one that does not read is passed over.</summary>
    public IReadOnlyList<AskDaorisReport> Of(string session)
    {
        var folder = FolderOf(session);
        if (!Directory.Exists(folder)) return [];
        return [.. Directory.EnumerateFiles(folder, "*" + Suffix).Select(ReadFile).OfType<AskDaorisReport>().OrderBy(report => report.Opened).ThenBy(report => report.Range, StringComparer.Ordinal)];
    }

    /// <summary>Every conversation's reports in <paramref name="state"/>.</summary>
    public IReadOnlyList<AskDaorisReport> InState(string state)
    {
        var sessions = DaorisHome.Entry(home, HomeEntry.Sessions);
        if (!Directory.Exists(sessions)) return [];
        return [.. Directory.EnumerateDirectories(sessions, "*" + QuestionSupervisorDecisions.DecisionsSuffix)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*" + Suffix)).Select(ReadFile).OfType<AskDaorisReport>().Where(report => report.State == state)];
    }

    public void Write(AskDaorisReport report)
    {
        Directory.CreateDirectory(FolderOf(report.Session));
        AtomicFile.WriteText(Path.Combine(FolderOf(report.Session), report.Range + Suffix), JsonSerializer.Serialize(report, Options));
    }

    /// <summary>The newest event of <paramref name="session"/>'s log the selection has seen; null for a conversation it never saw.</summary>
    public long? Seen(string session)
    {
        var path = Path.Combine(FolderOf(session), SeenFile);
        return File.Exists(path) && long.TryParse(AtomicFile.ReadText(path).Trim(), System.Globalization.CultureInfo.InvariantCulture, out var seq) ? seq : null;
    }

    public void MarkSeen(string session, long seq)
    {
        Directory.CreateDirectory(FolderOf(session));
        AtomicFile.WriteText(Path.Combine(FolderOf(session), SeenFile), seq.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static AskDaorisReport? ReadFile(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<AskDaorisReport>(AtomicFile.ReadText(path), Options);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
