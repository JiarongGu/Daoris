using System.Globalization;
using System.Text;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

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

    /// <summary>
    /// The submission it was read for (FABRIC-CLOSE1e): a submitted quest's verdict names it, so the service applies it to that submission
    /// alone; a done's names none, and neither does a verdict from before.
    /// </summary>
    public QuestSubmissionRef? Submission { get; init; }

    /// <summary>Whether every item was found: the one verdict that lets what the done held go on.</summary>
    public bool Found => Items.Count > 0 && Items.All(item => item.Result == EvidenceCodes.Found);

    /// <summary>How many items read <paramref name="result"/>.</summary>
    public int Count(string result) => Items.Count(item => item.Result == result);

    public bool Equals(EvidenceVerdict? other) =>
        other is not null && Commit == other.Commit && How == other.How && Session == other.Session && At == other.At
        && Machine == other.Machine && Submission == other.Submission && Items.SequenceEqual(other.Items);

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

            // FABRIC-CLOSE1e: the submission it was read for, written only when set, so a done's verdict is the body it always was.
            if (Submission is { } submission)
            {
                writer.WriteStartObject("submission");
                writer.WriteString("machine", submission.Machine);
                writer.WriteNumber("sequence", submission.Sequence);
                writer.WriteEndObject();
            }

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
            At = Moments.Read(Text(verdict, "at")),
            Machine = Text(verdict, "machine"),
            Submission = SubmissionOf(verdict),
        };
    }

    /// <summary>The submission a verdict names (FABRIC-CLOSE1e), or null: a done's, one from before, or one named only in half.</summary>
    private static QuestSubmissionRef? SubmissionOf(JsonElement verdict) =>
        verdict.TryGetProperty("submission", out var named) && named.ValueKind == JsonValueKind.Object
        && Text(named, "machine") is { Length: > 0 } machine
        && named.TryGetProperty("sequence", out var sequence) && sequence.ValueKind == JsonValueKind.Number && sequence.TryGetInt64(out var number) && number > 0
            ? new QuestSubmissionRef(machine, number)
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

    /// <summary>
    /// A set-up step's done, waiting for the person's verdict on its review (REVIEWENV1b, D154 point 9): after a departure and
    /// evidence, and lifted by their <c>reviewed</c> or skip alone, never by a yes.
    /// </summary>
    public const string Unreviewed = "unreviewed";

    /// <summary>Whether <paramref name="id"/> is a git object's full id: 40 hex characters, or 64 under SHA-256.</summary>
    public static bool IsObjectId(string? id) => id is { Length: 40 or 64 } && id.All(char.IsAsciiHexDigit);
}

/// <summary>
/// What the evidence door said of a verdict (EVID1a's <c>POST /api/quests/{id}/evidence</c>), or why nothing was posted: an
/// outcome's code and the service's words, or this side's where it said none.
/// </summary>
/// <param name="Outcome">One of the codes below.</param>
/// <param name="Message">The service's sentence, verbatim, or why nothing reached it.</param>
public sealed record EvidencePosted(string Outcome, string Message)
{
    /// <summary>The exchange kept the verdict: all found lifts the hold, anything else keeps it.</summary>
    public const string Kept = "kept";

    /// <summary>The done no longer waits on evidence (409): found already, or accepted as it stands.</summary>
    public const string NotWaiting = "not-waiting";

    /// <summary>The door refused the verdict (400), or holds no such quest (404).</summary>
    public const string Refused = "refused";

    /// <summary>A host older than the door.</summary>
    public const string NoDoor = "no-door";

    /// <summary>The host did not answer.</summary>
    public const string Unanswered = "unanswered";

    /// <summary>Nothing was read, so nothing was posted (<see cref="EvidenceReading.Unread"/> says why).</summary>
    public const string Unread = "unread";
}
