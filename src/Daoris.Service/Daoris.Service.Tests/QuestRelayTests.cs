using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The write-through relay (D47 §5/§9): a verb on a remote-homed quest goes synchronously to the
/// remote's own judgement — the one lock — and the local mirror takes the result. An unreachable
/// remote is a plain refusal that changed nothing, never a queued verb: a lock that is eventually
/// consistent is not a lock. Behind the same QuestExchange both doors use, so neither can drift.
/// </summary>
public sealed class QuestRelayTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-relay-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private FakeRemote _remote = null!;
    private KnowledgeService _service = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T10:00:00Z");

    /// <summary>
    /// The machine's remotes, one per workspace (D48 §5) — the map a real deployment builds from
    /// the home's `remotes.json`, with fakes in place of HTTP clients.
    /// </summary>
    private sealed class FakeRoutes(params (string Workspace, IRemoteQuestClient Client)[] entries)
        : IRemoteQuestRoutes
    {
        public IRemoteQuestClient? For(string? workspace) => entries
            .Where(entry => Daoris.Knowledge.Workspaces.Same(entry.Workspace, workspace))
            .Select(entry => entry.Client)
            .FirstOrDefault();

        public IReadOnlyCollection<string> Workspaces => entries.Select(entry => entry.Workspace).ToList();
    }

    private sealed class FakeRemote : IRemoteQuestClient
    {
        public RemoteQuestAnswer NextAnswer { get; set; } = new(0, "unreachable", null);
        public List<string> Calls { get; } = [];

        /// <summary>What the last publish carried across — the names the remote was told.</summary>
        public (IReadOnlyList<string> Links, IReadOnlyList<QuestAttachment> Attachments) Carried { get; private set; }

        public Task<RemoteQuestAnswer> PublishAsync(
            string from, string to, string title, string body,
            IReadOnlyList<string> links, IReadOnlyList<QuestAttachment> attachments,
            CancellationToken ct = default)
        {
            Calls.Add($"publish {to}:{title}");
            Carried = (links, attachments);
            return Task.FromResult(NextAnswer);
        }

        public Task<RemoteQuestAnswer> RespondAsync(
            string id, string action, string? reason, CancellationToken ct = default)
        {
            Calls.Add($"{action} {id}");
            return Task.FromResult(NextAnswer);
        }
    }

    public async Task InitializeAsync()
    {
        // One joined receiver and one local-only receiver: home follows the receiver (D47 §5).
        Repo("Federated", """
            {
              "source": "s", "packs": [],
              "domain": { "summary": "Shared with the team.", "owns": ["its area"], "accepts": ["a quest"] },
              "remote": { "join": true, "knowledge": false }
            }
            """);
        Repo("Homebody", """
            {
              "source": "s", "packs": [],
              "domain": { "summary": "Stays local.", "owns": ["itself"], "accepts": ["a quest"] }
            }
            """);
        // The asker is a member too — a sender's registry row is what names the circle a quest is
        // published INTO (D48 §4), and therefore which remote the relay reaches (§5).
        Repo("Asker", """
            {
              "source": "s", "packs": [],
              "domain": { "summary": "Asks for things.", "owns": ["its own tree"], "accepts": [] }
            }
            """);

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _remote = new FakeRemote();

        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        // The registry is an explicit list (D48 §3); the manifests above are imported into it.
        await _service.ImportAsync(_root, DateTimeOffset.UtcNow);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Repo(string name, string manifest)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), manifest);
    }

    /// <summary>The ordinary machine: one circle, one remote — the shape before workspaces existed.</summary>
    private QuestExchange Exchange(bool withRemote = true) =>
        new(_service, _quests, withRemote ? new FakeRoutes((Workspaces.Default, _remote)) : null);

    /// <summary>Re-wire a registered repository to another circle — `connect --workspace`, from here.</summary>
    private async Task WireAsync(string repository, string workspace)
    {
        var row = (await _service.RegistryAsync(ct: default))
            .First(r => string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase));
        await _service.RegisterAsync(row with { Workspace = workspace }, Now);
    }

    private static Quest RemoteQuest(
        QuestStatus status = QuestStatus.Open, string workspace = Workspaces.Default) => new(
        "abc123", "Asker", "Federated", "Do it", "why", status, null, Now, Now, Home: "remote",
        Workspace: workspace);

    [Fact]
    public async Task Publishing_to_a_joined_receiver_writes_through_and_mirrors()
    {
        _remote.NextAnswer = new(200, "Published quest `#abc123` to `Federated` — Open.", RemoteQuest());

        var outcome = await Exchange().PublishAsync("Asker", "Federated", "Do it", "why", Now);

        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
        Assert.Contains("publish Federated:Do it", _remote.Calls);
        var mirrored = (await _quests.FindAsync("abc123"))!;
        Assert.Equal("remote", mirrored.Home);
    }

    /// <summary>
    /// 🔴 The disclosure boundary for a quest's files (D65 §2, D47 §4): the remote is told the links and
    /// the NAMES, the mirror carries what the remote answered, and the bytes are kept on THIS machine
    /// under the quest's id — the home decided the id, and the file follows the record, never the wire.
    /// </summary>
    [Fact]
    public async Task A_joined_receivers_quest_tells_the_remote_names_and_keeps_the_bytes_here()
    {
        var home = _root + "-home";
        var upload = new QuestUpload("before.png", System.Text.Encoding.UTF8.GetBytes("pixels"));
        var described = QuestFiles.Describe(upload);
        _remote.NextAnswer = new(200, "Published quest `#abc123` to `Federated` — Open.", RemoteQuest() with
        {
            Links = ["https://tickets.example/T-9"],
            Attachments = [described],
        });

        var files = new QuestFiles(home);
        var outcome = await new QuestExchange(
                _service, _quests, new FakeRoutes((Workspaces.Default, _remote)), files)
            .PublishAsync(
                new QuestAsk("Asker", "Federated", "Do it", "why")
                {
                    Links = ["https://tickets.example/T-9"],
                    Uploads = [upload],
                },
                Now);

        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
        Assert.Equal(["https://tickets.example/T-9"], _remote.Carried.Links);
        Assert.Equal(described, Assert.Single(_remote.Carried.Attachments));

        var mirrored = (await _quests.FindAsync("abc123"))!;
        Assert.Equal("before.png", Assert.Single(mirrored.Attachments).Name);
        Assert.True(files.Has("abc123", described));

        Directory.Delete(home, recursive: true);
    }

    /// <summary>A local-only receiver's quests stay entirely local — the remote never hears of them.</summary>
    [Fact]
    public async Task Publishing_to_a_local_receiver_never_touches_the_remote()
    {
        var outcome = await Exchange().PublishAsync("Asker", "Homebody", "Do it", "why", Now);

        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
        Assert.Empty(_remote.Calls);
        Assert.Null(outcome.Quest!.Home);
    }

    [Fact]
    public async Task Responding_to_a_mirrored_quest_writes_through_and_takes_the_result()
    {
        await _quests.MirrorAsync(RemoteQuest());
        _remote.NextAnswer = new(200, "Quest `#abc123` is now Taken.", RemoteQuest(QuestStatus.Taken));

        var outcome = await Exchange().RespondAsync("abc123", "take", null, Now);

        Assert.Equal(QuestRespondRefusal.None, outcome.Refusal);
        Assert.Contains("take abc123", _remote.Calls);
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync("abc123"))!.Status);
    }

    /// <summary>The cross-machine race arrives here as the remote's own refusal, passed on verbatim.</summary>
    [Fact]
    public async Task A_remote_conflict_on_take_reads_as_already_taken()
    {
        await _quests.MirrorAsync(RemoteQuest());
        _remote.NextAnswer = new(409, "Quest `#abc123` is already taken — someone is working it. Stand down rather than doubling the work.", null);

        var outcome = await Exchange().RespondAsync("abc123", "take", null, Now);

        Assert.Equal(QuestRespondRefusal.AlreadyTaken, outcome.Refusal);
        Assert.Contains("already taken", outcome.Message);
    }

    /// <summary>
    /// Rule 2 of D47 §2: transitions write through or fail plainly — never queue. An unreachable
    /// remote changes nothing, says so, and the quest stays exactly as the last mirror left it.
    /// </summary>
    [Fact]
    public async Task An_unreachable_remote_is_a_plain_refusal_that_changed_nothing()
    {
        await _quests.MirrorAsync(RemoteQuest());
        _remote.NextAnswer = new(0, "connection refused", null);

        var outcome = await Exchange().RespondAsync("abc123", "take", null, Now);

        Assert.Equal(QuestRespondRefusal.HomeUnreachable, outcome.Refusal);
        Assert.Contains("nothing was changed", outcome.Message);
        Assert.Equal(QuestStatus.Open, (await _quests.FindAsync("abc123"))!.Status);
    }

    /// <summary>A mirror without a configured remote is the same refusal — the home decides, or nobody does.</summary>
    [Fact]
    public async Task A_mirrored_quest_with_no_remote_configured_refuses_every_verb()
    {
        await _quests.MirrorAsync(RemoteQuest());

        var outcome = await Exchange(withRemote: false).RespondAsync("abc123", "take", null, Now);

        Assert.Equal(QuestRespondRefusal.HomeUnreachable, outcome.Refusal);
        Assert.Equal(QuestStatus.Open, (await _quests.FindAsync("abc123"))!.Status);
    }

    [Fact]
    public async Task Publishing_to_a_joined_receiver_while_unreachable_publishes_nothing()
    {
        _remote.NextAnswer = new(0, "connection refused", null);

        var outcome = await Exchange().PublishAsync("Asker", "Federated", "Do it", "why", Now);

        Assert.Equal(QuestPublishRefusal.HomeUnreachable, outcome.Refusal);
        Assert.Null(await _quests.FindAsync("abc123"));
    }

    // ——— Which remote (D48 §5). A machine holding two circles holds two deployments' keys, and the
    // quest's own workspace is what decides which one hears a verb. Sending to the wrong one is not a
    // failed call: it is a lock taken at a deployment that had no business holding it.

    [Fact]
    public async Task A_quest_reaches_the_remote_serving_its_own_workspace()
    {
        var tools = new FakeRemote();
        await WireAsync("Asker", "aurora");
        await WireAsync("Federated", "aurora");
        _remote.NextAnswer = new(200, "Published quest `#abc123` to `Federated` — Open.", RemoteQuest());

        var outcome = await new QuestExchange(
                _service, _quests, new FakeRoutes(("aurora", _remote), ("tools", tools)))
            .PublishAsync("Asker", "Federated", "Do it", "why", Now);

        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
        Assert.Contains("publish Federated:Do it", _remote.Calls);
        Assert.Empty(tools.Calls);
        Assert.Equal("aurora", outcome.Quest!.Workspace);
    }

    /// <summary>
    /// A joined repository in a circle this machine has no remote for stays entirely local — absence
    /// is the default and it is silent (D21). The declaration says MAY; the machine says WHERE, and
    /// silence there is a workspace that syncs nowhere, not an error.
    /// </summary>
    [Fact]
    public async Task A_joined_receiver_in_an_unwired_circle_publishes_locally()
    {
        await WireAsync("Asker", "tools");
        await WireAsync("Federated", "tools");

        var outcome = await new QuestExchange(_service, _quests, new FakeRoutes(("aurora", _remote)))
            .PublishAsync("Asker", "Federated", "Do it", "why", Now);

        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
        Assert.Empty(_remote.Calls);
        Assert.Null(outcome.Quest!.Home);
        Assert.Equal("tools", outcome.Quest.Workspace);
    }

    [Fact]
    public async Task A_verb_resolves_its_remote_by_the_quests_own_workspace()
    {
        var tools = new FakeRemote();
        await _quests.MirrorAsync(RemoteQuest(workspace: "aurora"));
        _remote.NextAnswer = new(200, "Quest `#abc123` is now Taken.", RemoteQuest(QuestStatus.Taken));

        var outcome = await new QuestExchange(
                _service, _quests, new FakeRoutes(("aurora", _remote), ("tools", tools)))
            .RespondAsync("abc123", "take", null, Now);

        Assert.Equal(QuestRespondRefusal.None, outcome.Refusal);
        Assert.Contains("take abc123", _remote.Calls);
        Assert.Empty(tools.Calls);
    }

    /// <summary>A circle with no remote cannot answer for its quests — plainly, and changing nothing.</summary>
    [Fact]
    public async Task A_mirrored_quest_whose_circle_has_no_remote_refuses_naming_the_circle()
    {
        await _quests.MirrorAsync(RemoteQuest(workspace: "tools"));

        var outcome = await new QuestExchange(_service, _quests, new FakeRoutes(("aurora", _remote)))
            .RespondAsync("abc123", "take", null, Now);

        Assert.Equal(QuestRespondRefusal.HomeUnreachable, outcome.Refusal);
        Assert.Contains("tools", outcome.Message);
        Assert.Empty(_remote.Calls);
        Assert.Equal(QuestStatus.Open, (await _quests.FindAsync("abc123"))!.Status);
    }

    /// <summary>
    /// A quest this machine has never mirrored names no workspace. With one remote that is no
    /// ambiguity — it is tried, as it always was, because the mirror may simply be behind. With
    /// several, guessing would post a `take` at a deployment that never held the quest, so the answer
    /// is a refusal that says why and names the circles.
    /// </summary>
    [Fact]
    public async Task An_unmirrored_quest_is_not_guessed_at_across_several_circles()
    {
        var tools = new FakeRemote();

        var outcome = await new QuestExchange(
                _service, _quests, new FakeRoutes(("aurora", _remote), ("tools", tools)))
            .RespondAsync("zzz999", "take", null, Now);

        Assert.Equal(QuestRespondRefusal.HomeUnreachable, outcome.Refusal);
        Assert.Contains("aurora", outcome.Message);
        Assert.Contains("tools", outcome.Message);
        Assert.Empty(_remote.Calls);
        Assert.Empty(tools.Calls);
    }
}
