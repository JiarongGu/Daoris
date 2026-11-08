using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>A feed carries only the kinds a checkout's files make: a finished tree's record is this machine's own, never fed.</summary>
public sealed class FeedKindTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T10:00:00Z");

    private readonly InMemoryKnowledgeStore _store = new();
    private Microsoft.Data.Sqlite.SqliteConnection _connection = null!;
    private KnowledgeService _service = null!;

    public async Task InitializeAsync()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _service = new KnowledgeService(
            _store, new LexicalKnowledgeSearch(_store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry(), registrations: await RegistrationStore.OpenAsync(_connection));
        await _service.RegisterAsync(new Registration("Open", Adopted: true, "A repo.", ["x"], [], [], Entries: 0, Joined: true, SharesKnowledge: true), Now);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task A_feed_naming_the_record_kind_is_refused_whole()
    {
        var lesson = new KnowledgeEntry("Open", EntryKind.Decision, Provenance.Local, "A lesson", "What was learned.", "docs/DECISIONS.md", "A lesson");
        var forged = new KnowledgeEntry("Open", EntryKind.Record, Provenance.Local, "Finished tree t-1", "Landed.", "t-1");

        var outcome = await _service.FeedAsync(
            "Open", [lesson, forged], new FeedProvenance("aaaa1111bbbb2222", DateTimeOffset.Parse("2026-09-20T09:00:00Z"), "main", "person@machine-a"));

        Assert.Equal(FeedRefusal.Malformed, outcome.Refusal);
        Assert.Empty(await _store.AllAsync());
    }
}
