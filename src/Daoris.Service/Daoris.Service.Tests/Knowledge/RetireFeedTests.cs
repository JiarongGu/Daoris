using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>A retirement and a feed are one step each, so a feed admitted before a retirement cannot leave its provenance behind it.</summary>
public sealed class RetireFeedTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T10:00:00Z");

    private readonly Held _store = new(new InMemoryKnowledgeStore());
    private Microsoft.Data.Sqlite.SqliteConnection _connection = null!;
    private RegistrationStore _registrations = null!;
    private KnowledgeService _service = null!;

    public async Task InitializeAsync()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _registrations = await RegistrationStore.OpenAsync(_connection);
        _service = new KnowledgeService(
            _store, new LexicalKnowledgeSearch(_store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry(), registrations: _registrations);
        await _service.RegisterAsync(new Registration("Open", Adopted: true, "A repo.", ["x"], [], [], Entries: 0, Joined: true, SharesKnowledge: true), Now);
        await _service.RefreshAsync();
        _store.Armed = true;
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task A_feed_admitted_before_a_retirement_leaves_no_provenance_behind()
    {
        var entry = new KnowledgeEntry("Open", EntryKind.Decision, Provenance.Local, "A lesson", "What was learned.", "docs/DECISIONS.md", "A lesson");
        var feeding = _service.FeedAsync(
            "Open", [entry], new FeedProvenance("aaaa1111bbbb2222", DateTimeOffset.Parse("2026-09-20T09:00:00Z"), "main", "person@machine-a"));
        await _store.GenerationsAsked.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var retiring = _service.RetireAsync("Open");
        // Judged inside one gate, a retirement has nothing to do until the feed is through, so it is given a moment, not waited for.
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        _store.ReleaseGenerations.SetResult();
        await feeding;
        _store.ReleaseRetirement.SetResult();
        await retiring;

        Assert.Null(await _registrations.ProvenanceAsync("Open"));
        Assert.Empty(await _store.AllAsync());
    }

    /// <summary>A store that parks a feed after it read the generations, and a retirement before it writes its empty replacement.</summary>
    private sealed class Held(IKnowledgeStore inner) : IKnowledgeStore
    {
        public bool Armed { get; set; }

        public TaskCompletionSource GenerationsAsked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseGenerations { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseRetirement { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyDictionary<string, long>> GenerationsAsync(CancellationToken ct = default)
        {
            var generations = await inner.GenerationsAsync(ct);
            if (Armed && GenerationsAsked.TrySetResult()) await ReleaseGenerations.Task;
            return generations;
        }

        public async Task<bool> ReplaceRepositoryAsync(
            string repository, IReadOnlyList<KnowledgeEntry> entries, ReadingWrite reading,
            Func<CancellationToken, Task>? alongside = null, CancellationToken ct = default)
        {
            if (Armed && entries.Count == 0) await ReleaseRetirement.Task;
            return await inner.ReplaceRepositoryAsync(repository, entries, reading, alongside, ct);
        }

        public Task RestampAsync(string repository, string workspace, CancellationToken ct = default) => inner.RestampAsync(repository, workspace, ct);

        public Task<IndexReading?> ReadingAsync(string repository, CancellationToken ct = default) => inner.ReadingAsync(repository, ct);

        public Task<HeldTogether> HeldAsync(IReadOnlyCollection<string> ids, Func<KnowledgeEntry, bool> withFile, CancellationToken ct = default) =>
            inner.HeldAsync(ids, withFile, ct);

        public Task<IReadOnlyList<KnowledgeEntry>> AllAsync(CancellationToken ct = default) => inner.AllAsync(ct);

        public Task<KnowledgeEntry?> FindAsync(string id, CancellationToken ct = default) => inner.FindAsync(id, ct);

        public Task<IReadOnlyDictionary<string, int>> CountByRepositoryAsync(CancellationToken ct = default) => inner.CountByRepositoryAsync(ct);
    }
}
