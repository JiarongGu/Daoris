namespace Daoris.Knowledge;

/// <summary>The sessions table, its triggers and the columns later builds added: made once at open, and upgraded in place.</summary>
internal static class SessionSchema
{
    /// <summary>The states that hold a repository: a parked session counts, since the person is the flow control.</summary>
    internal const string ActiveStates = "'Queued', 'Starting', 'Working', 'AwaitingPerson'";

    /// <summary>
    /// The next revision, as one statement's subquery — so under SQLite's write lock no two writes can
    /// take the same number and none can land behind one already read (SYNC4's cursor rests on it).
    /// </summary>
    /// <remarks>
    /// One past the larger of the table's newest and the mark (HIST1a), which the statement's own trigger then moves: the
    /// table alone goes back when its newest record is deleted, and the push cursor would pass the next write by (H2).
    /// </remarks>
    internal const string NextRevision = """
        (SELECT MAX(
           COALESCE((SELECT MAX(revision) FROM sessions), 0),
           COALESCE((SELECT revision FROM session_revision WHERE one = 1), 0)) + 1)
        """;

    public static async Task EnsureAsync(ConnectionGate db, CancellationToken ct)
    {
        await using (var command = db.Command())
        {
            command.CommandText = $"""
                CREATE TABLE IF NOT EXISTS sessions (
                  id         TEXT PRIMARY KEY,
                  -- Nullable since D49 §3: a chat may serve no quest at all.
                  quest      TEXT NULL,
                  repository TEXT NOT NULL,
                  door       TEXT NOT NULL,
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
                  door_version TEXT NULL,
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

        await SessionColumnNames.RenameAsync(db, ct).ConfigureAwait(false);

        // Records made before a column existed must survive its arrival: a session record is the
        // reviewable trace of work that actually happened, and a store that dropped them on an upgrade
        // would lose exactly the history the person reviews. Workspaces came with D48, kinds with D49,
        // the tree with D51, the base commit with SURF6.
        foreach (var (column, definition) in new[]
        {
            ("workspace", $"workspace TEXT NOT NULL DEFAULT '{Workspaces.Default}'"),
            ("kind", $"kind TEXT NOT NULL DEFAULT '{nameof(SessionKind.Driven)}'"),
            ("door_version", "door_version TEXT NULL"),
            ("profile", "profile TEXT NULL"),
            ("tree", "tree TEXT NULL"),
            ("base_commit", "base_commit TEXT NULL"),
        })
        {
            await SchemaColumns.EnsureAsync(db, "sessions", column, definition, ct).ConfigureAwait(false);
        }

        await RelaxQuestAsync(db, ct).ConfigureAwait(false);

        // SYNC4, after the rebuild above so it cannot drop them: whose record a row is, and the order
        // this store wrote them in. Both are DERIVED for rows that already exist — a mirrored row's id
        // already carries its origin, and the order rows were written in is the order they were
        // inserted — so a store from before the sync pushes and serves every record it holds.
        if (!await SchemaColumns.HasAsync(db, "sessions", "origin", ct).ConfigureAwait(false))
        {
            await using var alter = db.Command();
            alter.CommandText = """
                ALTER TABLE sessions ADD COLUMN origin TEXT NULL;
                UPDATE sessions SET origin = substr(id, 1, instr(id, '/') - 1) WHERE instr(id, '/') > 0;
                """;
            await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        if (!await SchemaColumns.HasAsync(db, "sessions", "revision", ct).ConfigureAwait(false))
        {
            await using var alter = db.Command();
            alter.CommandText = """
                ALTER TABLE sessions ADD COLUMN revision INTEGER NOT NULL DEFAULT 0;
                UPDATE sessions SET revision = rowid;
                """;
            await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // INT4b: the ask an intake answers. After the rebuild, like SYNC4's pair, so it cannot drop it.
        await SchemaColumns.EnsureAsync(db, "sessions", "ask", "ask TEXT NULL", ct).ConfigureAwait(false);

        // STANDDOWN2: whether this session took its own quest, through its own connector. A record from
        // before it says nothing, and nothing is the old reading.
        await SchemaColumns.EnsureAsync(db, "sessions", "took", "took INTEGER NULL", ct).ConfigureAwait(false);

        // MSG1a (D137 §2.4): the person's words, a JSON list. The single answer it replaced is the desktop's migration's (D232).
        await SchemaColumns.EnsureAsync(db, "sessions", "said", "said TEXT NULL", ct).ConfigureAwait(false);

        // D104: a stop that was not the person's — the sweep's, or a shutdown's. A record from before it
        // says nothing, and nothing is the old reading: the person's stop.
        await SchemaColumns.EnsureAsync(db, "sessions", "interrupted", "interrupted INTEGER NULL", ct).ConfigureAwait(false);

        // TOOL4c (D125 §5.2): a failure an account's limit made. A record from before it says nothing, and
        // nothing is the old reading: a failure like any other. `limited`, since LIMIT is SQL's own word.
        await SchemaColumns.EnsureAsync(db, "sessions", "limited", "limited INTEGER NULL", ct).ConfigureAwait(false);

        // LANG1a (D142 point 2): the note's parts, a JSON list beside it. A record from before says none, and its note stands
        // as kept: nothing re-reads the English, and nothing rewrites it (the language design §6).
        await SchemaColumns.EnsureAsync(db, "sessions", "note_parts", "note_parts TEXT NULL", ct).ConfigureAwait(false);
        await NotePartNames.RenameAsync(db, ct).ConfigureAwait(false);

        // REVIEWENV1b (D154 point 9): what a set-up step's session asked Daoris to serve and said it showed, until its driver
        // posts each with the commit it read. This machine's own, like the person's words beside it: no wire names it, and
        // a record from before says nothing, which is the old reading.
        await SchemaColumns.EnsureAsync(db, "sessions", "review", "review TEXT NULL", ct).ConfigureAwait(false);

        // XAGENT1c (D155 point 11): the second opinion a reviewer's record reads another session's work for. This machine's
        // own, like the opinion it names: no wire carries it, and a record from before says none, which is the old reading.
        await SchemaColumns.EnsureAsync(db, "sessions", "opinion", "opinion TEXT NULL", ct).ConfigureAwait(false);

        // MIXAGENT2a-i (D163 point 2): the session a helper's record answers. This machine's own, as `opinion` is.
        await SchemaColumns.EnsureAsync(db, "sessions", "serves", "serves TEXT NULL", ct).ConfigureAwait(false);

        // COAGENT1b (D199 point 1): the piece a partner's record writes. This machine's own, as `serves` is, and a record from
        // before says none, which is the old reading.
        await SchemaColumns.EnsureAsync(db, "sessions", "piece", "piece TEXT NULL", ct).ConfigureAwait(false);

        // MIXAGENT1a-iii (D163 point 4): the session a record continues, why and who asked. This machine's own, as `opinion` is,
        // and a record from before says none, which is the old reading. `from_session`, since FROM is SQL's own word (as `limited`).
        await SchemaColumns.EnsureAsync(db, "sessions", "from_session", "from_session TEXT NULL", ct).ConfigureAwait(false);
        await SchemaColumns.EnsureAsync(db, "sessions", "from_why", "from_why TEXT NULL", ct).ConfigureAwait(false);
        await SchemaColumns.EnsureAsync(db, "sessions", "from_by", "from_by TEXT NULL", ct).ConfigureAwait(false);

        // SESSONE1a (D167 point 2): the entry's key, written at every open. It travels, so a mirror holds it too. A record from before it
        // says none, and the reader answers the rule's key for it without writing it here (D167 point 3).
        await SchemaColumns.EnsureAsync(db, "sessions", "entry", "entry TEXT NULL", ct).ConfigureAwait(false);

        // CHATTAKE1 (D167, the design's §1.2 rule 2): the quest a conversation took, which `took` alone never said. It travels, as the
        // entry does, since a teammate's reader keys the quest by it. A record from before says none, which is the old reading.
        await SchemaColumns.EnsureAsync(db, "sessions", "took_quest", "took_quest TEXT NULL", ct).ConfigureAwait(false);

        // The take's bind is gone with the column that said why one did not bind.
        if (await SchemaColumns.HasAsync(db, "sessions", "took_bind", ct).ConfigureAwait(false))
        {
            await using var dropBind = db.Command();
            dropBind.CommandText = "ALTER TABLE sessions DROP COLUMN took_bind";
            await dropBind.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // HELPMODE1a2 (D183 point 3): the access mode an Ask Daoris record's run got. This machine's own, as `opinion` is: no wire carries
        // it, and a help record from before says none, which reads as restricted, what it ran under.
        await SchemaColumns.EnsureAsync(db, "sessions", "help_mode", "help_mode TEXT NULL", ct).ConfigureAwait(false);

        // AUTONOMY1a (D184 point 1): the access setup a driven session started under. This machine's own, as `opinion` is: no wire
        // carries it, and a record from before says none, which is the old reading, a start under today's behaviour.
        await SchemaColumns.EnsureAsync(db, "sessions", "access", "access TEXT NULL", ct).ConfigureAwait(false);

        // KEEP1b (D195 point 6): the downloaded versions the record's starts were handed, a JSON list. This machine's own, as `opinion`
        // is: no wire carries it, and a record from before says none, which keeps the downloads' start-time rule.
        await SchemaColumns.EnsureAsync(db, "sessions", "resolved", "resolved TEXT NULL", ct).ConfigureAwait(false);
        // An existing record says none, preserving its reading. Mirrors never name these columns.
        foreach (var column in new[] { "actor", "choice", "asked", "actor_override", "ran", "chosen" })
            await SchemaColumns.EnsureAsync(db, "sessions", column, $"{column} TEXT NULL", ct).ConfigureAwait(false);

        await using (var cursor = db.Command())
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

        await SessionIndexes.EnsureAsync(db, ct).ConfigureAwait(false);

        // HIST1a: the highest revision issued, one row beside the cursor it keeps honest. The table's own maximum goes back
        // when its newest record is deleted, and the push cursor has already passed that number (H2). A store from before
        // starts the mark at the newest revision it holds, so nothing changes for one that never deleted a record. The
        // triggers move it in the statement that writes the revision, as one statement is what keeps a revision exact
        // under SQLite's write lock (see NextRevision): where no transaction is open, a second statement is a second
        // transaction, and another host could write between the two.
        await using (var mark = db.Command())
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

        // PUSH1d1b: a count the words kept move, for the change stream's `words` family. A word kept without a note issues no
        // revision (it never travels), so the family has its own. A trigger, as the others are, so a connector built before it
        // still moves it; it fires only when the list grows, so a take and a push's mark or cursor move nothing.
        await using (var words = db.Command())
        {
            words.CommandText = """
                CREATE TABLE IF NOT EXISTS words_revision (
                  one      INTEGER PRIMARY KEY CHECK (one = 1),
                  revision INTEGER NOT NULL
                );
                INSERT OR IGNORE INTO words_revision (one, revision) VALUES (1, 0);

                CREATE TRIGGER IF NOT EXISTS words_revision_kept AFTER UPDATE OF said ON sessions
                WHEN NEW.said IS NOT NULL AND json_array_length(NEW.said) > json_array_length(COALESCE(OLD.said, '[]'))
                BEGIN
                  UPDATE words_revision SET revision = revision + 1 WHERE one = 1;
                END;
                """;
            await words.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // FOUNDATION1 row p (D212, the foundation design §3.4 rule 5, §19.2): the words that left a record's `said`, one row per record left,
        // word id and producer key (`id:<word id>` for a word with none), written by this trigger in the statement that takes the word, so
        // every writer settles a word as it takes it, a connector built before the table included, and no take changes. It says only that
        // the word left `said`, never that a turn took it: a take-back, an uncertain word's settlement, a park's clear and a supersession
        // leave by the same statements. The attempt mark rewrites the list with every id kept and settles nothing. The trigger trims by
        // time alone, in its own statement over the index on the moment, with no count cap, so a settlement answers a retry for the seven
        // days after the word left. The moment is whole seconds, which every build of SQLite reads. A list that is not a JSON array on
        // either side settles nothing, never fails the statement that wrote it.
        await using (var settled = db.Command())
        {
            settled.CommandText = $"""
                -- One row per record left, word id and producer key (the unique identity); stored in the order of the key and the record, which is
                -- the read's index (the design's said_settled_key), so the table is one tree beside the moment's index, not three beside a rowid's.
                CREATE TABLE IF NOT EXISTS said_settled (
                  key     TEXT NOT NULL,
                  session TEXT NOT NULL,
                  word    TEXT NOT NULL,
                  at      INTEGER NOT NULL,
                  PRIMARY KEY (key, session, word)
                ) WITHOUT ROWID;
                CREATE INDEX IF NOT EXISTS said_settled_at ON said_settled (at);

                CREATE TRIGGER IF NOT EXISTS said_settled_left AFTER UPDATE OF said ON sessions
                WHEN OLD.said IS NOT NEW.said AND NEW.origin IS NULL AND OLD.said IS NOT NULL
                  AND json_valid(OLD.said) AND json_type(OLD.said) = 'array'
                  AND (NEW.said IS NULL OR (json_valid(NEW.said) AND json_type(NEW.said) = 'array'))
                BEGIN
                  INSERT OR REPLACE INTO said_settled (session, word, key, at)
                  SELECT NEW.id, json_extract(gone.value, '$.id'),
                         COALESCE(NULLIF(json_extract(gone.value, '$.key'), ''), 'id:' || json_extract(gone.value, '$.id')),
                         CAST(strftime('%s', 'now') AS INTEGER)
                  FROM (SELECT value FROM json_each(OLD.said) WHERE type = 'object') AS gone
                  WHERE json_extract(gone.value, '$.id') IS NOT NULL
                    AND NOT EXISTS (
                      SELECT 1 FROM (SELECT value FROM json_each(COALESCE(NEW.said, '[]')) WHERE type = 'object') AS kept
                      WHERE json_extract(kept.value, '$.id') IS json_extract(gone.value, '$.id'));
                  DELETE FROM said_settled WHERE at < CAST(strftime('%s', 'now') AS INTEGER) - {(long)SaidKeys.Horizon.TotalSeconds};
                END;
                """;
            await settled.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // FOUNDATION1 row k part 1 (D212, the foundation design §4.1, §19.12): the event journal, the ledger's raw transitions in commit order,
        // minted by triggers in the statement that writes the record, so every writer mints it, a connector built before the table included.
        // Read by nothing yet. The store's identity is the journal's epoch, so the schema writes it (the change stream's observer wrote it
        // until now and only reads it; an existing stamp stays as it stands).
        await using (var journal = db.Command())
        {
            journal.CommandText = EventJournal.Schema;
            await journal.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // SESSUX1f (D126 §5.4): whether a record went up to a remote, after the cursor it is derived from. For rows that
        // already exist it is read once from that cursor: a record of this machine's at or before what its workspace
        // pushed was examined by a push, so it is marked, which errs toward refusing a delete (a record of a repository
        // that had not joined is marked too). From here on a push marks what it sent.
        if (!await SchemaColumns.HasAsync(db, "sessions", "pushed", ct).ConfigureAwait(false))
        {
            await using var alter = db.Command();
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
    /// Let `quest` be null on a table created before chats existed (D49 §3).
    /// </summary>
    /// <remarks>
    /// SQLite cannot drop a NOT NULL constraint, so the table is rebuilt and the rows are COPIED —
    /// not recreated empty. The entry store rebuilds by discarding, and may: it holds derived data.
    /// This one holds records of work that happened, which nothing can re-derive. Guarded by a probe
    /// so it runs once, on the one upgrade that needs it, and never again.
    /// </remarks>
    private static async Task RelaxQuestAsync(ConnectionGate db, CancellationToken ct)
    {
        await using (var probe = db.Command())
        {
            probe.CommandText =
                "SELECT COUNT(*) FROM pragma_table_info('sessions') WHERE name = 'quest' AND \"notnull\" = 1";
            var constrained = Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false));
            if (constrained == 0) return;
        }

        await using var rebuild = db.Command();
        rebuild.CommandText = $"""
            CREATE TABLE sessions_relaxed (
              id         TEXT PRIMARY KEY,
              quest      TEXT NULL,
              repository TEXT NOT NULL,
              door       TEXT NOT NULL,
              state      TEXT NOT NULL,
              note       TEXT NULL,
              evidence   TEXT NULL,
              transcript TEXT NULL,
              created    TEXT NOT NULL,
              updated    TEXT NOT NULL,
              workspace  TEXT NOT NULL DEFAULT '{Workspaces.Default}',
              kind       TEXT NOT NULL DEFAULT '{nameof(SessionKind.Driven)}',
              door_version TEXT NULL,
              profile         TEXT NULL,
              tree            TEXT NULL,
              base_commit     TEXT NULL
            );
            INSERT INTO sessions_relaxed
              SELECT id, quest, repository, door, state, note, evidence, transcript, created,
                     updated, workspace, kind, door_version, profile, tree, base_commit
              FROM sessions;
            DROP TABLE sessions;
            ALTER TABLE sessions_relaxed RENAME TO sessions;
            CREATE INDEX IF NOT EXISTS sessions_repository ON sessions (repository, state);
            """;
        await rebuild.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
