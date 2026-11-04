using System.Text.Json;
using static Daoris.Knowledge.SessionRowCodec;
using static Daoris.Knowledge.SessionSchema;

namespace Daoris.Knowledge;

/// <summary>
/// The writes to the sessions table that make, move and mark a record: a new attempt, a state move, a teammate's mirror, a take, the
/// machine-local facts that never travel, a delete, and the sync cursors. Blind: whether any of it may be done is the ledger's to judge.
/// </summary>
internal sealed class SessionWrites(SessionStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>
    /// Record a new attempt, queued. Random id: two attempts at one quest are two records — and a
    /// chat, which may name no quest at all, is the same record with a different way in (D49 §3).
    /// </summary>
    public Task<Session> CreateAsync(
        string? quest, string repository, string adapter, DateTimeOffset now,
        string? workspace = null, SessionKind kind = SessionKind.Driven,
        string? harnessVersion = null, string? profile = null, string? tree = null,
        string? baseCommit = null, CancellationToken ct = default, string? ask = null, string? opinion = null,
        string? serves = null, string? from = null, string? fromWhy = null, string? fromBy = null,
        string? entryOrigin = null, string? entryPart = null, bool ownEntry = false, string? helpMode = null,
        IReadOnlyDictionary<string, string>? access = null, string? piece = null, string? actor = null) => _db.RunAsync<Session>(async () =>
    {
        // A why or an asker with no session continued is no link (MIXAGENT1a-iii): the three are kept together or not at all.
        var continues = Blank(from);
        var id = Guid.NewGuid().ToString("N")[..8];
        var session = new Session(
            id, quest, repository, adapter,
            SessionState.Queued, null, null, null, now, now, Workspaces.Normalize(workspace), kind,
            // Written at creation and never again: these say what the spawn ran ON, AS and IN, and a
            // later state change is about how it ended, not about what it was.
            Blank(harnessVersion), Blank(profile), Trees.Normalize(tree), Blank(baseCommit))
        {
            Ask = Blank(ask),
            Actor = Blank(actor),
            Opinion = Blank(opinion),
            Serves = Blank(serves),
            // A piece means nothing without the lead it serves (COAGENT1b): kept together or not at all.
            Piece = Blank(serves) is null ? null : Blank(piece),
            From = continues,
            FromWhy = continues is null ? null : Blank(fromWhy),
            FromBy = continues is null ? null : Blank(fromBy),
            // SESSONE1a: the key is the ledger's to decide, since the rule reads quests; this store writes what it is told. A caller that
            // tells it nothing leaves a record as one from before, which the reader answers a derived key and never writes back.
            Entry = ownEntry ? SessionEntries.Compose($"session:{id}", entryPart ?? repository)
                : Blank(entryOrigin) is { } origin ? SessionEntries.Compose(origin, entryPart ?? repository) : null,
        };
        // HELPMODE1a2: an Ask Daoris record told none reads as one from before does, restricted; any other record has none.
        // AUTONOMY1a: the access setup as told, which the ledger judged a short map of words; none told keeps none.
        session = session with { HelpMode = Blank(helpMode) ?? HelpModeRead(repository, null), Access = access is { Count: > 0 } ? access : null };

        await using var command = _db.Command();
        command.CommandText = $"""
            INSERT INTO sessions (id, quest, repository, adapter, state, note, evidence, transcript, created, updated, workspace, kind, harness_version, profile, tree, base_commit, ask, opinion, serves, piece, from_session, from_why, from_by, entry, help_mode, access, actor, revision)
            VALUES ($id, $quest, $repository, $adapter, $state, NULL, NULL, NULL, $created, $updated, $workspace, $kind, $harnessVersion, $profile, $tree, $baseCommit, $ask, $opinion, $serves, $piece, $from, $fromWhy, $fromBy, $entry, $helpMode, $access, $actor, {NextRevision})
            """;
        command.Parameters.AddWithValue("$entry", (object?)session.Entry ?? DBNull.Value);
        command.Parameters.AddWithValue("$actor", (object?)session.Actor ?? DBNull.Value);
        command.Parameters.AddWithValue("$access", (object?)SessionAccess.Written(session.Access) ?? DBNull.Value);
        // What it was told only: a record told none keeps none, and reads as one from before.
        command.Parameters.AddWithValue("$helpMode", (object?)Blank(helpMode) ?? DBNull.Value);
        command.Parameters.AddWithValue("$serves", (object?)session.Serves ?? DBNull.Value);
        command.Parameters.AddWithValue("$piece", (object?)session.Piece ?? DBNull.Value);
        command.Parameters.AddWithValue("$from", (object?)session.From ?? DBNull.Value);
        command.Parameters.AddWithValue("$fromWhy", (object?)session.FromWhy ?? DBNull.Value);
        command.Parameters.AddWithValue("$fromBy", (object?)session.FromBy ?? DBNull.Value);
        command.Parameters.AddWithValue("$ask", (object?)session.Ask ?? DBNull.Value);
        command.Parameters.AddWithValue("$opinion", (object?)session.Opinion ?? DBNull.Value);
        command.Parameters.AddWithValue("$workspace", session.Workspace);
        command.Parameters.AddWithValue("$kind", session.Kind.ToString());
        command.Parameters.AddWithValue("$harnessVersion", (object?)session.HarnessVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("$profile", (object?)session.Profile ?? DBNull.Value);
        command.Parameters.AddWithValue("$tree", (object?)session.Tree ?? DBNull.Value);
        command.Parameters.AddWithValue("$baseCommit", (object?)session.BaseCommit ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", session.Id);
        command.Parameters.AddWithValue("$quest", (object?)session.Quest ?? DBNull.Value);
        command.Parameters.AddWithValue("$repository", session.Repository);
        command.Parameters.AddWithValue("$adapter", session.Adapter);
        command.Parameters.AddWithValue("$state", session.State.ToString());
        command.Parameters.AddWithValue("$created", Moments.Write(session.Created));
        command.Parameters.AddWithValue("$updated", Moments.Write(session.Updated));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return session;
    }, ct);

    /// <summary>
    /// Move a session and attach what the move carries. An attachment left null keeps its old value:
    /// a later move must not erase the transcript an earlier one recorded.
    /// </summary>
    /// <param name="interrupted">That this move ends it not by the person's hand (D104) — kept once said.</param>
    /// <param name="limit">That an account's limit refused its turn (TOOL4c) — kept once said.</param>
    /// <param name="clearSaid">That the person's words go with this move (ANSWER1b, MSG1a): the ledger's to say.</param>
    /// <param name="forgive">
    /// That this move takes an ended record out of its ended state (MSG1a, D137 §2.3), so what ended it — a stop that was
    /// not the person's, a limit's failure — says nothing of the run that follows: both start unsaid again.
    /// </param>
    /// <param name="noteParts">
    /// The note's parts, written with the note (LANG1a): a note written without them clears the record's, so an older
    /// driver's note never sits beside stale parts; a move that writes no note keeps both.
    /// </param>
    public Task<Session?> SetStateAsync(
        string id, SessionState state, string? note, string? evidence, string? transcript,
        DateTimeOffset now, CancellationToken ct = default, bool interrupted = false, bool limit = false,
        bool clearSaid = false, bool forgive = false, string? noteParts = null) => _db.RunAsync<Session?>(async () =>
    {
        var session = await store.FindAsync(id, ct).ConfigureAwait(false);
        if (session is null) return null;

        var moved = session with
        {
            State = state,
            Note = note ?? session.Note,
            NoteParts = note is null ? session.NoteParts : noteParts,
            Evidence = evidence ?? session.Evidence,
            Transcript = transcript ?? session.Transcript,
            Updated = now,
            Interrupted = interrupted || (!forgive && session.Interrupted),
            Limit = limit || (!forgive && session.Limit),
            Said = clearSaid ? [] : session.Said,
        };

        // The words are cleared or left alone, never rewritten from what was read: a move is about the state.
        await using var command = _db.Command();
        command.CommandText = $"""
            UPDATE sessions SET state = $state, note = $note, note_parts = $noteParts, evidence = $evidence,
              transcript = $transcript, updated = $updated, interrupted = $interrupted, limited = $limited,
              said = CASE WHEN $clear THEN NULL ELSE said END,
              revision = {NextRevision} WHERE id = $id
            """;
        command.Parameters.AddWithValue("$noteParts", (object?)moved.NoteParts ?? DBNull.Value);
        command.Parameters.AddWithValue("$clear", clearSaid ? 1 : 0);
        command.Parameters.AddWithValue("$interrupted", moved.Interrupted ? 1 : (object)DBNull.Value);
        command.Parameters.AddWithValue("$limited", moved.Limit ? 1 : (object)DBNull.Value);
        command.Parameters.AddWithValue("$state", moved.State.ToString());
        command.Parameters.AddWithValue("$note", (object?)moved.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$evidence", (object?)moved.Evidence ?? DBNull.Value);
        command.Parameters.AddWithValue("$transcript", (object?)moved.Transcript ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", Moments.Write(moved.Updated));
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return moved;
    }, ct);

    /// <summary>
    /// Copy another machine's session record into this store, whole (D47 §6). The judgement already ran
    /// where the process lived — the ledger's rules governed the original — so a fed record upserts
    /// verbatim and is never re-judged. The caller keys it by origin + id and names the origin; the
    /// transcript never arrives, because no wire has a field for a machine path.
    /// </summary>
    public Task MirrorAsync(Session record, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = $"""
            INSERT INTO sessions (id, quest, repository, adapter, state, note, note_parts, evidence, transcript, created, updated, workspace, kind, harness_version, profile, tree, origin, limited, entry, took_quest, revision)
            VALUES ($id, $quest, $repository, $adapter, $state, $note, $noteParts, $evidence, NULL, $created, $updated, $workspace, $kind, $harnessVersion, NULL, NULL, $origin, $limited, $entry, $tookQuest, {NextRevision})
            ON CONFLICT (id) DO UPDATE SET
              took_quest = COALESCE($tookQuest, took_quest),
              state = $state, note = $note, note_parts = $noteParts, evidence = $evidence, updated = $updated, workspace = $workspace,
              kind = $kind, harness_version = $harnessVersion, origin = $origin, limited = $limited,
              entry = COALESCE($entry, entry),
              revision = {NextRevision}
            """;
        // SESSONE1a: the entry's key travels with the record (it names a line of work and a repository). A record from a machine from before
        // it brings none, which keeps whatever this one holds and otherwise leaves the reader to derive it.
        command.Parameters.AddWithValue("$entry", (object?)Blank(record.Entry) ?? DBNull.Value);
        // CHATTAKE1: the quest a chat took travels with it; none from a machine from before.
        command.Parameters.AddWithValue("$tookQuest", (object?)Blank(record.TookQuest) ?? DBNull.Value);
        // A limit names no account (TOOL4c), so it is copied like the state beside it.
        command.Parameters.AddWithValue("$limited", record.Limit ? 1 : (object)DBNull.Value);
        // The note's parts travel with the note, already cleaned where they left their machine (LANG1a).
        command.Parameters.AddWithValue("$noteParts", (object?)record.NoteParts ?? DBNull.Value);
        // Whose record: named, or read from the id a mirror is always keyed by — never empty, because
        // a mirrored row with no origin would count as this machine's own and hold its trees.
        command.Parameters.AddWithValue(
            "$origin", record.Origin ?? (record.Id.IndexOf('/') is > 0 and var slash ? record.Id[..slash] : record.Id));
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(record.Workspace));
        command.Parameters.AddWithValue("$kind", record.Kind.ToString());
        // The version crosses; the PROFILE NAME and the TREE never do — machine-local, like the
        // transcript beside them (D47 §4, D51). Written as literal NULLs rather than from the record,
        // so a caller that filled either field cannot make it travel by accident.
        command.Parameters.AddWithValue("$harnessVersion", (object?)record.HarnessVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$quest", (object?)record.Quest ?? DBNull.Value);
        command.Parameters.AddWithValue("$repository", record.Repository);
        command.Parameters.AddWithValue("$adapter", record.Adapter);
        command.Parameters.AddWithValue("$state", record.State.ToString());
        command.Parameters.AddWithValue("$note", (object?)record.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$evidence", (object?)record.Evidence ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", Moments.Write(record.Created));
        command.Parameters.AddWithValue("$updated", Moments.Write(record.Updated));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>
    /// Mark that this session took its quest itself (STANDDOWN2) — said by the session's own connector
    /// at the moment of the take. False when there is no such record of this machine's.
    /// </summary>
    public Task<bool> MarkTookAsync(string id, string? quest = null, CancellationToken ct = default, string? bind = null) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        // CHATTAKE1: a chat has no quest of its own, so the quest it took is named here, which keeps an earlier take's id when none is said.
        // The revision moves only with a quest named, so the team's copy of the chat learns it at the next push.
        command.CommandText = quest is null
            ? "UPDATE sessions SET took = 1 WHERE id = $id AND origin IS NULL"
            : $"UPDATE sessions SET took = 1, took_quest = $quest, revision = {NextRevision} WHERE id = $id AND origin IS NULL";
        command.Parameters.AddWithValue("$id", id);
        if (quest is not null) command.Parameters.AddWithValue("$quest", quest);
        if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 0) return false;

        // CLOSETAKE1: why the take's graph did not bind, kept on this machine's record alone (no revision: it is not pushed).
        if (bind is not null)
        {
            await using var kept = _db.Command();
            kept.CommandText = "UPDATE sessions SET took_bind = $bind WHERE id = $id AND origin IS NULL";
            kept.Parameters.AddWithValue("$id", id);
            kept.Parameters.AddWithValue("$bind", bind);
            await kept.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return true;
    }, ct);

    /// <summary>D205's later door: this machine's record only, without a sync revision. Omitted members keep their value.</summary>
    public Task<bool> KeepActorAsync(string id, string? choice, JsonElement? asked, JsonElement? changed, JsonElement? ran,
        JsonElement? chosen = null, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            UPDATE sessions SET choice = COALESCE($choice, choice), asked = COALESCE($asked, asked),
              actor_override = COALESCE($override, actor_override), ran = COALESCE($ran, ran), chosen = COALESCE($chosen, chosen)
            WHERE id = $id AND origin IS NULL
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$choice", (object?)choice ?? DBNull.Value);
        command.Parameters.AddWithValue("$asked", (object?)asked?.GetRawText() ?? DBNull.Value);
        command.Parameters.AddWithValue("$override", (object?)changed?.GetRawText() ?? DBNull.Value);
        command.Parameters.AddWithValue("$ran", (object?)ran?.GetRawText() ?? DBNull.Value);
        command.Parameters.AddWithValue("$chosen", (object?)chosen?.GetRawText() ?? DBNull.Value);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>
    /// Add the downloaded versions a start of this machine's record was handed (KEEP1b) to those its earlier starts were, in order, in one
    /// read and write. Blind, like every write here: that each is a version's key is the ledger's to judge. No revision, since nothing that
    /// travels changed. False when there is no such record of this machine's.
    /// </summary>
    public Task<bool> KeepResolvedAsync(string id, IReadOnlyList<string> versions, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        string? kept;
        await using (var read = _db.Command())
        {
            read.CommandText = "SELECT resolved FROM sessions WHERE id = $id AND origin IS NULL";
            read.Parameters.AddWithValue("$id", id);
            await using var row = await read.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await row.ReadAsync(ct).ConfigureAwait(false)) return false;
            kept = row.IsDBNull(0) ? null : row.GetString(0);
        }

        var all = (ResolvedRead(kept) ?? []).Concat(versions).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        await using var command = _db.Command();
        command.CommandText = "UPDATE sessions SET resolved = $resolved WHERE id = $id AND origin IS NULL";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$resolved", JsonSerializer.Serialize(all));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>
    /// Write the access mode an Ask Daoris record of this machine's runs under (HELPMODE1a2). Blind, like every write here: which mode
    /// is the ledger's to judge. No revision, since nothing that travels changed. False when there is no such record.
    /// </summary>
    public Task<bool> KeepHelpModeAsync(string id, string mode, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "UPDATE sessions SET help_mode = $mode WHERE id = $id AND origin IS NULL AND repository = $help";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$mode", mode);
        command.Parameters.AddWithValue("$help", SessionLedger.HelpRepository);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>
    /// Write the access setup a driven record of this machine's now runs under (AUTONOMY1b2). Blind, like every write here: that it is a
    /// short map of words is the ledger's to judge. No revision, since nothing that travels changed. False when there is no such record.
    /// </summary>
    public Task<bool> KeepAccessAsync(string id, IReadOnlyDictionary<string, string> access, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "UPDATE sessions SET access = $access WHERE id = $id AND origin IS NULL AND kind = $kind";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$access", (object?)SessionAccess.Written(access) ?? DBNull.Value);
        command.Parameters.AddWithValue("$kind", SessionKind.Driven.ToString());
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>
    /// Write this machine's record's review whole. A read, then the write it decides: the ledger and the exchange call it inside
    /// <see cref="SessionStore.ExclusiveAsync{T}"/> (REV3). No revision, since nothing that travels changed. False when there is no such record.
    /// </summary>
    public Task<bool> KeepReviewAsync(string id, SessionReview review, CancellationToken ct = default) => _db.RunAsync<bool>(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = "UPDATE sessions SET review = $review WHERE id = $id AND origin IS NULL";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$review", SessionReviewJson.ReviewJson(review));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }, ct);

    /// <summary>
    /// Mark these records as gone up to a remote (SESSUX1f, D126 §5.4), once a push took them. No revision moves: the mark
    /// is this machine's fact about the record, not a change to it, so the next push does not send it again. An id with
    /// no record of this machine's is passed over.
    /// </summary>
    public Task MarkPushedAsync(IEnumerable<string> ids, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            await using var command = _db.Command();
            command.CommandText = "UPDATE sessions SET pushed = 1 WHERE id = $id AND origin IS NULL";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }, ct);

    /// <summary>Move a workspace's cursors forward — never back, so a late answer cannot re-send the past.</summary>
    public Task AdvanceCursorAsync(
        string workspace, long? pushed = null, long? fetched = null, CancellationToken ct = default) => _db.RunAsync(async () =>
    {
        await using var command = _db.Command();
        command.CommandText = """
            INSERT INTO session_cursor (workspace, pushed, fetched) VALUES ($workspace, $pushed, $fetched)
            ON CONFLICT (workspace) DO UPDATE SET
              pushed = MAX(pushed, excluded.pushed), fetched = MAX(fetched, excluded.fetched)
            """;
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));
        command.Parameters.AddWithValue("$pushed", pushed ?? 0);
        command.Parameters.AddWithValue("$fetched", fetched ?? 0);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }, ct);

    /// <summary>
    /// Remove a record whole (SESSUX1f, D126 §5.4). Blind, like every write here: whether a record may go is the ledger's
    /// judgement, made before this is asked. False when there was none.
    /// <para>KEEPSIZE1b (the keep-size design §2.2): its links to finished trees' records go with it, and a record left with no session,
    /// in the same transaction, as the history clear's and the single delete's callers hold it.</para>
    /// </summary>
    public Task<bool> DeleteAsync(string id, CancellationToken ct = default) => _db.InTransactionAsync<bool>(async (_, inside) =>
    {
        await using var command = _db.Command();
        command.CommandText = "DELETE FROM sessions WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        var deleted = await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false) > 0;
        await WorkRecordStore.ForgetSessionAsync(_db, id, inside).ConfigureAwait(false);
        return deleted;
    }, ct);

    /// <summary>
    /// The finished trees' records no session names in <paramref name="workspace"/>, kept at or before <paramref name="through"/>, gone with a
    /// clear of history (KEEPSIZE1b, the keep-size design §2.2), inside its transaction; how many went.
    /// </summary>
    public Task<int> ForgetOrphanRecordsAsync(string workspace, DateTimeOffset through, CancellationToken ct = default) =>
        _db.RunAsync(() => WorkRecordStore.ForgetOrphansAsync(_db, workspace, through, ct), ct);
}
