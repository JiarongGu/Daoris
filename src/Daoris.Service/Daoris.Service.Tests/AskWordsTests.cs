using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// DRIFT1a (D133 §1): the person's words are the ask's record. Every sentence they give on an ask is kept
/// on the ask verbatim, with when it was given, to which session and on which quest: the ask itself, each
/// answer to a session that parked to ask them, and each message added to one while it ran. Before this,
/// an answer and an added message lived only in the session they were given to, and each reached that one
/// session (`docs/2026-10-02-ask-drift-evidence.md` §4.7).
/// </summary>
public sealed class AskWordsTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-askwords-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskStore _asks = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
    private SessionStore _sessions = null!;
    private SessionLedger _ledger = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-02T09:00:00Z");

    private const string Sentence = "complete the report and it will need the v3 bridge";

    public async Task InitializeAsync()
    {
        Repo("reports", """{ "summary": "The reports.", "owns": ["report pages", "the v3 bridge"], "accepts": ["a report"] }""");
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

    /// <summary>An ask published to <c>reports</c> at once, and the quest it became.</summary>
    private async Task<(Ask Ask, Quest Quest)> Asked(string sentence = Sentence)
    {
        var outcome = await _desk.AskAsync(new AskRequest("work", sentence) { To = "reports" }, Now);
        return (outcome.Ask!, outcome.Quest!);
    }

    /// <summary>A session on <paramref name="quest"/>, moved as far as <paramref name="state"/>.</summary>
    private async Task<Session> Running(Quest quest, string tree, string state = "working")
    {
        var session = (await _ledger.OpenAsync(quest.Id, "stub", Now, tree: Tree(tree))).Session!;
        foreach (var step in new[] { "starting", "working" })
        {
            await _ledger.AdvanceAsync(session.Id, step, null, null, null, Now);
        }

        if (state == "awaiting-person")
        {
            await _ledger.AdvanceAsync(session.Id, state, "Which report is the individual one?", null, null, Now);
        }

        return session;
    }

    /// <summary>The ask's own sentence is its first word, as it was asked, given to no session.</summary>
    [Fact]
    public async Task An_asks_first_word_is_its_own_sentence_and_a_new_ask_keeps_its_words_whole()
    {
        var (ask, _) = await Asked();

        var read = (await _desk.FindAsync(ask.Id))!;

        var first = Assert.Single(read.Words);
        Assert.Equal(new AskWord(AskWordKind.Asked, Sentence, Now), first);
        Assert.Null(read.WordsKeptFrom);
        // The answer a door hands back is the same record a read gives.
        Assert.Equal(read.Words, ask.Words);
    }

    /// <summary>
    /// An answer to a parked session is read back from the ask, verbatim, with when it was given, the
    /// session it was given to and the quest that session worked.
    /// </summary>
    [Fact]
    public async Task An_answer_to_a_parked_session_is_read_back_from_the_ask()
    {
        var (ask, quest) = await Asked();
        var parked = await Running(quest, "answer", "awaiting-person");
        await _ledger.AnswerAsync(parked.Id, "I think we should be using the v3 common-report", Now.AddMinutes(5));

        var kept = await _ledger.KeepOnAskAsync(
            parked.Id, AskWordKind.Answered, "  I think we should be using the v3 common-report\n", Now.AddMinutes(5));

        Assert.Equal(AskWordRefusal.None, kept.Refusal);
        Assert.Equal(ask.Id, kept.Ask);
        Assert.Contains($"ask `#{ask.Id}`", kept.Message);
        var words = (await _desk.FindAsync(ask.Id))!.Words;
        Assert.Equal(2, words.Count);
        Assert.Equal(
            new AskWord(AskWordKind.Answered, "I think we should be using the v3 common-report", Now.AddMinutes(5), parked.Id, quest.Id),
            words[1]);
    }

    /// <summary>A message added to a running session is read back from the ask the same way, newest last.</summary>
    [Fact]
    public async Task A_message_added_to_a_running_session_is_read_back_from_the_ask_newest_last()
    {
        var (ask, quest) = await Asked();
        var running = await Running(quest, "added");

        await _ledger.KeepOnAskAsync(running.Id, AskWordKind.Added, "no need for a new backend api", Now.AddMinutes(1));
        var second = await _ledger.KeepOnAskAsync(
            running.Id, AskWordKind.Added, "add any missing feature to the common-report module", Now.AddMinutes(2));

        Assert.Equal(AskWordRefusal.None, second.Refusal);
        var read = (await _desk.FindAsync(ask.Id))!;
        Assert.Equal(
            [
                new AskWord(AskWordKind.Asked, Sentence, Now),
                new AskWord(AskWordKind.Added, "no need for a new backend api", Now.AddMinutes(1), running.Id, quest.Id),
                new AskWord(AskWordKind.Added, "add any missing feature to the common-report module", Now.AddMinutes(2), running.Id, quest.Id),
            ],
            read.Words);
        Assert.Equal(Now.AddMinutes(2), read.Updated);
    }

    /// <summary>
    /// A chain step is asked by the ask too (D65 §4), so a word given to the session verifying the work
    /// lands on the same ask as one given to the session that built it — the case the drift lost.
    /// </summary>
    [Fact]
    public async Task A_word_given_on_a_chain_step_is_kept_on_the_same_ask()
    {
        var ask = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;
        var built = (await _desk.PublishAsync(
            ask.Id, "reports", Now,
            draft: new AskDraft("Build the report", "Through the bridge.")
            {
                Then = [new QuestStep("checker", "Verify {parent} in the browser", "Open it and look.")],
            })).Quest!;
        await _exchange.RespondAsync(built.Id, "take", null, Now);
        await _exchange.RespondAsync(built.Id, "done", "Built.", Now);
        var step = (await _quests.FromAsync(AskDesk.SenderOf(ask.Id))).Single(quest => quest.Id != built.Id);
        var verifying = await Running(step, "verify", "awaiting-person");

        var kept = await _ledger.KeepOnAskAsync(
            verifying.Id, AskWordKind.Answered, "we should be using v3 common-report", Now.AddHours(1));

        Assert.Equal(AskWordRefusal.None, kept.Refusal);
        var word = (await _desk.FindAsync(ask.Id))!.Words[^1];
        Assert.Equal((verifying.Id, step.Id), (word.Session, word.Quest));
    }

    /// <summary>
    /// An intake is a session on the ask with no quest yet (D65 §1b): what the person adds to it shapes
    /// every quest it publishes, so it is the ask's word too, given on no quest.
    /// </summary>
    [Fact]
    public async Task A_message_added_to_an_intake_is_kept_on_its_ask_on_no_quest()
    {
        var ask = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;
        var intake = (await _ledger.OpenIntakeAsync(ask.Id, "stub", Path.Combine(_root, "home", "intake", "work"), Now)).Session!;

        var kept = await _ledger.KeepOnAskAsync(intake.Id, AskWordKind.Added, "the bridge means common-report", Now.AddMinutes(1));

        Assert.Equal(AskWordRefusal.None, kept.Refusal);
        Assert.Equal(
            new AskWord(AskWordKind.Added, "the bridge means common-report", Now.AddMinutes(1), intake.Id, null),
            (await _desk.FindAsync(ask.Id))!.Words[^1]);
    }

    /// <summary>
    /// A session on no ask — a quest one repository asked of another, or a conversation on no quest —
    /// has nowhere to keep the words, and says so rather than putting them on an ask they were not given on.
    /// </summary>
    [Fact]
    public async Task Words_given_to_a_session_on_no_ask_are_not_kept_and_the_answer_says_why()
    {
        var (ask, _) = await Asked();
        var between = (await _exchange.PublishAsync(new QuestAsk("checker", "reports", "Expose a hook", "So we can check it."), Now)).Quest!;
        var worker = await Running(between, "between");
        var chat = (await _ledger.OpenChatAsync("reports", "stub", Now, tree: Tree("chat"))).Session!;

        var fromRepository = await _ledger.KeepOnAskAsync(worker.Id, AskWordKind.Added, "use the hook", Now);
        var fromChat = await _ledger.KeepOnAskAsync(chat.Id, AskWordKind.Added, "hello", Now);

        Assert.Equal(AskWordRefusal.NoAsk, fromRepository.Refusal);
        Assert.Contains($"`#{between.Id}`", fromRepository.Message);
        Assert.Contains("`checker`", fromRepository.Message);
        Assert.Equal(AskWordRefusal.NoAsk, fromChat.Refusal);
        Assert.Contains(chat.Id, fromChat.Message);
        Assert.Null(fromChat.Ask);
        Assert.Single((await _desk.FindAsync(ask.Id))!.Words);
    }

    /// <summary>Nothing is kept for a session that is not this machine's, nor for words that are not there.</summary>
    [Fact]
    public async Task An_unknown_or_another_machines_session_and_empty_words_are_refused()
    {
        var (ask, quest) = await Asked();
        var running = await Running(quest, "refused");
        await _sessions.MirrorAsync(new Session(
            "alice-laptop/ab12cd34", quest.Id, "reports", "stub", SessionState.Working, null, null, null, Now, Now));

        var unknown = await _ledger.KeepOnAskAsync("nothing1", AskWordKind.Added, "hello", Now);
        var theirs = await _ledger.KeepOnAskAsync("alice-laptop/ab12cd34", AskWordKind.Added, "hello", Now);
        var empty = await _ledger.KeepOnAskAsync(running.Id, AskWordKind.Added, "   ", Now);

        Assert.Equal(AskWordRefusal.NotFound, unknown.Refusal);
        Assert.Contains("nothing1", unknown.Message);
        Assert.Equal(AskWordRefusal.NotFound, theirs.Refusal);
        Assert.Equal(AskWordRefusal.Empty, empty.Refusal);
        Assert.Single((await _desk.FindAsync(ask.Id))!.Words);
        // The ask's own sentence is the ask's to keep, never a session's to add.
        await Assert.ThrowsAsync<ArgumentException>(() => _ledger.KeepOnAskAsync(running.Id, AskWordKind.Asked, "again", Now));
    }

    /// <summary>
    /// The words are appended where they are kept, in one statement (REV3): a publish or a close written
    /// afterwards, from another process, keeps them — each writes only the columns it owns.
    /// </summary>
    [Fact]
    public async Task A_later_publish_or_close_keeps_the_words()
    {
        var (ask, quest) = await Asked();
        var running = await Running(quest, "kept");
        await _ledger.KeepOnAskAsync(running.Id, AskWordKind.Added, "the footer comes from the v3 page", Now.AddMinutes(1));

        await _desk.PublishAsync(ask.Id, "checker", Now.AddMinutes(2));
        await _desk.CloseAsync(ask.Id, "Rehearsed only.", Now.AddMinutes(3));

        var words = (await _desk.FindAsync(ask.Id))!.Words;
        Assert.Equal("the footer comes from the v3 page", words[^1].Text);
        Assert.Equal(2, words.Count);
    }

    /// <summary>
    /// 🔴 A store from before DRIFT1a still reads every ask it holds, its sentence as its first word. Its
    /// later words were never kept and are not back-filled, so the record says from when they are kept
    /// rather than reading as though the person said nothing more; a word given afterwards is kept.
    /// </summary>
    [Fact]
    public async Task A_store_from_before_the_words_reads_its_asks_and_says_from_when_their_words_are_kept()
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
                  proposal TEXT NOT NULL DEFAULT '[]', quests TEXT NOT NULL DEFAULT '[]', intake TEXT NULL
                );
                INSERT INTO asks (id, workspace, sentence, state, tier, asked, updated, quests)
                VALUES ('a1b2c3', 'work', 'an older ask', 'Published', 'intake', '2026-10-01T02:26:00Z', '2026-10-01T02:29:00Z', '["q1"]');
                """;
            await create.ExecuteNonQueryAsync();
        }

        var before = DateTimeOffset.UtcNow;
        var asks = await AskStore.OpenAsync(old);
        var kept = (await asks.FindAsync("a1b2c3"))!;

        Assert.Equal("an older ask", kept.Sentence);
        Assert.Equal(["q1"], kept.Quests);
        Assert.Equal(
            new AskWord(AskWordKind.Asked, "an older ask", DateTimeOffset.Parse("2026-10-01T02:26:00Z")),
            Assert.Single(kept.Words));
        Assert.NotNull(kept.WordsKeptFrom);
        Assert.True(kept.WordsKeptFrom >= before, $"kept from {kept.WordsKeptFrom}, opened at {before}");

        // Opening it again moves nothing: the moment its words began to be kept is a fact, set once.
        var again = (await (await AskStore.OpenAsync(old)).FindAsync("a1b2c3"))!;
        Assert.Equal(kept.WordsKeptFrom, again.WordsKeptFrom);

        await asks.RecordWordAsync(
            "a1b2c3", new AskWord(AskWordKind.Answered, "use the common-report", Now, "s1", "q1"), Now);
        Assert.Equal("use the common-report", (await asks.FindAsync("a1b2c3"))!.Words[^1].Text);
    }

    /// <summary>A word kept by a newer build in a kind this one does not know is passed over, never a failed read.</summary>
    [Fact]
    public async Task A_word_of_a_kind_this_build_does_not_know_is_passed_over()
    {
        var (ask, _) = await Asked();
        await using (var write = _connection.CreateCommand())
        {
            write.CommandText = """
                UPDATE asks SET words = '[{"kind":"pointed","text":"this one","at":"2026-10-02T09:01:00Z"},{"kind":"added","text":"kept","at":"2026-10-02T09:02:00Z","session":"s1"}]'
                WHERE id = $id
                """;
            write.Parameters.AddWithValue("$id", ask.Id);
            await write.ExecuteNonQueryAsync();
        }

        var words = (await _desk.FindAsync(ask.Id))!.Words;

        Assert.Equal(
            [AskWordKind.Asked, AskWordKind.Added],
            words.Select(word => word.Kind));
        Assert.Equal(("kept", "s1", (string?)null), (words[1].Text, words[1].Session, words[1].Quest));
    }
}
