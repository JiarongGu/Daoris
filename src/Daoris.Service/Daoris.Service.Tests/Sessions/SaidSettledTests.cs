using System.Globalization;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// FOUNDATION1 row p (D212; the foundation design §3.4 rule 5, §19.2): a word that leaves a record's <c>said</c> is settled by the statement
/// that takes it, one row per record left, word id and producer key, and a producer's retry after the take is answered from it by every
/// keeping door but the supervisor's, inside the door's own exclusive step and before any refusal but the record's absence, over the line's
/// records back along <c>from</c> (the head walk's forward records are row d-ii's). Every case here runs on a database file of its own under
/// the repository's gitignored <c>_fixtures/</c>, since the retry that races a take needs two connections, which is what two processes are.
/// </summary>
public sealed class SaidSettledTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly string _root = Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures", "said-settled", Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        (_connection, _sessions, _ledger) = await OpenAsync();
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

    /// <summary>A host's connection to the file, its stores and its ledger: a second call is another process's.</summary>
    private async Task<(SqliteConnection, SessionStore, SessionLedger)> OpenAsync()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(_root, "service.db"), Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        await connection.OpenAsync();
        var quests = await QuestStore.OpenAsync(connection);
        var sessions = await SessionStore.OpenAsync(connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        // Registered with a root, so a keyed open of a conversation from a record of this repository finds its tree (row d-i's open).
        await service.RegisterAsync(new Registration("Owner", true, "A repo.", [], [], [], 0, Root: "/trees/owner"), Now);
        return (connection, sessions, new SessionLedger(quests, sessions, service));
    }

    /// <summary>
    /// Holds the next statement <paramref name="connection"/> runs that fires <paramref name="on"/>, inside that statement's own step, by a
    /// function and a TEMP trigger only that connection sees: the step has begun and holds the database's write lock, and no other
    /// connection's step begins until the release is set. So two hosts' steps are ordered by the test, never by a guess at timing.
    /// </summary>
    private static async Task<(TaskCompletionSource Held, TaskCompletionSource Release)> HoldAsync(SqliteConnection connection, string on)
    {
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.CreateFunction("hold_here", () =>
        {
            held.TrySetResult();
            release.Task.Wait(TimeSpan.FromSeconds(20));
            return 0;
        });
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE TEMP TRIGGER held_here {on} BEGIN SELECT hold_here(); END";
        await command.ExecuteNonQueryAsync();
        return (held, release);
    }

    private async Task<Session> EndedAsync(SessionStore? on = null, string? from = null, bool ownEntry = true)
    {
        var sessions = on ?? _sessions;
        var made = await sessions.CreateAsync(
            quest: null, "Owner", "stub", Now, kind: SessionKind.Chat, from: from, fromWhy: from is null ? null : "press", fromBy: from is null ? null : "person",
            ownEntry: ownEntry);
        return (await sessions.SetStateAsync(made.Id, SessionState.Completed, null, null, null, Now))!;
    }

    private Task<SessionSayOutcome> SayAsync(string id, string key, bool settled = true, bool running = false, string text = "the words", SessionLedger? by = null) =>
        (by ?? _ledger).SayAsync(id, text, null, Now, key: key, from: SaidFrom.Daoris, running: running, settled: settled);

    private async Task<List<(string Session, string Word, string Key, long At)>> RowsAsync(SqliteConnection? on = null)
    {
        await using var command = (on ?? _connection).CreateCommand();
        command.CommandText = "SELECT session, word, key, at FROM said_settled ORDER BY session, word, key";
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(string, string, string, long)>();
        while (await reader.ReadAsync()) rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3)));
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

    // ---- the settlement, written by the trigger ----

    /// <summary>
    /// A word that leaves <c>said</c> leaves a row on the record it left, by its id and its producer's key, or <c>id:&lt;word id&gt;</c> where it
    /// had none; keeping a word and marking its attempt settle nothing, since no word left.
    /// </summary>
    [Fact]
    public async Task A_take_settles_each_word_it_takes_under_its_key_or_its_id_and_a_keep_or_an_attempt_settles_none()
    {
        var record = await EndedAsync();
        var keyed = (await SayAsync(record.Id, "producer/1")).Word!;
        var keyless = (await _ledger.SayAsync(record.Id, "no key", null, Now)).Word!;
        await _sessions.MarkAttemptAsync(record.Id, keyed.Id, Now);

        Assert.Empty(await RowsAsync());

        await _sessions.TakeSaidAsync(record.Id, [keyed.Id, keyless.Id]);

        Assert.Equal(
            new[]
            {
                (record.Id, keyed.Id, "producer/1"),
                (record.Id, keyless.Id, $"id:{keyless.Id}"),
            }.Order().ToList(),
            (await RowsAsync()).Select(row => (row.Session, row.Word, row.Key)).Order().ToList());
        Assert.All(await RowsAsync(), row => Assert.InRange(row.At, Now.ToUnixTimeSeconds() - 5, Now.ToUnixTimeSeconds() + 60));
    }

    /// <summary>
    /// Every statement that leaves a word off a record settles it, not only the take: a park's clear and a state move that clears the words
    /// (<c>SetStateAsync</c>'s <c>clearSaid</c>), the person's take-back, and the owner's: it says only that the word left.
    /// </summary>
    [Fact]
    public async Task A_state_move_that_clears_the_words_and_a_take_back_settle_them_too()
    {
        var cleared = await EndedAsync();
        var word = (await SayAsync(cleared.Id, "producer/clear")).Word!;
        await _sessions.SetStateAsync(cleared.Id, SessionState.Completed, null, null, null, Now, clearSaid: true);

        var backed = await EndedAsync();
        var taken = (await SayAsync(backed.Id, "producer/back")).Word!;
        var done = await _ledger.TakeBackAsync(backed.Id, taken.Id, null, Now);
        Assert.Equal(SessionSayRefusal.None, done.Refusal);

        Assert.Equal(
            new[] { (cleared.Id, word.Id, "producer/clear"), (backed.Id, taken.Id, "producer/back") }.Order().ToList(),
            (await RowsAsync()).Select(row => (row.Session, row.Word, row.Key)).Order().ToList());
    }

    /// <summary>
    /// A record from before <c>said</c> holds its answer in the old column, which a take leaves as it was: nothing settles. Once a word is
    /// kept beside it, the answer is the list's first word under the id <c>answer</c> and settles as any keyless word does, on its own record.
    /// </summary>
    [Fact]
    public async Task An_answer_still_in_the_old_column_settles_nothing_and_beside_a_kept_word_settles_as_answer_on_its_record()
    {
        var plain = await EndedAsync();
        await ExecuteAsync("UPDATE sessions SET answer = 'old answer', said = NULL WHERE id = $id", ("$id", plain.Id));
        await _sessions.TakeSaidAsync(plain.Id, ["answer"]);
        Assert.Empty(await RowsAsync());

        var beside = await EndedAsync();
        await ExecuteAsync("UPDATE sessions SET answer = 'old answer', said = NULL WHERE id = $id", ("$id", beside.Id));
        var word = (await SayAsync(beside.Id, "producer/beside")).Word!;
        await _sessions.TakeSaidAsync(beside.Id, ["answer", word.Id]);

        Assert.Equal(
            new[] { (beside.Id, "answer", "id:answer"), (beside.Id, word.Id, "producer/beside") }.Order().ToList(),
            (await RowsAsync()).Select(row => (row.Session, row.Word, row.Key)).Order().ToList());
    }

    /// <summary>
    /// A list that is not a JSON array of words, on either side, settles nothing and never fails the statement that wrote it: a take must
    /// still land. (A list that is not JSON at all never reaches the trigger: the words counter before it, <c>words_revision_kept</c>, refuses it.)
    /// </summary>
    [Fact]
    public async Task A_list_that_is_not_an_array_of_words_settles_nothing_and_never_fails_the_statement()
    {
        var record = await EndedAsync();
        await ExecuteAsync("UPDATE sessions SET said = '[]' WHERE id = $id", ("$id", record.Id));
        await ExecuteAsync("UPDATE sessions SET said = '{\"a\":1}' WHERE id = $id", ("$id", record.Id));
        await ExecuteAsync("UPDATE sessions SET said = '[1, \"two\", {\"no\":\"id\"}]' WHERE id = $id", ("$id", record.Id));
        await ExecuteAsync("UPDATE sessions SET said = NULL WHERE id = $id", ("$id", record.Id));

        Assert.Empty(await RowsAsync());
    }

    /// <summary>
    /// An older connector composes its own stores and runs the statement it always ran over the migrated database: <c>TakeSaidAsync</c> as it
    /// was before the table, a read then a whole-list rewrite. The trigger settles the word whoever writes it.
    /// </summary>
    [Fact]
    public async Task An_older_connectors_own_take_statement_over_the_migrated_database_settles_its_word()
    {
        var record = await EndedAsync();
        var word = (await SayAsync(record.Id, "producer/older")).Word!;

        // The statement an older build ran (`SessionStore.TakeSaidAsync` before row p), word for word.
        await ExecuteAsync("UPDATE sessions SET said = $said, answer = NULL WHERE id = $id AND origin IS NULL", ("$said", DBNull.Value), ("$id", record.Id));

        Assert.Equal([(record.Id, word.Id, "producer/older")], (await RowsAsync()).Select(row => (row.Session, row.Word, row.Key)).ToList());
        Assert.Equal(record.Id, (await SayAsync(record.Id, "producer/older")).SettledOn);
    }

    // ---- the say door's answer ----

    /// <summary>
    /// A producer's retry after the take is answered <c>Settled</c>, its own terminal kind with no word and nothing kept; a retry from an
    /// older client, which sends no <c>settled</c>, is answered by today's door and keeps a second word under a new id.
    /// </summary>
    [Fact]
    public async Task A_retry_after_the_take_is_answered_settled_with_no_word_and_an_older_clients_keeps_a_second()
    {
        var record = await EndedAsync();
        var first = await SayAsync(record.Id, "producer/retry");
        await _sessions.TakeSaidAsync(record.Id, [first.Word!.Id]);

        var retry = await SayAsync(record.Id, "producer/retry");

        Assert.Equal(SessionSayRefusal.None, retry.Refusal);
        Assert.Equal(record.Id, retry.SettledOn);
        Assert.Null(retry.Word);
        Assert.Null(retry.Session);
        Assert.False(retry.Again);
        Assert.Contains($"`{record.Id}`", retry.Message, StringComparison.Ordinal);
        Assert.Empty((await _sessions.FindAsync(record.Id))!.Said);

        var older = await SayAsync(record.Id, "producer/retry", settled: false);
        Assert.Null(older.SettledOn);
        Assert.NotEqual(first.Word.Id, older.Word!.Id);
        Assert.Single((await _sessions.FindAsync(record.Id))!.Said);
    }

    /// <summary>
    /// A word still waiting on the record named is answered <c>Again</c> with that word, as today, to a client that asks too: only a key that
    /// left a record of the line is settled, and a settlement beside a waiting word does not take its place.
    /// </summary>
    [Fact]
    public async Task A_word_still_waiting_on_the_record_named_is_answered_again_with_it_even_beside_an_older_settlement()
    {
        var record = await EndedAsync();
        var first = await SayAsync(record.Id, "producer/waits");
        await _sessions.TakeSaidAsync(record.Id, [first.Word!.Id]);
        var second = await SayAsync(record.Id, "producer/waits", settled: false);

        var retry = await SayAsync(record.Id, "producer/waits");

        Assert.Null(retry.SettledOn);
        Assert.True(retry.Again);
        Assert.Equal(second.Word!.Id, retry.Word!.Id);
    }

    /// <summary>
    /// The settlement is asked before any refusal but the record's absence: a retry to a record that runs again is answered as kept, never
    /// refused <c>running</c>, and one to a record that stood down never <c>Never</c>; an older client's retry to either is refused as today.
    /// </summary>
    [Fact]
    public async Task The_settlement_is_asked_before_the_say_doors_running_and_never_refusals()
    {
        var record = await EndedAsync();
        var first = await SayAsync(record.Id, "producer/running");
        await _sessions.TakeSaidAsync(record.Id, [first.Word!.Id]);
        await _sessions.SetStateAsync(record.Id, SessionState.Working, null, null, null, Now);

        Assert.Equal(record.Id, (await SayAsync(record.Id, "producer/running")).SettledOn);
        Assert.Equal(SessionSayRefusal.Running, (await SayAsync(record.Id, "producer/running", settled: false)).Refusal);

        await _sessions.SetStateAsync(record.Id, SessionState.StoodDown, null, null, null, Now);
        Assert.Equal(record.Id, (await SayAsync(record.Id, "producer/running")).SettledOn);
        Assert.Equal(SessionSayRefusal.StoodDown, (await SayAsync(record.Id, "producer/running", settled: false)).Refusal);

        Assert.Equal(SessionSayRefusal.NotFound, (await SayAsync("nosuch01", "producer/running")).Refusal);
    }

    /// <summary>
    /// Equal keys on unrelated records are two words: a key settled on one record's line says nothing of another's, which keeps its own word.
    /// </summary>
    [Fact]
    public async Task Equal_keys_on_unrelated_records_are_two_words()
    {
        var a = await EndedAsync();
        var b = await EndedAsync();
        var first = await SayAsync(a.Id, "producer/shared");
        await _sessions.TakeSaidAsync(a.Id, [first.Word!.Id]);

        var other = await SayAsync(b.Id, "producer/shared");

        Assert.Null(other.SettledOn);
        Assert.NotNull(other.Word);
        Assert.Single((await _sessions.FindAsync(b.Id))!.Said);
        Assert.Equal(a.Id, (await SayAsync(a.Id, "producer/shared")).SettledOn);
    }

    /// <summary>
    /// A record with no stored <c>entry</c>, which the rule's key derives and never writes back, settles its word and answers it: the settlement
    /// is by the record's own id, the walk giving the line, so no <c>entry</c> index is needed.
    /// </summary>
    [Fact]
    public async Task A_record_with_no_stored_entry_settles_and_answers_its_word()
    {
        var record = await EndedAsync(ownEntry: false);
        Assert.Null((await _sessions.FindAsync(record.Id))!.Entry);
        var first = await SayAsync(record.Id, "producer/entryless");
        await _sessions.TakeSaidAsync(record.Id, [first.Word!.Id]);

        Assert.Equal(record.Id, (await SayAsync(record.Id, "producer/entryless")).SettledOn);

        // Its successor, whatever its entry is, answers it too, over the walk back along `from`.
        var successor = await EndedAsync(from: record.Id);
        Assert.Equal(record.Id, (await SayAsync(successor.Id, "producer/entryless")).SettledOn);
    }

    /// <summary>
    /// A settlement six days and twenty-three hours old answers its retry while more than 50,000 others are written that week; one seven days
    /// and a minute old is trimmed by the next statement that leaves a word, and is no answer. No count cap evicts a settlement inside the week.
    /// </summary>
    [Fact]
    public async Task A_settlement_six_days_and_twenty_three_hours_old_answers_among_more_than_fifty_thousand_and_one_a_minute_past_seven_days_is_trimmed()
    {
        var recent = await EndedAsync();
        var old = await EndedAsync();
        var at = Now.ToUnixTimeSeconds();
        await ExecuteAsync(
            "INSERT INTO said_settled (session, word, key, at) VALUES ($recent, 'w0000001', 'producer/recent', $recentAt), ($old, 'w0000002', 'producer/old', $oldAt)",
            ("$recent", recent.Id), ("$recentAt", at - (6 * 86400 + 23 * 3600)), ("$old", old.Id), ("$oldAt", at - (7 * 86400 + 60)));
        // More than 50,000 others, written that week by the statement that settles them, each a word of its own.
        await ExecuteAsync(
            """
            WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 50500)
            INSERT INTO said_settled (session, word, key, at)
            SELECT 'other' || (i % 40), printf('%08x', i), 'bulk:' || i, $at - (i % 600000) FROM n
            """,
            ("$at", at));
        Assert.True(await ScalarAsync("SELECT COUNT(*) FROM said_settled") > 50_000);

        Assert.Equal(recent.Id, (await SayAsync(recent.Id, "producer/recent")).SettledOn);
        // Past the horizon the reader never answers it, trimmed or not: a retry then is a new word, said on its line.
        var pastBefore = await SayAsync(old.Id, "producer/old");
        Assert.Null(pastBefore.SettledOn);
        Assert.NotNull(pastBefore.Word);
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM said_settled WHERE key = 'producer/old'"));

        // A statement that leaves a word trims it, by time alone, in the same statement.
        var another = await EndedAsync();
        var word = (await SayAsync(another.Id, "producer/another")).Word!;
        await _sessions.TakeSaidAsync(another.Id, [word.Id]);

        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) FROM said_settled WHERE key = 'producer/old'"));
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) FROM said_settled WHERE key = 'producer/recent'"));
        Assert.True(await ScalarAsync("SELECT COUNT(*) FROM said_settled WHERE key LIKE 'bulk:%'") >= 50_000);
        Assert.Equal(recent.Id, (await SayAsync(recent.Id, "producer/recent")).SettledOn);
    }

    /// <summary>
    /// A retry past seven days is a new word, kept as any word is under its key: the settlement's own age, however it came to be read, is
    /// the horizon, and nothing is hidden.
    /// </summary>
    [Fact]
    public async Task A_retry_past_seven_days_is_kept_as_a_new_word()
    {
        var record = await EndedAsync();
        var first = await SayAsync(record.Id, "producer/late");
        await _sessions.TakeSaidAsync(record.Id, [first.Word!.Id]);
        await ExecuteAsync("UPDATE said_settled SET at = at - $age", ("$age", 8 * 86400));

        var late = await SayAsync(record.Id, "producer/late");

        Assert.Null(late.SettledOn);
        Assert.NotEqual(first.Word.Id, late.Word!.Id);
    }

    // ---- a retry paused after its check, and the line's records ----

    /// <summary>
    /// A retry that arrives while another dispatcher holds the write lock and takes the word, then runs: it reads inside its own exclusive
    /// step, after the take, so the word is settled and the retry is answered so; one word, never a second. The keep that would have been a
    /// second word is what the check and the keep sharing one step prevents.
    /// </summary>
    [Fact]
    public async Task A_retry_paused_while_another_dispatcher_takes_the_word_keeps_one_word()
    {
        var record = await EndedAsync();
        var first = await SayAsync(record.Id, "producer/paused");
        var (otherConnection, other, otherLedger) = await OpenAsync();
        await using var owned = otherConnection;
        Assert.NotNull(otherLedger);

        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var taker = Task.Run(() => other.ExclusiveAsync(async inside =>
        {
            holding.SetResult();
            await release.Task;
            await other.TakeSaidAsync(record.Id, [first.Word!.Id], inside);
            return true;
        }));
        await holding.Task;

        var retry = Task.Run(() => SayAsync(record.Id, "producer/paused"));
        await Task.Delay(400);
        Assert.False(retry.IsCompleted, "the retry waits for the write lock the other dispatcher holds");

        release.SetResult();
        await taker;
        var answered = await retry;

        Assert.Equal(record.Id, answered.SettledOn);
        Assert.Empty((await _sessions.FindAsync(record.Id))!.Said);
    }

    /// <summary>
    /// Codex's case: a producer's say kept with its answer lost, the word taken on its record, the record continued by a successor the test's
    /// own open statement makes, and the retry forwarded to the successor answered as kept, one word; the same retry to the old record once
    /// it runs again answered as kept, never refused <c>running</c>.
    /// </summary>
    [Fact]
    public async Task A_retry_forwarded_to_a_successor_is_answered_as_kept_and_so_is_one_to_the_old_record_once_it_runs_again()
    {
        var old = await EndedAsync();
        var first = await SayAsync(old.Id, "producer/forwarded");
        await _sessions.TakeSaidAsync(old.Id, [first.Word!.Id]);
        var successor = await EndedAsync(from: old.Id);

        var forwarded = await SayAsync(successor.Id, "producer/forwarded");

        Assert.Equal(old.Id, forwarded.SettledOn);
        Assert.Empty((await _sessions.FindAsync(successor.Id))!.Said);

        await _sessions.SetStateAsync(old.Id, SessionState.Working, null, null, null, Now);
        var again = await SayAsync(old.Id, "producer/forwarded");
        Assert.Equal(old.Id, again.SettledOn);
        Assert.NotEqual(SessionSayRefusal.Running, again.Refusal);

        // Through two successors: the walk back along `from` reaches the record the word left from the third part.
        var third = await EndedAsync(from: successor.Id);
        Assert.Equal(old.Id, (await SayAsync(third.Id, "producer/forwarded")).SettledOn);
    }

    /// <summary>
    /// A key whose word waits on a record the line has left is answered by the door as it stands on the record named, as today: the shell's
    /// pump forwards a word whose say it lost to the successor, which keeps it as a new word; nothing here is settled, since the word never
    /// left <c>said</c>. From row d-ii the walk reads forward to the head as well as back, so a settlement on a later part answers a retry
    /// naming an earlier one.
    /// </summary>
    [Fact]
    public async Task A_key_whose_word_waits_on_a_record_the_line_has_left_is_kept_at_the_successor_as_today()
    {
        var old = await EndedAsync();
        var waiting = await SayAsync(old.Id, "producer/left");
        var successor = await EndedAsync(from: old.Id);

        var forwarded = await SayAsync(successor.Id, "producer/left");

        Assert.Null(forwarded.SettledOn);
        Assert.NotEqual(waiting.Word!.Id, forwarded.Word!.Id);
        Assert.Single((await _sessions.FindAsync(successor.Id))!.Said);
        Assert.Single((await _sessions.FindAsync(old.Id))!.Said);

        // 🔴 Row d-ii: a settlement on a later part is read from an earlier one, by the head walk's records forward.
        var later = await EndedAsync(from: successor.Id);
        var onLater = await SayAsync(later.Id, "producer/own");
        await _sessions.TakeSaidAsync(later.Id, [onLater.Word!.Id]);
        Assert.Equal(later.Id, (await SayAsync(old.Id, "producer/own")).SettledOn);
        Assert.Equal(later.Id, (await SayAsync(later.Id, "producer/own")).SettledOn);
    }

    // ---- row d-ii: the head judged inside the say door's step, and the settlement read forward to it ----

    /// <summary>
    /// 🔴 Row d-ii, open before say, across two connections: an open's step that has written the successor and not yet committed holds the
    /// write lock, so a say to the record it leaves waits; once the open commits, the say reads its successor inside its own step and is
    /// refused <c>Continued</c> naming it, nothing kept on the predecessor or the successor. Two hosts' steps, never an in-process lock.
    /// </summary>
    [Fact]
    public async Task An_open_that_commits_before_the_say_is_read_by_it_and_the_say_is_refused_continued_naming_the_successor()
    {
        var a = await EndedAsync();
        var (otherConnection, _, otherLedger) = await OpenAsync();
        await using var owned = otherConnection;
        var (held, release) = await HoldAsync(otherConnection, "AFTER INSERT ON main.sessions");
        var open = Task.Run(() => otherLedger.OpenChatAsync("Owner", "stub", Now, from: a.Id, opens: "race/open-first"));
        await held.Task.WaitAsync(TimeSpan.FromSeconds(20));

        var say = Task.Run(() => SayAsync(a.Id, "producer/race"));
        await Task.Delay(400);
        Assert.False(say.IsCompleted, "the say waits for the open's step");
        release.SetResult();
        var opened = await open;
        var answered = await say;

        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        Assert.Equal((SessionSayRefusal.Continued, opened.Session!.Id), (answered.Refusal, answered.Head));
        Assert.Empty((await _sessions.FindAsync(a.Id))!.Said);
        Assert.Empty((await _sessions.FindAsync(opened.Session.Id))!.Said);
    }

    /// <summary>
    /// 🔴 Row d-ii, say before open, across two connections: a say's step that has kept its word and not yet committed holds the write lock, so
    /// a keyed open from that record waits; once the say commits, the open judges the record its line's head, as it still is, and opens, and
    /// the word is on the old record where the open's own connection finds it (row d-ii's later part moves it). The word's retry to the old
    /// record is then refused <c>Continued</c>, the open having left it.
    /// </summary>
    [Fact]
    public async Task A_say_that_commits_before_the_open_keeps_its_word_on_the_old_record_where_the_later_open_finds_it()
    {
        var a = await EndedAsync();
        var (otherConnection, otherSessions, otherLedger) = await OpenAsync();
        await using var owned = otherConnection;
        var (held, release) = await HoldAsync(_connection, "AFTER UPDATE OF said ON main.sessions");
        var say = Task.Run(() => SayAsync(a.Id, "producer/race"));
        await held.Task.WaitAsync(TimeSpan.FromSeconds(20));

        var open = Task.Run(() => otherLedger.OpenChatAsync("Owner", "stub", Now, from: a.Id, opens: "race/say-first"));
        await Task.Delay(400);
        Assert.False(open.IsCompleted, "the open waits for the say's step");
        release.SetResult();
        var kept = await say;
        var opened = await open;

        Assert.Equal(SessionSayRefusal.None, kept.Refusal);
        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        Assert.Equal([kept.Word!.Id], (await otherSessions.FindAsync(a.Id))!.Said.Select(word => word.Id));
        Assert.Empty((await otherSessions.FindAsync(opened.Session!.Id))!.Said);
        Assert.Equal((SessionSayRefusal.Continued, opened.Session.Id), ((await SayAsync(a.Id, "producer/race")).Refusal, (await SayAsync(a.Id, "producer/race")).Head));
    }

    /// <summary>
    /// 🔴 Row d-ii, *O8-F1* the first way: an older build's unkeyed successor, whose open moved nothing (the test's own open statement), leaves K
    /// waiting on A. K's retry naming A is refused <c>Continued</c> naming B, never settled; forwarded to B it is kept there as a new word,
    /// never settled, and once: forwarded again it is answered as kept with that word. A's copy stays where it waits, which never goes on.
    /// </summary>
    [Fact]
    public async Task An_older_unkeyed_successor_leaves_K_on_A_whose_retry_is_refused_continued_and_kept_once_at_the_head_never_settled()
    {
        var a = await EndedAsync();
        var first = await SayAsync(a.Id, "producer/older-open");
        var b = await EndedAsync(from: a.Id);

        var retry = await SayAsync(a.Id, "producer/older-open");
        Assert.Equal((SessionSayRefusal.Continued, b.Id), (retry.Refusal, retry.Head));
        Assert.Null(retry.SettledOn);

        var forwarded = await SayAsync(b.Id, "producer/older-open");
        Assert.Equal(SessionSayRefusal.None, forwarded.Refusal);
        Assert.Null(forwarded.SettledOn);
        Assert.NotEqual(first.Word!.Id, forwarded.Word!.Id);

        var again = await SayAsync(b.Id, "producer/older-open");
        Assert.True(again.Again);
        Assert.Equal(forwarded.Word.Id, again.Word!.Id);
        Assert.Single((await _sessions.FindAsync(b.Id))!.Said);
        Assert.Equal([first.Word.Id], (await _sessions.FindAsync(a.Id))!.Said.Select(word => word.Id));
        Assert.Empty(await RowsAsync());
    }

    /// <summary>
    /// 🔴 Row d-ii, *O8-F1* the second way: K said at the head B, after its say to A was refused <c>Continued</c>, waits on B. K's retry naming
    /// A is answered <c>Settled</c>, naming B where the word waits to go, nothing kept on either; an older client's retry, which asks no
    /// settlement, is answered by the door as it stands on A: refused <c>Continued</c>.
    /// </summary>
    [Fact]
    public async Task K_waiting_on_the_head_answers_a_retry_naming_the_record_left_settled_with_nothing_kept()
    {
        var a = await EndedAsync();
        var b = await EndedAsync(from: a.Id);
        Assert.Equal(SessionSayRefusal.Continued, (await SayAsync(a.Id, "producer/at-head")).Refusal);
        var kept = await SayAsync(b.Id, "producer/at-head");
        Assert.Equal(SessionSayRefusal.None, kept.Refusal);

        var retry = await SayAsync(a.Id, "producer/at-head");

        Assert.Equal(SessionSayRefusal.None, retry.Refusal);
        Assert.Equal(b.Id, retry.SettledOn);
        Assert.Null(retry.Word);
        Assert.Null(retry.Session);
        Assert.Contains($"`{b.Id}`", retry.Message, StringComparison.Ordinal);
        Assert.Empty((await _sessions.FindAsync(a.Id))!.Said);
        Assert.Equal([kept.Word!.Id], (await _sessions.FindAsync(b.Id))!.Said.Select(word => word.Id));
        Assert.Equal(SessionSayRefusal.Continued, (await SayAsync(a.Id, "producer/at-head", settled: false)).Refusal);
    }

    /// <summary>
    /// 🔴 Row d-ii, *C5-1.2*, *C5-3.2*, *C6-1.3*: K kept on A with its answer lost and taken there, then B and C opened after it. A retry naming
    /// A, the record the line left, is answered <c>Settled</c> before it is refused <c>Continued</c>, and one naming C, the head, too: no second
    /// word on any part, and the same where A has no stored <c>entry</c>. A word taken on C answers a retry naming A, by the walk forward.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task K_taken_on_A_before_two_successors_answers_retries_naming_A_or_C_settled_with_no_second_word(bool entry)
    {
        var a = await EndedAsync(ownEntry: entry);
        Assert.Equal(entry, (await _sessions.FindAsync(a.Id))!.Entry is not null);
        var first = await SayAsync(a.Id, "producer/lost");
        await _sessions.TakeSaidAsync(a.Id, [first.Word!.Id]);
        var b = await EndedAsync(from: a.Id);
        var c = await EndedAsync(from: b.Id);

        Assert.Equal(a.Id, (await SayAsync(a.Id, "producer/lost")).SettledOn);
        Assert.Equal(a.Id, (await SayAsync(c.Id, "producer/lost")).SettledOn);
        Assert.Equal(a.Id, (await SayAsync(b.Id, "producer/lost")).SettledOn);
        foreach (var part in new[] { a, b, c }) Assert.Empty((await _sessions.FindAsync(part.Id))!.Said);

        var atHead = await SayAsync(c.Id, "producer/at-c");
        await _sessions.TakeSaidAsync(c.Id, [atHead.Word!.Id]);
        Assert.Equal(c.Id, (await SayAsync(a.Id, "producer/at-c")).SettledOn);
        foreach (var part in new[] { a, b, c }) Assert.Empty((await _sessions.FindAsync(part.Id))!.Said);
    }

    /// <summary>
    /// 🔴 Row d-ii: a keyless word settles by its id (<c>id:&lt;word&gt;</c>) and is answered over the whole line, back from the head to the
    /// part it left and forward from an earlier part to the head; a re-say at the head after <c>Continued</c> is answered as kept.
    /// </summary>
    [Fact]
    public async Task A_keyless_word_is_settled_by_its_id_over_the_line_and_a_re_say_at_the_head_after_continued_is_answered_as_kept()
    {
        var a = await EndedAsync();
        var keyless = (await _ledger.SayAsync(a.Id, "no key", null, Now)).Word!;
        await _sessions.TakeSaidAsync(a.Id, [keyless.Id]);
        var b = await EndedAsync(from: a.Id);
        var c = await EndedAsync(from: b.Id);

        Assert.Equal(a.Id, (await SayAsync(c.Id, $"id:{keyless.Id}")).SettledOn);

        var onHead = (await _ledger.SayAsync(c.Id, "no key either", null, Now)).Word!;
        await _sessions.TakeSaidAsync(c.Id, [onHead.Id]);
        Assert.Equal(c.Id, (await SayAsync(a.Id, $"id:{onHead.Id}")).SettledOn);

        // Codex's case at the head: K refused `Continued` at B, said again at the head C and kept, then retried at C: answered as kept, once.
        Assert.Equal((SessionSayRefusal.Continued, c.Id), ((await SayAsync(b.Id, "producer/re-say")).Refusal, (await SayAsync(b.Id, "producer/re-say")).Head));
        var reSaid = await SayAsync(c.Id, "producer/re-say");
        Assert.Equal(SessionSayRefusal.None, reSaid.Refusal);
        var retried = await SayAsync(c.Id, "producer/re-say");
        Assert.True(retried.Again);
        Assert.Equal(reSaid.Word!.Id, retried.Word!.Id);
        Assert.Single((await _sessions.FindAsync(c.Id))!.Said);
    }

    /// <summary>
    /// 🔴 Row d-ii, the walk's bound on the settlement: a retry whose walk back along <c>from</c> would pass <see cref="Lines.Bound"/> records is
    /// refused <c>Unplaced</c> with its sentence, nothing kept, rather than read over a line cut short, which could keep a taken word twice;
    /// a line back exactly to the bound is read whole and answers the settlement at its far end.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_settlement_walk_past_the_bound_is_refused_with_its_sentence_and_one_at_it_reads_the_far_end(bool past)
    {
        var parts = new List<Session> { await EndedAsync() };
        var first = await SayAsync(parts[0].Id, "producer/far");
        await _sessions.TakeSaidAsync(parts[0].Id, [first.Word!.Id]);
        // The links in one step, as a batch of opens would commit them, so the file's syncs stay few.
        await _sessions.ExclusiveAsync(async inside =>
        {
            for (var link = 0; link < Lines.Bound + (past ? 1 : 0); link++)
            {
                var next = await _sessions.CreateAsync(
                    quest: null, "Owner", "stub", Now, kind: SessionKind.Chat, from: parts[^1].Id, fromWhy: "limit", fromBy: "daoris", ownEntry: true, ct: inside);
                parts.Add((await _sessions.SetStateAsync(next.Id, SessionState.Completed, null, null, null, Now, inside))!);
            }

            return true;
        });

        var retry = await SayAsync(parts[^1].Id, "producer/far");

        if (!past)
        {
            Assert.Equal(parts[0].Id, retry.SettledOn);
            return;
        }

        Assert.Equal(SessionSayRefusal.Unplaced, retry.Refusal);
        Assert.Null(retry.SettledOn);
        Assert.Contains(Lines.Bound.ToString(CultureInfo.InvariantCulture), retry.Message, StringComparison.Ordinal);
        Assert.Empty((await _sessions.FindAsync(parts[^1].Id))!.Said);
        // An older client asks no settlement, so the door reads no walk back and keeps its word at the head, as today.
        Assert.Equal(SessionSayRefusal.None, (await SayAsync(parts[^1].Id, "producer/far", settled: false)).Refusal);
    }

    /// <summary>
    /// A word left on two records, by statements the test plants as an open would write them, settles a row on each, and its retry to either
    /// is answered as kept. Equal word ids on unrelated records, the literal <c>answer</c> among them, each settle and answer their own.
    /// </summary>
    [Fact]
    public async Task A_word_left_on_two_records_settles_a_row_on_each_and_equal_ids_on_unrelated_records_each_answer_their_own()
    {
        var a = await EndedAsync();
        var b = await EndedAsync();
        const string Planted = """[{"id":"d00dfeed","text":"moved words","at":"2026-10-26T01:02:03.0000000+00:00","files":[],"key":"producer/moved","from":"daoris"}]""";
        await ExecuteAsync("UPDATE sessions SET said = $said WHERE id = $id", ("$said", Planted), ("$id", a.Id));
        // The open's own statements: the word leaves the old record for the new one, which holds it, and is taken there.
        await ExecuteAsync("UPDATE sessions SET said = NULL WHERE id = $id", ("$id", a.Id));
        await ExecuteAsync("UPDATE sessions SET said = $said WHERE id = $id", ("$said", Planted), ("$id", b.Id));
        await ExecuteAsync("UPDATE sessions SET said = NULL WHERE id = $id", ("$id", b.Id));

        Assert.Equal(
            new[] { (a.Id, "d00dfeed", "producer/moved"), (b.Id, "d00dfeed", "producer/moved") }.Order().ToList(),
            (await RowsAsync()).Select(row => (row.Session, row.Word, row.Key)).Order().ToList());
        Assert.Equal(a.Id, (await SayAsync(a.Id, "producer/moved")).SettledOn);
        Assert.Equal(b.Id, (await SayAsync(b.Id, "producer/moved")).SettledOn);

        // The literal `answer` on every record that kept one before `said`: each its own row, never replacing another's.
        var c = await EndedAsync();
        var d = await EndedAsync();
        foreach (var record in new[] { c, d })
        {
            await ExecuteAsync("UPDATE sessions SET answer = $answer, said = NULL WHERE id = $id", ("$answer", $"old {record.Id}"), ("$id", record.Id));
            var word = (await SayAsync(record.Id, $"producer/beside-{record.Id}")).Word!;
            await _sessions.TakeSaidAsync(record.Id, ["answer", word.Id]);
        }

        var answers = (await RowsAsync()).Where(row => row.Word == "answer").Select(row => row.Session).Order().ToList();
        Assert.Equal(new[] { c.Id, d.Id }.Order().ToList(), answers);
        Assert.Equal(c.Id, (await SaidKeys.Settled(_sessions, [c.Id], "id:answer", Now))!.Record);
        Assert.Equal(d.Id, (await SaidKeys.Settled(_sessions, [d.Id], "id:answer", Now))!.Record);
        Assert.Null(await SaidKeys.Settled(_sessions, [a.Id], "id:answer", Now));
    }

    /// <summary>
    /// The walk back along <c>from</c> ends on a record it walked already: a cycle planted by hand is read, not looped. The word is kept and
    /// taken before the cycle is planted, since from row d-ii a line that loops keeps no word (<see cref="SessionSayRefusal.Unplaced"/>).
    /// </summary>
    [Fact]
    public async Task A_cycle_planted_by_hand_ends_the_walk_and_a_missing_predecessor_ends_it_too()
    {
        var a = await EndedAsync();
        var b = await EndedAsync(from: a.Id);
        var first = await SayAsync(b.Id, "producer/cycle");
        await _sessions.TakeSaidAsync(b.Id, [first.Word!.Id]);
        await ExecuteAsync("UPDATE sessions SET from_session = $b WHERE id = $a", ("$a", a.Id), ("$b", b.Id));

        Assert.Equal(b.Id, (await SayAsync(a.Id, "producer/cycle")).SettledOn);
        Assert.Equal(b.Id, (await SayAsync(b.Id, "producer/cycle")).SettledOn);

        var orphan = await EndedAsync(from: "gone0001");
        Assert.Null((await SayAsync(orphan.Id, "producer/cycle")).SettledOn);
    }

    // ---- the desks ----

    /// <summary>
    /// A helper's hand retried after its take keeps one word: the desk asks the settlement before its refusals, over the asker's line, and
    /// answers it <c>Settled</c> to a hand that asks; an older client's hand keeps a second word as today. A hand whose asker went on, with
    /// its word still waiting on the record the line left, is kept once on the newest asker.
    /// </summary>
    [Fact]
    public async Task A_helpers_hand_retried_after_its_take_keeps_one_word_and_one_whose_asker_went_on_is_kept_once_on_the_newest()
    {
        var desk = new HelperDesk(_sessions);
        var asker = await EndedAsync();
        var helper = await HelperOfAsync(asker);

        var first = await desk.HandAsync(helper.Id, "the answer", Now);
        Assert.Equal(HelperRefusal.None, first.Refusal);
        await _sessions.TakeSaidAsync(asker.Id, [first.Word!.Id]);

        var retry = await desk.HandAsync(helper.Id, "the answer", Now, settled: true);
        Assert.Equal(HelperRefusal.None, retry.Refusal);
        Assert.Equal(asker.Id, retry.SettledOn);
        Assert.Null(retry.Word);
        Assert.Null(retry.Session);
        Assert.Empty((await _sessions.FindAsync(asker.Id))!.Said);

        // Retried after the asker went on: its predecessor's settlement is read from the newest record.
        var successor = await EndedAsync(from: asker.Id);
        Assert.Equal(asker.Id, (await desk.HandAsync(helper.Id, "the answer", Now, settled: true)).SettledOn);
        Assert.Empty((await _sessions.FindAsync(successor.Id))!.Said);

        var older = await desk.HandAsync(helper.Id, "the answer", Now);
        Assert.Null(older.SettledOn);
        Assert.NotNull(older.Word);

        // A word waiting on the record the line left is not settled: the hand goes to the newest asker once, as today.
        var left = await EndedAsync();
        var leftHelper = await HelperOfAsync(left);
        var waiting = await desk.HandAsync(leftHelper.Id, "the other answer", Now, settled: true);
        Assert.NotNull(waiting.Word);
        var newest = await EndedAsync(from: left.Id);
        var kept = await desk.HandAsync(leftHelper.Id, "the other answer", Now, settled: true);
        Assert.Null(kept.SettledOn);
        Assert.Equal(newest.Id, kept.Session!.Id);
        Assert.Single((await _sessions.FindAsync(newest.Id))!.Said);
    }

    /// <summary>A partner's hand retried after its take keeps one word, on the lead's line, as a helper's does.</summary>
    [Fact]
    public async Task A_partners_hand_retried_after_its_take_keeps_one_word()
    {
        var desk = new PartnerDesk(_sessions);
        var lead = await EndedAsync();
        var partner = await _sessions.CreateAsync(
            quest: null, "Owner", "stub", Now, kind: SessionKind.Chat, serves: lead.Id, piece: "piece1", ownEntry: true, actor: "partner");

        var first = await desk.HandAsync(partner.Id, "the piece", Now);
        Assert.Equal(HelperRefusal.None, first.Refusal);
        await _sessions.TakeSaidAsync(lead.Id, [first.Word!.Id]);

        var retry = await desk.HandAsync(partner.Id, "the piece", Now, settled: true);
        Assert.Equal(lead.Id, retry.SettledOn);
        Assert.Null(retry.Word);
        Assert.Empty((await _sessions.FindAsync(lead.Id))!.Said);

        var older = await desk.HandAsync(partner.Id, "the piece", Now);
        Assert.Null(older.SettledOn);
        Assert.NotNull(older.Word);
    }

    /// <summary>An ended driven part of a quest, as a run here leaves one.</summary>
    private async Task<Session> DrivenAsync(string quest)
    {
        var made = await _sessions.CreateAsync(quest, "Owner", "stub", Now, kind: SessionKind.Driven, ownEntry: true);
        return (await _sessions.SetStateAsync(made.Id, SessionState.Completed, null, null, null, Now))!;
    }

    /// <summary>
    /// 🔴 Row d-ii (the design §3.4 rule 2): the helper desk reads the line's head as the say door does. An asker that is a driven part with no
    /// successor whose quest ran again here is handed the answer at that run, never on the part the line left; the hand retried after its
    /// take there is answered settled, over the asker's line back and the head walk's records forward, with no second word.
    /// </summary>
    [Fact]
    public async Task The_helper_desk_hands_to_the_head_the_say_door_reads_and_its_retry_after_the_take_there_is_settled()
    {
        var desk = new HelperDesk(_sessions);
        var asker = await DrivenAsync("q-helped");
        var helper = await HelperOfAsync(asker);
        var rerun = await DrivenAsync("q-helped");

        var handed = await desk.HandAsync(helper.Id, "the answer", Now, settled: true);

        Assert.Equal(HelperRefusal.None, handed.Refusal);
        Assert.Equal(rerun.Id, handed.Session!.Id);
        Assert.Empty((await _sessions.FindAsync(asker.Id))!.Said);
        Assert.Single((await _sessions.FindAsync(rerun.Id))!.Said);

        await _sessions.TakeSaidAsync(rerun.Id, [handed.Word!.Id]);
        var retry = await desk.HandAsync(helper.Id, "the answer", Now, settled: true);
        Assert.Equal(rerun.Id, retry.SettledOn);
        Assert.Empty((await _sessions.FindAsync(rerun.Id))!.Said);
    }

    /// <summary>
    /// 🔴 Row d-ii: the partner desk reads the line's head too, a lead's quest run again here taking its piece; a lead's line planted as a cycle
    /// has no head, so its piece's hand is refused with its sentence and nothing is kept on any part, never handed to a part the walk passed.
    /// </summary>
    [Fact]
    public async Task The_partner_desk_hands_to_the_head_and_refuses_a_line_that_loops()
    {
        var desk = new PartnerDesk(_sessions);
        var lead = await DrivenAsync("q-led");
        var partner = await _sessions.CreateAsync(
            quest: null, "Owner", "stub", Now, kind: SessionKind.Chat, serves: lead.Id, piece: "piece2", ownEntry: true, actor: "partner");
        var rerun = await DrivenAsync("q-led");

        var handed = await desk.HandAsync(partner.Id, "the piece", Now, settled: true);
        Assert.Equal(HelperRefusal.None, handed.Refusal);
        Assert.Equal(rerun.Id, handed.Session!.Id);
        Assert.Empty((await _sessions.FindAsync(lead.Id))!.Said);

        var x = await EndedAsync();
        var y = await EndedAsync(from: x.Id);
        await ExecuteAsync("UPDATE sessions SET from_session = $y WHERE id = $x", ("$x", x.Id), ("$y", y.Id));
        var looped = await _sessions.CreateAsync(
            quest: null, "Owner", "stub", Now, kind: SessionKind.Chat, serves: x.Id, piece: "piece3", ownEntry: true, actor: "partner");

        var refused = await desk.HandAsync(looped.Id, "the piece", Now, settled: true);

        Assert.Equal(HelperRefusal.CannotTake, refused.Refusal);
        Assert.Contains("loops", refused.Message, StringComparison.Ordinal);
        Assert.Empty((await _sessions.FindAsync(x.Id))!.Said);
        Assert.Empty((await _sessions.FindAsync(y.Id))!.Said);
    }

    /// <summary>
    /// The supervisor's door reads no settlement: its retry compares a decision, a question, a choice and a text, which a settlement cannot
    /// carry, so it answers its key only while its word waits, as today, whatever the request asks.
    /// </summary>
    [Fact]
    public async Task The_supervisors_door_reads_no_settlement()
    {
        var record = await EndedAsync();
        await ExecuteAsync(
            "INSERT INTO said_settled (session, word, key, at) VALUES ($id, 'ab12cd34', 'release/1', $at)",
            ("$id", record.Id), ("$at", Now.ToUnixTimeSeconds()));

        var outcome = await _ledger.SayAsync(
            record.Id, null, null, Now, key: "release/1", from: SaidFrom.Supervisor, decision: "d1", question: "Which?", choice: "A", settled: true);

        Assert.Null(outcome.SettledOn);
        Assert.NotEqual(SessionSayRefusal.None, outcome.Refusal);
    }

    private async Task<Session> HelperOfAsync(Session asker) => await _sessions.CreateAsync(
        quest: null, "Owner", "stub", Now, kind: SessionKind.Chat, serves: asker.Id, ownEntry: true, actor: "helper");
}
