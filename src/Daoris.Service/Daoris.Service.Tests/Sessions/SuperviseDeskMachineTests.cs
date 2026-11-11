using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A supervisor's turn on a machine subject: opened naming no repository and no workspace, so its record holds no repository and only
/// the store's none-named workspace; a machine open naming either is refused, and a subject's open with no repository still is.
/// </summary>
public sealed class SuperviseDeskMachineTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly string _root = Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures", "supervise-desk-machine", Guid.NewGuid().ToString("N")[..8]);

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

    private SuperviseOpen Open(string repository, string? workspace, bool machine) =>
        new("att-1", "second-check", repository, workspace, "s-asked", "claude-code", Path.Combine(_root, "reader")) { Machine = machine };

    [Fact]
    public async Task A_machine_subject_opens_a_record_naming_no_repository_and_no_workspace()
    {
        var opened = await new SuperviseDesk(_sessions).OpenAsync(Open("", null, machine: true), Now);

        Assert.Equal(HelperRefusal.None, opened.Refusal);
        var record = opened.Session!;
        Assert.Equal("", record.Repository);
        Assert.Equal(Workspaces.Normalize(null), record.Workspace);
        Assert.Equal("att-1", record.Decides);
        Assert.Equal("s-asked", record.Serves);
    }

    [Theory]
    [InlineData("engine", null)]
    [InlineData("", "games")]
    public async Task A_machine_subject_naming_a_repository_or_a_workspace_is_refused(string repository, string? workspace)
    {
        var opened = await new SuperviseDesk(_sessions).OpenAsync(Open(repository, workspace, machine: true), Now);

        Assert.Equal(HelperRefusal.BadShape, opened.Refusal);
        Assert.Null(opened.Session);
    }

    [Fact]
    public async Task A_subject_s_open_naming_no_repository_is_still_refused()
    {
        var opened = await new SuperviseDesk(_sessions).OpenAsync(Open("", null, machine: false), Now);

        Assert.Equal(HelperRefusal.BadShape, opened.Refusal);
    }
}
