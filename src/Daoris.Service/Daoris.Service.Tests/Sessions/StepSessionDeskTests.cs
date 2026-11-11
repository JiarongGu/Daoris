using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The record of the session that performs a plugin's step: a chat on no quest at the run's place with no tree of its own, marked with the
/// attempt it performs, read as the step's work, holding no place's lease; a blank part or a shared host is refused.
/// </summary>
public sealed class StepSessionDeskTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly string _root = Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures", "step-session-desk", Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(_root, "service.db"), Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        await _connection.OpenAsync();
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

    private static StepSessionOpen Open(string performs = "inv-1", string repository = "engine") =>
        new(performs, repository, "games", "claude-code") { Account = "work", DoorVersion = "2.1.0" };

    [Fact]
    public async Task The_record_is_a_chat_at_the_run_s_place_marked_with_its_attempt_read_as_the_step_s_work()
    {
        var opened = await new StepSessionDesk(_sessions).OpenAsync(Open(), Now);

        Assert.Equal(HelperRefusal.None, opened.Refusal);
        var record = opened.Session!;
        Assert.Equal(("engine", "games", SessionKind.Chat, "inv-1"), (record.Repository, record.Workspace, record.Kind, record.Performs));
        Assert.Null(record.Tree);
        Assert.Null(record.Quest);
        Assert.Equal(ParticipantWork.Step, ParticipantWork.Of(record));
        Assert.Equal("inv-1", (await _sessions.FindAsync(record.Id))!.Performs);
    }

    [Fact]
    public async Task A_working_step_session_holds_no_place_s_lease()
    {
        var record = (await new StepSessionDesk(_sessions).OpenAsync(Open(), Now)).Session!;
        await _sessions.SetStateAsync(record.Id, SessionState.Working, null, null, null, Now);

        Assert.Null(await new SessionPlaceLease(_sessions).HolderAsync("engine", Path.Combine(_root, "engine"), CancellationToken.None));
        Assert.Null(await _sessions.ActiveForAsync("engine"));
    }

    [Theory]
    [InlineData("", "engine")]
    [InlineData("inv-1", "")]
    public async Task An_open_naming_no_attempt_or_no_repository_is_refused(string performs, string repository)
    {
        var opened = await new StepSessionDesk(_sessions).OpenAsync(Open(performs, repository), Now);

        Assert.Equal(HelperRefusal.BadShape, opened.Refusal);
        Assert.Null(opened.Session);
    }

    [Fact]
    public async Task A_shared_host_opens_none()
    {
        var opened = await new StepSessionDesk(_sessions, local: false).OpenAsync(Open(), Now);

        Assert.Equal(HelperRefusal.Shared, opened.Refusal);
    }
}
