using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>Asks, held by the service beside the quests they become — machine-local, like the intake.</summary>
public sealed class AskStore
{
    /// <summary>The connection's gate: every command here runs inside it.</summary>
    private readonly ConnectionGate _db;

    private AskStore(SqliteConnection connection) => _db = ConnectionGate.For(connection);

    public static Task<AskStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default) =>
        ConnectionGate.For(connection).RunAsync(() => new AskStore(connection).EnsureSchemaAsync(ct), ct);

    private async Task<AskStore> EnsureSchemaAsync(CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS asks (
              id          TEXT PRIMARY KEY,
              workspace   TEXT NOT NULL,
              sentence    TEXT NOT NULL,
              state       TEXT NOT NULL,
              tier        TEXT NOT NULL,
              asked       TEXT NOT NULL,
              updated     TEXT NOT NULL,
              asker       TEXT NULL,
              note        TEXT NULL,
              links       TEXT NOT NULL DEFAULT '[]',
              attachments TEXT NOT NULL DEFAULT '[]',
              proposal    TEXT NOT NULL DEFAULT '[]',
              quests      TEXT NOT NULL DEFAULT '[]'
            );
            CREATE INDEX IF NOT EXISTS asks_workspace ON asks (workspace, state);
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // Which intake session served it. An ask made before the intake existed keeps every
        // word it had — it is the record of what a person asked, and nothing re-derives it.
        await SchemaColumns.EnsureAsync(_db, "asks", "intake", "intake TEXT NULL", ct).ConfigureAwait(false);

        // The person's words after the ask, appended where they are kept. A store from before keeps
        // every row it had, and their earlier answers and messages are not kept, so the
        // moment keeping began is written on each of them, once, rather than read as nothing more said.
        await SchemaColumns.EnsureAsync(_db, "asks", "words", "words TEXT NOT NULL DEFAULT '[]'", ct)
            .ConfigureAwait(false);
        if (!await SchemaColumns.HasAsync(_db, "asks", "words_kept_from", ct).ConfigureAwait(false))
        {
            await SchemaColumns.EnsureAsync(_db, "asks", "words_kept_from", "words_kept_from TEXT NULL", ct)
                .ConfigureAwait(false);
            await using var mark = _db.Command();
            mark.CommandText = "UPDATE asks SET words_kept_from = $now WHERE words_kept_from IS NULL";
            mark.Parameters.AddWithValue("$now", Moments.Write(DateTimeOffset.UtcNow));
            await mark.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // The go-aheads its sessions asked for, beside the person's words. A store from before keeps
        // every ask it had, each holding none: nothing has been asked on one in a way a later session could be handed.
        await SchemaColumns.EnsureAsync(_db, "asks", "go_aheads", "go_aheads TEXT NOT NULL DEFAULT '[]'", ct)
            .ConfigureAwait(false);

        // The person's review choices and the intake's proposals, each appended where it is kept. A
        // store from before keeps every ask it had, each holding none: nothing is chosen or proposed on it.
        await SchemaColumns.EnsureAsync(_db, "asks", "review_choices", "review_choices TEXT NOT NULL DEFAULT '[]'", ct)
            .ConfigureAwait(false);
        await SchemaColumns.EnsureAsync(_db, "asks", "review_proposals", "review_proposals TEXT NOT NULL DEFAULT '[]'", ct)
            .ConfigureAwait(false);

        // The person's kind and workflow choices, appended where they are kept. A store from before keeps
        // every ask it had, each holding none: nothing is chosen on it, so each repository's choice decides.
        await SchemaColumns.EnsureAsync(_db, "asks", "workflow_choices", "workflow_choices TEXT NOT NULL DEFAULT '[]'", ct)
            .ConfigureAwait(false);

        // The intake's kind proposals, appended as the review's are, beside the person's choices.
        await SchemaColumns.EnsureAsync(_db, "asks", "workflow_proposals", "workflow_proposals TEXT NOT NULL DEFAULT '[]'", ct)
            .ConfigureAwait(false);

        // The asker's short title, as a quest's. A store from before keeps every ask it had,
        // each holding none, so a page heads it with its sentence's first line.
        await SchemaColumns.EnsureAsync(_db, "asks", "short_title", "short_title TEXT NULL", ct).ConfigureAwait(false);

        // The paths its asker declared; an ask from before declared none.
        await SchemaColumns.EnsureAsync(_db, "asks", "paths", "paths TEXT NULL", ct).ConfigureAwait(false);

        // The person's through words; an ask from before holds none, so each run of it waits for their Accept.
        await SchemaColumns.EnsureAsync(_db, "asks", "through_words", "through_words TEXT NOT NULL DEFAULT '[]'", ct).ConfigureAwait(false);

        // The asks' revision, the change stream's `asks` family, one row moved by every insert, update and
        // delete of an ask. Kept by triggers, as `session_revision` is, because the triggers live in the file: a connector built
        // before them writes the row and still moves it, and no writer here has to remember to. A count, not a moment, so a
        // delete moves it too. Opened again, it keeps its count.
        await using (var revision = _db.Command())
        {
            revision.CommandText = """
                CREATE TABLE IF NOT EXISTS ask_revision (
                  one      INTEGER PRIMARY KEY CHECK (one = 1),
                  revision INTEGER NOT NULL
                );
                INSERT OR IGNORE INTO ask_revision (one, revision) VALUES (1, 0);

                CREATE TRIGGER IF NOT EXISTS ask_revision_asked AFTER INSERT ON asks
                BEGIN
                  UPDATE ask_revision SET revision = revision + 1 WHERE one = 1;
                END;

                CREATE TRIGGER IF NOT EXISTS ask_revision_changed AFTER UPDATE ON asks
                BEGIN
                  UPDATE ask_revision SET revision = revision + 1 WHERE one = 1;
                END;

                CREATE TRIGGER IF NOT EXISTS ask_revision_deleted AFTER DELETE ON asks
                BEGIN
                  UPDATE ask_revision SET revision = revision + 1 WHERE one = 1;
                END;
                """;
            await revision.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // An ask kept, and each move of its state, as a row of the event journal,
        // minted by triggers in the statement that writes it, so a connector built before them mints it too.
        await using (var journal = _db.Command())
        {
            journal.CommandText = EventJournal.AsksSchema;
            await journal.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>
    /// Keep a review choice the person set on the ask, appended in one statement, for the reason a word is.
    /// The latest stands. False when no ask has that id.
    /// </summary>
    public Task<bool> RecordReviewChoiceAsync(string id, AskReviewChoice choice, DateTimeOffset now, CancellationToken ct = default) =>
        AppendAsync(id, "review_choices", JsonFields.Written(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("choice", choice.Choice);
            writer.WriteString("at", Moments.Write(choice.At));
            if (choice.Words is { } words) writer.WriteString("words", words);
            writer.WriteEndObject();
        }), now, ct);

    /// <summary>
    /// Keep a kind and workflow the person set on the ask, appended in one statement, for the reason a word is.
    /// The latest stands. False when no ask has that id.
    /// </summary>
    public Task<bool> RecordWorkflowChoiceAsync(string id, AskWorkflowChoice choice, DateTimeOffset now, CancellationToken ct = default) =>
        AppendAsync(id, "workflow_choices", JsonFields.Written(writer => WriteWorkflowChoice(writer, choice)), now, ct);

    /// <summary>Keep the person's through words, or their take-back, appended as a choice is. False when no ask has that id.</summary>
    public Task<bool> RecordThroughAsync(string id, AskThrough through, DateTimeOffset now, CancellationToken ct = default) =>
        AppendAsync(id, "through_words", JsonFields.Written(writer => AskThroughs.Write(writer, through)), now, ct);

    /// <summary>One kind and workflow choice as the store keeps it: each field only where it names one, then when and the words.</summary>
    private static void WriteWorkflowChoice(Utf8JsonWriter writer, AskWorkflowChoice choice)
    {
        writer.WriteStartObject();
        if (choice.Kind is { } kind) writer.WriteString("kind", kind);
        if (choice.Workflow is { } workflow) writer.WriteString("workflow", workflow);
        writer.WriteString("at", Moments.Write(choice.At));
        if (choice.Words is { } words) writer.WriteString("words", words);
        writer.WriteEndObject();
    }

    /// <summary>Keep an intake's review proposal on the ask, appended as a choice is. False when no ask has that id.</summary>
    public Task<bool> RecordReviewProposalAsync(string id, AskReviewProposal proposal, DateTimeOffset now, CancellationToken ct = default) =>
        AppendAsync(id, "review_proposals", JsonFields.Written(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("choice", proposal.Choice);
            writer.WriteString("reason", proposal.Reason);
            writer.WriteString("at", Moments.Write(proposal.At));
            if (proposal.Session is { } session) writer.WriteString("session", session);
            if (proposal.Quest is { } quest) writer.WriteString("quest", quest);
            writer.WriteEndObject();
        }), now, ct);

    /// <summary>Keep an intake's kind proposal on the ask, appended as a review proposal is. False when no ask has that id.</summary>
    public Task<bool> RecordWorkflowProposalAsync(string id, AskWorkflowProposal proposal, DateTimeOffset now, CancellationToken ct = default) =>
        AppendAsync(id, "workflow_proposals", JsonFields.Written(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("kind", proposal.Kind);
            writer.WriteString("reason", proposal.Reason);
            writer.WriteString("at", Moments.Write(proposal.At));
            if (proposal.Session is { } session) writer.WriteString("session", session);
            if (proposal.Quest is { } quest) writer.WriteString("quest", quest);
            writer.WriteEndObject();
        }), now, ct);

    /// <summary>One item appended to one of the ask's lists, in one statement. The column is this class's own name.</summary>
    private Task<bool> AppendAsync(string id, string column, string item, DateTimeOffset now, CancellationToken ct) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"UPDATE asks SET {column} = json_insert({column}, '$[#]', json($item)), updated = $updated WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        command.Parameters.AddWithValue("$item", item);
        command.Parameters.AddWithValue("$updated", Moments.Write(now));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>The ask's handle: the same words in the same circle are the same ask.</summary>
    /// <param name="again">
    /// How many times these words were asked and CLOSED before. Zero is the id every ask
    /// has always had, so no existing ask moves.
    /// </param>
    internal static string MakeId(string workspace, string sentence, int again = 0) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(
                    $"{Workspaces.Normalize(workspace)}:{sentence.Trim()}" + (again == 0 ? "" : $"#again{again}"))))[..6]
            .ToLowerInvariant();

    /// <summary>Keep a new ask by insert only: false where the id holds one, whose row stays as it is, since a later whole-row write from a stale read would undo what a door did to it.</summary>
    public Task<bool> InsertAsync(Ask ask, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        // The review, kind and workflow choices are written with a new ask and only appended after.
        command.CommandText = """
            INSERT INTO asks (id, workspace, sentence, state, tier, asked, updated, asker, note, links, attachments, proposal, quests, intake, review_choices, workflow_choices, short_title, paths, through_words)
            VALUES ($id, $workspace, $sentence, $state, $tier, $asked, $updated, $asker, $note, $links, $attachments, $proposal, $quests, $intake, $reviewChoices, $workflowChoices, $short, $paths, $through)
            ON CONFLICT (id) DO NOTHING
            """;
        command.Parameters.AddWithValue("$through", AskThroughs.Written(ask.ThroughWords));
        command.Parameters.AddWithValue("$reviewChoices", Json(ask.ReviewChoices, (w, choice) =>
        {
            w.WriteStartObject();
            w.WriteString("choice", choice.Choice);
            w.WriteString("at", Moments.Write(choice.At));
            if (choice.Words is { } words) w.WriteString("words", words);
            w.WriteEndObject();
        }));
        command.Parameters.AddWithValue("$workflowChoices", Json(ask.WorkflowChoices, WriteWorkflowChoice));
        command.Parameters.AddWithValue("$intake", (object?)ask.Intake ?? DBNull.Value);
        command.Parameters.AddWithValue("$short", (object?)ask.Short ?? DBNull.Value);
        command.Parameters.AddWithValue("$paths", ask.Paths.Count == 0 ? DBNull.Value : Json(ask.Paths, (w, path) => w.WriteStringValue(path)));
        command.Parameters.AddWithValue("$id", ask.Id);
        command.Parameters.AddWithValue("$workspace", ask.Workspace);
        command.Parameters.AddWithValue("$sentence", ask.Sentence);
        command.Parameters.AddWithValue("$state", ask.State.ToString());
        command.Parameters.AddWithValue("$tier", ask.Tier);
        command.Parameters.AddWithValue("$asked", Moments.Write(ask.Asked));
        command.Parameters.AddWithValue("$updated", Moments.Write(ask.Updated));
        command.Parameters.AddWithValue("$asker", (object?)ask.Asker ?? DBNull.Value);
        command.Parameters.AddWithValue("$note", (object?)ask.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$links", Json(ask.Links, (w, link) => w.WriteStringValue(link)));
        command.Parameters.AddWithValue("$attachments", Json(ask.Attachments, (w, a) => a.Write(w)));
        command.Parameters.AddWithValue("$proposal", Json(ask.Proposal, (w, m) =>
        {
            w.WriteStartObject();
            w.WriteString("repository", m.Repository);
            w.WriteNumber("score", m.Score);
            w.WriteStartArray("matched");
            foreach (var word in m.Matched) w.WriteStringValue(word);
            w.WriteEndArray();
            w.WriteEndObject();
        }));
        command.Parameters.AddWithValue("$quests", Json(ask.Quests, (w, id) => w.WriteStringValue(id)));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>
    /// Record that the ask became <paramref name="quest"/>: appended where the list is kept, in one
    /// statement, so two publishers never overwrite each other.
    /// </summary>
    /// <remarks>
    /// An intake's connector and a person's page publish from two processes. Saving the
    /// whole record read would drop the first one's quest. A closed ask stays
    /// closed: a quest published before the close landed is still one the ask became.
    /// </remarks>
    /// <param name="tier">The tier the ask is now answered at, or null to keep it.</param>
    public Task RecordPublishedAsync(
        string id, string quest, string? tier, DateTimeOffset now, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            UPDATE asks SET
              quests = CASE WHEN EXISTS (SELECT 1 FROM json_each(asks.quests) WHERE json_each.value = $quest)
                            THEN quests ELSE json_insert(quests, '$[#]', $quest) END,
              state = CASE WHEN state = 'Closed' THEN state ELSE 'Published' END,
              tier = COALESCE($tier, tier),
              updated = $updated
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        command.Parameters.AddWithValue("$quest", quest);
        command.Parameters.AddWithValue("$tier", (object?)tier ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", Moments.Write(now));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>
    /// Close the ask with its reason — only the columns a close owns, so a quest published meanwhile
    /// stays on it.
    /// </summary>
    public Task RecordClosedAsync(string id, string note, DateTimeOffset now, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "UPDATE asks SET state = 'Closed', note = $note, updated = $updated WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        command.Parameters.AddWithValue("$note", note);
        command.Parameters.AddWithValue("$updated", Moments.Write(now));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>Remove the ask's row — the desk deleted it, and every quest asked by it first.</summary>
    public Task DeleteAsync(string id, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "DELETE FROM asks WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>Record the intake session serving the ask — that column alone, for the same reason.</summary>
    public Task RecordIntakeAsync(string id, string session, DateTimeOffset now, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "UPDATE asks SET intake = $intake, updated = $updated WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        command.Parameters.AddWithValue("$intake", session);
        command.Parameters.AddWithValue("$updated", Moments.Write(now));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>
    /// Clear the intake serving the ask, only where it is still <paramref name="session"/>: that
    /// column alone, compared and cleared in one statement, so a binding a later intake made is never cleared by an earlier one's end.
    /// </summary>
    public Task UnbindIntakeAsync(string id, string session, DateTimeOffset now, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "UPDATE asks SET intake = NULL, updated = $updated WHERE id = $id AND intake = $intake";
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        command.Parameters.AddWithValue("$intake", session);
        command.Parameters.AddWithValue("$updated", Moments.Write(now));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>
    /// Keep a word the person gave on the ask: appended where the list is kept, in one statement,
    /// for the reason a published quest is — the say door and a driver's report arrive from two
    /// processes, and a whole-record save would drop the other's word.
    /// </summary>
    /// <returns>False when no ask has that id, and nothing is kept.</returns>
    public Task<bool> RecordWordAsync(string id, AskWord word, DateTimeOffset now, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            UPDATE asks SET words = json_insert(words, '$[#]', json($word)), updated = $updated WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        command.Parameters.AddWithValue("$word", JsonFields.Written(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("kind", AskWord.Spell(word.Kind));
            writer.WriteString("text", word.Text);
            writer.WriteString("at", Moments.Write(word.At));
            if (word.Session is { } session) writer.WriteString("session", session);
            if (word.Quest is { } quest) writer.WriteString("quest", quest);
            writer.WriteEndObject();
        }));
        command.Parameters.AddWithValue("$updated", Moments.Write(now));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>
    /// Read the ask's go-aheads, decide, and write what is decided, as one step across hosts: two sessions
    /// asking for one act at once, or a request beside the person's answer, would otherwise each write what they read and
    /// drop the other's. The column alone is written, for the reason a word is.
    /// </summary>
    /// <param name="decide">
    /// Given every entry as stored, each with what this build reads of it: the entries to write back (an entry it could not
    /// read kept unchanged), or null to write nothing; and what to answer.
    /// </param>
    /// <returns>Whether an ask has that id, and what <paramref name="decide"/> answered; nothing is decided for none.</returns>
    public Task<(bool Found, T? Result)> DecideGoAheadsAsync<T>(
        string id,
        Func<IReadOnlyList<(JsonElement Raw, GoAhead? Read)>, (IReadOnlyList<(JsonElement Raw, GoAhead? Read)>? Next, T Result)> decide,
        DateTimeOffset now, CancellationToken ct = default) =>
        _db.InTransactionAsync<(bool Found, T? Result)>(async (_, inside) =>
        {
            string? stored;
            await using (var read = _db.Command())
            {
                read.CommandText = "SELECT go_aheads FROM asks WHERE id = $id";
                read.Parameters.AddWithValue("$id", id.TrimStart('#'));
                stored = await read.ExecuteScalarAsync(inside).ConfigureAwait(false) as string;
            }

            if (stored is null) return (false, default);

            var (next, result) = decide(GoAheads.Entries(stored));
            if (next is not null)
            {
                await using var write = _db.Command();
                write.CommandText = "UPDATE asks SET go_aheads = $goAheads, updated = $updated WHERE id = $id";
                write.Parameters.AddWithValue("$id", id.TrimStart('#'));
                write.Parameters.AddWithValue("$goAheads", GoAheads.Written(next));
                write.Parameters.AddWithValue("$updated", Moments.Write(now));
                await write.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            return (true, result);
        }, ct);

    public Task<Ask?> FindAsync(string id, CancellationToken ct = default) => _db.RunAsync<Ask?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT * FROM asks WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>A circle's asks — or every circle's — newest first; closed ones only when asked for.</summary>
    public Task<IReadOnlyList<Ask>> ListAsync(
        string? workspace = null, bool includeClosed = false, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<Ask>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"""
            SELECT * FROM asks
            WHERE ($workspace IS NULL OR workspace = $workspace COLLATE NOCASE)
              {(includeClosed ? "" : "AND state <> 'Closed'")}
            ORDER BY asked DESC
            """;
        command.Parameters.AddWithValue(
            "$workspace", workspace is null ? DBNull.Value : Workspaces.Normalize(workspace));
        var asks = new List<Ask>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) asks.Add(Read(reader));
        return asks;
    }, ct);

    private static Ask Read(SqliteDataReader reader)
    {
        string Text(string column) => reader.GetString(reader.GetOrdinal(column));
        string? Maybe(string column) =>
            reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetString(reader.GetOrdinal(column));

        return new Ask(
            Text("id"), Text("workspace"), Text("sentence"), Enum.Parse<AskState>(Text("state")), Text("tier"),
            Moments.ReadRequired(Text("asked")), Moments.ReadRequired(Text("updated")), Maybe("asker"), Maybe("note"))
        {
            Links = Items(Text("links"), e => e.GetString() ?? ""),
            Attachments = Items(Text("attachments"), QuestAttachment.Stored),
            Proposal = Items(Text("proposal"), e => new DeclarationMatch(
                e.GetProperty("repository").GetString() ?? "", e.GetProperty("score").GetInt32(),
                e.GetProperty("matched").EnumerateArray().Select(w => w.GetString() ?? "").ToList())),
            Quests = Items(Text("quests"), e => e.GetString() ?? ""),
            Intake = Maybe("intake"),
            Short = Maybe("short_title"),
            Paths = Maybe("paths") is { } paths ? Items(paths, e => e.GetString() ?? "") : [],
            Later = LaterWords(Text("words")),
            WordsKeptFrom = Maybe("words_kept_from") is { } from ? Moments.ReadRequired(from) : null,
            GoAheads = GoAheads.Read(Text("go_aheads")),
            ReviewChoices = ReviewChoicesOf(Text("review_choices")),
            ReviewProposals = ReviewProposalsOf(Text("review_proposals")),
            WorkflowChoices = WorkflowChoicesOf(Text("workflow_choices")),
            WorkflowProposals = WorkflowProposalsOf(Text("workflow_proposals")),
            ThroughWords = AskThroughs.Read(Text("through_words")),
        };
    }

    /// <summary>The kind proposals kept, oldest first; one this build cannot read is passed over, as a choice is.</summary>
    private static IReadOnlyList<AskWorkflowProposal> WorkflowProposalsOf(string json)
    {
        using var document = JsonDocument.Parse(json);
        var proposals = new List<AskWorkflowProposal>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (JsonFields.Text(element, "kind") is not { } kind || !WorkflowChoices.IsKind(kind)) continue;
            if (JsonFields.Text(element, "reason") is not { } reason || Moments.Read(element, "at") is not { } at) continue;
            proposals.Add(new AskWorkflowProposal(kind, reason, at, JsonFields.Text(element, "session"), JsonFields.Text(element, "quest")));
        }

        return proposals;
    }

    /// <summary>
    /// The kind and workflow choices kept, oldest first. One this build cannot read (a field of the wrong shape, or
    /// missing its moment) is passed over, never a failed read of the ask.
    /// </summary>
    private static IReadOnlyList<AskWorkflowChoice> WorkflowChoicesOf(string json)
    {
        using var document = JsonDocument.Parse(json);
        var choices = new List<AskWorkflowChoice>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object || Moments.Read(element, "at") is not { } at) continue;
            var kind = JsonFields.Text(element, "kind");
            var workflow = JsonFields.Text(element, "workflow");
            if (WorkflowChoices.Judge(kind, workflow) is not null) continue;
            choices.Add(new AskWorkflowChoice(kind, workflow, at, JsonFields.Text(element, "words")));
        }

        return choices;
    }

    /// <summary>
    /// The review choices kept, oldest first. One this build cannot read — not a choice, or missing its moment — is passed
    /// over, never a failed read of the ask.
    /// </summary>
    private static IReadOnlyList<AskReviewChoice> ReviewChoicesOf(string json)
    {
        using var document = JsonDocument.Parse(json);
        var choices = new List<AskReviewChoice>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (JsonFields.Text(element, "choice") is not { } choice || Reviews.JudgeChoice(choice) is not null) continue;
            if (Moments.Read(element, "at") is not { } at) continue;
            choices.Add(new AskReviewChoice(choice, at, JsonFields.Text(element, "words")));
        }

        return choices;
    }

    /// <summary>The review proposals kept, oldest first; one this build cannot read is passed over, as a choice is.</summary>
    private static IReadOnlyList<AskReviewProposal> ReviewProposalsOf(string json)
    {
        using var document = JsonDocument.Parse(json);
        var proposals = new List<AskReviewProposal>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (JsonFields.Text(element, "choice") is not { } choice || Reviews.JudgeChoice(choice) is not null) continue;
            if (JsonFields.Text(element, "reason") is not { } reason || Moments.Read(element, "at") is not { } at) continue;
            proposals.Add(new AskReviewProposal(choice, reason, at, JsonFields.Text(element, "session"), JsonFields.Text(element, "quest")));
        }

        return proposals;
    }

    /// <summary>
    /// The words kept after the ask, oldest first. A word whose kind this build does not know — a newer
    /// build's — or that lacks its words or its moment is passed over, never a failed read of the ask.
    /// </summary>
    private static IReadOnlyList<AskWord> LaterWords(string json)
    {
        using var document = JsonDocument.Parse(json);
        var words = new List<AskWord>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            // Only a session's words are kept here: the ask's own sentence is its record's.
            if (AskWord.Parse(JsonFields.Text(element, "kind")) is not { } kind || kind == AskWordKind.Asked) continue;
            if (JsonFields.Text(element, "text") is not { } text) continue;
            if (Moments.Read(element, "at") is not { } at) continue;

            words.Add(new AskWord(kind, text, at, JsonFields.Text(element, "session"), JsonFields.Text(element, "quest")));
        }

        return words;
    }

    // Hand-rolled for the same reason every store's lists are: nothing here may stop working under AOT.
    private static string Json<T>(IEnumerable<T> items, Action<Utf8JsonWriter, T> write) => JsonFields.Written(writer =>
    {
        writer.WriteStartArray();
        foreach (var item in items) write(writer, item);
        writer.WriteEndArray();
    });

    private static IReadOnlyList<T> Items<T>(string json, Func<JsonElement, T> read)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray().Select(read).ToList();
    }
}
