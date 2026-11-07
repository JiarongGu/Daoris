using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// How a quest, its parts and its log rows are written to and read from the store's columns, and how the log and the wire write the
/// parts they share. Written and read by hand rather than through the reflection serializer, so nothing here may quietly stop working
/// under AOT. The store's own columns and log, which only this service writes, are read as written; the wire judges what it reads instead.
/// </summary>
internal static class QuestRecordCodec
{
    /// <summary>A quest as its row holds it.</summary>
    internal static Quest Read(SqliteDataReader reader) => new(
        reader.GetString(reader.GetOrdinal("id")),
        reader.GetString(reader.GetOrdinal("sender")),
        reader.GetString(reader.GetOrdinal("receiver")),
        reader.GetString(reader.GetOrdinal("title")),
        reader.GetString(reader.GetOrdinal("body")),
        Enum.Parse<QuestStatus>(reader.GetString(reader.GetOrdinal("status"))),
        reader.IsDBNull(reader.GetOrdinal("note")) ? null : reader.GetString(reader.GetOrdinal("note")),
        Moments.ReadRequired(reader.GetString(reader.GetOrdinal("filed"))),
        Moments.ReadRequired(reader.GetString(reader.GetOrdinal("updated"))),
        Workspaces.Normalize(reader.GetString(reader.GetOrdinal("workspace"))))
    {
        Links = ReadLinks(reader.GetString(reader.GetOrdinal("links"))),
        Attachments = ReadAttachments(reader.GetString(reader.GetOrdinal("attachments"))),
        Then = ReadSteps(reader.GetString(reader.GetOrdinal("then_steps"))),
        Parent = reader.IsDBNull(reader.GetOrdinal("parent")) ? null : reader.GetString(reader.GetOrdinal("parent")),
        Conflicts = ReadConflicts(reader.GetString(reader.GetOrdinal("conflicts"))),
        Awaits = reader.IsDBNull(reader.GetOrdinal("awaits")) ? null : reader.GetString(reader.GetOrdinal("awaits")),
        PublishedBy = reader.IsDBNull(reader.GetOrdinal("published_by")) ? null : reader.GetString(reader.GetOrdinal("published_by")),
        Lanes = ReadLinks(reader.GetString(reader.GetOrdinal("lanes"))),
        Requirements = ReadRequirements(reader.GetString(reader.GetOrdinal("requirements"))),
        Answers = ReadAnswers(reader.GetString(reader.GetOrdinal("answers"))),
        Accepted = reader.IsDBNull(reader.GetOrdinal("accepted"))
            ? null
            : Moments.ReadRequired(reader.GetString(reader.GetOrdinal("accepted"))),
        Short = reader.IsDBNull(reader.GetOrdinal("short_title")) ? null : reader.GetString(reader.GetOrdinal("short_title")),
        Weight = reader.IsDBNull(reader.GetOrdinal("weight")) ? null : reader.GetString(reader.GetOrdinal("weight")),
        Paths = reader.IsDBNull(reader.GetOrdinal("paths")) ? [] : ReadLinks(reader.GetString(reader.GetOrdinal("paths"))),
        Request = reader.IsDBNull(reader.GetOrdinal("request")) ? null : reader.GetString(reader.GetOrdinal("request")),
        NoteParts = reader.IsDBNull(reader.GetOrdinal("note_parts")) ? null : reader.GetString(reader.GetOrdinal("note_parts")),
        Evidence = reader.IsDBNull(reader.GetOrdinal("evidence")) ? null : ReadVerdict(reader.GetString(reader.GetOrdinal("evidence"))),
        Review = reader.IsDBNull(reader.GetOrdinal("review")) ? null : ReadColumn(reader.GetString(reader.GetOrdinal("review")), Reviews.JudgedReview),
        SetUpIn = reader.IsDBNull(reader.GetOrdinal("set_up_in")) ? null : reader.GetString(reader.GetOrdinal("set_up_in")),
        SetUps = ReadList(reader.GetString(reader.GetOrdinal("set_ups")), Reviews.JudgedSetUp),
        Verdicts = ReadList(reader.GetString(reader.GetOrdinal("verdicts")), Reviews.JudgedVerdict),
        Closure = reader.IsDBNull(reader.GetOrdinal("closure")) ? null : reader.GetString(reader.GetOrdinal("closure")),
        Submission = reader.IsDBNull(reader.GetOrdinal("submission")) ? null : ReadSubmission(reader.GetString(reader.GetOrdinal("submission"))),
        GrewFrom = ReadRefColumn(reader, "grew_from"),
        SourceReopened = ReadRefColumn(reader, "source_reopened"),
        Reopened = reader.IsDBNull(reader.GetOrdinal("reopened")) ? null : ReadReopen(reader.GetString(reader.GetOrdinal("reopened"))),
        WorkflowChoices = ReadList(reader.GetString(reader.GetOrdinal("workflow_choices")), WorkflowChoices.Judged),
    };

    private static QuestSubmission ReadSubmission(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new QuestSubmission(
            root.GetProperty("machine").GetString() ?? "", root.GetProperty("sequence").GetInt64(),
            Moments.ReadRequired(root.GetProperty("at").GetString()!),
            root.TryGetProperty("note", out var note) ? note.GetString() : null,
            root.TryGetProperty("session", out var session) ? session.GetString() : null);
    }

    /// <summary>A quest's own reopen as its column keeps it; one not whole is none, and a row from before its session reads none.</summary>
    private static QuestReopen? ReadReopen(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return ReadRef(root) is { } named && root.TryGetProperty("of", out var of) && ReadRef(of) is { } reopened
            && root.TryGetProperty("at", out var at) && at.GetString() is { } when
            ? new QuestReopen(
                named.Machine, named.Sequence, reopened, root.TryGetProperty("note", out var note) ? note.GetString() : null,
                Moments.ReadRequired(when), root.TryGetProperty("session", out var session) ? session.GetString() : null)
            : null;
    }

    private static T? ReadColumn<T>(string json, Func<JsonElement, T?> judged) where T : class
    {
        using var document = JsonDocument.Parse(json);
        return judged(document.RootElement);
    }

    /// <summary>A list of a column, each item read through its judge; one that is not whole is passed over.</summary>
    private static IReadOnlyList<T> ReadList<T>(string json, Func<JsonElement, T?> judged) where T : class
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind != JsonValueKind.Array
            ? []
            : [.. document.RootElement.EnumerateArray().Select(judged).OfType<T>()];
    }

    private static QuestEvidenceVerdict? ReadVerdict(string json)
    {
        using var document = JsonDocument.Parse(json);
        return QuestEvidenceVerdict.Judged(document.RootElement);
    }

    /// <summary>An operation as its log row holds it (<see cref="QuestStore.OperationColumns"/>).</summary>
    internal static QuestOperation ReadOperation(SqliteDataReader reader)
    {
        using var document = JsonDocument.Parse(reader.GetString(5));
        return ReadPayload(
            new QuestOperation(
                reader.GetString(0), Enum.Parse<QuestOperationKind>(reader.GetString(1), ignoreCase: true), reader.GetString(2),
                reader.GetInt64(3), Moments.ReadRequired(reader.GetString(4)), Number: reader.IsDBNull(6) ? null : reader.GetInt64(6)),
            document.RootElement);
    }

    /// <summary>
    /// A row's payload read onto <paramref name="read"/>, its columns: what every kind reads, then the kind's own by the operation
    /// registered for it (<see cref="IQuestOperation.ReadPayload"/>).
    /// </summary>
    internal static QuestOperation ReadPayload(QuestOperation read, JsonElement payload)
    {
        var note = payload.TryGetProperty("note", out var said) ? said.GetString() : null;
        // Absent from every operation before note parts, and from every note written without them.
        var parts = note is not null && payload.TryGetProperty("noteParts", out var noted) ? NoteParts.Normalize(noted) : null;
        // Absent from every done before answers, and from one on a quest with no requirements.
        var answers = payload.TryGetProperty("answers", out var answered) ? ReadAnswers(answered) : null;
        return QuestOperations.Registered.For(read.Kind)!.ReadPayload(read with { Note = note, NoteParts = parts, Answers = answers }, payload);
    }

    /// <summary>
    /// What an operation's log row carries: its kind's payload, by the operation registered for it
    /// (<see cref="IQuestOperation.WritePayload"/>).
    /// </summary>
    internal static string PayloadJson(QuestOperation operation) =>
        JsonFields.Written(writer => QuestOperations.Registered.For(operation.Kind)!.WritePayload(writer, operation));

    /// <summary>How a kind is written in the log: its name in lowercase, as the design names it.</summary>
    internal static string KindText(QuestOperationKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>
    /// The session that said an operation, absent from every operation of a build before it,
    /// and from one no session said.
    /// </summary>
    internal static string? ReadSession(JsonElement payload) =>
        payload.TryGetProperty("session", out var speaker) && speaker.ValueKind == JsonValueKind.String ? speaker.GetString() : null;

    /// <summary>A close's provenance, or null where it is not whole: a key and a token, both strings.</summary>
    internal static QuestClose? ReadClose(JsonElement close) =>
        close.ValueKind == JsonValueKind.Object
        && close.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String
        && close.TryGetProperty("token", out var token) && token.ValueKind == JsonValueKind.String
        && key.GetString() is { Length: > 0 } named && token.GetString() is { Length: > 0 } computed
            ? new QuestClose(named, computed)
            : null;

    /// <summary>A close's provenance, as the log and the wire write it.</summary>
    internal static void WriteClose(Utf8JsonWriter writer, QuestClose close)
    {
        writer.WriteStartObject("close");
        writer.WriteString("key", close.Key);
        writer.WriteString("token", close.Token);
        writer.WriteEndObject();
    }

    /// <summary>An operation named by its machine and sequence, as the log and the wire write it.</summary>
    internal static void WriteRef(Utf8JsonWriter writer, string name, QuestOperationRef named)
    {
        writer.WriteStartObject(name);
        WriteRefFields(writer, named);
        writer.WriteEndObject();
    }

    /// <summary>The fields of an operation's name, for a caller that writes them into an object of its own.</summary>
    internal static void WriteRefFields(Utf8JsonWriter writer, QuestOperationRef named)
    {
        writer.WriteString("machine", named.Machine);
        writer.WriteNumber("sequence", named.Sequence);
    }

    private static QuestOperationRef? ReadRefColumn(SqliteDataReader reader, string column)
    {
        var at = reader.GetOrdinal(column);
        if (reader.IsDBNull(at)) return null;
        using var document = JsonDocument.Parse(reader.GetString(at));
        return ReadRef(document.RootElement);
    }

    /// <summary>An operation's name as the log and the wire hold it, or null where it is not whole: a machine that is a non-empty string, and a positive sequence.</summary>
    internal static QuestOperationRef? ReadRef(JsonElement named) =>
        named.ValueKind == JsonValueKind.Object
        && named.TryGetProperty("machine", out var machine) && machine.ValueKind == JsonValueKind.String
        && machine.GetString() is { Length: > 0 } at
        && named.TryGetProperty("sequence", out var sequence) && sequence.ValueKind == JsonValueKind.Number
        && sequence.TryGetInt64(out var count) && count > 0
            ? new QuestOperationRef(at, count)
            : null;

    /// <summary>
    /// A note's parts beside it: a list, only where the note is written with some, so an operation without them is
    /// recorded exactly as before, and a reader from before ignores the field.
    /// </summary>
    internal static void WriteNoteParts(Utf8JsonWriter writer, string? note, string? noteParts)
    {
        if (note is null || NoteParts.Element(noteParts) is not { } parts) return;
        writer.WritePropertyName("noteParts");
        parts.WriteTo(writer);
    }

    internal static string AnswersJson(IReadOnlyList<QuestAnswer> answers) => JsonFields.Written(writer => WriteAnswers(writer, answers));

    /// <summary>A done's answers, one shape in the cache, the log's payload and the wire: each its requirement's number and either <c>met</c>, or <c>departed</c> with the <c>quote</c> it turns on.</summary>
    internal static void WriteAnswers(Utf8JsonWriter writer, IReadOnlyList<QuestAnswer> answers)
    {
        writer.WriteStartArray();
        foreach (var answer in answers)
        {
            writer.WriteStartObject();
            writer.WriteNumber("requirement", answer.Requirement);
            if (answer.Met is not null) writer.WriteString("met", answer.Met);
            if (answer.Departed is not null) writer.WriteString("departed", answer.Departed);
            if (answer.Quote is not null) writer.WriteString("quote", answer.Quote);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static IReadOnlyList<QuestAnswer> ReadAnswers(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ReadAnswers(document.RootElement);
    }

    private static IReadOnlyList<QuestAnswer> ReadAnswers(JsonElement answers) =>
        answers.EnumerateArray()
            .Select(item => new QuestAnswer(
                item.GetProperty("requirement").GetInt32(),
                item.TryGetProperty("met", out var met) ? met.GetString() : null,
                item.TryGetProperty("departed", out var departed) ? departed.GetString() : null,
                item.TryGetProperty("quote", out var quote) ? quote.GetString() : null))
            .ToList();

    internal static string ConflictsJson(IReadOnlyList<QuestConflict> conflicts) => JsonFields.Written(writer =>
    {
        writer.WriteStartArray();
        foreach (var conflict in conflicts)
        {
            writer.WriteStartObject();
            writer.WriteString("machine", conflict.Machine);
            writer.WriteNumber("sequence", conflict.Sequence);
            writer.WriteString("attempted", conflict.Attempted.ToString());
            if (conflict.Note is not null) writer.WriteString("note", conflict.Note);
            writer.WriteString("at", Moments.Write(conflict.At));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    });

    private static IReadOnlyList<QuestConflict> ReadConflicts(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(item => new QuestConflict(
                item.GetProperty("machine").GetString() ?? "",
                Enum.Parse<QuestStatus>(item.GetProperty("attempted").GetString()!, ignoreCase: true),
                item.TryGetProperty("note", out var note) ? note.GetString() : null,
                Moments.ReadRequired(item.GetProperty("at").GetString()!),
                // Absent from a cache written before dismissals: zero, which the open refills.
                item.TryGetProperty("sequence", out var sequence) ? sequence.GetInt64() : 0))
            .ToList();
    }

    internal static string StepsJson(IReadOnlyList<QuestStep> steps) => JsonFields.Written(writer => WriteSteps(writer, steps));

    internal static void WriteSteps(Utf8JsonWriter writer, IReadOnlyList<QuestStep> steps)
    {
        writer.WriteStartArray();
        foreach (var step in steps)
        {
            writer.WriteStartObject();
            writer.WriteString("to", step.To);
            writer.WriteString("title", step.Title);
            writer.WriteString("body", step.Body);
            // Only on a set-up step: every other step reads as it did.
            if (step.SetUpIn is not null) writer.WriteString("setUpIn", step.SetUpIn);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static IReadOnlyList<QuestStep> ReadSteps(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ReadSteps(document.RootElement);
    }

    internal static IReadOnlyList<QuestStep> ReadSteps(JsonElement steps) =>
        steps.EnumerateArray()
            .Select(item => new QuestStep(
                item.GetProperty("to").GetString() ?? "",
                item.GetProperty("title").GetString() ?? "",
                item.GetProperty("body").GetString() ?? "")
            {
                SetUpIn = item.TryGetProperty("setUpIn", out var setUpIn) ? setUpIn.GetString() : null,
            })
            .ToList();

    internal static string RequirementsJson(IReadOnlyList<QuestRequirement> requirements) =>
        JsonFields.Written(writer => WriteRequirements(writer, requirements));

    /// <summary>A quest's requirements, one shape in its column, the log's payload and the wire.</summary>
    internal static void WriteRequirements(Utf8JsonWriter writer, IReadOnlyList<QuestRequirement> requirements)
    {
        writer.WriteStartArray();
        foreach (var requirement in requirements)
        {
            writer.WriteStartObject();
            writer.WriteString("quote", requirement.Quote);
            writer.WriteString("check", requirement.Check);
            // Only when it names some: a requirement with none reads as it did.
            if (requirement.Evidence.Count > 0)
            {
                writer.WritePropertyName("evidence");
                QuestEvidence.Write(writer, requirement.Evidence);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static IReadOnlyList<QuestRequirement> ReadRequirements(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ReadRequirements(document.RootElement);
    }

    internal static IReadOnlyList<QuestRequirement> ReadRequirements(JsonElement requirements) =>
        requirements.EnumerateArray()
            .Select(item => new QuestRequirement(
                item.GetProperty("quote").GetString() ?? "",
                item.GetProperty("check").GetString() ?? "")
            {
                // Absent from every requirement before evidence, and from one that names none.
                Evidence = item.TryGetProperty("evidence", out var evidence) ? QuestEvidence.Judged(evidence) ?? [] : [],
            })
            .ToList();

    internal static string LinksJson(IReadOnlyList<string> links) => JsonFields.Written(writer => WriteLinks(writer, links));

    internal static void WriteLinks(Utf8JsonWriter writer, IReadOnlyList<string> links)
    {
        writer.WriteStartArray();
        foreach (var link in links) writer.WriteStringValue(link);
        writer.WriteEndArray();
    }

    internal static string AttachmentsJson(IReadOnlyList<QuestAttachment> attachments) =>
        JsonFields.Written(writer => WriteAttachments(writer, attachments));

    internal static void WriteAttachments(Utf8JsonWriter writer, IReadOnlyList<QuestAttachment> attachments)
    {
        writer.WriteStartArray();
        foreach (var attachment in attachments) attachment.Write(writer);
        writer.WriteEndArray();
    }

    internal static IReadOnlyList<string> ReadLinks(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ReadLinks(document.RootElement);
    }

    internal static IReadOnlyList<string> ReadLinks(JsonElement links) =>
        links.EnumerateArray()
            .Select(item => item.GetString())
            .OfType<string>()
            .ToList();

    private static IReadOnlyList<QuestAttachment> ReadAttachments(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ReadAttachments(document.RootElement);
    }

    internal static IReadOnlyList<QuestAttachment> ReadAttachments(JsonElement attachments) =>
        attachments.EnumerateArray().Select(QuestAttachment.Stored).ToList();
}
