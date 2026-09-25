using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A schema bump rebuilds the index rather than migrating it, because the index is derived: a shared
/// deployment is re-fed whole by the next sync (the store's own remarks). That holds only if the
/// rebuild also forgets which commits it claimed to hold.
/// </summary>
public sealed class SchemaRebuildTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-rebuild-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    /// <summary>
    /// REV3 service F3: the rebuild dropped the entries and kept `feed_provenance`, so the same
    /// commit's re-feed was judged already held and the shared index stayed empty until that
    /// repository's next commit.
    /// </summary>
    [Fact]
    public async Task A_rebuilt_index_takes_the_same_commit_again()
    {
        var options = new ServiceOptions(Path.Combine(_root, "unread"), Path.Combine(_root, "knowledge.db"));
        var from = new FeedProvenance(
            "aaaa1111bbbb2222", DateTimeOffset.Parse("2026-09-20T09:00:00Z"), "main", "person@machine-a");
        var entry = new KnowledgeEntry(
            "ignored", EntryKind.Decision, Provenance.Canonical, "A lesson", "What was learned.",
            "docs/DECISIONS.md", "A lesson");

        await using (var shared = await ServiceFactory.CreateAsync(options, source: new EmptyKnowledgeSource()))
        {
            await shared.Service.RegisterAsync(
                new Registration("Open", Adopted: true, "A repo.", ["x"], [], [], 0,
                    Joined: true, SharesKnowledge: true, DefaultBranch: "main"),
                DateTimeOffset.UtcNow);
            Assert.True((await shared.Service.FeedAsync("Open", [entry], from)).Accepted);
        }

        // What the next start finds after a build whose schema moved on.
        SqliteConnection.ClearAllPools();
        await using (var older = new SqliteConnection($"Data Source={options.DatabasePath}"))
        {
            await older.OpenAsync();
            await using var command = older.CreateCommand();
            command.CommandText = "PRAGMA user_version = 2;";
            await command.ExecuteNonQueryAsync();
        }
        SqliteConnection.ClearAllPools();

        await using var restarted = await ServiceFactory.CreateAsync(options, source: new EmptyKnowledgeSource());
        Assert.True((await restarted.Service.FeedAsync("Open", [entry], from)).Accepted);
        Assert.Equal(["Open"], (await restarted.Service.SummarizeAsync()).Select(r => r.Repository));
    }
}
