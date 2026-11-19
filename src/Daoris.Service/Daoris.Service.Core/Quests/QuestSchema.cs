namespace Daoris.Knowledge;

/// <summary>
/// The tables the quest store keeps, made as it opens. Every table is created if missing, with the one current shape; a table only
/// this machine reads is never pushed or served.
/// </summary>
internal static class QuestSchema
{
    /// <summary>
    /// Makes the store's tables and stamps the version this build keeps.
    /// </summary>
    /// <returns>The version a newer build stamped on the store, or null where this build's is the newest that has written it.</returns>
    internal static async Task<int?> EnsureAsync(ConnectionGate db, CancellationToken ct)
    {
        await using (var command = db.Command())
        {
            // The log's position is this store's order of appending — and, on a remote, the NUMBER it
            // gives what it accepts. Machine + sequence names one operation anywhere;
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
        // change to that contract would hide its rows in a new table, as a take's did); it never refuses to open, since the
        // quests and their history are read and moved as before.
        // In a write transaction of its own, as every RETURNING write is (ConnectionGate.InTransactionAsync says why).
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
                  weight      TEXT NULL,
                  paths       TEXT NULL,
                  request     TEXT NULL,
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
                -- A chain is walked by its steps' parent, once for each session record a list answers, so the parent is indexed.
                CREATE INDEX IF NOT EXISTS quests_parent ON quests (parent);
                """;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // The trigger names `quest_machine.sequence`, so it follows the table. The mark is moved by the statement that writes the operation, not by its
        // callers, so no way into the log can forget it (the registry's tombstones are kept the same way). An operation
        // of this machine's kept back from a remote raises it too: that number is this machine's, and is held there.
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

        // Each operation kept is one `quest-op` row of the event journal, minted by
        // a trigger in the statement that appends it, so every writer mints it. The journal's table is made here too, so a store opened alone
        // never has a trigger writing to a table that is not there.
        await using (var journal = db.Command())
        {
            journal.CommandText = EventJournal.QuestLogSchema;
            await journal.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // And each move the replay writes to the cache, the source of the quest kinds an outside session hears.
        await using (var moves = db.Command())
        {
            moves.CommandText = EventJournal.QuestsSchema;
            await moves.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return newer;
    }
}
