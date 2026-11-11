using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The second check's reports, one file per range in its conversation's decisions folder beside its other decision files, written whole and
/// atomically at each change, and the newest event of each conversation the selection has seen. An observational record: never read as a
/// decision, its name's suffix being its own.
/// </summary>
public sealed class AskDaorisReports(string home)
{
    public const string Restarted = "Daoris restarted";

    private const string Suffix = ".second-check";
    private const string PacketSuffix = ".packet";
    private const string SeenFile = "ask-daoris.seen";

    private static readonly object Gate = new();

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

    /// <summary>Told each report written, with its home and conversation, so the window's readings and its bell are judged again.</summary>
    public static event Action<string, string>? Changed;

    public void Write(AskDaorisReport report)
    {
        Directory.CreateDirectory(FolderOf(report.Session));
        AtomicFile.WriteText(Path.Combine(FolderOf(report.Session), report.Range + Suffix), JsonSerializer.Serialize(report, Options));
        try
        {
            Changed?.Invoke(home, report.Session);
        }
        catch (Exception)
        {
            // A listener's failure is its own; the report is written either way.
        }
    }

    /// <summary>The person's <i>Seen</i> on one flag of a read report, kept once; false where there is no such flag or it is seen already.</summary>
    public bool See(string session, string range, int finding, DateTimeOffset now) =>
        Change(session, range, report =>
            finding >= 0 && finding < report.Findings.Count && report.Findings[finding] is { Severity: AskDaorisFinding.Flag, Seen: null } flag
                ? report with { Findings = [.. report.Findings.Select((each, at) => at == finding ? flag with { Seen = now } : each)] }
                : null) is not null;

    public AskDaorisReport? Find(string session, string range) => ReadFile(Path.Combine(FolderOf(session), range + Suffix));

    /// <summary>One report read, changed and written under the one lock every change takes; null where it is gone or the change declines.</summary>
    public AskDaorisReport? Change(string session, string range, Func<AskDaorisReport, AskDaorisReport?> change)
    {
        lock (Gate)
        {
            if (Find(session, range) is not { } report || change(report) is not { } changed) return null;
            Write(changed);
            return changed;
        }
    }

    public T Locked<T>(Func<T> change)
    {
        lock (Gate)
        {
            return change();
        }
    }

    public void KeepPacket(AskDaorisReport report, string packet)
    {
        Directory.CreateDirectory(FolderOf(report.Session));
        AtomicFile.WriteText(Path.Combine(FolderOf(report.Session), report.Range + PacketSuffix), packet);
    }

    public string? Packet(string session, string range)
    {
        var path = Path.Combine(FolderOf(session), range + PacketSuffix);
        return File.Exists(path) ? AtomicFile.ReadText(path) : null;
    }

    /// <summary>The start's sweep: a range a reader held when the last run ended closes unread, never read again; how many.</summary>
    public int SweepAtStart(DateTimeOffset now, MachineLog? log)
    {
        var running = InState(AskDaorisStates.Running);
        foreach (var report in running)
        {
            Change(report.Session, report.Range, each => each.State == AskDaorisStates.Running ? each with { State = AskDaorisStates.Unread, Closed = now, Why = Restarted } : null);
            log?.Info("ask-daoris.unread", ("range", report.Range), ("why", Restarted));
        }

        return running.Count;
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
