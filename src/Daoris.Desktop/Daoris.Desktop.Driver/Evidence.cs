using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One fact a requirement names as its evidence (EVID1a, D144 point 1), as the service answers it: exactly one of a
/// repository-relative <see cref="Path"/> the done's commit must hold, or a <see cref="Gate"/> the receiving repository
/// declares. The check stays the person's words; this is the part of it Daoris reads itself (EVID1b).
/// </summary>
public sealed record QuestEvidenceItem(string? Path, string? Gate = null)
{
    /// <summary>Its kind's word, as a verdict names it: <c>path</c> or <c>gate</c>.</summary>
    public string Kind => Path is not null ? "path" : "gate";

    /// <summary>What it names: the path, or the gate.</summary>
    public string Named => Path ?? Gate ?? "";
}

/// <summary>What a done waits on (EVID1a, D144 §3): one item of one requirement it answered met, by the requirement's number.</summary>
public sealed record WantedEvidence(int Requirement, QuestEvidenceItem Item);

/// <summary>What one evidence item was found to be in the commit read (D144 §3, §5): its requirement, what it names, and a code.</summary>
/// <param name="Requirement">The requirement it belongs to: its number, from 1, as the quest lists them.</param>
/// <param name="Result">One of <see cref="EvidenceCodes.PathResults"/> or <see cref="EvidenceCodes.GateResults"/>.</param>
public sealed record EvidenceRead(int Requirement, string? Path, string? Gate, string Result)
{
    /// <summary>The object id found at the path, where one was.</summary>
    public string? Object { get; init; }

    /// <summary>Whether this work changed it, between the tree's base and the commit read; null where that was not read.</summary>
    public bool? Changed { get; init; }

    /// <summary>For <c>case</c>: the path as the commit spells it, differing only in case. Still missing.</summary>
    public string? Spelled { get; init; }

    /// <summary>What it names: the path, or the gate.</summary>
    public string Named => Path ?? Gate ?? "";
}

/// <summary>
/// What Daoris read of a done's evidence (EVID1a, EVID1b; D144 §3, §5): the commit read and how it was chosen, the session
/// whose end was read, and each item's code. The body the evidence door takes, and what a quest answers it last kept.
/// </summary>
/// <param name="Commit">The commit read, by its full id (40 or 64 hex characters).</param>
/// <param name="How">How it was chosen: one of <see cref="EvidenceCodes.How"/>.</param>
public sealed record EvidenceVerdict(string Commit, string How, IReadOnlyList<EvidenceRead> Items)
{
    /// <summary>The session whose end was read: at a session's end or by the sweep; null for the terminal's check.</summary>
    public string? Session { get; init; }

    /// <summary>When it was read, as the quest answers it; null on a verdict not yet kept.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>The machine that read it, as the quest answers it; null on a verdict not yet kept.</summary>
    public string? Machine { get; init; }

    /// <summary>Whether every item was found: the one verdict that lets what the done held go on.</summary>
    public bool Found => Items.Count > 0 && Items.All(item => item.Result == EvidenceCodes.Found);

    /// <summary>How many items read <paramref name="result"/>.</summary>
    public int Count(string result) => Items.Count(item => item.Result == result);

    public bool Equals(EvidenceVerdict? other) =>
        other is not null && Commit == other.Commit && How == other.How && Session == other.Session && At == other.At
        && Machine == other.Machine && Items.SequenceEqual(other.Items);

    public override int GetHashCode() => HashCode.Combine(Commit, How, Session, Items.Count);

    /// <summary>
    /// The body <c>POST /api/quests/{id}/evidence</c> takes (EVID1a): the commit, how, the session where there is one, and
    /// each item with its code, and its object, whether the work changed it and its spelling where each was read.
    /// </summary>
    public string Json()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("commit", Commit);
            writer.WriteString("how", How);
            if (Session is not null) writer.WriteString("session", Session);
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

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// A verdict as a quest answers it (<c>evidence</c>), or null where it answers none or one that is not whole: no full
    /// commit, a way of reading nobody wrote, or an item with no requirement, no result, or naming both or neither.
    /// </summary>
    internal static EvidenceVerdict? Read(JsonElement quest)
    {
        if (quest.ValueKind != JsonValueKind.Object || !quest.TryGetProperty("evidence", out var verdict)
            || verdict.ValueKind != JsonValueKind.Object
            || Text(verdict, "commit") is not { } commit || !EvidenceCodes.IsObjectId(commit)
            || Text(verdict, "how") is not { } how || !EvidenceCodes.How.Contains(how)
            || !verdict.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var read = new List<EvidenceRead>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("requirement", out var number) || number.ValueKind != JsonValueKind.Number
                || !number.TryGetInt32(out var requirement)
                || Text(item, "result") is not { } result)
            {
                return null;
            }

            var path = Text(item, "path");
            var gate = Text(item, "gate");
            if ((path is null) == (gate is null)) return null;
            read.Add(new EvidenceRead(requirement, path, gate, result)
            {
                Object = Text(item, "object"),
                Changed = item.TryGetProperty("changed", out var changed) && changed.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? changed.ValueKind == JsonValueKind.True
                    : null,
                Spelled = Text(item, "spelled"),
            });
        }

        return new EvidenceVerdict(commit.ToLowerInvariant(), how, read)
        {
            Session = Text(verdict, "session"),
            At = DateTimeOffset.TryParse(Text(verdict, "at"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
                ? at
                : null,
            Machine = Text(verdict, "machine"),
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>
/// The codes evidence is written in (LANG1a; D144 §5, §6), the service's own (<c>QuestEvidenceCodes</c>): codes, never
/// sentences, so every reader words them itself.
/// </summary>
public static class EvidenceCodes
{
    public const string Found = "found";
    public const string Missing = "missing";
    public const string Uncommitted = "uncommitted";
    public const string Case = "case";

    /// <summary>A gate read where no landing queue runs it (D144 §4): EVID1d's, answered so until then.</summary>
    public const string NoQueue = "no-queue";

    /// <summary>What a path's read may say.</summary>
    public static readonly IReadOnlyList<string> PathResults = [Found, Missing, Uncommitted, Case];

    /// <summary>What a gate's read may say.</summary>
    public static readonly IReadOnlyList<string> GateResults = [Found, "not-declared", "not-run", "failed", NoQueue];

    public const string SessionEnd = "session-end";
    public const string Sweep = "sweep";
    public const string Terminal = "terminal";

    /// <summary>How the commit read was chosen (D144 §3): a session's end, the orphan sweep, or the person at the terminal.</summary>
    public static readonly IReadOnlyList<string> How = [SessionEnd, Sweep, Terminal];

    /// <summary>A hold's codes, as a quest answers <c>hold</c> (D144 §6).</summary>
    public const string Departed = "departed";
    public const string Unread = "evidence-unread";
    public const string MissingHold = "evidence-missing";

    /// <summary>Whether <paramref name="id"/> is a git object's full id: 40 hex characters, or 64 under SHA-256.</summary>
    public static bool IsObjectId(string? id) => id is { Length: 40 or 64 } && id.All(char.IsAsciiHexDigit);
}

/// <summary>
/// Which paths Daoris asks git for (EVID1b, D144 §2–§3): the service's own judge (<c>QuestEvidence.JudgePath</c>), read
/// again on this side before a path becomes a git argument. The two share no code, so each holds the same rows (twins).
/// </summary>
public static class EvidencePaths
{
    /// <summary>How long a path may be.</summary>
    public const int MaxLength = 300;

    /// <summary>Why <paramref name="path"/> is not one a commit can be asked for, or null when it is.</summary>
    public static string? Judge(string path)
    {
        if (path.Length == 0) return "it is empty";
        if (path.Length > MaxLength) return $"it is longer than {MaxLength} characters";
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
}
