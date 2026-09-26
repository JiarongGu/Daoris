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
}

/// <summary>Asks, held by the service beside the quests they become — machine-local, like the intake.</summary>
public sealed class AskStore
{
    private readonly SqliteConnection _connection;

    private AskStore(SqliteConnection connection) => _connection = connection;

    public static async Task<AskStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        var store = new AskStore(connection);
        await using var command = connection.CreateCommand();
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
        await SchemaColumns.EnsureAsync(connection, "asks", "intake", "intake TEXT NULL", ct).ConfigureAwait(false);

        return store;
    }

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
    public async Task SaveAsync(Ask ask, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO asks (id, workspace, sentence, state, tier, asked, updated, asker, note, links, attachments, proposal, quests, intake)
            VALUES ($id, $workspace, $sentence, $state, $tier, $asked, $updated, $asker, $note, $links, $attachments, $proposal, $quests, $intake)
            ON CONFLICT (id) DO UPDATE SET
              state = $state, tier = $tier, updated = $updated, note = $note,
              links = $links, attachments = $attachments, proposal = $proposal, quests = $quests, intake = $intake
            """;
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
    }

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
    public async Task RecordPublishedAsync(
        string id, string quest, string? tier, DateTimeOffset now, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
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
    }

    /// <summary>
    /// Close the ask with its reason — only the columns a close owns, so a quest published meanwhile
    /// stays on it (REV3).
    /// </summary>
    public async Task RecordClosedAsync(string id, string note, DateTimeOffset now, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "UPDATE asks SET state = 'Closed', note = $note, updated = $updated WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        command.Parameters.AddWithValue("$note", note);
        command.Parameters.AddWithValue("$updated", now.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Record the intake session serving the ask — that column alone, for the same reason (REV3).</summary>
    public async Task RecordIntakeAsync(string id, string session, DateTimeOffset now, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "UPDATE asks SET intake = $intake, updated = $updated WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        command.Parameters.AddWithValue("$intake", session);
        command.Parameters.AddWithValue("$updated", now.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<Ask?> FindAsync(string id, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM asks WHERE id = $id";
        command.Parameters.AddWithValue("$id", id.TrimStart('#'));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>A circle's asks — or every circle's — newest first; closed ones only when asked for.</summary>
    public async Task<IReadOnlyList<Ask>> ListAsync(
        string? workspace = null, bool includeClosed = false, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
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
    }

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
        };
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
}

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
public sealed class AskDesk(KnowledgeService service, AskStore asks, QuestExchange exchange, QuestFiles? files)
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

    public Task<IReadOnlyList<Ask>> ListAsync(string? workspace = null, bool includeClosed = false, CancellationToken ct = default) =>
        asks.ListAsync(workspace, includeClosed, ct);

    public Task<Ask?> FindAsync(string id, CancellationToken ct = default) => asks.FindAsync(id, ct);

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

        // The same words in the same circle are the same ask — a retry, or a person repeating
        // themselves, is answered with what became of the first, never a second copy. 🔴 Unless the
        // person CLOSED it (ASKAGAIN1): they ended that one, so the same words afterwards ask anew,
        // and the closed record stays as it was. Found asking a ticket again after its run failed.
        var id = AskStore.MakeId(workspace, sentence);
        for (var again = 1; await asks.FindAsync(id, ct).ConfigureAwait(false) is { } existing; again++)
        {
            if (existing.State != AskState.Closed)
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
    public async Task<AskOutcome> PublishAsync(
        string id, string to, DateTimeOffset now, CancellationToken ct = default,
        AskDraft? draft = null, string? session = null)
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

        var published = await PublishQuestAsync(ask, to, now, ct, draft).ConfigureAwait(false);
        if (published.Quest is null) return new(AskRefusal.QuestRefused, published.Message, ask);

        var byIntake = session is { Length: > 0 } && string.Equals(session, ask.Intake, StringComparison.Ordinal);
        await asks.RecordPublishedAsync(ask.Id, published.Quest.Id, byIntake ? ByIntake : null, now, ct)
            .ConfigureAwait(false);
        var took = await asks.FindAsync(ask.Id, ct).ConfigureAwait(false) ?? ask;
        return new(AskRefusal.None, published.Message, took, published.Quest);
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
        var closed = await asks.FindAsync(ask.Id, ct).ConfigureAwait(false) ?? ask;
        return new(AskRefusal.None, $"Ask `#{ask.Id}` is closed: {closed.Note}", closed);
    }

    /// <summary>
    /// One quest from the ask, asked BY the ask, in its circle, carrying its links and the files it
    /// kept — and, from an intake, its own words and chain beside them. The exchange judges it — who
    /// may be asked is the exchange's answer, never this desk's.
    /// </summary>
    private async Task<QuestPublishOutcome> PublishQuestAsync(
        Ask ask, string to, DateTimeOffset now, CancellationToken ct, AskDraft? draft = null)
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
                Workspace = ask.Workspace,
            },
            now, ct).ConfigureAwait(false);
    }

    /// <summary>What became of an ask, in a sentence — which tier answered, and what it said.</summary>
    private static string Describe(Ask ask) => ask.State switch
    {
        AskState.Published => $"it became {string.Join(", ", ask.Quests.Select(q => $"`#{q}`"))}.",
        AskState.Closed => $"closed: {ask.Note}",
        // The honest sentence once a harness is on it — "no intake harness ran" would be untrue now.
        _ when ask.Intake is { } intake =>
            $"intake session `{intake}` is answering it: it publishes where the declarations settle it, "
            + "and asks you where they do not.",
        _ when ask.Proposal.Count == 0 =>
            "by declarations only; no intake harness ran — and no repository's declarations share its words. "
            + "Nothing was published. Name the receiver, or declare what the owner owns and ask again.",
        _ => "by declarations only; no intake harness ran — proposed, best first: "
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
