using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The plan door: a part is a quest published under <c>(ask, part)</c>, so a retried write returns the same quest and two parts to one
/// repository are two; a plan with a cycle, a missing reference or a claim on a record is saved with the flag; a worker's split is
/// depth 2 and a stale write is refused. All in memory, so each runs in milliseconds.
/// </summary>
public sealed class TeamDeskTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-team-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-13T08:00:00Z");
    private const string Sentence = "make the media service read its field name from config and show it on the settings page";

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private AskStore _askStore = null!;
    private AskDesk _asks = null!;
    private TeamDesk _desk = null!;
    private Ask _ask = null!;

    public async Task InitializeAsync()
    {
        var dir = Path.Combine(_root, "family", "media-api");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"),
            """{ "source": "s", "packs": [], "domain": { "summary": "The media service.", "owns": ["media config"], "accepts": ["a media bug"] } }""");

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);
        var sessions = await SessionStore.OpenAsync(_connection);
        var asks = _askStore = await AskStore.OpenAsync(_connection);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.ImportAsync(Path.Combine(_root, "family"), Now);
        foreach (var row in await service.RegistryAsync()) await service.RegisterAsync(row with { Workspace = "work" }, Now);

        var files = new QuestFiles(Path.Combine(_root, "home"));
        _asks = new AskDesk(service, asks, new QuestExchange(service, _quests, remotes: null, files, sessions, asks), files, sessions);
        _desk = new TeamDesk(
            await TeamStore.OpenAsync(_connection), await TeamLedger.OpenAsync(_connection), _asks,
            (_, _) => Task.FromResult(new TeamPathPolicy(["TASKS.md", "docs/areas/**"], ["src/gen/**"])));
        _ask = (await _asks.AskAsync(new AskRequest("work", Sentence), Now)).Ask!;
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static TeamPart Part(string id, params string[] claims) =>
        new(id, "media-api", $"Part {id}", $"Do {id}, and show it.", $"{id} works on the page") { Claims = claims };

    private TeamSave Save(IReadOnlyList<TeamPart> parts, IReadOnlyList<TeamEdge>? edges = null, int expect = 0) =>
        new(_ask.Id, parts, edges ?? [], "split along what a person sees") { Expect = expect };

    [Fact]
    public async Task A_retried_write_returns_the_quest_each_part_already_is_even_reworded()
    {
        var first = await _desk.SaveAsync(Save([Part("keel")]), Now);
        var quest = first.Published.Single().Quest;
        Assert.NotNull(quest);

        var reworded = Part("keel") with { Title = "A new title", Body = "other words" };
        var again = await _desk.SaveAsync(Save([reworded], expect: 1), Now.AddMinutes(1));

        Assert.Equal(quest, again.Published.Single().Quest);
        Assert.Single(await _quests.FromAsync(AskDesk.SenderOf(_ask.Id)));
        Assert.Equal(2, again.Plan!.Revision);
        var kept = (await _quests.FromAsync(AskDesk.SenderOf(_ask.Id))).Single();
        Assert.Equal("A new title", kept.Title);
        Assert.Equal("other words", kept.Body);
        Assert.True(again.Published.Single().Revised);
        Assert.Contains("reworded", again.Message);
    }

    [Fact]
    public async Task A_revision_that_rescopes_a_part_changes_its_quest_paths_and_one_that_says_the_same_changes_nothing()
    {
        var first = await _desk.SaveAsync(Save([Part("keel", "src/a/**")]), Now);
        var quest = first.Published.Single().Quest!;

        var rescoped = await _desk.SaveAsync(Save([Part("keel", "src/b/**")], expect: 1), Now.AddMinutes(1));
        Assert.True(rescoped.Published.Single().Revised);
        Assert.Equal(["src/b/**"], (await _quests.FindAsync(quest))!.Paths);

        var same = await _desk.SaveAsync(Save([Part("keel", "src/b/**")], expect: 2), Now.AddMinutes(2));
        Assert.False(same.Published.Single().Revised);
    }

    [Fact]
    public async Task A_revision_of_a_part_whose_quest_is_closed_leaves_the_quest_as_it_closed()
    {
        var first = await _desk.SaveAsync(Save([Part("keel")]), Now);
        var quest = first.Published.Single().Quest!;
        await _quests.DeleteAsync(quest, Now.AddSeconds(30), travels: false);

        var again = await _desk.SaveAsync(Save([Part("keel") with { Title = "Reworded" }], expect: 1), Now.AddMinutes(1));

        Assert.False(again.Published.Single().Revised);
        Assert.Contains("not reworded", again.Message);
    }

    [Fact]
    public async Task Two_parts_to_one_repository_publish_two_quests()
    {
        var saved = await _desk.SaveAsync(Save([Part("api", "src/api/**"), Part("page", "src/page/**")]), Now);

        Assert.All(saved.Published, each => Assert.NotNull(each.Quest));
        Assert.Equal(2, saved.Published.Select(each => each.Quest).Distinct().Count());
        Assert.Equal(2, (await _quests.FromAsync(AskDesk.SenderOf(_ask.Id))).Count);
    }

    [Fact]
    public async Task A_cycle_a_missing_reference_and_a_claim_on_a_record_or_generated_path_are_flagged_and_saved()
    {
        var edges = new TeamEdge[]
        {
            new("a", "b", TeamEdge.After, null, "a needs b"), new("b", "a", TeamEdge.After, null, "b needs a"),
            new("a", "ghost", TeamEdge.After, null, "a needs a part nobody wrote"),
        };
        var saved = await _desk.SaveAsync(Save([Part("a", "TASKS.md", "src/a/**", "src/gen/x.ts"), Part("b", "docs/areas/driving.md")], edges), Now);

        Assert.Equal(TeamRefusal.None, saved.Refusal);
        var kinds = saved.Plan!.Flags.Select(flag => flag.Kind).ToList();
        Assert.Contains(TeamFlags.Cycle, kinds);
        Assert.Contains(TeamFlags.MissingReference, kinds);
        Assert.Contains(TeamFlags.ClaimRecord, kinds);
        Assert.Contains(TeamFlags.ClaimGenerated, kinds);
        Assert.Equal(["src/a/**"], saved.Plan.Parts[0].Claims);
        Assert.Empty(saved.Plan.Parts[1].Claims);
        Assert.Contains("integrator", saved.Plan.Flags.First(flag => flag.Kind == TeamFlags.ClaimRecord).Text);
        Assert.Equal(1, (await _desk.ReadAsync(_ask.Id)).Plans.Single().Revision);
    }

    [Fact]
    public async Task Two_parallel_parts_claiming_one_path_are_ordered_and_it_is_said()
    {
        var saved = await _desk.SaveAsync(Save([Part("one", "src/shared/**"), Part("two", "src/shared/util.ts"), Part("three", "src/other/**")]), Now);

        var order = Assert.Single(saved.Plan!.Edges);
        Assert.Equal(("two", "one", TeamEdge.Orders), (order.Part, order.On, order.Kind));
        Assert.Contains(saved.Plan.Flags, flag => flag.Kind == TeamFlags.ClaimOverlap && flag.Part == "two");
    }

    [Fact]
    public async Task A_write_over_a_newer_revision_than_the_one_read_is_refused()
    {
        await _desk.SaveAsync(Save([Part("keel")]), Now);

        var stale = await _desk.SaveAsync(Save([Part("keel"), Part("late")]), Now.AddMinutes(1));

        Assert.Equal(TeamRefusal.Moved, stale.Refusal);
    }

    [Fact]
    public async Task A_workers_split_of_its_own_part_counts_depth_two_and_its_parts_are_quests_too()
    {
        var top = await _desk.SaveAsync(Save([Part("feature")]), Now);
        var quest = top.Published.Single().Quest!;
        var worker = new Session("worker", quest, "media-api", "stub", SessionState.Working, null, null, null, Now, Now);

        var (ask, scope) = (await _desk.ScopeOfAsync(worker))!.Value;
        var split = await _desk.SaveAsync(
            new TeamSave(ask, [Part("feature.api", "src/api/**"), Part("feature.page", "src/page/**")], [], "three splits of my own") { Scope = scope },
            Now.AddMinutes(2));

        Assert.Equal(2, split.Plan!.Depth);
        Assert.All(split.Plan.Parts, part => Assert.Equal("feature", part.Parent));
        Assert.Equal(3, (await _quests.FromAsync(AskDesk.SenderOf(_ask.Id))).Count);
        Assert.Equal(2, (await _desk.ReadAsync(_ask.Id)).Plans.Count);
    }

    [Fact]
    public async Task The_session_that_wrote_a_plan_is_its_lead_and_each_revision_is_a_decision_with_its_reason()
    {
        await _desk.SaveAsync(Save([Part("keel")]) with { By = "lead-1" }, Now);
        await _desk.SaveAsync(Save([Part("keel"), Part("page")], expect: 1) with { By = "lead-2" }, Now.AddMinutes(5));
        await _desk.SaveAsync(Save([Part("keel"), Part("page")], expect: 2) with { By = "lead-2" }, Now.AddMinutes(6));

        var (members, decisions) = await _desk.TeamAsync(_ask.Id);

        Assert.Equal([("lead-1", 1), ("lead-2", 3)], members.Select(member => (member.Session, member.Revision)));
        Assert.All(members, member => Assert.Equal((TeamMember.Lead, (string?)null), (member.Role, member.Part)));
        Assert.Equal(["planned", "revised", "revised"], decisions.Select(decision => decision.Decision));
        Assert.Equal("split along what a person sees", decisions[1].Reason);
        Assert.Contains("keel", decisions[0].Act);
    }

    [Fact]
    public async Task A_plan_written_by_the_asks_own_intake_is_published_by_it_and_a_hand_written_one_is_not()
    {
        await _askStore.RecordIntakeAsync(_ask.Id, "intake-1", Now);

        await _desk.SaveAsync(Save([Part("api", "src/api/**")]), Now);
        Assert.NotEqual(AskDesk.ByIntake, (await _asks.FindAsync(_ask.Id))!.Tier);

        var byLead = await _desk.SaveAsync(Save([Part("api", "src/api/**"), Part("page", "src/page/**")], expect: 1) with { By = "intake-1" }, Now.AddMinutes(1));

        Assert.Equal(AskDesk.ByIntake, (await _asks.FindAsync(_ask.Id))!.Tier);
        Assert.All(byLead.Published, each => Assert.NotNull(each.Quest));
    }

    [Fact]
    public async Task A_workers_split_makes_it_the_lead_of_its_part_and_a_hand_written_plan_adds_no_member()
    {
        var top = await _desk.SaveAsync(Save([Part("feature")]), Now);
        await _desk.SaveAsync(new TeamSave(_ask.Id, [Part("feature.api")], [], "my own split") { Scope = "feature", By = "worker-1" }, Now.AddMinutes(1));

        var (members, decisions) = await _desk.TeamAsync(_ask.Id);

        var member = Assert.Single(members);
        Assert.Equal(("worker-1", "feature"), (member.Session, member.Part));
        Assert.Equal(["person", "worker-1"], decisions.Select(decision => decision.Author));
        Assert.Equal("feature", decisions[1].Scope);
        Assert.Equal(1, top.Plan!.Revision);
    }

    [Fact]
    public async Task A_split_of_a_part_no_plan_holds_is_refused()
    {
        var split = await _desk.SaveAsync(Save([Part("x")]) with { Scope = "nowhere" }, Now);

        Assert.Equal(TeamRefusal.Unfit, split.Refusal);
    }

    [Fact]
    public async Task The_connector_tool_writes_a_plan_and_reads_it_back_with_the_pointer_that_moved()
    {
        var tool = new Daoris.Knowledge.Mcp.TeamTools(_desk);
        var pack = new PartPack { Pointers = [new("change", "src/a.cs", "1-9", "bbb"), new("area", "docs/areas/x.md", "", "ccc")] };

        var wrote = await tool.PlanAsync(_ask.Id, [Part("keel") with { Pack = pack }], why: "one part is a plan");
        var read = await tool.PlanAsync(_ask.Id, seen: new Dictionary<string, string> { ["src/a.cs"] = "new", ["docs/areas/x.md"] = "ccc" });

        Assert.Contains("is quest `#", wrote);
        Assert.Contains("`keel` in `media-api`, quest `#", read);
        Assert.Contains("stale: `src/a.cs`", read);
        Assert.DoesNotContain("stale: `docs/areas/x.md`", read);
        var whole = System.Text.Json.JsonDocument.Parse(await tool.PlanAsync(_ask.Id, json: true)).RootElement;
        Assert.Equal("keel", whole.GetProperty("plans")[0].GetProperty("parts")[0].GetProperty("id").GetString());
        Assert.True(whole.GetProperty("quests").TryGetProperty("keel", out _));
        Assert.Contains("write one", await new Daoris.Knowledge.Mcp.TeamTools(_desk).PlanAsync("nope"));
    }

    [Fact]
    public async Task The_delivery_graph_is_kept_with_the_plan_and_read_back_with_the_edge_that_moved_and_each_question_once()
    {
        var schema = new DeliveryRef("schema-owner", "config-migration");
        var mirror = new DeliveryRef("backend", "mirror-api");
        var deployables = new DeliveryDeployable[]
        {
            new(schema, new("schema-owner", "daoris.json", "", "b1")) { Provides = [new("config-schema", DeliveryUse.Migrates)] },
            new(mirror, new("backend", "daoris.json", "", "b2")) { Consumes = [new("config-schema", DeliveryUse.Mirror)] },
        };
        var asked = new DeliveryQuestion("contract:viewers", DeliveryQuestion.Open, "Viewers or admins?");
        var tool = new Daoris.Knowledge.Mcp.TeamTools(_desk);

        var wrote = await tool.PlanAsync(_ask.Id, [Part("keel")], why: "plan", deployables: deployables, questions: [asked, asked]);
        var read = await tool.PlanAsync(_ask.Id, seen: new Dictionary<string, string> { ["backend:daoris.json"] = "moved" });

        Assert.Contains("2 deployable(s) with 1 release order(s) and 1 question(s)", wrote);
        var plan = (await _desk.ReadAsync(_ask.Id)).Plans.Single();
        Assert.Equal(DeliveryEdge.Declaration, plan.Releases.Single().Source);
        Assert.Equal(new DeliveryEvidence("backend", "daoris.json", "", "b2"), plan.Releases.Single().Evidence);
        Assert.Equal(deployables.Select(each => each.Ref), plan.Deployables.Select(each => each.Ref));
        Assert.Contains("release `backend/mirror-api` after `schema-owner/config-migration`", read);
        Assert.Contains("STALE", read);
        Assert.Equal(2, read.Split("question `").Length - 1);
        var split = await _desk.SaveAsync(Save([Part("x")], expect: 0) with { Scope = "keel", Deployables = deployables }, Now);
        Assert.Equal(TeamRefusal.Unfit, split.Refusal);
    }

    [Fact]
    public void A_moved_pointer_marks_its_pack_stale_alone()
    {
        var pack = new PartPack
        {
            Pointers = [new("area", "docs/areas/driving.md", "10-40", "aaa"), new("change", "src/a.cs", "1-9", "bbb"), new("change", "src/b.cs", "", "ccc")],
        };

        var moved = PackStaleness.Moved(pack, new Dictionary<string, string> { ["docs/areas/driving.md"] = "aaa", ["src/a.cs"] = "zzz" });

        Assert.Equal(["src/a.cs"], moved.Select(pointer => pointer.File));
        Assert.Empty(PackStaleness.Moved(null, new Dictionary<string, string>()));
    }
}
