using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// Session records, held by the service beside the quests they serve.
/// </summary>
/// <remarks>
/// <para><b>The record is service state; the process is not.</b> Every client benefits from the record —
/// the platform renders it, it survives a driver restart, and a remote deployment can sync records where
/// it could never sync processes. The process handle stays with the driver that spawned it (D46).</para>
///
/// <para><b>This store is deliberately as blind as the quest store.</b> What may move where is judgement,
/// and judgement lives in <see cref="SessionLedger"/> — one implementation for every door, for the same
/// reason <see cref="QuestExchange"/> exists. The store is the root that wires: the table is <see cref="SessionSchema"/>, the reads
/// are <see cref="SessionReads"/>, the writes <see cref="SessionWrites"/>, the words <see cref="SessionSaidStore"/>, and the columns'
/// spellings <see cref="SessionRowCodec"/>; each door here is one line to them.</para>
/// </remarks>
public sealed class SessionStore
{
    /// <summary>The connection's gate: every command here runs inside it (SQLITETX1).</summary>
    private readonly ConnectionGate _db;

    private readonly SessionReads reads;
    private readonly SessionWrites writes;
    private readonly SessionSaidStore said;

    private SessionStore(SqliteConnection connection)
    {
        _db = ConnectionGate.For(connection);
        Opens = new SessionOpens(_db);
        Memory = new MemoryOpStore(_db);
        reads = new SessionReads(this);
        writes = new SessionWrites(this);
        said = new SessionSaidStore(this);
    }

    internal ConnectionGate Db => _db;

    public SessionOpens Opens { get; }

    /// <summary>The memory coordinator's log of record, whose rows a session's commit point moves inside the ledger's step (KNOWCENTRAL2c).</summary>
    public MemoryOpStore Memory { get; }

    /// <summary>
    /// The id a record's answer from before <see cref="Session.Said"/> took as its word when the desktop's migration converted it
    /// (MSG1a, D232). Never one a word is given now, which are hexadecimal.
    /// </summary>
    public const string AnswerWordId = "answer";

    public static async Task<SessionStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        var store = new SessionStore(connection);
        await store._db.RunAsync(async () =>
        {
            await SessionSchema.EnsureAsync(store._db, ct).ConfigureAwait(false);
            await store.Opens.EnsureAsync(ct).ConfigureAwait(false);
            await store.Memory.EnsureAsync(ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
        return store;
    }

    /// <summary>
    /// Run a check and the write it decides as one step, holding the database's write lock throughout.
    /// </summary>
    /// <remarks>
    /// <para>Every host on a machine opens the same file: the desktop's host, and a connector for each
    /// session. The one-session-per-tree lock was a read followed by a write, so two opens at once could
    /// both read "free" and both write (REV3). <c>BEGIN IMMEDIATE</c> takes the file's write lock BEFORE
    /// the read, so a second host waits until the first has written, then reads what it wrote. The
    /// connection's gate does the same between requests inside one host, for every command (SQLITETX1).</para>
    ///
    /// <para>The work is handed an uncancellable token: once the transaction has begun, a cancelled request
    /// must not throw halfway (REV3). It calls the stores' own methods, which run inside the gate it holds
    /// and join its transaction.</para>
    /// </remarks>
    public Task<T> ExclusiveAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default) =>
        _db.InTransactionAsync((_, inside) => work(inside), ct);

    /// <summary>Record a new attempt, queued (D49 §3); see <see cref="SessionWrites.CreateAsync"/>.</summary>
    public Task<Session> CreateAsync(
        string? quest, string repository, string adapter, DateTimeOffset now,
        string? workspace = null, SessionKind kind = SessionKind.Driven,
        string? doorVersion = null, string? profile = null, string? tree = null,
        string? baseCommit = null, CancellationToken ct = default, string? ask = null, string? opinion = null,
        string? serves = null, string? from = null, string? fromWhy = null, string? fromBy = null,
        string? entryOrigin = null, string? entryPart = null, bool ownEntry = false, string? helpMode = null,
        IReadOnlyDictionary<string, string>? access = null, string? piece = null, string? actor = null) =>
        writes.CreateAsync(
            quest, repository, adapter, now, workspace, kind, doorVersion, profile, tree, baseCommit, ct, ask, opinion, serves, from,
            fromWhy, fromBy, entryOrigin, entryPart, ownEntry, helpMode, access, piece, actor);

    /// <summary>Move a session and attach what the move carries; see <see cref="SessionWrites.SetStateAsync"/>.</summary>
    public Task<Session?> SetStateAsync(
        string id, SessionState state, string? note, string? evidence, string? transcript,
        DateTimeOffset now, CancellationToken ct = default, bool interrupted = false, bool limit = false,
        bool clearSaid = false, bool forgive = false, string? noteParts = null) =>
        writes.SetStateAsync(id, state, note, evidence, transcript, now, ct, interrupted, limit, clearSaid, forgive, noteParts);

    /// <summary>Copy another machine's session record into this store, whole (D47 §6); see <see cref="SessionWrites.MirrorAsync"/>.</summary>
    public Task MirrorAsync(Session record, CancellationToken ct = default) => writes.MirrorAsync(record, ct);

    /// <summary>The record of one session; null where there is none.</summary>
    public Task<Session?> FindAsync(string id, CancellationToken ct = default) => reads.FindAsync(id, ct);

    /// <summary>This machine's record writing a partner's piece (COAGENT1b); see <see cref="SessionReads.ForPieceAsync"/>.</summary>
    public Task<Session?> ForPieceAsync(string piece, CancellationToken ct = default) => reads.ForPieceAsync(piece, ct);

    /// <summary>The newest of this machine's records continuing a session (COAGENT1b); see <see cref="SessionReads.ContinuedByAsync"/>.</summary>
    public Task<Session?> ContinuedByAsync(string id, CancellationToken ct = default) => reads.ContinuedByAsync(id, ct);

    /// <summary>The record a quest's publisher names (SESSONE1a); see <see cref="SessionReads.FindOrMirrorAsync"/>.</summary>
    public Task<Session?> FindOrMirrorAsync(string id, CancellationToken ct = default) => reads.FindOrMirrorAsync(id, ct);

    /// <summary>The conversation that took a quest (CHATTAKE1); see <see cref="SessionReads.FindChatTookAsync"/>.</summary>
    public Task<Session?> FindChatTookAsync(string questId, CancellationToken ct = default) => reads.FindChatTookAsync(questId, ct);

    /// <summary>The newest of this machine's records for a quest (D80); see <see cref="SessionReads.LastOwnForQuestAsync"/>.</summary>
    public Task<Session?> LastOwnForQuestAsync(string quest, CancellationToken ct = default) => reads.LastOwnForQuestAsync(quest, ct);

    /// <summary>The id of the newest of this machine's records for a quest; see <see cref="SessionReads.LastOwnIdForQuestAsync"/>.</summary>
    public Task<string?> LastOwnIdForQuestAsync(string quest, CancellationToken ct = default) => reads.LastOwnIdForQuestAsync(quest, ct);

    /// <summary>Whether a session of this machine's took a quest (STANDDOWN2); see <see cref="SessionReads.TookHereAsync"/>.</summary>
    public Task<bool> TookHereAsync(string quest, CancellationToken ct = default) => reads.TookHereAsync(quest, ct);

    /// <summary>This machine's session with a process on a quest; see <see cref="SessionReads.RunningForQuestAsync"/>.</summary>
    public Task<Session?> RunningForQuestAsync(string quest, CancellationToken ct = default) => reads.RunningForQuestAsync(quest, ct);

    /// <summary>The newest teammate's record for a quest that did not stand down (CARRY2); see <see cref="SessionReads.LastTeammateForQuestAsync"/>.</summary>
    public Task<Session?> LastTeammateForQuestAsync(string quest, CancellationToken ct = default) => reads.LastTeammateForQuestAsync(quest, ct);

    /// <summary>A record of a session started for a quest, whoever's (D95); see <see cref="SessionReads.AnyForQuestAsync"/>.</summary>
    public Task<Session?> AnyForQuestAsync(string quest, CancellationToken ct = default) => reads.AnyForQuestAsync(quest, ct);

    /// <summary>Every quest a session record names (D95); see <see cref="SessionReads.QuestsNamedAsync"/>.</summary>
    public Task<IReadOnlySet<string>> QuestsNamedAsync(CancellationToken ct = default) => reads.QuestsNamedAsync(ct);

    /// <summary>The session holding a working tree (D51); see <see cref="SessionReads.ActiveForAsync"/>.</summary>
    public Task<Session?> ActiveForAsync(string repository, string? tree = null, CancellationToken ct = default) =>
        reads.ActiveForAsync(repository, tree, ct);

    /// <summary>This machine's completed records in a tree whose note carries a code (HELPERWAIT1); see <see cref="SessionReads.CompletedWithCodeAsync"/>.</summary>
    public Task<IReadOnlyList<Session>> CompletedWithCodeAsync(
        string repository, string? tree, string code, CancellationToken ct = default) =>
        reads.CompletedWithCodeAsync(repository, tree, code, ct);

    /// <summary>The session with a process in a directory, the intake room's lock (D65 §1b); see <see cref="SessionReads.RunningInTreeAsync"/>.</summary>
    public Task<Session?> RunningInTreeAsync(string tree, CancellationToken ct = default) => reads.RunningInTreeAsync(tree, ct);

    /// <summary>Ask Daoris's one at a time, per workspace (ASKSCOPE1a); see <see cref="SessionReads.RunningInWorkspaceAsync"/>.</summary>
    public Task<Session?> RunningInWorkspaceAsync(
        string repository, string workspace, bool parked = false, CancellationToken ct = default) =>
        reads.RunningInWorkspaceAsync(repository, workspace, parked, ct);

    /// <summary>Sessions in one repository, or everywhere, live work first; see <see cref="SessionReads.ListAsync"/>.</summary>
    public Task<IReadOnlyList<Session>> ListAsync(
        string? repository = null, bool includeClosed = false, string? workspace = null,
        CancellationToken ct = default) =>
        reads.ListAsync(repository, includeClosed, workspace, ct);

    /// <summary>Where the store's writes stand, by every writer of its file; see <see cref="StoreMark"/>.</summary>
    public Task<StoreMark> MarkAsync(CancellationToken ct = default) => StoreMark.ReadAsync(_db, ct);

    /// <summary>This machine's own records written after a revision, what a push sends (SYNC4); see <see cref="SessionReads.OwnChangedSinceAsync"/>.</summary>
    public Task<IReadOnlyList<(Session Session, long Revision)>> OwnChangedSinceAsync(
        long revision, string workspace, CancellationToken ct = default) =>
        reads.OwnChangedSinceAsync(revision, workspace, ct);

    /// <summary>A remote's side: the team's records after a revision, one page; see <see cref="SessionReads.TeamSinceAsync"/>.</summary>
    public Task<SessionFetch> TeamSinceAsync(long since, string? caller, int limit = 500, CancellationToken ct = default) =>
        reads.TeamSinceAsync(since, caller, limit, ct);

    /// <summary>How far this machine has pushed its own records to a workspace's remote, and fetched the team's.</summary>
    public Task<(long Pushed, long Fetched)> CursorAsync(string workspace, CancellationToken ct = default) => reads.CursorAsync(workspace, ct);

    /// <summary>Move a workspace's cursors forward, never back; see <see cref="SessionWrites.AdvanceCursorAsync"/>.</summary>
    public Task AdvanceCursorAsync(
        string workspace, long? pushed = null, long? fetched = null, CancellationToken ct = default) =>
        writes.AdvanceCursorAsync(workspace, pushed, fetched, ct);

    /// <summary>D205's later door, this machine's record only; see <see cref="SessionWrites.KeepActorAsync"/>.</summary>
    public Task<bool> KeepActorAsync(string id, string? choice, JsonElement? asked, JsonElement? changed, JsonElement? ran,
        JsonElement? chosen = null, CancellationToken ct = default) =>
        writes.KeepActorAsync(id, choice, asked, changed, ran, chosen, ct);

    /// <summary>Add the downloaded versions a start was handed (KEEP1b); see <see cref="SessionWrites.KeepResolvedAsync"/>.</summary>
    public Task<bool> KeepResolvedAsync(string id, IReadOnlyList<string> versions, CancellationToken ct = default) =>
        writes.KeepResolvedAsync(id, versions, ct);

    /// <summary>Write the access mode an Ask Daoris record runs under (HELPMODE1a2); see <see cref="SessionWrites.KeepHelpModeAsync"/>.</summary>
    public Task<bool> KeepHelpModeAsync(string id, string mode, CancellationToken ct = default) => writes.KeepHelpModeAsync(id, mode, ct);

    /// <summary>Write the access setup a driven record now runs under (AUTONOMY1b2); see <see cref="SessionWrites.KeepAccessAsync"/>.</summary>
    public Task<bool> KeepAccessAsync(string id, IReadOnlyDictionary<string, string> access, CancellationToken ct = default) =>
        writes.KeepAccessAsync(id, access, ct);

    /// <summary>Mark these records as gone up to a remote (SESSUX1f); see <see cref="SessionWrites.MarkPushedAsync"/>.</summary>
    public Task MarkPushedAsync(IEnumerable<string> ids, CancellationToken ct = default) => writes.MarkPushedAsync(ids, ct);

    /// <summary>Remove a record whole (SESSUX1f); see <see cref="SessionWrites.DeleteAsync"/>.</summary>
    public Task<bool> DeleteAsync(string id, CancellationToken ct = default) => writes.DeleteAsync(id, ct);

    /// <summary>The finished trees' records no session names, gone with a clear of history (KEEPSIZE1b); see <see cref="SessionWrites.ForgetOrphanRecordsAsync"/>.</summary>
    public Task<int> ForgetOrphanRecordsAsync(string workspace, DateTimeOffset through, CancellationToken ct = default) =>
        writes.ForgetOrphanRecordsAsync(workspace, through, ct);

    /// <summary>Keep a word the person said on this machine's record (MSG1a); see <see cref="SessionSaidStore.KeepSaidAsync"/>.</summary>
    public Task<Session?> KeepSaidAsync(
        string id, SaidWord word, string? note = null, CancellationToken ct = default, string? noteParts = null) =>
        said.KeepSaidAsync(id, word, note, ct, noteParts);

    /// <summary>Take these words off this machine's record (MSG1a); see <see cref="SessionSaidStore.TakeSaidAsync"/>.</summary>
    public Task<Session?> TakeSaidAsync(string id, IReadOnlyCollection<string> ids, CancellationToken ct = default) =>
        said.TakeSaidAsync(id, ids, ct);

    /// <summary>Move picked words off one record onto another (FOUNDATION1 row d-ii part 3); see <see cref="SessionSaidStore.MoveSaidAsync"/>.</summary>
    internal Task<IReadOnlyList<SaidWord>> MoveSaidAsync(string from, string to, Func<SaidWord, bool> moves, CancellationToken ct = default) =>
        said.MoveSaidAsync(from, to, moves, ct);

    /// <summary>The newest settlement of a producer's key over these records (row p); see <see cref="SessionSaidStore.SettledAsync"/>.</summary>
    internal Task<SettledWord?> SettledAsync(IReadOnlyList<string> records, string key, long since, CancellationToken ct = default) =>
        said.SettledAsync(records, key, since, ct);

    /// <summary>The newest settlement of a key on any record; see <see cref="SessionSaidStore.SettledUnderAsync"/>.</summary>
    internal Task<SettledWord?> SettledUnderAsync(string key, long since, CancellationToken ct = default) =>
        said.SettledUnderAsync(key, since, ct);

    /// <summary>Mark the moment a dispatcher first wrote a waiting word to a harness (COWORK1c); see <see cref="SessionSaidStore.MarkAttemptAsync"/>.</summary>
    public Task<bool> MarkAttemptAsync(string id, string word, DateTimeOffset at, CancellationToken ct = default) =>
        said.MarkAttemptAsync(id, word, at, ct);

    /// <summary>Mark that this session took its quest itself (STANDDOWN2); see <see cref="SessionWrites.MarkTookAsync"/>.</summary>
    public Task<bool> MarkTookAsync(string id, string? quest = null, CancellationToken ct = default) =>
        writes.MarkTookAsync(id, quest, ct);

    /// <summary>What this machine's record keeps of its review (REVIEWENV1b); see <see cref="SessionReads.ReviewOfAsync"/>.</summary>
    public Task<SessionReview> ReviewOfAsync(string id, CancellationToken ct = default) => reads.ReviewOfAsync(id, ct);

    /// <summary>How many set-ups this machine's sessions on a quest said; see <see cref="SessionReads.SetUpsSaidForAsync"/>.</summary>
    public Task<int> SetUpsSaidForAsync(string quest, CancellationToken ct = default) => reads.SetUpsSaidForAsync(quest, ct);

    /// <summary>Write this machine's record's review whole; see <see cref="SessionWrites.KeepReviewAsync"/>.</summary>
    public Task<bool> KeepReviewAsync(string id, SessionReview review, CancellationToken ct = default) => writes.KeepReviewAsync(id, review, ct);
}
