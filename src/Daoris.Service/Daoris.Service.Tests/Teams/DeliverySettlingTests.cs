using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The person's two acts on a delivery plan: an answer settles a question wherever it is asked and survives the lead's next revision; a waiver
/// keeps an order, marks it, and no longer lets it close a circle or go stale. In memory, so each runs in milliseconds.
/// </summary>
public sealed class DeliverySettlingTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-14T08:00:00Z");
    private static readonly DeliveryRef Schema = new("schema-owner", "migration"), Mirror = new("backend", "mirror-api"), Web = new("frontend", "web-app");

    private SqliteConnection _connection = null!;
    private TeamDesk _desk = null!;
    private AskDesk _asks = null!;
    private Ask _ask = null!;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-settle-" + Guid.NewGuid().ToString("N")[..8]);

    public async Task InitializeAsync()
    {
        var dir = Path.Combine(_root, "family", "media-api");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"),
            """{ "source": "s", "packs": [], "domain": { "summary": "The media service.", "owns": ["media config"], "accepts": ["a media bug"] } }""");
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var quests = await QuestStore.OpenAsync(_connection);
        var sessions = await SessionStore.OpenAsync(_connection);
        var asks = await AskStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync()) await service.RegisterAsync(row with { Workspace = "work" }, Now);
        var files = new QuestFiles(Path.Combine(_root, "home"));
        _asks = new AskDesk(service, asks, new QuestExchange(service, quests, remotes: null, files, sessions, asks), files, sessions);
        _desk = new TeamDesk(await TeamStore.OpenAsync(_connection), await TeamLedger.OpenAsync(_connection), _asks, (_, _) => Task.FromResult(TeamPathPolicy.None));
        _ask = (await _asks.AskAsync(new AskRequest("work", "make the media service read its field name from config and show it on the settings page"), Now)).Ask!;
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static DeliveryDeployable Declared(DeliveryRef each, params DeliveryUse[] consumes) =>
        new(each, new(each.Repository, "daoris.json", "", "blob-" + each.Repository)) { Consumes = consumes };

    private static DeliveryEdge Order(DeliveryRef later, DeliveryRef earlier, string repository, string blob) =>
        new(later, earlier, $"{later.Id} reads what {earlier.Id} changes.", DeliveryEdge.CodeRead, new(repository, "src/client.ts", "1-9", blob));

    private static TeamPart Part() => new("keel", "media-api", "Keel", "Do it.", "it works on the page");

    private Task<TeamOutcome> Plan(IReadOnlyList<DeliveryEdge> releases, IReadOnlyList<DeliveryQuestion> questions, int expect = 0) =>
        _desk.SaveAsync(
            new TeamSave(_ask.Id, [Part()], [], "plan") { Expect = expect, Deployables = [Declared(Schema), Declared(Mirror), Declared(Web)], Releases = releases, Questions = questions },
            Now);

    private static DeliveryQuestion Viewers() =>
        new("contract:viewers", DeliveryQuestion.Open, "Do admins or viewers see candidates?") { Yes = "Both see them.", Blocks = "The web app's release.", Default = "Admins only." };

    [Fact]
    public void A_waived_order_no_longer_closes_a_circle_or_goes_stale()
    {
        var there = Order(Mirror, Schema, "backend", "b1");
        var back = Order(Schema, Mirror, "schema-owner", "s1");
        DeliveryDeployable[] deployables = [Declared(Schema), Declared(Mirror)];
        Assert.Contains(DeliveryGraph.Judge(deployables, [there, back], []).Questions, question => question.Kind == DeliveryQuestion.Cycle);

        var waiver = new DeliveryWaiver(Schema, Mirror, "The schema change is additive.", Now);
        var judged = DeliveryGraph.Judge(deployables, [there, back], [], [waiver]);

        Assert.Empty(judged.Questions);
        Assert.DoesNotContain(judged.Flags, flag => flag.Kind == TeamFlags.Cycle);
        Assert.Equal("The schema change is additive.", judged.Edges.Single(edge => edge.Deployable == Schema).Waived);
        Assert.Empty(DeliveryGraph.Stale(judged.Edges, new Dictionary<string, string> { ["schema-owner:src/client.ts"] = "moved" }));
    }

    [Fact]
    public void An_answered_question_is_not_asked_and_a_second_move_of_the_evidence_asks_again()
    {
        var edge = Order(Mirror, Schema, "backend", "b1");
        var first = new Dictionary<string, string> { ["backend:src/client.ts"] = "b2" };
        var stale = DeliveryGraph.Queue([], [edge], first).Single();
        var answer = new DeliveryAnswer(stale.Key, "The order stands.", Now);

        Assert.Empty(DeliveryGraph.Queue([], [edge], first, [answer]));
        Assert.Single(DeliveryGraph.Queue([], [edge], new Dictionary<string, string> { ["backend:src/client.ts"] = "b3" }, [answer]));
        Assert.Empty(DeliveryGraph.Queue([Viewers()], [], new Dictionary<string, string>(), [new("contract:viewers", "admins", Now)]));
    }

    [Fact]
    public async Task An_answer_is_kept_on_the_next_revision_settles_the_question_and_outlives_the_leads_rewrite()
    {
        await Plan([], [Viewers()]);
        Assert.Equal(["contract:viewers"], (await _desk.Settling.OpenQuestionsAsync([_ask.Id]))[_ask.Id].Select(question => question.Key));

        var answered = await _desk.Settling.AnswerAsync(_ask.Id, "contract:viewers", "  Admins only.  ", Now.AddMinutes(1));

        Assert.Equal(TeamRefusal.None, answered.Refusal);
        var plan = (await _desk.ReadAsync(_ask.Id)).Plans.Single();
        Assert.Equal(2, plan.Revision);
        Assert.Null(plan.By);
        Assert.Equal("Admins only.", plan.Answers.Single().Words);
        Assert.Empty(await _desk.Settling.OpenQuestionsAsync([_ask.Id]));

        var rewritten = await Plan([], [Viewers()], expect: 2);
        Assert.Equal(TeamRefusal.None, rewritten.Refusal);
        Assert.Empty(await _desk.Settling.OpenQuestionsAsync([_ask.Id]));
        Assert.Equal("Admins only.", (await _desk.ReadAsync(_ask.Id)).Plans.Single().Answers.Single().Words);
        Assert.Contains((await _desk.TeamAsync(_ask.Id)).Decisions, decision => decision.Author == "person" && decision.Revision == 2);
    }

    [Fact]
    public async Task An_answer_to_nothing_asked_or_in_no_words_is_refused_and_nothing_is_kept()
    {
        await Plan([], [Viewers()]);

        Assert.Equal(TeamRefusal.Unfit, (await _desk.Settling.AnswerAsync(_ask.Id, "contract:nobody", "yes", Now)).Refusal);
        Assert.Equal(TeamRefusal.Unfit, (await _desk.Settling.AnswerAsync(_ask.Id, "contract:viewers", "  ", Now)).Refusal);
        await _desk.Settling.AnswerAsync(_ask.Id, "contract:viewers", "Admins.", Now);
        Assert.Equal(TeamRefusal.Unfit, (await _desk.Settling.AnswerAsync(_ask.Id, "contract:viewers", "Again.", Now)).Refusal);
        Assert.Equal(TeamRefusal.NotFound, (await _desk.Settling.AnswerAsync("nope", "contract:viewers", "x", Now)).Refusal);
        Assert.Equal(2, (await _desk.ReadAsync(_ask.Id)).Plans.Single().Revision);
    }

    [Fact]
    public async Task A_stale_question_read_with_the_callers_blobs_is_answered_by_its_key()
    {
        await Plan([Order(Mirror, Schema, "backend", "b1")], []);
        var plan = (await _desk.ReadAsync(_ask.Id)).Plans.Single();
        var stale = DeliveryGraph.Open(plan, new Dictionary<string, string> { ["backend:src/client.ts"] = "b2" }).Single();

        Assert.Equal(TeamRefusal.None, (await _desk.Settling.AnswerAsync(_ask.Id, stale.Key, "Still holds.", Now)).Refusal);

        var after = (await _desk.ReadAsync(_ask.Id)).Plans.Single();
        Assert.Empty(DeliveryGraph.Open(after, new Dictionary<string, string> { ["backend:src/client.ts"] = "b2" }));
    }

    [Fact]
    public async Task A_waiver_is_kept_with_its_reason_settles_the_circle_and_is_carried_by_the_leads_rewrite()
    {
        var there = Order(Mirror, Schema, "backend", "b1");
        var back = Order(Schema, Mirror, "schema-owner", "s1");
        await Plan([there, back], []);
        var circle = (await _desk.ReadAsync(_ask.Id)).Plans.Single().Questions.Single(question => question.Kind == DeliveryQuestion.Cycle);
        Assert.Equal(circle.Key, (await _desk.Settling.OpenQuestionsAsync([_ask.Id]))[_ask.Id].Single().Key);

        var waived = await _desk.Settling.WaiveAsync(_ask.Id, Schema, Mirror, "The schema change is additive.", Now);

        Assert.Equal(TeamRefusal.None, waived.Refusal);
        var plan = (await _desk.ReadAsync(_ask.Id)).Plans.Single();
        Assert.Equal("The schema change is additive.", plan.Releases.Single(edge => edge.Deployable == Schema).Waived);
        Assert.DoesNotContain(plan.Flags, flag => flag.Kind == TeamFlags.Cycle);
        Assert.Empty(await _desk.Settling.OpenQuestionsAsync([_ask.Id]));

        await Plan([there, back], [], expect: plan.Revision);
        var again = (await _desk.ReadAsync(_ask.Id)).Plans.Single();
        Assert.NotNull(again.Releases.Single(edge => edge.Deployable == Schema).Waived);
        Assert.Empty(await _desk.Settling.OpenQuestionsAsync([_ask.Id]));
    }

    [Fact]
    public async Task A_waiver_needs_a_reason_and_an_order_the_plan_holds()
    {
        await Plan([Order(Mirror, Schema, "backend", "b1")], []);

        Assert.Equal(TeamRefusal.Unfit, (await _desk.Settling.WaiveAsync(_ask.Id, Mirror, Schema, " ", Now)).Refusal);
        Assert.Equal(TeamRefusal.Unfit, (await _desk.Settling.WaiveAsync(_ask.Id, Web, Schema, "why", Now)).Refusal);
        Assert.Equal(1, (await _desk.ReadAsync(_ask.Id)).Plans.Single().Revision);
    }

    [Fact]
    public async Task A_closed_ask_is_settled_no_more()
    {
        await Plan([], [Viewers()]);
        await _asks.CloseAsync(_ask.Id, "done", Now);

        Assert.Equal(TeamRefusal.Closed, (await _desk.Settling.AnswerAsync(_ask.Id, "contract:viewers", "Admins.", Now)).Refusal);
    }
}
