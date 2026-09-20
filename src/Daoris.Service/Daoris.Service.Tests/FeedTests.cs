using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The remote's knowledge ingest (D47 §4): fed entries are the only entries a remote has — it never
/// scans a filesystem — and the disclosure judgement runs AT the door, over what each repository's own
/// reviewed manifest declared. Join admits records; knowledge is a second, separate declaration.
/// </summary>
public sealed class FeedTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T10:00:00Z");

    private readonly InMemoryKnowledgeStore _store = new();
    private readonly KnowledgeService _service;

    public FeedTests()
    {
        // Registrations are the only family here — the remote's own shape, and since D48 §3 every
        // deployment's shape: the registry is an explicit list, never a view over a folder.
        _service = new KnowledgeService(
            _store, new LexicalKnowledgeSearch(_store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
    }

    private Task Register(string name, bool joined, bool shares) => _service.RegisterAsync(
        new Registration(name, Adopted: true, "A repo.", ["x"], [], [], Entries: 0,
            Joined: joined, SharesKnowledge: shares), Now);

    private static KnowledgeEntry Entry(string title = "A lesson") => new(
        "ignored-by-normalization", EntryKind.Decision, Provenance.Canonical, title,
        "What was learned.", "docs/DECISIONS.md", title);

    [Fact]
    public async Task Feeding_an_unjoined_repository_is_refused()
    {
        await Register("Silent", joined: false, shares: false);

        var outcome = await _service.FeedAsync("Silent", [Entry()]);

        Assert.False(outcome.Accepted);
        Assert.Contains("join", outcome.Message);
        Assert.Empty(await _store.AllAsync());
    }

    /// <summary>Join admits records; knowledge is its own declaration. One switch must not imply the other.</summary>
    [Fact]
    public async Task Feeding_a_joined_repository_that_does_not_share_knowledge_is_refused()
    {
        await Register("Reserved", joined: true, shares: false);

        var outcome = await _service.FeedAsync("Reserved", [Entry()]);

        Assert.False(outcome.Accepted);
        Assert.Contains("knowledge", outcome.Message);
        Assert.Empty(await _store.AllAsync());
    }

    /// <summary>
    /// Accepted entries are normalized at the door: the repository name comes from the registration and
    /// the provenance is forced Local — canonical doctrine is distributed by `sync`, never by the feed.
    /// </summary>
    [Fact]
    public async Task Feeding_a_sharing_repository_replaces_its_entries_normalized()
    {
        await Register("Open", joined: true, shares: true);

        var first = await _service.FeedAsync("Open", [Entry("First lesson"), Entry("Second lesson")]);
        var second = await _service.FeedAsync("Open", [Entry("Second lesson")]);

        Assert.True(first.Accepted);
        Assert.Equal(2, first.Entries);
        Assert.True(second.Accepted);

        var stored = await _store.AllAsync();
        var entry = Assert.Single(stored);
        Assert.Equal("Open", entry.Repository);
        Assert.Equal(Provenance.Local, entry.Provenance);
        Assert.Equal("Second lesson", entry.Title);
    }

    [Fact]
    public async Task Feeding_an_unknown_repository_is_refused()
    {
        var outcome = await _service.FeedAsync("Stranger", [Entry()]);

        Assert.False(outcome.Accepted);
        Assert.Empty(await _store.AllAsync());
    }
}

/// <summary>
/// The session feed's judgement, in Core beside the ledger's (D47 §6): records upsert whole under
/// their origin, only joined repositories' records are taken, and a refused feed changes nothing.
/// </summary>
public sealed class SessionFeedTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T10:00:00Z");

    private Microsoft.Data.Sqlite.SqliteConnection _connection = null!;
    private SessionStore _sessions = null!;
    private SessionFeed _feed = null!;

    public async Task InitializeAsync()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _sessions = await SessionStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(
            new Registration("Joined", Adopted: true, null, [], [], [], Entries: 0, Joined: true), Now);
        await service.RegisterAsync(
            new Registration("Homebody", Adopted: true, null, [], [], [], Entries: 0, Joined: false), Now);
        _feed = new SessionFeed(service, _sessions);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private static FedSessionRecord Record(
        string id = "ab12cd34", string repository = "Joined", string state = "completed", string? quest = "abc123") =>
        new(id, quest, repository, "stub", state, null, null, Now, Now);

    [Fact]
    public async Task A_joined_repositorys_records_mirror_under_their_origin()
    {
        var outcome = await _feed.FeedAsync("alice-laptop", [Record()]);

        Assert.Equal(SessionFeedRefusal.None, outcome.Refusal);
        Assert.Equal(1, outcome.Records);
        var mirrored = Assert.Single(await _sessions.ListAsync(includeClosed: true));
        Assert.Equal("alice-laptop/ab12cd34", mirrored.Id);
        Assert.Equal(SessionState.Completed, mirrored.State);
    }

    [Fact]
    public async Task An_unjoined_repositorys_records_are_refused()
    {
        var outcome = await _feed.FeedAsync("alice-laptop", [Record(repository: "Homebody")]);

        Assert.Equal(SessionFeedRefusal.NotJoined, outcome.Refusal);
        Assert.Contains("remote.join", outcome.Message);
        Assert.Empty(await _sessions.ListAsync(includeClosed: true));
    }

    /// <summary>A refused feed changes nothing — the good record beside the bad one stays unmirrored.</summary>
    [Fact]
    public async Task A_malformed_record_refuses_the_whole_feed_before_anything_lands()
    {
        var outcome = await _feed.FeedAsync("alice-laptop", [Record(), Record(id: "ef56ab78", state: "99")]);

        Assert.Equal(SessionFeedRefusal.Malformed, outcome.Refusal);
        Assert.Empty(await _sessions.ListAsync(includeClosed: true));
    }
}
