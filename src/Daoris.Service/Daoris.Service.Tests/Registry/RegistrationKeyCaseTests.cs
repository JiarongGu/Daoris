using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>A repository is one identity whatever its case: a second spelling updates the row the first made.</summary>
public sealed class RegistrationKeyCaseTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T10:00:00Z");

    private SqliteConnection _connection = null!;
    private RegistrationStore _store = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _store = await RegistrationStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private static Registration Row(string name, string summary) =>
        new(name, Adopted: true, summary, ["x"], [], [], Entries: 0);

    [Fact]
    public async Task A_second_spelling_updates_the_registration_and_makes_no_second_row()
    {
        await _store.UpsertAsync(Row("Foo", "first"), Now);
        await _store.UpsertAsync(Row("foo", "second"), Now);

        var held = Assert.Single(await _store.AllAsync());
        Assert.Equal("Foo", held.Repository);
        Assert.Equal("second", held.Summary);
    }

    [Fact]
    public async Task A_second_spelling_updates_each_provenance_and_the_code_map_it_holds()
    {
        var older = new FeedProvenance("aaa", Now, "main");
        var newer = new FeedProvenance("bbb", Now.AddHours(1), "main");
        await _store.RecordProvenanceAsync("Foo", older);
        await _store.RecordProvenanceAsync("foo", newer);
        await _store.RecordRegistrationProvenanceAsync("Foo", older);
        await _store.RecordRegistrationProvenanceAsync("foo", newer);
        await _store.RecordCodeMapAsync("Foo", "map.json", "{}", older);
        await _store.RecordCodeMapAsync("foo", "map.json", "{}", newer);

        Assert.Equal("bbb", (await _store.ProvenanceAsync("FOO"))!.Commit);
        Assert.Equal("bbb", (await _store.RegistrationProvenanceAsync("FOO"))!.Commit);
        Assert.Equal("bbb", (await _store.CodeMapProvenanceAsync("FOO"))!.Commit);
        Assert.Single(await _store.AllProvenanceAsync());
    }
}
