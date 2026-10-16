using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>
/// Whose words a part of a session's note is (LANG1a, D142 point 2; the language design §3): a words part is shown as
/// written, never through a catalogue.
/// </summary>
public static class NoteBy
{
    /// <summary>The agent's own words: its question, its last words, a turn's failure in its words.</summary>
    public const string Agent = "agent";

    /// <summary>The person's words: an answer, a finish's note, a decline's reason, an ask's closing note.</summary>
    public const string Person = "person";

    /// <summary>A program's words passed through: an exception's message, git's.</summary>
    public const string Program = "program";

    /// <summary>An English note from before parts, carried whole.</summary>
    public const string Before = "before";
}

/// <summary>
/// One line of a session's note (LANG1a, D142 point 2; the language design §3): a coded part, which the page words from its
/// code and values in the reader's language and whose <see cref="Text"/> is the English its writer wrote; or a words part,
/// someone's words, shown as written.
/// </summary>
/// <remarks>
/// <b>Values are facts, never sentences, and never a path or an account's name</b>: the note travels (D125 §3.6). A value is
/// text, a whole number, or a list of texts; one read back that is none of them is kept as it was read, so a newer writer's
/// part passes through this one unchanged. The one exception is a conversation's own lines, its first (<c>opening.*</c>, UX7d-1),
/// a wait for its account (<c>waits.*</c>, CONVNOTE2g) and a move by the usage curve (<c>chat.moved</c>, <c>chat.moved-from</c>,
/// SWITCH1c-i): they are kept only in the conversation's record, which never leaves the machine (D76 §2), so their values name
/// accounts by id.
/// </remarks>
[JsonConverter(typeof(NotePartJson))]
public sealed record NotePart
{
    /// <summary>The part's code, one of <see cref="NoteCodes"/>; null for a words part.</summary>
    public string? Code { get; init; }

    /// <summary>The facts its line carries, in the order written.</summary>
    public IReadOnlyList<KeyValuePair<string, object>> Values { get; init; } = [];

    /// <summary>A coded part's English as its writer wrote it: a substring of the note it was built into.</summary>
    public string? Text { get; init; }

    /// <summary>A words part's words.</summary>
    public string? Words { get; init; }

    /// <summary>Whose words, one of <see cref="NoteBy"/>.</summary>
    public string? By { get; init; }

    /// <summary>The value written under this name, or null.</summary>
    public object? Value(string name)
    {
        foreach (var (key, value) in Values)
        {
            if (string.Equals(key, name, StringComparison.Ordinal)) return value;
        }

        return null;
    }

    /// <summary>Someone's words as a part of their own.</summary>
    public static NotePart Said(string words, string by) => new() { Words = words, By = by };

    /// <summary>The parts as the record's JSON spells them, an array (the language design §3).</summary>
    public static void Write(Utf8JsonWriter writer, IReadOnlyList<NotePart> parts)
    {
        writer.WriteStartArray();
        foreach (var part in parts) NotePartJson.WritePart(writer, part);
        writer.WriteEndArray();
    }

    /// <summary>
    /// The parts a record answers, read without trusting the shape: an entry that is neither a coded part nor a words part
    /// is skipped. Null where the record answers none, a record from before parts, which is not the same as no parts.
    /// </summary>
    public static IReadOnlyList<NotePart>? Read(JsonElement record, string name = "noteParts")
    {
        if (record.ValueKind != JsonValueKind.Object || !record.TryGetProperty(name, out var parts)
            || parts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return [.. parts.EnumerateArray().Select(NotePartJson.ReadPart).OfType<NotePart>()];
    }
}

/// <summary>
/// A part as JSON, whichever serializer meets it: the record's own shape, <c>{code, values, text}</c> or <c>{words, by}</c>,
/// so the modules hand a part on to the page as the service answers it (LANG1a).
/// </summary>
public sealed class NotePartJson : JsonConverter<NotePart>
{
    public override NotePart? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return ReadPart(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, NotePart value, JsonSerializerOptions options) => WritePart(writer, value);

    internal static void WritePart(Utf8JsonWriter writer, NotePart part)
    {
        writer.WriteStartObject();
        if (part.Code is { } code)
        {
            writer.WriteString("code", code);
            writer.WriteStartObject("values");
            foreach (var (name, value) in part.Values)
            {
                writer.WritePropertyName(name);
                WriteValue(writer, value);
            }

            writer.WriteEndObject();
            if (part.Text is not null) writer.WriteString("text", part.Text);
        }
        else
        {
            writer.WriteString("words", part.Words ?? "");
            writer.WriteString("by", part.By ?? NoteBy.Before);
        }

        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case string text:
                writer.WriteStringValue(text);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            case IEnumerable<string> texts:
                writer.WriteStartArray();
                foreach (var text in texts) writer.WriteStringValue(text);
                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    internal static NotePart? ReadPart(JsonElement part)
    {
        if (part.ValueKind != JsonValueKind.Object) return null;
        if (Json.Text(part, "code") is { Length: > 0 } code)
        {
            var values = new List<KeyValuePair<string, object>>();
            if (part.TryGetProperty("values", out var given) && given.ValueKind == JsonValueKind.Object)
            {
                foreach (var value in given.EnumerateObject())
                {
                    values.Add(new(value.Name, ReadValue(value.Value)));
                }
            }

            return new NotePart { Code = code, Values = values, Text = Json.Text(part, "text") };
        }

        return Json.Text(part, "words") is { } words ? NotePart.Said(words, Json.Text(part, "by") ?? NoteBy.Before) : null;
    }

    private static object ReadValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number when value.TryGetInt64(out var number) => number,
        JsonValueKind.Array when value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String) =>
            (IReadOnlyList<string>)[.. value.EnumerateArray().Select(item => item.GetString()!)],
        _ => value.Clone(),
    };
}

/// <summary>
/// A note as its writer composes it (LANG1a, D142 point 2): its English, <see cref="Note"/>, byte for byte as the record has
/// always kept it, and its parts, each coded part's text a substring of it by construction.
/// </summary>
/// <remarks>
/// Composed by joining: <see cref="Then(string, Noted)"/> adds glue and a line, <see cref="Then(string)"/> glue alone, and
/// <see cref="Also"/> a part whose text the English already holds, where a writer's English interleaves two lines (an exit
/// inside a declined sentence, a program's words inside a tree's).
/// </remarks>
public sealed record Noted(string Note, IReadOnlyList<NotePart> Parts)
{
    /// <summary>One coded line, its English the text.</summary>
    public static Noted Of(NoteCode code, string text, params (string Name, object? Value)[] values) =>
        new(text, [code.Part(text, values)]);

    /// <summary>Someone's words, as a part of their own; nothing for no words.</summary>
    public static Noted Said(string words, string by) => new(words, words.Length == 0 ? [] : [NotePart.Said(words, by)]);

    /// <summary>
    /// A record's note as parts (the language design §3): its own parts, or its note whole as one <see cref="NoteBy.Before"/>
    /// part where it has none, or nothing.
    /// </summary>
    public static Noted From(string? note, IReadOnlyList<NotePart>? parts) =>
        parts is { Count: > 0 } ? new(note ?? "", parts)
        : string.IsNullOrEmpty(note) ? new("", [])
        : Said(note, NoteBy.Before);

    /// <summary>This, the glue, then the line.</summary>
    public Noted Then(string glue, Noted next) => new(Note + glue + next.Note, [.. Parts, .. next.Parts]);

    /// <summary>This, then glue that is no line of its own.</summary>
    public Noted Then(string glue) => new(Note + glue, Parts);

    /// <summary>A part whose text this English already holds.</summary>
    public Noted Also(NotePart part) => new(Note, [.. Parts, part]);

    /// <summary>
    /// This without someone's words that a coded line will say as facts (AGT3c): the last words part holding exactly
    /// <paramref name="words"/> by <paramref name="by"/>, and those words and the space before them out of the English. This,
    /// unchanged, where no such part is or the English does not hold them.
    /// </summary>
    public Noted Unsaid(string words, string by)
    {
        var at = -1;
        for (var part = Parts.Count - 1; part >= 0 && at < 0; part--)
        {
            if (Parts[part] is { Code: null } said && said.Words == words && said.By == by) at = part;
        }

        var start = at < 0 || words.Length == 0 ? -1 : Note.LastIndexOf(words, StringComparison.Ordinal);
        if (start < 0) return this;

        var from = start;
        while (from > 0 && char.IsWhiteSpace(Note[from - 1])) from--;
        return new(Note[..from] + Note[(start + words.Length)..], [.. Parts.Where((_, part) => part != at)]);
    }
}

/// <summary>
/// A code a line of a session's note is worded by (LANG1a, D142 points 3–4; the language design §4–§5): the page's key, and
/// the names of the facts its line carries, each a placeholder in its entry in both catalogues.
/// </summary>
/// <param name="Code">The code, as the record and the page spell it.</param>
/// <param name="Values">The facts its line carries.</param>
/// <param name="Key">The page's key where it is not <c>note.&lt;code&gt;</c>: a sentence the page already words.</param>
public sealed record NoteCode(string Code, IReadOnlyList<string> Values, string? Key = null)
{
    /// <summary>Where <c>why</c> is a reason's code, the reasons it may name, each worded with its own values.</summary>
    public NoteReasons? Why { get; init; }

    /// <summary>The page's key for this code.</summary>
    public string CatalogueKey => Key ?? $"note.{Code}";

    /// <summary>
    /// The part, with its values: each named among this code's, or among its reasons' own; a null value is not written, and
    /// the page then shows the part's text.
    /// </summary>
    /// <exception cref="ArgumentException">A value this code does not declare: its entry could not say it.</exception>
    public NotePart Part(string text, params (string Name, object? Value)[] values)
    {
        var kept = new List<KeyValuePair<string, object>>();
        foreach (var (name, value) in values)
        {
            if (!Values.Contains(name) && Why?.Carries(name) != true)
            {
                throw new ArgumentException($"`{Code}` declares no value `{name}`, so no entry of it can say it", nameof(values));
            }

            if (value is not null) kept.Add(new(name, value));
        }

        return new NotePart { Code = Code, Values = kept, Text = text };
    }
}

/// <summary>One reason a <c>why</c> may name, its own values, and its entry's key.</summary>
public sealed record NoteReason(string Code, IReadOnlyList<string> Values, string Key);

/// <summary>A family of reasons a <c>why</c> names (the language design §4: MSG1f's, and a cool-off's).</summary>
public sealed record NoteReasons(IReadOnlyList<NoteReason> All)
{
    /// <summary>Whether a value is one a reason here carries beside its code.</summary>
    public bool Carries(string value) => All.Any(reason => reason.Values.Contains(value));
}

/// <summary>
/// Every code the driver writes a line of a session's note by (LANG1a, D142 points 3–4; the language design §4, the
/// <c>LANG1a, built</c> note under D142 for what was added since). A twin (<c>.claude/knowledge/twins.md</c>): the page's
/// <c>note.json</c> in both languages words each, the service's <c>LedgerNoteCodes</c> declares the ledger's own, and the
/// web's <c>locales/note.test.ts</c> parses these declarations, one per line as written here, to hold the catalogue to them.
/// </summary>
/// <remarks>
/// <b>A site writes a part only through its code here</b>, and <c>NoteCodesTests</c> reads both catalogues, so a new line
/// fails until both languages word it.
/// </remarks>
public static class NoteCodes
{
    /// <summary>MSG1f's reasons (<see cref="ContinueWhy"/>), each with its own values, as the page words them (<c>work.say.why.*</c>).</summary>
    public static readonly NoteReasons Continue = new(
    [
        new(ContinueWhy.Account, [], "work.say.why.account"),
        new(ContinueWhy.Adapter, ["from", "to"], "work.say.why.adapter"),
        new(ContinueWhy.Unkept, [], "work.say.why.unkept"),
        new(ContinueWhy.Tree, [], "work.say.why.tree"),
        new(ContinueWhy.Unable, ["adapter"], "work.say.why.unable"),
        new(ContinueWhy.Offered, [], "work.say.why.offered"),
        new(ContinueWhy.Gone, [], "work.say.why.gone"),
        new(ContinueWhy.Refused, [], "work.say.why.refused"),
        new(ContinueWhy.Ended, [], "work.say.why.ended"),
        new(ContinueWhy.Elsewhere, ["agent"], "work.say.why.elsewhere"),
        new(ContinueWhy.Teammate, [], "work.say.why.teammate"),
        new(ContinueWhy.Intake, [], "work.say.why.intake"),
        new(ContinueWhy.StoodDown, [], "work.say.why.stoodDown"),
        new(ContinueWhy.Started, [], "work.say.why.started"),
    ]);

    /// <summary>Why a cool-off lasts until then, as the page words it (<c>harness.cooling.why.*</c>).</summary>
    public static readonly NoteReasons Cooling = new(
    [
        new(CoolingWhy.Stated, [], "harness.cooling.why.stated"),
        new(CoolingWhy.Assumed, [], "harness.cooling.why.assumed"),
        new(CoolingWhy.Default, [], "harness.cooling.why.default"),
        new(CoolingWhy.NotBelieved, [], "harness.cooling.why.notBelieved"),
    ]);

    /// <summary>
    /// Why a start ran on its account, a step of the walk as a conversation's first line says it (UX7d-1;
    /// <see cref="RotationWords.Steps"/>): each with its own values, the other account it names always <c>over</c>, and a
    /// moment, a percent or a window as a fact, never a clause.
    /// </summary>
    public static readonly NoteReasons Opening = new(
    [
        new(OpeningWhy.Cooling, ["agent", "over", "until", "cooling"], "note.opening.why.cooling"),
        new(OpeningWhy.Refused, ["agent", "over"], "note.opening.why.refused"),
        new(OpeningWhy.SignedOut, ["agent", "over"], "note.opening.why.signed-out"),
        new(OpeningWhy.KeptSelf, [], "note.opening.why.kept-self"),
        new(OpeningWhy.KeptLast, ["over"], "note.opening.why.kept-last"),
        new(OpeningWhy.Kept, ["over"], "note.opening.why.kept"),
        new(OpeningWhy.NearReached, ["over", "window"], "note.opening.why.near-reached"),
        new(OpeningWhy.NearWord, ["over", "window"], "note.opening.why.near-word"),
        new(OpeningWhy.NearCredits, ["over"], "note.opening.why.near-credits"),
        new(OpeningWhy.NearUsed, ["over", "used", "window", "near"], "note.opening.why.near-used"),
        new(OpeningWhy.OverCurve, ["over", "window", "used", "curve"], "note.opening.why.over-curve"),
        new(OpeningWhy.Near, ["over"], "note.opening.why.near"),
        new(OpeningWhy.Room, ["over", "used", "against"], "note.opening.why.room"),
        new(OpeningWhy.Fewest, [], "note.opening.why.fewest"),
        new(OpeningWhy.Lapsing, ["at"], "note.opening.why.lapsing"),
        new(OpeningWhy.MostRoom, ["room", "window"], "note.opening.why.most-room"),
        new(OpeningWhy.Behind, ["used", "gone"], "note.opening.why.behind"),
        new(OpeningWhy.Ahead, ["over", "used", "gone"], "note.opening.why.ahead"),
        new(OpeningWhy.NotStarted, [], "note.opening.why.not-started"),
        new(OpeningWhy.LeastRecent, [], "note.opening.why.least-recent"),
        new(OpeningWhy.Default, ["workspace"], "note.opening.why.default"),
        new(OpeningWhy.DefaultHere, [], "note.opening.why.default-here"),
        new(OpeningWhy.First, ["workspace"], "note.opening.why.first"),
        new(OpeningWhy.FirstHere, [], "note.opening.why.first-here"),
    ]);

    /// <summary>
    /// Who spoke the words a resumed conversation goes on with, as the page says it (CONVNOTE2a; <see cref="Continuations.Opening"/>):
    /// a reason per way the English says it, with no value of its own.
    /// </summary>
    public static readonly NoteReasons Resumed = new(
    [
        new(ResumedWhy.Answer, [], "note.resumed.why.answer"),
        new(ResumedWhy.Words, [], "note.resumed.why.words"),
        new(ResumedWhy.Findings, [], "note.resumed.why.findings"),
        new(ResumedWhy.AnswerAndFindings, [], "note.resumed.why.answer-and-findings"),
        new(ResumedWhy.WordsAndFindings, [], "note.resumed.why.words-and-findings"),
        new(ResumedWhy.Helper, [], "note.resumed.why.helper"),
        new(ResumedWhy.WordsAndHelper, [], "note.resumed.why.words-and-helper"),
    ]);

    /// <summary>Why the person's words never reached the session (CONVNOTE2d1; <see cref="UnreachedWhy"/>): the page's <c>note.words.why.*</c>.</summary>
    public static readonly NoteReasons Unreached = new(
    [
        new(UnreachedWhy.EndedFirst, [], "note.words.why.ended-first"),
        new(UnreachedWhy.Unkept, [], "note.words.why.unkept"),
    ]);

    // ——— A session's end (Observation, the driver's own ends).
    public static readonly NoteCode EndedAwaits = new("ended.awaits", ["awaits"]);
    public static readonly NoteCode EndedExit = new("ended.exit", ["exit"]);
    public static readonly NoteCode EndedTurnFailedTaken = new("ended.turn-failed-taken", []);
    public static readonly NoteCode EndedTurnFailedOpen = new("ended.turn-failed-open", []);
    public static readonly NoteCode EndedParkedAsked = new("ended.parked-asked", []);
    public static readonly NoteCode EndedParkedTranscript = new("ended.parked-transcript", []);
    public static readonly NoteCode EndedParkedShort = new("ended.parked-short", []);
    public static readonly NoteCode EndedAnsweredUnfinished = new("ended.answered-unfinished", ["awaits", "exit"]);
    public static readonly NoteCode EndedCarriedUnfinished = new("ended.carried-unfinished", ["exit"]);
    public static readonly NoteCode EndedDone = new("ended.done", []);
    public static readonly NoteCode EndedDeclined = new("ended.declined", []);
    public static readonly NoteCode EndedStoodDown = new("ended.stood-down", []);
    public static readonly NoteCode EndedTakenExit = new("ended.taken-exit", ["exit"]);
    public static readonly NoteCode EndedUntouched = new("ended.untouched", []);
    public static readonly NoteCode EndedUntouchedExit = new("ended.untouched-exit", ["exit"]);
    public static readonly NoteCode EndedWentOn = new("ended.went-on", []);
    public static readonly NoteCode EndedWentOnExit = new("ended.went-on-exit", ["exit"]);
    // A closed quest's session whose turn failed going on (TURNFAIL1): the door's reason follows, in the agent's words.
    public static readonly NoteCode EndedWentOnFailed = new("ended.went-on-failed", []);
    // The person's words a failed turn carried wait to go again (TURNFAIL1), or, failing twice, wait for the person.
    public static readonly NoteCode EndedWordsWait = new("ended.words-wait", []);
    public static readonly NoteCode EndedWordsWaitYou = new("ended.words-wait-you", []);
    public static readonly NoteCode EndedTimeout = new("ended.timeout", ["minutes"]);
    public static readonly NoteCode EndedStopped = new("ended.stopped", []);
    public static readonly NoteCode EndedLostClaim = new("ended.lost-claim", []);
    // A quest's session stopped to go on with another agent (MIXAGENT1a-ii): the agent it went to, never an account or a path.
    public static readonly NoteCode EndedHandedOver = new("ended.handed-over", ["agent"]);
    public static readonly NoteCode EndedDriverClosed = new("ended.driver-closed", []);

    // ——— An account's line, appended to a failure. A limit's cooling line names whose accounts they are, never which, and the
    // window its reset named, by codes of their own (AGT3d). `account.cooling` keeps what it declared and is no longer
    // written: notes already on machines carry it, and the page words a line only with every value its entry says (D142
    // point 4), so a code that gained a value would show each of them as recorded. A limit that named no window has its own
    // line for the same reason.
    public static readonly NoteCode AccountCooling = new("account.cooling", ["until", "why"]) { Why = Cooling };
    public static readonly NoteCode AccountCoolingWindow = new("account.cooling-window", ["until", "why", "owner", "window"]) { Why = Cooling };
    public static readonly NoteCode AccountCoolingNoWindow = new("account.cooling-no-window", ["until", "why", "owner"]) { Why = Cooling };
    public static readonly NoteCode AccountRefused = new("account.refused", ["owner"]);
    public static readonly NoteCode AccountRefusedOwn = new("account.refused-own", ["owner"]);
    public static readonly NoteCode AccountSignedOut = new("account.signed-out", ["owner"]);
    public static readonly NoteCode AccountSignedOutOwn = new("account.signed-out-own", ["owner"]);

    /// <summary>
    /// The account's lines that say its agent or its provider refused the account a start ran on, its sign-in (ROSTER1b) or
    /// its credential (AGT3b): the fault is the account's, never the quest's, so a failure carrying one is no strike
    /// (<see cref="ServiceClient.ReadStrikes"/>), read by the code the driver wrote, never by the words.
    /// </summary>
    public static IReadOnlyList<NoteCode> AccountsOwn { get; } = [AccountRefused, AccountRefusedOwn, AccountSignedOut, AccountSignedOutOwn];

    // ——— Why the account a resume asked for could not carry the person's words (MSG1g), after a line whose reason is
    // `account`: by TOOL6e's holds, naming no account, since the note travels.
    public static readonly NoteCode AccountResumeSignedOut = new("account.resume-signed-out", []);
    public static readonly NoteCode AccountResumeRefused = new("account.resume-refused", []);
    public static readonly NoteCode AccountResumeGone = new("account.resume-gone", []);
    public static readonly NoteCode AccountResumeOutside = new("account.resume-outside", []);
    public static readonly NoteCode AccountResumeKept = new("account.resume-kept", []);
    public static readonly NoteCode AccountResumeNewSession = new("account.resume-new-session", []);

    // ——— While it starts and works, replaced by its end (D51 rule 4).
    public static readonly NoteCode StartedTree = new("started.tree", ["branch", "basedOn"]);
    public static readonly NoteCode StartedTreeUnrecorded = new("started.tree-unrecorded", []);
    public static readonly NoteCode StartedJoined = new("started.joined", ["session"]);
    public static readonly NoteCode StartedResumesAnswered = new("started.resumes-answered", ["quest", "answered"]);
    public static readonly NoteCode StartedInAskingTree = new("started.in-asking-tree", ["session"]);
    public static readonly NoteCode StartedWordsOpen = new("started.words-open", ["quest", "session"]);
    public static readonly NoteCode StartedWordsTaken = new("started.words-taken", ["quest", "session"]);
    public static readonly NoteCode StartedAnswer = new("started.answer", ["quest", "session"]);
    public static readonly NoteCode StartedReleased = new("started.released", ["quest", "session"]);
    public static readonly NoteCode StartedCutOff = new("started.cut-off", ["quest", "session"]);
    // A quest handed to another agent (MIXAGENT1a-ii, D163 point 1): the carry-on's first line says which agent it came from, and
    // where the account the hand-over named could not run it, that another of the named agent's did. Facts only, no account's name.
    public static readonly NoteCode StartedHandedOver = new("started.handed-over", ["quest", "session", "agent"]);
    public static readonly NoteCode StartedHandedWalked = new("started.handed-walked", []);
    public static readonly NoteCode StartedOtherAccount = new("started.other-account", []);
    public static readonly NoteCode StartedSameTree = new("started.same-tree", []);
    public static readonly NoteCode StartedFellBack = new("started.fell-back", ["why"]) { Why = Continue };
    public static readonly NoteCode WorkingResumesAnswer = new("working.resumes-answer", []);
    public static readonly NoteCode WorkingGoesOn = new("working.goes-on", []);

    /// <summary>Why a conversation was handed to another agent (MIXAGENT1a-i, <see cref="HandOverWhy"/>): the page's <c>note.handed.why.*</c>.</summary>
    public static readonly NoteReasons Handed = new(
    [
        new(HandOverWhy.Press, [], "note.handed.why.press"),
        new(HandOverWhy.Near, [], "note.handed.why.near"),
        new(HandOverWhy.Limit, [], "note.handed.why.limit"),
        new(HandOverWhy.Agent, [], "note.handed.why.agent"),
        new(HandOverWhy.Curve, [], "note.handed.why.curve"),
    ]);

    /// <summary>Why no helper answered a question (MIXAGENT2a-ii, <see cref="HelperWhy"/>): the page's <c>note.helper.why.*</c>.</summary>
    public static readonly NoteReasons Helper = new(
    [
        new(HelperWhy.Off, [], "note.helper.why.off"),
        new(HelperWhy.AskerGone, [], "note.helper.why.asker-gone"),
        new(HelperWhy.AskerUnfit, [], "note.helper.why.asker-unfit"),
        new(HelperWhy.TooMany, [], "note.helper.why.too-many"),
        new(HelperWhy.Unreadable, [], "note.helper.why.unreadable"),
        new(HelperWhy.NotStarted, [], "note.helper.why.not-started"),
        new(HelperWhy.NoAnswer, [], "note.helper.why.no-answer"),
        new(HelperWhy.Conversation, [], "note.helper.why.conversation"),
        new(HelperWhy.CannotTake, [], "note.helper.why.cannot-take"),
        // HELPERNO1: the person's no to an agent's ask, added after the nine (a reason is additive).
        new(HelperWhy.Declined, [], "note.helper.why.declined"),
    ]);

    // ——— A helper (MIXAGENT2a-ii, D163 point 2): on the asker, a helper asked, answered or refused, with the agent and why;
    // on the helper, whose session it helps. Facts only: never an account's name or a path.
    public static readonly NoteCode HelperAsked = new("helper.asked", ["agent", "session"]);
    public static readonly NoteCode HelperAnswered = new("helper.answered", ["agent", "session"]);
    public static readonly NoteCode HelperAnsweredLastWords = new("helper.answered-last-words", ["agent", "session"]);
    public static readonly NoteCode HelperRefused = new("helper.refused", ["why"]) { Why = Helper };
    public static readonly NoteCode HelperRefusedBy = new("helper.refused-by", ["agent", "why"]) { Why = Helper };
    public static readonly NoteCode HelperHelping = new("helper.helping", ["session"]);

    // ——— A conversation handed to another agent (MIXAGENT1a-i, D163 §3): the old one's end says which agent and which
    // conversation it went to, the new one's first line where it came from and why. Facts only: never an account's name or a path.
    public static readonly NoteCode ChatHandedOver = new("chat.handed-over", ["agent", "session"]);
    public static readonly NoteCode ChatTakenOver = new("chat.taken-over", ["agent", "session", "why"]) { Why = Handed };
    public static readonly NoteCode ChatBriefingUnsaid = new("chat.briefing-unsaid", []);

    // ——— A conversation moved to another account of its agent by the usage curve (SWITCH1c-i, D165 point 3; the switch design
    // §4): the old one's last line says where it went and how far over its curve its account was, the new one's first where it
    // came from. They name the accounts, as `opening.*` does: a conversation's record stays on this machine.
    public static readonly NoteCode ChatMoved = new("chat.moved", ["to", "from", "window", "used", "curve"]);
    public static readonly NoteCode ChatMovedFrom = new("chat.moved-from", ["from", "window", "used", "curve"]);

    // ——— Where the person's words went, after the record's earlier parts.
    public static readonly NoteCode WentCannot = new("went.cannot", ["why"], Key: "work.say.cannot") { Why = Continue };
    public static readonly NoteCode WentNewSession = new("went.new-session", ["why"]) { Why = Continue };
    public static readonly NoteCode WentCarriedOn = new("went.carried-on", ["why"]) { Why = Continue };

    // ——— An intake.
    public static readonly NoteCode IntakeAskDeleted = new("intake.ask-deleted", []);
    public static readonly NoteCode IntakePublished = new("intake.published", ["quests", "ask"]);
    public static readonly NoteCode IntakeAskClosed = new("intake.ask-closed", ["ask"]);
    public static readonly NoteCode IntakeAskAnswered = new("intake.ask-answered", ["ask", "quests"]);
    public static readonly NoteCode IntakeTurnFailed = new("intake.turn-failed", ["ask"]);
    public static readonly NoteCode IntakeAsks = new("intake.asks", ["ask"]);
    public static readonly NoteCode IntakeExit = new("intake.exit", ["ask", "exit"]);
    public static readonly NoteCode IntakePersonAnswered = new("intake.person-answered", ["ask", "quests"]);
    public static readonly NoteCode IntakePersonClosed = new("intake.person-closed", ["ask"]);
    public static readonly NoteCode IntakePersonDeleted = new("intake.person-deleted", ["ask"]);

    // ——— A stop.
    public static readonly NoteCode StoppedPausedAsk = new("stopped.paused-ask", ["ask"]);
    public static readonly NoteCode StoppedPausedQuest = new("stopped.paused-quest", ["quest"]);
    public static readonly NoteCode StoppedAbandonedAsk = new("stopped.abandoned-ask", ["ask"]);
    public static readonly NoteCode StoppedAbandonedQuest = new("stopped.abandoned-quest", ["quest"]);
    public static readonly NoteCode StoppedCheckpointFinished = new("stopped.checkpoint-finished", []);
    public static readonly NoteCode StoppedCheckpointStopped = new("stopped.checkpoint-stopped", []);
    public static readonly NoteCode StoppedCheckpointMoved = new("stopped.checkpoint-moved", []);
    public static readonly NoteCode StoppedOrphan = new("stopped.orphan", []);
    public static readonly NoteCode ChatClosed = new("chat.closed", []);
    public static readonly NoteCode ChatEndedByPerson = new("chat.ended-by-person", []);
    public static readonly NoteCode ChatEnded = new("chat.ended", []);
    public static readonly NoteCode ChatCancelled = new("chat.cancelled", []);
    public static readonly NoteCode ChatNotKept = new("chat.not-kept", []);

    // ——— A landing at the quest's done, under a rule that accepts automatically (LAND2b): said in the conversation's record.
    public static readonly NoteCode LandingAccepted = new("landing.accepted", ["branch"]);
    public static readonly NoteCode LandingUnready = new("landing.plugin-unready", ["branch", "plugin"]);
    public static readonly NoteCode LandingPluginFailed = new("landing.plugin-failed", ["branch", "plugin"]);
    public static readonly NoteCode LandingNothing = new("landing.nothing", []);
    public static readonly NoteCode LandingHeld = new("landing.held", []);
    public static readonly NoteCode LandingUncommitted = new("landing.uncommitted", ["paths"]);
    public static readonly NoteCode LandingExists = new("landing.exists", ["branch"]);
    public static readonly NoteCode LandingRefused = new("landing.refused", []);
    // Held by the review's gate (REVIEWENV1c2): a code of its own, so a look's wait is never said as a refused landing; a note
    // written before it keeps `landing.refused`.
    public static readonly NoteCode LandingUnreviewed = new("landing.unreviewed", []);
    // Held by the second opinion's gate (XAGENT1g): a code of its own once the page words it; a note written before it keeps
    // `landing.refused`.
    public static readonly NoteCode LandingOpinion = new("landing.opinion", []);
    public static readonly NoteCode LandingNoTree = new("landing.no-tree", []);
    public static readonly NoteCode LandingNotDone = new("landing.not-done", []);

    // ——— A conversation's first line, where a start's account was chosen (UX7d-1, RotatedOpening): said in the conversation's
    // record, which never leaves the machine (D76 §2), so its values name accounts and a workspace. The line, then the rest's
    // step and a refused turn riding inside it, then what each account said, a line per account and one per thing it said.
    public static readonly NoteCode OpeningOpened = new("opening.opened", ["account", "why"]) { Why = Opening };
    public static readonly NoteCode OpeningCarriedOn = new("opening.carried-on", ["session", "account", "why"]) { Why = Opening };
    public static readonly NoteCode OpeningRest = new("opening.rest", ["why"]) { Why = Opening };
    public static readonly NoteCode OpeningTurnRefused = new("opening.turn-refused", ["turn"]);
    public static readonly NoteCode OpeningTurnContext = new("opening.turn-context", ["turn", "tokens"]);
    public static readonly NoteCode OpeningUnsaid = new("opening.unsaid", []);
    public static readonly NoteCode OpeningSaidAt = new("opening.said-at", ["account", "seen"]);
    public static readonly NoteCode OpeningSaidNothing = new("opening.said-nothing", ["account"]);
    public static readonly NoteCode OpeningSaidReached = new("opening.said-reached", ["window"]);
    // The agent's own pace warning, never near (USAGEREAD3a): `opening.said-warned` since; a note written before it keeps
    // `opening.said-near`, which nothing writes now.
    public static readonly NoteCode OpeningSaidNear = new("opening.said-near", ["window"]);
    public static readonly NoteCode OpeningSaidWarned = new("opening.said-warned", ["window"]);
    public static readonly NoteCode OpeningSaidCredits = new("opening.said-credits", []);
    public static readonly NoteCode OpeningSaidUsed = new("opening.said-used", ["used", "window"]);
    // Over its curve on a window, and the curve's value there now (SWITCH1b-ii): `opening.said-over` since; a note written
    // before keeps `opening.said-near-at`, the scope's one number, which nothing writes now.
    public static readonly NoteCode OpeningSaidNearAt = new("opening.said-near-at", ["near"]);
    public static readonly NoteCode OpeningSaidOver = new("opening.said-over", ["window", "curve"]);
    public static readonly NoteCode OpeningSaidClear = new("opening.said-clear", []);

    // ——— A resumed conversation's first line (CONVNOTE2a, Continuations.Opening): the adapter it resumed on is a fact, and
    // a version that moved is a part riding inside it. The conversation's record never leaves the machine (D76 §2).
    public static readonly NoteCode ResumedOpening = new("resumed.opening", ["adapter", "why"]) { Why = Resumed };
    public static readonly NoteCode ResumedMoved = new("resumed.moved", ["now", "then"]);
    public static readonly NoteCode ResumedNative = new("resumed.native", ["adapter"]);

    // ——— A permission the rules refused (CONVNOTE2b, Acp.AnswerPermissionAsync): the lead and the closing sentence are facts
    // of Daoris's; the call's title between them is the agent's words, which may hold a path (D142 point 2).
    public static readonly NoteCode RefusedLead = new("refused.lead", []);
    public static readonly NoteCode RefusedGoverns = new("refused.governs", []);

    // ——— A press's note and a chat's settings (CONVNOTE2c): the lead is Daoris's, what follows a landing's or a preface's lead
    // is a program's words (git's, a plugin's, Daoris's one-copy English to the agent, D142 point 6). The option and its value are
    // the agent's own names for them, never a path.
    public static readonly NoteCode LandingPersonAccepted = new("landing.person-accepted", []);
    public static readonly NoteCode LandingPersonHanded = new("landing.person-handed", []);
    // A part the person left out of its entry's Accept (ACCEPTALL1c, D167's amendment point 3): the branch its line of work landed on,
    // or the line it merged into, a fact. Its review reads it back (LandingRules.LeftOutOf) and leads with Discard.
    public static readonly NoteCode LandingLeftOut = new("landing.left-out", ["branch"]);
    public static readonly NoteCode ChatOptionSet = new("chat.option-set", ["option", "chosen"]);
    public static readonly NoteCode ChatToldWhere = new("chat.told-where", []);

    // ——— Words that did not reach an agent, or were not taken (CONVNOTE2d1, <see cref="WordsNotes"/>): the lead is Daoris's; what
    // follows a refusal is the agent's words, and what follows a failed take is a program's or the service's (it may hold a path).
    // The count and why a send never reached are facts, `why` one of `Unreached`'s.
    public static readonly NoteCode WordsRefused = new("words.refused", []);
    public static readonly NoteCode WordsUnreached = new("words.unreached", ["count", "why"]) { Why = Unreached };
    public static readonly NoteCode WordsNotTaken = new("words.not-taken", []);

    // The chat runner's and the modules' sites (CONVNOTE2d2): words a conversation never got, a send that found the agent gone,
    // a start's words not taken off their session (the session id a fact), and held words the record would not keep.
    public static readonly NoteCode WordsNotReached = new("words.not-reached", []);
    public static readonly NoteCode WordsNotSent = new("words.not-sent", []);
    public static readonly NoteCode WordsStartNotTaken = new("words.start-not-taken", ["session"]);
    public static readonly NoteCode WordsNotKept = new("words.not-kept", []);

    // ——— A server that drives Daoris's own browser, withheld (CONVNOTE2c, InAppBrowserServers.HandNotedAsync): the servers are
    // plugin-declared names; why it was not handed is a fact of the host's, or an exception's words riding after the lead.
    public static readonly NoteCode BrowserWithheld = new("browser.withheld", ["servers"]);
    public static readonly NoteCode BrowserNoHost = new("browser.no-host", []);
    public static readonly NoteCode BrowserNoneUp = new("browser.none-up", []);
    // The line a chat's agent was told of the browser it was handed (BRWHINT1a): the lead by its code, the line Daoris's one-copy English.
    public static readonly NoteCode ChatToldBrowser = new("chat.told-browser", []);
    // The repository's process a chat in a tree of its own was told at its start (PROCESS1d-ii): its commit rule's mode and where it
    // was set are facts, the words after the lead Daoris's one-copy English.
    public static readonly NoteCode ChatToldProcess = new("chat.told-process", ["mode", "source"]);

    // ——— A session's own background work (CONVNOTE2f, <see cref="BackgroundNotes"/>, BGWAIT1): the work's names are the agent's own
    // descriptions of it, said as code; a state is the harness's own word for how it ended, said as it came. Why a wait could not
    // be read is a program's words, which may hold a path.
    public static readonly NoteCode BackgroundUnread = new("background.unread", []);
    public static readonly NoteCode BackgroundWaits = new("background.waits", ["works"]);
    public static readonly NoteCode BackgroundWorkEnded = new("background.work-ended", ["work", "state"]);
    public static readonly NoteCode BackgroundGoesOn = new("background.goes-on", []);

    // ——— A conversation's door that failed (CONVNOTE2e, <see cref="DoorNotes"/>): the protocol session that could not open or failed,
    // and a turn that could not be taken, each a lead with a program's message after it (it may hold a path). A resume the agent
    // refused is Daoris's own sentence from its `ContinueReason`, so `why` is the `Continue` family's, then the agent's words where it
    // said any; the driver's door adds that the answer is carried on in a new session.
    public static readonly NoteCode DoorNotOpen = new("door.not-open", []);
    public static readonly NoteCode DoorTurnNotTaken = new("door.turn-not-taken", []);
    public static readonly NoteCode DoorFailed = new("door.failed", []);
    public static readonly NoteCode DoorResumeRefused = new("door.resume-refused", ["why"]) { Why = Continue };
    public static readonly NoteCode DoorAnswerCarriedOn = new("door.answer-carried-on", []);

    // ——— A review set-up's record (CONVNOTE2h, <see cref="ReviewNotes"/>): the environment's name and the repository a fact; the
    // commit is the 8-character short id. The service's message and the served sentence are a program's words, which may hold a path.
    public static readonly NoteCode ReviewNoRule = new("review.no-rule", ["named", "to"]);
    public static readonly NoteCode ReviewNoCommit = new("review.no-commit", []);
    public static readonly NoteCode ReviewPosted = new("review.posted", ["commit", "named"]);
    public static readonly NoteCode ReviewNotPosted = new("review.not-posted", []);
    public static readonly NoteCode ReviewPostsNext = new("review.posts-next", []);

    // ——— A second opinion's answers as the working session's turn ended (CONVNOTE2h, <see cref="OpinionAnswers.Told"/>): whose
    // findings and how many, then how many each way, each only above nought; a count the page says "(N in all)".
    public static readonly NoteCode OpinionAnswersLead = new("opinion.answers", ["who", "count"]);
    public static readonly NoteCode OpinionFixed = new("opinion.fixed", ["count"]);
    public static readonly NoteCode OpinionUnproven = new("opinion.unproven", ["count"]);
    public static readonly NoteCode OpinionRejected = new("opinion.rejected", ["count"]);
    public static readonly NoteCode OpinionUnresolved = new("opinion.unresolved", ["count"]);
    public static readonly NoteCode OpinionUnanswered = new("opinion.unanswered", ["count"]);
    public static readonly NoteCode OpinionDisputed = new("opinion.disputed", ["count"]);

    // ——— How the reviewer's own session ended (XAGENT1g4a, <see cref="OpinionAnswers.Concluded"/>): its turn failed (the failure
    // follows as the program's words), it read the work and ended, or it exited otherwise. The opinion's id is a short id.
    public static readonly NoteCode OpinionReviewerFailed = new("opinion.reviewer-failed", ["opinion"]);
    public static readonly NoteCode OpinionReviewerRead = new("opinion.reviewer-read", ["opinion"]);
    public static readonly NoteCode OpinionReviewerExited = new("opinion.reviewer-exited", ["opinion", "exit"]);

    // ——— A conversation's words waiting for its own account's reset (CONVNOTE2g, <see cref="ResumeWords"/>, MSG1g): the account
    // by its owner and its id, never by the name the person gave it, since a conversation's events never leave the machine (D152's
    // UX7d-1 note); until when a moment, why a cool-off's reason. The tool's own sign-in has a line of its own and its refresh
    // another. Each door out of the wait is a line, the session its command names a value; the command itself is the catalogue's.
    public static readonly NoteCode WaitsAccount = new("waits.account", ["owner", "account", "until", "why"]) { Why = Cooling };
    public static readonly NoteCode WaitsOwn = new("waits.own", ["owner", "until", "why"]) { Why = Cooling };
    public static readonly NoteCode WaitsReadAgain = new("waits.read-again", []);
    public static readonly NoteCode WaitsDoorNewSession = new("waits.door-new-session", ["session"]);
    public static readonly NoteCode WaitsDoorChat = new("waits.door-chat", ["session"]);
    public static readonly NoteCode WaitsDoorHelp = new("waits.door-help", []);

    // ——— A conversation's words held for another reason (CONVNOTE2g2, <see cref="HeldWords"/>): a reason of Daoris's own sentence is
    // one line, its repository or conversation a value; a program's message (driver.json unread, the roster's refusal, the ledger's)
    // rides as its words between `lead` (or the reason `config-unread`) and `tail`, never a value, since it may hold a path.
    public static readonly NoteCode HeldNoCheckout = new("held.no-checkout", ["repository"]);
    public static readonly NoteCode HeldReplaying = new("held.replaying", ["repository"]);
    public static readonly NoteCode HeldAnswering = new("held.answering", ["session"]);
    public static readonly NoteCode HeldConfigUnread = new("held.config-unread", []);
    public static readonly NoteCode HeldLead = new("held.lead", []);
    public static readonly NoteCode HeldTail = new("held.tail", []);

    // ——— The roster's refusal as a held note's reason (ROSTERREFUSE1, <see cref="HeldWords.Refused"/>): an account by its owner and its
    // id, never the name the person gave it (the page names it), until when a moment, why a cool-off's reason; the tool's own sign-in
    // is a line of its own, its refresh `waits.read-again`. `profile` is the id as a shell spells it in the sign-in command.
    public static readonly NoteCode HeldCoolingAccount = new("held.cooling-account", ["owner", "account", "until", "why"]) { Why = Cooling };
    public static readonly NoteCode HeldCoolingOwn = new("held.cooling-own", ["owner", "until", "why"]) { Why = Cooling };
    public static readonly NoteCode HeldSignedOut = new("held.signed-out", ["owner", "account", "profile"]);
    // OWNSPACE1a (D175 point 2): no Daoris account of the agent's is signed in, and its own home is never a start's.
    public static readonly NoteCode HeldNoAccount = new("held.no-account", ["owner"]);
    public static readonly NoteCode HeldNotInstalled = new("held.not-installed", ["agent"]);
    public static readonly NoteCode HeldPinNotInstalled = new("held.pin-not-installed", ["agent", "version"]);

    // ——— The roster's other refusals (ROSTERREFUSE2). A wait over a list: its lead, a clause for each account (the account by its id,
    // the page names it), the first reset, then the sign-in that would start it sooner, `logins` each command into an account, its id spelled for any shell. A pick
    // refused adds who is ready, `accounts` a list. An agent with its own tooling names that command as a program's words between
    // `not-installed-own` and `never-installs`, never a value, since it may hold a path. An account its provider refused, and the
    // clauses a wait adds (kept for conversations, and the accounts outside the list with the command that adds the first).
    public static readonly NoteCode HeldWaitEvery = new("held.wait-every", ["agent"]);
    public static readonly NoteCode HeldWaitSome = new("held.wait-some", ["agent"]);
    public static readonly NoteCode HeldClauseCooling = new("held.clause-cooling", ["account", "until"]);
    public static readonly NoteCode HeldClauseRefused = new("held.clause-refused", ["account"]);
    public static readonly NoteCode HeldClauseSignedOut = new("held.clause-signed-out", ["account"]);
    public static readonly NoteCode HeldClauseReady = new("held.clause-ready", ["account"]);
    public static readonly NoteCode HeldWaitFirst = new("held.wait-first", ["account", "until", "why"]) { Why = Cooling };
    public static readonly NoteCode HeldSignInSooner = new("held.sign-in-sooner", ["logins"]);
    public static readonly NoteCode HeldSignIn = new("held.sign-in", ["logins"]);
    public static readonly NoteCode HeldPickedReady = new("held.picked-ready", ["accounts"]);
    public static readonly NoteCode HeldPickedNone = new("held.picked-none", ["agent"]);
    public static readonly NoteCode HeldRefusedAccount = new("held.refused-account", ["owner", "account"]);
    public static readonly NoteCode HeldRefusedOwn = new("held.refused-own", ["owner"]);
    public static readonly NoteCode HeldSignInRefusedOwn = new("held.sign-in-refused-own", ["owner"]);
    public static readonly NoteCode HeldNotInstalledOwn = new("held.not-installed-own", ["agent"]);
    public static readonly NoteCode HeldNeverInstalls = new("held.never-installs", []);
    public static readonly NoteCode HeldKeptAside = new("held.kept-aside", ["account"]);
    public static readonly NoteCode HeldOutsideMachine = new("held.outside-machine", ["accounts", "first", "door"]);
    public static readonly NoteCode HeldOutsideWorkspace = new("held.outside-workspace", ["workspace", "accounts", "first", "door"]);

    /// <summary>Every code above, read off the declarations so none escapes the catalogue test.</summary>
    public static IReadOnlyList<NoteCode> All { get; } =
        [.. typeof(NoteCodes).GetFields().Where(field => field.FieldType == typeof(NoteCode)).Select(field => (NoteCode)field.GetValue(null)!)];

    /// <summary>
    /// A reason's values for a <c>why</c> (MSG1f): its code, then its own, read from what it says; <c>elsewhere</c>'s agent is
    /// the adapter the record ran on, which the page may say by its product's name.
    /// </summary>
    /// <param name="ranOn">The adapter the record ran on, or null where it names none.</param>
    public static (string Name, object? Value)[] Reason(ContinueReason why, string? ranOn = null)
    {
        var values = new List<(string Name, object? Value)> { ("why", why.Code) };
        values.AddRange(why.Values.Select(value => (value.Name, (object?)value.Value)));
        if (why.Code == ContinueWhy.Elsewhere && why.Values.All(value => value.Name != "agent")) values.Add(("agent", ranOn));
        return [.. values];
    }

    /// <summary>A moment as a value: ISO 8601 in UTC, to the second, which the page formats in the reader's language and zone.</summary>
    public static string Moment(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}

/// <summary>Why the person's words never reached the session, by code (CONVNOTE2d1): the page's <c>note.words.why.*</c>.</summary>
public static class UnreachedWhy
{
    /// <summary>The session ended while they waited.</summary>
    public const string EndedFirst = "ended-first";

    /// <summary>Daoris kept no id for the conversation, so there was nothing to resume.</summary>
    public const string Unkept = "unkept";

    /// <summary>The English a reason says, as a site writes it.</summary>
    public static string English(string why) => why switch
    {
        EndedFirst => "it ended first",
        Unkept => "Daoris kept no id for its conversation",
        _ => throw new ArgumentOutOfRangeException(nameof(why), why, "an unreached reason NoteCodes.Unreached does not declare"),
    };
}

/// <summary>
/// The notes for words that did not reach an agent, or that it or the record would not take (CONVNOTE2d1): each English as
/// its site always wrote it, byte for byte, with its parts.
/// </summary>
public static class WordsNotes
{
    /// <summary>What the agent said when it would not take the person's words mid-turn, in its own words.</summary>
    /// <param name="agentWords">The agent's own words.</param>
    /// <param name="who">What the English calls the one that refused: the chat runner has always said "conversation".</param>
    public static Noted Refused(string agentWords, string who = "session") =>
        Noted.Of(NoteCodes.WordsRefused, $"— the {who} would not take what the person added:")
            .Then(" ", Noted.Said(agentWords, NoteBy.Agent));

    /// <summary>Words that never reached a conversation, the program's own message beneath (CONVNOTE2d2; it may hold a path).</summary>
    public static Noted NeverReached(string message) =>
        Noted.Of(NoteCodes.WordsNotReached, "— what the person added never reached it:")
            .Then(" ", Noted.Said(message, NoteBy.Program));

    /// <summary>A message the agent's process had gone before it could take (CONVNOTE2d2).</summary>
    public static Noted NotSent() => Noted.Of(NoteCodes.WordsNotSent, "the message could not be sent: the agent had gone.");

    /// <summary>The words a conversation started with that could not be taken off their session's record, the service's message beneath (CONVNOTE2d2).</summary>
    public static Noted StartNotTaken(string session, string message) =>
        Noted.Of(
                NoteCodes.WordsStartNotTaken, $"— the words this conversation started with could not be taken off session `{session}`:",
                ("session", session))
            .Then(" ", Noted.Said(message, NoteBy.Program));

    /// <summary>Held words the record would not keep once it ended, the service's message beneath (CONVNOTE2d2).</summary>
    public static Noted NotKept(string message) =>
        Noted.Of(NoteCodes.WordsNotKept, "— what you said as it wound up was not kept for it to go on with:")
            .Then(" ", Noted.Said(message, NoteBy.Program));

    /// <summary>
    /// Words the person sent that never reached the session, and why. <paramref name="never"/> is the English between the count
    /// and the reason, which the two doors have always worded a little differently.
    /// </summary>
    public static Noted Unreached(int count, string never, string why) =>
        Noted.Of(
            NoteCodes.WordsUnreached, $"— {count} message(s) the person sent {never}: {UnreachedWhy.English(why)}.",
            ("count", count), ("why", why));

    /// <summary>Words handed to a session that could not be taken off its record, the service's own message beneath.</summary>
    public static Noted NotTaken(string message) =>
        Noted.Of(NoteCodes.WordsNotTaken, "— your words could not be taken off its record:")
            .Then(" ", Noted.Said(message, NoteBy.Program));
}

/// <summary>
/// The notes for a turn that ended while the session's own background work runs (CONVNOTE2f, BGWAIT1): each English as its
/// site always wrote it, byte for byte, with its parts.
/// </summary>
public static class BackgroundNotes
{
    /// <summary>Whether the quest waits on that work could not be read, the program's own message beneath (it may hold a path).</summary>
    public static Noted Unread(string message) =>
        Noted.Of(NoteCodes.BackgroundUnread,
                "— its turn ended while its own background work runs, and whether its quest waits on that work could not be read:")
            .Then(" ", Noted.Said(message, NoteBy.Program));

    /// <summary>The session waits for the work it names.</summary>
    public static Noted Waits(IReadOnlyList<string> works) =>
        Noted.Of(
            NoteCodes.BackgroundWaits,
            $"— its turn ended while its own background work runs: {string.Join(", ", works.Select(name => $"`{name}`"))}. "
            + "The session waits for that work, and goes on when it ends.",
            ("works", works));

    /// <summary>Each work and the state it ended in, as the English and the prompt to the agent both say them.</summary>
    public static string EndedSaid(IEnumerable<(string Name, string State)> ended) =>
        string.Join("; ", ended.Select(work => $"`{work.Name}` ({work.State})"));

    /// <summary>The work ended and the session goes on, a part to each work.</summary>
    public static Noted Ended(IReadOnlyList<(string Name, string State)> ended)
    {
        var noted = new Noted("— its background work ended: ", []);
        for (var each = 0; each < ended.Count; each++)
        {
            var (name, state) = ended[each];
            noted = noted.Then(each == 0 ? "" : "; ",
                Noted.Of(NoteCodes.BackgroundWorkEnded, EndedSaid([ended[each]]), ("work", name), ("state", state)));
        }

        return noted.Then(". ", Noted.Of(NoteCodes.BackgroundGoesOn, "The session goes on."));
    }
}

/// <summary>
/// The states background work is known to end in (CONVNOTE2i, D152's CONVNOTE2f note): the page words each from
/// <c>note.background.state.*</c> and shows any other as the harness sent it, so the record's <c>state</c> stays an open value.
/// </summary>
public static class BackgroundState
{
    /// <summary>The harness said it completed.</summary>
    public const string Completed = "completed";

    /// <summary>The harness said it failed.</summary>
    public const string Failed = "failed";

    /// <summary>The harness said it was stopped.</summary>
    public const string Stopped = "stopped";

    /// <summary>The harness kept no state for it (AcpBackground's fallback).</summary>
    public const string Ended = "ended";

    /// <summary>Every known state, which the page's list is held to key for key.</summary>
    public static readonly IReadOnlyList<string> Known = [Completed, Failed, Stopped, Ended];
}

/// <summary>
/// The notes for a conversation's door that failed (CONVNOTE2e): each English as its site always wrote it, byte for byte, with
/// its parts. An exception's message is a program's words, never a value, since it may hold a path.
/// </summary>
public static class DoorNotes
{
    /// <summary>The protocol session could not open, the program's own message beneath (the chat runner's record).</summary>
    public static Noted NotOpen(string message) =>
        Noted.Of(NoteCodes.DoorNotOpen, "the ACP session could not open:").Then(" ", Noted.Said(message, NoteBy.Program));

    /// <summary>A conversation's turn that could not be taken, the program's own message beneath (the chat runner's record).</summary>
    public static Noted TurnNotTaken(string message) =>
        Noted.Of(NoteCodes.DoorTurnNotTaken, "the turn could not be taken:").Then(" ", Noted.Said(message, NoteBy.Program));

    /// <summary>A driven session's protocol session that failed, the program's own message beneath.</summary>
    public static Noted Failed(string message) =>
        Noted.Of(NoteCodes.DoorFailed, "— the ACP session failed:").Then(" ", Noted.Said(message, NoteBy.Program));

    /// <summary>A conversation the agent would not resume (MSG1c): its refusal, as the chat runner has always said it.</summary>
    /// <param name="ranOn">The adapter it ran on, which <c>elsewhere</c>'s line names; null where none is known.</param>
    public static Noted ResumeRefused(AcpResumeRefused refused, string? ranOn) => Refusal(refused, ranOn).Then(".");

    /// <summary>An answer the agent would not resume its conversation for (ANSWER1a): its refusal, then that it is carried on.</summary>
    /// <param name="ranOn">The adapter it ran on, which <c>elsewhere</c>'s line names; null where none is known.</param>
    public static Noted AnswerCarriedOn(AcpResumeRefused refused, string? ranOn) =>
        Refusal(refused, ranOn)
            .Then("; ", Noted.Of(NoteCodes.DoorAnswerCarriedOn, "your answer is carried on in a new session"))
            .Then(".");

    /// <summary>
    /// The refusal's message after the lead's dash: its reason's sentence, coded by its reason, then the agent's words where it
    /// refused in any, as <see cref="AcpResumeRefused"/> composes its message.
    /// </summary>
    private static Noted Refusal(AcpResumeRefused refused, string? ranOn)
    {
        var said = new Noted("— ", []).Then("", Noted.Of(NoteCodes.DoorResumeRefused, refused.Why.Sentence, NoteCodes.Reason(refused.Why, ranOn)));
        return refused.Words is { Length: > 0 } words ? said.Then(": ", Noted.Said(words, NoteBy.Agent)) : said;
    }
}

/// <summary>
/// The notes for a review set-up's record (CONVNOTE2h): each English as its site always wrote it, byte for byte, without the
/// "— " lead the conversation's event adds, with its parts. The service's message and the served sentence are a program's words.
/// </summary>
public static class ReviewNotes
{
    /// <summary>No review rule names the environment any more, so its kind is not known.</summary>
    public static Noted NoRule(string named, string to) =>
        Noted.Of(
            NoteCodes.ReviewNoRule,
            $"its set-up was not posted: no review rule here names `{named}` for `{to}` any more, so its kind is not known. "
            + "Declare it, and its next end posts it.",
            ("named", named), ("to", to));

    /// <summary>The tree's commit could not be read.</summary>
    public static Noted NoCommit() => Noted.Of(NoteCodes.ReviewNoCommit, "its set-up was not posted: its tree's commit could not be read.");

    /// <summary>Posted with the commit Daoris read, the service's message and then what was served, each as its own words.</summary>
    /// <param name="commit">The full commit id; the 8-character short id is the value.</param>
    public static Noted Posted(string commit, string named, string message, string? served)
    {
        var noted = Noted.Of(
                NoteCodes.ReviewPosted, $"Daoris read `{TextBounds.ShortCommit(commit, 8)}` from its tree and posted what it showed in `{named}`:",
                ("commit", TextBounds.ShortCommit(commit, 8)),("named", named))
            .Then(" ", Noted.Said(message, NoteBy.Program));
        return served is { Length: > 0 }
            ? noted.Then(" ", Noted.Said($"{char.ToUpperInvariant(served[0])}{served[1..]}", NoteBy.Program))
            : noted;
    }

    /// <summary>Not posted, the program's message beneath; <paramref name="next"/> where it is the failure the next end retries.</summary>
    public static Noted NotPosted(string message, bool next)
    {
        var noted = Noted.Of(NoteCodes.ReviewNotPosted, "its set-up was not posted:").Then(" ", Noted.Said(message, NoteBy.Program));
        return next ? noted.Then(". ", Noted.Of(NoteCodes.ReviewPostsNext, "Its next end posts it.")) : noted;
    }
}

/// <summary>
/// Whose words a resumed conversation goes on with, by code (CONVNOTE2a): the page's <c>note.resumed.why.*</c>.
/// </summary>
public static class ResumedWhy
{
    /// <summary>The person's answer to a park.</summary>
    public const string Answer = "answer";

    /// <summary>The person's words to a conversation that had ended.</summary>
    public const string Words = "words";

    /// <summary>Another agent's findings alone.</summary>
    public const string Findings = "findings";

    /// <summary>The person's answer and another agent's findings.</summary>
    public const string AnswerAndFindings = "answer-and-findings";

    /// <summary>The person's words and another agent's findings.</summary>
    public const string WordsAndFindings = "words-and-findings";

    /// <summary>A helper's answer alone (MIXAGENT2a-ii).</summary>
    public const string Helper = "helper";

    /// <summary>The person's words and a helper's answer.</summary>
    public const string WordsAndHelper = "words-and-helper";
}

/// <summary>
/// Why a start ran on its account, by code (UX7d-1): each step of the walk as a conversation's first line says it
/// (<see cref="RotationWords.Steps"/>), the page's <c>note.opening.why.*</c>.
/// </summary>
public static class OpeningWhy
{
    /// <summary>The account its scope begins at is cooling.</summary>
    public const string Cooling = "cooling";

    /// <summary>Its provider refused the account its scope begins at.</summary>
    public const string Refused = "refused";

    /// <summary>The account its scope begins at is not signed in.</summary>
    public const string SignedOut = "signed-out";

    /// <summary>It runs on the account kept for conversations, which is where its scope begins.</summary>
    public const string KeptSelf = "kept-self";

    /// <summary>A conversation passed the kept account, which conversations take last.</summary>
    public const string KeptLast = "kept-last";

    /// <summary>Driven work passed the kept account.</summary>
    public const string Kept = "kept";

    /// <summary>The account passed said a limit is reached.</summary>
    public const string NearReached = "near-reached";

    /// <summary>
    /// The account passed said it is near a limit: its pace warning, which is no longer near (USAGEREAD3a), so nothing
    /// writes it now; a note written before keeps it.
    /// </summary>
    public const string NearWord = "near-word";

    /// <summary>The account passed said it is drawing on usage credits.</summary>
    public const string NearCredits = "near-credits";

    /// <summary>
    /// The account passed has used a window at or over what counts as near: the scope's one number, which the curve replaced
    /// (SWITCH1b-ii), so nothing writes it now; a note written before keeps it.
    /// </summary>
    public const string NearUsed = "near-used";

    /// <summary>The account passed has used a window over its curve: the window, its use and the curve's value now (SWITCH1b-ii).</summary>
    public const string OverCurve = "over-curve";

    /// <summary>The account passed is near its limit, with nothing it said to name.</summary>
    public const string Near = "near";

    /// <summary>
    /// Both it and the account weighed against are near; it has the more room (ACCTWALK3). Said by the other's own reason
    /// since the curve (SWITCH1b-ii), so nothing writes it now; a note written before keeps it.
    /// </summary>
    public const string Room = "room";

    /// <summary>It runs the fewest of Daoris's sessions.</summary>
    public const string Fewest = "fewest";

    /// <summary>Its week resets first.</summary>
    public const string Lapsing = "lapsing";

    /// <summary>It has the most room under its curve, on its tightest window (SWITCH1b-ii, the switch design §3).</summary>
    public const string MostRoom = "most-room";

    /// <summary>It is furthest behind its week's pace: written under <c>switch: off</c> only since the curve (SWITCH1b-ii).</summary>
    public const string Behind = "behind";

    /// <summary>The account it was weighed against is ahead of its week's pace: under <c>switch: off</c> only, as <see cref="Behind"/>.</summary>
    public const string Ahead = "ahead";

    /// <summary>Daoris has not started on it yet.</summary>
    public const string NotStarted = "not-started";

    /// <summary>Daoris started on it least recently.</summary>
    public const string LeastRecent = "least-recent";

    /// <summary>It is a workspace's default.</summary>
    public const string Default = "default";

    /// <summary>It is this machine's default.</summary>
    public const string DefaultHere = "default-here";

    /// <summary>It comes first in a workspace's list.</summary>
    public const string First = "first";

    /// <summary>It comes first in this machine's list.</summary>
    public const string FirstHere = "first-here";
}

/// <summary>Why a cool-off lasts until then, by code (TOOL4d): the page's <c>harness.cooling.why.*</c>.</summary>
public static class CoolingWhy
{
    /// <summary>The agent named the time.</summary>
    public const string Stated = "stated";

    /// <summary>The agent named the time without a zone, read in this machine's.</summary>
    public const string Assumed = "assumed";

    /// <summary>The agent named no time: Daoris's default.</summary>
    public const string Default = "default";

    /// <summary>The agent named a date more than 8 days off: Daoris's default.</summary>
    public const string NotBelieved = "notBelieved";

    /// <summary>The code for an entry, read as <see cref="CoolingWords"/> reads it for its sentence.</summary>
    public static string Of(CoolingEntry entry) =>
        entry.Stated ? (entry.AssumedZone ? Assumed : Stated) : entry.NotBelieved ? NotBelieved : Default;
}
