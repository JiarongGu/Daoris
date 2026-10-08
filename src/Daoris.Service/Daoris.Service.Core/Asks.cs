using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>What became of an ask.</summary>
public enum AskState
{
    /// <summary>Asked, and nothing has answered yet.</summary>
    Open,

    /// <summary>A tier proposed where it belongs — possibly nowhere — and nothing is published.</summary>
    Proposed,

    /// <summary>At least one quest carries it.</summary>
    Published,

    /// <summary>The person closed it, with a reason.</summary>
    Closed,

    /// <summary>
    /// Its work is finished (USE1c): it became at least one quest, and none of the quests asked by it is
    /// open or taken — chain steps included, since a step is asked by the ask too. Never stored: the
    /// desk derives it from the quests on every read, so a quest closed on another machine and synced
    /// in is reflected with no write to the ask. A closed ask stays closed, because that is the
    /// person's word on it.
    /// </summary>
    Done,
}

/// <summary>
/// A sentence entered at a WORKSPACE rather than at a repository (D65 §1a) — held as a record of what
/// was asked, with what, by whom, when, and what became of it.
/// </summary>
/// <param name="Id">Short, stable handle — the same sentence in the same circle is the same ask.</param>
/// <param name="Workspace">The circle it was asked in, and therefore whom it can reach (D48 §4).</param>
/// <param name="Sentence">The person's words, verbatim.</param>
/// <param name="State">What became of it.</param>
/// <param name="Tier">Which tier answered it — said on every record (<c>model-decoupling</c>).</param>
/// <param name="Asked">When it was asked.</param>
/// <param name="Updated">When it last changed.</param>
/// <param name="Asker">Who asked, when the door knows — a key's name at a keyed door; null is this machine's person.</param>
/// <param name="Note">Why it was closed, or why its named receiver was refused.</param>
public sealed record Ask(
    string Id, string Workspace, string Sentence, AskState State, string Tier,
    DateTimeOffset Asked, DateTimeOffset Updated, string? Asker = null, string? Note = null)
{
    /// <summary>Addresses it carries, judged as a quest's are — they travel to every quest it becomes.</summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>Files it carries, by name — the bytes are kept under this machine's home until it becomes quests.</summary>
    public IReadOnlyList<QuestAttachment> Attachments { get; init; } = [];

    /// <summary>What the declarations tier proposed, best first, with the words that matched.</summary>
    public IReadOnlyList<DeclarationMatch> Proposal { get; init; } = [];

    /// <summary>The quests it became, in the order they were published.</summary>
    public IReadOnlyList<string> Quests { get; init; } = [];

    /// <summary>
    /// The intake session that served it (D65 §1b), once one was opened — the record a reader follows
    /// to what the intake read, decided and asked. Null for an ask no intake has served.
    /// </summary>
    public string? Intake { get; init; }

    /// <summary>
    /// Whether it may be deleted (D95): nothing stands on any quest asked by it. Answered by the desk's
    /// reads, so a page offers the verb only where the service would take it; false on a record read
    /// straight from the store, which judges nothing.
    /// </summary>
    public bool Deletable { get; init; }

    /// <summary>
    /// What the person said on it after asking it (DRIFT1a), oldest first: each answer to a session that
    /// parked to ask them and each message added to one while it ran. Appended where it is kept and never
    /// rewritten, so nothing the agents wrote stands in for it (D133 §1).
    /// </summary>
    public IReadOnlyList<AskWord> Later { get; init; } = [];

    /// <summary>
    /// Every word the person gave on it (DRIFT1a, D133 §1): its own sentence first, as it was asked and to
    /// no session, then <see cref="Later"/>. Derived from the sentence rather than stored beside it, so an
    /// ask from before the words were kept still opens with what it asked.
    /// </summary>
    public IReadOnlyList<AskWord> Words => [new(AskWordKind.Asked, Sentence, Asked), .. Later];

    /// <summary>
    /// From when its later words are kept, for an ask made before they were (DRIFT1a): the moment this
    /// machine's store first opened on a build that keeps them. Its answers and messages before then were
    /// never kept and are not back-filled, so <see cref="Words"/> is not all it was told. Null for every ask
    /// made since, whose words are kept from its first.
    /// </summary>
    /// <remarks>
    /// Kept from then by the doors that report a word: the answer door at once, and a message added to a
    /// running session once its driver reports it (`POST /api/sessions/{id}/added`).
    /// </remarks>
    public DateTimeOffset? WordsKeptFrom { get; init; }

    /// <summary>
    /// The go-aheads its sessions asked the person for (KNOWUSE1a, D135 §2), one per act, oldest first, each with every
    /// session's request for it and the person's answer: kept beside their words, so a later session is handed what was
    /// approved and refused rather than asking it again.
    /// </summary>
    public IReadOnlyList<GoAhead> GoAheads { get; init; } = [];

    /// <summary>
    /// The person's review choices for every chain it publishes that sets none (REVIEWENV1b, D154 point 3; design §1.4 row 4,
    /// §1.5), oldest first, each with when and any words they gave. The latest stands, as a go-ahead's answer does.
    /// </summary>
    public IReadOnlyList<AskReviewChoice> ReviewChoices { get; init; } = [];

    /// <summary>
    /// Its intake's review proposals (REVIEWENV1b, design §1.5–§1.6), oldest first, each with its reason: a reading, kept
    /// beside the person's choice and applied only by their press.
    /// </summary>
    public IReadOnlyList<AskReviewProposal> ReviewProposals { get; init; } = [];
}

/// <summary>A review choice the person set on an ask (REVIEWENV1b, design §1.5): <c>off</c>, <c>on</c> or an environment's name.</summary>
/// <param name="Choice">What they chose.</param>
/// <param name="At">When they chose it.</param>
/// <param name="Words">Their words with it, where they gave any.</param>
public sealed record AskReviewChoice(string Choice, DateTimeOffset At, string? Words = null);

/// <summary>
/// A review choice an intake proposed for a chain it published (REVIEWENV1b, design §1.5–§1.6), with its reason: a reading
/// the person may apply, never a choice.
/// </summary>
/// <param name="Choice">What it proposes: <c>off</c>, <c>on</c> or an environment's name.</param>
/// <param name="Reason">Why, in at most <see cref="Reviews.ReasonLimit"/> characters.</param>
/// <param name="At">When it proposed it.</param>
/// <param name="Session">The intake session that proposed it: the record that names the tier.</param>
/// <param name="Quest">The quest it published the proposal with, the chain's first; null for none.</param>
public sealed record AskReviewProposal(string Choice, string Reason, DateTimeOffset At, string? Session = null, string? Quest = null);

/// <summary>How the person gave a word on an ask (DRIFT1a, D133 §1).</summary>
public enum AskWordKind
{
    /// <summary>The ask's own sentence — only ever its first word, derived from the record.</summary>
    Asked,

    /// <summary>An answer to a session that parked to ask them.</summary>
    Answered,

    /// <summary>A message added to a session while it ran.</summary>
    Added,

    /// <summary>
    /// Words said to a session after it ended, which went on with them (MSG1a, D137 §2.4): kept once a session took
    /// them — the reopened record itself, or the session a fallback handed them to.
    /// </summary>
    Reopened,

    /// <summary>Their words with a <c>reviewed</c> on a set-up step (REVIEWENV1b, design §3.5).</summary>
    Reviewed,

    /// <summary>Their words with a <c>not-yet</c> on a set-up step (REVIEWENV1b, design §3.4): what is not right yet.</summary>
    NotYet,

    /// <summary>Their words with a skip of a review (REVIEWENV1b, design §3.6).</summary>
    Skipped,
}

/// <summary>One thing the person said on an ask, verbatim (DRIFT1a, D133 §1).</summary>
/// <param name="Kind">How they gave it.</param>
/// <param name="Text">Their words, as given, trimmed at the ends only.</param>
/// <param name="At">When they gave it.</param>
/// <param name="Session">The session they gave it to; null for the ask's own sentence.</param>
/// <param name="Quest">The quest that session worked; null for the ask's sentence and for an intake, which works none yet.</param>
public sealed record AskWord(AskWordKind Kind, string Text, DateTimeOffset At, string? Session = null, string? Quest = null)
{
    /// <summary>The kind's spelling, in the store and on the wire: a not-yet as the verdict says it (REVIEWENV1b).</summary>
    public static string Spell(AskWordKind kind) => kind == AskWordKind.NotYet ? Reviews.NotYet : kind.ToString().ToLowerInvariant();

    /// <summary>The reverse of <see cref="Spell"/>: null for a kind this build does not know.</summary>
    internal static AskWordKind? Parse(string? spelled) =>
        Enum.GetValues<AskWordKind>().Where(kind => Spell(kind) == spelled).Select(kind => (AskWordKind?)kind).FirstOrDefault();
}

/// <summary>Asks, held by the service beside the quests they become — machine-local, like the intake.</summary>
public sealed class AskStore
{
    /// <summary>The connection's gate: every command here runs inside it (SQLITETX1).</summary>
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

        // INT4b: which intake session served it. An ask made before the intake existed keeps every
        // word it had — it is the record of what a person asked, and nothing re-derives it.
        await SchemaColumns.EnsureAsync(_db, "asks", "intake", "intake TEXT NULL", ct).ConfigureAwait(false);

        // DRIFT1a (D133 §1): the person's words after the ask, appended where they are kept. A store from
        // before keeps every row it had, and their earlier answers and messages were never kept, so the
        // moment keeping began is written on each of them, once, rather than read as nothing more said.
        await SchemaColumns.EnsureAsync(_db, "asks", "words", "words TEXT NOT NULL DEFAULT '[]'", ct)
            .ConfigureAwait(false);
        if (!await SchemaColumns.HasAsync(_db, "asks", "words_kept_from", ct).ConfigureAwait(false))
        {
            await SchemaColumns.EnsureAsync(_db, "asks", "words_kept_from", "words_kept_from TEXT NULL", ct)
                .ConfigureAwait(false);
            await using var mark = _db.Command();
            mark.CommandText = "UPDATE asks SET words_kept_from = $now WHERE words_kept_from IS NULL";
            mark.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            await mark.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // KNOWUSE1a (D135 §2): the go-aheads its sessions asked for, beside the person's words. A store from before keeps
        // every ask it had, each holding none: nothing was ever asked on one in a way a later session could be handed.
        await SchemaColumns.EnsureAsync(_db, "asks", "go_aheads", "go_aheads TEXT NOT NULL DEFAULT '[]'", ct)
            .ConfigureAwait(false);

        // REVIEWENV1b (D154 point 3): the person's review choices and the intake's proposals, each appended where it is kept. A
        // store from before keeps every ask it had, each holding none: nothing was chosen or proposed on it.
        await SchemaColumns.EnsureAsync(_db, "asks", "review_choices", "review_choices TEXT NOT NULL DEFAULT '[]'", ct)
            .ConfigureAwait(false);
        await SchemaColumns.EnsureAsync(_db, "asks", "review_proposals", "review_proposals TEXT NOT NULL DEFAULT '[]'", ct)
            .ConfigureAwait(false);

        return this;
    }

    /// <summary>
    /// Keep a review choice the person set on the ask (REVIEWENV1b), appended in one statement, for the reason a word is
    /// (REV3). The latest stands. False when no ask has that id.
    /// </summary>
    public Task<bool> RecordReviewChoiceAsync(string id, AskReviewChoice choice, DateTimeOffset now, CancellationToken ct = default) =>
        AppendAsync(id, "review_choices", JsonFields.Written(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("choice", choice.Choice);
            writer.WriteString("at", choice.At.ToString("O"));
            if (choice.Words is { } words) writer.WriteString("words", words);
            writer.WriteEndObject();
        }), now, ct);

    /// <summary>Keep an intake's review proposal on the ask (REVIEWENV1b), appended as a choice is. False when no ask has that id.</summary>
    public Task<bool> RecordReviewProposalAsync(string id, AskReviewProposal proposal, DateTimeOffset now, CancellationToken ct = default) =>
        AppendAsync(id, "review_proposals", JsonFields.Written(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("choice", proposal.Choice);
            writer.WriteString("reason", proposal.Reason);
            writer.WriteString("at", proposal.At.ToString("O"));
            if (proposal.Session is { } session) writer.WriteString("session", session);
            if (proposal.Quest is { } quest) writer.WriteString("quest", quest);
            writer.WriteEndObject();
        }), now, ct);

    /// <summary>One item appended to one of the ask's lists, in one statement (REV3). The column is this class's own name.</summary>
    private Task<bool> AppendAsync(string id, string column, string item, DateTimeOffset now, CancellationToken ct) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"UPDATE asks SET {column} = json_insert({column}, '$[#]', json($item)), updated = $updated WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        command.Parameters.AddWithValue("$item", item);
        command.Parameters.AddWithValue("$updated", now.ToString("O"));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>The ask's handle: the same words in the same circle are the same ask.</summary>
    /// <param name="again">
    /// How many times these words were asked and CLOSED before (ASKAGAIN1). Zero is the id every ask
    /// has always had, so no existing ask moves.
    /// </param>
    internal static string MakeId(string workspace, string sentence, int again = 0) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(
                    $"{Workspaces.Normalize(workspace)}:{sentence.Trim()}" + (again == 0 ? "" : $"#again{again}"))))[..6]
            .ToLowerInvariant();

    /// <summary>Write the ask whole — insert, or replace the row this id already names.</summary>
    public Task SaveAsync(Ask ask, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        // The review choices are written with a new ask, from its composer (REVIEWENV1b), and only appended after: a later save
        // of the whole record never drops one a door appended meanwhile (REV3).
        command.CommandText = """
            INSERT INTO asks (id, workspace, sentence, state, tier, asked, updated, asker, note, links, attachments, proposal, quests, intake, review_choices)
            VALUES ($id, $workspace, $sentence, $state, $tier, $asked, $updated, $asker, $note, $links, $attachments, $proposal, $quests, $intake, $reviewChoices)
            ON CONFLICT (id) DO UPDATE SET
              state = $state, tier = $tier, updated = $updated, note = $note,
              links = $links, attachments = $attachments, proposal = $proposal, quests = $quests, intake = $intake
            """;
        command.Parameters.AddWithValue("$reviewChoices", Json(ask.ReviewChoices, (w, choice) =>
        {
            w.WriteStartObject();
            w.WriteString("choice", choice.Choice);
            w.WriteString("at", choice.At.ToString("O"));
            if (choice.Words is { } words) w.WriteString("words", words);
            w.WriteEndObject();
        }));
        command.Parameters.AddWithValue("$intake", (object?)ask.Intake ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", ask.Id);
        command.Parameters.AddWithValue("$workspace", ask.Workspace);
        command.Parameters.AddWithValue("$sentence", ask.Sentence);
        command.Parameters.AddWithValue("$state", ask.State.ToString());
        command.Parameters.AddWithValue("$tier", ask.Tier);
        command.Parameters.AddWithValue("$asked", ask.Asked.ToString("O"));
        command.Parameters.AddWithValue("$updated", ask.Updated.ToString("O"));
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
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>
    /// Record that the ask became <paramref name="quest"/>: appended where the list is kept, in one
    /// statement, so two publishers never overwrite each other (REV3).
    /// </summary>
    /// <remarks>
    /// An intake's connector and a person's page publish from two processes. Each used to save the
    /// whole record it had read, and the second save dropped the first one's quest. A closed ask stays
    /// closed: a quest that was published before the close landed is still one the ask became.
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
        command.Parameters.AddWithValue("$updated", now.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>
    /// Close the ask with its reason — only the columns a close owns, so a quest published meanwhile
    /// stays on it (REV3).
    /// </summary>
    public Task RecordClosedAsync(string id, string note, DateTimeOffset now, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "UPDATE asks SET state = 'Closed', note = $note, updated = $updated WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        command.Parameters.AddWithValue("$note", note);
        command.Parameters.AddWithValue("$updated", now.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>Remove the ask's row — the desk deleted it, and every quest asked by it first (D95).</summary>
    public Task DeleteAsync(string id, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "DELETE FROM asks WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>Record the intake session serving the ask — that column alone, for the same reason (REV3).</summary>
    public Task RecordIntakeAsync(string id, string session, DateTimeOffset now, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "UPDATE asks SET intake = $intake, updated = $updated WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        command.Parameters.AddWithValue("$intake", session);
        command.Parameters.AddWithValue("$updated", now.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>
    /// Keep a word the person gave on the ask (DRIFT1a): appended where the list is kept, in one statement,
    /// for the reason a published quest is (REV3) — the answer door and a driver's report arrive from two
    /// processes, and a whole-record save would drop the other's word.
    /// </summary>
    /// <returns>False when no ask has that id, and nothing was kept.</returns>
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
            writer.WriteString("at", word.At.ToString("O"));
            if (word.Session is { } session) writer.WriteString("session", session);
            if (word.Quest is { } quest) writer.WriteString("quest", quest);
            writer.WriteEndObject();
        }));
        command.Parameters.AddWithValue("$updated", now.ToString("O"));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>
    /// Read the ask's go-aheads, decide, and write what was decided, as one step across hosts (KNOWUSE1a): two sessions
    /// asking for one act at once, or a request beside the person's answer, would otherwise each write what they read and
    /// drop the other's. The column alone is written, for REV3's reason.
    /// </summary>
    /// <param name="decide">
    /// Given every entry as stored, each with what this build reads of it: the entries to write back (an entry it could not
    /// read kept as it was), or null to write nothing; and what to answer.
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
                write.Parameters.AddWithValue("$updated", now.ToString("O"));
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
            DateTimeOffset.Parse(Text("asked")), DateTimeOffset.Parse(Text("updated")), Maybe("asker"), Maybe("note"))
        {
            Links = Items(Text("links"), e => e.GetString() ?? ""),
            Attachments = Items(Text("attachments"), QuestAttachment.Stored),
            Proposal = Items(Text("proposal"), e => new DeclarationMatch(
                e.GetProperty("repository").GetString() ?? "", e.GetProperty("score").GetInt32(),
                e.GetProperty("matched").EnumerateArray().Select(w => w.GetString() ?? "").ToList())),
            Quests = Items(Text("quests"), e => e.GetString() ?? ""),
            Intake = Maybe("intake"),
            Later = LaterWords(Text("words")),
            WordsKeptFrom = Maybe("words_kept_from") is { } from ? DateTimeOffset.Parse(from) : null,
            GoAheads = GoAheads.Read(Text("go_aheads")),
            ReviewChoices = ReviewChoicesOf(Text("review_choices")),
            ReviewProposals = ReviewProposalsOf(Text("review_proposals")),
        };
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
            if (Moment(element, "at") is not { } at) continue;
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
            if (JsonFields.Text(element, "reason") is not { } reason || Moment(element, "at") is not { } at) continue;
            proposals.Add(new AskReviewProposal(choice, reason, at, JsonFields.Text(element, "session"), JsonFields.Text(element, "quest")));
        }

        return proposals;
    }

    private static DateTimeOffset? Moment(JsonElement element, string name) =>
        DateTimeOffset.TryParse(
            JsonFields.Text(element, name), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var at)
            ? at
            : null;

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
            if (!DateTimeOffset.TryParse(
                    JsonFields.Text(element, "at"), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var at))
            {
                continue;
            }

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

/// <summary>What a person asks, at a workspace.</summary>
/// <param name="Workspace">The circle it is asked in.</param>
/// <param name="Sentence">The person's words.</param>
public sealed record AskRequest(string Workspace, string Sentence)
{
    public IReadOnlyList<string> Links { get; init; } = [];

    public IReadOnlyList<QuestUpload> Uploads { get; init; } = [];

    /// <summary>The receiver, when the asker already knows whose problem it is — published at once.</summary>
    public string? To { get; init; }

    /// <summary>Who asked, when the door knows.</summary>
    public string? Asker { get; init; }

    /// <summary>
    /// The person's review choice for the ask, from the composer (REVIEWENV1b, design §1.5): <c>off</c>, <c>on</c> or an
    /// environment's name. Null for none.
    /// </summary>
    public string? Review { get; init; }

    /// <summary>The person's words with their review choice, where they give any.</summary>
    public string? ReviewWords { get; init; }
}

/// <summary>An intake's review proposal for the chain it publishes (REVIEWENV1b, design §1.5): a choice and its reason.</summary>
/// <param name="Choice"><c>off</c>, <c>on</c> or an environment's name.</param>
/// <param name="Reason">Why, in at most <see cref="Reviews.ReasonLimit"/> characters.</param>
public sealed record ReviewProposed(string? Choice, string? Reason);

/// <summary>
/// The quest an ask becomes, in words other than the ask's own — what an intake session writes once
/// it has read the ticket (D65 §1b). A publish with no draft uses the person's sentence, as it always has.
/// </summary>
/// <param name="Title">One line: what is wanted. Blank takes the ask's first line.</param>
/// <param name="Body">Why, and the evidence, with the ask's words quoted beneath. Blank takes the ask's words alone.</param>
public sealed record AskDraft(string? Title, string? Body)
{
    /// <summary>Addresses beyond the ask's own — the ask's always travel too.</summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>Files beyond the ask's own — the ask's always travel too.</summary>
    public IReadOnlyList<QuestUpload> Uploads { get; init; } = [];

    /// <summary>What to ask next once this closes done (D65 §4) — judged by the exchange, like any chain.</summary>
    public IReadOnlyList<QuestStep> Then { get; init; } = [];

    /// <summary>
    /// What the person requires, in their own words with the check that proves each (DRIFT1c, D133 §3) —
    /// judged by the exchange against the ask's words.
    /// </summary>
    public IReadOnlyList<QuestRequirement> Requirements { get; init; } = [];

    /// <summary>
    /// The quest's short title (SESSUX1j), in the intake's words: the few that tell it apart in a list. Blank leaves the
    /// quest to be named from its own words.
    /// </summary>
    public string? Short { get; init; }

    /// <summary>
    /// The chain's review choice (REVIEWENV1b, design §1.5): from an intake, only with the person's words it quotes, which
    /// the exchange checks against the ask's. Null for none.
    /// </summary>
    public QuestReview? Review { get; init; }

    /// <summary>
    /// The intake's review proposal for this chain, where it has none of the person's words to set one on (design §1.5–§1.6):
    /// kept on the ask, beside the choice, for the person's press. Null for none.
    /// </summary>
    public ReviewProposed? ReviewProposal { get; init; }
}

/// <summary>Why an ask did not do what was asked of it — or <see cref="None"/> when it did.</summary>
public enum AskRefusal
{
    None,

    /// <summary>No sentence, or a close with no reason.</summary>
    Empty,

    /// <summary>A link or a file a quest could not carry either.</summary>
    BadCarry,

    /// <summary>The circle is not one this machine holds.</summary>
    UnknownWorkspace,

    /// <summary>No ask under that id.</summary>
    NotFound,

    /// <summary>The ask was closed; it becomes nothing more.</summary>
    Closed,

    /// <summary>The quest it was to become was refused by the exchange — the ask itself is kept.</summary>
    QuestRefused,

    /// <summary>
    /// A delete that would remove a record something stands on — a quest asked by it that somebody took
    /// or answered, or that a session was started for — so the ask stays whole (D95).
    /// </summary>
    Kept,

    /// <summary>
    /// A review choice or proposal that is not one (REVIEWENV1b): not <c>off</c>, <c>on</c> or an environment's name, words
    /// or a reason past its bound, a proposal with no reason or from no intake, or a choice and a proposal at once.
    /// </summary>
    BadReview,
}

/// <param name="Refusal"><see cref="AskRefusal.None"/> when it did what was asked.</param>
/// <param name="Message">The whole answer, phrased once here for every door.</param>
/// <param name="Ask">The ask as it now stands — present even when its quest was refused.</param>
/// <param name="Quest">The quest it became on this call, when it became one.</param>
public sealed record AskOutcome(AskRefusal Refusal, string Message, Ask? Ask, Quest? Quest = null);

/// <summary>
/// The judgement over asks (D65 §1a): what may be asked where, what the no-model tier proposes, and
/// how an ask becomes a quest. The store holds state; this decides — the exchange's shape, for the
/// same reason: two doors (the page and `daoris-driver ask`), one set of answers.
/// </summary>
/// <remarks>
/// <b>An ask's quests are asked BY the ask</b> — <c>ask #id</c> is their sender. It cannot be mistaken
/// for a repository, it names the record a reader can follow back to the person's words, and the
/// exchange places it in the ask's own circle, because an ask has no registry row to say it (D48 §4).
/// </remarks>
public sealed partial class AskDesk(KnowledgeService service, AskStore asks, QuestExchange exchange, QuestFiles? files)
{
    /// <summary>The folder under the home an ask keeps its files in, until it becomes quests.</summary>
    public const string Folder = "asks";

    /// <summary>The tier that answers with no harness at all: the repositories' own declarations.</summary>
    public const string ByDeclarations = "declarations";

    /// <summary>The asker named the receiver — no tier had to decide.</summary>
    public const string ByName = "named";

    /// <summary>
    /// The ask's own intake session published it (D65 §1b) — a harness decided, and which harness is
    /// on the session record. Never a model's name (D24): the harness carries the model.
    /// </summary>
    public const string ByIntake = "intake";

    /// <summary>How long a quest's title may be when an ask's first line becomes one.</summary>
    private const int TitleLength = 100;

    private readonly QuestFiles? _askFiles = files?.For(Folder);

    /// <summary>The sender every quest an ask becomes is published by.</summary>
    public static string SenderOf(string askId) => $"ask #{askId}";

    /// <summary>
    /// The ask a quest was asked by, read from its sender (DRIFT1a) — the reverse of <see cref="SenderOf"/>;
    /// null for a quest a repository asked. A chain step names the ask too, since it is published on the
    /// ask's behalf (D65 §4).
    /// </summary>
    public static string? AskOf(string? sender)
    {
        var prefix = SenderOf("");
        return sender is not null && sender.Length > prefix.Length && sender.StartsWith(prefix, StringComparison.Ordinal)
            ? sender[prefix.Length..]
            : null;
    }

    /// <summary>
    /// A circle's asks, or every circle's, newest first, each as it STANDS (USE1c) — a done one only
    /// when closed ones are asked for, as a closed one is.
    /// </summary>
    public async Task<IReadOnlyList<Ask>> ListAsync(string? workspace = null, bool includeClosed = false, CancellationToken ct = default)
    {
        var stored = await asks.ListAsync(workspace, includeClosed, ct).ConfigureAwait(false);
        // One read for every ask's quests: a sender that is an ask always begins the same way.
        var everyAsked = await exchange.Store.FromAsync(SenderOf(""), startingWith: true, ct).ConfigureAwait(false);
        var deletable = await exchange.DeletableAsync(everyAsked, ct).ConfigureAwait(false);
        var asked = everyAsked.ToLookup(quest => quest.From, StringComparer.Ordinal);
        var standing = stored.Select(ask => Stood(ask, [.. asked[SenderOf(ask.Id)]], deletable));
        return [.. includeClosed ? standing : standing.Where(ask => ask.State != AskState.Done)];
    }

    /// <summary>One ask as it stands (USE1c), or null when there is none.</summary>
    public async Task<Ask?> FindAsync(string id, CancellationToken ct = default)
    {
        if (await asks.FindAsync(id, ct).ConfigureAwait(false) is not { } ask) return null;
        var asked = await exchange.Store.FromAsync(SenderOf(ask.Id), ct: ct).ConfigureAwait(false);
        return Stood(ask, asked, await exchange.DeletableAsync(asked, ct).ConfigureAwait(false));
    }

    /// <summary>An ask as a reader is given it: as it stands, and whether it may be deleted (D95).</summary>
    private static Ask Stood(Ask ask, IReadOnlyList<Quest> asked, IReadOnlySet<string> deletable) =>
        Standing(ask, asked) with { Deletable = asked.All(quest => deletable.Contains(quest.Id)) };

    /// <summary>
    /// What an ask is, read against the quests asked by it (USE1c): a published ask whose every quest
    /// has closed is <see cref="AskState.Done"/>. Derived on every read and never stored, because the
    /// quests can move on another machine, and the sync that brings the move here knows nothing of
    /// asks (D68 §2). One judgement, for every reader of an ask's state.
    /// </summary>
    /// <remarks>
    /// A quest deleted (D95), here or on another machine, leaves the record as if the ask had never
    /// become it; a published ask left with none is a proposal again, for a person to publish or close.
    /// A quest closed done departing from what the person required has not closed for the ask (DRIFT1d,
    /// D133 §4): it waits for their yes, and an ask reading done would say they had agreed.
    /// </remarks>
    /// <param name="asked">Every quest asked by this ask — chain steps included, closed ones included.</param>
    public static Ask Standing(Ask ask, IReadOnlyList<Quest> asked)
    {
        var held = asked.Select(quest => quest.Id).ToHashSet(StringComparer.Ordinal);
        var standing = ask.Quests.All(held.Contains) ? ask : ask with { Quests = [.. ask.Quests.Where(held.Contains)] };
        if (standing.State != AskState.Published) return standing;
        if (asked.Count == 0) return standing with { State = AskState.Proposed };
        return asked.All(quest => quest.Status is QuestStatus.Done or QuestStatus.Declined && !quest.Held)
            ? standing with { State = AskState.Done }
            : standing;
    }

    /// <summary>
    /// Delete an ask made by mistake (D95), with every quest asked by it — or none of it, when any of
    /// those quests must stay. An ask that became nothing goes alone. The files it kept go with it.
    /// </summary>
    public async Task<AskOutcome> DeleteAsync(string id, DateTimeOffset now, CancellationToken ct = default)
    {
        var ask = await asks.FindAsync(id, ct).ConfigureAwait(false);
        if (ask is null) return new(AskRefusal.NotFound, $"No ask `#{id.TrimStart('#')}`.", Ask: null);

        const string Instead =
            "An ask is deleted only with every quest asked by it — close the ask instead, with the reason, and it leaves the list.";
        var asked = await exchange.Store.FromAsync(SenderOf(ask.Id), ct: ct).ConfigureAwait(false);
        foreach (var quest in asked)
        {
            if (await exchange.KeptAsync(quest, ct).ConfigureAwait(false) is { } kept)
            {
                return new(
                    AskRefusal.Kept,
                    $"Ask `#{ask.Id}` stays, with its quests: quest `#{quest.Id}` {kept.Stands}. {Instead}",
                    await FindAsync(ask.Id, ct).ConfigureAwait(false));
            }
        }

        var deleted = new List<string>();
        var notes = new List<string>();
        foreach (var quest in asked)
        {
            var outcome = await exchange.DeleteAsync(quest.Id, now, ct).ConfigureAwait(false);
            if (outcome.Refusal == QuestDeleteRefusal.None)
            {
                deleted.Add($"`#{quest.Id}`");
                if (outcome.Unconfirmed) notes.Add(outcome.Message);
                continue;
            }

            // It moved while the ask was being deleted — taken on another machine first, most likely.
            // The ask stays, with whatever is left of it, and the answer says what did go.
            return new(
                AskRefusal.Kept,
                $"Ask `#{ask.Id}` stays: {outcome.Message}"
                + (deleted.Count > 0 ? $" Deleted before it: {string.Join(", ", deleted)}." : ""),
                await FindAsync(ask.Id, ct).ConfigureAwait(false));
        }

        await asks.DeleteAsync(ask.Id, ct).ConfigureAwait(false);
        _askFiles?.Forget(ask.Id);
        var message = $"Deleted ask `#{ask.Id}`"
                      + (deleted.Count == 0 ? "." : $", and the quests asked by it with it: {string.Join(", ", deleted)}.");
        return new(AskRefusal.None, string.Join("\n\n", [message, .. notes]), Ask: null);
    }

    /// <summary>
    /// Record an ask, and answer it: a named receiver is published to at once; otherwise the
    /// declarations tier proposes and SAYS SO, and nothing is published on a guess.
    /// </summary>
    public async Task<AskOutcome> AskAsync(AskRequest request, DateTimeOffset now, CancellationToken ct = default)
    {
        var sentence = request.Sentence.Trim();
        if (sentence.Length == 0)
        {
            return new(AskRefusal.Empty, "An ask needs its words — what is wanted, and why.", Ask: null);
        }

        var registered = await service.RegistryAsync(ct: ct).ConfigureAwait(false);
        var workspace = Workspaces.Normalize(request.Workspace);
        var held = registered.Select(r => r.InWorkspace).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
        if (!held.Any(circle => Workspaces.Same(circle, workspace)))
        {
            return new(
                AskRefusal.UnknownWorkspace,
                $"`{workspace}` is not a workspace this machine holds — an ask is made in a workspace, and the "
                + $"workspace is whom it can reach. Held here: {string.Join(", ", held)}.",
                Ask: null);
        }

        var (unfit, why, links) = exchange.JudgeCarry(request.Links, request.Uploads);
        if (unfit is not null) return new(AskRefusal.BadCarry, why, Ask: null);

        // The person's review choice from the composer (REVIEWENV1b), judged before anything is kept.
        AskReviewChoice? chosen = null;
        if (request.Review is not null)
        {
            var (choice, unfitChoice) = JudgeChoice(request.Review, request.ReviewWords, now);
            if (unfitChoice is not null) return new(AskRefusal.BadReview, unfitChoice, Ask: null);
            chosen = choice;
        }

        // The same words in the same circle are the same ask — a retry, or a person repeating
        // themselves, is answered with what became of the first, never a second copy. 🔴 Unless the
        // person CLOSED it (ASKAGAIN1): they ended that one, so the same words afterwards ask anew,
        // and the closed record stays as it was. Found asking a ticket again after its run failed. A DONE
        // ask ended as surely (USE1c): its work finished, and the default list no longer shows it.
        var id = AskStore.MakeId(workspace, sentence);
        for (var again = 1; await FindAsync(id, ct).ConfigureAwait(false) is { } existing; again++)
        {
            if (existing.State is not (AskState.Closed or AskState.Done))
            {
                return new(AskRefusal.None, $"Ask `#{id}` was already asked in `{workspace}` — {Describe(existing)}", existing);
            }

            id = AskStore.MakeId(workspace, sentence, again);
        }

        var attachments = new List<QuestAttachment>();
        foreach (var upload in request.Uploads)
        {
            attachments.Add(await _askFiles!.KeepAsync(id, upload, ct).ConfigureAwait(false));
        }

        var ask = new Ask(id, workspace, sentence, AskState.Open, ByDeclarations, now, now, request.Asker)
        {
            Links = links,
            Attachments = attachments.DistinctBy(a => a.Sha256).ToList(),
            Proposal = DeclarationsTier.Rank(sentence, registered, workspace),
            ReviewChoices = chosen is null ? [] : [chosen],
        };

        if (request.To is { Length: > 0 } to)
        {
            var published = await PublishQuestAsync(ask, to, now, ct).ConfigureAwait(false);
            if (published.Quest is not null)
            {
                var took = ask with { State = AskState.Published, Tier = ByName, Quests = [published.Quest.Id] };
                await asks.SaveAsync(took, ct).ConfigureAwait(false);
                return new(AskRefusal.None, $"Asked as `#{id}` in `{workspace}`.\n\n{published.Message}", took, published.Quest);
            }

            // The receiver the person named could not be asked. The ask is still what they meant, so
            // it is kept — proposed by declarations, so the refusal arrives with somewhere to go.
            var kept = ask with { State = AskState.Proposed, Note = published.Message };
            await asks.SaveAsync(kept, ct).ConfigureAwait(false);
            return new(
                AskRefusal.QuestRefused,
                $"{published.Message}\n\nThe ask is kept as `#{id}` — {Describe(kept)}",
                kept);
        }

        var proposed = ask with { State = AskState.Proposed };
        await asks.SaveAsync(proposed, ct).ConfigureAwait(false);
        return new(AskRefusal.None, $"Asked as `#{id}` in `{workspace}` — {Describe(proposed)}", proposed);
    }

    /// <summary>
    /// A person turns an ask into a quest to <paramref name="to"/> — a proposal accepted, or a receiver
    /// they chose themselves — or the ask's intake session does, in its own words (D65 §1b). An ask may
    /// become several quests; each is recorded on it.
    /// </summary>
    /// <param name="draft">The quest's words when they are not the ask's — an intake's, which read the ticket.</param>
    /// <param name="session">
    /// The session publishing, when it is one. Only the ask's OWN intake moves its tier to
    /// <see cref="ByIntake"/> — a session naming itself is a claim, and the ask is what can check it.
    /// </param>
    /// <param name="byAgent">
    /// An agent publishes, whether or not it names a session: a connector always does (REVIEWENV1b3). A session named is an
    /// agent too, so only a publish with neither carries the person's authority.
    /// </param>
    public async Task<AskOutcome> PublishAsync(
        string id, string to, DateTimeOffset now, CancellationToken ct = default,
        AskDraft? draft = null, string? session = null, bool byAgent = false)
    {
        var ask = await asks.FindAsync(id, ct).ConfigureAwait(false);
        if (ask is null)
        {
            return new(AskRefusal.NotFound, $"No ask `#{id.TrimStart('#')}`.", Ask: null);
        }

        if (ask.State == AskState.Closed)
        {
            return new(AskRefusal.Closed, $"Ask `#{ask.Id}` is closed ({ask.Note}) — it becomes nothing more.", ask);
        }

        // An intake's review proposal (REVIEWENV1b), judged before anything is published, so a refused one leaves nothing behind.
        var agent = byAgent || !string.IsNullOrWhiteSpace(session);
        var (proposed, unfitProposal) = JudgeProposal(draft, session, agent, now);
        if (unfitProposal is not null) return new(AskRefusal.BadReview, unfitProposal, ask);

        var published = await PublishQuestAsync(ask, to, now, ct, draft, session, agent).ConfigureAwait(false);
        if (published.Quest is null) return new(AskRefusal.QuestRefused, published.Message, ask);

        var byIntake = session is { Length: > 0 } && string.Equals(session, ask.Intake, StringComparison.Ordinal);
        await asks.RecordPublishedAsync(ask.Id, published.Quest.Id, byIntake ? ByIntake : null, now, ct)
            .ConfigureAwait(false);
        var message = published.Message;
        if (proposed is not null)
        {
            await asks.RecordReviewProposalAsync(ask.Id, proposed with { Quest = published.Quest.Id }, now, ct).ConfigureAwait(false);
            message += $"\n\nProposed review `{proposed.Choice}` for this chain, kept on ask `#{ask.Id}` with your reason: a proposal, which "
                       + "only the person's press applies.";
        }

        var took = await FindAsync(ask.Id, ct).ConfigureAwait(false) ?? ask;
        return new(AskRefusal.None, message, took, published.Quest);
    }

    /// <summary>The person answers their own ask: closed, with the reason, and final.</summary>
    public async Task<AskOutcome> CloseAsync(string id, string reason, DateTimeOffset now, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new(AskRefusal.Empty, "Closing an ask needs a reason — it is the record of what became of it.", Ask: null);
        }

        var ask = await asks.FindAsync(id, ct).ConfigureAwait(false);
        if (ask is null) return new(AskRefusal.NotFound, $"No ask `#{id.TrimStart('#')}`.", Ask: null);

        await asks.RecordClosedAsync(ask.Id, reason.Trim(), now, ct).ConfigureAwait(false);
        var closed = await FindAsync(ask.Id, ct).ConfigureAwait(false) ?? ask;
        return new(AskRefusal.None, $"Ask `#{ask.Id}` is closed: {closed.Note}", closed);
    }

    /// <summary>
    /// One quest from the ask, asked BY the ask, in its circle, carrying its links and the files it
    /// kept — and, from an intake, its own words and chain beside them. The exchange judges it — who
    /// may be asked is the exchange's answer, never this desk's.
    /// </summary>
    private async Task<QuestPublishOutcome> PublishQuestAsync(
        Ask ask, string to, DateTimeOffset now, CancellationToken ct, AskDraft? draft = null, string? session = null, bool agent = false)
    {
        var uploads = new List<QuestUpload>();
        foreach (var attachment in ask.Attachments)
        {
            if (_askFiles is not null && _askFiles.Has(ask.Id, attachment))
            {
                uploads.Add(new QuestUpload(
                    attachment.Name,
                    await File.ReadAllBytesAsync(_askFiles.PathOf(ask.Id, attachment), ct).ConfigureAwait(false)));
            }
        }

        // The ask's own carry always travels: the person gave those, and whatever the intake read
        // beside them adds to it rather than replacing it.
        return await exchange.PublishAsync(
            new QuestAsk(
                SenderOf(ask.Id), to,
                draft?.Title is { } title && !string.IsNullOrWhiteSpace(title) ? title.Trim() : TitleOf(ask.Sentence),
                string.IsNullOrWhiteSpace(draft?.Body) ? BodyOf(ask) : DraftBodyOf(ask, draft.Body))
            {
                Links = [.. ask.Links.Concat(draft?.Links ?? []).Distinct(StringComparer.Ordinal)],
                Uploads = [.. uploads, .. draft?.Uploads ?? []],
                Then = draft?.Then ?? [],
                // The person's words, each with its check (DRIFT1c): the exchange judges them against this ask's.
                Requirements = draft?.Requirements ?? [],
                // The intake's short title (SESSUX1j); a publish with none is named from the ask's words.
                Short = draft?.Short,
                // The chain's review choice (REVIEWENV1b): the exchange checks a session's quote against this ask's words.
                Review = draft?.Review,
                Workspace = ask.Workspace,
                // The session publishing, as its connector names it (SESS1): the intake that read the ask.
                PublishedBy = session,
                // Whether an agent publishes, apart from which session it credits (REVIEWENV1b3).
                ByAgent = agent,
            },
            now, ct).ConfigureAwait(false);
    }

    /// <summary>What became of an ask, in a sentence — which tier answered, and what it said.</summary>
    private static string Describe(Ask ask) => ask.State switch
    {
        AskState.Published => $"it became {string.Join(", ", ask.Quests.Select(q => $"`#{q}`"))}.",
        AskState.Done => $"done: it became {string.Join(", ", ask.Quests.Select(q => $"`#{q}`"))}, and every quest it asked has closed.",
        AskState.Closed => $"closed: {ask.Note}",
        // The honest sentence once a harness is on it — "no intake harness ran" would be untrue now.
        _ when ask.Intake is { } intake =>
            $"intake session `{intake}` is answering it: it publishes where the declarations settle it, "
            + "and asks you where they do not.",
        _ when ask.Proposal.Count == 0 =>
            "by declarations only; no intake agent ran — and no repository's declarations share its words. "
            + "Nothing was published. Name the receiver, or declare what the owner owns and ask again.",
        _ => "by declarations only; no intake agent ran — proposed, best first: "
             + string.Join("; ", ask.Proposal.Select(m => $"`{m.Repository}` ({string.Join(", ", m.Matched)})"))
             + ". Nothing was published: a proposal is a person's to accept.",
    };

    /// <summary>The ask's first line, cut at a word if it is long — a quest's title is one line.</summary>
    internal static string TitleOf(string sentence)
    {
        var line = sentence.Split('\n', 2)[0].Trim();
        if (line.Length <= TitleLength) return line;
        var cut = line[..TitleLength];
        var space = cut.LastIndexOf(' ');
        return (space > TitleLength / 2 ? cut[..space] : cut).TrimEnd() + "…";
    }

    /// <summary>The ask's words whole, and where they came from.</summary>
    private static string BodyOf(Ask ask) =>
        $"{ask.Sentence}\n\n— asked at workspace `{ask.Workspace}` (ask `#{ask.Id}`).";

    /// <summary>
    /// An intake's words first — it read the ticket — and the person's own beneath them, verbatim: a
    /// receiver weighing a paraphrase deserves the words it was paraphrased from.
    /// </summary>
    private static string DraftBodyOf(Ask ask, string words)
    {
        var quoted = string.Join("\n", ask.Sentence.Split('\n').Select(line => $"> {line.TrimEnd('\r')}"));
        return $"{words.Trim()}\n\n— asked at workspace `{ask.Workspace}` (ask `#{ask.Id}`), in the asker's words:\n\n{quoted}";
    }
}
