using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// SESSUX1f (D126 §5.4): the record's half of deleting a session, judged by the ledger. Only a conversation that served no
/// quest may go: ended, this machine's, named by no ask's intake and no quest's publisher, and held by no remote. The
/// tree and a landing are the machine's half, the driver's to judge.
/// </summary>
public sealed class SessionDeleteTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private SessionStore _sessions = null!;
    private AskStore _asks = null!;
    private SessionLedger _ledger = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-03T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);
        _asks = await AskStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(
            new Registration("Owner", Adopted: true, "A repo.", [], [], [], Entries: 0, Root: "/trees/owner"), Now);

        _ledger = new SessionLedger(_quests, _sessions, service, _asks);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    /// <summary>A chat in <c>Owner</c> that served no quest, ended as <paramref name="state"/>.</summary>
    private async Task<Session> EndedChat(SessionState state = SessionState.Completed)
    {
        var chat = await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat);
        await _sessions.SetStateAsync(chat.Id, state, null, null, null, Now.AddMinutes(5));
        return chat;
    }

    [Fact]
    public async Task An_ended_conversation_that_served_no_quest_is_deleted_whole()
    {
        var chat = await EndedChat();

        var outcome = await _ledger.DeleteAsync(chat.Id);

        Assert.Equal(SessionDeleteRefusal.None, outcome.Refusal);
        Assert.Equal(chat.Id, outcome.Session!.Id);
        Assert.Contains($"`{chat.Id}`", outcome.Message);
        Assert.Null(await _sessions.FindAsync(chat.Id));
    }

    /// <summary>Ask Daoris's conversation serves no quest and no ask, so once it ended it may go too.</summary>
    [Fact]
    public async Task An_ended_ask_daoris_conversation_is_deleted()
    {
        var help = await _sessions.CreateAsync(null, SessionLedger.HelpRepository, "stub", Now, kind: SessionKind.Chat);
        await _sessions.SetStateAsync(help.Id, SessionState.Stopped, null, null, null, Now.AddMinutes(1));

        Assert.Equal(SessionDeleteRefusal.None, (await _ledger.DeleteAsync(help.Id)).Refusal);
    }

    [Fact]
    public async Task A_live_conversation_is_refused_and_kept()
    {
        var chat = await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat);
        await _sessions.SetStateAsync(chat.Id, SessionState.Working, null, null, null, Now);

        var outcome = await _ledger.DeleteAsync(chat.Id);

        Assert.Equal(SessionDeleteRefusal.Live, outcome.Refusal);
        Assert.Contains("stop it first", outcome.Message);
        Assert.NotNull(await _sessions.FindAsync(chat.Id));
    }

    /// <summary>A teammate's record ran on their machine (SYNC4): it is refused naming the machine, before anything else.</summary>
    [Fact]
    public async Task A_teammates_record_is_refused_naming_its_machine()
    {
        await _sessions.MirrorAsync(new Session(
            "bob-laptop/ab12cd34", null, "Owner", "stub", SessionState.Working, null, null, null, Now, Now, Kind: SessionKind.Chat));

        var outcome = await _ledger.DeleteAsync("bob-laptop/ab12cd34");

        Assert.Equal(SessionDeleteRefusal.NotOurs, outcome.Refusal);
        Assert.Equal("bob-laptop", outcome.Origin);
        Assert.Contains("`bob-laptop`", outcome.Message);
        Assert.NotNull(await _sessions.FindAsync("bob-laptop/ab12cd34"));
    }

    /// <summary>A session that served a quest is that work's record, and the strikes are counted from it: archive it instead.</summary>
    [Fact]
    public async Task A_session_that_served_a_quest_is_refused_naming_the_quest()
    {
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Why.", Now);
        var driven = await _sessions.CreateAsync(quest.Id, "Owner", "stub", Now);
        await _sessions.SetStateAsync(driven.Id, SessionState.Failed, null, null, null, Now.AddMinutes(1));

        var outcome = await _ledger.DeleteAsync(driven.Id);

        Assert.Equal(SessionDeleteRefusal.ServedQuest, outcome.Refusal);
        Assert.Equal(quest.Id, outcome.Quest);
        Assert.Contains($"`#{quest.Id}`", outcome.Message);
        Assert.Contains("archive it instead", outcome.Message);
        Assert.NotNull(await _sessions.FindAsync(driven.Id));
    }

    /// <summary>An intake is named by its ask, which a reader follows to what it read and decided (D65 §1b).</summary>
    [Fact]
    public async Task An_intake_is_refused_naming_its_ask()
    {
        var intake = await _sessions.CreateAsync(null, "ask #a1b2c3", "stub", Now, kind: SessionKind.Chat, ask: "a1b2c3");
        await _sessions.SetStateAsync(intake.Id, SessionState.Completed, null, null, null, Now.AddMinutes(1));
        await _asks.SaveAsync(new Ask("a1b2c3", Workspaces.Default, "Please do it.", AskState.Published, "intake", Now, Now)
        {
            Intake = intake.Id,
        });

        var outcome = await _ledger.DeleteAsync(intake.Id);

        Assert.Equal(SessionDeleteRefusal.Named, outcome.Refusal);
        Assert.Equal(("a1b2c3", (string?)null), (outcome.Ask, outcome.Quest));
        Assert.Contains("`#a1b2c3`", outcome.Message);
        Assert.NotNull(await _sessions.FindAsync(intake.Id));
    }

    /// <summary>A conversation that published a quest is named by it (SESS1): that quest's page says what caused it.</summary>
    [Fact]
    public async Task A_conversation_that_published_a_quest_is_refused_naming_the_quest()
    {
        var chat = await EndedChat();
        var published = await _quests.PublishAsync("Owner", "Asker", "Is the header a free string?", "Asked in a chat.", Now, publishedBy: chat.Id);

        var outcome = await _ledger.DeleteAsync(chat.Id);

        Assert.Equal(SessionDeleteRefusal.Named, outcome.Refusal);
        Assert.Equal(((string?)null, published.Id), (outcome.Ask, outcome.Quest));
        Assert.Contains($"`#{published.Id}`", outcome.Message);
    }

    /// <summary>
    /// A record pushed to a remote leaves the team's copy behind, since a session record does not travel as a deletion: it
    /// is refused naming the workspace whose remote holds it.
    /// </summary>
    [Fact]
    public async Task A_pushed_conversation_is_refused_naming_its_workspace()
    {
        var chat = await _sessions.CreateAsync(null, "Owner", "stub", Now, workspace: "aurora", kind: SessionKind.Chat);
        await _sessions.SetStateAsync(chat.Id, SessionState.Completed, null, null, null, Now.AddMinutes(1));
        await _sessions.MarkPushedAsync([chat.Id]);

        var outcome = await _ledger.DeleteAsync(chat.Id);

        Assert.Equal(SessionDeleteRefusal.OnRemote, outcome.Refusal);
        Assert.Equal("aurora", outcome.Workspace);
        Assert.Contains("`aurora`", outcome.Message);
        Assert.NotNull(await _sessions.FindAsync(chat.Id));
    }

    [Fact]
    public async Task An_unknown_session_is_not_found()
    {
        var outcome = await _ledger.DeleteAsync("nope1234");

        Assert.Equal(SessionDeleteRefusal.NotFound, outcome.Refusal);
        Assert.Contains("`nope1234`", outcome.Message);
    }

    /// <summary>The judgement alone deletes nothing: the driver asks it for a refusal's words before its own half.</summary>
    [Fact]
    public async Task The_judgement_alone_answers_and_deletes_nothing()
    {
        var chat = await EndedChat();
        var quest = await _quests.PublishAsync("Asker", "Owner", "Do the thing", "Why.", Now);
        var driven = await _sessions.CreateAsync(quest.Id, "Owner", "stub", Now);

        Assert.Equal(SessionDeleteRefusal.None, (await _ledger.JudgeDeleteAsync(chat.Id)).Refusal);
        Assert.Equal(SessionDeleteRefusal.Live, (await _ledger.JudgeDeleteAsync(driven.Id)).Refusal);
        Assert.NotNull(await _sessions.FindAsync(chat.Id));
    }

    /// <summary>A list's answer says which records the ledger would delete, as D95's quests say theirs, judged by the same rule.</summary>
    [Fact]
    public async Task Deletable_answers_for_a_whole_list_by_the_same_rule()
    {
        var gone = await EndedChat();
        var live = await _sessions.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat);
        var publisher = await EndedChat();
        await _quests.PublishAsync("Owner", "Asker", "Asked in a chat", "Why.", Now, publishedBy: publisher.Id);
        var pushed = await EndedChat();
        await _sessions.MarkPushedAsync([pushed.Id]);

        var deletable = await _ledger.DeletableAsync(await _sessions.ListAsync(includeClosed: true));

        Assert.Equal([gone.Id], deletable);
        Assert.DoesNotContain(live.Id, deletable);
    }
}
