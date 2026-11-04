namespace Daoris.Knowledge;

/// <summary>
/// The tables the quest store keeps, made and upgraded as it opens. Every table is created if missing and every column added if
/// missing, so a store from any earlier build opens and keeps its quests; a table only this machine reads is never pushed or served.
/// </summary>
internal static class QuestSchema
{
    /// <summary>
    /// Makes the store's tables, stamps the version this build keeps, adds the columns an older store lacks and settles the rows an older
    /// build left behind.
    /// </summary>
    /// <returns>The version a newer build stamped on the store, or null where this build's is the newest that has written it.</returns>
    internal static async Task<int?> EnsureAsync(ConnectionGate db, CancellationToken ct)
    {
        await using (var command = db.Command())
        {
            // The log's position is this store's order of appending — and, on a remote, the NUMBER it
            // gives what it accepts (design §8). Machine + sequence names one operation anywhere;
            // position names it here; `remote` is where the remote placed it, null while pending.
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS quest_log (
                  position  INTEGER PRIMARY KEY,
                  quest     TEXT NOT NULL,
                  kind      TEXT NOT NULL,
                  machine   TEXT NOT NULL,
                  sequence  INTEGER NOT NULL,
                  at        TEXT NOT NULL,
                  payload   TEXT NOT NULL,
                  remote    INTEGER NULL,
                  UNIQUE (machine, sequence)
                );
                CREATE INDEX IF NOT EXISTS quest_log_quest ON quest_log (quest, position);
                -- `sequence` is the highest this machine has issued (HIST1a): the log's own maximum
                -- goes back when its newest rows are removed, and a remote already holds that number.
                CREATE TABLE IF NOT EXISTS quest_machine (
                  one      INTEGER PRIMARY KEY CHECK (one = 1),
                  id       TEXT NOT NULL,
                  sequence INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS quest_cursor (
                  workspace TEXT PRIMARY KEY COLLATE NOCASE,
                  number    INTEGER NOT NULL
                );
                -- How each circle's last pass ended (SYNC6a): when it last reached its remote, when it
                -- last tried, the quests it left behind and the wall it hit. Kept in the store rather
                -- than in a host's memory, so a restarted host still knows when the circle last synced.
                CREATE TABLE IF NOT EXISTS quest_passes (
                  workspace TEXT PRIMARY KEY COLLATE NOCASE,
                  synced    TEXT NULL,
                  tried     TEXT NOT NULL,
                  behind    TEXT NOT NULL,
                  problem   TEXT NULL,
                  closure   TEXT NULL
                );
                -- A remote's half of the workspace's handshake (FABRIC-CLOSE1i): each machine that pushed here, and whether its last
                -- push declared it knows a bound quest. A push declaring nothing, as a build before it pushes, marks every machine its
                -- operations name undeclared. Answered beside a page and a push's answer, by id only; never pushed.
                CREATE TABLE IF NOT EXISTS quest_declared (
                  machine TEXT PRIMARY KEY,
                  closure INTEGER NOT NULL,
                  at      TEXT NOT NULL
                );
                -- The machines a person retired at this remote (FABRIC-CLOSE1i2): no longer counted by the handshake, until a push heard
                -- from one again marks it as that push says. A table of its own, so a remote before it counts them as it always did.
                CREATE TABLE IF NOT EXISTS quest_retired (
                  machine TEXT PRIMARY KEY,
                  at      TEXT NOT NULL
                );
                -- The quests this machine cleared whose operations a remote had numbered (HIST1b, D153 point 3):
                -- the absence made a record, since a fetch by cursor cannot carry one. Never pushed or served.
                CREATE TABLE IF NOT EXISTS quest_forgotten (
                  id TEXT PRIMARY KEY,
                  at TEXT NOT NULL
                );
                -- Each closure graph's close this machine answered (FABRIC-CLOSE1e, §E5 step 4): its key, its token, the
                -- submissions it named and how it ended, a refusal as much as a close, on which quest. The same close sent again
                -- answers that first outcome. The Done keeps the key as provenance; this keeps the call. Never pushed or served.
                CREATE TABLE IF NOT EXISTS quest_closes (
                  key     TEXT PRIMARY KEY,
                  token   TEXT NOT NULL,
                  named   TEXT NOT NULL,
                  refusal TEXT NOT NULL,
                  quest   TEXT NULL,
                  at      TEXT NOT NULL
                );
                -- The closure graph each open quest binds at its take (FABRIC-CLOSE1l, §E5's cutover), as the driver named it
                -- when it opened the quest's first session: the take on this machine binds it in the take's own write, and
                -- the row goes with it. Never pushed or served. Its owner (FABRIC-CLOSE1o): the session it was named for, the
                -- publication it was named on and when it lapses, nullable so a build before them still writes its three columns, and
                -- a row without them owns nothing. Since FABRIC-CLOSE1w only builds before it write here, and no row here owns anything.
                CREATE TABLE IF NOT EXISTS quest_take_binds (
                  id    TEXT PRIMARY KEY,
                  graph TEXT NOT NULL,
                  at    TEXT NOT NULL,
                  session            TEXT NULL,
                  published_machine  TEXT NULL,
                  published_sequence INTEGER NULL,
                  expires            TEXT NULL
                );
                -- The binds this build names, owned (FABRIC-CLOSE1w): the compatible-build boundary. A build before it knows only
                -- quest_take_binds, so its upsert there can never replace an owned bind's graph, nor its take bind one by the id alone.
                -- Every column is required, since no build that writes here leaves an owner out. Never pushed or served.
                CREATE TABLE IF NOT EXISTS quest_owned_take_binds (
                  id                 TEXT PRIMARY KEY,
                  graph              TEXT NOT NULL,
                  at                 TEXT NOT NULL,
                  session            TEXT NOT NULL,
                  published_machine  TEXT NOT NULL,
                  published_sequence INTEGER NOT NULL,
                  expires            TEXT NOT NULL
                );
                -- Each bring-forward this machine made (SYNCMERGE1b0, D178 points 3 and 8): the conflict it was to settle and the take it
                -- made, each by machine and sequence, and when. The log forgets one whose take lost, with its mark (D178 point 5), and this
                -- still says that second loss was a bring-forward's. It goes with the quest's log. Never pushed or served to another machine.
                CREATE TABLE IF NOT EXISTS quest_forwards (
                  quest             TEXT NOT NULL,
                  conflict_machine  TEXT NOT NULL,
                  conflict_sequence INTEGER NOT NULL,
                  take_machine      TEXT NOT NULL,
                  take_sequence     INTEGER NOT NULL,
                  at                TEXT NOT NULL,
                  PRIMARY KEY (take_machine, take_sequence)
                );
                CREATE INDEX IF NOT EXISTS quest_forwards_quest ON quest_forwards (quest);
                -- The build that last changed what this store keeps (CLOSETAKE1, QUESTSTOREVER1). Not PRAGMA user_version: the quest tables
                -- share the knowledge index's file, whose rebuild owns that number. Never lowered, never pushed or served.
                CREATE TABLE IF NOT EXISTS quest_store_version (
                  one     INTEGER PRIMARY KEY CHECK (one = 1),
                  version INTEGER NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // 🔴 A store a newer build stamped keeps its stamp and refuses what this build cannot write into it (a take's bind: the next
        // change to that contract would hide its rows in a new table, as FABRIC-CLOSE1w's did); it never refuses to open, since the
        // quests and their history are read and moved as before.
        // In a write transaction of its own, as every RETURNING write is (QUESTOPENLOCK1, ConnectionGate.InTransactionAsync says why).
        var newer = await db.InTransactionAsync(async (transaction, inside) =>
        {
            await using var stamp = db.Command();
            stamp.Transaction = transaction;
            stamp.CommandText = """
                INSERT INTO quest_store_version (one, version) VALUES (1, $version)
                ON CONFLICT(one) DO UPDATE SET version = MAX(version, excluded.version)
                RETURNING version
                """;
            stamp.Parameters.AddWithValue("$version", QuestStore.StoreVersion);
            var stamped = Convert.ToInt32(await stamp.ExecuteScalarAsync(inside).ConfigureAwait(false));
            return stamped > QuestStore.StoreVersion ? stamped : (int?)null;
        }, ct).ConfigureAwait(false);

        await using (var command = db.Command())
        {
            command.CommandText = $"""
                CREATE TABLE IF NOT EXISTS quests (
                  id        TEXT PRIMARY KEY,
                  sender    TEXT NOT NULL,
                  receiver  TEXT NOT NULL,
                  title     TEXT NOT NULL,
                  body      TEXT NOT NULL,
                  status    TEXT NOT NULL,
                  note      TEXT NULL,
                  filed     TEXT NOT NULL,
                  updated   TEXT NOT NULL,
                  workspace   TEXT NOT NULL DEFAULT '{Workspaces.Default}',
                  links       TEXT NOT NULL DEFAULT '[]',
                  attachments TEXT NOT NULL DEFAULT '[]',
                  then_steps  TEXT NOT NULL DEFAULT '[]',
                  parent      TEXT NULL,
                  conflicts   TEXT NOT NULL DEFAULT '[]',
                  awaits      TEXT NULL,
                  published_by TEXT NULL,
                  lanes       TEXT NOT NULL DEFAULT '[]',
                  requirements TEXT NOT NULL DEFAULT '[]',
                  answers     TEXT NOT NULL DEFAULT '[]',
                  accepted    TEXT NULL,
                  held        INTEGER NOT NULL DEFAULT 0,
                  short_title TEXT NULL,
                  evidence    TEXT NULL,
                  review      TEXT NULL,
                  set_up_in   TEXT NULL,
                  set_ups     TEXT NOT NULL DEFAULT '[]',
                  verdicts    TEXT NOT NULL DEFAULT '[]',
                  note_parts  TEXT NULL,
                  closure     TEXT NULL,
                  submission  TEXT NULL,
                  grew_from   TEXT NULL,
                  source_reopened TEXT NULL,
                  reopened    TEXT NULL,
                  workflow_choices TEXT NOT NULL DEFAULT '[]'
                );
                CREATE INDEX IF NOT EXISTS quests_receiver ON quests (receiver, status);
                """;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // A store from before workspaces has no workspace; one from before quests carried anything
        // (D65) has neither list; one from before sync has no conflicts, and a log with no numbers.
        // Their quests survive the upgrade — the one workspace there was, and nothing carried or
        // raced, which is exactly right, because everything in them was its own.
        foreach (var (table, column, definition) in new[]
        {
            ("quests", "workspace", $"workspace TEXT NOT NULL DEFAULT '{Workspaces.Default}'"),
            ("quests", "links", "links TEXT NOT NULL DEFAULT '[]'"),
            ("quests", "attachments", "attachments TEXT NOT NULL DEFAULT '[]'"),
            // A chain (D65 §4): `then` is an SQL keyword, so the column says what it holds.
            ("quests", "then_steps", "then_steps TEXT NOT NULL DEFAULT '[]'"),
            ("quests", "parent", "parent TEXT NULL"),
            ("quests", "conflicts", "conflicts TEXT NOT NULL DEFAULT '[]'"),
            // Ask and wait (D79): the question a taken quest waits on.
            ("quests", "awaits", "awaits TEXT NULL"),
            // The session that published it (SESS1); a quest from before says none, which is true.
            ("quests", "published_by", "published_by TEXT NULL"),
            // The lanes it addresses (D115 §2.2); a quest from before asked the whole repository.
            ("quests", "lanes", "lanes TEXT NOT NULL DEFAULT '[]'"),
            // What the person requires (DRIFT1c); a quest from before named none.
            ("quests", "requirements", "requirements TEXT NOT NULL DEFAULT '[]'"),
            // How a done answered them, and the person's yes to a departure (DRIFT1d); a quest from before has
            // neither, and nothing it carries waits. `held` is the replay's own reading, kept so a list can ask it.
            ("quests", "answers", "answers TEXT NOT NULL DEFAULT '[]'"),
            ("quests", "accepted", "accepted TEXT NULL"),
            ("quests", "held", "held INTEGER NOT NULL DEFAULT 0"),
            // The publisher's short title (SESSUX1j); a quest from before was given none, and is named from its words.
            ("quests", "short_title", "short_title TEXT NULL"),
            // The work's mark (FOUNDATION1x); a quest from before was marked none, and starts on Build's order.
            ("quests", "weight", "weight TEXT NULL"),
            // The paths it declares (INTEGRATE1l); a quest from before declared none.
            ("quests", "paths", "paths TEXT NULL"),
            // The workflow request's key (INTEGRATE1p); a quest from before no request published.
            ("quests", "request", "request TEXT NULL"),
            // What Daoris last read of a done's evidence (EVID1a); a quest from before named none, so none was read.
            ("quests", "evidence", "evidence TEXT NULL"),
            // The chain's review choice, a set-up step's environment, its set-ups and the person's verdicts (REVIEWENV1b); a
            // quest from before chose nothing, is no set-up step, and was never shown or reviewed.
            ("quests", "review", "review TEXT NULL"),
            ("quests", "set_up_in", "set_up_in TEXT NULL"),
            ("quests", "set_ups", "set_ups TEXT NOT NULL DEFAULT '[]'"),
            ("quests", "verdicts", "verdicts TEXT NOT NULL DEFAULT '[]'"),
            // The note's parts (QUESTCLOSE1b1); a quest from before has a note with none, which the page shows as recorded.
            ("quests", "note_parts", "note_parts TEXT NULL"),
            // A closure graph's binding and its current submission (FABRIC-CLOSE1a); a quest from before has neither.
            ("quests", "closure", "closure TEXT NULL"),
            ("quests", "submission", "submission TEXT NULL"),
            // The submission a step grew from, and the one its source's reopen withdrew it from (FABRIC-CLOSE1c); a quest from before
            // has neither, which reads as before.
            ("quests", "grew_from", "grew_from TEXT NULL"),
            ("quests", "source_reopened", "source_reopened TEXT NULL"),
            // A quest's own reopen no later submission answered (FABRIC-CLOSE1c4); a row from before keeps none, and the next replay
            // that writes it reads the log.
            ("quests", "reopened", "reopened TEXT NULL"),
            // The person's workflow choices on a quest before it started (WORKFLOW1e2); no build before it made one, so no row from
            // before needs its log read again.
            ("quests", "workflow_choices", "workflow_choices TEXT NOT NULL DEFAULT '[]'"),
            ("quest_log", "remote", "remote INTEGER NULL"),
            // The remote's handshake as a circle's last pass heard it (FABRIC-CLOSE1i); a pass from before heard none, which binds nothing.
            ("quest_passes", "closure", "closure TEXT NULL"),
            // A bind named for a take, owned (FABRIC-CLOSE1o): CLOSE1l's rows named only the quest. `CREATE TABLE IF NOT EXISTS`
            // upgrades nothing, so the columns are added here, on a local store and a shared one alike. FABRIC-CLOSE1w keeps them for
            // the builds between, which write them; this build reads no owner there.
            ("quest_take_binds", "session", "session TEXT NULL"),
            ("quest_take_binds", "published_machine", "published_machine TEXT NULL"),
            ("quest_take_binds", "published_sequence", "published_sequence INTEGER NULL"),
            ("quest_take_binds", "expires", "expires TEXT NULL"),
        })
        {
            await SchemaColumns.EnsureAsync(db, table, column, definition, ct).ConfigureAwait(false);
        }

        // FABRIC-CLOSE1o: a pending bind whose owner cannot be shown is discarded, since no take can show it is the one it was named
        // for. FABRIC-CLOSE1w: that is every row of quest_take_binds, since this build names its own in quest_owned_take_binds, and a
        // row there, whatever owner it names, may be an older build's upsert over another's graph. The quest and its history are
        // untouched: nothing is bound until a take, so its take is today's, closing by its done.
        await using (var unowned = db.Command())
        {
            unowned.CommandText = "DELETE FROM quest_take_binds";
            await unowned.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // The mirror is gone (design §8). Its rows were copies of a remote's quests, so they are
        // dropped rather than migrated: this machine's cursor starts at zero, and its first fetch
        // brings every one of them back as history. The column that marked them goes with them.
        if (await SchemaColumns.HasAsync(db, "quests", "home", ct).ConfigureAwait(false))
        {
            await using var drop = db.Command();
            drop.CommandText = """
                DELETE FROM quests WHERE home IS NOT NULL;
                ALTER TABLE quests DROP COLUMN home;
                """;
            await drop.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // HIST1a: a store from before the mark starts it at the highest sequence its own machine holds in the log, which
        // is what every number so far was one past. Nothing changes for a store that never removed anything; one that
        // removes rows after the upgrade cannot take the sequence back.
        if (!await SchemaColumns.HasAsync(db, "quest_machine", "sequence", ct).ConfigureAwait(false))
        {
            await using var mark = db.Command();
            mark.CommandText = """
                ALTER TABLE quest_machine ADD COLUMN sequence INTEGER NOT NULL DEFAULT 0;
                UPDATE quest_machine SET sequence =
                  (SELECT COALESCE(MAX(sequence), 0) FROM quest_log WHERE machine = quest_machine.id);
                """;
            await mark.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // After the column, which it names. The mark is moved by the statement that writes the operation, not by its
        // callers, so no way into the log can forget it (the registry's tombstones are kept the same way). An operation
        // of this machine's kept back from a remote raises it too: that number was this machine's, and is held there.
        await using (var trigger = db.Command())
        {
            trigger.CommandText = """
                CREATE TRIGGER IF NOT EXISTS quest_sequence_issued AFTER INSERT ON quest_log
                BEGIN
                  UPDATE quest_machine SET sequence = NEW.sequence
                  WHERE one = 1 AND id = NEW.machine AND sequence < NEW.sequence;
                END;
                """;
            await trigger.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // FOUNDATION1 row k part 2 (D212, the foundation design §4.1): each operation kept is one `quest-op` row of the event journal, minted by
        // a trigger in the statement that appends it, so every writer mints it. The journal's table is made here too, so a store opened alone
        // never has a trigger writing to a table that is not there.
        await using (var journal = db.Command())
        {
            journal.CommandText = EventJournal.QuestLogSchema;
            await journal.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return newer;
    }
}
