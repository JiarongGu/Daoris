using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The feed gate: a feed is admitted, ordered and written in one step, so what a declaration queued before it decided is what it meets.
/// </summary>
public sealed class FeedGateTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T10:00:00Z");

    private readonly InMemoryKnowledgeStore _store = new();
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
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private static Registration Declared(bool shares) =>
        new("Open", Adopted: true, "A repo.", ["x"], [], [], Entries: 0, Joined: true, SharesKnowledge: shares);

    private static FeedProvenance From(string commit, string at) => new(commit, DateTimeOffset.Parse(at), "main", "person@machine-a");

    /// <summary>
    /// 🔴 A feed admitted while its repository shared, then queued behind the declaration that stops it sharing, is judged again once
    /// that declaration stands: admitted before the gate, it would write what the repository no longer lets leave.
    /// </summary>
    [Fact]
    public async Task A_feed_queued_behind_a_declaration_that_stops_sharing_is_refused()
    {
        await _service.RegisterAsync(Declared(shares: true), Now);
        var registering = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var asked = 0;
        var admitted = new TaskCompletionSource();
        var feeds = new KnowledgeFeeds(
            _store, _registrations,
            async ct =>
            {
                var rows = await _service.RegistryAsync(ct: ct);
                // The declaration's own look is the first; the next is the feed's.
                if (Interlocked.Increment(ref asked) == 2) admitted.TrySetResult();
                return rows;
            },
            async (declared, now, ct) =>
            {
                registering.SetResult();
                await release.Task;
                return await _service.RegisterAsync(declared, now, ct);
            },
            () => { }, TimeProvider.System, new SemaphoreSlim(1, 1));

        var withdrawing = feeds.RegisterFedAsync(Declared(shares: false), From("aaaa1111bbbb2222", "2026-09-20T09:00:00Z"), Now, default);
        await registering.Task;
        var entry = new KnowledgeEntry("Open", EntryKind.Decision, Provenance.Local, "A lesson", "What was learned.", "docs/DECISIONS.md", "A lesson");
        var feeding = feeds.FeedAsync("Open", [entry], From("cccc3333dddd4444", "2026-09-20T09:30:00Z"), default);
        // A feed judged inside the gate has nothing to say until the declaration is through, so it is given a moment, not waited for.
        await Task.WhenAny(admitted.Task, Task.Delay(TimeSpan.FromMilliseconds(500)));
        release.SetResult();
        await withdrawing;

        Assert.Equal(FeedRefusal.NotSharing, (await feeding).Refusal);
        Assert.Empty(await _store.AllAsync());
    }
}
