using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

public sealed class SessionStoreTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-19T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _sessions = await SessionStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private Task<Session> Create(string quest = "abc123", string repository = "Owner", string adapter = "stub") =>
        _sessions.CreateAsync(quest, repository, adapter, Now);

    /// <summary>
    /// A session RECORD lives in the service, beside the quest it serves; the PROCESS never does.
    /// The record is what the platform renders and what survives a driver restart.
    /// </summary>
    [Fact]
    public async Task A_created_session_is_queued_and_names_its_work()
    {
        var session = await Create();

        Assert.Equal(SessionState.Queued, session.State);
        Assert.Equal("abc123", session.Quest);
        Assert.Equal("Owner", session.Repository);
        Assert.Equal("stub", session.Adapter);
        Assert.Null(session.Note);
    }

    /// <summary>Two attempts at the same quest are two sessions — a retry is its own record.</summary>
    [Fact]
    public async Task Two_sessions_for_one_quest_are_distinct()
    {
        var first = await Create();
        var second = await Create();

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, (await _sessions.ListAsync(includeClosed: true)).Count);
    }

    [Fact]
    public async Task Moving_state_records_the_note_and_the_evidence()
    {
        var session = await Create();

        var moved = await _sessions.SetStateAsync(
            session.Id, SessionState.Failed, note: "exit 2", evidence: "gates: red",
            transcript: null, Now.AddHours(1));

        Assert.Equal(SessionState.Failed, moved!.State);
        Assert.Equal("exit 2", moved.Note);
        Assert.Equal("gates: red", moved.Evidence);
        Assert.Equal(Now.AddHours(1), moved.Updated);
    }

    /// <summary>
    /// D104: a stop the sweep or a shutdown made, not the person, is kept on the record as interrupted —
    /// read back as written, and false for every move that did not say so.
    /// </summary>
    [Fact]
    public async Task An_interrupted_stop_is_kept_on_the_record()
    {
        var interrupted = await Create();
        var persons = await Create();

        await _sessions.SetStateAsync(interrupted.Id, SessionState.Working, null, null, null, Now);
        await _sessions.SetStateAsync(interrupted.Id, SessionState.Stopped, "the driver was stopped.", null, null, Now, interrupted: true);
        await _sessions.SetStateAsync(persons.Id, SessionState.Stopped, "the person stopped it.", null, null, Now);

        Assert.True((await _sessions.FindAsync(interrupted.Id))!.Interrupted);
        Assert.False((await _sessions.FindAsync(persons.Id))!.Interrupted);
    }

    /// <summary>
    /// TOOL4c (D125 §5.2): a failure an account's limit made is kept on the record as a limit — read back as
    /// written, and false for every failure that did not say so.
    /// </summary>
    [Fact]
    public async Task A_limit_is_kept_on_the_failed_record()
    {
        var limited = await Create();
        var crashed = await Create();

        await _sessions.SetStateAsync(limited.Id, SessionState.Working, null, null, null, Now);
        await _sessions.SetStateAsync(
            limited.Id, SessionState.Failed, "the ACP agent refused the call: You've hit your weekly limit", null, null, Now,
            limit: true);
        await _sessions.SetStateAsync(crashed.Id, SessionState.Failed, "exit 2", null, null, Now);

        Assert.True((await _sessions.FindAsync(limited.Id))!.Limit);
        Assert.False((await _sessions.FindAsync(crashed.Id))!.Limit);
        Assert.False((await _sessions.FindAsync(limited.Id))!.Interrupted);
    }

    /// <summary>A teammate's record keeps its limit when mirrored, on its first copy and on each later one.</summary>
    [Fact]
    public async Task A_mirrored_record_keeps_its_limit()
    {
        var fed = new Session(
            "alice-laptop/ab12cd34", "abc123", "Owner", "claude-code",
            SessionState.Working, null, null, null, Now, Now);

        await _sessions.MirrorAsync(fed);
        Assert.False((await _sessions.FindAsync(fed.Id))!.Limit);

        await _sessions.MirrorAsync(fed with { State = SessionState.Failed, Updated = Now.AddHours(1), Limit = true });
        Assert.True((await _sessions.FindAsync(fed.Id))!.Limit);

        await _sessions.MirrorAsync(new Session(
            "bob-desktop/ef56ab78", "abc123", "Owner", "claude-code",
            SessionState.Failed, null, null, null, Now, Now) { Limit = true });
        Assert.True((await _sessions.FindAsync("bob-desktop/ef56ab78"))!.Limit);
    }

    /// <summary>
    /// ANSWER1b, as MSG1a keeps it (D137 §2.4): a word kept with a note leaves the state where it is and is a new revision,
    /// so the note goes up with the next push; a word kept with no note writes no revision, since the words never travel.
    /// Words are kept in order, each whole, and `Answer` is them joined by a blank line. A teammate's mirrored record is
    /// never written to here. A move told to clear the words clears them, and any other move keeps them.
    /// </summary>
    [Fact]
    public async Task Words_are_kept_in_order_and_a_move_clears_them_only_when_told()
    {
        var parked = await Create();
        await _sessions.SetStateAsync(parked.Id, SessionState.AwaitingPerson, "which port?", null, null, Now);
        var before = (await _sessions.OwnChangedSinceAsync(0, Workspaces.Default)).Single().Revision;

        var answered = await _sessions.KeepSaidAsync(
            parked.Id, new SaidWord("w1", "8080.", Now.AddMinutes(1), []), note: "which port?\n\nAnswered: 8080.");

        Assert.Equal((SessionState.AwaitingPerson, "8080.", "which port?\n\nAnswered: 8080.", Now.AddMinutes(1)),
            (answered!.State, answered.Answer, answered.Note, answered.Updated));
        var after = (await _sessions.OwnChangedSinceAsync(before, Workspaces.Default)).Single().Revision;
        Assert.True(after > before);

        await _sessions.KeepSaidAsync(parked.Id, new SaidWord("w2", "9090.", Now.AddMinutes(2), ["notes.md"], Reopens: true));
        Assert.Empty(await _sessions.OwnChangedSinceAsync(after, Workspaces.Default));
        var both = (await _sessions.FindAsync(parked.Id))!;
        Assert.Equal(["w1", "w2"], both.Said.Select(word => word.Id));
        Assert.Equal((false, true), (both.Said[0].Reopens, both.Said[1].Reopens));
        Assert.Equal(["notes.md"], both.Said[1].Files);
        Assert.Equal(Now.AddMinutes(2), both.Said[1].At);
        Assert.Equal("8080.\n\n9090.", both.Answer);
        Assert.Equal(("which port?\n\nAnswered: 8080.", Now.AddMinutes(1)), (both.Note, both.Updated));

        await _sessions.SetStateAsync(parked.Id, SessionState.Working, null, null, null, Now.AddMinutes(3));
        Assert.Equal(2, (await _sessions.FindAsync(parked.Id))!.Said.Count);
        await _sessions.SetStateAsync(parked.Id, SessionState.AwaitingPerson, "and which host?", null, null, Now.AddMinutes(4), clearSaid: true);
        var cleared = (await _sessions.FindAsync(parked.Id))!;
        Assert.Empty(cleared.Said);
        Assert.Null(cleared.Answer);

        var fed = new Session(
            "alice-laptop/ab12cd34", "abc123", "Owner", "claude-code", SessionState.AwaitingPerson, "which port?", null, null, Now, Now);
        await _sessions.MirrorAsync(fed);
        Assert.Null(await _sessions.KeepSaidAsync(fed.Id, new SaidWord("w3", "8080.", Now, []), note: "which port?\n\nAnswered: 8080."));
        Assert.Empty((await _sessions.FindAsync(fed.Id))!.Said);
        Assert.Null(await _sessions.TakeSaidAsync(fed.Id, ["w3"]));
    }

    /// <summary>
    /// MSG1a: taking words removes exactly those named, in any order, passing over an id the record does not hold; the
    /// last one taken leaves nothing. No revision is written, since the words never travel.
    /// </summary>
    [Fact]
    public async Task Taking_words_removes_those_named_and_writes_no_revision()
    {
        var ended = await Create();
        await _sessions.SetStateAsync(ended.Id, SessionState.Completed, "landed.", null, null, Now);
        foreach (var id in new[] { "w1", "w2", "w3" })
        {
            await _sessions.KeepSaidAsync(ended.Id, new SaidWord(id, $"said {id}", Now, [], Reopens: true));
        }

        var revision = (await _sessions.OwnChangedSinceAsync(0, Workspaces.Default)).Single().Revision;

        var taken = await _sessions.TakeSaidAsync(ended.Id, ["w3", "w1", "nope"]);

        Assert.Equal(["w2"], taken!.Said.Select(word => word.Id));
        Assert.Empty(await _sessions.OwnChangedSinceAsync(revision, Workspaces.Default));
        Assert.Empty((await _sessions.TakeSaidAsync(ended.Id, ["w2"]))!.Said);
    }

    /// <summary>
    /// MSG1a (D137 §2.4): a store from before `said` kept a park's answer in its `answer` column. It reads as the record's
    /// first word, and a word kept after it joins it in the record's one list, so an answered park from before goes on
    /// with both and the old column holds nothing the list does not.
    /// </summary>
    [Fact]
    public async Task An_answer_from_before_said_reads_as_its_first_word_and_a_later_word_joins_it()
    {
        await using var old = new SqliteConnection("Data Source=:memory:");
        await old.OpenAsync();
        await using (var create = old.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE sessions (
                  id TEXT PRIMARY KEY, quest TEXT NULL, repository TEXT NOT NULL, adapter TEXT NOT NULL,
                  state TEXT NOT NULL, note TEXT NULL, evidence TEXT NULL, transcript TEXT NULL,
                  created TEXT NOT NULL, updated TEXT NOT NULL, answer TEXT NULL
                );
                INSERT INTO sessions (id, quest, repository, adapter, state, note, created, updated, answer)
                  VALUES ('abcd1234', 'q1', 'Owner', 'stub', 'AwaitingPerson', 'which port?', '2026-10-02T10:00:00Z',
                          '2026-10-02T10:05:00Z', '8080.');
                """;
            await create.ExecuteNonQueryAsync();
        }

        var store = await SessionStore.OpenAsync(old);

        var before = (await store.FindAsync("abcd1234"))!;
        var first = Assert.Single(before.Said);
        Assert.Equal(("answer", "8080.", DateTimeOffset.Parse("2026-10-02T10:05:00Z"), false), (first.Id, first.Text, first.At, first.Reopens));
        Assert.Empty(first.Files);
        Assert.Equal("8080.", before.Answer);
        // XAGENT1c: a record from before reads as before: its answer is the person's, and it names no opinion.
        Assert.Null(first.By);
        Assert.Null(before.Opinion);

        await store.KeepSaidAsync("abcd1234", new SaidWord("w2", "9090, not 8080.", Now, []));

        var joined = (await store.FindAsync("abcd1234"))!;
        Assert.Equal(["answer", "w2"], joined.Said.Select(word => word.Id));
        Assert.Equal("8080.\n\n9090, not 8080.", joined.Answer);
        await using var column = old.CreateCommand();
        column.CommandText = "SELECT answer FROM sessions WHERE id = 'abcd1234'";
        Assert.Equal(DBNull.Value, await column.ExecuteScalarAsync());
    }

    /// <summary>
    /// MSG1a: a word a later build wrote with fields this one does not know reads as the word it is, and a malformed one —
    /// no id, no text, no moment — is passed over, never a failed read of the record.
    /// </summary>
    [Fact]
    public async Task A_word_with_unknown_fields_reads_and_a_malformed_one_is_passed_over()
    {
        var ended = await Create();
        await using (var plant = _connection.CreateCommand())
        {
            plant.CommandText = """
                UPDATE sessions SET said = '[{"id":"w1","text":"kept","at":"2026-09-19T10:00:00+00:00","files":["a.md",3],"tone":"calm"},{"text":"no id","at":"2026-09-19T10:00:00+00:00"},{"id":"w3","at":"2026-09-19T10:00:00+00:00"},{"id":"w4","text":"no moment"},7]' WHERE id = $id
                """;
            plant.Parameters.AddWithValue("$id", ended.Id);
            await plant.ExecuteNonQueryAsync();
        }

        var word = Assert.Single((await _sessions.FindAsync(ended.Id))!.Said);

        Assert.Equal(("w1", "kept"), (word.Id, word.Text));
        Assert.Equal(["a.md"], word.Files);
        // XAGENT1c: a word with no `by` is the person's, as every word kept before the field is.
        Assert.Null(word.By);
    }

    /// <summary>
    /// SESSUX1f (D126 §5.4): a record that went up to a remote is marked so, and the mark is no change of the record, so
    /// it moves no revision and the next push does not send it again. It is kept through later moves, since the remote
    /// still holds the copy it was sent.
    /// </summary>
    [Fact]
    public async Task A_record_that_went_up_is_marked_pushed_without_a_new_revision_and_keeps_the_mark()
    {
        var sent = await Create();
        var home = await Create();
        var before = (await _sessions.OwnChangedSinceAsync(0, Workspaces.Default)).Max(change => change.Revision);

        await _sessions.MarkPushedAsync([sent.Id, "not-a-session"]);

        Assert.Empty(await _sessions.OwnChangedSinceAsync(before, Workspaces.Default));
        Assert.True((await _sessions.FindAsync(sent.Id))!.Pushed);
        Assert.False((await _sessions.FindAsync(home.Id))!.Pushed);
        await _sessions.SetStateAsync(sent.Id, SessionState.Stopped, null, null, null, Now.AddMinutes(1));
        Assert.True((await _sessions.FindAsync(sent.Id))!.Pushed);
    }

    /// <summary>
    /// 🔴 HIST1a (D153 point 4, H2): the revision never goes back. Deleting the newest record, as D126's delete does an
    /// ended conversation whose close was the newest write, leaves every later write numbered past what a push already
    /// examined: a new record, a move and a teammate's copy, even from a store opened again. Taken back, the push cursor
    /// would pass them by and they would never go up. Whether the newest revision was written by the record's making or
    /// by its move, each is kept.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deleting_the_newest_record_never_hands_its_revision_out_again(bool movedLast)
    {
        var older = await Create();
        var newest = await Create();
        if (movedLast) await _sessions.SetStateAsync(newest.Id, SessionState.Completed, "talked it through.", null, null, Now);
        var through = (await _sessions.OwnChangedSinceAsync(0, Workspaces.Default)).Max(change => change.Revision);
        Assert.True(await _sessions.DeleteAsync(newest.Id));

        var next = await Create();
        await _sessions.SetStateAsync(older.Id, SessionState.Working, null, null, null, Now.AddMinutes(1));
        await _sessions.MirrorAsync(new Session(
            "alice-laptop/ab12cd34", "abc123", "Owner", "claude-code", SessionState.Working, null, null, null, Now, Now));
        var reopened = await SessionStore.OpenAsync(_connection);
        var after = await reopened.CreateAsync("abc123", "Owner", "stub", Now.AddMinutes(2));

        var written = await reopened.OwnChangedSinceAsync(through, Workspaces.Default);
        Assert.Equal([next.Id, older.Id, after.Id], written.Select(change => change.Session.Id));
        Assert.Equal([through + 1, through + 2, through + 4], written.Select(change => change.Revision));
    }

    /// <summary>
    /// HIST1a: a store from before the mark starts it at the newest revision it holds, so a delete made after the upgrade
    /// cannot take the revision back either.
    /// </summary>
    [Fact]
    public async Task A_store_from_before_the_mark_starts_it_at_the_newest_revision()
    {
        await Create();
        var newest = await Create();
        var through = (await _sessions.OwnChangedSinceAsync(0, Workspaces.Default)).Max(change => change.Revision);
        await using (var older = _connection.CreateCommand())
        {
            // The build before HIST1a: no mark, and nothing that moves one.
            older.CommandText = """
                DROP TRIGGER IF EXISTS session_revision_written;
                DROP TRIGGER IF EXISTS session_revision_moved;
                DROP TABLE IF EXISTS session_revision;
                """;
            await older.ExecuteNonQueryAsync();
        }

        var upgraded = await SessionStore.OpenAsync(_connection);
        Assert.True(await upgraded.DeleteAsync(newest.Id));
        var next = await upgraded.CreateAsync("abc123", "Owner", "stub", Now.AddMinutes(1));

        var written = Assert.Single(await upgraded.OwnChangedSinceAsync(through, Workspaces.Default));
        Assert.Equal((next.Id, through + 1), (written.Session.Id, written.Revision));
    }

    /// <summary>
    /// SESSUX1f: the store deletes a record whole when asked, and says whether there was one. It judges nothing: whether a
    /// record may go is the ledger's.
    /// </summary>
    [Fact]
    public async Task A_record_is_deleted_whole_and_an_unknown_one_is_said()
    {
        var gone = await Create();
        var kept = await Create();

        Assert.True(await _sessions.DeleteAsync(gone.Id));
        Assert.False(await _sessions.DeleteAsync(gone.Id));

        Assert.Null(await _sessions.FindAsync(gone.Id));
        Assert.NotNull(await _sessions.FindAsync(kept.Id));
    }

    /// <summary>An attachment set earlier survives a later move that does not mention it.</summary>
    [Fact]
    public async Task An_unmentioned_attachment_is_kept_not_erased()
    {
        var session = await Create();
        await _sessions.SetStateAsync(session.Id, SessionState.Starting, null, null, "logs/s1.txt", Now);

        var moved = await _sessions.SetStateAsync(session.Id, SessionState.Working, null, null, null, Now);

        Assert.Equal("logs/s1.txt", moved!.Transcript);
    }

    /// <summary>The one-session-per-tree rule needs one question answered fast: who is active where.</summary>
    [Fact]
    public async Task The_active_session_for_a_repository_is_findable()
    {
        var session = await Create();
        await Create(quest: "def456", repository: "Elsewhere");

        var active = await _sessions.ActiveForAsync("Owner");

        Assert.Equal(session.Id, active!.Id);
        Assert.Null(await _sessions.ActiveForAsync("Nobody"));
    }

    /// <summary>
    /// Since D51 the question is per TREE: a repository may hold more than one, and a session in one
    /// of them is not a session in another. The store answers; the ledger judges.
    /// </summary>
    [Fact]
    public async Task The_active_session_is_asked_per_tree()
    {
        var main = await _sessions.CreateAsync("abc123", "Owner", "stub", Now, tree: "/trees/owner");

        Assert.Equal(main.Id, (await _sessions.ActiveForAsync("Owner", "/trees/owner"))!.Id);
        Assert.Null(await _sessions.ActiveForAsync("Owner", "/trees/owner-2"));
        Assert.Equal("/trees/owner", (await _sessions.FindAsync(main.Id))!.Tree);
    }

    /// <summary>
    /// Unknown means "possibly yours", on either side. A record that never said which tree it was in
    /// — anything from before D51, or a record mirrored from a machine that rightly sent no path —
    /// holds every tree in its repository, and an ASK that names no tree is answered by any of them.
    /// The lock errs toward refusing, because the other way round is two agents in one working tree.
    /// </summary>
    [Fact]
    public async Task A_tree_nobody_named_is_answered_conservatively()
    {
        var unstated = await Create();

        Assert.Equal(unstated.Id, (await _sessions.ActiveForAsync("Owner", "/trees/anywhere"))!.Id);
        Assert.Equal(unstated.Id, (await _sessions.ActiveForAsync("Owner"))!.Id);

        await _sessions.SetStateAsync(unstated.Id, SessionState.Completed, null, null, null, Now);
        var stated = await _sessions.CreateAsync("def456", "Owner", "stub", Now, tree: "/trees/owner");
        Assert.Equal(stated.Id, (await _sessions.ActiveForAsync("Owner"))!.Id);
    }

    /// <summary>A parked session still holds its repository: the person clearing it is the flow control.</summary>
    [Fact]
    public async Task Awaiting_person_still_counts_as_active()
    {
        var session = await Create();
        await _sessions.SetStateAsync(session.Id, SessionState.AwaitingPerson, null, null, null, Now);

        Assert.Equal(session.Id, (await _sessions.ActiveForAsync("Owner"))!.Id);
    }

    [Fact]
    public async Task A_finished_session_frees_its_repository()
    {
        var session = await Create();
        await _sessions.SetStateAsync(session.Id, SessionState.Completed, null, null, null, Now);

        Assert.Null(await _sessions.ActiveForAsync("Owner"));
    }

    /// <summary>Live work first, and closed records only on request — the same shape as the quest list.</summary>
    [Fact]
    public async Task Listing_hides_finished_sessions_unless_asked()
    {
        var done = await Create();
        await _sessions.SetStateAsync(done.Id, SessionState.Completed, null, null, null, Now);
        await Create(quest: "def456", repository: "Elsewhere");

        Assert.Single(await _sessions.ListAsync());
        Assert.Equal(2, (await _sessions.ListAsync(includeClosed: true)).Count);
        Assert.Empty(await _sessions.ListAsync("Owner"));
        Assert.Single(await _sessions.ListAsync("Owner", includeClosed: true));
    }

    [Fact]
    public async Task An_unknown_id_yields_nothing_rather_than_throwing()
    {
        Assert.Null(await _sessions.SetStateAsync("zzzzzzzz", SessionState.Working, null, null, null, Now));
        Assert.Null(await _sessions.FindAsync("zzzzzzzz"));
    }

    /// <summary>
    /// A fed record is the copy of a judgement that already ran on the machine that owns the process
    /// (D47 §6): it upserts whole and is never re-judged — the ledger's rules governed the original.
    /// Keyed by origin + id, because two machines will eventually mint the same random id.
    /// </summary>
    [Fact]
    public async Task A_mirrored_record_upserts_whole_and_carries_no_transcript()
    {
        var fed = new Session(
            "alice-laptop/ab12cd34", "abc123", "Owner", "claude-code",
            SessionState.Working, null, null, null, Now, Now);

        await _sessions.MirrorAsync(fed);
        await _sessions.MirrorAsync(fed with
        {
            State = SessionState.Completed,
            Evidence = "commit deadbee",
            Updated = Now.AddHours(1),
        });

        var read = (await _sessions.ListAsync(includeClosed: true)).Single(s => s.Id == "alice-laptop/ab12cd34");
        Assert.Equal(SessionState.Completed, read.State);
        Assert.Equal("commit deadbee", read.Evidence);
        Assert.Null(read.Transcript);
    }

    /// <summary>The wire spelling parses back — the same tolerance the ledger's advance door has.</summary>
    [Fact]
    public void The_wire_spelling_parses_back()
    {
        Assert.True(Session.TryParse("stood-down", out var stood) && stood == SessionState.StoodDown);
        Assert.True(Session.TryParse("awaiting-person", out var parked) && parked == SessionState.AwaitingPerson);
        Assert.True(Session.TryParse("Working", out var working) && working == SessionState.Working);
        Assert.False(Session.TryParse("paused", out _));
        Assert.False(Session.TryParse("", out _));
    }

    /// <summary>
    /// What a session ran ON and AS (D49 §4) — written once at spawn, and never revised by a later
    /// state change, which is about how it ENDED rather than about what it was.
    /// </summary>
    [Fact]
    public async Task A_record_carries_the_tool_and_the_account_it_ran_as()
    {
        var session = await _sessions.CreateAsync(
            "abc123", "Owner", "claude-code", Now,
            harnessVersion: "2.1.220 (Claude Code)", profile: "work");

        await _sessions.SetStateAsync(
            session.Id, SessionState.Completed, "done", null, null, Now.AddHours(1));

        var read = await _sessions.FindAsync(session.Id);
        Assert.Equal("2.1.220 (Claude Code)", read!.HarnessVersion);
        Assert.Equal("work", read.Profile);
    }

    /// <summary>Whitespace is nothing said — an empty version would read as a version of "".</summary>
    [Fact]
    public async Task An_unstated_tool_or_account_is_null_rather_than_blank()
    {
        var session = await _sessions.CreateAsync(
            "abc123", "Owner", "stub", Now, harnessVersion: "  ", profile: "");

        var read = await _sessions.FindAsync(session.Id);
        Assert.Null(read!.HarnessVersion);
        Assert.Null(read.Profile);
    }

    /// <summary>
    /// <b>The account name never crosses a machine boundary</b> (D49 §4) — machine-local, exactly like
    /// the transcript beside it. Written as a literal NULL by the mirror rather than taken from the
    /// record, so a caller that filled the field cannot make it travel by accident: the feed has no
    /// field for it, and the store would refuse it anyway. Two guards, one rule.
    /// </summary>
    [Fact]
    public async Task A_mirrored_record_keeps_the_tool_version_and_never_the_account_or_the_tree()
    {
        var fed = new Session(
            "alice-laptop/ab12cd34", "abc123", "Owner", "claude-code",
            SessionState.Working, null, null, null, Now, Now,
            HarnessVersion: "2.1.220 (Claude Code)", Profile: "alice-personal",
            // A tree is a PATH on somebody else's machine (D51) — the newest thing on the list the
            // transcript started. The feed has no field for it, and this is the guard that holds even
            // when a caller fills one in anyway.
            Tree: "/srv/work/Owner");

        await _sessions.MirrorAsync(fed);

        var read = await _sessions.FindAsync("alice-laptop/ab12cd34");
        Assert.Equal("2.1.220 (Claude Code)", read!.HarnessVersion);
        Assert.Null(read.Profile);
        Assert.Null(read.Tree);
    }

    /// <summary>
    /// XAGENT1c (D155 points 7 and 11): a reviewer's record is a chat that names the opinion it reads for, and another
    /// agent's claims wait in `said` naming that opinion as `by`. `answer`, which every reader takes as the person's words,
    /// joins only theirs; a word with no `by` is the person's, as every word before the field was. Neither travels: the
    /// mirror writes no opinion.
    /// </summary>
    [Fact]
    public async Task A_reviewers_record_names_its_opinion_and_another_agents_words_say_by_whom()
    {
        var reviewer = await _sessions.CreateAsync(
            null, "Owner", "codex-acp", Now, kind: SessionKind.Chat, tree: "clones/abcd1234", opinion: "abcd1234");
        var read = (await _sessions.FindAsync(reviewer.Id))!;
        Assert.Equal((SessionKind.Chat, null, "abcd1234"), (read.Kind, read.Quest, read.Opinion));
        Assert.Null((await Create()).Opinion);

        var working = await Create();
        await _sessions.SetStateAsync(working.Id, SessionState.Completed, "landed.", null, null, Now);
        await _sessions.KeepSaidAsync(working.Id, new SaidWord("w1", "Also add the changelog line.", Now, [], Reopens: true));
        await _sessions.KeepSaidAsync(working.Id, new SaidWord("w2", "Another agent claims…", Now, [], Reopens: true, By: "abcd1234"));

        var words = (await _sessions.FindAsync(working.Id))!;
        Assert.Equal(new string?[] { null, "abcd1234" }, words.Said.Select(word => word.By));
        Assert.Equal([true, false], words.Said.Select(word => word.Persons));
        Assert.Equal("Also add the changelog line.", words.Answer);

        await _sessions.TakeSaidAsync(working.Id, ["w1"]);
        var theirs = (await _sessions.FindAsync(working.Id))!;
        Assert.Equal("abcd1234", Assert.Single(theirs.Said).By);
        Assert.Null(theirs.Answer);

        await _sessions.MirrorAsync(reviewer with { Id = "alice-laptop/" + reviewer.Id, Origin = "alice-laptop" });
        Assert.Null((await _sessions.FindAsync("alice-laptop/" + reviewer.Id))!.Opinion);
    }

    /// <summary>A chat is the same row with no quest in it (D49 §3).</summary>
    [Fact]
    public async Task A_chat_round_trips_with_no_quest()
    {
        var chat = await _sessions.CreateAsync(
            null, "Owner", "stub", Now, workspace: null, kind: SessionKind.Chat);

        var read = await _sessions.FindAsync(chat.Id);

        Assert.Equal(SessionKind.Chat, read!.Kind);
        Assert.Null(read.Quest);
        // And it holds the repository exactly as driven work does — the lock reads the same row.
        Assert.Equal(chat.Id, (await _sessions.ActiveForAsync("Owner"))!.Id);
    }
}

/// <summary>
/// The upgrade that let a session have no quest (D49 §3).
/// </summary>
/// <remarks>
/// SQLite cannot drop a NOT NULL constraint, so the table is rebuilt — and the rows are COPIED, not
/// discarded. The entry store may rebuild by discarding because its contents are derived; a session
/// record is the reviewable trace of work that actually happened, and nothing can re-derive it.
/// </remarks>
public sealed class SessionSchemaUpgradeTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-19T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        // The table exactly as it stood before chats: quest NOT NULL, no kind column.
        await using var old = _connection.CreateCommand();
        old.CommandText = """
            CREATE TABLE sessions (
              id         TEXT PRIMARY KEY,
              quest      TEXT NOT NULL,
              repository TEXT NOT NULL,
              adapter    TEXT NOT NULL,
              state      TEXT NOT NULL,
              note       TEXT NULL,
              evidence   TEXT NULL,
              transcript TEXT NULL,
              created    TEXT NOT NULL,
              updated    TEXT NOT NULL,
              workspace  TEXT NOT NULL DEFAULT 'default'
            );
            INSERT INTO sessions (id, quest, repository, adapter, state, evidence, created, updated, workspace)
            VALUES ('old12345', 'abc123', 'Elder', 'claude-code', 'Completed', 'commit deadbee',
                    '2026-09-18T10:00:00+00:00', '2026-09-18T11:00:00+00:00', 'aurora');
            """;
        await old.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task The_upgrade_keeps_every_record_and_lets_a_chat_open()
    {
        var sessions = await SessionStore.OpenAsync(_connection);

        var elder = await sessions.FindAsync("old12345");
        Assert.NotNull(elder);
        Assert.Equal("abc123", elder!.Quest);
        Assert.Equal("commit deadbee", elder.Evidence);
        Assert.Equal("aurora", elder.Workspace);
        // Everything that predates kinds is driven — which is what it was.
        Assert.Equal(SessionKind.Driven, elder.Kind);
        // …and everything that predates D51 names no tree, which the lock reads as "possibly any of
        // them". A record of work that happened survives every column that arrives after it.
        Assert.Null(elder.Tree);
        // …and a record from before D104 says nothing about being interrupted: the old reading, a stop
        // that was the person's.
        Assert.False(elder.Interrupted);
        // …nor about an account's limit (TOOL4c): the old reading, a failure like any other.
        Assert.False(elder.Limit);
        // …and it names no second opinion (XAGENT1c): the old reading, a session that read nobody's work for one.
        Assert.Null(elder.Opinion);

        var chat = await sessions.CreateAsync(
            null, "Elder", "stub", Now, workspace: null, kind: SessionKind.Chat);
        Assert.Null((await sessions.FindAsync(chat.Id))!.Quest);
    }

    /// <summary>Opening twice must not rebuild twice — a guard that ran every time would be a rewrite
    /// of the whole table on every start, and the second one would find nothing to relax.</summary>
    [Fact]
    public async Task The_rebuild_runs_once()
    {
        await SessionStore.OpenAsync(_connection);
        var second = await SessionStore.OpenAsync(_connection);

        Assert.NotNull(await second.FindAsync("old12345"));
    }

    /// <summary>
    /// SESSUX1f (D126 §5.4): a store from before the pushed mark derives it once, from its push cursor. A record of this
    /// machine's at or before what its workspace pushed went up as far as anything here can tell, so it is marked, which
    /// errs toward refusing a delete; one written after, a teammate's and one of a workspace never pushed are not.
    /// </summary>
    [Fact]
    public async Task A_store_from_before_the_pushed_mark_derives_it_from_its_cursor()
    {
        var sessions = await SessionStore.OpenAsync(_connection);
        var earlier = await sessions.CreateAsync(null, "Elder", "stub", Now, kind: SessionKind.Chat);
        await sessions.AdvanceCursorAsync("aurora", pushed: (await sessions.OwnChangedSinceAsync(0, "aurora")).Max(change => change.Revision));
        await sessions.AdvanceCursorAsync(
            Workspaces.Default, pushed: (await sessions.OwnChangedSinceAsync(0, Workspaces.Default)).Max(change => change.Revision));
        var later = await sessions.CreateAsync(null, "Elder", "stub", Now, workspace: "aurora", kind: SessionKind.Chat);
        var elsewhere = await sessions.CreateAsync(null, "Elder", "stub", Now, workspace: "borealis", kind: SessionKind.Chat);
        await sessions.MirrorAsync(new Session("bob/fe12dc34", null, "Elder", "stub", SessionState.Completed, null, null, null, Now, Now));
        await using (var drop = _connection.CreateCommand())
        {
            drop.CommandText = "ALTER TABLE sessions DROP COLUMN pushed";
            await drop.ExecuteNonQueryAsync();
        }

        var upgraded = await SessionStore.OpenAsync(_connection);

        Assert.True((await upgraded.FindAsync("old12345"))!.Pushed);
        Assert.True((await upgraded.FindAsync(earlier.Id))!.Pushed);
        Assert.False((await upgraded.FindAsync(later.Id))!.Pushed);
        Assert.False((await upgraded.FindAsync(elsewhere.Id))!.Pushed);
        Assert.False((await upgraded.FindAsync("bob/fe12dc34"))!.Pushed);
    }
}
