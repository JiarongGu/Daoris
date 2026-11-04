using Microsoft.Data.Sqlite;
using static Daoris.Knowledge.QuestRecordCodec;

namespace Daoris.Knowledge;

/// <summary>
/// Quests, held by the service rather than written into anyone's repository.
/// </summary>
/// <remarks>
/// <para><b>This is a server responsibility, and the first version got it wrong.</b> The original
/// implementation had one repository's agent write a quest straight into another repository's backlog
/// file. That is the very thing `repository-owns-its-work` forbids — an outside edit is still an
/// outside edit when it is one file and uncommitted, and it arrives from the party that knows that
/// codebase least. Building the tool that way made the rule's own tooling break the rule.</para>
///
/// <para><b>So the service holds them and repositories pull.</b> An agent publishes a quest here; the
/// receiving repository's own agent reads what is addressed to it and decides what to do — including
/// materializing it into its backlog, which is then that repository editing itself. Nobody reaches
/// across.</para>
///
/// <para><b>Only a repository something can answer for is addressed.</b> That is an adopter, whose own
/// connector sees the quest, or a repository registered with a root on this machine, whose driven
/// session is handed a connector over the protocol door (D70). A quest nothing could answer would sit
/// in a queue nobody reads, so refusing at publish time says that immediately, rather than letting it
/// look delivered.</para>
///
/// <para><b>A quest is its history</b> (D68, SYNC1). Every verb appends an operation to
/// <c>quest_log</c>, stamped with this store's machine and that machine's next sequence number, and
/// the <c>quests</c> table is rewritten from replaying the quest's history through
/// <see cref="QuestTransitions"/> in the same transaction — so what the platform reads is a cache of
/// the replay, never a second truth beside it.</para>
///
/// <para><b>A remote is where histories meet</b> (D68 §3, design §8): a machine fetches what the
/// remote accepted, rebases what it has not pushed on top, and pushes; the remote accepts a quest's
/// operations only when nothing reached that quest since the push was rebased. Both halves live here,
/// behind the same table: <see cref="IntegrateAsync"/>, <see cref="PendingAsync"/> and
/// <see cref="AcceptedAsync"/> for a machine, <see cref="OperationsSinceAsync"/> and
/// <see cref="ReceiveAsync"/> for a remote.</para>
/// </remarks>
public sealed class QuestStore
{
    /// <summary>
    /// Hex characters in a quest id: 48 bits (design §7). Six were enough while a machine held its own
    /// quests; once every machine holds every quest touching its repositories, unrelated asks collide.
    /// </summary>
    internal const int IdLength = 12;

    /// <summary>What an id was before it widened — a quest published then keeps the id it was quoted by.</summary>
    internal const int LegacyIdLength = 6;

    /// <summary>
    /// How long a graph named for a quest's take waits for it (FABRIC-CLOSE1o): the driver names it as the quest's first session
    /// opens, whose first act is the take, so a take later than this is not that start's, and goes unbound, saying so.
    /// </summary>
    public static readonly TimeSpan TakeBindLife = TimeSpan.FromHours(1);

    /// <summary>
    /// The version of what this build keeps in the store (CLOSETAKE1, QUESTSTOREVER1), stamped in <c>quest_store_version</c>. 1 is the
    /// build with owned take binds (FABRIC-CLOSE1w); raise it with any change to a table or a contract an older build would misread.
    /// </summary>
    public const int StoreVersion = 1;

    /// <summary>The version a newer build stamped on this store, or null where this build's is the newest that has written it.</summary>
    public int? NewerVersion { get; private set; }

    /// <summary>The connection's gate: every command here runs inside it (SQLITETX1).</summary>
    private readonly ConnectionGate _db;

    /// <summary>
    /// The clock a take measures its wait for the write on (FABRIC-CLOSE1w), so the bind named for it is judged at the moment the
    /// write began, never at the caller's moment before it queued.
    /// </summary>
    private readonly TimeProvider _time;

    private readonly QuestCache _cache;

    private QuestStore(SqliteConnection connection, TimeProvider time)
    {
        _db = ConnectionGate.For(connection);
        _time = time;
        _cache = new QuestCache(this);
        _sync = new QuestLogSync(this);
    }

    internal ConnectionGate Db => _db;

    private readonly QuestLogSync _sync;

    internal QuestLogSync Sync => _sync;

    /// <summary>What a machine has made and a remote has not yet accepted, in a circle (see <see cref="QuestLogSync"/>).</summary>
    public Task<IReadOnlyList<QuestOperation>> PendingAsync(
        string workspace, Func<string, bool> shared, CancellationToken ct = default) =>
        _sync.PendingAsync(workspace, shared, ct);

    /// <summary>Records the numbers a remote gave operations this machine pushed.</summary>
    public Task AcceptedAsync(IReadOnlyList<QuestAcceptance> accepted, CancellationToken ct = default) =>
        _sync.AcceptedAsync(accepted, ct);

    /// <summary>How a circle stands against its remote: what waits to be pushed, and what the last pass found.</summary>
    public Task<QuestStanding> StandingAsync(string workspace, Func<string, bool> shared, CancellationToken ct = default) =>
        _sync.StandingAsync(workspace, shared, ct);

    /// <summary>What a remote accepted after a position, for a machine to fetch.</summary>
    public Task<QuestFetch> OperationsSinceAsync(long since, int limit = 500, CancellationToken ct = default) =>
        _sync.OperationsSinceAsync(since, limit, ct);

    /// <summary>A remote's half of a push: accepts the operations of each quest nothing reached since the push was rebased.</summary>
    public Task<QuestPush> ReceiveAsync(
        long @base, IReadOnlyList<QuestOperation> pushed, Func<Quest, string?> judge, Func<Quest, string> workspaceOf,
        CancellationToken ct = default) =>
        _sync.ReceiveAsync(@base, pushed, judge, workspaceOf, ct);

    /// <summary>Write a quest's row as its replay gives it: the row is a cache, so a replay alone decides what it says.</summary>
    internal Task WriteCacheAsync(Quest quest, SqliteTransaction transaction, CancellationToken ct) =>
        _cache.WriteAsync(quest, transaction, ct);

    /// <summary>
    /// This store's machine: the stable id every operation it writes is stamped with (D68 §2) — never
    /// the key, which rotates.
    /// </summary>
    /// <remarks>
    /// Kept in the store, beside the sequence it numbers, rather than in a file of its own: a machine
    /// whose store was deleted starts its sequence again at one, and under the same id that would
    /// name operations a remote already holds. A new store is a new machine, and cannot be anything else.
    /// </remarks>
    public string Machine { get; private set; } = "";

    /// <param name="connection">The store's connection, opened.</param>
    /// <param name="ct">Honoured while waiting for the connection's gate.</param>
    /// <param name="time">The clock a take measures its wait on (FABRIC-CLOSE1w); the system's where none is named.</param>
    public static Task<QuestStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default, TimeProvider? time = null) =>
        ConnectionGate.For(connection).RunAsync(async () =>
        {
            _ = Operations;
            var store = new QuestStore(connection, time ?? TimeProvider.System);
            store.NewerVersion = await QuestSchema.EnsureAsync(store._db, ct).ConfigureAwait(false);
            store.Machine = await store.EnsureMachineAsync(ct).ConfigureAwait(false);
            await store.LoadClosuresAsync(ct).ConfigureAwait(false);
            await store.GiveHistoriesAsync(ct).ConfigureAwait(false);
            await store._cache.RepairAsync(ct).ConfigureAwait(false);
            return store;
        }, ct);

    /// <summary>
    /// The store's machine id, made the first time any host opens it. One fixed row, so two hosts
    /// opening a new store at once cannot each make one: the second insert is ignored and both read
    /// the first.
    /// </summary>
    private async Task<string> EnsureMachineAsync(CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = """
            INSERT OR IGNORE INTO quest_machine (one, id) VALUES (1, $id);
            SELECT id FROM quest_machine WHERE one = 1;
            """;
        command.Parameters.AddWithValue(
            "$id", Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8)).ToLowerInvariant());
        return (string)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
    }

    /// <summary>
    /// Give every quest of this store's own that the log has never seen its history, from what its row
    /// says: published when it was filed, and the move to where it stands when it last moved.
    /// </summary>
    /// <remarks>
    /// A quest is not derivable from anything, so a store from before the log is migrated rather than
    /// rebuilt (the index's rule is for what can be re-read). Checked again inside the transaction,
    /// because two hosts open one store and only one of them may write the histories.
    /// </remarks>
    private async Task GiveHistoriesAsync(CancellationToken ct)
    {
        const string Unlogged = """
            SELECT * FROM quests
            WHERE NOT EXISTS (SELECT 1 FROM quest_log WHERE quest_log.quest = quests.id)
            ORDER BY filed, id
            """;

        await using (var probe = _db.Command())
        {
            probe.CommandText = $"SELECT EXISTS ({Unlogged})";
            if (Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false)) == 0) return;
        }

        await InTransactionAsync(async (transaction, inside) =>
        {
            var unlogged = new List<Quest>();
            await using (var select = _db.Command())
            {
                select.Transaction = transaction;
                select.CommandText = Unlogged;
                await using var reader = await select.ExecuteReaderAsync(inside).ConfigureAwait(false);
                while (await reader.ReadAsync(inside).ConfigureAwait(false)) unlogged.Add(Read(reader));
            }

            foreach (var quest in unlogged) await GiveHistoryAsync(quest, transaction, inside).ConfigureAwait(false);
            return unlogged.Count;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>One quest's history, from its row — the operations that replay to exactly what it says.</summary>
    internal async Task<IReadOnlyList<QuestOperation>> GiveHistoryAsync(
        Quest quest, SqliteTransaction transaction, CancellationToken ct)
    {
        var history = new List<QuestOperation>
        {
            await AppendAsync(quest.Id, QuestOperationKind.Published, quest.Filed, null, quest, transaction, ct)
                .ConfigureAwait(false),
        };

        if (QuestTransitions.KindFor(quest.Status) is { } moved)
        {
            history.Add(await AppendAsync(quest.Id, moved, quest.Updated, quest.Note, null, transaction, ct, noteParts: quest.NoteParts)
                .ConfigureAwait(false));
        }

        return history;
    }

    /// <summary>
    /// Run <paramref name="work"/> in a write transaction taken at once (BEGIN IMMEDIATE), not at its
    /// first write: the judgement is made on what it reads, so the read must already hold the lock, or
    /// two hosts over one file could both judge the same open quest and both append a take (D47 §5).
    /// </summary>
    /// <remarks>
    /// <para>Within one host, the connection's gate holds every other request's work off until this one
    /// ends, its commands as well as its transactions (<see cref="ConnectionGate"/>, SQLITETX1).</para>
    ///
    /// <para>It COMMITS whenever the work returns, including a refusal that wrote nothing, and rolls back
    /// only on a throw. Cancellation is not one, once the transaction has begun: the wait honours the
    /// caller, and the work is handed a token nobody cancels (REV3).</para>
    /// </remarks>
    private Task<T> InTransactionAsync<T>(
        Func<SqliteTransaction, CancellationToken, Task<T>> work, CancellationToken ct) =>
        _db.InTransactionAsync(work, ct);

    /// <summary>A registered operation's verb, run in a write transaction (<see cref="InTransactionAsync{T}"/>) handed to it as its <see cref="QuestTx"/>.</summary>
    private Task<T> WriteAsync<T>(Func<QuestTx, Task<T>> write, CancellationToken ct) =>
        InTransactionAsync((transaction, inside) => write(new QuestTx(this, transaction, inside)), ct);

    /// <summary>The kinds of operation their modules register; a door reaches a module's verbs by its type.</summary>
    private static QuestOperations Operations => QuestOperations.Registered;

    internal SqliteCommand Command() => _db.Command();

    internal TimeProvider Time => _time;

    /// <summary>
    /// A handle derived from who asked, of whom, and for what — twelve hex characters (design §7).
    /// </summary>
    /// <remarks>
    /// Content-derived so publishing the same quest twice collides rather than multiplying — an agent
    /// that retries should not produce a second copy of the same ask, and the same ask made on two
    /// machines is one quest. A chain's step also derives from its PARENT (D65 §4): "Verify in the
    /// browser" is a title many chains will use, and a step that collided with an earlier quest of
    /// those words would quietly join somebody else's closed quest. The widening kept the hash, so an
    /// id from before it is exactly the first six characters of the same ask's id now. A quest to
    /// LANES widens the same way (D115 §2.2), only when there are some, so every quest to a whole
    /// repository keeps its id and a lane's ask is not the repository's. A workflow request's key widens it the same way (INTEGRATE1p,
    /// R5's C32), only when there is one, so distinct runs or nodes make distinct quests whatever their words.
    /// </remarks>
    internal static string MakeId(
        string from, string to, string title, string? parent = null, IReadOnlyList<string>? lanes = null, string? request = null) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(
                    $"{from}->{to}:{title.Trim()}" + (parent is null ? "" : $"<-{parent}")
                    + (lanes is { Count: > 0 } ? $"@{string.Join('+', lanes.Order(StringComparer.Ordinal))}" : "")
                    + (request is null ? "" : $"#{request}"))))[..IdLength]
            .ToLowerInvariant();

    /// <summary>Publish a quest. Returns the existing one unchanged if it was already asked.</summary>
    /// <param name="workspace">
    /// The circle both sides share — decided by <see cref="QuestExchange"/>, which is where the
    /// same-workspace clause lives. The store holds state; it does not judge who may ask whom.
    /// </param>
    /// <param name="links">Addresses the quest carries, already judged by the exchange.</param>
    /// <param name="attachments">Files the quest carries, by name — the bytes are never this store's.</param>
    /// <param name="then">The chain after this quest (D65 §4), already judged by the exchange.</param>
    /// <param name="publishedBy">The session whose connector published it, when one did (SESS1).</param>
    /// <param name="lanes">The lanes of <paramref name="to"/> it addresses, already judged by the exchange (D115 §2.2).</param>
    /// <param name="requirements">What the person requires, already judged by the exchange against their words (DRIFT1c).</param>
    /// <param name="shortTitle">The publisher's short title, already judged by the exchange (SESSUX1j); null for none.</param>
    /// <param name="review">The chain's review choice, already judged by the exchange (REVIEWENV1b); null for none.</param>
    /// <param name="setUpIn">A set-up step's environment, already judged by the exchange (REVIEWENV1b); null for any other quest.</param>
    /// <param name="parent">
    /// The quest a set-up step follows, when a door publishes one after its chain's close (REVIEWENV1b, D149 point 1): its id
    /// derives from it as a chain step's does, so pressing twice is one step. Null for every other publish.
    /// </param>
    public async Task<Quest> PublishAsync(
        string from, string to, string title, string body, DateTimeOffset now,
        string? workspace = null,
        IReadOnlyList<string>? links = null,
        IReadOnlyList<QuestAttachment>? attachments = null,
        IReadOnlyList<QuestStep>? then = null,
        CancellationToken ct = default,
        string? publishedBy = null,
        IReadOnlyList<string>? lanes = null,
        IReadOnlyList<QuestRequirement>? requirements = null,
        string? shortTitle = null,
        QuestReview? review = null,
        string? setUpIn = null,
        string? parent = null,
        string? weight = null,
        IReadOnlyList<string>? paths = null,
        string? request = null)
    {
        return await WriteAsync(
            tx => Operations.Of<QuestPublishOperation>().PublishAsync(
                tx, from, to, title, body, now, workspace, links, attachments, then, publishedBy, lanes, requirements, shortTitle, review,
                setUpIn, parent, weight, paths, request),
            ct).ConfigureAwait(false);
    }

    /// <summary>The lanes a publish addresses, as its id and its record hold them: once each, sorted.</summary>
    internal static List<string> Sorted(IReadOnlyList<string>? lanes) =>
        (lanes ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// The id these words would publish under, when this machine forgot that quest (HIST1b, D153 point 3), at either
    /// width an id has had; null when it did not. The exchange asks it before a publish, so the same words asked again
    /// are refused here rather than making a fresh quest the remote, which still holds the closed one, refuses for good (H5).
    /// </summary>
    internal async Task<string?> ForgottenIdAsync(
        string from, string to, string title, IReadOnlyList<string>? lanes, CancellationToken ct, string? request = null)
    {
        // A request's key widens the id as it did at its publish (INTEGRATE1p), and such an id never had the shorter width.
        var id = MakeId(from, to, title, lanes: Sorted(lanes), request: request);
        foreach (var held in request is null ? new[] { id, id[..LegacyIdLength] } : [id])
        {
            if (await ForgottenAsync(held, ct).ConfigureAwait(false)) return held;
        }

        return null;
    }

    /// <summary>
    /// Append one operation, stamped with this machine and its next sequence number — counted inside
    /// the caller's write transaction, so two hosts over one file cannot take the same number.
    /// </summary>
    /// <remarks>
    /// The next number is one past the larger of the log's highest for this machine and the store's mark (HIST1a),
    /// which the insert's trigger then moves. The log alone goes back when its newest rows are removed, and the number
    /// handed out again is one a remote already holds: it answers a push of it as a retry, and a fetch brings its own
    /// operation back under it, so the new move is lost without an error (H1).
    /// </remarks>
    internal async Task<QuestOperation> AppendAsync(
        string quest, QuestOperationKind kind, DateTimeOffset at, string? note, Quest? published,
        SqliteTransaction transaction, CancellationToken ct, QuestOperationRef? dismisses = null,
        IReadOnlyList<QuestAnswer>? answers = null, bool whileOpen = false, QuestEvidenceVerdict? evidence = null,
        QuestSetUp? setUp = null, QuestReviewVerdict? review = null, string? noteParts = null, QuestClose? close = null,
        QuestOperationRef? submission = null, string? session = null, QuestOperationRef? forward = null,
        QuestWorkflowChoice? choice = null)
    {
        // Each field kept only on the kinds that carry it, so the row written and the operation handed back read the same; a publish
        // carries the quest as asked, open from this moment.
        var operation = new QuestOperation(
            quest, kind, Machine, 0, at, note,
            published is null
                ? null
                : published with { Status = QuestStatus.Open, Note = null, NoteParts = null, Filed = at, Updated = at, Conflicts = [] },
            Dismisses: dismisses,
            Answers: answers is { Count: > 0 } ? answers : null,
            WhileOpen: whileOpen && JudgedOpen(kind),
            Evidence: kind == QuestOperationKind.Evidenced ? evidence : null,
            SetUp: kind == QuestOperationKind.SetUp ? setUp : null,
            Verdict: kind == QuestOperationKind.Verdict ? review : null,
            // Parts only beside a note, as the store keeps them (QUESTCLOSE1b1): what it reads back is what it wrote.
            NoteParts: note is null ? null : NoteParts.Normalize(NoteParts.Element(noteParts)),
            Close: kind == QuestOperationKind.Done ? close : null,
            Submission: Carries(kind) ? submission : null,
            Session: KeepsSession(kind) ? session : null,
            Forward: kind == QuestOperationKind.Dismissed ? forward : null,
            Choice: kind == QuestOperationKind.Chosen ? choice : null);

        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO quest_log (quest, kind, machine, sequence, at, payload)
            VALUES ($quest, $kind, $machine,
                    (SELECT MAX(
                       COALESCE((SELECT MAX(sequence) FROM quest_log WHERE machine = $machine), 0),
                       COALESCE((SELECT sequence FROM quest_machine WHERE one = 1 AND id = $machine), 0)) + 1),
                    $at, $payload)
            RETURNING sequence
            """;
        command.Parameters.AddWithValue("$quest", quest);
        command.Parameters.AddWithValue("$kind", KindText(kind));
        command.Parameters.AddWithValue("$machine", Machine);
        command.Parameters.AddWithValue("$at", Moments.Write(at));
        command.Parameters.AddWithValue("$payload", PayloadJson(operation));
        return operation with { Sequence = Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false)) };
    }

    /// <summary>The kinds that name a submission (FABRIC-CLOSE1c): a reopen, and a rebind.</summary>
    internal static bool Names(QuestOperationKind kind) => kind is QuestOperationKind.Reopened or QuestOperationKind.Rebound;

    /// <summary>
    /// The kinds that keep the session that said them through its connector: a ready (FABRIC-CLOSE1c4), and a done and a decline
    /// (HANDBACK1, D219), each the act a handback is written at. On any other kind it is never written, kept or read; on these it is
    /// written only where a session said it, so a close through any other door, and every one from before, reads as it did.
    /// </summary>
    internal static bool KeepsSession(QuestOperationKind kind) =>
        kind is QuestOperationKind.Submitted or QuestOperationKind.Done or QuestOperationKind.Declined;

    /// <summary>
    /// The kinds a submission may ride on: the two that name one (<see cref="Names"/>), and a take, which carries its step's source as it
    /// found it (FABRIC-CLOSE1c18), only where it found one. On any other kind it is never written, kept or read.
    /// </summary>
    internal static bool Carries(QuestOperationKind kind) => Names(kind) || kind == QuestOperationKind.Taken;

    /// <summary>
    /// The kinds a <c>whileOpen</c> mark may carry: a decline (PAUSE1c), and a publication's rebind (FABRIC-CLOSE1c15). On any other
    /// kind it is never written, kept or read.
    /// </summary>
    internal static bool JudgedOpen(QuestOperationKind kind) => kind is QuestOperationKind.Declined or QuestOperationKind.Rebound;

    /// <summary>
    /// A quest's history, in the order it replays (design §8): what a remote accepted, by its number,
    /// then what this machine has not pushed, in the order it was made. Empty for an unknown id.
    /// </summary>
    public Task<IReadOnlyList<QuestOperation>> HistoryAsync(string id, CancellationToken ct = default) =>
        InGateAsync(() => HistoryAsync(id, transaction: null, ct), ct);

    internal async Task<IReadOnlyList<QuestOperation>> HistoryAsync(
        string id, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        // None handed in keeps the one the command was made in: the connection hands a new command the transaction it
        // has open, and a caller holding one without its object (HistoryDesk's, HistoryWithinAsync) must not lose it.
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = $"""
            SELECT {OperationColumns} FROM quest_log WHERE quest = $id
            ORDER BY remote IS NULL, remote, position
            """;
        command.Parameters.AddWithValue("$id", id);

        var history = new List<QuestOperation>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) history.Add(ReadOperation(reader));
        return history;
    }

    /// <inheritdoc cref="QuestTakeOperation.MoveAsync"/>
    public Task<QuestMove> MoveAsync(
        string id, QuestStatus status, string? note, DateTimeOffset now, CancellationToken ct = default,
        IReadOnlyList<QuestAnswer>? answers = null, bool whileOpen = false, string? noteParts = null, bool byAgent = false,
        QuestTaker? taker = null, string? session = null)
    {
        // FABRIC-CLOSE1w: `now` is the caller's, taken before this queued for the gate and the write, so the take measures that wait.
        var asked = _time.GetTimestamp();
        return WriteAsync(
            tx => Operations.Of<QuestTakeOperation>().MoveAsync(
                tx, id, status, note, now, asked, answers, whileOpen, noteParts, byAgent, taker, session),
            ct);
    }

    /// <inheritdoc cref="QuestBindOperation.BindAsync"/>
    public Task<QuestMove> BindAsync(string id, string graph, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestBindOperation>().BindAsync(tx, id, graph, now), ct);

    /// <inheritdoc cref="QuestBindOperation.BindAtTakeAsync"/>
    public Task<QuestMove> BindAtTakeAsync(string id, string graph, string session, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestBindOperation>().BindAtTakeAsync(tx, id, graph, session, now), ct);

    /// <inheritdoc cref="QuestSubmitOperation.SubmitAsync"/>
    public Task<QuestMove> SubmitAsync(
        string id, string? note, IReadOnlyList<QuestAnswer> answers, DateTimeOffset now, CancellationToken ct = default,
        string? session = null) =>
        WriteAsync(tx => Operations.Of<QuestSubmitOperation>().SubmitAsync(tx, id, note, answers, now, session), ct);

    /// <inheritdoc cref="QuestReopenOperation.ReopenAsync"/>
    public Task<QuestReopening> ReopenAsync(
        string id, QuestOperationRef submission, string? note, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestReopenOperation>().ReopenAsync(tx, id, submission, note, now), ct);

    /// <inheritdoc cref="QuestReopenOperation.ContinueAsync"/>
    public Task<QuestReopening> ContinueAsync(string id, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestReopenOperation>().ContinueAsync(tx, id, now), ct);

    /// <inheritdoc cref="QuestCloseOperation.CloseAsync"/>
    public Task<QuestClosing> CloseAsync(
        string at, IReadOnlyList<(string Id, QuestOperationRef Submission)> named, QuestClose close, DateTimeOffset now,
        CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestCloseOperation>().CloseAsync(tx, at, named, close, now), ct);

    /// <inheritdoc cref="QuestCloseOperation.CloseOutcomeAsync"/>
    public Task<QuestClosing> CloseOutcomeAsync(
        IReadOnlyList<(string Id, QuestOperationRef Submission)> named, QuestClose close, DateTimeOffset now,
        CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestCloseOperation>().CloseOutcomeAsync(tx, named, close, now), ct);

    /// <inheritdoc cref="QuestAcceptOperation.AcceptAsync"/>
    public Task<QuestMove> AcceptAsync(string id, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestAcceptOperation>().AcceptAsync(tx, id, now), ct);

    /// <inheritdoc cref="QuestSetUpOperation.SetUpAsync"/>
    public Task<QuestMove> SetUpAsync(string id, QuestSetUp setUp, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestSetUpOperation>().SetUpAsync(tx, id, setUp, now), ct);

    /// <inheritdoc cref="QuestVerdictOperation.VerdictAsync"/>
    public Task<QuestMove> VerdictAsync(string id, QuestReviewVerdict verdict, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestVerdictOperation>().VerdictAsync(tx, id, verdict, now), ct);

    /// <inheritdoc cref="QuestChoiceOperation.ChooseAsync"/>
    public Task<QuestMove> ChooseAsync(string id, QuestWorkflowChoice choice, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestChoiceOperation>().ChooseAsync(tx, id, choice, now), ct);

    /// <summary>
    /// Every quest of the <c>follows</c> chain <paramref name="id"/> is on (D149 point 1; REVIEWENV1b3): its first quest, found by
    /// walking <c>parent</c> up, and every quest that follows it at any depth, a chain step its close published or a set-up step
    /// a door published after it. Empty for an unknown id. The late set-up door judges a repository's part of the chain on it.
    /// </summary>
    public Task<IReadOnlyList<Quest>> ChainAsync(string id, CancellationToken ct = default) =>
        _db.RunAsync<IReadOnlyList<Quest>>(async () =>
        {
            async Task<Quest?> One(string named)
            {
                await using var command = _db.Command();
                command.CommandText = "SELECT * FROM quests WHERE id = $id";
                command.Parameters.AddWithValue("$id", named);
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
            }

            async Task<List<Quest>> Following(string parent)
            {
                await using var command = _db.Command();
                command.CommandText = "SELECT * FROM quests WHERE parent = $parent ORDER BY filed";
                command.Parameters.AddWithValue("$parent", parent);
                var steps = new List<Quest>();
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false)) steps.Add(Read(reader));
                return steps;
            }

            // Up to the first quest; a parent seen twice, which no chain makes, ends the walk rather than looping.
            if (await One(id).ConfigureAwait(false) is not { } first) return [];
            var seen = new HashSet<string>(StringComparer.Ordinal) { first.Id };
            while (first.Parent is { } parent && seen.Add(parent) && await One(parent).ConfigureAwait(false) is { } above) first = above;

            var chain = new List<Quest> { first };
            var visited = new HashSet<string>(StringComparer.Ordinal) { first.Id };
            for (var next = 0; next < chain.Count; next++)
            {
                foreach (var step in await Following(chain[next].Id).ConfigureAwait(false))
                {
                    if (visited.Add(step.Id)) chain.Add(step);
                }
            }

            return chain;
        }, ct);

    /// <inheritdoc cref="QuestEvidenceOperation.EvidenceAsync"/>
    public Task<QuestMove> EvidenceAsync(string id, QuestEvidenceVerdict verdict, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestEvidenceOperation>().EvidenceAsync(tx, id, verdict, now), ct);

    /// <inheritdoc cref="QuestWaitOperation.WaitAsync"/>
    public Task<QuestMove> WaitAsync(string id, string on, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestWaitOperation>().WaitAsync(tx, id, on, now), ct);

    /// <inheritdoc cref="QuestDismissOperation.DismissAsync"/>
    public Task<QuestDismissal> DismissAsync(
        string id, string? machine, long? sequence, DateTimeOffset now, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestDismissOperation>().DismissAsync(tx, id, machine, sequence, now), ct);

    /// <inheritdoc cref="QuestDismissOperation.BringForwardAsync"/>
    public Task<QuestBringingForward> BringForwardAsync(
        string id, QuestOperationRef conflict, DateTimeOffset now, CancellationToken ct = default, bool running = false) =>
        WriteAsync(tx => Operations.Of<QuestDismissOperation>().BringForwardAsync(tx, id, conflict, now, running), ct);

    /// <summary>This machine's bring-forwards on a quest (SYNCMERGE1b0), in the order they were made.</summary>
    private async Task<IReadOnlyList<QuestForwardPress>> PressesAsync(string id, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT conflict_machine, conflict_sequence, take_machine, take_sequence, at FROM quest_forwards
            WHERE quest = $quest ORDER BY at, take_sequence
            """;
        command.Parameters.AddWithValue("$quest", id);
        var presses = new List<QuestForwardPress>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            presses.Add(new QuestForwardPress(
                new QuestOperationRef(reader.GetString(0), reader.GetInt64(1)), new QuestOperationRef(reader.GetString(2), reader.GetInt64(3)),
                Moments.ReadRequired(reader.GetString(4))));
        }

        return presses;
    }

    /// <summary>
    /// Who holds a quest's step and how each take that lost on it stands or ended (SYNCMERGE1b0, <see cref="QuestLog.Read"/>), with this
    /// machine's own bring-forwards: what the quest's answer and the claim's say of them, and what the session ledger carries a
    /// stand-down on by. <see cref="QuestTakeReading.None"/> for a quest this store does not hold.
    /// </summary>
    public Task<QuestTakeReading> ReadTakesAsync(string id, CancellationToken ct = default) =>
        InGateAsync(async () => QuestLog.Read(
            await HistoryAsync(id, transaction: null, ct).ConfigureAwait(false), await PressesAsync(id, ct).ConfigureAwait(false)), ct);

    /// <summary>
    /// <see cref="ReadTakesAsync(string, CancellationToken)"/> for each of <paramref name="quests"/> with something to read: taken, or one
    /// whose log holds a conflict, settled or not. By id; a quest with neither is absent, and reads as <see cref="QuestTakeReading.None"/>.
    /// A list answers many quests, so only those have their log read.
    /// </summary>
    public Task<IReadOnlyDictionary<string, QuestTakeReading>> ReadTakesAsync(IEnumerable<Quest> quests, CancellationToken ct = default) =>
        InGateAsync<IReadOnlyDictionary<string, QuestTakeReading>>(async () =>
        {
            var contested = new HashSet<string>(StringComparer.Ordinal);
            await using (var command = _db.Command())
            {
                command.CommandText = $"SELECT DISTINCT quest FROM quest_log WHERE kind = '{KindText(QuestOperationKind.Conflict)}'";
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false)) contested.Add(reader.GetString(0));
            }

            var read = new Dictionary<string, QuestTakeReading>(StringComparer.Ordinal);
            foreach (var quest in quests.Where(quest => quest.Status == QuestStatus.Taken || contested.Contains(quest.Id)))
            {
                read[quest.Id] = QuestLog.Read(
                    await HistoryAsync(quest.Id, transaction: null, ct).ConfigureAwait(false),
                    await PressesAsync(quest.Id, ct).ConfigureAwait(false));
            }

            return read;
        }, ct);

    /// <summary>
    /// Delete an open quest (D95), judged against the replayed history inside the write, as every move
    /// is: only an open quest goes. Whether anything outside this store stands on it — a session, a
    /// waiting quest — is the exchange's judgement, made before this is asked.
    /// </summary>
    /// <param name="travels">
    /// Whether the quest may have left this machine: its circle has a remote and its receiver is joined.
    /// A quest that may have, or whose history a remote numbered, is tombstoned — a <see cref="QuestOperationKind.Deleted"/>
    /// operation the next pass carries, because a copy elsewhere would otherwise come back on the next
    /// fetch. Any other quest simply goes, its history with it, since nothing anywhere holds a copy.
    /// </param>
    public Task<QuestDeletion> DeleteAsync(string id, DateTimeOffset now, bool travels, CancellationToken ct = default) =>
        WriteAsync(tx => Operations.Of<QuestDeleteOperation>().DeleteAsync(tx, id, now, travels), ct);

    // ——— Clearing finished history from this machine (HIST1b, D153 point 3, history-clearing design §3).

    /// <summary>
    /// Remove one quest from this machine: its row and its whole log together, since a row with no log is given a fresh
    /// pending history at the next open and a log with no row lets a publish append to it (H3). Where a remote numbered any
    /// of its operations it is <b>forgotten</b>: <c>quest_forgotten</c> keeps its id, and the fetch skips it from then on
    /// (<see cref="IntegrateAsync"/>), so no later move and no cursor at zero brings it back (H4). No operation is
    /// written, so nothing travels: the team's copy is untouched, and a new store fetches it whole.
    /// </summary>
    /// <remarks>
    /// Blind, like every write here, and opening no transaction of its own: <see cref="HistoryDesk"/> judges the unit and
    /// calls this inside the one transaction it clears the unit in, so the mark, the log and the row go together or not at
    /// all. The gate it takes is the one that transaction's work already holds (SQLITETX1).
    /// </remarks>
    /// <returns>Whether it was forgotten, rather than simply going because nothing of it was ever numbered.</returns>
    internal Task<bool> ForgetAsync(string id, DateTimeOffset at, CancellationToken ct) => _db.RunAsync<bool>(async () =>
    {
        bool numbered;
        await using (var probe = _db.Command())
        {
            probe.CommandText = "SELECT EXISTS (SELECT 1 FROM quest_log WHERE quest = $id AND remote IS NOT NULL)";
            probe.Parameters.AddWithValue("$id", id);
            numbered = Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false)) != 0;
        }

        await using var command = _db.Command();
        command.CommandText = (numbered ? "INSERT OR IGNORE INTO quest_forgotten (id, at) VALUES ($id, $at);" : "")
                              + "DELETE FROM quest_log WHERE quest = $id; DELETE FROM quest_forwards WHERE quest = $id; DELETE FROM quests WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$at", Moments.Write(at));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return numbered;
    }, ct);

    /// <summary>Whether this machine forgot the quest (HIST1b): it cleared it after a remote numbered it.</summary>
    public Task<bool> ForgottenAsync(string id, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM quest_forgotten WHERE id = $id)";
        command.Parameters.AddWithValue("$id", id);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct).ConfigureAwait(false)) != 0;
    }, ct);

    /// <summary>Every quest this machine forgot (HIST1b): what the quest fetch and the session fetch skip.</summary>
    public Task<IReadOnlySet<string>> ForgottenAsync(CancellationToken ct = default) =>
        InGateAsync(() => ForgottenAsync(transaction: null, ct), ct);

    private async Task<IReadOnlySet<string>> ForgottenAsync(SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = "SELECT id FROM quest_forgotten";
        var forgotten = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) forgotten.Add(reader.GetString(0));
        return forgotten;
    }

    /// <summary>
    /// A quest's history read inside a transaction the caller already holds — <see cref="HistoryDesk"/>'s, which judges a
    /// unit again where it clears it. The same read as <see cref="HistoryAsync(string, CancellationToken)"/>: the gate is
    /// reentrant within the work that holds it (SQLITETX1), and the read joins that work's transaction.
    /// </summary>
    internal Task<IReadOnlyList<QuestOperation>> HistoryWithinAsync(string id, CancellationToken ct) =>
        HistoryAsync(id, ct);

    /// <summary>
    /// The taken quests waiting on <paramref name="question"/> (D79) — what keeps a question from being
    /// deleted: deleted, the quests waiting on it would wait on nothing for good.
    /// </summary>
    public Task<IReadOnlyList<Quest>> WaitingOnAsync(string question, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<Quest>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT * FROM quests WHERE awaits = $question AND status = 'Taken' ORDER BY filed";
        command.Parameters.AddWithValue("$question", question);

        var quests = new List<Quest>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) quests.Add(Read(reader));
        return quests;
    }, ct);

    /// <summary>Every question a taken quest waits on (D79) — what <see cref="WaitingOnAsync"/> answers, for a whole list at once.</summary>
    public Task<IReadOnlySet<string>> AwaitedAsync(CancellationToken ct = default) => _db.RunAsync<IReadOnlySet<string>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT DISTINCT awaits FROM quests WHERE awaits IS NOT NULL AND status = 'Taken'";

        var awaited = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) awaited.Add(reader.GetString(0));
        return awaited;
    }, ct);

    // ——— A machine's half of the sync (D68 §3, design §8).

    /// <summary>The last number this machine fetched from a workspace's remote — zero before its first fetch.</summary>
    public Task<long> CursorAsync(string workspace, CancellationToken ct = default) =>
        InGateAsync(() => CursorAsync(workspace, transaction: null, ct), ct);

    private async Task<long> CursorAsync(string workspace, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        // None handed in keeps the one the command was made in, as HistoryAsync's does.
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = "SELECT number FROM quest_cursor WHERE workspace = $workspace";
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is { } number and not DBNull
            ? Convert.ToInt64(number)
            : 0;
    }

    /// <summary>
    /// Take what a workspace's remote accepted, in its order, and rebase what this machine has not
    /// pushed on top — through the one table. Answers the cursor it now stands at and every move that
    /// became a conflict on the way.
    /// </summary>
    /// <param name="workspace">The circle whose remote these came from: what a publish among them is filed under here (SYNC0a).</param>
    /// <param name="fetched">The remote's operations, each carrying its number.</param>
    /// <param name="through">The last number the fetch covered; the cursor moves there.</param>
    /// <remarks>
    /// An operation this machine already holds — its own, pushed and fetched back — takes the number it
    /// was given; any other is kept under the machine that made it. Then every quest the fetch touched
    /// is replayed: accepted operations by number, then pending ones. A pending move that no longer
    /// applies becomes a <see cref="QuestOperationKind.Conflict"/> and is never dropped (design §5).
    /// <para>An operation on a quest this machine forgot (<see cref="ForgetAsync"/>) is passed over, and the cursor still
    /// moves past it (HIST1b): a closed quest still takes a conflict or a dismissal from another machine, and kept, either
    /// would make half a quest here; a cursor back at zero would bring the whole of it.</para>
    /// </remarks>
    public Task<QuestIntegration> IntegrateAsync(
        string workspace, IReadOnlyList<QuestOperation> fetched, long through, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var circle = Workspaces.Normalize(workspace);
            var forgotten = await ForgottenAsync(transaction, inside).ConfigureAwait(false);
            var touched = new List<string>();
            foreach (var operation in fetched
                         .Where(o => o.Number is not null && !forgotten.Contains(o.Quest))
                         .OrderBy(o => o.Number))
            {
                if (await _sync.PositionOfAsync(operation.Machine, operation.Sequence, transaction, inside).ConfigureAwait(false)
                    is { } held)
                {
                    await _sync.NumberAsync(held, operation.Number!.Value, transaction, inside).ConfigureAwait(false);
                }
                else
                {
                    var filed = operation.Published is null
                        ? operation
                        : operation with { Published = operation.Published with { Workspace = circle } };
                    await _sync.KeepAsync(filed, operation.Number, transaction, inside).ConfigureAwait(false);
                }

                if (!touched.Contains(operation.Quest, StringComparer.Ordinal)) touched.Add(operation.Quest);
            }

            // A stray an older build left pending on a lost take (WAITCLAIM4) sits on a quest no fetch touches once the
            // take's conflict has come back, so it is rebased whether or not anything arrived for it.
            foreach (var quest in await _sync.StrayQuestsAsync(circle, transaction, inside).ConfigureAwait(false))
            {
                if (!forgotten.Contains(quest) && !touched.Contains(quest, StringComparer.Ordinal)) touched.Add(quest);
            }

            var conflicts = new List<QuestOperation>();
            foreach (var quest in touched)
            {
                conflicts.AddRange(await _sync.RebaseAsync(quest, transaction, inside).ConfigureAwait(false));
            }

            var cursor = Math.Max(await CursorAsync(circle, transaction, inside).ConfigureAwait(false), through);
            await using (var command = _db.Command())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO quest_cursor (workspace, number) VALUES ($workspace, $number)
                    ON CONFLICT (workspace) DO UPDATE SET number = excluded.number
                    """;
                command.Parameters.AddWithValue("$workspace", circle);
                command.Parameters.AddWithValue("$number", cursor);
                await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            return new QuestIntegration(cursor, conflicts);
        }, ct);

    /// <summary>
    /// A follow-up published only by a close that has now lost (D65 §4): it was the chain moving on,
    /// and the chain did not move. Forgotten only while every operation of it is this machine's and none was
    /// pushed; one another machine holds, or published itself on its own close, stays.
    /// </summary>
    /// <remarks>
    /// FABRIC-CLOSE1e2: what this machine did on it goes with it, as a lost submission's step's does (<see cref="ForgetStepsAsync"/>): a
    /// take of it becomes a conflict naming the close that went, so its claim reads lost and its session is stopped, where it once stood
    /// and synced on its own, numbered on a done that lost, until a person declined it. A remote holds no such follow-up while its parent
    /// is not done there (<see cref="AwaitsItsSourceAsync"/>), so the raced order reaches this too.
    /// </remarks>
    /// <param name="published">What published it, as its take's conflict names it: the parent's close, or the yes or verdict that let it go.</param>
    /// <returns>The takes rewritten, as the rebase reports its conflicts.</returns>
    internal Task<IReadOnlyList<QuestOperation>> ForgetFollowUpsAsync(
        string parent, bool takeLost, string published, bool reopened, SqliteTransaction transaction, CancellationToken ct) =>
        ForgetStepsAsync(parent, takeLost, reopened, transaction, ct, published);

    /// <summary>
    /// The steps a lost submission published (FABRIC-CLOSE1e, §E5), with what this machine did on them since, and the steps they
    /// published in turn: each grew from a submission that no longer stands, so none was asked. A step is forgotten only while
    /// every operation of it is this machine's and none was pushed; one a remote holds is every machine's, and stays. A lost close's
    /// follow-ups go the same way (<see cref="ForgetFollowUpsAsync"/>, FABRIC-CLOSE1e2).
    /// </summary>
    /// <remarks>
    /// Where this machine took a step, its take is not forgotten but rewritten as a conflict, which the claim reads as lost
    /// (<see cref="QuestLog.Claim"/>), so the driver stops the step's session as it stops any session whose take lost (D69). Its
    /// publication gone, the conflict is on a quest no list shows and no push carries, as one on a quest deleted first is (D95),
    /// and its note says why: the parent's submission went. Should the winner's chain publish the same step later (its id comes
    /// from the parent and the step), the conflict stands on it and goes up, as any take that lost is shown on the winner's quest.
    /// <para>Where the parent's take lost to the person's reopen (<paramref name="reopened"/>, FABRIC-CLOSE1c22), the conflict says so in
    /// its words and in <see cref="QuestOperation.LostToTheReopen"/>: the step's history holds only the conflict, so the claim's answer
    /// (<see cref="LostToTheReopenAsync"/>) could not read the reopen from it, and the driver would stop the session in another
    /// machine's words.</para>
    /// </remarks>
    /// <param name="published">What of the parent published it, as its take's conflict names it: its submission, or its close.</param>
    /// <returns>The takes rewritten, as the rebase reports its conflicts.</returns>
    private async Task<IReadOnlyList<QuestOperation>> ForgetStepsAsync(
        string parent, bool takeLost, bool reopened, SqliteTransaction transaction, CancellationToken ct, string published = "submission")
    {
        var children = new List<string>();
        await using (var command = _db.Command())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT id FROM quests WHERE parent = $parent";
            command.Parameters.AddWithValue("$parent", parent);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) children.Add(reader.GetString(0));
        }

        var told = new List<QuestOperation>();
        foreach (var child in children)
        {
            var history = await HistoryAsync(child, transaction, ct).ConfigureAwait(false);
            if (history is not [{ Kind: QuestOperationKind.Published }, ..]
                || history.Any(operation => operation.Number is not null || operation.Machine != Machine))
            {
                continue;
            }

            told.AddRange(await ForgetStepsAsync(child, takeLost, reopened, transaction, ct, published).ConfigureAwait(false));
            foreach (var operation in history)
            {
                var position = (await _sync.PositionOfAsync(operation.Machine, operation.Sequence, transaction, ct).ConfigureAwait(false))!.Value;
                if (operation.Kind != QuestOperationKind.Taken)
                {
                    await _sync.ForgetOperationAsync(position, transaction, ct).ConfigureAwait(false);
                    continue;
                }

                var lost = operation with
                {
                    Kind = QuestOperationKind.Conflict, Attempted = QuestStatus.Taken,
                    Note = StepWentWith(parent, takeLost, operation.Note, reopened, published),
                    NoteParts = null, WhileOpen = false, Close = null, Submission = null, LostToTheReopen = reopened,
                };
                await _sync.RewriteAsync(position, lost, transaction, ct).ConfigureAwait(false);
                told.Add(lost);
            }

            await _sync.DropCacheAsync(child, transaction, ct).ConfigureAwait(false);
        }

        return told;
    }

    /// <summary>
    /// A lost step's take's note as it was given, then why the step went (FABRIC-CLOSE1e): the parent's submission that published
    /// it no longer stands, with the parent's take or to another machine's move on the parent. A parent's take lost to the person's
    /// reopen (<paramref name="reopened"/>, FABRIC-CLOSE1c22) has no winner, so it says the reopen, as the take's refusals do
    /// (<see cref="QuestExchange.LostTake"/>). A step a close published names that close instead (<paramref name="published"/>,
    /// FABRIC-CLOSE1e2).
    /// </summary>
    internal static string StepWentWith(string parent, bool takeLost, string? note, bool reopened = false, string published = "submission")
    {
        var said = $"This quest was the next step of #{parent}'s {published}, which "
                   + (!takeLost ? $"lost to another machine's move on #{parent}"
                      : reopened ? $"went with this machine's take of #{parent}: the person's reopen of the work it grew from reached the "
                                   + "remote first"
                      : $"went with this machine's take of #{parent}: another machine's take reached the remote first")
                   + ". The step went with it, so its work is not wanted.";
        return string.IsNullOrWhiteSpace(note) ? said : $"{note.TrimEnd()} {said}";
    }

    /// <summary>
    /// A lost step's take's note as it was given, then why it lost (FABRIC-CLOSE1c25): the step was published anew on other work,
    /// from the submission another machine made of <paramref name="parent"/>, so a take made on the work that went is not wanted.
    /// </summary>
    internal static string StepPublishedAnew(string parent, string? note)
    {
        var said = $"This quest was the next step of #{parent}'s submission, and was published anew on other work, from the submission "
                   + $"another machine made of #{parent}. This machine's take of it, made on the work that went, is not wanted.";
        return string.IsNullOrWhiteSpace(note) ? said : $"{note.TrimEnd()} {said}";
    }

    /// <summary>
    /// Where THIS machine's claim on a quest stands (D68 §4, D69): held once a remote numbered its take,
    /// unconfirmed while the take is only here, lost once the rebase made it a conflict — and none when
    /// this machine never took it. A take on a machine with no remote is never numbered, so it stays
    /// unconfirmed, which nothing acts on.
    /// </summary>
    public Task<QuestClaim> ClaimAsync(string id, CancellationToken ct = default) =>
        InGateAsync(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = $"SELECT {OperationColumns} FROM quest_log WHERE quest = $id AND machine = $machine";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$machine", Machine);

            var mine = new List<QuestOperation>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) mine.Add(ReadOperation(reader));
            return QuestLog.Claim(mine, Machine);
        }, ct);

    /// <summary>
    /// Whether THIS machine's claim on a quest reads lost because its take lost to the person's reopen of the work the step grew from
    /// (FABRIC-CLOSE1c18, <see cref="QuestLog.LostToTheReopen"/>), not to another machine's take: what the claim's answer says beside
    /// it, so the driver stops the session in words that are true. False where the claim does not read lost.
    /// </summary>
    public async Task<bool> LostToTheReopenAsync(string id, CancellationToken ct = default)
    {
        if (await ClaimAsync(id, ct).ConfigureAwait(false) != QuestClaim.Lost) return false;
        return QuestLog.LostToTheReopen(await HistoryAsync(id, ct).ConfigureAwait(false), Machine);
    }

    /// <summary>
    /// How a circle's pass ended (SYNC6a). A pass that reached the remote moves <see cref="QuestStanding.Synced"/>;
    /// one that hit a wall keeps it and names the wall, because when the circle last synced and why
    /// the last try did not are two facts.
    /// </summary>
    public Task RecordPassAsync(string workspace, QuestSyncReport report, DateTimeOffset at, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            await using var command = _db.Command();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO quest_passes (workspace, synced, tried, behind, problem, closure)
                VALUES ($workspace, $synced, $tried, $behind, $problem, $closure)
                ON CONFLICT (workspace) DO UPDATE SET
                  synced = COALESCE($synced, synced), tried = $tried, behind = $behind, problem = $problem, closure = $closure
                """;
            var circle = Workspaces.Normalize(workspace);
            command.Parameters.AddWithValue("$workspace", circle);
            command.Parameters.AddWithValue("$synced", report.Problem is null ? Moments.Write(at) : DBNull.Value);
            command.Parameters.AddWithValue("$tried", Moments.Write(at));
            // A list of quest ids, kept the way links are: a plain array of strings.
            command.Parameters.AddWithValue("$behind", LinksJson(report.Behind));
            command.Parameters.AddWithValue("$problem", (object?)report.Problem ?? DBNull.Value);
            // FABRIC-CLOSE1i: the undeclared machines, a plain array of ids; none where the pass heard no handshake, a wall's included.
            command.Parameters.AddWithValue("$closure", report.Closure is { } closure ? LinksJson(closure.Undeclared) : DBNull.Value);
            var written = await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            Heard(circle, report.Closure);
            return written;
        }, ct);

    /// <summary>
    /// The remote's handshake as <paramref name="workspace"/>'s last pass heard it (FABRIC-CLOSE1i): null where no pass has, a wall, or a
    /// remote before it. Read without the store's gate, so a take's own write can ask it: kept beside the row its pass recorded.
    /// </summary>
    public QuestClosure? ClosureOf(string workspace) =>
        _closures.TryGetValue(Workspaces.Normalize(workspace), out var closure) ? closure : null;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, QuestClosure> _closures = new(StringComparer.OrdinalIgnoreCase);

    private void Heard(string circle, QuestClosure? closure)
    {
        if (closure is null) _closures.TryRemove(circle, out _);
        else _closures[circle] = closure;
    }

    /// <summary>Each circle's handshake as its last pass recorded it (FABRIC-CLOSE1i), read as the store opens.</summary>
    private async Task LoadClosuresAsync(CancellationToken ct)
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT workspace, closure FROM quest_passes WHERE closure IS NOT NULL";
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            Heard(reader.GetString(0), new QuestClosure(ReadLinks(reader.GetString(1))));
        }
    }

    /// <summary>
    /// A remote's half of the workspace's handshake (FABRIC-CLOSE1i): what a push said of its machine. A push declaring
    /// <paramref name="declares"/> marks that machine declared; one declaring none, as a build before it pushes, marks every machine its
    /// operations name undeclared, so a machine run back to an older build is heard as one. A push declaring one machine says nothing
    /// of another its operations name. A machine it marks is counted again where a person had retired it (FABRIC-CLOSE1i2).
    /// </summary>
    public Task DeclaredAsync(string? declares, IReadOnlyList<QuestOperation> pushed, DateTimeOffset at, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            List<(string Machine, bool Closure)> marked = declares is { Length: > 0 } declared
                ? [(declared, true)]
                : [.. pushed.Select(o => o.Machine).Where(m => m.Length > 0).Distinct(StringComparer.Ordinal).Select(m => (m, false))];
            foreach (var (machine, closure) in marked)
            {
                await using var command = _db.Command();
                command.Transaction = transaction;
                command.CommandText = """
                    DELETE FROM quest_retired WHERE machine = $machine;
                    INSERT INTO quest_declared (machine, closure, at) VALUES ($machine, $closure, $at)
                    ON CONFLICT (machine) DO UPDATE SET closure = excluded.closure, at = excluded.at
                    """;
                command.Parameters.AddWithValue("$machine", machine);
                command.Parameters.AddWithValue("$closure", closure ? 1 : 0);
                command.Parameters.AddWithValue("$at", Moments.Write(at));
                await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            return marked.Count;
        }, ct);

    /// <summary>
    /// A remote's answer to the workspace's handshake (FABRIC-CLOSE1i): every machine it holds an operation or a push from, but itself,
    /// whose last push did not declare <c>closure</c>, ordered by id. A shared host holds one workspace by construction, so these are the
    /// workspace's machines as far as it has heard them; one that has never pushed has made nothing here to misread. A machine a person
    /// retired here is not counted until a push marks it again (FABRIC-CLOSE1i2).
    /// </summary>
    public Task<QuestClosure> ClosureAsync(CancellationToken ct = default) =>
        InGateAsync(async () =>
        {
            await using var command = _db.Command();
            command.CommandText = """
                SELECT machine FROM (SELECT DISTINCT machine FROM quest_log UNION SELECT machine FROM quest_declared)
                WHERE machine <> $self AND machine NOT IN (SELECT machine FROM quest_declared WHERE closure = 1)
                  AND machine NOT IN (SELECT machine FROM quest_retired)
                ORDER BY machine
                """;
            command.Parameters.AddWithValue("$self", Machine);
            var undeclared = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) undeclared.Add(reader.GetString(0));
            return new QuestClosure(undeclared);
        }, ct);

    /// <summary>
    /// A person's retirement of <paramref name="machine"/> at a remote (FABRIC-CLOSE1i2): a machine whose store was deleted, or that
    /// left the workspace, no longer counted by <see cref="ClosureAsync"/>, until a push marks it again (<see cref="DeclaredAsync"/>).
    /// False, and nothing kept, where the remote has heard nothing from it: no operation and no push, or the remote itself.
    /// </summary>
    public Task<bool> RetireMachineAsync(string machine, DateTimeOffset at, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            await using var command = _db.Command();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO quest_retired (machine, at)
                SELECT $machine, $at
                WHERE $machine <> $self
                  AND (EXISTS (SELECT 1 FROM quest_log WHERE machine = $machine) OR EXISTS (SELECT 1 FROM quest_declared WHERE machine = $machine))
                ON CONFLICT (machine) DO UPDATE SET at = excluded.at
                """;
            command.Parameters.AddWithValue("$machine", machine);
            command.Parameters.AddWithValue("$self", Machine);
            command.Parameters.AddWithValue("$at", Moments.Write(at));
            return await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false) > 0;
        }, ct);

    /// <summary>
    /// The remote's handshake as a retirement there answered it (FABRIC-CLOSE1i2), kept as <paramref name="workspace"/>'s last pass keeps
    /// what it heard, so a bind reads it at once rather than at the next pass. Nothing where no pass has recorded the circle yet: its
    /// first pass hears the remote for itself.
    /// </summary>
    public Task HeardClosureAsync(string workspace, QuestClosure closure, CancellationToken ct = default) =>
        InTransactionAsync(async (transaction, inside) =>
        {
            var circle = Workspaces.Normalize(workspace);
            await using var command = _db.Command();
            command.Transaction = transaction;
            command.CommandText = "UPDATE quest_passes SET closure = $closure WHERE workspace = $workspace";
            command.Parameters.AddWithValue("$workspace", circle);
            command.Parameters.AddWithValue("$closure", LinksJson(closure.Undeclared));
            var written = await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            if (written > 0) Heard(circle, closure);
            return written;
        }, ct);

    // ——— A remote's half.

    /// <summary>
    /// A read inside the connection's gate, as every command here is (SQLITETX1): it never meets another
    /// request's open transaction, so a pending operation is never read from one that then rolled back.
    /// </summary>
    private Task<T> InGateAsync<T>(Func<Task<T>> read, CancellationToken ct) => _db.RunAsync(read, ct);

    /// <summary>The columns <see cref="ReadOperation"/> reads, in its order.</summary>
    internal const string OperationColumns =
        "quest_log.quest, quest_log.kind, quest_log.machine, quest_log.sequence, quest_log.at, quest_log.payload, quest_log.remote";

    public Task<Quest?> FindAsync(string id, CancellationToken ct = default) =>
        InGateAsync(() => FindAsync(id, transaction: null, ct), ct);

    internal async Task<Quest?> FindAsync(string id, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        // None handed in keeps the one the command was made in, as HistoryAsync's does.
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = "SELECT * FROM quests WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>
    /// Quests addressed to one repository, or every quest when none is named.
    /// </summary>
    /// <remarks>
    /// Open and taken first: what is outstanding is the question worth asking, and a finished queue
    /// pushing live work off the end is how a list stops being read. A done that a departure holds for the
    /// person's yes is outstanding too (DRIFT1d): it waits on someone, and what follows it waits with it — the
    /// driver keeps a quest waiting on it waiting because it is listed here.
    /// </remarks>
    public Task<IReadOnlyList<Quest>> ListAsync(
        string? receiver = null, bool includeClosed = false, string? workspace = null,
        CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<Quest>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"""
            SELECT * FROM quests
            WHERE ($receiver IS NULL OR receiver = $receiver)
              AND ($workspace IS NULL OR workspace = $workspace COLLATE NOCASE)
              {(includeClosed ? "" : "AND (status IN ('Open', 'Taken') OR held = 1)")}
            ORDER BY CASE status WHEN 'Open' THEN 0 WHEN 'Taken' THEN 1 ELSE 2 END, filed
            """;
        command.Parameters.AddWithValue("$receiver", (object?)receiver ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$workspace", workspace is null ? DBNull.Value : Workspaces.Normalize(workspace));

        var quests = new List<Quest>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) quests.Add(Read(reader));
        return quests;
    }, ct);

    /// <summary>
    /// Every quest one sender asked, closed ones included — or, <paramref name="startingWith"/>, every
    /// quest whose sender begins with it. What an ask's standing is read from (USE1c): its quests are
    /// the ones asked BY it, chain steps included, whichever machine last moved them.
    /// </summary>
    public Task<IReadOnlyList<Quest>> FromAsync(
        string sender, bool startingWith = false, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<Quest>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = startingWith
            ? "SELECT * FROM quests WHERE substr(sender, 1, length($sender)) = $sender ORDER BY filed"
            : "SELECT * FROM quests WHERE sender = $sender ORDER BY filed";
        command.Parameters.AddWithValue("$sender", sender);

        var quests = new List<Quest>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) quests.Add(Read(reader));
        return quests;
    }, ct);

}
