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
/// part passes through this one unchanged.
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
        if (Text(part, "code") is { Length: > 0 } code)
        {
            var values = new List<KeyValuePair<string, object>>();
            if (part.TryGetProperty("values", out var given) && given.ValueKind == JsonValueKind.Object)
            {
                foreach (var value in given.EnumerateObject())
                {
                    values.Add(new(value.Name, ReadValue(value.Value)));
                }
            }

            return new NotePart { Code = code, Values = values, Text = Text(part, "text") };
        }

        return Text(part, "words") is { } words ? NotePart.Said(words, Text(part, "by") ?? NoteBy.Before) : null;
    }

    private static object ReadValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number when value.TryGetInt64(out var number) => number,
        JsonValueKind.Array when value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String) =>
            (IReadOnlyList<string>)[.. value.EnumerateArray().Select(item => item.GetString()!)],
        _ => value.Clone(),
    };

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
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
    ]);

    /// <summary>Why a cool-off lasts until then, as the page words it (<c>harness.cooling.why.*</c>).</summary>
    public static readonly NoteReasons Cooling = new(
    [
        new(CoolingWhy.Stated, [], "harness.cooling.why.stated"),
        new(CoolingWhy.Assumed, [], "harness.cooling.why.assumed"),
        new(CoolingWhy.Default, [], "harness.cooling.why.default"),
        new(CoolingWhy.NotBelieved, [], "harness.cooling.why.notBelieved"),
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
    public static readonly NoteCode EndedTimeout = new("ended.timeout", ["minutes"]);
    public static readonly NoteCode EndedStopped = new("ended.stopped", []);
    public static readonly NoteCode EndedLostClaim = new("ended.lost-claim", []);
    public static readonly NoteCode EndedDriverClosed = new("ended.driver-closed", []);

    // ——— An account's line, appended to a failure.
    public static readonly NoteCode AccountCooling = new("account.cooling", ["until", "why"]) { Why = Cooling };
    public static readonly NoteCode AccountRefused = new("account.refused", ["owner"]);
    public static readonly NoteCode AccountRefusedOwn = new("account.refused-own", ["owner"]);

    // ——— While it starts and works, replaced by its end (D51 rule 4).
    public static readonly NoteCode StartedTree = new("started.tree", ["branch", "basedOn"]);
    public static readonly NoteCode StartedTreeUnrecorded = new("started.tree-unrecorded", []);
    public static readonly NoteCode StartedResumesAnswered = new("started.resumes-answered", ["quest", "answered"]);
    public static readonly NoteCode StartedInAskingTree = new("started.in-asking-tree", ["session"]);
    public static readonly NoteCode StartedWordsOpen = new("started.words-open", ["quest", "session"]);
    public static readonly NoteCode StartedWordsTaken = new("started.words-taken", ["quest", "session"]);
    public static readonly NoteCode StartedAnswer = new("started.answer", ["quest", "session"]);
    public static readonly NoteCode StartedReleased = new("started.released", ["quest", "session"]);
    public static readonly NoteCode StartedCutOff = new("started.cut-off", ["quest", "session"]);
    public static readonly NoteCode StartedOtherAccount = new("started.other-account", []);
    public static readonly NoteCode StartedSameTree = new("started.same-tree", []);
    public static readonly NoteCode StartedFellBack = new("started.fell-back", ["why"]) { Why = Continue };
    public static readonly NoteCode WorkingResumesAnswer = new("working.resumes-answer", []);
    public static readonly NoteCode WorkingGoesOn = new("working.goes-on", []);

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
    public static readonly NoteCode LandingNoTree = new("landing.no-tree", []);
    public static readonly NoteCode LandingNotDone = new("landing.not-done", []);

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
