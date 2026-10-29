using System.Globalization;
using System.Text;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// FOUNDATION1 row k part 1 (D212; the foundation design §4.1, §19.12): the service keeps an event journal of the ledger's raw transitions,
/// minted by triggers on <c>sessions</c> in the statement that writes the record, so every writer mints it, a connector built before the
/// table included, and read by nothing yet. Every case runs on a database file of its own under the repository's gitignored
/// <c>_fixtures/</c>, as <see cref="SaidSettledTests"/> does.
/// </summary>
public sealed class EventJournalTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-30T09:00:00Z");

    private readonly string _root = Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures", "event-journal", Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(_root, "service.db"), Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        await _connection.OpenAsync();
        await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        SqlitePools.Release(Path.Combine(_root, "service.db"));
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A file the scanner holds for a moment: the next prune of _fixtures takes it.
        }
    }

    private sealed record Row(
        long Position, string Record, string? Family, string Change, string? StateOld, string? StateNew, long? LimitedOld, long? LimitedNew,
        string? NoteCodes, string? Marks, string? Word, string? Author, string? Level, long? Keyed, long? Over);

    private async Task<List<Row>> RowsAsync(string? record = null)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT position, record, family, change, state_old, state_new, limited_old, limited_new, note_codes, marks, word, word_author,
                   word_level, word_keyed, words_over
            FROM event_journal WHERE $record IS NULL OR record = $record ORDER BY position
            """;
        command.Parameters.AddWithValue("$record", (object?)record ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Row>();
        while (await reader.ReadAsync())
        {
            string? Text(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
            long? Number(int i) => reader.IsDBNull(i) ? null : reader.GetInt64(i);
            rows.Add(new Row(
                reader.GetInt64(0), reader.GetString(1), Text(2), reader.GetString(3), Text(4), Text(5), Number(6), Number(7), Text(8), Text(9),
                Text(10), Text(11), Text(12), Number(13), Number(14)));
        }

        return rows;
    }

    private async Task<long> ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return await command.ExecuteNonQueryAsync();
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private Task<Session> OpenedAsync(SessionKind kind = SessionKind.Chat) => _sessions.CreateAsync(null, "Owner", "stub", Now, workspace: "team", kind: kind);

    private Task<Session?> MoveAsync(string id, SessionState state, string? note = null, string? noteParts = null, bool limit = false, bool clearSaid = false, bool forgive = false) =>
        _sessions.SetStateAsync(id, state, note, null, null, Now, limit: limit, clearSaid: clearSaid, forgive: forgive, noteParts: noteParts);

    private static string Coded(params string[] codes) =>
        "[" + string.Join(",", codes.Select(code => $$"""{"code":"{{code}}","values":{},"text":"private text"}""")) + "]";

    // ---- the transitions, minted by the triggers ----

    /// <summary>
    /// A record opened, and each move of its state after it, is one row each, in the order the statements committed, as the raw transition:
    /// the record, the state on either side, never a word of its note.
    /// </summary>
    [Fact]
    public async Task Each_move_of_a_record_is_one_row_in_order_as_its_raw_transition()
    {
        var record = await OpenedAsync();
        await MoveAsync(record.Id, SessionState.Working);
        await MoveAsync(record.Id, SessionState.Completed, "private note text");

        var rows = await RowsAsync();

        Assert.Equal(
            [("opened", null, "Queued"), ("state", "Queued", "Working"), ("state", "Working", "Completed")],
            rows.Select(row => (row.Change, row.StateOld, row.StateNew)).ToList());
        Assert.All(rows, row => Assert.Equal((record.Id, "sessions"), (row.Record, row.Family)));
        Assert.Equal(rows.Select(row => row.Position).Order().ToList(), rows.Select(row => row.Position).ToList());
        Assert.Equal(rows.Count, rows.Select(row => row.Position).Distinct().Count());
        Assert.Equal("chat", rows[0].Marks);
    }

    /// <summary>
    /// A write that names <c>state</c>, <c>limited</c> and <c>said</c> without changing them, a note-only move, a same-state move and an attempt
    /// mark journal nothing: a trigger that fired on the statement's list would journal every move three times.
    /// </summary>
    [Fact]
    public async Task A_same_state_write_a_note_only_move_and_an_attempt_mark_journal_nothing()
    {
        var record = await OpenedAsync();
        await MoveAsync(record.Id, SessionState.Working);
        var word = new SaidWord("w1", "the words", Now, [], From: SaidFrom.Daoris, Key: "k/1");
        await _sessions.KeepSaidAsync(record.Id, word);
        var before = await RowsAsync();

        await MoveAsync(record.Id, SessionState.Working);
        await MoveAsync(record.Id, SessionState.Working, "a note", Coded("ended.exit"));
        await _sessions.KeepSaidAsync(record.Id, word with { Id = "w2" }, "an answer line", noteParts: Coded("ended.parked-asked"));
        var afterKept = await RowsAsync();
        await ExecuteAsync("UPDATE sessions SET note = 'only the note', updated = $updated WHERE id = $id", ("$updated", "2026-10-30T10:00:00Z"), ("$id", record.Id));
        Assert.True(await _sessions.MarkAttemptAsync(record.Id, "w1", Now));

        Assert.Equal(before.Count + 1, afterKept.Count);
        Assert.Equal("word-added", afterKept[^1].Change);
        Assert.Equal(afterKept.Count, (await RowsAsync()).Count);
    }

    /// <summary>
    /// Another machine's record is not this machine's fact: a mirror pass, which upserts verbatim, journals nothing, on its insert and on every
    /// pass after, and neither does a move or a word on a record of its origin's.
    /// </summary>
    [Fact]
    public async Task A_mirror_pass_journals_nothing()
    {
        var fed = new Session("alice-laptop/ab12cd34", "abc123", "Owner", "claude-code", SessionState.Working, null, null, null, Now, Now);

        await _sessions.MirrorAsync(fed);
        await _sessions.MirrorAsync(fed with { State = SessionState.Completed, Limit = true, Note = "ended", NoteParts = Coded("ended.exit") });

        Assert.Equal(0, await ScalarAsync("SELECT count(*) FROM event_journal"));
    }

    /// <summary>
    /// A limit that comes with a move is the same row: the row carries the flag on either side, so one move is one row. A flag that moves alone
    /// is its own row, and a forgiving reopen clears it in the row of the state it leaves.
    /// </summary>
    [Fact]
    public async Task A_limit_moving_with_the_state_is_its_rows_flag_and_alone_is_its_own_row()
    {
        var record = await OpenedAsync();
        await MoveAsync(record.Id, SessionState.Working);
        await MoveAsync(record.Id, SessionState.Failed, "limited", Coded("ended.limited"), limit: true);
        await ExecuteAsync("UPDATE sessions SET limited = NULL WHERE id = $id", ("$id", record.Id));
        await ExecuteAsync("UPDATE sessions SET limited = 1 WHERE id = $id", ("$id", record.Id));
        await MoveAsync(record.Id, SessionState.Working, forgive: true);

        var rows = (await RowsAsync()).Skip(2).ToList();

        Assert.Equal(
            [("state", "Working", "Failed", 0L, 1L), ("limited", "Failed", "Failed", 1L, 0L), ("limited", "Failed", "Failed", 0L, 1L), ("state", "Failed", "Working", 1L, 0L)],
            rows.Select(row => (row.Change, row.StateOld, row.StateNew, row.LimitedOld, row.LimitedNew)).ToList());
    }

    /// <summary>
    /// A word kept and a word taken are a row each, by its id and its author class, its level and whether it carries a key, never its text or
    /// its key; a record's words cleared by a move are a row each beside the move's own.
    /// </summary>
    [Fact]
    public async Task A_word_kept_and_a_word_taken_are_rows_with_no_text_and_no_key()
    {
        var record = await OpenedAsync();
        await _sessions.KeepSaidAsync(record.Id, new SaidWord("w1", "secret person words", Now, []));
        await _sessions.KeepSaidAsync(record.Id, new SaidWord("w2", "secret agent words", Now, [], From: SaidFrom.Agent, Key: "secret/key", Level: SaidLevel.Urgent));
        await _sessions.KeepSaidAsync(record.Id, new SaidWord("w3", "secret opinion", Now, [], By: "reviewer"));
        await _sessions.KeepSaidAsync(record.Id, new SaidWord("w4", "secret help", Now, [], Helper: "helper-1"));
        await _sessions.KeepSaidAsync(record.Id, new SaidWord("w5", "secret supervisor", Now, [], From: SaidFrom.Supervisor));
        await _sessions.KeepSaidAsync(record.Id, new SaidWord("w6", "secret daoris", Now, [], From: SaidFrom.Daoris));
        await _sessions.TakeSaidAsync(record.Id, ["w2"]);
        await MoveAsync(record.Id, SessionState.Completed, clearSaid: true);

        var rows = (await RowsAsync()).Skip(1).ToList();

        Assert.Equal(
            [
                ("word-added", "w1", "person", null, 0L), ("word-added", "w2", "agent", "urgent", 1L), ("word-added", "w3", "opinion", null, 0L),
                ("word-added", "w4", "helper", null, 0L), ("word-added", "w5", "supervisor", null, 0L), ("word-added", "w6", "daoris", null, 0L),
                ("word-removed", "w2", "agent", "urgent", 1L),
            ],
            rows.Take(7).Select(row => (row.Change, row.Word, row.Author, row.Level, row.Keyed)).ToList());
        Assert.Equal(
            ["w1", "w3", "w4", "w5", "w6"],
            rows.Skip(7).Where(row => row.Change == "word-removed").Select(row => row.Word!).Order().ToList());
        Assert.Contains(rows.Skip(7), row => row is { Change: "state", StateOld: "Queued", StateNew: "Completed" });
        Assert.Equal(1, rows.Skip(7).Count(row => row.Change == "state"));

        await using var all = _connection.CreateCommand();
        all.CommandText = "SELECT * FROM event_journal";
        await using var reader = await all.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "";
                Assert.DoesNotContain("secret", value, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// The answer of a record from before the list sits in the old column, so it is not a word added when the list begins beside it.
    /// </summary>
    [Fact]
    public async Task An_answer_in_the_old_column_is_not_a_word_added_when_the_list_begins_beside_it()
    {
        var record = await OpenedAsync();
        await ExecuteAsync("UPDATE sessions SET answer = 'old answer', said = NULL WHERE id = $id", ("$id", record.Id));
        Assert.Single(await RowsAsync());

        await _sessions.KeepSaidAsync(record.Id, new SaidWord("w1", "next", Now, []));

        Assert.Equal(["opened", "word-added"], (await RowsAsync()).Select(row => row.Change).ToList());
        Assert.Equal("w1", (await RowsAsync())[1].Word);
    }

    // ---- the immutable inputs ----

    /// <summary>
    /// An end, a reopen and an end: each end is coded from its own row, whatever the record's note says by the time a reader runs, since a
    /// record's state and notes are overwritten.
    /// </summary>
    [Fact]
    public async Task An_end_a_reopen_and_an_end_are_each_coded_from_their_own_rows()
    {
        var record = await OpenedAsync();
        await MoveAsync(record.Id, SessionState.Working);
        await MoveAsync(record.Id, SessionState.Failed, "one", Coded("ended.exit", "ended.first"), limit: true);
        await MoveAsync(record.Id, SessionState.Working, "again", Coded("started.reopened"), forgive: true);
        await MoveAsync(record.Id, SessionState.Completed, "two", Coded("ended.done"));

        var moves = (await RowsAsync()).Where(row => row.Change == "state").ToList();

        Assert.Equal(
            [("Queued", "Working", null), ("Working", "Failed", "ended.exit,ended.first"), ("Failed", "Working", "started.reopened"), ("Working", "Completed", "ended.done")],
            moves.Select(row => (row.StateOld, row.StateNew, row.NoteCodes)).ToList());
        Assert.Equal([0L, 1L, 0L, 0L], moves.Select(row => row.LimitedNew!.Value).ToList());
        // The record itself holds only the last end: what the first said is in its row alone.
        var held = (await _sessions.FindAsync(record.Id))!.NoteParts!;
        Assert.Contains("ended.done", held, StringComparison.Ordinal);
        Assert.DoesNotContain("ended.first", held, StringComparison.Ordinal);
    }

    /// <summary>A park, its answer and a park again: each park's rows carry the park's own codes and the words that arrived between.</summary>
    [Fact]
    public async Task A_park_its_answer_and_a_park_again_are_each_coded_from_their_own_rows()
    {
        var record = await OpenedAsync(SessionKind.Driven);
        await MoveAsync(record.Id, SessionState.Working);
        await MoveAsync(record.Id, SessionState.AwaitingPerson, "which port?", Coded("ended.parked-asked"));
        await _sessions.KeepSaidAsync(record.Id, new SaidWord("a1", "8080", Now, []));
        await MoveAsync(record.Id, SessionState.Working, clearSaid: true);
        await MoveAsync(record.Id, SessionState.AwaitingPerson, "which host?", Coded("ended.parked-pipe"));

        var rows = (await RowsAsync()).Skip(1).ToList();

        Assert.Equal(
            [
                ("Queued", "Working", null),
                ("Working", "AwaitingPerson", "ended.parked-asked"),
                ("AwaitingPerson", "Working", "ended.parked-asked"),
                ("Working", "AwaitingPerson", "ended.parked-pipe"),
            ],
            rows.Where(row => row.Change == "state").Select(row => (row.StateOld, row.StateNew, row.NoteCodes)).ToList());
        Assert.Equal(
            [("word-added", "a1"), ("word-removed", "a1")],
            rows.Where(row => row.Change != "state").Select(row => (row.Change, row.Word!)).ToList());
        // The park's answer sits between the two parks, in the order the statements committed; the move that takes the answer's word away
        // is two rows of one statement, whose order between themselves is the engine's.
        Assert.Equal(
            ["state", "state", "word-added", "state", "state", "state"],
            rows.Select(row => row.Change == "word-removed" ? "state" : row.Change).ToList());
    }

    // ---- the overflow, the bound per row and per table ----

    /// <summary>A statement that adds more than 64 words journals the first 64 and one overflow row with the count, so an overflow is said.</summary>
    [Fact]
    public async Task More_than_sixty_four_words_in_one_statement_journal_sixty_four_and_one_overflow_row()
    {
        var record = await OpenedAsync();
        var words = new StringBuilder("[");
        for (var i = 0; i < 70; i++) words.Append(CultureInfo.InvariantCulture, $$"""{{(i == 0 ? "" : ",")}}{"id":"w{{i:D2}}","text":"t","at":"2026-10-30T09:00:00Z","files":[]}""");
        words.Append(']');

        await ExecuteAsync("UPDATE sessions SET said = $said WHERE id = $id", ("$said", words.ToString()), ("$id", record.Id));

        var rows = (await RowsAsync()).Skip(1).ToList();
        Assert.Equal(65, rows.Count);
        Assert.Equal(64, rows.Count(row => row.Change == "word-added"));
        Assert.Equal(Enumerable.Range(0, 64).Select(i => $"w{i:D2}").ToList(), rows.Where(row => row.Change == "word-added").Select(row => row.Word!).ToList());
        Assert.Equal(("words-overflow", 6L), (rows[^1].Change, rows[^1].Over));

        await ExecuteAsync("UPDATE sessions SET said = NULL WHERE id = $id", ("$id", record.Id));
        var removed = (await RowsAsync()).Skip(66).ToList();
        Assert.Equal(64, removed.Count(row => row.Change == "word-removed"));
        Assert.Equal(("words-overflow", -6L), (removed[^1].Change, removed[^1].Over));
    }

    /// <summary>
    /// A row is at most a KiB whatever the record holds: ids are cut at 64 characters, codes at 32, and a note's codes at eight, so a record
    /// with long values and a dozen long codes journals rows the bound still holds.
    /// </summary>
    [Fact]
    public async Task A_row_is_bounded_whatever_the_record_holds()
    {
        var parts = "[" + string.Join(",", Enumerable.Range(0, 12).Select(i =>
            "{\"code\":\"" + new string((char)('a' + i), 100) + "\",\"values\":{}}")) + "]";
        await ExecuteAsync(
            """
            INSERT INTO sessions (id, repository, adapter, state, created, updated, workspace, kind, from_session, from_why, from_by, entry, quest, ask, opinion, serves, piece, help_mode)
            VALUES ($id, 'r', 'a', $state, 'c', 'u', $long, 'Chat', $long, $long, $long, $long, $long, 'a', 'o', 's', 'p', 'm')
            """,
            ("$id", new string('i', 200)), ("$state", new string('S', 200)), ("$long", new string('x', 300)));
        await ExecuteAsync("UPDATE sessions SET state = $state, note_parts = $parts", ("$state", new string('T', 200)), ("$parts", parts));
        var words = "[" + string.Join(",", Enumerable.Range(0, 3).Select(i =>
            "{\"id\":\"" + new string('w', 200) + i + "\",\"from\":\"agent\",\"level\":\"" + new string('l', 200) + "\",\"key\":\"k\"}")) + "]";
        await ExecuteAsync("UPDATE sessions SET said = $said", ("$said", words));

        var rows = await RowsAsync();
        Assert.Equal(5, rows.Count);
        Assert.Equal(8, rows[1].NoteCodes!.Split(',').Length);
        Assert.All(rows[1].NoteCodes!.Split(','), code => Assert.True(code.Length <= 64));
        Assert.All(rows, row => Assert.True(row.Record.Length <= 64));

        var widest = await ScalarAsync(
            """
            SELECT max(length(record) + COALESCE(length(family), 0) + length(change) + COALESCE(length(state_old), 0) + COALESCE(length(state_new), 0)
              + COALESCE(length(note_codes), 0) + COALESCE(length(marks), 0) + COALESCE(length(entry), 0) + COALESCE(length(from_session), 0)
              + COALESCE(length(from_why), 0) + COALESCE(length(from_by), 0) + COALESCE(length(workspace), 0) + COALESCE(length(quest), 0)
              + COALESCE(length(word), 0) + COALESCE(length(word_author), 0) + COALESCE(length(word_level), 0) + COALESCE(length(op), 0)
              + COALESCE(length(ask), 0) + COALESCE(length(message), 0) + COALESCE(length(line_code), 0))
            FROM event_journal
            """);
        Assert.InRange(widest, 1, 1024);
    }

    /// <summary>
    /// The insert's own statement deletes the oldest rows past 50,000, so a writer that never trims bounds the table: the statements here are an
    /// older build's own. A position is never reused after a trim, or after the table is emptied.
    /// </summary>
    [Fact]
    public async Task The_table_holds_at_most_fifty_thousand_rows_trimmed_by_the_inserting_statement_and_positions_are_never_reused()
    {
        var record = await OpenedAsync();
        await ExecuteAsync(
            """
            WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 49999)
            INSERT INTO event_journal (at, record, family, change) SELECT 0, 'filler', 'sessions', 'state' FROM n
            """);
        Assert.Equal(50000, await ScalarAsync("SELECT count(*) FROM event_journal"));
        var floor = await ScalarAsync("SELECT min(position) FROM event_journal");

        // An older build's statement: a state move without the columns that came after.
        await ExecuteAsync("UPDATE sessions SET state = 'Working', updated = 'u' WHERE id = $id", ("$id", record.Id));
        await ExecuteAsync("UPDATE sessions SET state = 'Completed', updated = 'u' WHERE id = $id", ("$id", record.Id));

        Assert.Equal(50000, await ScalarAsync("SELECT count(*) FROM event_journal"));
        Assert.Equal(floor + 2, await ScalarAsync("SELECT min(position) FROM event_journal"));
        Assert.Equal(2, await ScalarAsync($"SELECT count(*) FROM event_journal WHERE record = '{record.Id}'"));

        var newest = await ScalarAsync("SELECT max(position) FROM event_journal");
        await ExecuteAsync("DELETE FROM event_journal");
        await ExecuteAsync("UPDATE sessions SET state = 'Failed', updated = 'u' WHERE id = $id", ("$id", record.Id));
        Assert.True(await ScalarAsync("SELECT min(position) FROM event_journal") > newest);
    }

    // ---- an older build's own statements ----

    /// <summary>
    /// An older connector composes its own statements over the migrated database and does not know the table: its insert, its state move
    /// before <c>limited</c> and <c>note_parts</c>, and its words' whole-list save mint rows and never fail.
    /// </summary>
    [Fact]
    public async Task An_older_builds_own_statements_over_the_migrated_database_mint_rows_and_never_fail()
    {
        await ExecuteAsync(
            """
            INSERT INTO sessions (id, quest, repository, adapter, state, note, evidence, transcript, created, updated, workspace, kind, harness_version, profile, tree, base_commit, revision)
            VALUES ('older001', NULL, 'Owner', 'stub', 'Queued', NULL, NULL, NULL, 'c', 'u', 'default', 'Driven', NULL, NULL, NULL, NULL, 7)
            """);
        await ExecuteAsync("UPDATE sessions SET state = 'Working', note = 'n', evidence = NULL, transcript = NULL, updated = 'u2' WHERE id = 'older001'");
        await ExecuteAsync(
            "UPDATE sessions SET said = '[{\"id\":\"w1\",\"text\":\"t\",\"at\":\"a\",\"files\":[]}]', answer = NULL WHERE id = 'older001' AND origin IS NULL");
        await ExecuteAsync("UPDATE sessions SET said = $said, answer = NULL WHERE id = 'older001' AND origin IS NULL", ("$said", DBNull.Value));

        Assert.Equal(
            [("opened", null), ("state", null), ("word-added", "w1"), ("word-removed", "w1")],
            (await RowsAsync("older001")).Select(row => (row.Change, row.Word)).ToList());
    }

    /// <summary>A note's parts or a list of words that is not what the writer meant never fails the statement that wrote it, and journals what it can.</summary>
    [Fact]
    public async Task Malformed_notes_and_lists_never_fail_the_statement()
    {
        var record = await OpenedAsync();
        await ExecuteAsync("UPDATE sessions SET state = 'Working', note_parts = 'not json' WHERE id = $id", ("$id", record.Id));
        await ExecuteAsync("UPDATE sessions SET state = 'Failed', note_parts = '{\"code\":\"x\"}' WHERE id = $id", ("$id", record.Id));
        await ExecuteAsync("UPDATE sessions SET state = 'Working', note_parts = '[1,\"two\",{\"nothing\":1},{\"code\":5}]' WHERE id = $id", ("$id", record.Id));
        await ExecuteAsync("UPDATE sessions SET state = 'Failed', note_parts = NULL WHERE id = $id", ("$id", record.Id));
        await ExecuteAsync("UPDATE sessions SET said = '[]' WHERE id = $id", ("$id", record.Id));
        await ExecuteAsync("UPDATE sessions SET said = '{\"a\":1}' WHERE id = $id", ("$id", record.Id));
        await ExecuteAsync("UPDATE sessions SET said = '[1, \"two\", {\"no\":\"id\"}]' WHERE id = $id", ("$id", record.Id));
        await ExecuteAsync("UPDATE sessions SET said = NULL WHERE id = $id", ("$id", record.Id));

        var rows = await RowsAsync();

        Assert.Equal(5, rows.Count);
        Assert.Equal([null, null, "5", null], rows.Skip(1).Select(row => row.NoteCodes).ToList());
    }

    // ---- the store's identity ----

    /// <summary>
    /// The store's identity is written by the schema's initialisation, once, and an existing stamp (old files carry one, written by the change
    /// stream's observer) is kept as it stands.
    /// </summary>
    [Fact]
    public async Task The_schema_writes_the_stores_identity_once_and_keeps_an_existing_stamp()
    {
        var first = await StampsAsync();
        Assert.Single(first);
        Assert.Matches("^[0-9a-f]{32}$", first[0]);

        await SessionStore.OpenAsync(_connection);
        Assert.Equal(first, await StampsAsync());

        await ExecuteAsync("UPDATE store_identity SET id = 'stamped-by-an-older-build' WHERE one = 1");
        await SessionStore.OpenAsync(_connection);
        Assert.Equal(["stamped-by-an-older-build"], await StampsAsync());
    }

    private async Task<List<string>> StampsAsync()
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT id FROM store_identity";
        await using var reader = await command.ExecuteReaderAsync();
        var stamps = new List<string>();
        while (await reader.ReadAsync()) stamps.Add(reader.GetString(0));
        return stamps;
    }
}
