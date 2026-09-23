using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A shared quest at the exchange (D68): every verb commits on this machine, whoever the receiver is,
/// and nothing fails because a remote is down; a take on a shared quest claims by push (D69). What may
/// leave is decided by the receiver (design §8); what a quest carries leaves as names, never bytes;
/// and a chain is all shared or all local.
/// </summary>
public sealed class QuestShareTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-share-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private KnowledgeService _service = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-24T10:00:00Z");

    public async Task InitializeAsync()
    {
        // One joined receiver and one local-only receiver: what may leave follows the receiver.
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
        Repo("Asker", """
            {
              "source": "s", "packs": [],
              "domain": { "summary": "Asks for things.", "owns": ["its own tree"], "accepts": [] }
            }
            """);

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(_root, DateTimeOffset.UtcNow);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (_remoteConnection is not null) await _remoteConnection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Repo(string name, string manifest)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), manifest);
    }

    /// <summary>
    /// The ordinary machine: its one circle wired to <paramref name="remote"/> — offline by default, and
    /// with null, a machine with no remote at all.
    /// </summary>
    private QuestExchange Exchange(IRemote? remote, QuestFiles? files = null) =>
        new(_service, _quests, new OneRemote(remote), files);

    private QuestExchange Exchange(QuestFiles? files = null) => Exchange(new UnreachableRemote(), files);

    /// <summary>A second store, standing in for the remote itself.</summary>
    private async Task<QuestStore> RemoteStoreAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        _remoteConnection = connection;
        return await QuestStore.OpenAsync(connection);
    }

    private SqliteConnection? _remoteConnection;

    /// <summary>
    /// 🔴 Nothing fails because a remote is down (D68 §1): a quest to a joined receiver is published HERE,
    /// and a take on it commits HERE — unconfirmed, and the answer says so and why. The sync carries both
    /// when the remote answers again.
    /// </summary>
    [Fact]
    public async Task A_shared_quest_is_published_and_taken_here_while_the_remote_is_down()
    {
        var published = await Exchange().PublishAsync("Asker", "Federated", "Do it", "why", Now);
        var taken = await Exchange().RespondAsync(published.Quest!.Id, "take", null, Now.AddHours(1));

        Assert.Equal(QuestPublishRefusal.None, published.Refusal);
        Assert.Equal(QuestRespondRefusal.None, taken.Refusal);
        Assert.Contains("UNCONFIRMED", taken.Message);
        Assert.Contains("could not be reached", taken.Message);
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync(published.Quest.Id))!.Status);
        Assert.Equal(QuestClaim.Unconfirmed, await _quests.ClaimAsync(published.Quest.Id));
        Assert.Equal(2, (await _quests.PendingAsync(Workspaces.Default, _ => true)).Count);
    }

    /// <summary>The claim by push (D69): a take on a shared quest waits for the remote, which numbers it.</summary>
    [Fact]
    public async Task A_take_on_a_shared_quest_claims_at_the_remote_before_it_answers()
    {
        var remote = await RemoteStoreAsync();
        var exchange = Exchange(new StoreRemote(remote));
        var published = await exchange.PublishAsync("Asker", "Federated", "Do it", "why", Now);

        var taken = await exchange.RespondAsync(published.Quest!.Id, "take", null, Now.AddHours(1));

        Assert.Equal(QuestRespondRefusal.None, taken.Refusal);
        Assert.Contains("the remote confirmed the claim", taken.Message);
        Assert.Equal(QuestStatus.Taken, (await remote.FindAsync(published.Quest.Id))!.Status);
        Assert.Equal(QuestClaim.Held, await _quests.ClaimAsync(published.Quest.Id));
        Assert.Empty(await _quests.PendingAsync(Workspaces.Default, _ => true));
    }

    /// <summary>
    /// 🔴 The online race, decided before any work (D69): another machine's take reached the remote
    /// first, so this take is rebased into a conflict and the session hears the stand-down it always
    /// has. The quest is the winner's, here too, and the loser's attempt is kept on it.
    /// </summary>
    [Fact]
    public async Task A_take_that_lost_at_the_remote_stands_down_and_is_kept_as_a_conflict()
    {
        var remote = await RemoteStoreAsync();
        var exchange = Exchange(new StoreRemote(remote));
        var published = await exchange.PublishAsync("Asker", "Federated", "Do it", "why", Now);
        await QuestSync.RunAsync(_quests, _service, new StoreRemote(remote), Workspaces.Default);
        await remote.MoveAsync(published.Quest!.Id, QuestStatus.Taken, "another machine's session", Now.AddMinutes(5));

        var taken = await exchange.RespondAsync(published.Quest.Id, "take", "this machine's session", Now.AddMinutes(6));

        Assert.Equal(QuestRespondRefusal.AlreadyTaken, taken.Refusal);
        Assert.Contains("taken on another machine first", taken.Message);
        Assert.Contains("Stand down", taken.Message);
        var here = (await _quests.FindAsync(published.Quest.Id))!;
        Assert.Equal("another machine's session", here.Note);
        Assert.Equal(_quests.Machine, Assert.Single(here.Conflicts).Machine);
        Assert.Equal(QuestClaim.Lost, await _quests.ClaimAsync(published.Quest.Id));
    }

    /// <summary>Silence means local: a take on a quest nobody shares is complete when it commits, and asks nothing.</summary>
    [Fact]
    public async Task A_take_on_a_quest_nobody_shares_never_asks_the_remote()
    {
        var remote = new StoreRemote(await RemoteStoreAsync());
        var exchange = Exchange(remote);
        var published = await exchange.PublishAsync("Asker", "Homebody", "Stay home", "why", Now);

        var taken = await exchange.RespondAsync(published.Quest!.Id, "take", null, Now.AddHours(1));

        Assert.Equal(QuestRespondRefusal.None, taken.Refusal);
        Assert.Equal($"Quest `#{published.Quest.Id}` is now Taken.", taken.Message);
        Assert.Equal(0, remote.Calls);
    }

    /// <summary>
    /// 🔴 The disclosure boundary for a quest's files (D65 §2, D47 §4): the bytes are kept on THIS
    /// machine under the quest's id, and what will travel — the pending publish — carries the name, the
    /// hash and the size, and has no field for anything more.
    /// </summary>
    [Fact]
    public async Task A_shared_quest_keeps_its_bytes_here_and_travels_with_names()
    {
        var home = _root + "-home";
        var upload = new QuestUpload("before.png", System.Text.Encoding.UTF8.GetBytes("pixels"));
        var described = QuestFiles.Describe(upload);
        var files = new QuestFiles(home);

        var outcome = await Exchange(files: files).PublishAsync(
            new QuestAsk("Asker", "Federated", "Do it", "why")
            {
                Links = ["https://tickets.example/T-9"],
                Uploads = [upload],
            },
            Now);

        var travelling = Assert.Single(await _quests.PendingAsync(Workspaces.Default, _ => true)).Published!;
        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
        Assert.Equal(described, Assert.Single(travelling.Attachments));
        Assert.Equal(["https://tickets.example/T-9"], travelling.Links);
        Assert.True(files.Has(outcome.Quest!.Id, described));

        Directory.Delete(home, recursive: true);
    }

    /// <summary>
    /// 🔴 All shared or all local (D65 §4, D68): each step is published on the machine that closes the
    /// one before it, and a shared quest may close on a teammate's machine, which cannot see a
    /// repository local to this one. A chain that straddles is refused when composed, naming both.
    /// </summary>
    [Theory]
    [InlineData("Federated", "Homebody")]
    [InlineData("Homebody", "Federated")]
    public async Task A_chain_that_straddles_shared_and_local_is_refused_when_composed(string first, string next)
    {
        var outcome = await Exchange().PublishAsync(
            new QuestAsk("Asker", first, "Develop", "why") { Then = [new QuestStep(next, "Verify", "b")] }, Now);

        Assert.Equal(QuestPublishRefusal.BadChain, outcome.Refusal);
        Assert.Contains(first, outcome.Message);
        Assert.Contains(next, outcome.Message);
        Assert.Empty(await _quests.ListAsync());
    }

    /// <summary>On a machine with no remote, nothing is shared — so the same chain is all local, and fit.</summary>
    [Fact]
    public async Task The_same_chain_is_fit_on_a_machine_with_no_remote()
    {
        var outcome = await Exchange(remote: null).PublishAsync(
            new QuestAsk("Asker", "Federated", "Develop", "why") { Then = [new QuestStep("Homebody", "Verify", "b")] }, Now);

        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
    }

    /// <summary>A shared chain travels whole: its publish carries every step, and whichever machine closes each publishes the next.</summary>
    [Fact]
    public async Task A_shared_chain_travels_whole()
    {
        await Exchange().PublishAsync(
            new QuestAsk("Asker", "Federated", "Do it", "why") { Then = [new QuestStep("Federated", "Verify {parent}", "b")] },
            Now);

        var travelling = Assert.Single(await _quests.PendingAsync(Workspaces.Default, _ => true)).Published!;
        Assert.Equal("Verify {parent}", Assert.Single(travelling.Then).Title);
    }

    /// <summary>
    /// A remote takes nobody's word for a publish (design §8): the receiver must be registered there, and
    /// what it carries must be what a record may name — the same judgement a publish at that door runs.
    /// </summary>
    [Fact]
    public async Task A_remote_judges_a_pushed_publish_as_its_own_door_would()
    {
        var registered = await _service.RegistryAsync(ct: default);
        Quest Asked(string to, IReadOnlyList<string>? links = null) =>
            new("abcdefabcdef", "Asker", to, "Do it", "why", QuestStatus.Open, null, Now, Now) { Links = links ?? [] };

        Assert.Null(Exchange().JudgeReceived(Asked("Federated", ["https://tickets.example/T-1"]), registered));
        Assert.Contains("not registered at this deployment", Exchange().JudgeReceived(Asked("Stranger"), registered));
        Assert.Contains("not a link a quest can carry", Exchange().JudgeReceived(Asked("Federated", ["javascript:alert(1)"]), registered));
    }
}
