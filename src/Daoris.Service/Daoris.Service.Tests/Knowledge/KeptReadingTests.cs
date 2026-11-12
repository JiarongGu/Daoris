using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A host over the registered roots takes, at its first use, the readings an earlier process over the same store left, where this
/// build stamped them and the root has not moved since: a start reads what it kept instead of reading every checkout again.
/// </summary>
public sealed class KeptReadingTests : IDisposable
{
    private static readonly DateTimeOffset First = DateTimeOffset.Parse("2026-11-11T08:00:00Z");
    private static readonly DateTimeOffset Later = DateTimeOffset.Parse("2026-11-11T09:00:00Z");

    private readonly string _root = Path.Combine(Scratch(), "kept-reading-" + Guid.NewGuid().ToString("N")[..8]);

    private string Database => Path.Combine(_root, "knowledge.db");

    private string Family => Path.Combine(_root, "family");

    public KeptReadingTests()
    {
        Write("family/served/daoris.json", """{ "source": "s", "documents": { "decisions": "docs/decisions" } }""");
        Write("family/served/docs/decisions/D900.md", "# D900: The probe lock\n\nOne probe at a time.\n");
        Write("family/sibling/daoris.json", """{ "source": "s" }""");
        Write("family/sibling/.claude/knowledge/note.md", "# note\n\nThe sibling's probe lock.\n");
    }

    public void Dispose()
    {
        SqlitePools.Release(Database);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task A_start_takes_the_readings_an_earlier_process_left_where_no_root_moved()
    {
        await ReadOnceAsync();

        var later = await SummaryAtAsync(Later);

        Assert.Equal(First, later["served"].IndexedAt);
        Assert.Equal(First, later["sibling"].IndexedAt);
        Assert.Equal(1, later["served"].Total);
    }

    [Fact]
    public async Task A_root_that_moved_since_the_earlier_process_is_read_again_at_the_first_use_and_no_other()
    {
        await ReadOnceAsync();
        Write("family/served/docs/decisions/D901.md", "# D901: Zeppelins\n\nWritten while no process ran.\n");

        await using var composed = await ServiceFactory.CreateAsync(new ServiceOptions(Family, Database), time: new FixedTime(Later));
        var summary = (await composed.Service.SummarizeAsync()).ToDictionary(r => r.Repository);

        Assert.Equal(Later, summary["served"].IndexedAt);
        Assert.Equal(First, summary["sibling"].IndexedAt);
        Assert.Contains(await composed.Service.SearchAsync(new KnowledgeQuery("zeppelins")), hit => hit.Entry.Title.Contains("Zeppelins"));
    }

    [Fact]
    public async Task A_reading_another_build_stamped_is_read_again()
    {
        await ReadOnceAsync();
        await ExecuteAsync($"UPDATE {SqliteKnowledgeStore.ReadingsTable} SET scanner = 'an-older-build' WHERE repository = 'served';");

        var later = await SummaryAtAsync(Later);

        Assert.Equal(Later, later["served"].IndexedAt);
    }

    [Fact]
    public async Task A_reading_an_unstamping_writer_moved_on_is_read_again()
    {
        await ReadOnceAsync();
        // A build before the stamp writes the row as it always has: its generation moves on and the stamp it never wrote stays.
        await ExecuteAsync($"UPDATE {SqliteKnowledgeStore.ReadingsTable} SET generation = generation + 1 WHERE repository = 'sibling';");

        var later = await SummaryAtAsync(Later);

        Assert.Equal(Later, later["sibling"].IndexedAt);
        Assert.Equal(First, later["served"].IndexedAt);
    }

    [Fact]
    public async Task A_held_repository_no_registered_root_reads_takes_a_whole_reading_which_retires_it()
    {
        await ReadOnceAsync();
        var store = await SqliteKnowledgeStore.OpenAsync(Database);
        await store.ReplaceRepositoryAsync(
            "ghost",
            [new KnowledgeEntry("ghost", EntryKind.Knowledge, Provenance.Local, "note", "A ghost's probe.", "note.md")],
            ReadingWrite.Taken(First));
        await store.DisposeAsync();
        SqlitePools.Release(Database);

        var later = await SummaryAtAsync(Later);

        Assert.DoesNotContain("ghost", later.Keys);
        Assert.Equal(Later, later["served"].IndexedAt);
    }

    private async Task ReadOnceAsync()
    {
        var first = await SummaryAtAsync(First);
        Assert.Equal(["served", "sibling"], first.Keys.Order());
    }

    private async Task<Dictionary<string, RepositorySummary>> SummaryAtAsync(DateTimeOffset at)
    {
        await using var composed = await ServiceFactory.CreateAsync(new ServiceOptions(Family, Database), time: new FixedTime(at));
        return (await composed.Service.SummarizeAsync()).ToDictionary(r => r.Repository);
    }

    private async Task ExecuteAsync(string sql)
    {
        // Unpooled, so its closed connection holds the file no longer and no pool of the process is cleared for it.
        await using var connection = new SqliteConnection($"Data Source={Database};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private void Write(string relative, string content)
    {
        var file = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    /// <summary>The repository's gitignored scratch, never the machine's temporary folder.</summary>
    private static string Scratch()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return Path.Combine(at?.FullName ?? throw new InvalidOperationException("no repository above the test's build"), "_fixtures");
    }

    private sealed class FixedTime(DateTimeOffset at) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => at;
    }
}
