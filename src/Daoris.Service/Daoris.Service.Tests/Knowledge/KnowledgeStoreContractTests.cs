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
        SqlitePools.Release(_file);
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

    [Fact]
    public Task The_memory_store_refuses_a_write_whose_generation_moved_and_writes_nothing_beside_it() =>
        A_write_whose_generation_moved_is_refused_and_writes_nothing(new InMemoryKnowledgeStore());

    [Fact]
    public Task The_sqlite_store_refuses_a_write_whose_generation_moved_and_writes_nothing_beside_it() =>
        A_write_whose_generation_moved_is_refused_and_writes_nothing(_sqlite);

    /// <summary>KNOWCENTRAL2a: a write lands only on the generation its writer read, and what rides with it lands with it.</summary>
    private static async Task A_write_whose_generation_moved_is_refused_and_writes_nothing(IKnowledgeStore store)
    {
        var at = DateTimeOffset.Parse("2026-10-31T09:00:00Z");
        var read = (await store.GenerationsAsync()).GetValueOrDefault("alpha");
        Assert.True(await store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "One")], new ReadingWrite(read, "c1", null, false, at)));
        var alongside = false;

        Assert.False(await store.ReplaceRepositoryAsync(
            "alpha", [Entry("alpha", "Two")], new ReadingWrite(read, "c2", null, false, at), _ => Task.FromResult(alongside = true)));

        Assert.Equal(["One"], (await store.AllAsync()).Select(entry => entry.Title));
        Assert.False(alongside);
        var held = await store.HeldAsync(["alpha:docs/One.md#One"], _ => false);
        Assert.Equal(("c1", read + 1), (held.Readings["alpha"].Commit, held.Readings["alpha"].Generation));
    }

    private static async Task A_repeated_id_is_refused_and_changes_nothing(IKnowledgeStore store)
    {
        await store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "One")]);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "Two"), Entry("alpha", "Two")]));

        Assert.Equal(["One"], (await store.AllAsync()).Select(entry => entry.Title));
    }
}
