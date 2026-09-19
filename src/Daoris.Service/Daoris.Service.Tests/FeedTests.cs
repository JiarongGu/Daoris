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

    private sealed class NoSource : IKnowledgeSource
    {
        public string Name => "test";
        public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeEntry>>([]);
    }

    private readonly InMemoryKnowledgeStore _store = new();
    private readonly KnowledgeService _service;

    public FeedTests()
    {
        // A registry rooted nowhere: pushed registrations are the only family, the remote's own shape.
        _service = new KnowledgeService(
            _store, new LexicalKnowledgeSearch(_store), new NoSource(),
            DisclosurePolicy.LocalOnly,
            registry: new Registry(Path.Combine(Path.GetTempPath(), "daoris-nowhere-" + Guid.NewGuid().ToString("N")[..8])));
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
