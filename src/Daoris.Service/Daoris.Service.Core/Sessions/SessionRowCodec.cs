using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>A session row and its words, read to the record and written back: the one place the columns' spellings are known.</summary>
internal static class SessionRowCodec
{
    public static Session Read(SqliteDataReader reader) => new(
        reader.GetString(reader.GetOrdinal("id")),
        reader.IsDBNull(reader.GetOrdinal("quest")) ? null : reader.GetString(reader.GetOrdinal("quest")),
        reader.GetString(reader.GetOrdinal("repository")),
        reader.GetString(reader.GetOrdinal("door")),
        Enum.Parse<SessionState>(reader.GetString(reader.GetOrdinal("state"))),
        reader.IsDBNull(reader.GetOrdinal("note")) ? null : reader.GetString(reader.GetOrdinal("note")),
        reader.IsDBNull(reader.GetOrdinal("evidence")) ? null : reader.GetString(reader.GetOrdinal("evidence")),
        reader.IsDBNull(reader.GetOrdinal("transcript")) ? null : reader.GetString(reader.GetOrdinal("transcript")),
        Moments.ReadRequired(reader.GetString(reader.GetOrdinal("created"))),
        Moments.ReadRequired(reader.GetString(reader.GetOrdinal("updated"))),
        Workspaces.Normalize(reader.GetString(reader.GetOrdinal("workspace"))),
        Enum.TryParse<SessionKind>(reader.GetString(reader.GetOrdinal("kind")), out var kind)
            ? kind
            // A row with a kind this build does not know is a DRIVEN record as far as anything here
            // can act on it: the lifecycle is identical, and refusing to read a record would lose the
            // trace of work that actually happened.
            : SessionKind.Driven,
        reader.IsDBNull(reader.GetOrdinal("door_version"))
            ? null : reader.GetString(reader.GetOrdinal("door_version")),
        reader.IsDBNull(reader.GetOrdinal("profile")) ? null : reader.GetString(reader.GetOrdinal("profile")),
        reader.IsDBNull(reader.GetOrdinal("tree")) ? null : reader.GetString(reader.GetOrdinal("tree")),
        reader.IsDBNull(reader.GetOrdinal("base_commit"))
            ? null : reader.GetString(reader.GetOrdinal("base_commit")))
    {
        Origin = reader.IsDBNull(reader.GetOrdinal("origin")) ? null : reader.GetString(reader.GetOrdinal("origin")),
        Ask = reader.IsDBNull(reader.GetOrdinal("ask")) ? null : reader.GetString(reader.GetOrdinal("ask")),
        Opinion = reader.IsDBNull(reader.GetOrdinal("opinion")) ? null : reader.GetString(reader.GetOrdinal("opinion")),
        Serves = reader.IsDBNull(reader.GetOrdinal("serves")) ? null : reader.GetString(reader.GetOrdinal("serves")),
        Piece = reader.IsDBNull(reader.GetOrdinal("piece")) ? null : reader.GetString(reader.GetOrdinal("piece")),
        From = reader.IsDBNull(reader.GetOrdinal("from_session")) ? null : reader.GetString(reader.GetOrdinal("from_session")),
        FromWhy = reader.IsDBNull(reader.GetOrdinal("from_why")) ? null : reader.GetString(reader.GetOrdinal("from_why")),
        FromBy = reader.IsDBNull(reader.GetOrdinal("from_by")) ? null : reader.GetString(reader.GetOrdinal("from_by")),
        Entry = reader.IsDBNull(reader.GetOrdinal("entry")) ? null : reader.GetString(reader.GetOrdinal("entry")),
        Took = !reader.IsDBNull(reader.GetOrdinal("took")) && reader.GetInt64(reader.GetOrdinal("took")) != 0,
        TookQuest = reader.IsDBNull(reader.GetOrdinal("took_quest")) ? null : reader.GetString(reader.GetOrdinal("took_quest")),
        Said = SaidOf(reader),
        Interrupted = !reader.IsDBNull(reader.GetOrdinal("interrupted")) && reader.GetInt64(reader.GetOrdinal("interrupted")) != 0,
        Limit = !reader.IsDBNull(reader.GetOrdinal("limited")) && reader.GetInt64(reader.GetOrdinal("limited")) != 0,
        Pushed = !reader.IsDBNull(reader.GetOrdinal("pushed")) && reader.GetInt64(reader.GetOrdinal("pushed")) != 0,
        NoteParts = reader.IsDBNull(reader.GetOrdinal("note_parts")) ? null : reader.GetString(reader.GetOrdinal("note_parts")),
        HelpMode = HelpModeRead(
            reader.GetString(reader.GetOrdinal("repository")),
            reader.IsDBNull(reader.GetOrdinal("help_mode")) ? null : reader.GetString(reader.GetOrdinal("help_mode"))),
        Access = SessionAccess.Read(reader.IsDBNull(reader.GetOrdinal("access")) ? null : reader.GetString(reader.GetOrdinal("access"))),
        Resolved = ResolvedRead(reader.IsDBNull(reader.GetOrdinal("resolved")) ? null : reader.GetString(reader.GetOrdinal("resolved"))),
        Actor = reader.IsDBNull(reader.GetOrdinal("actor")) ? null : reader.GetString(reader.GetOrdinal("actor")),
        Choice = reader.IsDBNull(reader.GetOrdinal("choice")) ? null : reader.GetString(reader.GetOrdinal("choice")),
        Asked = ActorElement(reader, "asked"),
        Override = ActorElement(reader, "actor_override"),
        Ran = ActorElement(reader, "ran"),
        Chosen = ActorElement(reader, "chosen"),
    };

    /// <summary>One of the actor columns' provenance objects as kept; null for none.</summary>
    private static JsonElement? ActorElement(SqliteDataReader reader, string column) =>
        reader.IsDBNull(reader.GetOrdinal(column)) ? null : JsonSerializer.Deserialize<JsonElement>(reader.GetString(reader.GetOrdinal(column)));

    /// <summary>A record's handed versions as kept (KEEP1b): the list, in order; null for none, or for a column that does not read as one.</summary>
    public static IReadOnlyList<string>? ResolvedRead(string? kept)
    {
        if (kept is null) return null;
        try
        {
            using var document = JsonDocument.Parse(kept);
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? [.. document.RootElement.EnumerateArray()
                    .Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// A record's access mode as kept (HELPMODE1a2): Ask Daoris's as written, or <see cref="SessionLedger.HelpRestricted"/> where none
    /// was, a record from before the modes; null for every other record, which runs under none.
    /// </summary>
    public static string? HelpModeRead(string repository, string? kept) =>
        string.Equals(repository, SessionLedger.HelpRepository, StringComparison.Ordinal) ? kept ?? SessionLedger.HelpRestricted : null;

    /// <summary>Whitespace is nothing said, not a value: an empty version reads as a version of "".</summary>
    public static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The record's words: its list.</summary>
    private static IReadOnlyList<SaidWord> SaidOf(SqliteDataReader reader)
    {
        var said = reader.GetOrdinal("said");
        return reader.IsDBNull(said) ? [] : SaidWords(reader.GetString(said));
    }

    /// <summary>
    /// The words of a list as stored, in order. A word missing its id, its words or its moment is passed over, and a field
    /// this build does not know is ignored: a later build's word, or a malformed one, is never a failed read of the record.
    /// </summary>
    private static IReadOnlyList<SaidWord> SaidWords(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return [];

            var words = new List<SaidWord>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (WordOf(element) is { } word) words.Add(word);
            }

            return words;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>One word of a list as stored; null for an element that is no word, which every reader passes over.</summary>
    public static SaidWord? WordOf(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || JsonFields.Text(element, "id") is not { Length: > 0 } wordId
            || JsonFields.Text(element, "text") is not { } text
            || Moments.Read(element, "at") is not { } at)
        {
            return null;
        }

        var files = JsonFields.Items(element, "files")
            .Where(file => file.ValueKind == JsonValueKind.String)
            .Select(file => file.GetString()!)
            .ToList();
        var reopens = element.TryGetProperty("reopens", out var flag) && flag.ValueKind == JsonValueKind.True;
        // XAGENT1c: whose claims these are; absent, or not a name, is the person's, which every word before it was.
        var by = JsonFields.Text(element, "by") is { Length: > 0 } opinion ? opinion : null;
        // MIXAGENT2a-i: the helper whose answer this is, its own field; absent is not a helper's.
        var helper = JsonFields.Text(element, "helper") is { Length: > 0 } helped ? helped : null;
        var channel = JsonFields.Text(element, "channel") is { Length: > 0 } carried ? carried : null;
        // COWORK1b: whose words where not the person's, and the producer's key; absent on every word before them.
        var from = JsonFields.Text(element, "from") is { Length: > 0 } author ? author : null;
        var key = JsonFields.Text(element, "key") is { Length: > 0 } produced ? produced : null;
        // COWORK1c: its level, a later build's word read as none, and when a dispatcher first wrote it to a harness.
        var level = JsonFields.Text(element, "level") is { Length: > 0 } asked && SaidLevel.Known(asked) ? asked : null;
        var attempt = Moments.Read(element, "attempt");
        // SUPERVISOR1a-i: what a supervisor's word claims, read as claims; absent on every other word.
        var decision = JsonFields.Text(element, "decision") is { Length: > 0 } decided ? decided : null;
        var question = JsonFields.Text(element, "question") is { Length: > 0 } questioned ? questioned : null;
        var choice = JsonFields.Text(element, "choice") is { Length: > 0 } chose ? chose : null;
        return new SaidWord(wordId, text, at, files, reopens, by, helper, channel, from, key, level, attempt, decision, question, choice);
    }

    // Hand-rolled for the reason every store's lists are: nothing here may stop working under AOT.
    public static string SaidJson(IEnumerable<SaidWord> words) => JsonFields.Written(writer =>
    {
        writer.WriteStartArray();
        foreach (var word in words) Write(writer, word);
        writer.WriteEndArray();
    });

    public static string WordJson(SaidWord word) => JsonFields.Written(writer => Write(writer, word));

    private static void Write(Utf8JsonWriter writer, SaidWord word)
    {
        writer.WriteStartObject();
        writer.WriteString("id", word.Id);
        writer.WriteString("text", word.Text);
        writer.WriteString("at", Moments.Write(word.At));
        writer.WriteStartArray("files");
        foreach (var file in word.Files) writer.WriteStringValue(file);
        writer.WriteEndArray();
        if (word.Reopens) writer.WriteBoolean("reopens", true);
        if (word.By is not null) writer.WriteString("by", word.By);
        if (word.Helper is not null) writer.WriteString("helper", word.Helper);
        if (word.Channel is not null) writer.WriteString("channel", word.Channel);
        if (word.From is not null) writer.WriteString("from", word.From);
        if (word.Key is not null) writer.WriteString("key", word.Key);
        if (word.Level is not null) writer.WriteString("level", word.Level);
        if (word.Attempt is { } attempt) writer.WriteString("attempt", Moments.Write(attempt));
        if (word.Decision is not null) writer.WriteString("decision", word.Decision);
        if (word.Question is not null) writer.WriteString("question", word.Question);
        if (word.Choice is not null) writer.WriteString("choice", word.Choice);
        writer.WriteEndObject();
    }
}
