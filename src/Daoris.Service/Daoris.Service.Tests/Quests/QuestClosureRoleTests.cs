using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A role's yes accepts a departure alone: a done held for its unread evidence refuses it, and so does a departure beside a met
/// requirement whose evidence is awaited, which the one yes would lift with it; the person's yes still accepts either.
/// </summary>
public sealed class QuestClosureRoleTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-closure-role-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private QuestExchange _exchange = null!;
    private AskDesk _desk = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-17T09:00:00Z");

    private const string Graph = "sha256:0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0";

    private static readonly QuestRequirement Report =
        new("write the bridge report into docs/report-bridge.md", "The report is in docs/report-bridge.md.")
        {
            Evidence = [new QuestEvidence("docs/report-bridge.md")],
        };

    private static readonly QuestRequirement Bridge = new("through the v3 bridge", "The report opens through the bridge's route.");

    public async Task InitializeAsync()
    {
        var dir = Path.Combine(_root, "family", "reports");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"),
            """{ "source": "s", "packs": [], "domain": { "summary": "The reports.", "owns": ["report pages", "the v3 bridge"], "accepts": ["a report"] } }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        var asks = await AskStore.OpenAsync(_connection);

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
        _exchange = new QuestExchange(service, _quests, files: files, asks: asks);
        _desk = new AskDesk(service, asks, _exchange, files);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task A_roles_yes_never_accepts_a_done_held_for_its_evidence()
    {
        var ask = (await _desk.AskAsync(new AskRequest("work", "write the bridge report into docs/report-bridge.md"), Now)).Ask!;
        var published = await _desk.PublishAsync(ask.Id, "reports", Now, draft: new AskDraft("Write the bridge report", "Through the bridge.")
        {
            Requirements = [Report],
        });
        var id = published.Quest!.Id;
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(id, "take", null, Now)).Refusal);
        Assert.True((await _quests.SeedBound(id, Graph, Now)).Moved);
        var done = await _exchange.RespondAsync(id, "done", "Built.", Now, answers: [new QuestAnswer(1, Met: "Done as the check says.")]);
        Assert.Equal(QuestHold.EvidenceUnread, done.Quest!.Hold);

        var role = await _exchange.AcceptAsync(id, Now, by: "role:daoris-landing · codex-acp");

        Assert.Equal(QuestRespondRefusal.NotHeld, role.Refusal);
        Assert.Contains("a role's yes does not accept", role.Message, StringComparison.Ordinal);
        Assert.Equal(QuestHold.EvidenceUnread, (await _quests.FindAsync(id))!.Hold);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.AcceptAsync(id, Now)).Refusal);
    }

    /// <summary>🔴 A done departing from one requirement while another's evidence is awaited: its hold reads departed, yet a yes would lift the evidence with it.</summary>
    [Fact]
    public async Task A_roles_yes_never_accepts_a_departure_beside_evidence_still_awaited()
    {
        var ask = (await _desk.AskAsync(new AskRequest("work", "write the bridge report into docs/report-bridge.md through the v3 bridge"), Now)).Ask!;
        var published = await _desk.PublishAsync(ask.Id, "reports", Now, draft: new AskDraft("Write the bridge report", "Through the bridge.")
        {
            Requirements = [Report, Bridge],
        });
        var id = published.Quest!.Id;
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(id, "take", null, Now)).Refusal);
        var done = await _exchange.RespondAsync(id, "done", "Built.", Now, answers:
            [new QuestAnswer(1, Met: "Done as the check says."), new QuestAnswer(2, null, Departed: "It opens on its own page.", Quote: Bridge.Quote)]);
        Assert.Equal((QuestHold.Departed, true), (done.Quest!.Hold, done.Quest.AwaitsEvidence));

        var role = await _exchange.AcceptAsync(id, Now, by: "role:daoris-landing · codex-acp");

        Assert.Equal(QuestRespondRefusal.NotHeld, role.Refusal);
        Assert.Contains("a role's yes does not accept", role.Message, StringComparison.Ordinal);
        var standing = (await _quests.FindAsync(id))!;
        Assert.Equal((QuestHold.Departed, true), (standing.Hold, standing.AwaitsEvidence));
        Assert.Null(standing.Accepted);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.AcceptAsync(id, Now)).Refusal);
    }

    /// <summary>The driver's judge passed the departure: the service keeps the policy's yes with its rule and revision, judging nothing of it.</summary>
    [Fact]
    public async Task The_risk_policys_yes_accepts_a_departure_alone_kept_with_its_rule_and_never_lifts_evidence_still_awaited()
    {
        const string policy = "policy:work.done · rule seed at r1";
        var alone = await DoneAsync([Bridge], [new QuestAnswer(1, null, Departed: "It opens on its own page.", Quote: Bridge.Quote)]);
        var beside = await DoneAsync([Report, Bridge],
            [new QuestAnswer(1, Met: "Done as the check says."), new QuestAnswer(2, null, Departed: "It opens on its own page.", Quote: Bridge.Quote)]);

        var passed = await _exchange.AcceptAsync(alone, Now, by: policy);
        Assert.Equal(QuestRespondRefusal.None, passed.Refusal);
        Assert.Contains($"by {policy}", passed.Message, StringComparison.Ordinal);
        Assert.Null((await _quests.FindAsync(alone))!.Hold);
        Assert.Equal(policy, (await _quests.HistoryAsync(alone)).Last(each => each.Kind == QuestOperationKind.Accepted).By);

        var waits = await _exchange.AcceptAsync(beside, Now, by: policy);
        Assert.Equal(QuestRespondRefusal.NotHeld, waits.Refusal);
        Assert.Contains("the risk policy's yes does not lift: it waits on its check", waits.Message, StringComparison.Ordinal);
        Assert.Null((await _quests.FindAsync(beside))!.Accepted);
    }

    private async Task<string> DoneAsync(QuestRequirement[] requirements, QuestAnswer[] answers)
    {
        var ask = (await _desk.AskAsync(new AskRequest("work", "write the bridge report into docs/report-bridge.md through the v3 bridge"), Now)).Ask!;
        var published = await _desk.PublishAsync(ask.Id, "reports", Now, draft: new AskDraft("Write the bridge report", "Through the bridge.")
        {
            Requirements = requirements,
        });
        var id = published.Quest!.Id;
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.RespondAsync(id, "take", null, Now)).Refusal);
        Assert.Equal(QuestHold.Departed, (await _exchange.RespondAsync(id, "done", "Built.", Now, answers: answers)).Quest!.Hold);
        return id;
    }
}
