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

    /// <summary>An attachment set earlier survives a later move that does not mention it.</summary>
    [Fact]
    public async Task An_unmentioned_attachment_is_kept_not_erased()
    {
        var session = await Create();
        await _sessions.SetStateAsync(session.Id, SessionState.Starting, null, null, "logs/s1.txt", Now);

        var moved = await _sessions.SetStateAsync(session.Id, SessionState.Working, null, null, null, Now);

        Assert.Equal("logs/s1.txt", moved!.Transcript);
    }

    /// <summary>The one-session-per-repository rule needs one question answered fast: who is active where.</summary>
    [Fact]
    public async Task The_active_session_for_a_repository_is_findable()
    {
        var session = await Create();
        await Create(quest: "def456", repository: "Elsewhere");

        var active = await _sessions.ActiveForAsync("Owner");

        Assert.Equal(session.Id, active!.Id);
        Assert.Null(await _sessions.ActiveForAsync("Nobody"));
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
}
