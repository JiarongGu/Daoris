using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// DRIFT1d (D133 §4): a done answers each requirement. Closing a quest that carries the person's requirements
/// says, for each, <c>met</c> with how its check was met, or <c>departed</c> with the reason and the person's own
/// words it turns on, quoted. A done that leaves one unanswered is refused, naming it; a departure is kept on the
/// quest and holds what follows it, a chain's next step or a quest waiting on it, until the person accepts it.
/// One session closed its quest "per your answer", on its own reading, and the next verified that reading rather
/// than the ask (<c>docs/2026-10-02-ask-drift-evidence.md</c> §4.5).
/// </summary>
public sealed class QuestAnswerTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-answers-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskStore _asks = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
    private KnowledgeService _service = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-03T09:00:00Z");

    private const string Sentence = "complete the ticket I logged, and this will need the v3 bridge";

    private const string Answered = "the calculation is in the ticket, and we should be using the v3 common-report";

    private static readonly QuestRequirement Bridge =
        new("this will need the v3 bridge", "The report opens in the older shell through the bridge's route.");

    private static readonly QuestRequirement Common =
        new("using the v3 common-report", "The report is a common-report configuration, not a report type of its own.");

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

    /// <summary>
    /// The drift's build quest: asked by the ask, carrying both of the person's requirements (the second quoting
    /// their answer, DRIFT1a), with a verifying step to follow, and taken by its repository.
    /// </summary>
    private async Task<(Ask Ask, Quest Quest)> Built(bool chain = true, IReadOnlyList<QuestRequirement>? requirements = null)
    {
        var ask = (await _desk.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;
        await _asks.RecordWordAsync(ask.Id, new AskWord(AskWordKind.Answered, Answered, Now, "s1", "q0"), Now);
        var published = await _desk.PublishAsync(ask.Id, "reports", Now, draft: new AskDraft("Build the daily report", "Through the bridge.")
        {
            Requirements = requirements ?? [Bridge, Common],
            Then = chain ? [new QuestStep("checker", "Verify {parent} in the browser", "Open it and look.")] : [],
        });
        Assert.Equal(AskRefusal.None, published.Refusal);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(published.Quest!.Id, "take", null, Now)).Refusal);
        return (ask, published.Quest);
    }

    private Task<QuestRespondOutcome> Done(Quest quest, params QuestAnswer[] answers) =>
        _exchange.RespondAsync(quest.Id, "done", "Built.", Now, answers: answers);

    private static QuestAnswer Met(int requirement, string how = "Opened it through the bridge's route and read it.") =>
        new(requirement, Met: how);

    private static QuestAnswer Departed(int requirement, string quote, string reason = "The ticket's calculation needed a type of its own.") =>
        new(requirement, Met: null, Departed: reason, Quote: quote);

    private async Task<IReadOnlyList<Quest>> FollowUps(Ask ask, Quest parent) =>
        [.. (await _quests.FromAsync(AskDesk.SenderOf(ask.Id))).Where(quest => quest.Parent == parent.Id)];

    // ——— Unanswered is refused, naming it.

    /// <summary>
    /// 🔴 A done that answers one requirement of two is refused, naming the one left — its words and its check — and
    /// nothing closes: the quest stays taken, and its next step is not published.
    /// </summary>
    [Fact]
    public async Task A_done_that_leaves_a_requirement_unanswered_is_refused_naming_it()
    {
        var (ask, quest) = await Built();

        var refused = await Done(quest, Met(1));

        Assert.Equal(QuestRespondRefusal.Unanswered, refused.Refusal);
        Assert.Contains("requirement 2: \"using the v3 common-report\"", refused.Message);
        Assert.Contains(Common.Check, refused.Message);
        Assert.DoesNotContain("requirement 1:", refused.Message);
        Assert.Null(refused.Quest);
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync(quest.Id))!.Status);
        Assert.Empty(await FollowUps(ask, quest));
    }

    /// <summary>A done with no answers at all, as every done was before, names every requirement it leaves.</summary>
    [Fact]
    public async Task A_done_with_no_answers_on_a_quest_with_requirements_names_each()
    {
        var (_, quest) = await Built();

        var refused = await _exchange.RespondAsync(quest.Id, "done", "Built, per your answer.", Now);

        Assert.Equal(QuestRespondRefusal.Unanswered, refused.Refusal);
        Assert.Contains("requirement 1: \"this will need the v3 bridge\"", refused.Message);
        Assert.Contains("requirement 2: \"using the v3 common-report\"", refused.Message);
        Assert.Contains("`met`", refused.Message);
        Assert.Contains("`departed`", refused.Message);
    }

    // ——— Met.

    /// <summary>Each requirement met: the quest closes done with its answers kept, nothing is held, and the chain goes on as it did.</summary>
    [Fact]
    public async Task A_done_meeting_each_requirement_closes_with_its_answers_and_the_chain_goes_on()
    {
        var (ask, quest) = await Built();

        var closed = await Done(quest, Met(1), Met(2, "It is a common-report configuration; no new type was added."));

        Assert.Equal(QuestRespondRefusal.None, closed.Refusal);
        var held = (await _quests.FindAsync(quest.Id))!;
        Assert.Equal(QuestStatus.Done, held.Status);
        Assert.Equal([Met(1), Met(2, "It is a common-report configuration; no new type was added.")], held.Answers);
        Assert.False(held.Held);
        Assert.Single(await FollowUps(ask, quest));
        Assert.Contains("Then: published", closed.Message);
        Assert.Equal(held.Answers, QuestLog.Replay(await _quests.HistoryAsync(quest.Id))!.Answers);
    }

    // ——— Departed: kept, held, and what follows waits for the person's yes.

    /// <summary>
    /// 🔴 A departure, quoting the person's own words it turns on, closes the quest done with the departure kept on
    /// it, and holds its next step: it is not published, the quest stays among the outstanding, and the answer says
    /// what waits and how the person accepts it.
    /// </summary>
    [Fact]
    public async Task A_departure_closes_the_quest_and_holds_its_next_step_for_the_persons_yes()
    {
        var (ask, quest) = await Built();

        var closed = await Done(quest, Met(1), Departed(2, "the calculation is in the ticket"));

        Assert.Equal(QuestRespondRefusal.None, closed.Refusal);
        var held = (await _quests.FindAsync(quest.Id))!;
        Assert.Equal(QuestStatus.Done, held.Status);
        Assert.True(held.Held);
        Assert.Null(held.Accepted);
        Assert.Equal(Departed(2, "the calculation is in the ticket"), held.Answers[1]);
        Assert.Empty(await FollowUps(ask, quest));
        Assert.Contains(quest.Id, (await _quests.ListAsync()).Select(listed => listed.Id));
        Assert.Contains("held for the person's yes", closed.Message);
        Assert.Contains("\"Verify #" + quest.Id + " in the browser\"", closed.Message);
        Assert.Contains($"`daoris-driver quest accept {quest.Id}`", closed.Message);
    }

    /// <summary>
    /// 🔴 The drift's own close: a departure that attributes a reading to the person ("per your answer") without their
    /// words is refused, naming the words they never said, and nothing closes.
    /// </summary>
    [Fact]
    public async Task A_departure_quoting_words_the_person_never_said_is_refused_naming_them()
    {
        var (_, quest) = await Built();

        var refused = await Done(quest, Met(1), Departed(2, "use a dedicated formula for the calculation"));

        Assert.Equal(QuestRespondRefusal.NotQuoted, refused.Refusal);
        Assert.Contains("requirement 2: \"use a dedicated formula for the calculation\"", refused.Message);
        Assert.Contains($"ask `#", refused.Message);
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync(quest.Id))!.Status);
    }

    /// <summary>
    /// A departure may quote any of the person's words on the ask, verbatim within whitespace and case; on a host that
    /// does not hold the ask, the quest's own requirements are the person's words it holds, and only those are quoted.
    /// </summary>
    [Fact]
    public async Task A_departure_quotes_the_asks_words_or_where_the_ask_is_not_held_the_requirements()
    {
        var (_, quest) = await Built();
        var blind = new QuestExchange(_service, _quests);

        var answerOnly = await blind.RespondAsync(quest.Id, "done", "Built.", Now, answers: [Met(1), Departed(2, "the calculation is in the ticket")]);
        var requirement = await blind.RespondAsync(quest.Id, "done", "Built.", Now, answers: [Met(1), Departed(2, "USING the v3\n common-report")]);

        Assert.Equal(QuestRespondRefusal.NotQuoted, answerOnly.Refusal);
        Assert.Contains("requirements", answerOnly.Message);
        Assert.Equal(QuestRespondRefusal.None, requirement.Refusal);
        Assert.True((await _quests.FindAsync(quest.Id))!.Held);
    }

    /// <summary>An answer is one requirement's, by its number, and says one thing: each way it is not one is refused, saying which.</summary>
    [Theory]
    [InlineData("none", "names no requirement")]
    [InlineData("three", "names requirement 3")]
    [InlineData("twice", "answered twice")]
    [InlineData("both", "`met` and `departed` at once")]
    [InlineData("neither", "neither `met` nor `departed`")]
    [InlineData("no-quote", "the person's own words it turns on")]
    [InlineData("no-reason", "without its reason")]
    [InlineData("met-quote", "a met answer quotes nothing")]
    [InlineData("long", "longer than 2000 characters")]
    public async Task An_answer_that_is_not_one_is_refused_saying_which(string shape, string said)
    {
        var (_, quest) = await Built();
        QuestAnswer[] answers = shape switch
        {
            "none" => [Met(1), new QuestAnswer(0, Met: "Done.")],
            "three" => [Met(1), Met(2), Met(3)],
            "twice" => [Met(1), Met(1)],
            "both" => [Met(1), new QuestAnswer(2, Met: "Done.", Departed: "Not done.", Quote: "the v3 common-report")],
            "neither" => [Met(1), new QuestAnswer(2, Met: null)],
            "no-quote" => [Met(1), new QuestAnswer(2, Met: null, Departed: "Another way.", Quote: " ")],
            "no-reason" => [Met(1), new QuestAnswer(2, Met: null, Departed: "  ", Quote: "the v3 common-report")],
            "met-quote" => [Met(1), new QuestAnswer(2, Met: "Done.", Quote: "the v3 common-report")],
            _ => [Met(1), Met(2, new string('x', QuestExchange.MaxRequirementLength + 1))],
        };

        var refused = await Done(quest, answers);

        Assert.Equal(QuestRespondRefusal.BadAnswer, refused.Refusal);
        Assert.Contains(said, refused.Message);
        Assert.Equal(QuestStatus.Taken, (await _quests.FindAsync(quest.Id))!.Status);
    }

    /// <summary>A quest with no requirements closes as it always did, and takes no answers, since there is nothing to answer.</summary>
    [Fact]
    public async Task A_quest_with_no_requirements_closes_as_before_and_takes_no_answers()
    {
        var plain = (await _exchange.PublishAsync(new QuestAsk("checker", "reports", "Expose a hook", "So it can be checked."), Now)).Quest!;
        await _exchange.RespondAsync(plain.Id, "take", null, Now);

        var answered = await _exchange.RespondAsync(plain.Id, "done", "Exposed.", Now, answers: [Met(1)]);
        var closed = await _exchange.RespondAsync(plain.Id, "done", "Exposed.", Now);

        Assert.Equal(QuestRespondRefusal.BadAnswer, answered.Refusal);
        Assert.Contains("carries no requirements", answered.Message);
        Assert.Equal(QuestRespondRefusal.None, closed.Refusal);
        Assert.Equal($"Quest `#{plain.Id}` is now Done.", closed.Message);
        Assert.Empty(closed.Quest!.Answers);
        Assert.False(closed.Quest.Held);
    }

    /// <summary>Answers are a done's: a take, a wait or a decline carrying them is refused rather than dropping them.</summary>
    [Fact]
    public async Task Answers_are_given_only_at_done()
    {
        var (_, quest) = await Built();

        var question = (await _exchange.PublishAsync(new QuestAsk("reports", "checker", "What does the check need?", "Why."), Now)).Quest!;

        var declined = await _exchange.RespondAsync(quest.Id, "decline", "Not ours.", Now, answers: [Met(1), Met(2)]);
        var waited = await _exchange.RespondAsync(quest.Id, "wait", null, Now, on: question.Id, answers: [Met(1), Met(2)]);

        Assert.Equal(QuestRespondRefusal.BadAnswer, declined.Refusal);
        Assert.Contains("closing `done`", declined.Message);
        Assert.Equal(QuestRespondRefusal.BadAnswer, waited.Refusal);
        var standing = (await _quests.FindAsync(quest.Id))!;
        Assert.Equal(QuestStatus.Taken, standing.Status);
        Assert.Null(standing.Awaits);
    }

    // ——— The person's yes.

    /// <summary>
    /// 🔴 The person accepts the departure: the held step is published then, carrying the requirements as any step does,
    /// the quest is no longer held, and it leaves the outstanding.
    /// </summary>
    [Fact]
    public async Task Accepting_a_departure_publishes_the_held_step_and_releases_the_quest()
    {
        var (ask, quest) = await Built();
        await Done(quest, Met(1), Departed(2, "the calculation is in the ticket"));
        var later = Now.AddMinutes(5);

        var accepted = await _exchange.AcceptAsync(quest.Id, later);

        Assert.Equal(QuestRespondRefusal.None, accepted.Refusal);
        var standing = (await _quests.FindAsync(quest.Id))!;
        Assert.False(standing.Held);
        Assert.Equal(later, standing.Accepted);
        Assert.Equal(QuestStatus.Done, standing.Status);
        var step = Assert.Single(await FollowUps(ask, quest));
        Assert.Equal([Bridge, Common], step.Requirements);
        Assert.Contains($"Then: published `#{step.Id}`", accepted.Message);
        Assert.DoesNotContain(quest.Id, (await _quests.ListAsync()).Select(listed => listed.Id));
    }

    /// <summary>Only a quest held by a departure takes a yes: a taken one, one met in full and one already accepted each say why not.</summary>
    [Fact]
    public async Task Only_a_held_quest_takes_a_yes()
    {
        var (_, taken) = await Built(chain: false);
        var whileTaken = await _exchange.AcceptAsync(taken.Id, Now);
        await Done(taken, Met(1), Departed(2, "the v3 common-report"));
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.AcceptAsync(taken.Id, Now)).Refusal);
        var again = await _exchange.AcceptAsync(taken.Id, Now);
        var unknown = await _exchange.AcceptAsync("feedfacecafe", Now);

        Assert.Equal(QuestRespondRefusal.NotHeld, whileTaken.Refusal);
        Assert.Contains("Taken", whileTaken.Message);
        Assert.Equal(QuestRespondRefusal.NotHeld, again.Refusal);
        Assert.Contains("already accepted", again.Message);
        Assert.Equal(QuestRespondRefusal.NotFound, unknown.Refusal);
    }

    /// <summary>
    /// A quest that waits on a held question is held with it (D79's resume): the done names it as waiting for the yes,
    /// the question stays among the outstanding the driver plans from, and the yes says it resumes.
    /// </summary>
    [Fact]
    public async Task A_quest_waiting_on_a_held_question_waits_for_the_yes_too()
    {
        var (_, question) = await Built(chain: false);
        var waiting = (await _exchange.PublishAsync(new QuestAsk("reports", "checker", "Check the daily report", "Once it is built."), Now)).Quest!;
        await _exchange.RespondAsync(waiting.Id, "take", null, Now);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(waiting.Id, "wait", null, Now, on: question.Id)).Refusal);

        var closed = await Done(question, Met(1), Departed(2, "the v3 common-report"));

        Assert.Contains($"`#{waiting.Id}`", closed.Message);
        Assert.Contains(question.Id, (await _quests.ListAsync()).Select(listed => listed.Id));
        var accepted = await _exchange.AcceptAsync(question.Id, Now);
        Assert.Contains($"`#{waiting.Id}`", accepted.Message);
        Assert.Contains("resumes", accepted.Message);
    }

    /// <summary>An ask whose quest is held is not done: what the person asked waits on their yes, and it is done once they give it.</summary>
    [Fact]
    public async Task An_ask_whose_quest_is_held_is_not_done_until_the_yes()
    {
        var (ask, quest) = await Built(chain: false);

        await Done(quest, Met(1), Departed(2, "the v3 common-report"));
        var whileHeld = (await _desk.FindAsync(ask.Id))!.State;
        await _exchange.AcceptAsync(quest.Id, Now);

        Assert.Equal(AskState.Published, whileHeld);
        Assert.Equal(AskState.Done, (await _desk.FindAsync(ask.Id))!.State);
    }

    // ——— The record: the history, the wire.

    /// <summary>The answers and the yes are operations in the quest's history: a replay, and the wire, say the same on any machine.</summary>
    [Fact]
    public async Task Answers_and_the_yes_are_in_the_quests_history_and_cross_the_wire()
    {
        var (_, quest) = await Built(chain: false);
        await Done(quest, Met(1), Departed(2, "the v3 common-report"));

        var held = await _quests.HistoryAsync(quest.Id);
        Assert.True(QuestLog.Replay(held)!.Held);
        await _exchange.AcceptAsync(quest.Id, Now);
        var history = await _quests.HistoryAsync(quest.Id);
        var pushed = QuestWire.ReadPush(QuestWire.Push(0, history))!.Value.Operations;

        Assert.Equal(QuestOperationKind.Accepted, history[^1].Kind);
        var replayed = QuestLog.Replay(pushed)!;
        Assert.Equal([Met(1), Departed(2, "the v3 common-report")], replayed.Answers);
        Assert.False(replayed.Held);
        Assert.Equal(Now, replayed.Accepted);
    }

    /// <summary>An answer half-made on the wire is an operation not whole; a done from a build before answers reads as one with none.</summary>
    [Fact]
    public void An_answer_on_the_wire_that_is_not_one_makes_the_operation_not_whole()
    {
        const string Push = """
            { "base": 0, "operations": [{ "machine": "m1", "sequence": 2, "quest": "abcdefabcdef", "kind": "done",
              "at": "2026-10-03T09:00:00Z", "note": "Built." ANSWERS }] }
            """;

        Assert.Null(QuestWire.ReadPush(Push.Replace("ANSWERS", """, "answers": [{ "requirement": 1, "departed": "Another way." }]""")));
        Assert.Null(QuestWire.ReadPush(Push.Replace("ANSWERS", """, "answers": [{ "met": "Opened it." }]""")));
        Assert.Empty(QuestWire.ReadPush(Push.Replace("ANSWERS", ""))!.Value.Operations[0].Answers ?? []);
    }

    /// <summary>Two yeses are one: a yes applies only while the departure waits for one, so another machine's later yes moves nothing.</summary>
    [Fact]
    public void A_yes_applies_only_to_a_quest_its_departure_holds()
    {
        var done = new Quest("abcdefabcdef", "ask #a1", "reports", "t", "b", QuestStatus.Done, "Built.", Now, Now, "work")
        {
            Requirements = [Bridge],
            Answers = [Departed(1, "the v3 bridge")],
        };
        var yes = new QuestOperation("abcdefabcdef", QuestOperationKind.Accepted, "m2", 7, Now);

        Assert.True(QuestLog.Applies(done, yes));
        Assert.False(QuestLog.Applies(done with { Accepted = Now }, yes));
        Assert.False(QuestLog.Applies(done with { Answers = [Met(1)] }, yes));
        Assert.False(QuestLog.Applies(done with { Status = QuestStatus.Taken }, yes));
    }
}
