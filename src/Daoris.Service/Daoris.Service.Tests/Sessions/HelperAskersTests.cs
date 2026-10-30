using System.Text.Json;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// FOUNDATION1q (D212; the foundation design §2.3, §7 row 9; the survey's §1.3 row 6): the service's two doors that ask whether a
/// session may ask for a helper, the desk that opens a helper's record and the connector's <c>helper_ask</c>, keep their own guard over
/// the store, held to the driver's start layer by one table, <c>fixtures/helper-askers.json</c>, row for row. Ask Daoris is refused at both
/// (D203 point 6).
/// </summary>
public sealed class HelperAskersTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly string _root = Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures", "helper-askers", Guid.NewGuid().ToString("N")[..8]);

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

    public static TheoryData<string> Rows => [.. Table.Read().Rows.Select(row => row.Name)];

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task The_desk_and_the_connector_answer_each_asker_as_the_start_layer_does(string name)
    {
        var table = Table.Read();
        var row = table.Rows.Single(each => each.Name == name);
        var marks = row.Marks;
        var asker = await _sessions.CreateAsync(
            null, marks.Repository, "claude-code", Now, kind: marks.Kind == "chat" ? SessionKind.Chat : SessionKind.Driven, tree: Path.Combine(_root, "own"),
            ask: marks.Ask, opinion: marks.Opinion, serves: marks.Serves, piece: marks.Piece);

        var opened = await new HelperDesk(_sessions).OpenAsync(new HelperOpen(asker.Id, "codex-acp", Path.Combine(_root, "clone")), Now);
        var (asked, said) = new HelperBox(Path.Combine(_root, "home")).Ask(asker, asker.Id, "Why?", null, Now);

        Assert.Equal(row.Refused is null ? HelperRefusal.None : HelperRefusal.BadShape, opened.Refusal);
        Assert.Equal(row.Refused is null, asked is not null);
        if (row.Refused is { } refused)
        {
            Assert.Contains(table.Says[refused], opened.Message);
            Assert.Contains(table.Says[refused], said);
        }
    }

    private sealed record Marks(string Kind, string Repository, string? Ask, string? Serves, string? Piece, string? Opinion);

    private sealed record Row(string Name, Marks Marks, string? Refused);

    private sealed record Table(IReadOnlyDictionary<string, string> Says, IReadOnlyList<Row> Rows)
    {
        public static Table Read() =>
            JsonSerializer.Deserialize<Table>(
                File.ReadAllText(Path.Combine(SourceFiles.WorkspaceFolder(), "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "helper-askers.json")),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
