using static Daoris.Knowledge.SessionRowCodec;
using static Daoris.Knowledge.SessionSchema;

namespace Daoris.Knowledge;

/// <summary>
/// The reads of the sessions table: one record, the records of a quest, the tree and workspace locks, the lists, and what the
/// sync pushes and fetches. Blind, like the writes: what a read means is the ledger's to judge.
/// </summary>
internal sealed class SessionReads(SessionStore store)
{
    private readonly ConnectionGate _db = store.Db;

    public Task<Session?> FindAsync(string id, CancellationToken ct = default) => _db.RunAsync<Session?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT * FROM sessions WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>
    /// This machine's record writing <paramref name="piece"/>: the partner's open is idempotent by the piece's
    /// id, so a record already open for it is that one. The oldest first, should two ever be.
    /// </summary>
    public Task<Session?> ForPieceAsync(string piece, CancellationToken ct = default) => _db.RunAsync<Session?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT * FROM sessions WHERE piece = $piece AND origin IS NULL ORDER BY created LIMIT 1";
        command.Parameters.AddWithValue("$piece", piece);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>
    /// The newest of this machine's records continuing <paramref name="id"/> by <c>from</c> (a hand-over, a move, a carry-on), or null where
    /// none does: a piece's answer goes to its lead's newest successor.
    /// </summary>
    public Task<Session?> ContinuedByAsync(string id, CancellationToken ct = default) => _db.RunAsync<Session?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT * FROM sessions WHERE from_session = $id AND origin IS NULL ORDER BY created DESC, rowid DESC LIMIT 1";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>
    /// The record a quest's <c>PublishedBy</c> names: this machine's by that id, else a teammate's mirror, keyed
    /// <c>origin/id</c>, since the publisher's own machine wrote the id it knew the session by. Ids are random and 32 bits wide, so the
    /// first match stands; this machine's own is preferred.
    /// </summary>
    public Task<Session?> FindOrMirrorAsync(string id, CancellationToken ct = default) => _db.RunAsync<Session?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT * FROM sessions WHERE id = $id OR (origin IS NOT NULL AND substr(id, instr(id, '/') + 1) = $id)
            ORDER BY origin IS NOT NULL, revision LIMIT 1
            """;
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>
    /// The conversation that took <paramref name="questId"/>: this machine's first, else a teammate's
    /// mirror, the earliest taker standing. Null when none recorded one, which is every chat from before the field.
    /// </summary>
    public Task<Session?> FindChatTookAsync(string questId, CancellationToken ct = default) => _db.RunAsync<Session?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT * FROM sessions WHERE took_quest = $quest COLLATE NOCASE AND quest IS NULL
            ORDER BY origin IS NOT NULL, created, rowid LIMIT 1
            """;
        command.Parameters.AddWithValue("$quest", questId.TrimStart('#'));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>
    /// The newest of THIS machine's records for a quest, or null — whether a session here left it
    /// unfinished is what lets one carry it on. A teammate's record is their machine's run.
    /// </summary>
    public Task<Session?> LastOwnForQuestAsync(string quest, CancellationToken ct = default) => _db.RunAsync<Session?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT * FROM sessions WHERE quest = $quest AND origin IS NULL
            ORDER BY created DESC, rowid DESC LIMIT 1
            """;
        command.Parameters.AddWithValue("$quest", quest);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>
    /// The id alone of the newest of THIS machine's records for a quest, or null: the head walk's second probe, on
    /// <c>sessions_quest</c>, for a driven part with no successor, which is most often that newest record itself and so needs no more of the
    /// row than its id inside the say door's step.
    /// </summary>
    public Task<string?> LastOwnIdForQuestAsync(string quest, CancellationToken ct = default) => _db.RunAsync<string?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT id FROM sessions WHERE quest = $quest AND origin IS NULL ORDER BY created DESC, rowid DESC LIMIT 1";
        command.Parameters.AddWithValue("$quest", quest);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
    }, ct);

    /// <summary>
    /// Whether a session of THIS machine's took <paramref name="quest"/> through its own connector:
    /// the take is held here, so a person's stop of its session leaves it this machine's to
    /// carry on once released. A teammate's record never says so: its take is its machine's.
    /// </summary>
    public Task<bool> TookHereAsync(string quest, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT 1 FROM sessions WHERE quest = $quest AND origin IS NULL AND took = 1 LIMIT 1";
        command.Parameters.AddWithValue("$quest", quest);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not null;
    }, ct);

    /// <summary>
    /// THIS machine's session with a process on <paramref name="quest"/>, or about to have one, or null: driven for it, or a conversation
    /// that took it. What holds the person's bring-forward of a lost take until the losing session has ended,
    /// so its late moves cannot stand on the fresh take. <b>Parked does not count</b>, as for
    /// <see cref="RunningInTreeAsync"/>: a parked session asked the person and ended, and has no process to move.
    /// </summary>
    public Task<Session?> RunningForQuestAsync(string quest, CancellationToken ct = default) => _db.RunAsync<Session?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT * FROM sessions
            WHERE (quest = $quest OR (quest IS NULL AND took_quest = $quest COLLATE NOCASE))
              AND state IN ('Queued', 'Starting', 'Working') AND origin IS NULL
            ORDER BY created DESC, rowid DESC LIMIT 1
            """;
        command.Parameters.AddWithValue("$quest", quest.TrimStart('#'));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>
    /// The newest teammate's record for a quest that did not stand down, or null: what names another machine's take,
    /// since the quest's log names a machine by an id no person reads. A stand-down says its machine has no take.
    /// </summary>
    public Task<Session?> LastTeammateForQuestAsync(string quest, CancellationToken ct = default) => _db.RunAsync<Session?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT * FROM sessions WHERE quest = $quest AND origin IS NOT NULL AND state <> $stoodDown
            ORDER BY created DESC, rowid DESC LIMIT 1
            """;
        command.Parameters.AddWithValue("$quest", quest);
        command.Parameters.AddWithValue("$stoodDown", nameof(SessionState.StoodDown));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>
    /// A record of a session started for <paramref name="quest"/> — this machine's or a teammate's, in
    /// any state — or null. A quest a record names is not deleted: the record would name nothing.
    /// </summary>
    public Task<Session?> AnyForQuestAsync(string quest, CancellationToken ct = default) => _db.RunAsync<Session?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT * FROM sessions WHERE quest = $quest ORDER BY created, rowid LIMIT 1";
        command.Parameters.AddWithValue("$quest", quest);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>Every quest a session record names — <see cref="AnyForQuestAsync"/> for a whole list at once.</summary>
    public Task<IReadOnlySet<string>> QuestsNamedAsync(CancellationToken ct = default) => _db.RunAsync<IReadOnlySet<string>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT DISTINCT quest FROM sessions WHERE quest IS NOT NULL";

        var named = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) named.Add(reader.GetString(0));
        return named;
    }, ct);

    /// <summary>
    /// The session holding a working tree, if any — the one-session-per-TREE question.
    /// </summary>
    /// <param name="tree">
    /// Which tree is being asked about. Null is the repository's main tree, path unknown.
    /// </param>
    /// <remarks>
    /// <b>Unknown means "possibly yours", on either side.</b> A stored row with no tree — anything
    /// from before trees are stored, or a record mirrored from a machine that rightly sent no path — holds every
    /// tree in its repository; and an ask that names no tree is answered by any active session there.
    /// The lock errs toward refusing, because a refusal is a sentence naming what holds the tree and
    /// the other way round is two agents in one working tree.
    ///
    /// <para><b>The team's records are not this machine's lock</b>. A teammate's
    /// session holds a tree on THEIR machine — the tree is the unit of exclusion, and it is per machine
    /// by definition — so a record that came down with an origin is never counted here, however
    /// active it is. Counting it would let a machine that went quiet mid-session lock a repository on
    /// every other machine for good.</para>
    /// </remarks>
    public Task<Session?> ActiveForAsync(
        string repository, string? tree = null, CancellationToken ct = default) => _db.RunAsync<Session?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"""
            SELECT * FROM sessions
            WHERE repository = $repository AND state IN ({ActiveStates}) AND origin IS NULL
              AND ($tree IS NULL OR tree IS NULL OR tree = $tree)
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$repository", repository);
        command.Parameters.AddWithValue("$tree", (object?)Trees.Normalize(tree) ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>
    /// This machine's completed records in <paramref name="tree"/> whose note carries <paramref name="code"/>, matched as
    /// <see cref="ActiveForAsync"/> matches a tree: the sessions that ended waiting on their own helper, which keep their tree
    /// while their quest is taken. Whether it is, is the caller's to read: the quests are not this store's.
    /// </summary>
    public Task<IReadOnlyList<Session>> CompletedWithCodeAsync(
        string repository, string? tree, string code, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<Session>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT * FROM sessions
            WHERE repository = $repository AND state = 'Completed' AND origin IS NULL AND quest IS NOT NULL
              AND note_parts LIKE $code ESCAPE '\'
              AND ($tree IS NULL OR tree IS NULL OR tree = $tree)
            """;
        command.Parameters.AddWithValue("$repository", repository);
        command.Parameters.AddWithValue("$tree", (object?)Trees.Normalize(tree) ?? DBNull.Value);
        command.Parameters.AddWithValue("$code", $"%\"{code.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}\"%");
        var found = new List<Session>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) found.Add(Read(reader));
        return found;
    }, ct);

    /// <summary>
    /// The session with a PROCESS in this directory, whichever record it belongs to — the intake
    /// room's lock, where every ask in a circle is a different "repository" in one room.
    /// </summary>
    /// <remarks>
    /// <b>Parked does not count here</b>, unlike <see cref="ActiveForAsync"/>. The tree lock keeps a
    /// parked session's tree because its git state is work in flight; a parked intake has no process
    /// and no tree — it asked the person and ended — and counting it would let one unanswered question
    /// stop every later ask in the circle.
    /// </remarks>
    public Task<Session?> RunningInTreeAsync(string tree, CancellationToken ct = default) => _db.RunAsync<Session?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT * FROM sessions
            WHERE tree = $tree AND state IN ('Queued', 'Starting', 'Working') AND origin IS NULL
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$tree", (object?)Trees.Normalize(tree) ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>
    /// This machine's session of <paramref name="repository"/> running in <paramref name="workspace"/>, if any: Ask Daoris's
    /// one at a time, which is per workspace and not per tree, since every
    /// workspace's conversation runs in the one room. A team's record is never this machine's, as for the tree's lock.
    /// </summary>
    /// <param name="parked">Whether a parked record counts, as the tree's lock counts it for going on (<see cref="ActiveForAsync"/>).</param>
    public Task<Session?> RunningInWorkspaceAsync(
        string repository, string workspace, bool parked = false, CancellationToken ct = default) => _db.RunAsync<Session?>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"""
            SELECT * FROM sessions
            WHERE repository = $repository AND workspace = $workspace COLLATE NOCASE AND origin IS NULL
              AND state IN ({(parked ? ActiveStates : "'Queued', 'Starting', 'Working'")})
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$repository", repository);
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }, ct);

    /// <summary>
    /// Sessions in one repository, or everywhere. Live work first, closed records on request —
    /// the same shape as the quest list, for the same reason.
    /// </summary>
    public Task<IReadOnlyList<Session>> ListAsync(
        string? repository = null, bool includeClosed = false, string? workspace = null,
        CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<Session>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"""
            SELECT * FROM sessions
            WHERE ($repository IS NULL OR repository = $repository)
              AND ($workspace IS NULL OR workspace = $workspace COLLATE NOCASE)
              {(includeClosed ? "" : $"AND state IN ({ActiveStates})")}
            ORDER BY CASE WHEN state IN ({ActiveStates}) THEN 0 ELSE 1 END, created
            """;
        command.Parameters.AddWithValue("$repository", (object?)repository ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$workspace", workspace is null ? DBNull.Value : Workspaces.Normalize(workspace));

        var sessions = new List<Session>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) sessions.Add(Read(reader));
        return sessions;
    }, ct);

    // ——— Records both ways, by cursor.

    /// <summary>
    /// This machine's OWN records in a workspace written after <paramref name="revision"/>, in the
    /// order they were written, each with its revision — what a push sends. A record from the team is
    /// never here: it is somebody else's to feed, and feeding it would launder it through this key.
    /// </summary>
    public Task<IReadOnlyList<(Session Session, long Revision)>> OwnChangedSinceAsync(
        long revision, string workspace, CancellationToken ct = default) => _db.RunAsync<IReadOnlyList<(Session Session, long Revision)>>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT *, revision AS rev FROM sessions
            WHERE origin IS NULL AND revision > $revision AND workspace = $workspace COLLATE NOCASE
            ORDER BY revision
            """;
        command.Parameters.AddWithValue("$revision", revision);
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));

        var changed = new List<(Session, long)>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            changed.Add((Read(reader), reader.GetInt64(reader.GetOrdinal("rev"))));
        }

        return changed;
    }, ct);

    /// <summary>
    /// A remote's side: the team's records it holds after <paramref name="since"/>, in revision order,
    /// one page at a time — leaving out <paramref name="caller"/>'s own, which it already has, while
    /// still moving the page past them so they are not scanned again.
    /// </summary>
    public Task<SessionFetch> TeamSinceAsync(
        long since, string? caller, int limit = 500, CancellationToken ct = default) => _db.RunAsync<SessionFetch>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            SELECT *, revision AS rev FROM sessions
            WHERE origin IS NOT NULL AND revision > $since
            ORDER BY revision LIMIT $take
            """;
        command.Parameters.AddWithValue("$since", since);
        command.Parameters.AddWithValue("$take", limit + 1);

        var scanned = new List<(Session Session, long Revision)>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            scanned.Add((Read(reader), reader.GetInt64(reader.GetOrdinal("rev"))));
        }

        var more = scanned.Count > limit;
        var page = more ? scanned[..limit] : scanned;
        return new SessionFetch(
            page.Where(s => !string.Equals(s.Session.Origin, caller, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Session)
                .ToList(),
            page.Count == 0 ? since : page[^1].Revision,
            more);
    }, ct);

    /// <summary>How far this machine has pushed its own records to a workspace's remote, and fetched the team's.</summary>
    public Task<(long Pushed, long Fetched)> CursorAsync(string workspace, CancellationToken ct = default) => _db.RunAsync<(long Pushed, long Fetched)>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT pushed, fetched FROM session_cursor WHERE workspace = $workspace";
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? (reader.GetInt64(0), reader.GetInt64(1)) : (0, 0);
    }, ct);

    // ——— A session's review on its record: blind, like every write here; the ledger and the exchange judge.

    /// <summary>What this machine's record <paramref name="id"/> keeps of its review; <see cref="SessionReview.None"/> for none.</summary>
    public Task<SessionReview> ReviewOfAsync(string id, CancellationToken ct = default) => _db.RunAsync<SessionReview>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT review FROM sessions WHERE id = $id AND origin IS NULL";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is string json
            ? SessionReviewJson.ReviewOf(json)
            : SessionReview.None;
    }, ct);

    /// <summary>
    /// How many set-ups this machine's sessions on <paramref name="quest"/> said, posted or not: what a set-up step's done is
    /// judged by, before its driver has read any commit (design §2.6).
    /// </summary>
    public Task<int> SetUpsSaidForAsync(string quest, CancellationToken ct = default) => _db.RunAsync<int>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "SELECT review FROM sessions WHERE quest = $quest AND origin IS NULL AND review IS NOT NULL";
        command.Parameters.AddWithValue("$quest", quest);
        var said = 0;
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) said += SessionReviewJson.ReviewOf(reader.GetString(0)).Said.Count;
        return said;
    }, ct);
}
