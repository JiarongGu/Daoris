using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The contract both knowledge stores keep, asserted against each.
/// </summary>
/// <remarks>
/// Most of the suite runs on <see cref="InMemoryKnowledgeStore"/>, so wherever it is looser than the
/// SQLite store a test passes on something production refuses. A refresh that writes one id
/// twice would pass every test and fail on the real index.
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

    /// <summary>A write lands only on the generation its writer read, and what rides with it lands with it.</summary>
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

    /// <summary>
    /// What every read of a store answers after the same writes, as text: the two stores must answer alike, a workspace as given
    /// or not, a restamp, a held read, a find and a count.
    /// </summary>
    private static async Task<string> WhatAnswersAsync(IKnowledgeStore store)
    {
        var at = DateTimeOffset.Parse("2026-10-31T09:00:00Z");
        string[] titles = ["One", "Two"];
        await store.ReplaceRepositoryAsync(
            "alpha", [Entry("alpha", titles[0]) with { Workspace = "  Circle  " }, Entry("alpha", titles[1]) with { Workspace = "" }],
            new ReadingWrite(null, "c1", new RootSignature("c1", 5, 2), false, at));
        await store.ReplaceRepositoryAsync("beta", [Entry("beta", "Three")], ReadingWrite.Taken(at));
        await store.ReplaceRepositoryAsync("gamma", [], ReadingWrite.Taken(at));
        await store.RestampAsync("BETA", " Other ");

        var all = (await store.AllAsync()).OrderBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(entry => $"{entry.Id}@{entry.Workspace}");
        var held = await store.HeldAsync(["alpha:docs/One.md#One", "beta:docs/Three.md#Three", "none"], entry => entry.Repository == "alpha");
        var heldText = string.Join(",", held.Entries.Keys.Order(StringComparer.Ordinal))
            + "|" + string.Join(",", held.Files.Select(file => $"{file.Key.Repository}:{file.Key.Path}={file.Value.Count}"))
            + "|" + string.Join(",", held.Readings.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}:{pair.Value.Generation}:{pair.Value.Commit}:{pair.Value.Signature}"));
        var counts = (await store.CountByRepositoryAsync()).OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}");
        var found = await store.FindAsync("beta:docs/Three.md#Three");
        return string.Join(";", all) + "/" + heldText + "/" + string.Join(",", counts)
            + "/" + found?.Workspace + "/" + (await store.FindAsync("none") is null) + "/" + (await store.OfFileAsync("alpha", "docs/One.md")).Count;
    }

    [Fact]
    public async Task The_memory_store_answers_every_read_as_the_sqlite_store_does()
    {
        Assert.Equal(await WhatAnswersAsync(_sqlite), await WhatAnswersAsync(new InMemoryKnowledgeStore()));
    }

    [Fact]
    public async Task A_workspace_is_held_trimmed_and_the_default_where_none_is_given()
    {
        var store = new InMemoryKnowledgeStore();
        await store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "One") with { Workspace = "  Circle  " }, Entry("alpha", "Two") with { Workspace = " " }]);

        Assert.Equal(["Circle", Workspaces.Default], (await store.AllAsync()).Select(entry => entry.Workspace));
    }

    private static async Task A_repeated_id_is_refused_and_changes_nothing(IKnowledgeStore store)
    {
        await store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "One")]);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "Two"), Entry("alpha", "Two")]));

        Assert.Equal(["One"], (await store.AllAsync()).Select(entry => entry.Title));
    }
}
