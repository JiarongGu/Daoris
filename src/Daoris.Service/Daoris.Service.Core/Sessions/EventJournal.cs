namespace Daoris.Knowledge;

/// <summary>
/// FOUNDATION1 row k part 1 (D212, the foundation design §4.1, §19.12): the schema of the service's event journal, <c>event_journal</c>, and
/// the triggers on <c>sessions</c> that mint it. One row per raw transition of a record this machine owns, in the order the statements
/// committed (<c>position</c>, never reused), read by nothing yet.
/// </summary>
/// <remarks>
/// <para><b>Minted by trigger</b>, in the statement that writes the record, so every writer mints it: the host, the driver through it, each
/// connector, and one built before the table. A trigger journals the raw transition only (the record, the column, its old and new value, a
/// word's id on either side of <c>said</c>), guarded on what changed, since <c>AFTER UPDATE OF</c> fires on a statement's list and the
/// state move names <c>state</c>, <c>limited</c> and <c>said</c> every time. An attempt mark rewrites <c>said</c> at its length and journals
/// nothing; a mirror's upsert (<c>origin</c> set) is another machine's record and journals nothing.</para>
///
/// <para><b>Never raising</b>: the triggers hold no <c>RAISE</c> and the inserts can violate no constraint (the three required columns are
/// always given a value, and a list that is not a JSON array journals nothing), so no writer's statement fails through them. A storage
/// failure fails the statement as it fails the record's write: capture is atomic with the write.</para>
///
/// <para><b>Bounded</b>: a row holds ids cut at 64 characters, codes at 32, and at most eight of a note's codes, never a word's text or key, a
/// note, a path or an account, so a row is under a KiB. The statement that inserts also deletes the rows past the newest 50,000, so a
/// writer built before the table, which never trims, still bounds it. A statement that adds or takes more than 64 words journals the first
/// 64 and one <c>words-overflow</c> row with the count past them.</para>
///
/// <para><b>The moves.</b> A <c>state</c> row is a move of the state, carrying the limit flag on either side; a <c>limited</c> row is the flag
/// moving with the state where it was, so a move that also limits is one row.</para>
///
/// <para><b>Part 2</b> adds the triggers of the quest log (<see cref="QuestLogSchema"/>), the asks (<see cref="AsksSchema"/>) and the channel
/// (<see cref="ChannelSchema"/>) in the same shape, each in its own store's schema after <see cref="Table"/>, which every one of them runs
/// first, and the change stream's <c>journal</c> family that wakes a reader who asks for it. <c>line_code</c> stays empty: it was set aside for
/// Daoris's lines, which are read from the quests per read and never stored.</para>
/// </remarks>
internal static class EventJournal
{
    /// <summary>The rows kept: the insert's own statement deletes the oldest past it.</summary>
    internal const int Rows = 50_000;

    /// <summary>The words one statement journals before an overflow row says the rest.</summary>
    internal const int WordsPerStatement = 64;

    /// <summary>The store's clock, whole milliseconds since the epoch, which every build of SQLite reads.</summary>
    private const string Now = "CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER)";

    /// <summary>The codes of the note's parts on NEW, at most eight, never their values or text; a note that is not JSON codes none.</summary>
    private const string NoteCodes = """
        CASE WHEN json_valid(NEW.note_parts) THEN
          (SELECT group_concat(coded.code, ',') FROM (
             SELECT substr(json_extract(part.value, '$.code'), 1, 64) AS code FROM json_each(NEW.note_parts) AS part
             WHERE part.type = 'object' AND json_extract(part.value, '$.code') IS NOT NULL ORDER BY part.key LIMIT 8) AS coded)
        END
        """;

    /// <summary>The work's facts on NEW: its kind, then the flags of what it holds, as closed codes.</summary>
    private const string Marks = """
        lower(substr(COALESCE(NEW.kind, ''), 1, 16))
          || CASE WHEN COALESCE(NEW.ask, '') <> '' THEN ',ask' ELSE '' END
          || CASE WHEN COALESCE(NEW.opinion, '') <> '' THEN ',opinion' ELSE '' END
          || CASE WHEN COALESCE(NEW.serves, '') <> '' THEN ',serves' ELSE '' END
          || CASE WHEN COALESCE(NEW.piece, '') <> '' THEN ',piece' ELSE '' END
          || CASE WHEN COALESCE(NEW.help_mode, '') <> '' THEN ',help-mode' ELSE '' END
          || CASE WHEN NEW.repository = 'daoris:help' THEN ',daoris:help' ELSE '' END
        """;

    private const string Record = "COALESCE(substr(NEW.id, 1, 64), '')";

    /// <summary>The trim: the rows past the newest <see cref="Rows"/>, in the statement that inserted.</summary>
    private static readonly string Trim = $"DELETE FROM event_journal WHERE position <= (SELECT max(position) FROM event_journal) - {Rows};";

    /// <summary>The words of <paramref name="now"/>'s list that <paramref name="before"/>'s list does not hold, by id, in list order.</summary>
    private static string WordsOnlyIn(string now, string before, string except) => $"""
        SELECT w.value AS v, w.key AS k FROM json_each(COALESCE({now}.said, '[]')) AS w
        WHERE w.type = 'object' AND json_extract(w.value, '$.id') IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM json_each(COALESCE({before}.said, '[]')) AS o
                          WHERE o.type = 'object' AND json_extract(o.value, '$.id') IS json_extract(w.value, '$.id'))
          {except}
        """;

    /// <summary>One row per word, up to 64, then one overflow row with the count past them (negative for words taken).</summary>
    private static string WordRows(string change, string now, string before, string except, string sign) => $"""
        INSERT INTO event_journal (at, record, family, change, workspace, quest, word, word_author, word_level, word_keyed)
        SELECT {Now}, {Record}, 'sessions', '{change}', substr(NEW.workspace, 1, 64), substr(NEW.quest, 1, 64),
               substr(json_extract(listed.v, '$.id'), 1, 64),
               CASE json_extract(listed.v, '$.from')
                 WHEN 'daoris' THEN 'daoris' WHEN 'agent' THEN 'agent' WHEN 'supervisor' THEN 'supervisor'
                 ELSE CASE WHEN COALESCE(json_extract(listed.v, '$.helper'), '') <> '' THEN 'helper'
                           WHEN COALESCE(json_extract(listed.v, '$.by'), '') <> '' THEN 'opinion'
                           ELSE 'person' END END,
               substr(json_extract(listed.v, '$.level'), 1, 32),
               CASE WHEN COALESCE(json_extract(listed.v, '$.key'), '') <> '' THEN 1 ELSE 0 END
        FROM ({WordsOnlyIn(now, before, except)} ORDER BY k LIMIT {WordsPerStatement}) AS listed;
        INSERT INTO event_journal (at, record, family, change, workspace, quest, words_over)
        SELECT {Now}, {Record}, 'sessions', 'words-overflow', substr(NEW.workspace, 1, 64), substr(NEW.quest, 1, 64),
               {sign}((SELECT count(*) FROM ({WordsOnlyIn(now, before, except)})) - {WordsPerStatement})
        WHERE json_array_length(COALESCE({now}.said, '[]')) > {WordsPerStatement}
          AND (SELECT count(*) FROM ({WordsOnlyIn(now, before, except)})) > {WordsPerStatement};
        """;

    /// <summary>A list that is not a JSON array, on either side, journals no word and never fails the statement that wrote it.</summary>
    private const string ListsAreArrays = """
        (OLD.said IS NULL OR (json_valid(OLD.said) AND json_type(OLD.said) = 'array'))
        AND (NEW.said IS NULL OR (json_valid(NEW.said) AND json_type(NEW.said) = 'array'))
        """;

    /// <summary>
    /// The table, its index and the store's identity, each created only where it is not already there. Every store whose table has a trigger
    /// that writes the journal runs this first, so a store opened alone on a new database never has a trigger writing to a table that is not
    /// there (a trigger that failed would fail its writer's statement).
    /// </summary>
    internal const string Table = """
        CREATE TABLE IF NOT EXISTS store_identity (one INTEGER PRIMARY KEY CHECK (one = 1), id TEXT NOT NULL);
        INSERT OR IGNORE INTO store_identity (one, id) VALUES (1, lower(hex(randomblob(16))));

        -- Every column but the position, the moment, the record and the change is nullable, so no writer's insert can leave out a value a
        -- constraint requires. AUTOINCREMENT, so a position is never reused after a trim or after the table is emptied.
        CREATE TABLE IF NOT EXISTS event_journal (
          position     INTEGER PRIMARY KEY AUTOINCREMENT,
          at           INTEGER NOT NULL,
          record       TEXT NOT NULL,
          family       TEXT NULL,
          change       TEXT NOT NULL,
          state_old    TEXT NULL,
          state_new    TEXT NULL,
          limited_old  INTEGER NULL,
          limited_new  INTEGER NULL,
          note_codes   TEXT NULL,
          marks        TEXT NULL,
          entry        TEXT NULL,
          from_session TEXT NULL,
          from_why     TEXT NULL,
          from_by      TEXT NULL,
          workspace    TEXT NULL,
          quest        TEXT NULL,
          word         TEXT NULL,
          word_author  TEXT NULL,
          word_level   TEXT NULL,
          word_keyed   INTEGER NULL,
          words_over   INTEGER NULL,
          op           TEXT NULL,
          ask          TEXT NULL,
          message      TEXT NULL,
          line_code    TEXT NULL
        );
        CREATE INDEX IF NOT EXISTS event_journal_record ON event_journal (record, position);
        """;

    /// <summary>The table and the triggers on <c>sessions</c>.</summary>
    internal static readonly string Schema = $"""
        {Table}

        CREATE TRIGGER IF NOT EXISTS event_journal_opened AFTER INSERT ON sessions
        WHEN NEW.origin IS NULL
        BEGIN
          INSERT INTO event_journal (at, record, family, change, state_new, marks, entry, from_session, from_why, from_by, workspace, quest)
          VALUES ({Now}, {Record}, 'sessions', 'opened', substr(NEW.state, 1, 32), {Marks}, substr(NEW.entry, 1, 64),
                  substr(NEW.from_session, 1, 64), substr(NEW.from_why, 1, 32), substr(NEW.from_by, 1, 32),
                  substr(NEW.workspace, 1, 64), substr(NEW.quest, 1, 64));
          {Trim}
        END;

        CREATE TRIGGER IF NOT EXISTS event_journal_state AFTER UPDATE OF state ON sessions
        WHEN OLD.state IS NOT NEW.state AND NEW.origin IS NULL
        BEGIN
          INSERT INTO event_journal (at, record, family, change, state_old, state_new, limited_old, limited_new, note_codes, marks, workspace, quest)
          VALUES ({Now}, {Record}, 'sessions', 'state', substr(OLD.state, 1, 32), substr(NEW.state, 1, 32),
                  COALESCE(OLD.limited, 0) <> 0, COALESCE(NEW.limited, 0) <> 0, {NoteCodes}, {Marks},
                  substr(NEW.workspace, 1, 64), substr(NEW.quest, 1, 64));
          {Trim}
        END;

        CREATE TRIGGER IF NOT EXISTS event_journal_limited AFTER UPDATE OF limited ON sessions
        WHEN (COALESCE(OLD.limited, 0) <> 0) IS NOT (COALESCE(NEW.limited, 0) <> 0) AND OLD.state IS NEW.state AND NEW.origin IS NULL
        BEGIN
          INSERT INTO event_journal (at, record, family, change, state_old, state_new, limited_old, limited_new, note_codes, marks, workspace, quest)
          VALUES ({Now}, {Record}, 'sessions', 'limited', substr(OLD.state, 1, 32), substr(NEW.state, 1, 32),
                  COALESCE(OLD.limited, 0) <> 0, COALESCE(NEW.limited, 0) <> 0, {NoteCodes}, {Marks},
                  substr(NEW.workspace, 1, 64), substr(NEW.quest, 1, 64));
          {Trim}
        END;

        CREATE TRIGGER IF NOT EXISTS event_journal_said AFTER UPDATE OF said ON sessions
        WHEN OLD.said IS NOT NEW.said AND NEW.origin IS NULL AND {ListsAreArrays}
        BEGIN
          {WordRows("word-added", "NEW", "OLD", "AND NOT (OLD.said IS NULL AND OLD.answer IS NOT NULL AND json_extract(w.value, '$.id') = 'answer')", "")}
          {WordRows("word-removed", "OLD", "NEW", "", "-")}
          {Trim}
        END;
        """;

    /// <summary>
    /// The quest log's trigger (row k part 2): one <c>quest-op</c> row for each operation kept, made here or integrated from a remote, naming the
    /// quest and the operation's kind, never its payload. Bookkeeping on an operation kept (a remote's number, a rebase's rewrite in place) and a
    /// delete of older operations are not moves and journal nothing, as the quests family does not wake for them.
    /// </summary>
    internal static readonly string QuestLogSchema = $"""
        {Table}

        CREATE TRIGGER IF NOT EXISTS event_journal_quest_op AFTER INSERT ON quest_log
        BEGIN
          INSERT INTO event_journal (at, record, family, change, op, quest)
          VALUES ({Now}, COALESCE(substr(NEW.quest, 1, 64), ''), 'quest_log', 'quest-op', substr(NEW.kind, 1, 32), substr(NEW.quest, 1, 64));
          {Trim}
        END;
        """;

    /// <summary>
    /// The asks' triggers (row k part 2): an <c>ask</c> row for an ask kept and for each move of its state, guarded on the state changing, so a
    /// whole save that changes none, a word, a list appended to, an intake bound and a delete journal nothing. The row names the ask, its scope
    /// and the state on either side, never its sentence, note or words.
    /// </summary>
    internal static readonly string AsksSchema = $"""
        {Table}

        CREATE TRIGGER IF NOT EXISTS event_journal_ask_kept AFTER INSERT ON asks
        BEGIN
          INSERT INTO event_journal (at, record, family, change, state_new, ask, workspace)
          VALUES ({Now}, COALESCE(substr(NEW.id, 1, 64), ''), 'asks', 'ask', substr(NEW.state, 1, 32), substr(NEW.id, 1, 64), substr(NEW.workspace, 1, 64));
          {Trim}
        END;

        CREATE TRIGGER IF NOT EXISTS event_journal_ask_state AFTER UPDATE OF state ON asks
        WHEN OLD.state IS NOT NEW.state
        BEGIN
          INSERT INTO event_journal (at, record, family, change, state_old, state_new, ask, workspace)
          VALUES ({Now}, COALESCE(substr(NEW.id, 1, 64), ''), 'asks', 'ask', substr(OLD.state, 1, 32), substr(NEW.state, 1, 32),
                  substr(NEW.id, 1, 64), substr(NEW.workspace, 1, 64));
          {Trim}
        END;
        """;

    /// <summary>
    /// The memory log's triggers (KNOWCENTRAL2c, the memory coordinator design §3): a <c>memory</c> row for each operation kept and each move
    /// of its state, naming the operation, its kind, its target or the entry it wrote, its session and scope; never its text, reason or
    /// repository. Waking the journal's readers wakes the applier, and the journal never travels.
    /// </summary>
    internal static readonly string MemorySchema = $"""
        {Table}

        CREATE TRIGGER IF NOT EXISTS event_journal_memory_op AFTER INSERT ON memory_ops
        BEGIN
          INSERT INTO event_journal (at, record, family, change, state_new, op, entry, from_session, workspace)
          VALUES ({Now}, COALESCE(substr(NEW.id, 1, 64), ''), 'memory', 'memory', substr(NEW.state, 1, 32), substr(NEW.kind, 1, 32),
                  substr(COALESCE(NEW.target, NEW.entry), 1, 64), substr(NEW.session, 1, 64), substr(NEW.workspace, 1, 64));
          {Trim}
        END;

        CREATE TRIGGER IF NOT EXISTS event_journal_memory_state AFTER UPDATE OF state ON memory_ops
        WHEN OLD.state IS NOT NEW.state
        BEGIN
          INSERT INTO event_journal (at, record, family, change, state_old, state_new, op, entry, from_session, workspace)
          VALUES ({Now}, COALESCE(substr(NEW.id, 1, 64), ''), 'memory', 'memory', substr(OLD.state, 1, 32), substr(NEW.state, 1, 32),
                  substr(NEW.kind, 1, 32), substr(COALESCE(NEW.target, NEW.entry), 1, 64), substr(NEW.session, 1, 64), substr(NEW.workspace, 1, 64));
          {Trim}
        END;
        """;

    /// <summary>
    /// The channel's trigger (row k part 2): one <c>message</c> row for each message kept, the person's, a session's and a teammate's mirrored
    /// alike (a mirror seen again, a post the cap refused and a message already kept are no insert, so journal nothing). A message is never
    /// changed once kept, and a read mark, a cursor and a channel forgotten with its ask are not moves. The row names the message, its ask, its
    /// scope, the entry and the session it came from as ids, and its kind as closed codes (<c>message</c>, <c>question</c> or <c>answer</c>,
    /// then <c>urgent</c> and <c>mirrored</c>); never its text, its repository or its agent. The channel's <c>line_code</c> stays empty: Daoris's
    /// lines are read from the quests per read and never stored (§4.2).
    /// </summary>
    internal static readonly string ChannelSchema = $"""
        {Table}

        CREATE TRIGGER IF NOT EXISTS event_journal_message AFTER INSERT ON channel
        BEGIN
          INSERT INTO event_journal (at, record, family, change, message, ask, workspace, entry, from_session, marks)
          VALUES ({Now}, COALESCE(substr(NEW.id, 1, 64), ''), 'channel', 'message', substr(NEW.id, 1, 64), substr(NEW.ask, 1, 64),
                  substr(NEW.workspace, 1, 64), substr(NEW.entry, 1, 64), substr(NEW.session, 1, 64),
                  CASE WHEN COALESCE(NEW.question, 0) <> 0 THEN 'question' WHEN COALESCE(NEW.re, '') <> '' THEN 'answer' ELSE 'message' END
                    || CASE WHEN NEW.urgent = 1 THEN ',urgent' ELSE '' END
                    || CASE WHEN NEW.origin IS NOT NULL THEN ',mirrored' ELSE '' END);
          {Trim}
        END;
        """;
}
