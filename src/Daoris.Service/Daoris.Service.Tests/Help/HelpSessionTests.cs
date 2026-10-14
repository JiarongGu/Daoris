using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// Ask Daoris's session (HELP1a, D89): a conversation about Daoris itself, opened in a room under the
/// driver's home the way an intake's is — a chat serving no quest and no ask, in no repository, one
/// running per room. The harness carries the model; nothing here names one.
/// </summary>
public sealed class HelpSessionTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-help-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-29T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        _ledger = new SessionLedger(quests, _sessions);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Room() => Path.Combine(_root, "home", "help");

    [Fact]
    public async Task A_help_session_is_a_conversation_in_its_room_serving_nothing()
    {
        var opened = await _ledger.OpenHelpAsync("stub", Room(), Now, harnessVersion: "1.2.3", profile: "work");

        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        var session = opened.Session!;
        // A conversation (SES2), the kind every build already reads as one nothing plans from.
        Assert.Equal(SessionKind.Chat, session.Kind);
        Assert.Null(session.Quest);
        Assert.Null(session.Ask);
        // Its own "repository", which no folder can be called: a colon is in no folder name.
        Assert.Equal("daoris:help", session.Repository);
        Assert.Equal(SessionLedger.HelpRepository, session.Repository);
        Assert.Equal("default", session.Workspace);
        Assert.Equal(Trees.Normalize(Room()), session.Tree);
        // The room is no repository, and git asked about it would walk up.
        Assert.Null(session.BaseCommit);
        Assert.Equal("1.2.3", session.HarnessVersion);
        Assert.Equal("work", session.Profile);
        Assert.Contains(session.Id, opened.Message);
    }

    /// <summary>One conversation per machine: a second open while one runs is refused, naming it.</summary>
    [Fact]
    public async Task One_help_session_runs_at_a_time_and_an_ended_one_leaves_the_room()
    {
        var holding = (await _ledger.OpenHelpAsync("stub", Room(), Now)).Session!;

        var busy = await _ledger.OpenHelpAsync("stub", Room(), Now);

        Assert.Equal(SessionOpenRefusal.RepositoryBusy, busy.Refusal);
        Assert.Contains(holding.Id, busy.Message);
        Assert.Null(busy.Session);

        await _sessions.SetStateAsync(holding.Id, SessionState.Completed, null, null, null, Now);
        Assert.Equal(SessionOpenRefusal.None, (await _ledger.OpenHelpAsync("stub", Room(), Now)).Refusal);
    }

    [Fact]
    public async Task A_help_session_needs_its_room()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _ledger.OpenHelpAsync("stub", " ", Now));
    }
}
