using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// DRIFT1c (D133 §3): a quest's requirements quote the person. Each requirement is the person's own words,
/// quoted, with the check that proves it; the service refuses a quote found in neither the ask nor what they
/// said on it since, naming the words, and a chain step inherits its parent's requirements. The intake's
/// paraphrase kept the word "bridge" and lost what the receiving repository's own documents said it meant
/// (`docs/2026-10-02-ask-drift-evidence.md` §3.1); a quote is a fact a gate checks with no model.
/// </summary>
public sealed class QuestRequirementTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-requirements-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskStore _asks = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
    private KnowledgeService _service = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-03T09:00:00Z");

    private const string Sentence = "complete the ticket I logged, and this will need the v3 bridge";

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

    /// <summary>An ask nobody has published yet, as an intake finds it.</summary>
    private async Task<Ask> Asked(string sentence = Sentence) =>
        (await _desk.AskAsync(new AskRequest("work", sentence), Now)).Ask!;

    /// <summary>The ask published to <c>reports</c> in an intake's words, with these requirements.</summary>
    private Task<AskOutcome> Publish(Ask ask, params QuestRequirement[] requirements) =>
        _desk.PublishAsync(ask.Id, "reports", Now, draft: new AskDraft("Build the daily report", "Reached through the bridge.")
        {
            Requirements = requirements,
        });

    /// <summary>A requirement quoting the person's own sentence is published and read back from the quest.</summary>
    [Fact]
    public async Task A_requirement_quoting_the_asks_words_is_published_and_kept_on_the_quest()
    {
        var ask = await Asked();
        var bridge = new QuestRequirement("this will need the v3 bridge", "The report opens in the older shell through the bridge's route.");

        var published = await Publish(ask, bridge);

        Assert.Equal(AskRefusal.None, published.Refusal);
        Assert.Equal([bridge], published.Quest!.Requirements);
        Assert.Equal([bridge], (await _quests.FindAsync(published.Quest.Id))!.Requirements);
        Assert.Contains("1 requirement", published.Message);
    }

    /// <summary>Verbatim within whitespace and case: the person's spacing and capitals are not their words.</summary>
    [Fact]
    public async Task A_quote_is_matched_whatever_its_spacing_and_case()
    {
        var ask = await Asked();

        var published = await Publish(ask, new QuestRequirement("  This WILL need\n the  V3   bridge ", "  It opens through the bridge.\n"));

        Assert.Equal(AskRefusal.None, published.Refusal);
        // Kept as given, trimmed at the ends: the quote is what was checked, and nothing rewrites it.
        Assert.Equal(new QuestRequirement("This WILL need\n the  V3   bridge", "It opens through the bridge."), Assert.Single(published.Quest!.Requirements));
    }

    /// <summary>
    /// What the person said after the ask is theirs too (DRIFT1a): an answer to a parked session and a message
    /// added to a running one may each be quoted.
    /// </summary>
    [Fact]
    public async Task An_answer_or_an_added_message_kept_on_the_ask_may_be_quoted()
    {
        var ask = await Asked();
        await _asks.RecordWordAsync(ask.Id, new AskWord(AskWordKind.Answered, "we should be using the v3 common-report", Now, "s1", "q1"), Now);
        await _asks.RecordWordAsync(ask.Id, new AskWord(AskWordKind.Added, "add any missing feature into the common-report module", Now, "s2", "q1"), Now);

        var published = await Publish(
            ask,
            new QuestRequirement("using the v3 common-report", "The report is a common-report configuration."),
            new QuestRequirement("into the common-report module", "A missing feature lands in the module, not in the report."));

        Assert.Equal(AskRefusal.None, published.Refusal);
        Assert.Equal(2, published.Quest!.Requirements.Count);
    }

    /// <summary>
    /// 🔴 A quote the person never said is refused, naming the words it could not find, and nothing is
    /// published: a paraphrase in a requirement is what drifted.
    /// </summary>
    [Fact]
    public async Task A_quote_found_in_none_of_the_persons_words_is_refused_naming_it()
    {
        var ask = await Asked();

        var refused = await Publish(ask, new QuestRequirement("make it reachable through the v3 bridge", "It opens in the older shell."));

        Assert.Equal(AskRefusal.QuestRefused, refused.Refusal);
        Assert.Contains("\"make it reachable through the v3 bridge\"", refused.Message);
        Assert.Contains($"ask `#{ask.Id}`", refused.Message);
        Assert.Empty(await _quests.ListAsync(includeClosed: true));
        Assert.Empty((await _desk.FindAsync(ask.Id))!.Quests);
    }

    /// <summary>Of several, each quote that is not found is named, and none that is.</summary>
    [Fact]
    public async Task Every_quote_not_found_is_named_and_none_that_was()
    {
        var ask = await Asked();

        var refused = await _exchange.PublishAsync(
            new QuestAsk(AskDesk.SenderOf(ask.Id), "reports", "Build it", "Why.")
            {
                Workspace = "work",
                Requirements =
                [
                    new("use the bridge's own route", "a"),
                    new("the v3 bridge", "b"),
                    new("from the v3 page", "c"),
                ],
            },
            Now);

        Assert.Equal(QuestPublishRefusal.NotQuoted, refused.Refusal);
        Assert.Contains("requirement 1: \"use the bridge's own route\"", refused.Message);
        Assert.Contains("requirement 3: \"from the v3 page\"", refused.Message);
        Assert.DoesNotContain("requirement 2", refused.Message);
        Assert.Null(refused.Quest);
    }

    /// <summary>A quote stands in one thing the person said: two of their sentences joined are words they never said together.</summary>
    [Fact]
    public async Task A_quote_spanning_two_of_the_persons_words_is_refused()
    {
        var ask = await Asked("use the bridge");
        await _asks.RecordWordAsync(ask.Id, new AskWord(AskWordKind.Answered, "for the common-report", Now, "s1", "q1"), Now);

        var refused = await Publish(ask, new QuestRequirement("use the bridge for the common-report", "It goes through the bridge."));

        Assert.Equal(AskRefusal.QuestRefused, refused.Refusal);
        Assert.Contains("\"use the bridge for the common-report\"", refused.Message);
    }

    /// <summary>A requirement is both halves: the person's words and the check that proves them. Each one missing is named.</summary>
    [Theory]
    [InlineData("", "It opens through the bridge.", "Requirement 1")]
    [InlineData("the v3 bridge", "   ", "Requirement 1")]
    public async Task A_requirement_without_its_quote_or_its_check_is_refused_naming_which(string quote, string check, string named)
    {
        var ask = await Asked();

        var refused = await _exchange.PublishAsync(
            new QuestAsk(AskDesk.SenderOf(ask.Id), "reports", "Build it", "Why.") { Workspace = "work", Requirements = [new(quote, check)] },
            Now);

        Assert.Equal(QuestPublishRefusal.BadRequirement, refused.Refusal);
        Assert.Contains(named, refused.Message);
        Assert.Contains("check", refused.Message);
    }

    /// <summary>A quest carries a bounded number of requirements, each of a bounded length, so the record stays one a session can be handed.</summary>
    [Fact]
    public async Task Too_many_requirements_or_one_too_long_are_refused()
    {
        var ask = await Asked();
        QuestAsk With(IReadOnlyList<QuestRequirement> requirements) =>
            new(AskDesk.SenderOf(ask.Id), "reports", "Build it", "Why.") { Workspace = "work", Requirements = requirements };

        var many = await _exchange.PublishAsync(
            With([.. Enumerable.Range(0, QuestExchange.MaxRequirements + 1).Select(_ => new QuestRequirement("the v3 bridge", "It opens."))]), Now);
        var longCheck = await _exchange.PublishAsync(
            With([new QuestRequirement("the v3 bridge", new string('x', QuestExchange.MaxRequirementLength + 1))]), Now);

        Assert.Equal(QuestPublishRefusal.BadRequirement, many.Refusal);
        Assert.Contains($"at most {QuestExchange.MaxRequirements}", many.Message);
        Assert.Equal(QuestPublishRefusal.BadRequirement, longCheck.Refusal);
        Assert.Contains($"longer than {QuestExchange.MaxRequirementLength} characters", longCheck.Message);
    }

    /// <summary>
    /// A quest one repository asks of another is asked on no ask, so there are no words of the person's to
    /// quote, and the refusal says where what is needed goes instead.
    /// </summary>
    [Fact]
    public async Task A_requirement_on_a_quest_no_ask_asked_is_refused_saying_why()
    {
        var refused = await _exchange.PublishAsync(
            new QuestAsk("checker", "reports", "Expose a hook", "So it can be checked.") { Requirements = [new("a hook", "It is there.")] },
            Now);

        Assert.Equal(QuestPublishRefusal.BadRequirement, refused.Refusal);
        Assert.Contains("`checker`", refused.Message);
        Assert.Contains("body", refused.Message);
        Assert.Empty(await _quests.ListAsync(includeClosed: true));
    }

    /// <summary>A host that keeps no asks has no words to check a quote against, and refuses rather than keep it unchecked.</summary>
    [Fact]
    public async Task A_host_with_no_asks_refuses_a_requirement_rather_than_keep_it_unchecked()
    {
        var ask = await Asked();
        var blind = new QuestExchange(_service, _quests);

        var refused = await blind.PublishAsync(
            new QuestAsk(AskDesk.SenderOf(ask.Id), "reports", "Build it", "Why.") { Workspace = "work", Requirements = [new("the v3 bridge", "It opens.")] },
            Now);

        Assert.Equal(QuestPublishRefusal.BadRequirement, refused.Refusal);
        Assert.Contains($"ask `#{ask.Id}`", refused.Message);
    }

    /// <summary>
    /// Old clients keep working: a publish that names no requirements is published exactly as before, by
    /// every door, and its quest carries none.
    /// </summary>
    [Fact]
    public async Task A_publish_without_requirements_is_published_as_before()
    {
        var ask = await Asked();

        var byDesk = await _desk.PublishAsync(ask.Id, "reports", Now);
        var byRepository = await _exchange.PublishAsync(new QuestAsk("checker", "reports", "Expose a hook", "So it can be checked."), Now);

        Assert.Equal(AskRefusal.None, byDesk.Refusal);
        Assert.Empty(byDesk.Quest!.Requirements);
        Assert.Equal(QuestPublishRefusal.None, byRepository.Refusal);
        Assert.Empty(byRepository.Quest!.Requirements);
        Assert.DoesNotContain("requirement", byRepository.Message);
    }

    /// <summary>
    /// A follow-up inherits its parent's requirements: the verifying step is measured by what the person
    /// asked, not by the build's closing note (§4.5), and so is every step after it.
    /// </summary>
    [Fact]
    public async Task A_chain_step_inherits_its_parents_requirements_all_the_way_down()
    {
        var ask = await Asked();
        var bridge = new QuestRequirement("this will need the v3 bridge", "The report opens through the bridge's route.");
        var built = (await _desk.PublishAsync(ask.Id, "reports", Now, draft: new AskDraft("Build the daily report", "Through the bridge.")
        {
            Requirements = [bridge],
            Then =
            [
                new QuestStep("checker", "Verify {parent} in the browser", "Open it and look."),
                new QuestStep("reports", "Report on {parent}", "Say what was found."),
            ],
        })).Quest!;

        // Each done answers the requirement it carries (DRIFT1d): met, so nothing holds the chain.
        QuestAnswer[] met = [new(1, Met: "It opens through the bridge's route.")];
        await _exchange.RespondAsync(built.Id, "take", null, Now);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(built.Id, "done", "Built.", Now, answers: met)).Refusal);
        var step = (await _quests.FromAsync(AskDesk.SenderOf(ask.Id))).Single(quest => quest.Parent == built.Id);
        await _exchange.RespondAsync(step.Id, "take", null, Now);
        await _exchange.RespondAsync(step.Id, "done", "Verified.", Now, answers: met);
        var report = (await _quests.FromAsync(AskDesk.SenderOf(ask.Id))).Single(quest => quest.Parent == step.Id);

        Assert.Equal([bridge], step.Requirements);
        Assert.Equal([bridge], report.Requirements);
        // Carried in the step's own history, so a replay on any machine says the same.
        Assert.Equal([bridge], QuestLog.Replay(await _quests.HistoryAsync(step.Id))!.Requirements);
    }

    /// <summary>A quest's requirements survive its history: the log replays them, and the wire carries them; a quest with none crosses as it did.</summary>
    [Fact]
    public async Task A_quests_requirements_are_in_its_history_and_cross_the_wire()
    {
        var ask = await Asked();
        var bridge = new QuestRequirement("the v3 bridge", "It opens through the bridge.");
        var quest = (await Publish(ask, bridge)).Quest!;
        var history = await _quests.HistoryAsync(quest.Id);

        Assert.Equal([bridge], QuestLog.Replay(history)!.Requirements);
        var pushed = QuestWire.ReadPush(QuestWire.Push(0, history))!.Value;
        Assert.Equal([bridge], pushed.Operations[0].Published!.Requirements);

        var plain = (await _exchange.PublishAsync(new QuestAsk("checker", "reports", "Expose a hook", "b"), Now)).Quest!;
        Assert.DoesNotContain("requirements", QuestWire.Push(0, await _quests.HistoryAsync(plain.Id)));
    }

    /// <summary>A requirement half-made on the wire is an operation not whole, never one replayed with a blank half.</summary>
    [Fact]
    public void A_requirement_on_the_wire_without_its_check_makes_the_operation_not_whole()
    {
        const string Push = """
            { "base": 0, "operations": [{ "machine": "m1", "sequence": 1, "quest": "abcdefabcdef", "kind": "published",
              "at": "2026-10-03T09:00:00Z",
              "asked": { "from": "ask #a1b2c3", "to": "reports", "title": "t", "body": "b", "links": [], "attachments": [], "then": [] REQUIRED } }] }
            """;
        const string HalfMade = """, "requirements": [{ "quote": "the v3 bridge" }]""";

        Assert.Null(QuestWire.ReadPush(Push.Replace("REQUIRED", HalfMade)));
        // A publish from a build before requirements names none, and reads as a quest with none.
        Assert.Empty(QuestWire.ReadPush(Push.Replace("REQUIRED", ""))!.Value.Operations[0].Published!.Requirements);
    }

    /// <summary>
    /// A remote holds no asks, so it cannot check a quote — that was judged where the ask is — but a
    /// requirement with half its words is no requirement, and a record naming one would lie.
    /// </summary>
    [Fact]
    public async Task A_remote_refuses_a_pushed_quest_whose_requirement_is_half_made()
    {
        var registered = await _service.RegistryAsync();
        var asked = new Quest("abcdefabcdef", "ask #a1b2c3", "reports", "t", "b", QuestStatus.Open, null, Now, Now, "work");

        Assert.Null(_exchange.JudgeReceived(asked with { Requirements = [new("the v3 bridge", "It opens.")] }, registered));
        Assert.Contains("Requirement 1", _exchange.JudgeReceived(asked with { Requirements = [new("the v3 bridge", " ")] }, registered));
    }

    /// <summary>
    /// An ask from before its words were kept (DRIFT1a) holds its sentence, and nothing the person said on
    /// it before then: the refusal says from when its words are kept, rather than reading as though nothing
    /// more was said.
    /// </summary>
    [Fact]
    public async Task A_refusal_on_an_ask_from_before_its_words_were_kept_says_from_when()
    {
        var ask = await Asked();
        await using (var mark = _connection.CreateCommand())
        {
            mark.CommandText = "UPDATE asks SET words_kept_from = '2026-10-02T12:00:00Z' WHERE id = $id";
            mark.Parameters.AddWithValue("$id", ask.Id);
            await mark.ExecuteNonQueryAsync();
        }

        var refused = await Publish(ask, new QuestRequirement("use the common-report", "It is a configuration."));

        Assert.Equal(AskRefusal.QuestRefused, refused.Refusal);
        Assert.Contains("2026-10-02 12:00 UTC", refused.Message);
    }
}
