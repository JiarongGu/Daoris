using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A store an older build wrote and no purge emptied is refused at open, naming the file and what to do, and left as it is: no
/// build reads its shape, so its first query would fail on a column it lacks. A store of this build's shape opens.
/// </summary>
public sealed class OlderStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-older-store-" + Guid.NewGuid().ToString("N")[..8]);

    private string Family => Path.Combine(_root, "family");

    private string Database => Path.Combine(_root, "index", "knowledge.db");

    public void Dispose()
    {
        SqlitePools.Release(Database);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task A_store_whose_table_lacks_a_column_is_refused_naming_the_file_and_left_as_it_was()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        await using (var older = new SqliteConnection($"Data Source={Database};Pooling=False"))
        {
            await older.OpenAsync();
            await using var command = older.CreateCommand();
            command.CommandText = "CREATE TABLE sessions (id TEXT PRIMARY KEY, quest TEXT NULL, repository TEXT NOT NULL)";
            await command.ExecuteNonQueryAsync();
        }

        var before = await File.ReadAllBytesAsync(Database);

        var refused = await Assert.ThrowsAsync<OlderStoreException>(() => ServiceFactory.CreateAsync(new ServiceOptions(Family, Database)));

        Assert.Equal(Database, refused.Store);
        Assert.Contains("sessions.door", refused.Missing);
        Assert.Contains(Database, refused.Message);
        Assert.Contains("purge the home's history or start a fresh home", refused.Message);
        SqlitePools.Release(Database);
        Assert.Equal(before, await File.ReadAllBytesAsync(Database));
    }

    [Fact]
    public async Task A_store_of_this_builds_shape_opens_again()
    {
        await using (await ServiceFactory.CreateAsync(new ServiceOptions(Family, Database)))
        {
        }

        await using var again = await ServiceFactory.CreateAsync(new ServiceOptions(Family, Database));
        Assert.NotNull(again);
    }
}
