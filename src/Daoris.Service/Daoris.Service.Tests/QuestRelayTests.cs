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

    private sealed class NoSource : IKnowledgeSource
    {
        public string Name => "test";
        public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeEntry>>([]);
    }

    private sealed class FakeRemote : IRemoteQuestClient
    {
        public RemoteQuestAnswer NextAnswer { get; set; } = new(0, "unreachable", null);
        public List<string> Calls { get; } = [];

        public Task<RemoteQuestAnswer> PublishAsync(
            string from, string to, string title, string body, CancellationToken ct = default)
        {
            Calls.Add($"publish {to}:{title}");
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

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _remote = new FakeRemote();

        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new NoSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry(_root));
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

    private QuestExchange Exchange(bool withRemote = true) =>
        new(_service, _quests, withRemote ? _remote : null);

    private static Quest RemoteQuest(QuestStatus status = QuestStatus.Open) => new(
        "abc123", "Asker", "Federated", "Do it", "why", status, null, Now, Now, Home: "remote");

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
}
