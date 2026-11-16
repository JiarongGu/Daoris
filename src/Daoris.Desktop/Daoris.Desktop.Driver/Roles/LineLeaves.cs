using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One leave on the reporting line: the level whose holder left the subject (<see cref="LineLevels"/>), its role, the bound and the reading.</summary>
public sealed record LineLeave(DateTimeOffset At, string Level, string? Role, string? Bound, string Reading);

/// <summary>
/// The leaves kept on each supervisor's subject, every kind's (D304 point 3): <c>line.leaves</c> in the subject's session's decisions folder,
/// one JSON line per leave, appended and never rewritten, each naming the level that made it. The vacancy kind keeps its own beside its gate
/// (<see cref="OpinionCasts"/>), and a subject that names no session (a proposed widening) keeps none: its leave is final as before.
/// </summary>
/// <remarks>An older build reads no such file and keeps each leave final, the person's, as before. A line that does not read is passed over.</remarks>
public sealed class LineLeaves(string home)
{
    private const string FileName = "line.leaves";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    // One writer at a time in this process: the door and an agent's desk may leave at once.
    private static readonly object Gate = new();

    /// <summary>Where a session's leaves are kept; null for a session that is no plain id.</summary>
    public string? PathOf(string session) =>
        SafeNames.IsPlainId(session) ? Path.Combine(DaorisHome.Entry(home, HomeEntry.Sessions, session + QuestionSupervisorDecisions.DecisionsSuffix), FileName) : null;

    /// <summary>Whether a leave on <paramref name="subject"/> can be kept: it names a session.</summary>
    public bool Keeps(SupervisorSubject subject) => PathOf(subject.Session) is not null;

    /// <summary>A leave on <paramref name="subject"/>, kept after every one before it.</summary>
    public void Keep(SupervisorSubject subject, LineLeave leave)
    {
        if (PathOf(subject.Session) is not { } path) throw new DriverException($"`{subject.Session}` names no session whose leaves this machine keeps.");
        var line = JsonSerializer.Serialize(new Kept(subject.Kind, subject.Id, subject.Held, leave.At, leave.Level, leave.Role, leave.Bound, leave.Reading), Options);
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, line + "\n", new UTF8Encoding(false));
        }
    }

    /// <summary>The leaves on <paramref name="subject"/>, in the order kept; none where nothing reads.</summary>
    public IReadOnlyList<LineLeave> Of(SupervisorSubject subject)
    {
        if (PathOf(subject.Session) is not { } path || !File.Exists(path)) return [];
        string[] lines;
        lock (Gate) lines = File.ReadAllLines(path);
        return [.. lines.Select(Parse).OfType<Kept>()
            .Where(kept => kept.Kind == subject.Kind && kept.Id == subject.Id && kept.Held == subject.Held && kept.Level is not null)
            .Select(kept => new LineLeave(kept.At, kept.Level!, kept.Role, kept.Bound, kept.Reading ?? ""))];
    }

    private static Kept? Parse(string line)
    {
        try
        {
            return string.IsNullOrWhiteSpace(line) ? null : JsonSerializer.Deserialize<Kept>(line, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Kept(string? Kind, string? Id, string? Held, DateTimeOffset At, string? Level, string? Role, string? Bound, string? Reading);
}

/// <summary>
/// Where a subject waits now, read with <see cref="ReportingLine.Climb"/> over the levels that left it (D304 point 3): its place's supervisor
/// until that level left it, then the machine's; only an owner-class bound reaches the owner role, and other help the machine's supervisor
/// leaves, or that finds no supervisor above, ends as gave up with what was tried.
/// </summary>
public static class LineClimb
{
    /// <summary>The levels that left, each with its newest reading, keyed by level.</summary>
    public static IReadOnlyDictionary<string, string> Left(IReadOnlyList<LineLeave> leaves)
    {
        var left = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var leave in leaves) left[leave.Level] = leave.Reading;
        return left;
    }

    /// <summary>Whether a leave named an owner-class bound (doctrine rule 1's list), which passes every level to the owner role.</summary>
    public static bool OwnerClassed(IReadOnlyList<LineLeave> leaves) => leaves.Any(leave => OwnerClass.Is(leave.Bound));

    /// <summary>Where help waits on <paramref name="line"/> once <paramref name="leaves"/> were made.</summary>
    public static LineWait Wait(ReportingLine line, IReadOnlyList<LineLeave> leaves) => line.Climb(OwnerClassed(leaves), Left(leaves));

    /// <summary>
    /// The place whose supervisor lists the subject: <paramref name="place"/> where nobody left it (the admission's own reading), the machine
    /// once the climb waits on the machine's supervisor held by an agent or a terminal, none once it waits on the person or gave up.
    /// </summary>
    public static RolePlace? Place(ReportingLine line, RolePlace place, IReadOnlyList<LineLeave> leaves)
    {
        if (leaves.Count == 0) return place;
        return Wait(line, leaves).At switch
        {
            { Holder: LineHolders.Agent or LineHolders.Terminal, Level: var level } => LineLevels.IsBelowMachine(level) ? place : new RolePlace(),
            _ => null,
        };
    }
}
