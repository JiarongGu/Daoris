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
}
