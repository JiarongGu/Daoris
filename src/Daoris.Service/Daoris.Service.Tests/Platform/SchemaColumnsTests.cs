using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>A column another connection added between the check and the alter is the column wanted, not a failed open.</summary>
public sealed class SchemaColumnsTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        await using var create = _connection.CreateCommand();
        create.CommandText = "CREATE TABLE t (id INTEGER PRIMARY KEY)";
        await create.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Adding_a_column_that_is_already_there_is_not_an_error()
    {
        var gate = ConnectionGate.For(_connection);
        await gate.RunAsync(async () =>
        {
            await SchemaColumns.AddAsync(gate, "t", "extra TEXT NULL", CancellationToken.None);
            await SchemaColumns.AddAsync(gate, "t", "extra TEXT NULL", CancellationToken.None);
            Assert.True(await SchemaColumns.HasAsync(gate, "t", "extra", CancellationToken.None));
        }, CancellationToken.None);
    }
}
