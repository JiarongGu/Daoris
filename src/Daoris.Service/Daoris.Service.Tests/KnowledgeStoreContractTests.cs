using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The contract both knowledge stores keep, asserted against each (REV3 CLEAN1).
/// </summary>
/// <remarks>
/// Most of the suite runs on <see cref="InMemoryKnowledgeStore"/>, so wherever it is looser than the
/// SQLite store a test passes on something production refuses. That is how a refresh that wrote one id
/// twice passed every test and failed on the real index (REV3, service F4).
/// </remarks>
public sealed class KnowledgeStoreContractTests : IAsyncLifetime
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"daoris-contract-{Guid.NewGuid():N}.db");
    private SqliteKnowledgeStore _sqlite = null!;

    public async Task InitializeAsync() => _sqlite = await SqliteKnowledgeStore.OpenAsync(_file);

    public async Task DisposeAsync()
    {
        await _sqlite.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_file)) File.Delete(_file);
    }

    private static KnowledgeEntry Entry(string repository, string title) =>
        new(repository, EntryKind.Decision, Provenance.Local, title, $"Body of {title}.", $"docs/{title}.md", title);

    [Fact]
    public Task The_memory_store_refuses_a_repeated_id_and_keeps_what_it_held() =>
        A_repeated_id_is_refused_and_changes_nothing(new InMemoryKnowledgeStore());

    [Fact]
    public Task The_sqlite_store_refuses_a_repeated_id_and_keeps_what_it_held() =>
        A_repeated_id_is_refused_and_changes_nothing(_sqlite);

    private static async Task A_repeated_id_is_refused_and_changes_nothing(IKnowledgeStore store)
    {
        await store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "One")]);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "Two"), Entry("alpha", "Two")]));

        Assert.Equal(["One"], (await store.AllAsync()).Select(entry => entry.Title));
    }
}
