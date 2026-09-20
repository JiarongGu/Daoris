using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The remote's knowledge ingest (D47 §4): fed entries are the only entries a remote has — it never
/// scans a filesystem — and the disclosure judgement runs AT the door, over what each repository's own
/// reviewed manifest declared. Join admits records; knowledge is a second, separate declaration.
/// </summary>
public sealed class FeedTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T10:00:00Z");

    private readonly InMemoryKnowledgeStore _store = new();
    private Microsoft.Data.Sqlite.SqliteConnection _connection = null!;
    private KnowledgeService _service = null!;

    public async Task InitializeAsync()
    {
        // A REAL registration store, because since D48 §6 the door's judgement has a memory: what
        // commit this deployment already holds is the thing a stale feed is compared against, and it
        // must survive a restart or the first feed after one would always win.
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var registrations = await RegistrationStore.OpenAsync(_connection);

        // Registrations are the only family here — the remote's own shape, and since D48 §3 every
        // deployment's shape: the registry is an explicit list, never a view over a folder.
        _service = new KnowledgeService(
            _store, new LexicalKnowledgeSearch(_store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry(), registrations: registrations);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private Task Register(string name, bool joined, bool shares, string? defaultBranch = null) =>
        _service.RegisterAsync(
            new Registration(name, Adopted: true, "A repo.", ["x"], [], [], Entries: 0,
                Joined: joined, SharesKnowledge: shares, DefaultBranch: defaultBranch), Now);

    private static KnowledgeEntry Entry(string title = "A lesson") => new(
        "ignored-by-normalization", EntryKind.Decision, Provenance.Canonical, title,
        "What was learned.", "docs/DECISIONS.md", title);

    /// <summary>The ordinary feed: the canonical line, at a moment in its history.</summary>
    private static FeedProvenance From(
        string commit = "aaaa1111bbbb2222", string branch = "main", string at = "2026-09-20T09:00:00Z",
        string? origin = "person@machine-a") =>
        new(commit, DateTimeOffset.Parse(at), branch, origin);

    [Fact]
    public async Task Feeding_an_unjoined_repository_is_refused()
    {
        await Register("Silent", joined: false, shares: false);

        var outcome = await _service.FeedAsync("Silent", [Entry()], From());

        Assert.Equal(FeedRefusal.NotJoined, outcome.Refusal);
        Assert.Contains("join", outcome.Message);
        Assert.Empty(await _store.AllAsync());
    }

    /// <summary>Join admits records; knowledge is its own declaration. One switch must not imply the other.</summary>
    [Fact]
    public async Task Feeding_a_joined_repository_that_does_not_share_knowledge_is_refused()
    {
        await Register("Reserved", joined: true, shares: false);

        var outcome = await _service.FeedAsync("Reserved", [Entry()], From());

        Assert.Equal(FeedRefusal.NotSharing, outcome.Refusal);
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

        var first = await _service.FeedAsync("Open", [Entry("First lesson"), Entry("Second lesson")], From());
        var second = await _service.FeedAsync(
            "Open", [Entry("Second lesson")], From("cccc3333dddd4444", at: "2026-09-20T09:30:00Z"));

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
        var outcome = await _service.FeedAsync("Stranger", [Entry()], From());

        Assert.Equal(FeedRefusal.NotJoined, outcome.Refusal);
        Assert.Empty(await _store.AllAsync());
    }

    // ——— Which point in the history speaks (D48 §6). Wholesale replacement between two machines is a
    // flapping generator without these three: each tick, whichever fed last would overwrite the other.

    /// <summary>
    /// A feed that names no commit cannot be compared with what is held, so it is refused rather than
    /// taken on trust — an unguarded replacement is exactly the failure these rules exist to end.
    /// </summary>
    [Fact]
    public async Task A_feed_that_names_no_commit_is_refused()
    {
        await Register("Open", joined: true, shares: true);

        var outcome = await _service.FeedAsync("Open", [Entry()], provenance: null);

        Assert.Equal(FeedRefusal.NoProvenance, outcome.Refusal);
        Assert.Empty(await _store.AllAsync());
    }

    /// <summary>
    /// Only the canonical line feeds knowledge. A feature-branch checkout is work in flight: the
    /// branch and its review display it better than an index would, and its records still travel.
    /// </summary>
    [Fact]
    public async Task A_feed_from_a_branch_that_is_not_the_canonical_line_is_refused_naming_both()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");

        var outcome = await _service.FeedAsync("Open", [Entry()], From(branch: "feature/streaming"));

        Assert.Equal(FeedRefusal.NotDefaultBranch, outcome.Refusal);
        Assert.Contains("feature/streaming", outcome.Message);
        Assert.Contains("main", outcome.Message);
        Assert.True(outcome.Information);
        Assert.Empty(await _store.AllAsync());
    }

    /// <summary>
    /// A repository that never declared a canonical line has not told the deployment which branch is
    /// the family's — so any branch feeds, and the provenance served names which one did. Refusing
    /// instead would silence every repository that simply never said.
    /// </summary>
    [Fact]
    public async Task A_repository_with_no_declared_default_branch_feeds_from_any_line()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: null);

        var outcome = await _service.FeedAsync("Open", [Entry()], From(branch: "whatever"));

        Assert.True(outcome.Accepted);
        Assert.Equal("whatever", (await _service.SummarizeAsync()).Single().Fed!.Branch);
    }

    /// <summary>
    /// The flapping this exists to end: a stale checkout may not clobber a fresher one. The refusal is
    /// INFORMATION — that machine is simply behind — and the index keeps what it has.
    /// </summary>
    [Fact]
    public async Task A_feed_from_an_older_commit_keeps_what_is_held_and_says_why()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedAsync(
            "Open", [Entry("The newer lesson")], From("newnewnew", at: "2026-09-20T12:00:00Z"));

        var stale = await _service.FeedAsync(
            "Open", [Entry("The older lesson")], From("oldoldold", at: "2026-09-19T12:00:00Z"));

        Assert.Equal(FeedRefusal.Stale, stale.Refusal);
        Assert.True(stale.Information);
        Assert.Contains("newer commit", stale.Message);
        Assert.Equal("The newer lesson", (await _store.AllAsync()).Single().Title);
    }

    /// <summary>The newest canonical view replaces wholesale — which is what makes DELETE work for free.</summary>
    [Fact]
    public async Task A_feed_from_a_newer_commit_replaces_and_a_deletion_travels_with_it()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedAsync(
            "Open", [Entry("Kept"), Entry("Deleted upstream")], From("oldoldold", at: "2026-09-19T12:00:00Z"));

        var newer = await _service.FeedAsync(
            "Open", [Entry("Kept")], From("newnewnew", at: "2026-09-20T12:00:00Z"));

        Assert.True(newer.Accepted);
        Assert.Equal("Kept", (await _store.AllAsync()).Single().Title);
        var fed = (await _service.SummarizeAsync()).Single().Fed!;
        Assert.Equal("newnewnew", fed.Commit);
        Assert.Equal("person@machine-a", fed.Origin);
    }

    /// <summary>
    /// Re-feeding the same commit stays idempotent, as the feed always was — a tick that re-sent what
    /// it sent last time must not be answered as though the machine had gone backwards.
    /// </summary>
    [Fact]
    public async Task Re_feeding_the_same_commit_is_idempotent()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedAsync("Open", [Entry()], From());

        var again = await _service.FeedAsync("Open", [Entry()], From());

        Assert.True(again.Accepted);
        Assert.Single(await _store.AllAsync());
    }

    /// <summary>
    /// A retired repository takes its position in the history with it. A leftover row would refuse the
    /// first feed after it re-joined, as though this deployment still held a commit it had dropped.
    /// </summary>
    [Fact]
    public async Task Retiring_forgets_the_commit_this_deployment_held()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await _service.FeedAsync("Open", [Entry()], From("newnewnew", at: "2026-09-20T12:00:00Z"));

        await _service.RetireAsync("Open");
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        var afterRejoin = await _service.FeedAsync(
            "Open", [Entry("Fed again")], From("oldoldold", at: "2026-09-19T12:00:00Z"));

        Assert.True(afterRejoin.Accepted);
    }

    /// <summary>
    /// The declared line survives an ordinary re-registration. `daoris connect` says nothing about
    /// branches and runs on every tick; a null that overwrote would quietly reopen every branch.
    /// </summary>
    [Fact]
    public async Task An_ordinary_re_registration_preserves_the_declared_canonical_line()
    {
        await Register("Open", joined: true, shares: true, defaultBranch: "main");
        await Register("Open", joined: true, shares: true, defaultBranch: null);

        var outcome = await _service.FeedAsync("Open", [Entry()], From(branch: "feature/x"));

        Assert.Equal(FeedRefusal.NotDefaultBranch, outcome.Refusal);
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

    /// <summary>
    /// A conversation mirrors as a conversation (D49 §3): no quest, and the KIND travels. Without it
    /// every chat would arrive at a teammate's deployment looking like planned work — and demanding a
    /// quest would make it unmirrorable, so the repository would simply fall silent instead.
    /// </summary>
    [Fact]
    public async Task A_chat_mirrors_with_no_quest_and_says_it_was_a_chat()
    {
        var outcome = await _feed.FeedAsync(
            "alice-laptop",
            [Record(id: "c0ffee11", quest: null) with { Kind = "Chat" }]);

        Assert.Equal(SessionFeedRefusal.None, outcome.Refusal);
        var mirrored = Assert.Single(await _sessions.ListAsync(includeClosed: true));
        Assert.Equal(SessionKind.Chat, mirrored.Kind);
        Assert.Null(mirrored.Quest);
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
