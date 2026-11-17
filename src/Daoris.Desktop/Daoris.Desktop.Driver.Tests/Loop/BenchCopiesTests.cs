using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The bench's copied records, each a chat of its own: never a part of the template's line or its landing entry.</summary>
public sealed class BenchCopiesTests : IDisposable
{
    private const string Template = "chat-template";

    private readonly SqliteConnection _store = new("Data Source=:memory:");

    public BenchCopiesTests()
    {
        _store.Open();
        Run("CREATE TABLE sessions (id TEXT PRIMARY KEY, state TEXT, line TEXT, entry TEXT, tree TEXT, base_commit TEXT, created TEXT, updated TEXT, revision INTEGER)");
        Run($"INSERT INTO sessions VALUES ('{Template}', 'Completed', '{Template}', 'session:{Template}/engine', 'tree', 'base', 'then', 'then', 7)");
    }

    public void Dispose() => _store.Dispose();

    [Fact]
    public void Each_copy_is_a_line_and_an_entry_of_its_own()
    {
        BenchCopies.Insert(_store, Template, 3, DateTimeOffset.UtcNow);

        var copies = Rows("SELECT id, line, entry FROM sessions WHERE id <> $template ORDER BY id");
        Assert.Equal(["00000000", "00000001", "00000002"], copies.Select(row => row[0]));
        Assert.All(copies, row => Assert.Equal(row[0], row[1]));
        Assert.All(copies, row => Assert.Equal($"session:{row[0]}/engine", row[2]));
        Assert.Single(Rows("SELECT id FROM sessions WHERE line = $template"));
    }

    [Fact]
    public void Each_copy_keeps_the_template_s_state_with_no_tree_and_a_revision_of_its_own()
    {
        BenchCopies.Insert(_store, Template, 2, DateTimeOffset.UtcNow);

        var copies = Rows("SELECT state, tree, base_commit, revision FROM sessions WHERE id <> $template ORDER BY id");
        Assert.All(copies, row => Assert.Equal(("Completed", (string?)null, (string?)null), (row[0], row[1], row[2])));
        Assert.Equal(["8", "9"], copies.Select(row => row[3]));
    }

    private void Run(string sql)
    {
        using var command = _store.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private List<string?[]> Rows(string sql)
    {
        using var command = _store.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$template", Template);
        using var reader = command.ExecuteReader();
        var rows = new List<string?[]>();
        while (reader.Read())
        {
            rows.Add([.. Enumerable.Range(0, reader.FieldCount).Select(at => reader.IsDBNull(at) ? null : Convert.ToString(reader.GetValue(at)))]);
        }

        return rows;
    }
}
