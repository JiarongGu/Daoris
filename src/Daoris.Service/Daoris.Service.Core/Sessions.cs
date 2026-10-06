using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// Where a driver-started session is in its life. Observed, never self-reported: the driver moves a
/// session by what it can see — process lifetime, and the quest's own transitions.
/// </summary>
public enum SessionState
{
    /// <summary>The driver decided to start this; waiting on the repository's slot, hold, or tree.</summary>
    Queued,

    /// <summary>The process is being spawned.</summary>
    Starting,

    /// <summary>The process is alive.</summary>
    Working,

    /// <summary>Parked at a checkpoint only the person can clear. Still holds its repository.</summary>
    AwaitingPerson,

    /// <summary>Exited; its quest reached Done.</summary>
    Completed,

    /// <summary>Exited; its quest reached Declined, with the reason on the quest.</summary>
    Declined,

    /// <summary>Exited without working: the quest was already taken or closed by someone else.</summary>
    StoodDown,

    /// <summary>The process died, or exited with the quest unexplained.</summary>
    Failed,

    /// <summary>
    /// The person cancelled it — or, where the record says <see cref="Session.Interrupted"/>, the orphan
    /// sweep or the driver's shutdown ended it (D104).
    /// </summary>
    Stopped,
}

/// <summary>How a session was entered — the one thing that differs between them (D49 §3).</summary>
public enum SessionKind
{
    /// <summary>Planned from a quest by the driver: the loop D46 built.</summary>
    Driven,

    /// <summary>
    /// Opened by a person, to talk. It may serve no quest at all, may take one mid-conversation
    /// through its own connector, or may end by publishing new ones — the quest system is where work
    /// lands, not the toll to start talking.
    /// </summary>
    Chat,
}

/// <param name="Id">Short random handle — an attempt, so never content-derived: a retry is a new record.</param>
/// <param name="Quest">
/// The quest this session was started to serve — <b>null for a chat</b>, which may serve none (D49 §3).
/// A driven session always has one: it is what the driver planned from.
/// </param>
/// <param name="Repository">The repository it runs in — the quest's receiver, denormalized for listing.</param>
/// <param name="Adapter">Which harness adapter spawned it. A name for the record, never a model (D24).</param>
/// <param name="State">Where it is.</param>
/// <param name="Note">What the driver observed, when a state needs explaining.</param>
/// <param name="Evidence">The outcome bundle the person reviews — gate results, landed commits.</param>
/// <param name="Transcript">A machine-local path to the captured run. Diagnostic, never the record.</param>
/// <param name="Created">When the driver queued it.</param>
/// <param name="Updated">When its state last moved.</param>
/// <param name="Workspace">
/// The circle this record travels within (D48) — taken from its quest, never passed alongside it, so
/// the two can never disagree. The driver itself is per machine and crosses workspaces (design §4):
/// the workspace decides where a record travels, not which machine may work it.
/// </param>
/// <param name="Kind">
/// Driven or chat (D49 §3). It changes almost nothing — the same record, the same observed lifecycle,
/// the same one-session-per-repository lock — which is the point: a conversation is a session, not a
/// second kind of thing with its own rules to keep in step.
/// </param>
/// <param name="HarnessVersion">
/// The harness version observed at spawn (D49 §4) — "which tool produced this", answerable later. The
/// same authorship instinct as version-stamping a release, applied to the tool that did the work. It
/// travels: it is a fact about a tool, not about a machine.
/// </param>
/// <param name="Profile">
/// Which named credential profile the session ran as (D49 §4) — a NAME, never a path and never
/// anything from inside the profile.
/// </param>
/// <param name="Tree">
/// The working tree this session holds (D51) — <b>the unit of exclusion</b>, and what the ledger keys
/// the lock on. Null means the repository's main tree, whose path this deployment may not know: a
/// record from before D51, or one mirrored from another machine.
/// </param>
/// <remarks>
/// <b>The profile name and the tree are machine-local, and are guarded exactly as the transcript
/// is</b> (D47 §4). The profile answers "which account did this run as" to the machine that ran it;
/// the tree is a filesystem path, which is the sharpest reason of the three. Both are wiring in the
/// sense D48 §2 draws — WHERE and WHO are the machine's and the account's. The version is a fact
/// about a tool and travels; these do not.
/// </remarks>
public sealed record Session(
    string Id,
    string? Quest,
    string Repository,
    string Adapter,
    SessionState State,
    string? Note,
    string? Evidence,
    string? Transcript,
    DateTimeOffset Created,
    DateTimeOffset Updated,
    string Workspace = Workspaces.Default,
    SessionKind Kind = SessionKind.Driven,
    string? HarnessVersion = null,
    string? Profile = null,
    string? Tree = null,
    string? BaseCommit = null)
{
    /// <summary>
    /// Whose record this is, when it is not this machine's: the key the remote knew its machine by
    /// (D47 §6). Null for this machine's own; a record that has one is the team's — its id is
    /// `origin/id`, it is read-only here, and it holds no tree on this machine (SYNC4).
    /// </summary>
    public string? Origin { get; init; }

    /// <summary>
    /// The ask this session was opened to answer — an INTAKE (D65 §1b), a conversation Daoris opened
    /// for an ask in a room it owns rather than in any repository. Null for every other session.
    /// </summary>
    /// <remarks>
    /// The kind stays <see cref="SessionKind.Chat"/>: an intake serves no quest and is never planned
    /// from one, which is exactly what a build that predates it already reads a chat as. A new kind
    /// would read as DRIVEN there — the one reading that is wrong.
    /// </remarks>
    public string? Ask { get; init; }

    /// <summary>
    /// Whether this session took its own quest, through its own connector (STANDDOWN2). A session
    /// ending with its quest still taken is then waiting on the person, not standing down for
    /// somebody else — which the quest's state alone cannot tell.
    /// </summary>
    public bool Took { get; init; }

    /// <summary>
    /// The person's words waiting for this record to go on with them, in the order said (MSG1a, D137 §2.4): an answer to
    /// a park (STANDDOWN2, ANSWER1b), or words to a session that ended, which reopen it. A second word joins the first and
    /// never replaces it. Kept until taken, by the resumed run's first prompt or the session a fallback handed them to;
    /// parking again clears them, so a session that asks again is answered anew.
    /// </summary>
    /// <remarks>
    /// <b>This machine's own, like the transcript</b> (D47 §4): answered only to a caller on it, never on the wire to a
    /// remote, and keeping them writes no revision, since nothing that travels changed.
    /// </remarks>
    public IReadOnlyList<SaidWord> Said { get; init; } = [];

    /// <summary>
    /// The words waiting, joined by a blank line, or null when none wait: what an answer was before <see cref="Said"/>
    /// (ANSWER1b), kept for every reader from before it — the driver's resume, its carry-on and the page's answered park.
    /// </summary>
    public string? Answer => Said.Count == 0 ? null : string.Join("\n\n", Said.Select(word => word.Text));

    /// <summary>
    /// A <see cref="SessionState.Stopped"/> record that was not the person's stop (D104): the orphan
    /// sweep found nothing running it, or the driver shut down under it. A take ended so is carried on
    /// like a failed one (D80); a person's stop is carried on only once they release it, and only of a take
    /// a session here made (SESSUX1b2). False for everything else, and for every record from before the
    /// field, which is the old reading.
    /// </summary>
    public bool Interrupted { get; init; }

    /// <summary>
    /// A <see cref="SessionState.Failed"/> record whose turn an account's limit refused (TOOL4c, D125 §5.2),
    /// as the driver read it from the door's failure. The quest waits for the reset or rotates rather than
    /// spending a strike on it, which is the driver's to decide. False for everything else, and for every
    /// record from before the field, which is the old reading: a failure like any other.
    /// </summary>
    /// <remarks>
    /// <b>It names no account</b>, so unlike <see cref="Profile"/> it is served to every caller and travels
    /// with the record: a teammate reading a quest's sessions sees that one was cut off by a limit, never
    /// whose.
    /// </remarks>
    public bool Limit { get; init; }

    /// <summary>
    /// Whether this machine's record went up to a workspace's remote (SESSUX1f, D126 §5.4): the team holds a copy, and a
    /// session record does not travel as a deletion, so a delete here would leave theirs. Kept once said, through every
    /// later move, since the copy stays. This machine's own fact: it never travels.
    /// </summary>
    public bool Pushed { get; init; }

    /// <summary>
    /// The note's parts, each line's code and values beside its English (LANG1a, D142 point 2; the language design §3): a JSON
    /// list as <see cref="Knowledge.NoteParts"/> keeps it. Null for a record from before parts, and for a note written without
    /// them, whose note then stands as kept.
    /// </summary>
    /// <remarks>It travels with the note, every string in it cleaned as the note is (<see cref="Knowledge.NoteParts.ForAnotherMachine"/>).</remarks>
    public string? NoteParts { get; init; }

    /// <summary>Whether this session still holds its repository. Parked counts: the person is the flow control.</summary>
    public bool Active => State is SessionState.Queued or SessionState.Starting
        or SessionState.Working or SessionState.AwaitingPerson;

    /// <summary>This state's public spelling — what every door prints and accepts back.</summary>
    public string StateName => Spell(State);

    /// <summary>The design's spelling (D46 §4): kebab-case on the wire, enum names in the store.</summary>
    public static string Spell(SessionState state) => state switch
    {
        SessionState.AwaitingPerson => "awaiting-person",
        SessionState.StoodDown => "stood-down",
        _ => state.ToString().ToLowerInvariant(),
    };

    /// <summary>The reverse of <see cref="Spell"/>: what every door accepts back, tolerantly.</summary>
    public static bool TryParse(string value, out SessionState state) =>
        Enum.TryParse(value.Replace("-", "", StringComparison.Ordinal), ignoreCase: true, out state)
        && Enum.IsDefined(state);
}

/// <summary>One thing the person said to a session, waiting for it to go on with it (MSG1a, D137 §2.4).</summary>
/// <param name="Id">The word's own handle, so the record can say again where the session took it (D136 §3).</param>
/// <param name="Text">Their words, trimmed at the ends only.</param>
/// <param name="At">When they said it.</param>
/// <param name="Files">The names of the files they gave with it — names alone, never where the files are.</param>
/// <param name="Reopens">
/// Said after the record ended, so it goes on by reopening it; such a word is kept on its ask once taken, as
/// <c>reopened</c> (D133 §1). False for an answer to a park, which its door keeps on the ask at once.
/// </param>
public sealed record SaidWord(string Id, string Text, DateTimeOffset At, IReadOnlyList<string> Files, bool Reopens = false);

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
/// reason <see cref="QuestExchange"/> exists.</para>
/// </remarks>
public sealed class SessionStore
{
    private readonly SqliteConnection _connection;

    private SessionStore(SqliteConnection connection) => _connection = connection;

    public static async Task<SessionStore> OpenAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        var store = new SessionStore(connection);
        await store.EnsureSchemaAsync(ct).ConfigureAwait(false);
        return store;
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using (var command = _connection.CreateCommand())
        {
            command.CommandText = $"""
                CREATE TABLE IF NOT EXISTS sessions (
                  id         TEXT PRIMARY KEY,
                  -- Nullable since D49 §3: a chat may serve no quest at all.
                  quest      TEXT NULL,
                  repository TEXT NOT NULL,
                  adapter    TEXT NOT NULL,
                  state      TEXT NOT NULL,
                  note       TEXT NULL,
                  evidence   TEXT NULL,
                  transcript TEXT NULL,
                  created    TEXT NOT NULL,
                  updated    TEXT NOT NULL,
                  workspace  TEXT NOT NULL DEFAULT '{Workspaces.Default}',
                  kind       TEXT NOT NULL DEFAULT '{nameof(SessionKind.Driven)}',
                  -- D49 §4: which tool, and which account. Written once at spawn and never moved —
                  -- a record of what ran, not a field a later state change may revise.
                  harness_version TEXT NULL,
                  profile         TEXT NULL,
                  -- D51: which working tree it holds. Null means the repository's main tree, whose
                  -- path this deployment may not know — and the lock reads that conservatively.
                  tree            TEXT NULL,
                  -- SURF6: the commit this tree stood at when the spawn began, so the range the
                  -- review reads is a FACT rather than a guess reconstructed from the evidence
                  -- string. Written once at spawn like the three above. It is a repository fact, not
                  -- a machine one — but the remote feed copies a named allowlist and does not name
                  -- it, so it stays here unless someone decides otherwise.
                  base_commit     TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS sessions_repository ON sessions (repository, state);
                """;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // Records made before a column existed must survive its arrival: a session record is the
        // reviewable trace of work that actually happened, and a store that dropped them on an upgrade
        // would lose exactly the history the person reviews. Workspaces came with D48, kinds with D49,
        // the tree with D51, the base commit with SURF6.
        foreach (var (column, definition) in new[]
        {
            ("workspace", $"workspace TEXT NOT NULL DEFAULT '{Workspaces.Default}'"),
            ("kind", $"kind TEXT NOT NULL DEFAULT '{nameof(SessionKind.Driven)}'"),
            ("harness_version", "harness_version TEXT NULL"),
            ("profile", "profile TEXT NULL"),
            ("tree", "tree TEXT NULL"),
            ("base_commit", "base_commit TEXT NULL"),
        })
        {
            await SchemaColumns.EnsureAsync(_connection, "sessions", column, definition, ct).ConfigureAwait(false);
        }

        await RelaxQuestAsync(ct).ConfigureAwait(false);

        // SYNC4, after the rebuild above so it cannot drop them: whose record a row is, and the order
        // this store wrote them in. Both are DERIVED for rows that already exist — a mirrored row's id
        // already carries its origin, and the order rows were written in is the order they were
        // inserted — so a store from before the sync pushes and serves every record it holds.
        if (!await SchemaColumns.HasAsync(_connection, "sessions", "origin", ct).ConfigureAwait(false))
        {
            await using var alter = _connection.CreateCommand();
            alter.CommandText = """
                ALTER TABLE sessions ADD COLUMN origin TEXT NULL;
                UPDATE sessions SET origin = substr(id, 1, instr(id, '/') - 1) WHERE instr(id, '/') > 0;
                """;
            await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        if (!await SchemaColumns.HasAsync(_connection, "sessions", "revision", ct).ConfigureAwait(false))
        {
            await using var alter = _connection.CreateCommand();
            alter.CommandText = """
                ALTER TABLE sessions ADD COLUMN revision INTEGER NOT NULL DEFAULT 0;
                UPDATE sessions SET revision = rowid;
                """;
            await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // INT4b: the ask an intake answers. After the rebuild, like SYNC4's pair, so it cannot drop it.
        await SchemaColumns.EnsureAsync(_connection, "sessions", "ask", "ask TEXT NULL", ct).ConfigureAwait(false);

        // STANDDOWN2: whether this session took its own quest, through its own connector. A record from
        // before it says nothing, and nothing is the old reading. And the person's answer to one that
        // parked to ask them, which the session that carries the quest on is handed.
        await SchemaColumns.EnsureAsync(_connection, "sessions", "took", "took INTEGER NULL", ct).ConfigureAwait(false);
        await SchemaColumns.EnsureAsync(_connection, "sessions", "answer", "answer TEXT NULL", ct).ConfigureAwait(false);

        // MSG1a (D137 §2.4): the person's words, a JSON list, which the single answer above became. Nothing is moved at
        // the column's arrival: a record from before reads its answer as its first word, and the first word kept after
        // it writes both into this list and empties the old column, so the two never both hold words.
        await SchemaColumns.EnsureAsync(_connection, "sessions", "said", "said TEXT NULL", ct).ConfigureAwait(false);

        // D104: a stop that was not the person's — the sweep's, or a shutdown's. A record from before it
        // says nothing, and nothing is the old reading: the person's stop.
        await SchemaColumns.EnsureAsync(_connection, "sessions", "interrupted", "interrupted INTEGER NULL", ct).ConfigureAwait(false);

        // TOOL4c (D125 §5.2): a failure an account's limit made. A record from before it says nothing, and
        // nothing is the old reading: a failure like any other. `limited`, since LIMIT is SQL's own word.
        await SchemaColumns.EnsureAsync(_connection, "sessions", "limited", "limited INTEGER NULL", ct).ConfigureAwait(false);

        // LANG1a (D142 point 2): the note's parts, a JSON list beside it. A record from before says none, and its note stands
        // as kept: nothing re-reads the English, and nothing rewrites it (the language design §6).
        await SchemaColumns.EnsureAsync(_connection, "sessions", "note_parts", "note_parts TEXT NULL", ct).ConfigureAwait(false);

        await using (var cursor = _connection.CreateCommand())
        {
            cursor.CommandText = """
                CREATE TABLE IF NOT EXISTS session_cursor (
                  workspace TEXT PRIMARY KEY COLLATE NOCASE,
                  pushed    INTEGER NOT NULL DEFAULT 0,
                  fetched   INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS sessions_revision ON sessions (revision);
                """;
            await cursor.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // HIST1a: the highest revision issued, one row beside the cursor it keeps honest. The table's own maximum goes back
        // when its newest record is deleted, and the push cursor has already passed that number (H2). A store from before
        // starts the mark at the newest revision it holds, so nothing changes for one that never deleted a record. The
        // triggers move it in the statement that writes the revision, as one statement is what keeps a revision exact
        // under SQLite's write lock (see NextRevision): where no transaction is open, a second statement is a second
        // transaction, and another host could write between the two.
        await using (var mark = _connection.CreateCommand())
        {
            mark.CommandText = """
                CREATE TABLE IF NOT EXISTS session_revision (
                  one      INTEGER PRIMARY KEY CHECK (one = 1),
                  revision INTEGER NOT NULL
                );
                INSERT OR IGNORE INTO session_revision (one, revision) SELECT 1, COALESCE(MAX(revision), 0) FROM sessions;

                CREATE TRIGGER IF NOT EXISTS session_revision_written AFTER INSERT ON sessions
                BEGIN
                  UPDATE session_revision SET revision = NEW.revision WHERE one = 1 AND revision < NEW.revision;
                END;

                CREATE TRIGGER IF NOT EXISTS session_revision_moved AFTER UPDATE OF revision ON sessions
                BEGIN
                  UPDATE session_revision SET revision = NEW.revision WHERE one = 1 AND revision < NEW.revision;
                END;
                """;
            await mark.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // SESSUX1f (D126 §5.4): whether a record went up to a remote, after the cursor it is derived from. For rows that
        // already exist it is read once from that cursor: a record of this machine's at or before what its workspace
        // pushed was examined by a push, so it is marked, which errs toward refusing a delete (a record of a repository
        // that had not joined is marked too). From here on a push marks what it sent.
        if (!await SchemaColumns.HasAsync(_connection, "sessions", "pushed", ct).ConfigureAwait(false))
        {
            await using var alter = _connection.CreateCommand();
            alter.CommandText = """
                ALTER TABLE sessions ADD COLUMN pushed INTEGER NULL;
                UPDATE sessions SET pushed = 1
                WHERE origin IS NULL AND revision <= COALESCE(
                  (SELECT c.pushed FROM session_cursor c WHERE c.workspace = sessions.workspace COLLATE NOCASE), 0);
                """;
            await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The next revision, as one statement's subquery — so under SQLite's write lock no two writes can
    /// take the same number and none can land behind one already read (SYNC4's cursor rests on it).
    /// </summary>
    /// <remarks>
    /// One past the larger of the table's newest and the mark (HIST1a), which the statement's own trigger then moves: the
    /// table alone goes back when its newest record is deleted, and the push cursor would pass the next write by (H2).
    /// </remarks>
    private const string NextRevision = """
        (SELECT MAX(
           COALESCE((SELECT MAX(revision) FROM sessions), 0),
           COALESCE((SELECT revision FROM session_revision WHERE one = 1), 0)) + 1)
        """;

    /// <summary>
    /// Let `quest` be null on a table created before chats existed (D49 §3).
    /// </summary>
    /// <remarks>
    /// SQLite cannot drop a NOT NULL constraint, so the table is rebuilt and the rows are COPIED —
    /// not recreated empty. The entry store rebuilds by discarding, and may: it holds derived data.
    /// This one holds records of work that happened, which nothing can re-derive. Guarded by a probe
    /// so it runs once, on the one upgrade that needs it, and never again.
    /// </remarks>
    private async Task RelaxQuestAsync(CancellationToken ct)
    {
        await using (var probe = _connection.CreateCommand())
        {
            probe.CommandText =
                "SELECT COUNT(*) FROM pragma_table_info('sessions') WHERE name = 'quest' AND \"notnull\" = 1";
            var constrained = Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false));
            if (constrained == 0) return;
        }

        await using var rebuild = _connection.CreateCommand();
        rebuild.CommandText = $"""
            CREATE TABLE sessions_relaxed (
              id         TEXT PRIMARY KEY,
              quest      TEXT NULL,
              repository TEXT NOT NULL,
              adapter    TEXT NOT NULL,
              state      TEXT NOT NULL,
              note       TEXT NULL,
              evidence   TEXT NULL,
              transcript TEXT NULL,
              created    TEXT NOT NULL,
              updated    TEXT NOT NULL,
              workspace  TEXT NOT NULL DEFAULT '{Workspaces.Default}',
              kind       TEXT NOT NULL DEFAULT '{nameof(SessionKind.Driven)}',
              harness_version TEXT NULL,
              profile         TEXT NULL,
              tree            TEXT NULL,
              base_commit     TEXT NULL
            );
            INSERT INTO sessions_relaxed
              SELECT id, quest, repository, adapter, state, note, evidence, transcript, created,
                     updated, workspace, kind, harness_version, profile, tree, base_commit
              FROM sessions;
            DROP TABLE sessions;
            ALTER TABLE sessions_relaxed RENAME TO sessions;
            CREATE INDEX IF NOT EXISTS sessions_repository ON sessions (repository, state);
            """;
        await rebuild.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Record a new attempt, queued. Random id: two attempts at one quest are two records — and a
    /// chat, which may name no quest at all, is the same record with a different way in (D49 §3).
    /// </summary>
    /// <summary>
    /// Run a check and the write it decides as one step, holding the database's write lock throughout.
    /// </summary>
    /// <remarks>
    /// <para>Every host on a machine opens the same file: the desktop's host, and a connector for each
    /// session. The one-session-per-tree lock was a read followed by a write, so two opens at once could
    /// both read "free" and both write (REV3). <c>BEGIN IMMEDIATE</c> takes the file's write lock BEFORE
    /// the read, so a second host waits until the first has written, then reads what it wrote. The
    /// connection's gate does the same between requests inside one host.</para>
    ///
    /// <para>The work is handed an uncancellable token: once the transaction has begun, a throw would roll
    /// back whatever other statements joined it (<see cref="QuestStore"/>, REV3). It must not take the
    /// connection's gate itself, which is not reentrant.</para>
    /// </remarks>
    public async Task<T> ExclusiveAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        var gate = ConnectionGate.For(_connection);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var transaction = _connection.BeginTransaction(deferred: false);
            var result = await work(CancellationToken.None).ConfigureAwait(false);
            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Session> CreateAsync(
        string? quest, string repository, string adapter, DateTimeOffset now,
        string? workspace = null, SessionKind kind = SessionKind.Driven,
        string? harnessVersion = null, string? profile = null, string? tree = null,
        string? baseCommit = null, CancellationToken ct = default, string? ask = null)
    {
        var session = new Session(
            Guid.NewGuid().ToString("N")[..8], quest, repository, adapter,
            SessionState.Queued, null, null, null, now, now, Workspaces.Normalize(workspace), kind,
            // Written at creation and never again: these say what the spawn ran ON, AS and IN, and a
            // later state change is about how it ended, not about what it was.
            Blank(harnessVersion), Blank(profile), Trees.Normalize(tree), Blank(baseCommit))
        {
            Ask = Blank(ask),
        };

        await using var command = _connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO sessions (id, quest, repository, adapter, state, note, evidence, transcript, created, updated, workspace, kind, harness_version, profile, tree, base_commit, ask, revision)
            VALUES ($id, $quest, $repository, $adapter, $state, NULL, NULL, NULL, $created, $updated, $workspace, $kind, $harnessVersion, $profile, $tree, $baseCommit, $ask, {NextRevision})
            """;
        command.Parameters.AddWithValue("$ask", (object?)session.Ask ?? DBNull.Value);
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
        command.Parameters.AddWithValue("$created", session.Created.ToString("O"));
        command.Parameters.AddWithValue("$updated", session.Updated.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return session;
    }

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
    public async Task<Session?> SetStateAsync(
        string id, SessionState state, string? note, string? evidence, string? transcript,
        DateTimeOffset now, CancellationToken ct = default, bool interrupted = false, bool limit = false,
        bool clearSaid = false, bool forgive = false, string? noteParts = null)
    {
        var session = await FindAsync(id, ct).ConfigureAwait(false);
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
        await using var command = _connection.CreateCommand();
        command.CommandText = $"""
            UPDATE sessions SET state = $state, note = $note, note_parts = $noteParts, evidence = $evidence,
              transcript = $transcript, updated = $updated, interrupted = $interrupted, limited = $limited,
              said = CASE WHEN $clear THEN NULL ELSE said END, answer = CASE WHEN $clear THEN NULL ELSE answer END,
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
        command.Parameters.AddWithValue("$updated", moved.Updated.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return moved;
    }

    /// <summary>
    /// Copy another machine's session record into this store, whole (D47 §6). The judgement already ran
    /// where the process lived — the ledger's rules governed the original — so a fed record upserts
    /// verbatim and is never re-judged. The caller keys it by origin + id and names the origin; the
    /// transcript never arrives, because no wire has a field for a machine path.
    /// </summary>
    public async Task MirrorAsync(Session record, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO sessions (id, quest, repository, adapter, state, note, note_parts, evidence, transcript, created, updated, workspace, kind, harness_version, profile, tree, origin, limited, revision)
            VALUES ($id, $quest, $repository, $adapter, $state, $note, $noteParts, $evidence, NULL, $created, $updated, $workspace, $kind, $harnessVersion, NULL, NULL, $origin, $limited, {NextRevision})
            ON CONFLICT (id) DO UPDATE SET
              state = $state, note = $note, note_parts = $noteParts, evidence = $evidence, updated = $updated, workspace = $workspace,
              kind = $kind, harness_version = $harnessVersion, origin = $origin, limited = $limited,
              revision = {NextRevision}
            """;
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
        command.Parameters.AddWithValue("$created", record.Created.ToString("O"));
        command.Parameters.AddWithValue("$updated", record.Updated.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<Session?> FindAsync(string id, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM sessions WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>
    /// The newest of THIS machine's records for a quest, or null — whether a session here left it
    /// unfinished is what lets one carry it on (D80). A teammate's record is their machine's run.
    /// </summary>
    public async Task<Session?> LastOwnForQuestAsync(string quest, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM sessions WHERE quest = $quest AND origin IS NULL
            ORDER BY created DESC, rowid DESC LIMIT 1
            """;
        command.Parameters.AddWithValue("$quest", quest);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>
    /// Whether a session of THIS machine's took <paramref name="quest"/> through its own connector
    /// (STANDDOWN2): the take is held here, so a person's stop of its session leaves it this machine's to
    /// carry on once released (SESSUX1b2). A teammate's record never says so: its take is its machine's.
    /// </summary>
    public async Task<bool> TookHereAsync(string quest, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sessions WHERE quest = $quest AND origin IS NULL AND took = 1 LIMIT 1";
        command.Parameters.AddWithValue("$quest", quest);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not null;
    }

    /// <summary>
    /// The newest teammate's record for a quest that did not stand down, or null: what names another machine's take
    /// (CARRY2), since the quest's log names a machine by an id no person reads. A stand-down says its machine has no take.
    /// </summary>
    public async Task<Session?> LastTeammateForQuestAsync(string quest, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM sessions WHERE quest = $quest AND origin IS NOT NULL AND state <> $stoodDown
            ORDER BY created DESC, rowid DESC LIMIT 1
            """;
        command.Parameters.AddWithValue("$quest", quest);
        command.Parameters.AddWithValue("$stoodDown", nameof(SessionState.StoodDown));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>
    /// A record of a session started for <paramref name="quest"/> — this machine's or a teammate's, in
    /// any state — or null (D95). A quest a record names is not deleted: the record would name nothing.
    /// </summary>
    public async Task<Session?> AnyForQuestAsync(string quest, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT * FROM sessions WHERE quest = $quest ORDER BY created, rowid LIMIT 1";
        command.Parameters.AddWithValue("$quest", quest);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>Every quest a session record names — <see cref="AnyForQuestAsync"/> for a whole list at once (D95).</summary>
    public async Task<IReadOnlySet<string>> QuestsNamedAsync(CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT quest FROM sessions WHERE quest IS NOT NULL";

        var named = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) named.Add(reader.GetString(0));
        return named;
    }

    /// <summary>
    /// The session holding a working tree, if any — the one-session-per-TREE question (D51).
    /// </summary>
    /// <param name="tree">
    /// Which tree is being asked about. Null is the repository's main tree, path unknown.
    /// </param>
    /// <remarks>
    /// <b>Unknown means "possibly yours", on either side.</b> A stored row with no tree — anything
    /// from before D51, or a record mirrored from a machine that rightly sent no path — holds every
    /// tree in its repository; and an ask that names no tree is answered by any active session there.
    /// The lock errs toward refusing, because a refusal is a sentence naming what holds the tree and
    /// the other way round is two agents in one working tree.
    ///
    /// <para><b>The team's records are not this machine's lock</b> (D47 §6, SYNC4). A teammate's
    /// session holds a tree on THEIR machine — the tree is the unit of exclusion, and it is per machine
    /// by definition — so a record that came down with an origin is never counted here, however
    /// active it is. Counting it would let a machine that went quiet mid-session lock a repository on
    /// every other machine for good.</para>
    /// </remarks>
    public async Task<Session?> ActiveForAsync(
        string repository, string? tree = null, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
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
    }

    /// <summary>
    /// The session with a PROCESS in this directory, whichever record it belongs to — the intake
    /// room's lock (D65 §1b), where every ask in a circle is a different "repository" in one room.
    /// </summary>
    /// <remarks>
    /// <b>Parked does not count here</b>, unlike <see cref="ActiveForAsync"/>. The tree lock keeps a
    /// parked session's tree because its git state is work in flight; a parked intake has no process
    /// and no tree — it asked the person and ended — and counting it would let one unanswered question
    /// stop every later ask in the circle.
    /// </remarks>
    public async Task<Session?> RunningInTreeAsync(string tree, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM sessions
            WHERE tree = $tree AND state IN ('Queued', 'Starting', 'Working') AND origin IS NULL
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$tree", (object?)Trees.Normalize(tree) ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>
    /// Sessions in one repository, or everywhere. Live work first, closed records on request —
    /// the same shape as the quest list, for the same reason.
    /// </summary>
    public async Task<IReadOnlyList<Session>> ListAsync(
        string? repository = null, bool includeClosed = false, string? workspace = null,
        CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
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
    }

    private const string ActiveStates = "'Queued', 'Starting', 'Working', 'AwaitingPerson'";

    // ——— SYNC4: records both ways, by cursor.

    /// <summary>
    /// This machine's OWN records in a workspace written after <paramref name="revision"/>, in the
    /// order they were written, each with its revision — what a push sends. A record from the team is
    /// never here: it is somebody else's to feed, and feeding it would launder it through this key.
    /// </summary>
    public async Task<IReadOnlyList<(Session Session, long Revision)>> OwnChangedSinceAsync(
        long revision, string workspace, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
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
    }

    /// <summary>
    /// A remote's side: the team's records it holds after <paramref name="since"/>, in revision order,
    /// one page at a time — leaving out <paramref name="caller"/>'s own, which it already has, while
    /// still moving the page past them so they are not scanned again.
    /// </summary>
    public async Task<SessionFetch> TeamSinceAsync(
        long since, string? caller, int limit = 500, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
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
    }

    /// <summary>How far this machine has pushed its own records to a workspace's remote, and fetched the team's.</summary>
    public async Task<(long Pushed, long Fetched)> CursorAsync(string workspace, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT pushed, fetched FROM session_cursor WHERE workspace = $workspace";
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? (reader.GetInt64(0), reader.GetInt64(1)) : (0, 0);
    }

    /// <summary>Move a workspace's cursors forward — never back, so a late answer cannot re-send the past.</summary>
    public async Task AdvanceCursorAsync(
        string workspace, long? pushed = null, long? fetched = null, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO session_cursor (workspace, pushed, fetched) VALUES ($workspace, $pushed, $fetched)
            ON CONFLICT (workspace) DO UPDATE SET
              pushed = MAX(pushed, excluded.pushed), fetched = MAX(fetched, excluded.fetched)
            """;
        command.Parameters.AddWithValue("$workspace", Workspaces.Normalize(workspace));
        command.Parameters.AddWithValue("$pushed", pushed ?? 0);
        command.Parameters.AddWithValue("$fetched", fetched ?? 0);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static Session Read(SqliteDataReader reader) => new(
        reader.GetString(reader.GetOrdinal("id")),
        reader.IsDBNull(reader.GetOrdinal("quest")) ? null : reader.GetString(reader.GetOrdinal("quest")),
        reader.GetString(reader.GetOrdinal("repository")),
        reader.GetString(reader.GetOrdinal("adapter")),
        Enum.Parse<SessionState>(reader.GetString(reader.GetOrdinal("state"))),
        reader.IsDBNull(reader.GetOrdinal("note")) ? null : reader.GetString(reader.GetOrdinal("note")),
        reader.IsDBNull(reader.GetOrdinal("evidence")) ? null : reader.GetString(reader.GetOrdinal("evidence")),
        reader.IsDBNull(reader.GetOrdinal("transcript")) ? null : reader.GetString(reader.GetOrdinal("transcript")),
        DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("created"))),
        DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("updated"))),
        Workspaces.Normalize(reader.GetString(reader.GetOrdinal("workspace"))),
        Enum.TryParse<SessionKind>(reader.GetString(reader.GetOrdinal("kind")), out var kind)
            ? kind
            // A row with a kind this build does not know is a DRIVEN record as far as anything here
            // can act on it: the lifecycle is identical, and refusing to read a record would lose the
            // trace of work that actually happened.
            : SessionKind.Driven,
        reader.IsDBNull(reader.GetOrdinal("harness_version"))
            ? null : reader.GetString(reader.GetOrdinal("harness_version")),
        reader.IsDBNull(reader.GetOrdinal("profile")) ? null : reader.GetString(reader.GetOrdinal("profile")),
        reader.IsDBNull(reader.GetOrdinal("tree")) ? null : reader.GetString(reader.GetOrdinal("tree")),
        reader.IsDBNull(reader.GetOrdinal("base_commit"))
            ? null : reader.GetString(reader.GetOrdinal("base_commit")))
    {
        Origin = reader.IsDBNull(reader.GetOrdinal("origin")) ? null : reader.GetString(reader.GetOrdinal("origin")),
        Ask = reader.IsDBNull(reader.GetOrdinal("ask")) ? null : reader.GetString(reader.GetOrdinal("ask")),
        Took = !reader.IsDBNull(reader.GetOrdinal("took")) && reader.GetInt64(reader.GetOrdinal("took")) != 0,
        Said = SaidOf(reader),
        Interrupted = !reader.IsDBNull(reader.GetOrdinal("interrupted")) && reader.GetInt64(reader.GetOrdinal("interrupted")) != 0,
        Limit = !reader.IsDBNull(reader.GetOrdinal("limited")) && reader.GetInt64(reader.GetOrdinal("limited")) != 0,
        Pushed = !reader.IsDBNull(reader.GetOrdinal("pushed")) && reader.GetInt64(reader.GetOrdinal("pushed")) != 0,
        NoteParts = reader.IsDBNull(reader.GetOrdinal("note_parts")) ? null : reader.GetString(reader.GetOrdinal("note_parts")),
    };

    /// <summary>
    /// Mark these records as gone up to a remote (SESSUX1f, D126 §5.4), once a push took them. No revision moves: the mark
    /// is this machine's fact about the record, not a change to it, so the next push does not send it again. An id with
    /// no record of this machine's is passed over.
    /// </summary>
    public async Task MarkPushedAsync(IEnumerable<string> ids, CancellationToken ct = default)
    {
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "UPDATE sessions SET pushed = 1 WHERE id = $id AND origin IS NULL";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Remove a record whole (SESSUX1f, D126 §5.4). Blind, like every write here: whether a record may go is the ledger's
    /// judgement, made before this is asked. False when there was none.
    /// </summary>
    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM sessions WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    /// <summary>
    /// The id a record from before <see cref="Session.Said"/> gives its answer as a word (MSG1a). Never one a word is
    /// given now, which are hexadecimal.
    /// </summary>
    public const string AnswerWordId = "answer";

    /// <summary>
    /// Keep a word the person said on this machine's record, after the words already waiting, leaving its state where it
    /// is (STANDDOWN2, ANSWER1b, MSG1a). With a <paramref name="note"/> (an answer's line) the note is written too, as a
    /// new revision at the word's moment, since the note travels; with none, nothing that travels changes, so no
    /// revision is written. Null when there is no such record of this machine's.
    /// </summary>
    /// <remarks>
    /// <para><b>Appended in one statement</b>, for the reason an ask's words are (REV3): a whole-list save from two
    /// doors at once would drop one of them.</para>
    ///
    /// <para><b>A record from before the list</b> holds its answer in the old column: it becomes the list's first
    /// word in the same statement, and the old column is emptied, so the record's words have one home from here.</para>
    /// </remarks>
    /// <param name="noteParts">The note's parts beside it (LANG1a), written with it; a note without them clears the record's.</param>
    public async Task<Session?> KeepSaidAsync(
        string id, SaidWord word, string? note = null, CancellationToken ct = default, string? noteParts = null)
    {
        await using (var command = _connection.CreateCommand())
        {
            command.CommandText = $"""
                UPDATE sessions SET
                  said = json_insert(
                    COALESCE(said, CASE WHEN answer IS NULL THEN '[]' ELSE json_array(json_object(
                      'id', '{AnswerWordId}', 'text', answer, 'at', updated, 'files', json('[]'))) END),
                    '$[#]', json($word)),
                  answer = NULL
                  {(note is null ? "" : $", note = $note, note_parts = $noteParts, updated = $updated, revision = {NextRevision}")}
                WHERE id = $id AND origin IS NULL
                """;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$word", WordJson(word));
            if (note is not null)
            {
                command.Parameters.AddWithValue("$note", note);
                command.Parameters.AddWithValue("$noteParts", (object?)noteParts ?? DBNull.Value);
                command.Parameters.AddWithValue("$updated", word.At.ToString("O"));
            }

            if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 0) return null;
        }

        return await FindAsync(id, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Take these words off this machine's record (MSG1a, D137 §2.4): the session took them. An id it does not hold is
    /// passed over, and the last word taken leaves the record with none. No revision, since the words never travel. Null
    /// when there is no such record of this machine's.
    /// </summary>
    /// <remarks>A read, then the write it decides: the ledger calls it inside <see cref="ExclusiveAsync{T}"/> (REV3).</remarks>
    public async Task<Session?> TakeSaidAsync(string id, IReadOnlyCollection<string> ids, CancellationToken ct = default)
    {
        var session = await FindAsync(id, ct).ConfigureAwait(false);
        if (session is not { Origin: null }) return null;

        var left = session.Said.Where(word => !ids.Contains(word.Id, StringComparer.Ordinal)).ToList();
        await using (var command = _connection.CreateCommand())
        {
            command.CommandText = "UPDATE sessions SET said = $said, answer = NULL WHERE id = $id AND origin IS NULL";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$said", left.Count == 0 ? DBNull.Value : SaidJson(left));
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return session with { Said = left };
    }

    /// <summary>
    /// The record's words: its list, or, on a record from before the list, its answer as the first word, said when the
    /// record last changed, which is when the answer was kept (ANSWER1b).
    /// </summary>
    private static IReadOnlyList<SaidWord> SaidOf(SqliteDataReader reader)
    {
        var said = reader.GetOrdinal("said");
        if (!reader.IsDBNull(said)) return SaidWords(reader.GetString(said));

        var answer = reader.GetOrdinal("answer");
        return reader.IsDBNull(answer)
            ? []
            : [new SaidWord(AnswerWordId, reader.GetString(answer), DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("updated"))), [])];
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
                if (element.ValueKind != JsonValueKind.Object
                    || JsonFields.Text(element, "id") is not { Length: > 0 } wordId
                    || JsonFields.Text(element, "text") is not { } text
                    || !DateTimeOffset.TryParse(
                        JsonFields.Text(element, "at"), System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var at))
                {
                    continue;
                }

                var files = JsonFields.Items(element, "files")
                    .Where(file => file.ValueKind == JsonValueKind.String)
                    .Select(file => file.GetString()!)
                    .ToList();
                var reopens = element.TryGetProperty("reopens", out var flag) && flag.ValueKind == JsonValueKind.True;
                words.Add(new SaidWord(wordId, text, at, files, reopens));
            }

            return words;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // Hand-rolled for the reason every store's lists are: nothing here may stop working under AOT.
    private static string SaidJson(IEnumerable<SaidWord> words) => JsonFields.Written(writer =>
    {
        writer.WriteStartArray();
        foreach (var word in words) Write(writer, word);
        writer.WriteEndArray();
    });

    private static string WordJson(SaidWord word) => JsonFields.Written(writer => Write(writer, word));

    private static void Write(Utf8JsonWriter writer, SaidWord word)
    {
        writer.WriteStartObject();
        writer.WriteString("id", word.Id);
        writer.WriteString("text", word.Text);
        writer.WriteString("at", word.At.ToString("O"));
        writer.WriteStartArray("files");
        foreach (var file in word.Files) writer.WriteStringValue(file);
        writer.WriteEndArray();
        if (word.Reopens) writer.WriteBoolean("reopens", true);
        writer.WriteEndObject();
    }

    /// <summary>
    /// Mark that this session took its quest itself (STANDDOWN2) — said by the session's own connector
    /// at the moment of the take. False when there is no such record of this machine's.
    /// </summary>
    public async Task<bool> MarkTookAsync(string id, CancellationToken ct = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "UPDATE sessions SET took = 1 WHERE id = $id AND origin IS NULL";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    /// <summary>Whitespace is nothing said, not a value: an empty version reads as a version of "".</summary>
    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
