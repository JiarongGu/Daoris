using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWUSE1a (D135 §2): a go-ahead is asked once and held on the ask. A session that needs the person's yes for an act
/// outside its repository asks for it on the ask its quest was asked by, named by the act: its kind, where it lands and
/// what it touches. A request for an act already asked joins the first rather than asking again, and the person's answer
/// is kept on it. Three production acts drew thirteen asks because nothing held a go-ahead between sessions
/// (`docs/2026-10-03-knowledge-use-evidence.md` §5.1).
/// </summary>
public sealed partial class GoAheadTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-goahead-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskStore _asks = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-03T09:00:00Z");

    private const string Sentence = "the dashboard figure reads zero";

    public async Task InitializeAsync()
    {
        Repo("dashboards", """{ "summary": "The dashboards.", "owns": ["dashboard figures"], "accepts": ["a figure"] }""");
        Repo("checker", """{ "summary": "Checks things in a browser.", "owns": ["verification"], "accepts": ["a check"] }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _asks = await AskStore.OpenAsync(_connection);
        _sessions = await SessionStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync())
        {
            await service.RegisterAsync(row with { Workspace = "work" }, Now);
        }

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(service, _quests, files: files);
        _desk = new AskDesk(service, _asks, _exchange, files);
        _ledger = new SessionLedger(_quests, _sessions, service, _asks);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Repo(string name, string domain)
    {
        var dir = Path.Combine(_root, "family", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""{ "source": "s", "packs": [], "domain": {{domain}} }""");
    }

    private string Tree(string name) => Path.Combine(_root, "trees", name);

    private async Task<(Ask Ask, Quest Quest)> Asked(string sentence = Sentence)
    {
        var outcome = await _desk.AskAsync(new AskRequest("work", sentence) { To = "dashboards" }, Now);
        return (outcome.Ask!, outcome.Quest!);
    }

    /// <summary>A session on <paramref name="quest"/>, working, in a tree of its own name.</summary>
    private async Task<Session> Working(Quest quest, string tree)
    {
        var session = (await _ledger.OpenAsync(quest.Id, "stub", Now, tree: Tree(tree))).Session!;
        foreach (var step in new[] { "starting", "working" })
        {
            await _ledger.AdvanceAsync(session.Id, step, null, null, null, Now);
        }

        return session;
    }

    /// <summary>The session parks, and a carry-on opens on its quest in the same tree.</summary>
    private async Task<Session> CarriedOn(Session parked, Quest quest)
    {
        await _ledger.AdvanceAsync(parked.Id, "awaiting-person", "It needs the production write.", null, null, Now);
        await _ledger.AnswerAsync(parked.Id, "carry on", Now.AddMinutes(1));
        await _ledger.AdvanceAsync(parked.Id, "completed", "The answer is carried on in a new session.", null, null, Now.AddMinutes(2));
        var next = (await _ledger.OpenAsync(quest.Id, "stub", Now.AddMinutes(3), tree: Tree("carry"))).Session!;
        Assert.NotNull(next);
        return next;
    }

    /// <summary>
    /// A session's request is kept on its ask as go-ahead 1, waiting on the person: the act by its kind, where it lands and
    /// what it touches, and who asked it, on which quest, when and why.
    /// </summary>
    [Fact]
    public async Task A_sessions_request_is_kept_on_its_ask_as_one_go_ahead_waiting_on_the_person()
    {
        var (ask, quest) = await Asked();
        var session = await Working(quest, "first");

        var outcome = await _ledger.AskGoAheadAsync(
            session.Id, "write", "production", "dashboard configuration", "The tile reads its target from it.", Now.AddMinutes(4));

        Assert.Equal(GoAheadRefusal.None, outcome.Refusal);
        Assert.Equal(GoAheadJoin.New, outcome.Join);
        Assert.Equal(ask.Id, outcome.Ask);
        var held = Assert.Single((await _desk.FindAsync(ask.Id))!.GoAheads);
        Assert.Equal(1, held.Number);
        Assert.Equal(("write", "production", "dashboard configuration"), (held.Kind, held.On, held.Act));
        Assert.Equal(GoAheadState.Asked, held.State);
        Assert.Null(held.Answer);
        Assert.Null(held.Near);
        Assert.Equal(
            new GoAheadRequest(session.Id, quest.Id, Now.AddMinutes(4), "The tile reads its target from it."),
            Assert.Single(held.Asked));
        Assert.Contains("go-ahead 1", outcome.Message, StringComparison.Ordinal);
        Assert.Contains($"ask `#{ask.Id}`", outcome.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The proof: a second request for one act joins the first. A carry-on on another account asks for the same production
    /// write in other words (`prod`, the possessive, the act's own kind and place repeated in what it touches), and nothing
    /// new is asked of the person: go-ahead 1 now holds both requests, and the answer says it was asked before.
    /// </summary>
    [Fact]
    public async Task A_second_request_for_one_act_joins_the_first()
    {
        var (ask, quest) = await Asked();
        var first = await Working(quest, "first");
        await _ledger.AskGoAheadAsync(first.Id, "write", "production", "dashboard configuration", "The tile's target.", Now.AddMinutes(4));
        var carryOn = await CarriedOn(first, quest);

        var outcome = await _ledger.AskGoAheadAsync(
            carryOn.Id, "Write", "prod", "the dashboard's production configuration write", "Still needs the put.", Now.AddMinutes(9));

        Assert.Equal(GoAheadRefusal.None, outcome.Refusal);
        Assert.Equal(GoAheadJoin.Joined, outcome.Join);
        var held = Assert.Single((await _desk.FindAsync(ask.Id))!.GoAheads);
        Assert.Equal(1, held.Number);
        Assert.Equal("dashboard configuration", held.Act);
        Assert.Equal([first.Id, carryOn.Id], held.Asked.Select(request => request.Session));
        Assert.Equal("Still needs the put.", held.Asked[1].Why);
        Assert.Contains("go-ahead 1", outcome.Message, StringComparison.Ordinal);
        Assert.Contains("not answered", outcome.Message, StringComparison.Ordinal);
        Assert.Contains("not asked again", outcome.Message, StringComparison.Ordinal);
    }

    /// <summary>A request naming every word of an earlier act, and more, is that act made precise: it joins.</summary>
    [Fact]
    public async Task A_request_holding_every_word_of_an_earlier_act_joins_it()
    {
        var (ask, quest) = await Asked();
        var session = await Working(quest, "first");
        await _ledger.AskGoAheadAsync(session.Id, "write", "production", "menu entry", "The report needs one.", Now.AddMinutes(4));

        var outcome = await _ledger.AskGoAheadAsync(
            session.Id, "write", "production", "the comparison report's menu entries", "Both sites.", Now.AddMinutes(5));

        Assert.Equal(GoAheadJoin.Joined, outcome.Join);
        var held = Assert.Single((await _desk.FindAsync(ask.Id))!.GoAheads);
        // The same session asking again is not a second request of its own.
        Assert.Single(held.Asked);
    }

    /// <summary>
    /// Where the words cannot tell: a request sharing words with an earlier act without naming all of them may be it, or a
    /// part of it, or another act. It is asked once more, as a go-ahead of its own that names the near one, and the answer
    /// says it could not match it, so the person's yes never stretches past what they read.
    /// </summary>
    [Fact]
    public async Task A_request_the_words_cannot_tell_is_asked_once_more_and_says_it_could_not_match()
    {
        var (ask, quest) = await Asked();
        var session = await Working(quest, "first");
        await _ledger.AskGoAheadAsync(session.Id, "write", "production", "menu entry for the report", "One entry.", Now.AddMinutes(4));

        var outcome = await _ledger.AskGoAheadAsync(session.Id, "write", "production", "menu entries", "The entries.", Now.AddMinutes(5));

        Assert.Equal(GoAheadJoin.Unmatched, outcome.Join);
        var held = (await _desk.FindAsync(ask.Id))!.GoAheads;
        Assert.Equal([1, 2], held.Select(goAhead => goAhead.Number));
        Assert.Equal(1, held[1].Near);
        Assert.Contains("could not tell", outcome.Message, StringComparison.Ordinal);
        Assert.Contains("go-ahead 1", outcome.Message, StringComparison.Ordinal);
        Assert.Contains("go-ahead 2", outcome.Message, StringComparison.Ordinal);
    }

    /// <summary>Another kind, another place or no word shared is another act: asked as its own, naming none near.</summary>
    [Theory]
    [InlineData("release", "production", "dashboard configuration")]
    [InlineData("write", "development", "dashboard configuration")]
    [InlineData("write", "production", "display subtitle")]
    public async Task Another_kind_place_or_thing_is_another_act(string kind, string on, string act)
    {
        var (ask, quest) = await Asked();
        var session = await Working(quest, "first");
        await _ledger.AskGoAheadAsync(session.Id, "write", "production", "dashboard configuration", "The target.", Now.AddMinutes(4));

        var outcome = await _ledger.AskGoAheadAsync(session.Id, kind, on, act, "Another.", Now.AddMinutes(5));

        Assert.Equal(GoAheadJoin.New, outcome.Join);
        var second = (await _desk.FindAsync(ask.Id))!.GoAheads[1];
        Assert.Equal(2, second.Number);
        Assert.Null(second.Near);
    }

    /// <summary>
    /// The person answers a go-ahead on its ask, with words or none; a later request for the act joins it and is told the
    /// answer, verbatim, so a session never asks again what the person already answered.
    /// </summary>
    [Fact]
    public async Task An_answer_is_kept_on_the_go_ahead_and_a_later_request_is_told_it()
    {
        var (ask, quest) = await Asked();
        var first = await Working(quest, "first");
        await _ledger.AskGoAheadAsync(first.Id, "write", "production", "dashboard configuration", "The target.", Now.AddMinutes(4));

        var answered = await _desk.AnswerGoAheadAsync(ask.Id, 1, approved: true, "  run the put  ", Now.AddMinutes(6));

        Assert.Equal(GoAheadAnswerRefusal.None, answered.Refusal);
        var held = Assert.Single(answered.Ask!.GoAheads);
        Assert.Equal(GoAheadState.Approved, held.State);
        Assert.Equal(new GoAheadAnswer(true, "run the put", Now.AddMinutes(6)), held.Answer);
        Assert.Contains("approved", answered.Message, StringComparison.Ordinal);

        var carryOn = await CarriedOn(first, quest);
        var joined = await _ledger.AskGoAheadAsync(carryOn.Id, "write", "prod", "dashboard configuration", "Again.", Now.AddMinutes(9));

        Assert.Equal(GoAheadJoin.Joined, joined.Join);
        Assert.Equal(GoAheadState.Approved, joined.GoAhead!.State);
        Assert.Contains("approved", joined.Message, StringComparison.Ordinal);
        Assert.Contains("run the put", joined.Message, StringComparison.Ordinal);
    }

    /// <summary>A refusal is kept as a refusal, said to a request that joins it; a later answer replaces the earlier.</summary>
    [Fact]
    public async Task A_refusal_is_kept_and_a_later_answer_replaces_it()
    {
        var (ask, quest) = await Asked();
        var session = await Working(quest, "first");
        await _ledger.AskGoAheadAsync(session.Id, "release", "production", "comparison report", "Ship it.", Now.AddMinutes(4));

        await _desk.AnswerGoAheadAsync(ask.Id, 1, approved: false, "test it on dev first", Now.AddMinutes(5));
        var joined = await _ledger.AskGoAheadAsync(session.Id, "deploy", "live", "comparison report", "Ship it now.", Now.AddMinutes(6));

        Assert.Equal(GoAheadState.Refused, joined.GoAhead!.State);
        Assert.Contains("refused", joined.Message, StringComparison.Ordinal);
        Assert.Contains("test it on dev first", joined.Message, StringComparison.Ordinal);

        var changed = await _desk.AnswerGoAheadAsync(ask.Id, 1, approved: true, null, Now.AddMinutes(8));
        Assert.Equal(new GoAheadAnswer(true, null, Now.AddMinutes(8)), Assert.Single(changed.Ask!.GoAheads).Answer);
    }

    /// <summary>An answer to no ask, to a go-ahead the ask does not hold, or with words past the bound changes nothing.</summary>
    [Fact]
    public async Task An_answer_is_refused_for_no_ask_no_go_ahead_and_words_past_the_bound()
    {
        var (ask, quest) = await Asked();
        var session = await Working(quest, "first");
        await _ledger.AskGoAheadAsync(session.Id, "write", "production", "dashboard configuration", "The target.", Now.AddMinutes(4));

        var noAsk = await _desk.AnswerGoAheadAsync("ffffff", 1, approved: true, null, Now);
        var noGoAhead = await _desk.AnswerGoAheadAsync(ask.Id, 2, approved: true, null, Now);
        var tooLong = await _desk.AnswerGoAheadAsync(ask.Id, 1, approved: true, new string('x', GoAheads.WordsLimit + 1), Now);

        Assert.Equal(GoAheadAnswerRefusal.NotFound, noAsk.Refusal);
        Assert.Equal(GoAheadAnswerRefusal.NoGoAhead, noGoAhead.Refusal);
        Assert.Contains("go-ahead 2", noGoAhead.Message, StringComparison.Ordinal);
        Assert.Contains("1", noGoAhead.Message, StringComparison.Ordinal);
        Assert.Equal(GoAheadAnswerRefusal.TooLong, tooLong.Refusal);
        Assert.Null(Assert.Single((await _desk.FindAsync(ask.Id))!.GoAheads).Answer);
    }

    /// <summary>
    /// A request that names no kind the five spell, no place, nothing it touches, or no reason is refused, saying which,
    /// and nothing is kept.
    /// </summary>
    [Theory]
    [InlineData("copy", "production", "dashboard configuration", "why", "write, release, push, sign-in or run")]
    [InlineData("write", "  ", "dashboard configuration", "why", "where")]
    [InlineData("write", "production", "the", "why", "what it touches")]
    [InlineData("write", "production", "dashboard configuration", " ", "why")]
    public async Task A_request_missing_its_act_or_reason_is_refused_saying_which(
        string kind, string on, string act, string why, string said)
    {
        var (ask, quest) = await Asked();
        var session = await Working(quest, "first");

        var outcome = await _ledger.AskGoAheadAsync(session.Id, kind, on, act, why, Now);

        Assert.NotEqual(GoAheadRefusal.None, outcome.Refusal);
        Assert.Contains(said, outcome.Message, StringComparison.Ordinal);
        Assert.Empty((await _desk.FindAsync(ask.Id))!.GoAheads);
    }

    /// <summary>
    /// A session on no ask, a quest one repository asked of another or a conversation, has no ask to hold a go-ahead, and
    /// is told so, with what to do instead; a session this machine does not hold is refused.
    /// </summary>
    [Fact]
    public async Task A_session_on_no_ask_is_told_there_is_none_to_hold_it()
    {
        var published = await _exchange.PublishAsync(new QuestAsk("checker", "dashboards", "fix the figure", "It reads zero."), Now);
        var onRepositorysQuest = await Working(published.Quest!, "repository");
        var chat = (await _ledger.OpenChatAsync("checker", "stub", Now, tree: Tree("chat"))).Session!;

        var repository = await _ledger.AskGoAheadAsync(onRepositorysQuest.Id, "write", "production", "configuration", "why", Now);
        var conversation = await _ledger.AskGoAheadAsync(chat.Id, "write", "production", "configuration", "why", Now);
        var nobody = await _ledger.AskGoAheadAsync("nobody", "write", "production", "configuration", "why", Now);

        Assert.Equal(GoAheadRefusal.NoAsk, repository.Refusal);
        Assert.Contains("last message", repository.Message, StringComparison.Ordinal);
        Assert.Equal(GoAheadRefusal.NoAsk, conversation.Refusal);
        Assert.Equal(GoAheadRefusal.NotFound, nobody.Refusal);
    }

    /// <summary>A chain step is asked by the ask too (D65 §4): its sessions' go-aheads are held on the same ask.</summary>
    [Fact]
    public async Task A_follow_up_steps_session_asks_on_the_same_ask()
    {
        var outcome = await _desk.AskAsync(new AskRequest("work", Sentence), Now);
        var published = await _desk.PublishAsync(
            outcome.Ask!.Id, "dashboards", Now,
            draft: new AskDraft("fix the figure", "It reads zero.") { Then = [new QuestStep("checker", "verify {parent}", "Look at it.")] });
        var build = await Working(published.Quest!, "build");
        await _ledger.AskGoAheadAsync(build.Id, "write", "production", "dashboard configuration", "The target.", Now.AddMinutes(1));
        await _exchange.RespondAsync(published.Quest!.Id, "take", null, Now.AddMinutes(2));
        await _exchange.RespondAsync(published.Quest!.Id, "done", "landed", Now.AddMinutes(3));
        var step = (await _quests.FromAsync(AskDesk.SenderOf(outcome.Ask.Id))).Single(quest => quest.Parent == published.Quest.Id);
        var verify = await Working(step, "verify");

        var joined = await _ledger.AskGoAheadAsync(verify.Id, "write", "production", "dashboard configuration", "To check it.", Now.AddMinutes(4));

        Assert.Equal(GoAheadJoin.Joined, joined.Join);
        Assert.Equal(step.Id, Assert.Single((await _desk.FindAsync(outcome.Ask.Id))!.GoAheads).Asked[1].Quest);
    }

    /// <summary>A later publish and a close write the ask's own columns and keep its go-aheads.</summary>
    [Fact]
    public async Task A_later_publish_and_close_keep_the_go_aheads()
    {
        var (ask, quest) = await Asked();
        var session = await Working(quest, "first");
        await _ledger.AskGoAheadAsync(session.Id, "push", "origin", "report branch", "To open the pull request.", Now.AddMinutes(1));

        await _desk.PublishAsync(ask.Id, "checker", Now.AddMinutes(2));
        await _desk.CloseAsync(ask.Id, "done with it", Now.AddMinutes(3));

        Assert.Equal("push", Assert.Single((await _desk.FindAsync(ask.Id))!.GoAheads).Kind);
    }

    /// <summary>
    /// A store from before keeps every ask it had, reading none held; and a go-ahead this build cannot read (a newer kind,
    /// a half-written entry) is passed over, never a failed read of the ask, and kept as written by the next request.
    /// </summary>
    [Fact]
    public async Task A_store_from_before_reads_none_and_an_unreadable_go_ahead_is_passed_over_and_kept()
    {
        var (ask, quest) = await Asked();
        await using (var raw = _connection.CreateCommand())
        {
            raw.CommandText = """
                UPDATE asks SET go_aheads = '[{"number":1,"kind":"teleport","on":"production","act":"the moon","asked":[]},{"kind":"write"}]'
                WHERE id = $id
                """;
            raw.Parameters.AddWithValue("$id", ask.Id);
            await raw.ExecuteNonQueryAsync();
        }

        Assert.Empty((await _desk.FindAsync(ask.Id))!.GoAheads);

        var session = await Working(quest, "first");
        var outcome = await _ledger.AskGoAheadAsync(session.Id, "write", "production", "dashboard configuration", "why", Now);

        // Numbered after what it could not read, which it kept.
        Assert.Equal(2, outcome.GoAhead!.Number);
        await using var read = _connection.CreateCommand();
        read.CommandText = "SELECT go_aheads FROM asks WHERE id = $id";
        read.Parameters.AddWithValue("$id", ask.Id);
        var stored = (string)(await read.ExecuteScalarAsync())!;
        Assert.Contains("teleport", stored, StringComparison.Ordinal);
    }

    /// <summary>A store opened before go-aheads existed gains the column and reads every ask as holding none.</summary>
    [Fact]
    public async Task A_store_opened_before_go_aheads_gains_the_column()
    {
        await using var old = new SqliteConnection("Data Source=:memory:");
        await old.OpenAsync();
        await using (var create = old.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE asks (
                  id TEXT PRIMARY KEY, workspace TEXT NOT NULL, sentence TEXT NOT NULL, state TEXT NOT NULL,
                  tier TEXT NOT NULL, asked TEXT NOT NULL, updated TEXT NOT NULL, asker TEXT NULL, note TEXT NULL,
                  links TEXT NOT NULL DEFAULT '[]', attachments TEXT NOT NULL DEFAULT '[]',
                  proposal TEXT NOT NULL DEFAULT '[]', quests TEXT NOT NULL DEFAULT '[]');
                INSERT INTO asks (id, workspace, sentence, state, tier, asked, updated)
                VALUES ('abc123', 'work', 'an old ask', 'Proposed', 'declarations', '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z');
                """;
            await create.ExecuteNonQueryAsync();
        }

        var store = await AskStore.OpenAsync(old);

        Assert.Empty((await store.FindAsync("abc123"))!.GoAheads);
    }

    /// <summary>
    /// How an act is named, so two wordings of one act join (D135 §2): its kind by five words and their usual synonyms, where
    /// it lands with an environment's usual names read as one, and what it touches by its words, without case, the
    /// small words, the possessive, a plural's ending, and its own kind and place said again.
    /// </summary>
    [Theory]
    [InlineData("write", "write")]
    [InlineData(" Put ", "write")]
    [InlineData("update", "write")]
    [InlineData("deploy", "release")]
    [InlineData("publish", "release")]
    [InlineData("pull request", "push")]
    [InlineData("Sign in", "sign-in")]
    [InlineData("login", "sign-in")]
    [InlineData("execute", "run")]
    [InlineData("copy", null)]
    [InlineData("", null)]
    public void A_kind_is_read_by_its_five_words_and_their_synonyms(string said, string? kind) =>
        Assert.Equal(kind, GoAheadAct.Kind(said));

    [Theory]
    [InlineData("production", "production")]
    [InlineData("PROD", "production")]
    [InlineData("the live site", "production")]
    [InlineData("dev cloud", "development")]
    [InlineData("staging", "staging")]
    [InlineData("QA", "test")]
    [InlineData("localhost", "local")]
    [InlineData("the Analytics API", "analytics api")]
    [InlineData("  the ", "")]
    public void Where_an_act_lands_reads_an_environments_usual_names_as_one(string said, string on) =>
        Assert.Equal(on, GoAheadAct.Where(said));

    [Theory]
    [InlineData("dashboard configuration", "write", "production", "configuration dashboard")]
    [InlineData("the dashboard's Configuration", "write", "production", "configuration dashboard")]
    [InlineData("production configuration write", "write", "production", "configuration")]
    [InlineData("menu entries", "write", "production", "entry menu")]
    [InlineData("report 2 tiles", "write", "production", "2 report tile")]
    [InlineData("the test dashboard", "write", "production", "dashboard test")]
    [InlineData("delete the old entries", "write", "production", "delete entry old")]
    public void What_an_act_touches_is_read_by_its_words(string act, string kind, string on, string words) =>
        Assert.Equal(words, string.Join(" ", GoAheadAct.Words(act, kind, on).Order(StringComparer.Ordinal)));

    [Theory]
    [InlineData("write", "production", "dashboard configuration", "write", "production", "dashboard configuration", GoAheadMatch.Same)]
    [InlineData("write", "production", "dashboard configuration", "write", "production", "configuration of the dashboard tile", GoAheadMatch.Same)]
    [InlineData("write", "production", "dashboard configuration tile", "write", "production", "dashboard configuration", GoAheadMatch.Unclear)]
    [InlineData("write", "production", "menu entry", "write", "production", "entry for the report", GoAheadMatch.Unclear)]
    [InlineData("write", "production", "menu entries", "write", "production", "display subtitle", GoAheadMatch.Other)]
    [InlineData("write", "production", "menu entries", "release", "production", "menu entries", GoAheadMatch.Other)]
    [InlineData("write", "production", "menu entries", "write", "development", "menu entries", GoAheadMatch.Other)]
    public void Two_wordings_join_when_the_later_names_every_word_of_the_earlier(
        string kind, string on, string act, string laterKind, string laterOn, string laterAct, GoAheadMatch match) =>
        Assert.Equal(match, GoAheadAct.Match(new GoAhead(1, kind, on, act), laterKind, laterOn, laterAct));
}
