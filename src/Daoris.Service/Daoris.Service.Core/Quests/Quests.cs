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
/// session is handed a connector over the protocol door. A quest nothing could answer would sit
/// in a queue nobody reads, so refusing at publish time says that immediately, rather than letting it
/// look delivered.</para>
///
/// <para><b>A quest is its history</b>. Every verb appends an operation to
/// <c>quest_log</c>, stamped with this store's machine and that machine's next sequence number, and
/// the <c>quests</c> table is rewritten from replaying the quest's history through
/// <see cref="QuestTransitions"/> in the same transaction — so what the platform reads is a cache of
/// the replay, never a second truth beside it.</para>
///
/// <para><b>A remote is where histories meet</b>: a machine fetches what the
/// remote accepted, rebases what it has not pushed on top, and pushes; the remote accepts a quest's
/// operations only when nothing reached that quest since the push is rebased. Both halves live here,
/// behind the same table: <see cref="IntegrateAsync"/>, <see cref="PendingAsync"/> and
/// <see cref="AcceptedAsync"/> for a machine, <see cref="OperationsSinceAsync"/> and
/// <see cref="ReceiveAsync"/> for a remote.</para>
/// </remarks>
public sealed class QuestStore
{
    /// <summary>
    /// Hex characters in a quest id: 48 bits. Every machine holds every quest touching its repositories,
    /// so a shorter id lets unrelated asks collide.
    /// </summary>
    internal const int IdLength = 12;

    /// <summary>
    /// The version of what this build keeps in the store, stamped in <c>quest_store_version</c>;
    /// raise it with any change to a table or a contract an older build would misread.
    /// </summary>
    public const int StoreVersion = 1;

    /// <summary>The version a newer build stamps on this store, or null where this build's is the newest that has written it.</summary>
    public int? NewerVersion { get; private set; }

    /// <summary>The connection's gate: every command here runs inside it.</summary>
    private readonly ConnectionGate _db;

    /// <summary>
    /// The clock a take measures its wait for the write on, so the bind named for it is judged at the moment the
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
        _chain = new QuestChain(this);
        _integrator = new QuestIntegrator(this);
        _followUps = new QuestFollowUps(this);
        _passes = new QuestPasses(this);
        _claims = new QuestClaims(this);
        _histories = new QuestHistories(this);
        _takeReadings = new QuestTakeReadings(this);
        _forgetting = new QuestForgetting(this);
        _reads = new QuestReads(this);
    }

    private readonly QuestHistories _histories;
    private readonly QuestTakeReadings _takeReadings;
    private readonly QuestForgetting _forgetting;
    private readonly QuestReads _reads;

    internal Task GiveHistoriesAsync(CancellationToken ct) => _histories.GiveHistoriesAsync(ct);

    /// <summary>One quest's history, from its row: the operations that replay to exactly what it says.</summary>
    internal Task<IReadOnlyList<QuestOperation>> GiveHistoryAsync(Quest quest, SqliteTransaction transaction, CancellationToken ct) =>
        _histories.GiveHistoryAsync(quest, transaction, ct);

    /// <summary>A quest's history in the order it replays: what a remote accepted, by number, then what this machine has not pushed.</summary>
    public Task<IReadOnlyList<QuestOperation>> HistoryAsync(string id, CancellationToken ct = default) => _histories.HistoryAsync(id, ct);

    internal Task<IReadOnlyList<QuestOperation>> HistoryAsync(string id, SqliteTransaction? transaction, CancellationToken ct) =>
        _histories.HistoryAsync(id, transaction, ct);

    /// <summary>A quest's history read inside a transaction the caller already holds.</summary>
    internal Task<IReadOnlyList<QuestOperation>> HistoryWithinAsync(string id, CancellationToken ct) => _histories.HistoryWithinAsync(id, ct);

    /// <summary>Who holds a quest's step and how each take that lost on it stands.</summary>
    public Task<QuestTakeReading> ReadTakesAsync(string id, CancellationToken ct = default) => _takeReadings.ReadTakesAsync(id, ct);

    /// <summary>The take reading of each of the quests with something to read, by id.</summary>
    public Task<IReadOnlyDictionary<string, QuestTakeReading>> ReadTakesAsync(IEnumerable<Quest> quests, CancellationToken ct = default) =>
        _takeReadings.ReadTakesAsync(quests, ct);

    /// <summary>Remove one quest from this machine, forgetting it where a remote numbered it (see <see cref="QuestForgetting"/>).</summary>
    internal Task<bool> ForgetAsync(string id, DateTimeOffset at, CancellationToken ct) => _forgetting.ForgetAsync(id, at, ct);

    /// <summary>Whether this machine forgot the quest.</summary>
    public Task<bool> ForgottenAsync(string id, CancellationToken ct = default) => _forgetting.ForgottenAsync(id, ct);

    /// <summary>Every quest this machine forgot.</summary>
    public Task<IReadOnlySet<string>> ForgottenAsync(CancellationToken ct = default) => _forgetting.ForgottenAsync(ct);

    internal Task<IReadOnlySet<string>> ForgottenAsync(SqliteTransaction? transaction, CancellationToken ct) =>
        _forgetting.ForgottenAsync(transaction, ct);

    /// <summary>The taken quests waiting on a question.</summary>
    public Task<IReadOnlyList<Quest>> WaitingOnAsync(string question, CancellationToken ct = default) => _reads.WaitingOnAsync(question, ct);

    /// <summary>Every question a taken quest waits on.</summary>
    public Task<IReadOnlySet<string>> AwaitedAsync(CancellationToken ct = default) => _reads.AwaitedAsync(ct);

    /// <summary>The last number this machine fetched from a workspace's remote: zero before its first fetch.</summary>
    public Task<long> CursorAsync(string workspace, CancellationToken ct = default) => _reads.CursorAsync(workspace, ct);

    internal Task<long> CursorAsync(string workspace, SqliteTransaction? transaction, CancellationToken ct) =>
        _reads.CursorAsync(workspace, transaction, ct);

    /// <summary>One quest by id, or null.</summary>
    public Task<Quest?> FindAsync(string id, CancellationToken ct = default) => _reads.FindAsync(id, ct);

    internal Task<Quest?> FindAsync(string id, SqliteTransaction? transaction, CancellationToken ct) =>
        _reads.FindAsync(id, transaction, ct);

    /// <summary>Quests addressed to one repository, or every quest when none is named; what is outstanding first.</summary>
    public Task<IReadOnlyList<Quest>> ListAsync(
        string? receiver = null, bool includeClosed = false, string? workspace = null, CancellationToken ct = default) =>
        _reads.ListAsync(receiver, includeClosed, workspace, ct);

    /// <summary>Quests a sender filed, or whose sender starts with the name.</summary>
    public Task<IReadOnlyList<Quest>> FromAsync(string sender, bool startingWith = false, CancellationToken ct = default) =>
        _reads.FromAsync(sender, startingWith, ct);

    private readonly QuestPasses _passes;
    private readonly QuestClaims _claims;

    internal Task LoadClosuresAsync(CancellationToken ct) => _passes.LoadClosuresAsync(ct);

    /// <summary>How a circle's pass ended (see <see cref="QuestPasses"/>).</summary>
    public Task RecordPassAsync(string workspace, QuestSyncReport report, DateTimeOffset at, CancellationToken ct = default) =>
        _passes.RecordPassAsync(workspace, report, at, ct);

    /// <summary>The remote's handshake as a circle's last pass heard it; null where none has.</summary>
    public QuestClosure? ClosureOf(string workspace) => _passes.ClosureOf(workspace);

    /// <summary>A remote's half of the handshake: what a push said of its machine.</summary>
    public Task DeclaredAsync(string? declares, IReadOnlyList<QuestOperation> pushed, DateTimeOffset at, CancellationToken ct = default) =>
        _passes.DeclaredAsync(declares, pushed, at, ct);

    /// <summary>A remote's answer to the handshake: the machines whose last push did not declare closure.</summary>
    public Task<QuestClosure> ClosureAsync(CancellationToken ct = default) => _passes.ClosureAsync(ct);

    /// <summary>A person's retirement of a machine at a remote; false where the remote has heard nothing from it.</summary>
    public Task<bool> RetireMachineAsync(string machine, DateTimeOffset at, CancellationToken ct = default) =>
        _passes.RetireMachineAsync(machine, at, ct);

    /// <summary>The handshake as a retirement answered it, kept as the last pass keeps what it heard.</summary>
    public Task HeardClosureAsync(string workspace, QuestClosure closure, CancellationToken ct = default) =>
        _passes.HeardClosureAsync(workspace, closure, ct);

    /// <summary>Where this machine's claim on a quest stands: held, unconfirmed, lost or none.</summary>
    public Task<QuestClaim> ClaimAsync(string id, CancellationToken ct = default) => _claims.ClaimAsync(id, ct);

    /// <summary>Whether this machine's lost claim lost to the person's reopen rather than another machine's take.</summary>
    public Task<bool> LostToTheReopenAsync(string id, CancellationToken ct = default) => _claims.LostToTheReopenAsync(id, ct);

    private readonly QuestChain _chain;
    private readonly QuestIntegrator _integrator;
    private readonly QuestFollowUps _followUps;

    /// <summary>Every quest of the <c>follows</c> chain <paramref name="id"/> is on; empty for an unknown id (see <see cref="QuestChain"/>).</summary>
    public Task<IReadOnlyList<Quest>> ChainAsync(string id, CancellationToken ct = default) => _chain.ChainAsync(id, ct);

    /// <summary>Keeps what a remote accepted and replays the quests it touched; the cursor moves to <paramref name="through"/> (see <see cref="QuestIntegrator"/>).</summary>
    public Task<QuestIntegration> IntegrateAsync(
        string workspace, IReadOnlyList<QuestOperation> fetched, long through, CancellationToken ct = default) =>
        _integrator.IntegrateAsync(workspace, fetched, through, ct);

    /// <summary>The takes a lost parent move rewrites into conflicts (see <see cref="QuestFollowUps"/>).</summary>
    internal Task<IReadOnlyList<QuestOperation>> ForgetFollowUpsAsync(
        string parent, bool takeLost, string published, bool reopened, SqliteTransaction transaction, CancellationToken ct) =>
        _followUps.ForgetFollowUpsAsync(parent, takeLost, published, reopened, transaction, ct);

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

    /// <summary>A remote's half of a push: accepts the operations of each quest nothing reached since the push is rebased.</summary>
    public Task<QuestPush> ReceiveAsync(
        long @base, IReadOnlyList<QuestOperation> pushed, Func<Quest, string?> judge, Func<Quest, string> workspaceOf,
        CancellationToken ct = default) =>
        _sync.ReceiveAsync(@base, pushed, judge, workspaceOf, ct);

    /// <summary>Write a quest's row as its replay gives it: the row is a cache, so a replay alone decides what it says.</summary>
    internal Task WriteCacheAsync(Quest quest, SqliteTransaction transaction, CancellationToken ct) =>
        _cache.WriteAsync(quest, transaction, ct);

    /// <summary>
    /// This store's machine: the stable id every operation it writes is stamped with — never
    /// the key, which rotates.
    /// </summary>
    /// <remarks>
    /// Kept in the store, beside the sequence it numbers, rather than in a file of its own: a machine
    /// whose store is deleted starts its sequence again at one, and under the same id that would
    /// name operations a remote already holds. A new store is a new machine, and cannot be anything else.
    /// </remarks>
    public string Machine { get; private set; } = "";

    /// <param name="connection">The store's connection, opened.</param>
    /// <param name="ct">Honoured while waiting for the connection's gate.</param>
    /// <param name="time">The clock a take measures its wait on; the system's where none is named.</param>
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
    /// Run <paramref name="work"/> in a write transaction taken at once (BEGIN IMMEDIATE), not at its
    /// first write: the judgement is made on what it reads, so the read must already hold the lock, or
    /// two hosts over one file could both judge the same open quest and both append a take.
    /// </summary>
    /// <remarks>
    /// <para>Within one host, the connection's gate holds every other request's work off until this one
    /// ends, its commands as well as its transactions (<see cref="ConnectionGate"/>).</para>
    ///
    /// <para>It COMMITS whenever the work returns, including a refusal that wrote nothing, and rolls back
    /// only on a throw. Cancellation is not one, once the transaction has begun: the wait honours the
    /// caller, and the work is handed a token nobody cancels.</para>
    /// </remarks>
    private Task<T> InTransactionAsync<T>(
        Func<SqliteTransaction, CancellationToken, Task<T>> work, CancellationToken ct) =>
        _db.InTransactionAsync(work, ct);

    /// <summary>A registered operation's verb, run in a write transaction (<see cref="InTransactionAsync{T}"/>) handed to it as its <see cref="QuestTx"/>.</summary>
    internal Task<T> WriteAsync<T>(Func<QuestTx, Task<T>> write, CancellationToken ct = default) =>
        InTransactionAsync((transaction, inside) => write(new QuestTx(this, transaction, inside)), ct);

    /// <summary>The kinds of operation their modules register; a door reaches a module's verbs by its type.</summary>
    private static QuestOperations Operations => QuestOperations.Registered;

    internal SqliteCommand Command() => _db.Command();

    internal TimeProvider Time => _time;

    /// <summary>
    /// A handle derived from who asked, of whom, and for what — twelve hex characters.
    /// </summary>
    /// <remarks>
    /// Content-derived so publishing the same quest twice collides rather than multiplying — an agent
    /// that retries should not produce a second copy of the same ask, and the same ask made on two
    /// machines is one quest. A chain's step also derives from its PARENT: "Verify in the
    /// browser" is a title many chains will use, and a step that collided with an earlier quest of
    /// those words would quietly join somebody else's closed quest. The widening keeps the hash, so an
    /// id from before it is exactly the first six characters of the same ask's id now. A quest to
    /// LANES widens the same way, only when there are some, so every quest to a whole
    /// repository keeps its id and a lane's ask is not the repository's. A workflow request's key widens it the same way,
    /// only when there is one, so distinct runs or nodes make distinct quests whatever their words. The same words asked after the
    /// first ask closed are a new ask: <paramref name="again"/> counts the closed ones before it, and zero is the id every quest has always had.
    /// </remarks>
    internal static string MakeId(
        string from, string to, string title, string? parent = null, IReadOnlyList<string>? lanes = null, string? request = null,
        int again = 0) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(
                    $"{from}->{to}:{title.Trim()}" + (parent is null ? "" : $"<-{parent}")
                    + (lanes is { Count: > 0 } ? $"@{string.Join('+', lanes.Order(StringComparer.Ordinal))}" : "")
                    + (request is null ? "" : $"#{request}")
                    + (again == 0 ? "" : $"~again{again}"))))[..IdLength]
            .ToLowerInvariant();

    /// <summary>Publish a quest. Returns the quest standing under these words unchanged if one is open or taken.</summary>
    /// <param name="workspace">
    /// The circle both sides share — decided by <see cref="QuestExchange"/>, which is where the
    /// same-workspace clause lives. The store holds state; it does not judge who may ask whom.
    /// </param>
    /// <param name="links">Addresses the quest carries, already judged by the exchange.</param>
    /// <param name="attachments">Files the quest carries, by name — the bytes are never this store's.</param>
    /// <param name="then">The chain after this quest, already judged by the exchange.</param>
    /// <param name="publishedBy">The session whose connector published it, when one did.</param>
    /// <param name="lanes">The lanes of <paramref name="to"/> it addresses, already judged by the exchange.</param>
    /// <param name="requirements">What the person requires, already judged by the exchange against their words.</param>
    /// <param name="shortTitle">The publisher's short title, already judged by the exchange; null for none.</param>
    /// <param name="review">The chain's review choice, already judged by the exchange; null for none.</param>
    /// <param name="setUpIn">A set-up step's environment, already judged by the exchange; null for any other quest.</param>
    /// <param name="parent">
    /// The quest a set-up step follows, when a door publishes one after its chain's close: its id
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
        string? request = null) =>
        (await PublishHeldAsync(
            from, to, title, body, now, workspace, links, attachments, then, ct, publishedBy, lanes, requirements, shortTitle, review,
            setUpIn, parent, weight, paths, request).ConfigureAwait(false)).Quest;

    /// <summary><see cref="PublishAsync"/>, with <c>Held</c> true when the publish is a retry that writes nothing.</summary>
    internal async Task<(Quest Quest, bool Held)> PublishHeldAsync(
        string from, string to, string title, string body, DateTimeOffset now,
        string? workspace,
        IReadOnlyList<string>? links,
        IReadOnlyList<QuestAttachment>? attachments,
        IReadOnlyList<QuestStep>? then,
        CancellationToken ct,
        string? publishedBy,
        IReadOnlyList<string>? lanes,
        IReadOnlyList<QuestRequirement>? requirements,
        string? shortTitle,
        QuestReview? review,
        string? setUpIn,
        string? parent,
        string? weight,
        IReadOnlyList<string>? paths,
        string? request)
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
    /// The id these words would publish under, when this machine forgot that quest; null when it did
    /// not. The exchange asks it before a publish that carries a request key, which has no next id, so the same key asked again is refused
    /// here rather than making a fresh quest the remote, which still holds the closed one, refuses for good.
    /// </summary>
    internal async Task<string?> ForgottenIdAsync(
        string from, string to, string title, IReadOnlyList<string>? lanes, CancellationToken ct, string? request = null)
    {
        var id = MakeId(from, to, title, lanes: Sorted(lanes), request: request);
        return await ForgottenAsync(id, ct).ConfigureAwait(false) ? id : null;
    }

    /// <summary>
    /// Append one operation, stamped with this machine and its next sequence number — counted inside
    /// the caller's write transaction, so two hosts over one file cannot take the same number.
    /// </summary>
    /// <remarks>
    /// The next number is one past the larger of the log's highest for this machine and the store's mark,
    /// which the insert's trigger then moves. The log alone goes back when its newest rows are removed, and the number
    /// handed out again is one a remote already holds: it answers a push of it as a retry, and a fetch brings its own
    /// operation back under it, so the new move is lost without an error.
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
            // Parts only beside a note, as the store keeps them: what it reads back is what it wrote.
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

    /// <summary>The kinds that name a submission: a reopen, and a rebind.</summary>
    internal static bool Names(QuestOperationKind kind) => kind is QuestOperationKind.Reopened or QuestOperationKind.Rebound;

    /// <summary>
    /// The kinds that keep the session that said them through its connector: a ready, and a done and a decline
    /// each the act a handback is written at. On any other kind it is never written, kept or read; on these it is
    /// written only where a session said it, so a close through any other door, and every one from before, reads as it did.
    /// </summary>
    internal static bool KeepsSession(QuestOperationKind kind) =>
        kind is QuestOperationKind.Submitted or QuestOperationKind.Done or QuestOperationKind.Declined;

    /// <summary>
    /// The kinds a submission may ride on: the two that name one (<see cref="Names"/>), and a take, which carries its step's source as it
    /// found it, only where it found one. On any other kind it is never written, kept or read.
    /// </summary>
    internal static bool Carries(QuestOperationKind kind) => Names(kind) || kind == QuestOperationKind.Taken;

    /// <summary>
    /// The kinds a <c>whileOpen</c> mark may carry: a decline, and a publication's rebind. On any other
    /// kind it is never written, kept or read.
    /// </summary>
    internal static bool JudgedOpen(QuestOperationKind kind) => kind is QuestOperationKind.Declined or QuestOperationKind.Rebound;

    /// <inheritdoc cref="QuestTakeOperation.MoveAsync"/>
    public Task<QuestMove> MoveAsync(
        string id, QuestStatus status, string? note, DateTimeOffset now, CancellationToken ct = default,
        IReadOnlyList<QuestAnswer>? answers = null, bool whileOpen = false, string? noteParts = null, bool byAgent = false,
        string? session = null) =>
        WriteAsync(
            tx => Operations.Of<QuestTakeOperation>().MoveAsync(
                tx, id, status, note, now, answers, whileOpen, noteParts, byAgent, session),
            ct);

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

    /// <summary>
    /// Delete an open quest, judged against the replayed history inside the write, as every move
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

    /// <summary>Remove a closed quest (<see cref="QuestClosedRemoval"/>): tombstoned where it may have travelled, simply gone otherwise.</summary>
    public Task<QuestDeletion> RemoveClosedAsync(string id, DateTimeOffset now, bool travels, CancellationToken ct = default) =>
        WriteAsync(tx => QuestClosedRemoval.RemoveAsync(tx, id, now, travels), ct);

    /// <summary>The columns <see cref="QuestRecordCodec.ReadOperation"/> reads, in its order.</summary>
    internal const string OperationColumns =
        "quest_log.quest, quest_log.kind, quest_log.machine, quest_log.sequence, quest_log.at, quest_log.payload, quest_log.remote";
}
