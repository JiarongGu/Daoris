using System.Text.Json;
using static Daoris.Knowledge.JsonFields;

namespace Daoris.Knowledge;

/// <summary>
/// One fact a requirement names as its evidence (EVID1a, D144 point 1): exactly one of a repository-relative
/// <see cref="Path"/> the done's commit must hold, or a <see cref="Gate"/> the receiving repository declares. The
/// check stays the words a person reads; this is the part of it a fact settles. One shape at every door, in the log
/// and on the wire.
/// </summary>
/// <param name="Path">A file or folder, forward slashes, relative to the receiving repository's root.</param>
/// <param name="Gate">A gate's name as the receiving repository's gates file declares it.</param>
public sealed record QuestEvidence(string? Path = null, string? Gate = null)
{
    /// <summary>How many items one requirement names: the facts the check turns on, not an inventory of the work.</summary>
    public const int MaxItems = 5;

    /// <summary>How long a path may be.</summary>
    public const int MaxPathLength = 300;

    /// <summary>How long a gate's name may be.</summary>
    public const int MaxGateLength = 64;

    /// <summary>Its kind's word, as a verdict and a refusal name it: <c>path</c> or <c>gate</c>.</summary>
    public string Kind => Path is not null ? "path" : "gate";

    /// <summary>What it names: the path, or the gate.</summary>
    public string Named => Path ?? Gate ?? "";

    /// <summary>
    /// Why <paramref name="path"/> is not one a commit can be asked for, or null when it is (D144 §2). A path is read by
    /// the driver from git, one argument and never through a shell, so it is judged before it is kept: forward slashes,
    /// relative, inside the tree, and named one way, so the verdict's fact is about the path the person named.
    /// </summary>
    /// <remarks>Also what keeps a machine's path off the wire (D47 §4): an absolute path or a drive is refused here.</remarks>
    public static string? JudgePath(string path)
    {
        if (path.Length == 0) return "it is empty";
        if (path.Length > MaxPathLength) return $"it is longer than {MaxPathLength} characters";
        if (path.Any(char.IsControl)) return "it holds a control character";
        if (path.Contains('\\')) return "it holds a backslash, and a path is written with forward slashes";
        if (path.StartsWith('/')) return "it starts with `/`, and a path is relative to the repository's root";
        if (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':') return "it names a drive, and a path is relative to the repository's root";
        if (path.StartsWith('-')) return "it starts with `-`, which git would read as an option";

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0) return "it holds an empty segment (a doubled or trailing `/`), and a folder is named without one";
            if (segment == ".") return "it holds a `.` segment, and a path is named one way, from the repository's root";
            if (segment == "..") return "it holds a `..` segment, which reaches outside the path it names";
            if (string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase)) return "it reaches into `.git`, which is git's, never the work's";
        }

        return null;
    }

    /// <summary>Why <paramref name="gate"/> is not a gate's name, or null when it is: letters, digits, <c>-</c>, <c>_</c> or <c>.</c>.</summary>
    public static string? JudgeGate(string gate) =>
        gate.Length == 0 ? "it is empty"
        : gate.Length > MaxGateLength ? $"it is longer than {MaxGateLength} characters"
        : gate.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') ? null
        : "a gate's name is letters, digits, `-`, `_` or `.`";

    /// <summary>A requirement's evidence, one shape in the store's column, the log's payload and on the wire.</summary>
    internal static void Write(Utf8JsonWriter writer, IReadOnlyList<QuestEvidence> evidence)
    {
        writer.WriteStartArray();
        foreach (var item in evidence)
        {
            writer.WriteStartObject();
            if (item.Path is not null) writer.WriteString("path", item.Path);
            if (item.Gate is not null) writer.WriteString("gate", item.Gate);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// A requirement's evidence read from JSON another build wrote: null when an item is not an object, names both or
    /// neither, or names a path or gate the exchange would refuse — half of a fact is not one, and a path that is a
    /// machine's would carry that machine across (D47 §4).
    /// </summary>
    internal static IReadOnlyList<QuestEvidence>? Judged(JsonElement items)
    {
        if (items.ValueKind != JsonValueKind.Array) return null;
        var read = new List<QuestEvidence>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) return null;
            var path = Text(item, "path");
            var gate = Text(item, "gate");
            if ((path is null) == (gate is null)) return null;
            if (path is not null && JudgePath(path) is not null) return null;
            if (gate is not null && JudgeGate(gate) is not null) return null;
            read.Add(new QuestEvidence(path, gate));
        }

        return read.Count > MaxItems ? null : read;
    }
}

/// <summary>
/// Why a done is held for the person (DRIFT1d, EVID1a; D133 §4, D144 §6): a departure from what they required, a met
/// answer whose evidence Daoris has not yet read, or one whose evidence it read and did not find.
/// </summary>
public enum QuestHold
{
    /// <summary>The done departed from a requirement: only the person's yes lets it go on.</summary>
    Departed,

    /// <summary>A met answer names evidence nobody has read yet: the driver reads it when the session that made the done ends.</summary>
    EvidenceUnread,

    /// <summary>The evidence was read, and something it names was not in the commit read.</summary>
    EvidenceMissing,
}

/// <summary>The codes a hold and a verdict are written in (LANG1a): codes, never sentences, so every reader words them itself.</summary>
public static class QuestEvidenceCodes
{
    /// <summary>A hold's code, as the doors answer it.</summary>
    public static string Spell(QuestHold hold) => hold switch
    {
        QuestHold.Departed => "departed",
        QuestHold.EvidenceUnread => "evidence-unread",
        _ => "evidence-missing",
    };

    /// <summary>What a path's read may say (D144 §3, §5).</summary>
    public static readonly IReadOnlyList<string> PathResults = ["found", "missing", "uncommitted", "case"];

    /// <summary>What a gate's read may say (D144 §4, §5), read from the landing queue's verdict.</summary>
    public static readonly IReadOnlyList<string> GateResults = ["found", "not-declared", "not-run", "failed", "no-queue"];

    /// <summary>How the commit read was chosen (D144 §3): a session's end, the orphan sweep, or the person at the terminal.</summary>
    public static readonly IReadOnlyList<string> How = ["session-end", "sweep", "terminal"];

    /// <summary>The one result that is the evidence there: every other leaves the done held.</summary>
    public const string Found = "found";

    /// <summary>Whether <paramref name="id"/> is a git object's full id: 40 hex characters, or 64 under SHA-256.</summary>
    public static bool IsObjectId(string? id) => id is { Length: 40 or 64 } && id.All(char.IsAsciiHexDigit);

    /// <summary>Whether <paramref name="session"/> reads as a session's id: at most 64 letters, digits, <c>-</c>, <c>_</c> or <c>.</c>.</summary>
    public static bool IsSessionId(string? session) =>
        session is { Length: > 0 and <= 64 } && session.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}

/// <summary>What one evidence item was found to be, in the commit read (D144 §3, §5): its requirement, what it names, and a code.</summary>
/// <param name="Requirement">The requirement it belongs to: its number, from 1, as the quest lists them.</param>
/// <param name="Path">The path it names — null for a gate.</param>
/// <param name="Gate">The gate it names — null for a path.</param>
/// <param name="Result">One of <see cref="QuestEvidenceCodes.PathResults"/> or <see cref="QuestEvidenceCodes.GateResults"/>.</param>
public sealed record QuestEvidenceRead(int Requirement, string? Path, string? Gate, string Result)
{
    /// <summary>The object id found at the path, where one was.</summary>
    public string? Object { get; init; }

    /// <summary>Whether this work changed it, between the tree's base and the commit read; null where that was not read.</summary>
    public bool? Changed { get; init; }

    /// <summary>For <c>case</c>: the path as the commit spells it, differing only in case — named, and still missing.</summary>
    public string? Spelled { get; init; }

    /// <summary>Whether it names what <paramref name="item"/> names.</summary>
    public bool Names(QuestEvidence item) =>
        string.Equals(Path, item.Path, StringComparison.Ordinal) && string.Equals(Gate, item.Gate, StringComparison.Ordinal);
}

/// <summary>
/// What Daoris read of a done's evidence (EVID1a, D144 points 3 and 6): the commit read and how it was chosen, and each
/// item's code. Kept on the quest in an <see cref="QuestOperationKind.Evidenced"/> operation, which travels like every
/// verb (D68): names and codes only, never bytes or a machine's path (D47 §4).
/// </summary>
/// <param name="Commit">The commit read, by its full id.</param>
/// <param name="How">How it was chosen: one of <see cref="QuestEvidenceCodes.How"/>.</param>
/// <param name="Items">One read for each item of each met requirement that names evidence.</param>
public sealed record QuestEvidenceVerdict(string Commit, string How, IReadOnlyList<QuestEvidenceRead> Items)
{
    /// <summary>The session whose end was read — at a session's end or by the sweep; null for the terminal's check.</summary>
    public string? Session { get; init; }

    /// <summary>When it was read: the operation's time, set on the quest as the replay keeps it.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>The machine that read it: the operation's, set on the quest as the replay keeps it.</summary>
    public string? Machine { get; init; }

    /// <summary>Whether every item was found: the one verdict that lets what the done held go on.</summary>
    public bool Found => Items.Count > 0 && Items.All(item => item.Result == QuestEvidenceCodes.Found);

    /// <summary>
    /// Whether it reads exactly what <paramref name="quest"/> waits on (D144 §3): one read for each item of each met
    /// requirement that names evidence, and nothing else.
    /// </summary>
    public bool Covers(Quest quest)
    {
        var wanted = quest.EvidenceWanted;
        if (wanted.Count != Items.Count) return false;
        var matched = new HashSet<int>();
        foreach (var (requirement, item) in wanted)
        {
            var index = Items.ToList().FindIndex(read => read.Requirement == requirement && read.Names(item));
            if (index < 0 || !matched.Add(index)) return false;
        }

        return true;
    }

    public bool Equals(QuestEvidenceVerdict? other) =>
        other is not null && Commit == other.Commit && How == other.How && Session == other.Session && At == other.At
        && Machine == other.Machine && Items.SequenceEqual(other.Items);

    public override int GetHashCode() => HashCode.Combine(Commit, How, Session, At, Machine, Items.Count);

    /// <summary>
    /// Its one shape in the log's payload, the quest's column and on the wire. When and which machine are the
    /// operation's own, so they are written only in the column, which holds the verdict as the quest stands.
    /// </summary>
    internal void Write(Utf8JsonWriter writer, bool standing = false)
    {
        writer.WriteStartObject();
        writer.WriteString("commit", Commit);
        writer.WriteString("how", How);
        if (Session is not null) writer.WriteString("session", Session);
        if (standing && At is { } at) writer.WriteString("at", at.ToString("O"));
        if (standing && Machine is not null) writer.WriteString("machine", Machine);
        writer.WriteStartArray("items");
        foreach (var item in Items)
        {
            writer.WriteStartObject();
            writer.WriteNumber("requirement", item.Requirement);
            if (item.Path is not null) writer.WriteString("path", item.Path);
            if (item.Gate is not null) writer.WriteString("gate", item.Gate);
            writer.WriteString("result", item.Result);
            if (item.Object is not null) writer.WriteString("object", item.Object);
            if (item.Changed is { } changed) writer.WriteBoolean("changed", changed);
            if (item.Spelled is not null) writer.WriteString("spelled", item.Spelled);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>
    /// A verdict read from JSON, judged as the wire judges what it reads: null when it is not whole — no full commit, a
    /// way of reading or a result nobody wrote, an item naming both or neither, or a path or object that is not one.
    /// The store's own column is read through the same judge, since it only ever holds what passed it.
    /// </summary>
    internal static QuestEvidenceVerdict? Judged(JsonElement verdict)
    {
        if (verdict.ValueKind != JsonValueKind.Object
            || Text(verdict, "commit") is not { } commit || !QuestEvidenceCodes.IsObjectId(commit)
            || Text(verdict, "how") is not { } how || !QuestEvidenceCodes.How.Contains(how)
            || !verdict.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var session = Text(verdict, "session");
        if (verdict.TryGetProperty("session", out _) && !QuestEvidenceCodes.IsSessionId(session)) return null;

        var read = new List<QuestEvidenceRead>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || Number(item, "requirement") is not { } requirement || requirement is < 1 or > int.MaxValue
                || Text(item, "result") is not { } result)
            {
                return null;
            }

            var path = Text(item, "path");
            var gate = Text(item, "gate");
            if ((path is null) == (gate is null)) return null;
            if (path is not null && (QuestEvidence.JudgePath(path) is not null || !QuestEvidenceCodes.PathResults.Contains(result))) return null;
            if (gate is not null && (QuestEvidence.JudgeGate(gate) is not null || !QuestEvidenceCodes.GateResults.Contains(result))) return null;

            var found = Text(item, "object");
            if (item.TryGetProperty("object", out _) && !QuestEvidenceCodes.IsObjectId(found)) return null;
            var spelled = Text(item, "spelled");
            if (item.TryGetProperty("spelled", out _) && (spelled is null || QuestEvidence.JudgePath(spelled) is not null)) return null;
            bool? changed = null;
            if (item.TryGetProperty("changed", out var flag))
            {
                if (flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return null;
                changed = flag.ValueKind == JsonValueKind.True;
            }

            read.Add(new QuestEvidenceRead((int)requirement, path, gate, result) { Object = found, Changed = changed, Spelled = spelled });
        }

        DateTimeOffset? at = Text(verdict, "at") is { } when && DateTimeOffset.TryParse(when, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
        return new QuestEvidenceVerdict(commit.ToLowerInvariant(), how, read)
        {
            Session = session, At = at, Machine = Text(verdict, "machine"),
        };
    }
}
