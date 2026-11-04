using System.Text.Json;
using static Daoris.Knowledge.SessionRowCodec;
using static Daoris.Knowledge.SessionSchema;

namespace Daoris.Knowledge;

/// <summary>
/// The words waiting on a record, as the table keeps them (MSG1a, D137 §2.4): kept after the ones already there, taken off, moved to another
/// record, marked as written to a harness, and the settlements of the words that left. Blind, like every write here: whose words and where
/// they go is the ledger's to judge.
/// </summary>
internal sealed class SessionSaidStore(SessionStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>
    /// Keep a word the person said on this machine's record, after the words already waiting, leaving its state where it
    /// is (STANDDOWN2, ANSWER1b, MSG1a). With a <paramref name="note"/> (an answer's line) the note is written too, as a
    /// new revision at the word's moment, since the note travels; with none, nothing that travels changes, so no
    /// revision is written. Null when there is no such record of this machine's.
    /// </summary>
    /// <remarks>
    /// <para><b>Appended in one statement</b>, for the reason an ask's words are (REV3): a whole-list save from two
    /// doors at once would drop one of them.</para>
    /// </remarks>
    /// <param name="noteParts">The note's parts beside it (LANG1a), written with it; a note without them clears the record's.</param>
    public Task<Session?> KeepSaidAsync(
        string id, SaidWord word, string? note = null, CancellationToken ct = default, string? noteParts = null) => _db.RunAsync<Session?>(async () =>
    {
        await using (var command = _db.Command())
        {
            command.CommandText = $"""
                UPDATE sessions SET
                  said = json_insert(COALESCE(said, '[]'), '$[#]', json($word))
                  {(note is null ? "" : $", note = $note, note_parts = $noteParts, updated = $updated, revision = {NextRevision}")}
                WHERE id = $id AND origin IS NULL
                """;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$word", WordJson(word));
            if (note is not null)
            {
                command.Parameters.AddWithValue("$note", note);
                command.Parameters.AddWithValue("$noteParts", (object?)noteParts ?? DBNull.Value);
                command.Parameters.AddWithValue("$updated", Moments.Write(word.At));
            }

            if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 0) return null;
        }

        return await store.FindAsync(id, ct).ConfigureAwait(false);
    }, ct);

    /// <summary>
    /// Take these words off this machine's record (MSG1a, D137 §2.4): the session took them. An id it does not hold is
    /// passed over, and the last word taken leaves the record with none. No revision, since the words never travel. Null
    /// when there is no such record of this machine's.
    /// </summary>
    /// <remarks>A read, then the write it decides: the ledger calls it inside <see cref="SessionStore.ExclusiveAsync{T}"/> (REV3).</remarks>
    public Task<Session?> TakeSaidAsync(string id, IReadOnlyCollection<string> ids, CancellationToken ct = default) => _db.RunAsync<Session?>(async () =>
    {
        var session = await store.FindAsync(id, ct).ConfigureAwait(false);
        if (session is not { Origin: null }) return null;

        var left = session.Said.Where(word => !ids.Contains(word.Id, StringComparer.Ordinal)).ToList();
        await using (var command = _db.Command())
        {
            command.CommandText = "UPDATE sessions SET said = $said WHERE id = $id AND origin IS NULL";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$said", left.Count == 0 ? DBNull.Value : SaidJson(left));
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return session with { Said = left };
    }, ct);

    /// <summary>
    /// Move the words <paramref name="moves"/> picks off one record of this machine's onto another (FOUNDATION1 row d-ii part 3, the design §3.4
    /// rule 3), as the ledger's keyed open judges them. Each keeps its element as stored, its id and every field, a later build's among them,
    /// but its attempt, which was the record left's dispatcher's: on the record opened no dispatcher has written it yet, and a word kept there
    /// with another record's attempt would be taken back as uncertain at the next come-up and never go. They go in the order they waited,
    /// ahead of any word the record opened holds, and leave the record left in the same step, so the settlement's trigger writes a row there
    /// for each. The words moved, as kept on the record opened; none where either record is not this machine's or none moves.
    /// </summary>
    /// <remarks>A read, then the writes it decides: the ledger calls it inside its keyed open's exclusive step, so no say or take lands between.</remarks>
    public Task<IReadOnlyList<SaidWord>> MoveSaidAsync(string from, string to, Func<SaidWord, bool> moves, CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyList<SaidWord>>(async () =>
        {
            var left = await StoredWordsAsync(from, ct).ConfigureAwait(false);
            var opened = await StoredWordsAsync(to, ct).ConfigureAwait(false);
            if (left is null || opened is null) return [];

            var moved = new List<(string Element, SaidWord Word)>();
            var kept = new List<string>();
            foreach (var (element, word) in left)
            {
                if (word is not null && moves(word)) moved.Add((Unattempted(element), word with { Attempt = null }));
                else kept.Add(element);
            }

            if (moved.Count == 0) return [];

            await WriteWordsAsync(from, kept, ct).ConfigureAwait(false);
            await WriteWordsAsync(to, [.. moved.Select(each => each.Element), .. opened.Select(each => each.Element)], ct).ConfigureAwait(false);
            return [.. moved.Select(each => each.Word)];
        }, ct);

    /// <summary>
    /// A record's words as stored, each element's text beside the word it reads as, null for an element that is no word, which stays as it
    /// is. Null where no record of this machine's has the id.
    /// </summary>
    private async Task<IReadOnlyList<(string Element, SaidWord? Word)>?> StoredWordsAsync(string id, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT said FROM sessions WHERE id = $id AND origin IS NULL";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        return reader.IsDBNull(0) ? [] : StoredElements(reader.GetString(0));
    }

    /// <summary>A list's elements as stored, in order; none for a list that is not a JSON array, which nothing here rewrites.</summary>
    private static IReadOnlyList<(string Element, SaidWord? Word)> StoredElements(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? [.. document.RootElement.EnumerateArray().Select(element => (element.GetRawText(), WordOf(element)))]
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>The element without its <c>attempt</c>, every other field as it was; the element itself where it has none.</summary>
    private static string Unattempted(string element)
    {
        using var document = JsonDocument.Parse(element);
        if (!document.RootElement.TryGetProperty("attempt", out _)) return element;
        return JsonFields.Written(writer =>
        {
            writer.WriteStartObject();
            foreach (var field in document.RootElement.EnumerateObject())
            {
                if (field.Name != "attempt") field.WriteTo(writer);
            }

            writer.WriteEndObject();
        });
    }

    /// <summary>A record's list written whole from its elements as stored, none leaving it with no words. No revision: the words never travel.</summary>
    private async Task WriteWordsAsync(string id, IReadOnlyList<string> elements, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = "UPDATE sessions SET said = $said WHERE id = $id AND origin IS NULL";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$said", elements.Count == 0 ? DBNull.Value : JsonFields.Written(writer =>
        {
            writer.WriteStartArray();
            foreach (var element in elements) writer.WriteRawValue(element, skipInputValidation: true);
            writer.WriteEndArray();
        }));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The newest settlement of a producer's key over these records, within the horizon (row p, the design §19.2): the word that left
    /// one of them under <paramref name="key"/>, and when. Null where none did. A read, which the ledger and the desks make inside their
    /// own exclusive step, so no take lands between it and the keep it decides.
    /// </summary>
    /// <param name="since">The earliest moment still answered, in whole seconds since the epoch (<see cref="SaidKeys.Horizon"/> before now).</param>
    public Task<SettledWord?> SettledAsync(IReadOnlyList<string> records, string key, long since, CancellationToken ct = default) => _db.RunAsync<SettledWord?>(async () =>
    {
        if (records.Count == 0) return null;
        await using var command = _db.Command();
        var names = records.Select((_, at) => $"$r{at}").ToList();
        command.CommandText = $"""
            SELECT session, word, key, at FROM said_settled
            WHERE key = $key AND at >= $since AND session IN ({string.Join(", ", names)})
            ORDER BY at DESC, session LIMIT 1
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$since", since);
        for (var at = 0; at < records.Count; at++) command.Parameters.AddWithValue(names[at], records[at]);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false)
            ? new SettledWord(reader.GetString(0), reader.GetString(1), reader.GetString(2), DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)))
            : null;
    }, ct);

    /// <summary>
    /// The newest settlement of <paramref name="key"/> on any record, within the horizon: for a key that names its own source, a move's
    /// (LINESTOGETHER1b), which no other producer can write. One probe on the table's key.
    /// </summary>
    public Task<SettledWord?> SettledUnderAsync(string key, long since, CancellationToken ct = default) => _db.RunAsync<SettledWord?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT session, word, key, at FROM said_settled WHERE key = $key AND at >= $since ORDER BY at DESC, session LIMIT 1";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$since", since);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false)
            ? new SettledWord(reader.GetString(0), reader.GetString(1), reader.GetString(2), DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)))
            : null;
    }, ct);

    /// <summary>
    /// Mark the moment a dispatcher first wrote one waiting word to a harness (COWORK1c, the message-urgency design §6): before the
    /// wire, so a shell that comes up again tells a word that may have reached the agent from one that never left. Only the first
    /// mark holds. No revision, since the words never travel. False where this machine's record holds no such word.
    /// </summary>
    /// <remarks>
    /// <b>One statement</b>, for the reason a word is appended in one (REV3): the word is found in the list and marked in place in the
    /// same write, so a word kept or taken off meanwhile is never lost under a whole-list save.
    /// </remarks>
    public Task<bool> MarkAttemptAsync(string id, string word, DateTimeOffset at, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            UPDATE sessions SET said = json_insert(said, (
                SELECT '$[' || each.key || '].attempt' FROM json_each(sessions.said) AS each
                WHERE json_extract(each.value, '$.id') = $word LIMIT 1), $at)
            WHERE id = $id AND origin IS NULL AND said IS NOT NULL
              AND EXISTS (SELECT 1 FROM json_each(sessions.said) AS each WHERE json_extract(each.value, '$.id') = $word)
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$word", word);
        command.Parameters.AddWithValue("$at", Moments.Write(at));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);
}
