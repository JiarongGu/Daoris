using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// QUESTCLOSE1 (D126's note): the person marks a quest done. On the install (2026-10-08) two driven sessions were finished at
/// a checkpoint, each record moved to completed, and each quest stayed taken: nothing the person could press closed it, since
/// a done on a quest an ask asked must answer each of the person's requirements, which only the agent's done can. The
/// person's done is theirs: it answers none of their own requirements, and its note says it was theirs, with their words.
/// </summary>
public sealed class QuestPersonDoneTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-person-done-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskStore _asks = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
    private KnowledgeService _service = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-08T09:00:00Z");

    private const string Sentence = "write up the onboarding for the new client, and it needs the v3 bridge";

    private static readonly QuestRequirement Bridge =
        new("it needs the v3 bridge", "The write-up names the bridge's route.");

    public async Task InitializeAsync()
    {
        Repo("reports", """{ "summary": "The reports.", "owns": ["report pages", "the v3 bridge"], "accepts": ["a report"] }""");
        Repo("checker", """{ "summary": "Checks things in a browser.", "owns": ["verification"], "accepts": ["a check"] }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        _asks = await AskStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await _service.RegistryAsync())
        {
            await _service.RegisterAsync(row with { Workspace = "work" }, Now);
        }

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _exchange = new QuestExchange(_service, _quests, files: files, asks: _asks);
        _desk = new AskDesk(_service, _asks, _exchange, files);
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

    /// <summary>The owner's shape: a quest an ask asked, carrying the person's requirement and a step to follow, taken.</summary>
    private async Task<(Ask Ask, Quest Quest)> Taken(bool take = true)
    {
        var ask = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;
        var published = await _desk.PublishAsync(ask.Id, "reports", Now, draft: new AskDraft("Write up the onboarding", "For the new client.")
        {
            Requirements = [Bridge],
            Then = [new QuestStep("checker", "Read {parent}'s write-up", "Open it and read it.")],
        });
        Assert.Equal(AskRefusal.None, published.Refusal);
        if (take) Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(published.Quest!.Id, "take", null, Now)).Refusal);
        return (ask, published.Quest!);
    }

    private async Task<IReadOnlyList<Quest>> FollowUps(Ask ask, Quest parent) =>
        [.. (await _quests.FromAsync(AskDesk.SenderOf(ask.Id))).Where(quest => quest.Parent == parent.Id)];

    /// <summary>
    /// 🔴 The owner's case: a taken quest carrying the person's requirement closes done on the person's word, answering none,
    /// its note saying it was theirs with their words, nothing held, and its chain going on as any done's does.
    /// </summary>
    [Fact]
    public async Task The_persons_done_closes_a_taken_quest_with_requirements_and_says_it_was_theirs()
    {
        var (ask, quest) = await Taken();

        var closed = await _exchange.PersonDoneAsync(quest.Id, "  The write-up is in the shared folder.  ", Now);

        Assert.Equal(QuestRespondRefusal.None, closed.Refusal);
        var done = (await _quests.FindAsync(quest.Id))!;
        Assert.Equal(QuestStatus.Done, done.Status);
        Assert.Equal("The person marked this done: The write-up is in the shared folder.", done.Note);
        Assert.Empty(done.Answers);
        Assert.False(done.Held);
        Assert.Single(await FollowUps(ask, quest));
        Assert.Contains($"Quest `#{quest.Id}` is now Done: you marked it done.", closed.Message);
        Assert.Contains("Then: published", closed.Message);
        // The log holds it as any done, so every machine that replays it reads the same quest.
        Assert.Equal(done.Note, QuestLog.Replay(await _quests.HistoryAsync(quest.Id))!.Note);
    }

    /// <summary>With no words of theirs, the note is the sentence alone: still theirs, never blank, never the agent's.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Without_words_the_note_says_the_person_marked_it_done(string? words)
    {
        var (_, quest) = await Taken();

        await _exchange.PersonDoneAsync(quest.Id, words, Now);

        Assert.Equal("The person marked this done.", (await _quests.FindAsync(quest.Id))!.Note);
        Assert.Equal(QuestExchange.PersonDoneNote(words), (await _quests.FindAsync(quest.Id))!.Note);
    }

    /// <summary>An open quest closes on the person's word too, as the quest page's done always could.</summary>
    [Fact]
    public async Task The_persons_done_closes_an_open_quest()
    {
        var (_, quest) = await Taken(take: false);

        var closed = await _exchange.PersonDoneAsync(quest.Id, null, Now);

        Assert.Equal(QuestRespondRefusal.None, closed.Refusal);
        Assert.Equal(QuestStatus.Done, closed.Quest!.Status);
    }

    /// <summary>A closed quest does not move, and the answer names the state; an unknown id is no quest.</summary>
    [Fact]
    public async Task A_closed_quest_and_an_unknown_one_are_refused()
    {
        var (_, quest) = await Taken();
        await _exchange.PersonDoneAsync(quest.Id, "Done once.", Now);

        var again = await _exchange.PersonDoneAsync(quest.Id, "Done twice.", Now);
        var unknown = await _exchange.PersonDoneAsync("#feedfacecafe", null, Now);

        Assert.Equal(QuestRespondRefusal.Closed, again.Refusal);
        Assert.Contains($"Quest `#{quest.Id}` is Done", again.Message);
        Assert.Null(again.Quest);
        Assert.Equal("The person marked this done: Done once.", (await _quests.FindAsync(quest.Id))!.Note);
        Assert.Equal(QuestRespondRefusal.NotFound, unknown.Refusal);
    }

    /// <summary>
    /// The agent's done is unchanged: it still answers each requirement, so the person's door is the one way a done answers
    /// none, and it is never reached through the respond door.
    /// </summary>
    [Fact]
    public async Task The_agents_done_still_answers_each_requirement()
    {
        var (_, quest) = await Taken();

        var refused = await _exchange.RespondAsync(quest.Id, "done", "The person marked this done.", Now);

        Assert.Equal(QuestRespondRefusal.Unanswered, refused.Refusal);
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync(quest.Id))!.Status);
    }

    /// <summary>A quest waiting on this one (D79) resumes once the person's done closes it, and the answer says so.</summary>
    [Fact]
    public async Task A_quest_waiting_on_it_is_said_to_resume()
    {
        var (_, quest) = await Taken();
        var asker = (await _exchange.PublishAsync(new QuestAsk("reports", "checker", "Check the write-up", "Before the client reads it."), Now)).Quest!;
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(asker.Id, "take", null, Now)).Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(asker.Id, "wait", null, Now, on: quest.Id)).Refusal);

        var closed = await _exchange.PersonDoneAsync(quest.Id, null, Now);

        Assert.Contains($"`#{asker.Id}` (`checker`) waits on it, and resumes at the driver's next look.", closed.Message);
    }
}
