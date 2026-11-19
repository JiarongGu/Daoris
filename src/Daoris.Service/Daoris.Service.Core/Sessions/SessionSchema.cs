namespace Daoris.Knowledge;

/// <summary>The sessions table and its triggers, made once at open with the one current shape.</summary>
internal static class SessionSchema
{
    /// <summary>The states that hold a repository: a parked session counts, since the person is the flow control.</summary>
    internal const string ActiveStates = "'Queued', 'Starting', 'Working', 'AwaitingPerson', 'AwaitingStep'";

    /// <summary>
    /// The next revision, as one statement's subquery — so under SQLite's write lock no two writes can
    /// take the same number and none can land behind one already read (the sync cursor rests on it).
    /// </summary>
    /// <remarks>
    /// One past the larger of the table's newest and the mark, which the statement's own trigger then moves: the
    /// table alone goes back when its newest record is deleted, and the push cursor would pass the next write by.
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
                  base_commit     TEXT NULL,
                  -- Whose record a row is (a mirrored row's id carries its origin; null is this machine's own), and the order this
                  -- store wrote them in, which the sync cursor rests on.
                  origin          TEXT NULL,
                  revision        INTEGER NOT NULL DEFAULT 0,
                  -- The ask an intake answers.
                  ask             TEXT NULL,
                  -- Whether this session took its own quest, through its own connector.
                  took            INTEGER NULL,
                  -- The person's words, a JSON list.
                  said            TEXT NULL,
                  -- A stop that is not the person's: the sweep's, or a shutdown's.
                  interrupted     INTEGER NULL,
                  -- A failure an account's limit made. `limited`, since LIMIT is SQL's own word.
                  limited         INTEGER NULL,
                  -- The note's parts, a JSON list beside it, and the turns' own record.
                  note_parts      TEXT NULL,
                  turns           TEXT NULL,
                  -- What a set-up step's session asked Daoris to serve and said it showed, until its driver posts each with the
                  -- commit it read. This machine's own, like the person's words beside it: no wire names it.
                  review          TEXT NULL,
                  -- The second opinion a reviewer's record reads another session's work for, the session a helper's record
                  -- answers and the piece a partner's record writes. This machine's own: no wire carries them.
                  opinion         TEXT NULL,
                  serves          TEXT NULL,
                  piece           TEXT NULL,
                  decides         TEXT NULL,
                  performs        TEXT NULL,
                  holds_place     TEXT NULL,
                  -- The session a record continues, why and who asked. `from_session`, since FROM is SQL's own word.
                  from_session    TEXT NULL,
                  from_why        TEXT NULL,
                  from_by         TEXT NULL,
                  -- The record's conversation, given as the record opens and never moved.
                  line            TEXT NULL,
                  -- The entry's key, written at every open. It travels, so a mirror holds it too.
                  entry           TEXT NULL,
                  -- The quest a conversation took, which `took` alone never says. It travels, as the entry does.
                  took_quest      TEXT NULL,
                  -- The access mode an Ask Daoris record's run gets, the access setup a driven session started under and the
                  -- downloaded versions the record's starts get (a JSON list). This machine's own: no wire carries them.
                  help_mode       TEXT NULL,
                  access          TEXT NULL,
                  resolved        TEXT NULL,
                  -- Mirrors never name these columns.
                  actor           TEXT NULL,
                  choice          TEXT NULL,
                  asked           TEXT NULL,
                  actor_override  TEXT NULL,
                  ran             TEXT NULL,
                  chosen          TEXT NULL,
                  -- Whether a record went up to a remote: a push marks what it sent, and a record marked is refused a delete.
                  pushed          INTEGER NULL
                );
                CREATE INDEX IF NOT EXISTS sessions_repository ON sessions (repository, state);
                """;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

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

        // The highest revision issued, one row beside the cursor it keeps honest. The table's own maximum goes back
        // when its newest record is deleted, and the push cursor has already passed that number. The
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

        // The words that left a record's `said`, one row per record left,
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

        // The event journal, the ledger's raw transitions in commit order,
        // minted by triggers in the statement that writes the record, so every writer mints it, a connector built before the table included.
        // Read by nothing yet. The store's identity is the journal's epoch, so the schema writes it (an existing stamp
        // stays as it stands).
        await using (var journal = db.Command())
        {
            journal.CommandText = EventJournal.Schema;
            await journal.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }
}
