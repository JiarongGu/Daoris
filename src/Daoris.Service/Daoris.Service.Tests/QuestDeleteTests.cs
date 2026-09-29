using System.Text;
using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// Deleting a quest made by mistake (QUEST1, D95), at the exchange: only a quest nobody has started on
/// goes, every refusal names what to do instead, a quest that never left the machine simply goes, and
/// a shared one is deleted by push — confirmed, lost to a take, or unconfirmed while the remote is away.
/// </summary>
public sealed class QuestDeleteTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-delete-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private KnowledgeService _service = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T10:00:00Z");

    public async Task InitializeAsync()
    {
        // One joined receiver and one local-only receiver: whether a delete travels follows the receiver.
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
              "domain": { "summary": "Asks for things.", "owns": ["its own tree"], "accepts": ["a question"] }
            }
            """);

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);

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
        if (Directory.Exists(_root + "-home")) Directory.Delete(_root + "-home", recursive: true);
    }

    private void Repo(string name, string manifest)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), manifest);
    }

    /// <summary>A machine whose circle is wired to <paramref name="remote"/> — null is a machine with no remote.</summary>
    private QuestExchange Exchange(IRemote? remote = null, QuestFiles? files = null) =>
        new(_service, _quests, remote is null ? null : new OneRemote(remote), files, _sessions);

    private SqliteConnection? _remoteConnection;

    private async Task<QuestStore> RemoteStoreAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        _remoteConnection = connection;
        return await QuestStore.OpenAsync(connection);
    }

    private static async Task<Quest> Publish(QuestExchange exchange, string to = "Homebody", string title = "A test quest") =>
        (await exchange.PublishAsync("Asker", to, title, "Published by mistake.", Now)).Quest!;

    /// <summary>
    /// A quest nobody has taken, to a receiver nobody shares, simply goes: its record and its history,
    /// with the answer saying nothing else holds a copy.
    /// </summary>
    [Fact]
    public async Task An_untaken_quest_that_never_left_the_machine_simply_goes()
    {
        var exchange = Exchange();
        var quest = await Publish(exchange);

        var deleted = await exchange.DeleteAsync(quest.Id, Now.AddMinutes(1));

        Assert.Equal(QuestDeleteRefusal.None, deleted.Refusal);
        Assert.Contains($"Deleted quest `#{quest.Id}`", deleted.Message);
        Assert.Contains("never left this machine", deleted.Message);
        Assert.Null(await _quests.FindAsync(quest.Id));
        Assert.Empty(await _quests.HistoryAsync(quest.Id));
    }

    /// <summary>
    /// 🔴 A taken quest is somebody's work in a tree: it stays, and the refusal says what to do instead —
    /// decline it, with the reason, so the asker hears why.
    /// </summary>
    [Fact]
    public async Task A_taken_quest_stays_and_the_refusal_says_to_decline_it()
    {
        var exchange = Exchange();
        var quest = await Publish(exchange);
        await exchange.RespondAsync(quest.Id, "take", null, Now.AddMinutes(1));

        var refused = await exchange.DeleteAsync(quest.Id, Now.AddMinutes(2));

        Assert.Equal(QuestDeleteRefusal.Kept, refused.Refusal);
        Assert.Contains("is Taken", refused.Message);
        Assert.Contains("Decline it", refused.Message);
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync(quest.Id))!.Status);
    }

    /// <summary>A done quest is the record of work, and a declined one the trace of a decision: both stay, and already leave the list.</summary>
    [Theory]
    [InlineData("done", "is Done")]
    [InlineData("decline", "is Declined")]
    public async Task A_closed_quest_keeps_its_record_and_the_refusal_says_it_already_leaves_the_list(string action, string said)
    {
        var exchange = Exchange();
        var quest = await Publish(exchange);
        await exchange.RespondAsync(quest.Id, action, "An answer.", Now.AddMinutes(1));

        var refused = await exchange.DeleteAsync(quest.Id, Now.AddMinutes(2));

        Assert.Equal(QuestDeleteRefusal.Kept, refused.Refusal);
        Assert.Contains(said, refused.Message);
        Assert.Contains("leaves the list", refused.Message);
        Assert.NotNull(await _quests.FindAsync(quest.Id));
    }

    /// <summary>
    /// An open quest a session was started for keeps its record: the session's record names it, and a
    /// record naming a quest that is gone would lead nowhere. Decline it instead.
    /// </summary>
    [Fact]
    public async Task An_open_quest_a_session_was_started_for_keeps_its_record()
    {
        var exchange = Exchange();
        var quest = await Publish(exchange);
        var session = await _sessions.CreateAsync(quest.Id, "Homebody", "stub", Now.AddMinutes(1));

        var refused = await exchange.DeleteAsync(quest.Id, Now.AddMinutes(2));

        Assert.Equal(QuestDeleteRefusal.Kept, refused.Refusal);
        Assert.Contains($"session `{session.Id}`", refused.Message);
        Assert.Contains("Decline it", refused.Message);
        Assert.NotNull(await _quests.FindAsync(quest.Id));
    }

    /// <summary>
    /// The question a taken quest waits on (D79) keeps its record: deleted, the waiting quest would wait
    /// on nothing for good. Declined, its taker is resumed with the reason.
    /// </summary>
    [Fact]
    public async Task The_question_a_taken_quest_waits_on_keeps_its_record()
    {
        var exchange = Exchange();
        var work = await Publish(exchange, "Federated", "The work");
        await exchange.RespondAsync(work.Id, "take", null, Now.AddMinutes(1));
        var question = (await exchange.PublishAsync("Federated", "Asker", "Which one?", "why", Now.AddMinutes(2))).Quest!;
        await exchange.RespondAsync(work.Id, "wait", null, Now.AddMinutes(3), on: question.Id);

        var refused = await exchange.DeleteAsync(question.Id, Now.AddMinutes(4));

        Assert.Equal(QuestDeleteRefusal.Kept, refused.Refusal);
        Assert.Contains($"`#{work.Id}`", refused.Message);
        Assert.Contains("waits on", refused.Message);
        Assert.NotNull(await _quests.FindAsync(question.Id));
    }

    [Fact]
    public async Task Deleting_a_quest_nobody_holds_is_refused_as_not_found()
    {
        var refused = await Exchange().DeleteAsync("#nosuchquest", Now);

        Assert.Equal(QuestDeleteRefusal.NotFound, refused.Refusal);
        Assert.Contains("No quest `#nosuchquest`", refused.Message);
    }

    /// <summary>What the page is told may go — the same judgement the delete runs, so no page re-derives it.</summary>
    [Fact]
    public async Task The_exchange_says_which_quests_may_be_deleted()
    {
        var exchange = Exchange();
        var free = await Publish(exchange, title: "Free to go");
        var taken = await Publish(exchange, title: "Being worked");
        var started = await Publish(exchange, title: "A session started");
        await exchange.RespondAsync(taken.Id, "take", null, Now.AddMinutes(1));
        await _sessions.CreateAsync(started.Id, "Homebody", "stub", Now.AddMinutes(1));

        var deletable = await exchange.DeletableAsync(await _quests.ListAsync(includeClosed: true));

        Assert.Equal([free.Id], deletable);
    }

    /// <summary>A deleted quest's files on this machine go with it — the person asked for it gone.</summary>
    [Fact]
    public async Task A_deleted_quest_takes_the_files_this_machine_kept_for_it()
    {
        var files = new QuestFiles(_root + "-home");
        var exchange = Exchange(files: files);
        var quest = (await exchange.PublishAsync(
            new QuestAsk("Asker", "Homebody", "With a file", "why")
            {
                Uploads = [new QuestUpload("trace.log", Encoding.UTF8.GetBytes("stack"))],
            },
            Now)).Quest!;
        Assert.True(files.Has(quest.Id, Assert.Single(quest.Attachments)));

        await exchange.DeleteAsync(quest.Id, Now.AddMinutes(1));

        Assert.False(Directory.Exists(Path.GetDirectoryName(files.DirectoryOf(quest.Id))));
    }

    // ——— A shared quest: deleted by push, as a take claims by push (D69).

    /// <summary>A delete on a shared quest waits for the remote, which keeps the tombstone — confirmed, and said so.</summary>
    [Fact]
    public async Task A_shared_quest_is_deleted_at_the_remote_before_the_answer_returns()
    {
        var remote = await RemoteStoreAsync();
        var exchange = Exchange(new StoreRemote(remote));
        var quest = await Publish(exchange, "Federated");
        await QuestSync.RunAsync(_quests, _service, new StoreRemote(remote), Workspaces.Default);

        var deleted = await exchange.DeleteAsync(quest.Id, Now.AddMinutes(1));

        Assert.Equal(QuestDeleteRefusal.None, deleted.Refusal);
        Assert.Contains("the remote confirmed", deleted.Message);
        Assert.Null(await remote.FindAsync(quest.Id));
        Assert.Null(await _quests.FindAsync(quest.Id));
        Assert.Empty(await _quests.PendingAsync(Workspaces.Default, _ => true));
    }

    /// <summary>
    /// Offline, the delete commits here and is UNCONFIRMED: it travels on the next pass, and the answer
    /// says the quest comes back taken if another machine took it first.
    /// </summary>
    [Fact]
    public async Task A_shared_quest_deleted_while_the_remote_is_away_is_unconfirmed_and_travels_later()
    {
        var exchange = Exchange(new UnreachableRemote());
        var quest = await Publish(exchange, "Federated");

        var deleted = await exchange.DeleteAsync(quest.Id, Now.AddMinutes(1));

        Assert.Equal(QuestDeleteRefusal.None, deleted.Refusal);
        Assert.Contains("UNCONFIRMED", deleted.Message);
        Assert.Contains("could not be reached", deleted.Message);
        Assert.Null(await _quests.FindAsync(quest.Id));
        Assert.Contains(await _quests.PendingAsync(Workspaces.Default, _ => true), o => o.Kind == QuestOperationKind.Deleted);
    }

    /// <summary>
    /// 🔴 The online race: another machine's take reached the remote first, so the delete lost and was
    /// dropped. The quest stands taken here too, and the answer says so, and what to do instead.
    /// </summary>
    [Fact]
    public async Task A_shared_delete_that_lost_to_a_take_says_so_and_the_quest_stays_taken()
    {
        var remote = await RemoteStoreAsync();
        var exchange = Exchange(new StoreRemote(remote));
        var quest = await Publish(exchange, "Federated");
        await QuestSync.RunAsync(_quests, _service, new StoreRemote(remote), Workspaces.Default);
        await remote.MoveAsync(quest.Id, QuestStatus.Taken, "another machine's session", Now.AddMinutes(5));

        var deleted = await exchange.DeleteAsync(quest.Id, Now.AddMinutes(6));

        Assert.Equal(QuestDeleteRefusal.TakenElsewhere, deleted.Refusal);
        Assert.Contains("taken on another machine first", deleted.Message);
        Assert.Contains("Decline it", deleted.Message);
        var here = (await _quests.FindAsync(quest.Id))!;
        Assert.Equal(QuestStatus.Taken, here.Status);
        Assert.Empty(here.Conflicts);
    }

    // ——— An ask (D95): deleted with the quests asked by it, or not at all.

    private async Task<(AskDesk Desk, QuestExchange Exchange, QuestFiles Files)> DeskAsync()
    {
        var files = new QuestFiles(_root + "-home");
        var exchange = Exchange(files: files);
        return (new AskDesk(_service, await AskStore.OpenAsync(_connection), exchange, files), exchange, files);
    }

    /// <summary>An ask none of whose quests was taken goes, and every quest asked by it goes with it.</summary>
    [Fact]
    public async Task An_ask_whose_quests_nobody_took_is_deleted_with_them()
    {
        var (desk, _, _) = await DeskAsync();
        var asked = (await desk.AskAsync(new AskRequest(Workspaces.Default, "A test ask") { To = "Homebody" }, Now)).Ask!;
        var second = (await desk.PublishAsync(asked.Id, "Federated", Now.AddMinutes(1))).Quest!;

        var deleted = await desk.DeleteAsync(asked.Id, Now.AddMinutes(2));

        Assert.Equal(AskRefusal.None, deleted.Refusal);
        Assert.Contains($"Deleted ask `#{asked.Id}`", deleted.Message);
        Assert.Contains($"`#{second.Id}`", deleted.Message);
        Assert.Null(await desk.FindAsync(asked.Id));
        Assert.Null(await _quests.FindAsync(asked.Quests[0]));
        Assert.Null(await _quests.FindAsync(second.Id));
        Assert.Empty(await desk.ListAsync(includeClosed: true));
    }

    /// <summary>An ask that became nothing goes alone, and the files it kept on this machine go with it.</summary>
    [Fact]
    public async Task An_ask_that_became_nothing_is_deleted_with_its_files()
    {
        var (desk, _, files) = await DeskAsync();
        var asked = (await desk.AskAsync(
            new AskRequest(Workspaces.Default, "A test ask with a file")
            {
                Uploads = [new QuestUpload("note.txt", Encoding.UTF8.GetBytes("words"))],
            },
            Now)).Ask!;
        var kept = files.For(AskDesk.Folder);
        Assert.True(kept.Has(asked.Id, Assert.Single(asked.Attachments)));

        var deleted = await desk.DeleteAsync(asked.Id, Now.AddMinutes(1));

        Assert.Equal(AskRefusal.None, deleted.Refusal);
        Assert.Null(await desk.FindAsync(asked.Id));
        Assert.False(kept.Has(asked.Id, asked.Attachments[0]));
    }

    /// <summary>
    /// 🔴 An ask one of whose quests somebody took stays WHOLE — the ask and every quest — and the refusal
    /// names the quest that holds it and says to close the ask instead.
    /// </summary>
    [Fact]
    public async Task An_ask_with_a_quest_somebody_took_stays_whole()
    {
        var (desk, exchange, _) = await DeskAsync();
        var asked = (await desk.AskAsync(new AskRequest(Workspaces.Default, "A real ask") { To = "Homebody" }, Now)).Ask!;
        var untaken = (await desk.PublishAsync(asked.Id, "Federated", Now.AddMinutes(1))).Quest!;
        await exchange.RespondAsync(asked.Quests[0], "take", null, Now.AddMinutes(2));

        var refused = await desk.DeleteAsync(asked.Id, Now.AddMinutes(3));

        Assert.Equal(AskRefusal.Kept, refused.Refusal);
        Assert.Contains($"`#{asked.Quests[0]}`", refused.Message);
        Assert.Contains("is Taken", refused.Message);
        Assert.Contains("close the ask instead", refused.Message);
        Assert.NotNull(await desk.FindAsync(asked.Id));
        Assert.NotNull(await _quests.FindAsync(untaken.Id));
    }

    /// <summary>
    /// A quest deleted from an ask leaves the ask as if it had never become that quest: derived, so a
    /// delete synced in from another machine counts too. With none left it is a proposal again.
    /// </summary>
    [Fact]
    public async Task An_ask_whose_only_quest_was_deleted_is_a_proposal_again()
    {
        var (desk, exchange, _) = await DeskAsync();
        var asked = (await desk.AskAsync(new AskRequest(Workspaces.Default, "Asked of the wrong one") { To = "Homebody" }, Now)).Ask!;

        await exchange.DeleteAsync(asked.Quests[0], Now.AddMinutes(1));

        var standing = (await desk.FindAsync(asked.Id))!;
        Assert.Equal(AskState.Proposed, standing.State);
        Assert.Empty(standing.Quests);
        Assert.Single(await desk.ListAsync());
    }

    /// <summary>Each ask says whether it may go — the desk's own judgement, so no page re-derives the rule.</summary>
    [Fact]
    public async Task An_ask_says_whether_it_may_be_deleted()
    {
        var (desk, exchange, _) = await DeskAsync();
        var free = (await desk.AskAsync(new AskRequest(Workspaces.Default, "Free to go") { To = "Homebody" }, Now)).Ask!;
        var held = (await desk.AskAsync(new AskRequest(Workspaces.Default, "Being worked") { To = "Federated" }, Now)).Ask!;
        await exchange.RespondAsync(held.Quests[0], "take", null, Now.AddMinutes(1));

        Assert.True((await desk.FindAsync(free.Id))!.Deletable);
        Assert.False((await desk.FindAsync(held.Id))!.Deletable);
        Assert.Equal([free.Id], (await desk.ListAsync()).Where(ask => ask.Deletable).Select(ask => ask.Id));
    }

    [Fact]
    public async Task Deleting_an_ask_nobody_holds_is_refused_as_not_found()
    {
        var (desk, _, _) = await DeskAsync();

        Assert.Equal(AskRefusal.NotFound, (await desk.DeleteAsync("zzzzzz", Now)).Refusal);
    }

    /// <summary>
    /// The ledger reads an ask's standing as the desk does: an ask whose every quest was deleted is a
    /// proposal again, so an intake may answer it — or the driver's loop would ask, and be refused, on
    /// every tick.
    /// </summary>
    [Fact]
    public async Task An_intake_may_open_for_an_ask_whose_quests_were_all_deleted()
    {
        var (desk, exchange, _) = await DeskAsync();
        var asked = (await desk.AskAsync(new AskRequest(Workspaces.Default, "Asked of the wrong one") { To = "Homebody" }, Now)).Ask!;
        await exchange.DeleteAsync(asked.Quests[0], Now.AddMinutes(1));
        var ledger = new SessionLedger(_quests, _sessions, _service, await AskStore.OpenAsync(_connection));

        var opened = await ledger.OpenIntakeAsync(asked.Id, "stub", Path.Combine(_root, "room"), Now.AddMinutes(2));

        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
    }
}
