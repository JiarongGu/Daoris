using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A test lets go of its own SQLite file by clearing that database's pool, never every pool (FLAKE3). Clearing all of them
/// reached the pools of the classes running beside it, and Microsoft.Data.Sqlite reads a connection still being opened as
/// leaked and disposes it: <c>SearchTests</c> then met <c>ObjectDisposedException</c> on a connection it had just opened.
/// </summary>
public sealed class SqlitePoolsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-pools-" + Guid.NewGuid().ToString("N")[..8]);

    public SqlitePoolsTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        SqlitePools.Release(Path.Combine(_root, "mine.db"), Path.Combine(_root, "theirs.db"));
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private async Task<string> WrittenAndClosedAsync(string name)
    {
        var file = Path.Combine(_root, name);
        await using var store = await SqliteKnowledgeStore.OpenAsync(file);
        await store.ReplaceRepositoryAsync("alpha", [
            new KnowledgeEntry("alpha", EntryKind.Decision, Provenance.Local, "D1", "A body.", "docs/D1.md", "D1"),
        ]);
        return file;
    }

    /// <summary>A store's closed connection goes back to its pool, which holds the file open until the pool lets it go.</summary>
    [Fact]
    public async Task Releasing_a_database_lets_go_of_the_file_its_closed_store_left_held()
    {
        var mine = await WrittenAndClosedAsync("mine.db");

        SqlitePools.Release(mine);

        File.Delete(mine);
        Assert.False(File.Exists(mine));
    }

    /// <summary>
    /// The release reaches the one database it names: another database's pooled connection is left as it was, so a class
    /// releasing its own file never touches a connection another class is opening.
    /// </summary>
    [Fact]
    public async Task Releasing_one_database_leaves_another_database_s_pool_as_it_was()
    {
        var mine = await WrittenAndClosedAsync("mine.db");
        var theirs = await WrittenAndClosedAsync("theirs.db");

        SqlitePools.Release(mine);

        File.Delete(mine);
        // Windows refuses to delete a file a pooled connection still holds; elsewhere a delete succeeds regardless.
        if (OperatingSystem.IsWindows()) Assert.Throws<IOException>(() => File.Delete(theirs));
    }

    /// <summary>
    /// The class, not the call: xUnit runs this project's classes in parallel, and <c>ClearAllPools</c> clears every pool in
    /// the process. A source scan, because the next test written will not know to.
    /// </summary>
    [Fact]
    public void No_test_here_clears_every_pool_in_the_process()
    {
        var tests = TestsRoot();
        var clearing = Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && Path.GetFileName(file) != "SqlitePoolsTests.cs")
            .Where(file => File.ReadAllText(file).Contains("ClearAllPools(", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(tests, file))
            .ToList();

        Assert.True(
            clearing.Count == 0,
            "ClearAllPools disposes connections other classes are opening; release the test's own database "
            + "(SqlitePools.Release):\n" + string.Join('\n', clearing));
    }

    private static string TestsRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Daoris.Service.Tests.csproj")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("the service tests' source was not found above the test binary");
    }
}
