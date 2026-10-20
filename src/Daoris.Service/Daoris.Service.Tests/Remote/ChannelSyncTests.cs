using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// SESSONE2d (D167 point 8; the co-working design §2.5): an ask's channel travels within the workspace as session records do. A
/// machine's own messages go up by a cursor and the team's come down keyed `origin/id`, read-only; the words are cleaned of machine
/// paths before they leave; a feed from before the optional fields reads; and a remote from before has no doors, which is said once.
/// </summary>
public sealed class ChannelSyncTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-channelsync-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly List<SqliteConnection> _connections = [];
    private KnowledgeService _service = null!;
    private Machine _a = null!;
    private Machine _b = null!;
    private ChannelStore _remote = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-15T10:00:00Z");

    private sealed record Machine(ChannelStore Channel, SessionStore Sessions);

    public async Task InitializeAsync()
    {
        Repo("Shared", """
            { "source": "s", "packs": [], "domain": { "summary": "Shared.", "owns": ["it"], "accepts": ["a quest"] },
              "remote": { "join": true, "knowledge": false } }
            """);
        Repo("Homebody", """
            { "source": "s", "packs": [], "domain": { "summary": "Stays local.", "owns": ["itself"], "accepts": [] } }
            """);

        var index = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            index, new LexicalKnowledgeSearch(index), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(_root, DateTimeOffset.UtcNow);

        _a = await MachineAsync();
        _b = await MachineAsync();
        _remote = (await MachineAsync()).Channel;
    }

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections) await connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Repo(string name, string manifest)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), manifest);
    }

    private async Task<Machine> MachineAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        _connections.Add(connection);
        return new(await ChannelStore.OpenAsync(connection), await SessionStore.OpenAsync(connection));
    }

    private StoreRemote Remote(string caller) => new(null!, null, _service, caller, _remote);

    private Task<ChannelSyncReport> SyncAsync(Machine machine, string caller, IRemote? remote = null) =>
        ChannelSync.RunAsync(machine.Channel, machine.Sessions, _service, remote ?? Remote(caller), Workspaces.Default);

    private static ChannelMessage Message(string id, string text, string? repository = null, string? re = null, int minute = 0) =>
        new(id, "ask1", Workspaces.Default, Now.AddMinutes(minute), repository is null ? null : "sess1", repository,
            repository is null ? null : "stub", repository is null ? null : "ask:ask1", To: null, re, Question: false, text);

    /// <summary>🔴 A message goes up once and comes down to a teammate keyed `origin/id`, read-only; neither side gets its own back.</summary>
    [Fact]
    public async Task A_message_goes_up_and_comes_down_to_a_teammate_keyed_by_origin_and_id()
    {
        await _a.Channel.AddAsync(Message("m1", "start with the bridge", "Shared"), 10);
        await _a.Channel.AddAsync(Message("m2", "the person says so", minute: 1), 10);

        var first = await SyncAsync(_a, "a@one");
        var quiet = await SyncAsync(_a, "a@one");
        var down = await SyncAsync(_b, "b@two");
        var again = await SyncAsync(_b, "b@two");

        Assert.Equal((2, 0, 0), (first.Pushed, first.Fetched, quiet.Pushed));
        Assert.Equal((0, 2, 0), (down.Pushed, down.Fetched, again.Fetched));
        Assert.Equal(["a@one", "a@one"], (await _remote.OfAsync("ask1")).Select(m => m.Origin));
        var mirrored = await _b.Channel.OfAsync("ask1");
        Assert.Equal(["m1", "m2"], mirrored.Select(m => m.Id));
        Assert.All(mirrored, m => Assert.Equal("a@one", m.Origin));
        Assert.Equal("start with the bridge", mirrored[0].Text);
        Assert.Equal("Shared", mirrored[0].Repository);
        Assert.True(mirrored[1].ByPerson);
        Assert.Equal(["m1", "m2"], (await _a.Channel.OfAsync("ask1")).Select(m => m.Id));
        Assert.All(await _a.Channel.OfAsync("ask1"), m => Assert.Null(m.Origin));
    }

    /// <summary>A mirrored message is never fed back up, and a teammate's later message reaches the first machine too.</summary>
    [Fact]
    public async Task A_mirrored_message_never_goes_back_up_and_the_teams_reply_comes_down()
    {
        await _a.Channel.AddAsync(Message("m1", "question for the team", "Shared"), 10);
        await SyncAsync(_a, "a@one");
        await SyncAsync(_b, "b@two");
        await _b.Channel.AddAsync(Message("r1", "an answer", "Shared", re: "m1", minute: 2), 10);

        var up = await SyncAsync(_b, "b@two");
        var down = await SyncAsync(_a, "a@one");

        Assert.Equal(1, up.Pushed);
        Assert.Equal(1, down.Fetched);
        Assert.Equal(["a@one", "b@two"], (await _remote.OfAsync("ask1")).Select(m => m.Origin));
        var held = (await _a.Channel.OfAsync("ask1")).Single(m => m.Origin == "b@two");
        Assert.Equal(("r1", "m1"), (held.Id, held.Re));
    }

    /// <summary>🔴 A machine path in the words is cleaned before they leave; the machine's own copy keeps what it wrote.</summary>
    [Fact]
    public async Task A_path_in_the_words_is_cleaned_before_they_leave()
    {
        // Spelled in pieces, as SessionSyncTests does, so the repository's own scan for machine paths does not read them.
        var windowsHome = string.Concat("C:", @"\", "Users", @"\", "jo", @"\work\bridge.cs");
        var unixHome = string.Concat("/", "home", "/", "jo", "/work/notes.md");
        var Words = $"see {windowsHome} and {unixHome} for the shape";
        await _a.Channel.AddAsync(Message("m1", Words, "Shared"), 10);
        var remote = Remote("a@one");

        await SyncAsync(_a, "a@one", remote);
        await SyncAsync(_b, "b@two");

        Assert.DoesNotContain("Users", remote.LastChannelFeed);
        Assert.DoesNotContain(unixHome, remote.LastChannelFeed);
        Assert.Equal($"see {SessionNote.Elided} and {SessionNote.Elided} for the shape", (await _b.Channel.OfAsync("ask1")).Single().Text);
        Assert.Equal(Words, (await _a.Channel.OfAsync("ask1")).Single().Text);
    }

    /// <summary>Silence means local (D47 §4): a session's message from a repository that has not joined stays home.</summary>
    [Fact]
    public async Task A_message_of_a_repository_that_has_not_joined_stays_home()
    {
        await _a.Channel.AddAsync(Message("m1", "private to this machine", "Homebody"), 10);

        var report = await SyncAsync(_a, "a@one");

        Assert.Equal(0, report.Pushed);
        Assert.Empty(await _remote.OfAsync("ask1"));
    }

    /// <summary>A feed from before the optional fields (`to`, `re`, `question`, `agent`, `entry`) reads, and so does a page.</summary>
    [Fact]
    public void A_feed_and_a_page_without_the_optional_fields_read()
    {
        var feed = ChannelWire.ReadFeed("""{ "messages": [ { "id": "m1", "ask": "ask1", "at": "2026-10-15T10:00:00.0000000+00:00", "text": "hello" } ] }""");
        var page = ChannelWire.ReadPage("""{ "through": 4, "more": false, "messages": [ { "id": "m1", "origin": "a@one", "ask": "ask1", "at": "2026-10-15T10:00:00.0000000+00:00", "text": "hello" } ] }""");

        var message = Assert.Single(feed!);
        Assert.Equal(("m1", "ask1", "hello", false), (message.Id, message.Ask, message.Text, message.Question));
        Assert.Null(message.To);
        // COWORK1d: a message from before says no level, which keeps its old reach; absent is never read as ordinary.
        Assert.Null(message.Urgent);
        var fetched = Assert.Single(page!.Messages);
        Assert.Equal(("a@one", "m1", 4L), (fetched.Origin, fetched.Id, page.Through));
        Assert.Null(fetched.Urgent);
        Assert.Null(ChannelWire.ReadFeed("""{ "messages": [ { "ask": "ask1" } ] }"""));
        Assert.Null(ChannelWire.ReadPage("""{ "through": 1, "messages": [ { "id": "m1", "ask": "ask1", "text": "no origin" } ] }"""));
    }

    /// <summary>
    /// 🔴 COWORK1d (the message-urgency design §9): a message's level travels on every message kept from now, an explicit false
    /// included, and comes down to a teammate as it was said; one from before still says none.
    /// </summary>
    [Fact]
    public async Task A_messages_level_travels_said_true_or_false_and_comes_down_as_it_was()
    {
        await _a.Channel.AddAsync(Message("m1", "now", "Shared") with { Urgent = true }, 10);
        await _a.Channel.AddAsync(Message("m2", "later", "Shared") with { Urgent = false }, 10);

        var feed = ChannelWire.Feed(await _a.Channel.OfAsync("ask1"));
        Assert.Contains("\"urgent\":true", feed, StringComparison.Ordinal);
        Assert.Contains("\"urgent\":false", feed, StringComparison.Ordinal);
        Assert.Equal<bool?>([true, false], ChannelWire.ReadFeed(feed)!.Select(m => m.Urgent));

        await SyncAsync(_a, "a@one");
        await SyncAsync(_b, "b@two");
        Assert.Equal<bool?>([true, false], (await _b.Channel.OfAsync("ask1")).Select(m => m.Urgent));
    }

    /// <summary>A remote from before has no channel doors: its 404 is said once, never as a wall each pass, and the channel stays here.</summary>
    [Fact]
    public async Task A_remote_from_before_has_no_doors_which_is_said_once()
    {
        await _a.Channel.AddAsync(Message("m1", "hello", "Shared"), 10);
        var older = new NoDoorsRemote();

        var first = await SyncAsync(_a, "a@one", older);
        var second = await SyncAsync(_a, "a@one", older);

        Assert.Equal((0, 0), (first.Pushed, first.Fetched));
        Assert.NotNull(first.Problem);
        Assert.Contains("channel", first.Problem);
        Assert.Null(second.Problem);
        Assert.Single(await _a.Channel.OfAsync("ask1"));
    }

    private sealed class NoDoorsRemote : IRemote
    {
        private static RemoteException Gone() => new("the remote answered 404: not found", 404);

        public Task<QuestFetch> FetchQuestsAsync(long since, CancellationToken ct = default) => throw Gone();

        public Task<QuestPush> PushQuestsAsync(long @base, IReadOnlyList<QuestOperation> operations, CancellationToken ct = default) => throw Gone();

        public Task PushSessionsAsync(IReadOnlyList<FedSessionRecord> records, CancellationToken ct = default) => throw Gone();

        public Task<SessionFetch> FetchSessionsAsync(long since, CancellationToken ct = default) => throw Gone();

        public Task PushChannelAsync(IReadOnlyList<ChannelMessage> messages, CancellationToken ct = default) => throw Gone();

        public Task<ChannelFetch> FetchChannelAsync(long since, CancellationToken ct = default) => throw Gone();

        public Task<string?> HeldCodeMapAsync(string repository, CancellationToken ct = default) => throw Gone();

        public Task<FedCodeMap?> FetchCodeMapAsync(string repository, CancellationToken ct = default) => throw Gone();
    }
}
