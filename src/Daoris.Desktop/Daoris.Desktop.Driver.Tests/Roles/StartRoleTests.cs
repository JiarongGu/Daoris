using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1x step 1 (the foundation design §2.4, §2.7): the start plan finds a start's role by its work and place from the role
/// catalogue, keeps the seeded role for <c>actor</c>, and a continuation of a record an added role started runs as that seeded work.
/// </summary>
public sealed class StartRoleTests : IDisposable
{
    private static readonly RolePlace Machine = new();
    private static readonly RolePlace InEnginePlace = new(Repository: "engine");
    private static readonly RoleScope InEngine = new(RoleScope.Repository, "engine");

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "rolestep1b-start-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (!Directory.Exists(_home)) return;
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private static readonly DriverConfig Config = DriverConfig.Empty with
    {
        Actors = new Dictionary<string, IReadOnlyList<ActorChoice>>
        {
            ["build"] = [new ActorChoice("claude", "sonnet")],
            ["design"] = [new ActorChoice("claude", "opus", "high")],
            ["designer"] = [new ActorChoice("codex", "opus", "high"), new ActorChoice("claude", "opus")],
        },
    };

    private static StartPlan Plan(string work, RoleCatalog catalogue, RolePlace place = default, IReadOnlyList<string>? fence = null) =>
        new(work, Config, null!, null!, new PassMemory(), fence: fence, catalogue: catalogue, place: place) { Subject = "quest:q1" };

    private static RoleCatalog WithDesigner(string work = "design", RoleScope? scope = null, long revision = 3) =>
        RoleCatalog.Of([RoleCatalogTests.Stored(RoleCatalogTests.Working("designer", work, scope), revision)]);

    [Fact]
    public void A_quest_marked_design_starts_on_the_seeded_Design_role_and_on_the_role_that_takes_design_more_specifically_once_one_does()
    {
        var before = Plan("design", RoleCatalog.Of([]), Machine);
        Assert.Equal(("design", "design", 0L), (before.Role, before.Seeded, before.Revision));
        Assert.Equal([new ActorEffective("claude", "opus", "high", "1")], before.Order);

        var after = Plan("design", WithDesigner(scope: InEngine), InEnginePlace);
        Assert.Equal(("designer", "design", 3L), (after.Role, after.Seeded, after.Revision));
        Assert.Equal([new ActorEffective("codex", "opus", "high", "1"), new ActorEffective("claude", "opus", null, "2")], after.Order);
    }

    [Fact]
    public void A_quest_s_weight_marks_its_work_and_a_design_start_says_whose_order_it_ran_on()
    {
        var quest = new QuestView("q1", "a", "b", "t", "body", "Open");
        Assert.Equal("build", Daoris.Driver.Driver.StartWork(quest));
        Assert.Equal("build", Daoris.Driver.Driver.StartWork(quest with { Weight = "exact" }));
        Assert.Equal("design", Daoris.Driver.Driver.StartWork(quest with { Weight = "design" }));

        Assert.Equal("Design work: it ran on Build's order.", RotatedOpening.DesignLine(ActorWords.Name("build")).Note);
        Assert.Equal("Design work: it ran on designer's order.", RotatedOpening.DesignLine("designer").Note);
    }

    /// <summary>The service's quest answer (its door carries <c>weight</c>), read by the client and started, with and without a role taking design.</summary>
    [Fact]
    public void A_quest_published_with_weight_design_starts_on_the_seeded_Design_role_then_on_the_role_that_takes_it_more_specifically()
    {
        var answered = QuestViews.Read(
            """[{"id":"q1","from":"a","to":"b","title":"Decide the cap","body":"Two ways.","status":"Open","weight":"design"},""" +
            """{"id":"q2","from":"a","to":"b","title":"Cap it","body":"b","status":"Open"}]""");

        Assert.Equal("design", answered[0].Weight);
        Assert.Null(answered[1].Weight);

        var marked = Daoris.Driver.Driver.StartWork(answered[0]);
        Assert.Equal("design", Plan(marked, RoleCatalog.Of([]), Machine).Role);
        Assert.Equal("designer", Plan(marked, WithDesigner(scope: InEngine), InEnginePlace).Role);
        Assert.Equal("build", Plan(Daoris.Driver.Driver.StartWork(answered[1]), WithDesigner(scope: InEngine), InEnginePlace).Role);
    }

    [Fact]
    public void A_chat_continuation_keeps_its_binding_s_role_and_a_removed_role_falls_to_the_one_that_takes_chat_now()
    {
        var catalogue = RoleCatalog.Of(
        [
            new StoredRole("chat", "removed", "{}", 2), RoleCatalogTests.Stored(RoleCatalogTests.Working("talker", "chat"), 4),
        ]);
        var config = Config with
        {
            Actors = new Dictionary<string, IReadOnlyList<ActorChoice>>(Config.Actors)
            {
                ["chat"] = [new ActorChoice("claude")], ["talker"] = [new ActorChoice("codex")],
            },
        };
        var record = new PriorSession("s1", null) { Actor = "chat" };

        Assert.Equal("claude", StartPlan.ContinuedOn(record, config, "chat"));
        Assert.Equal("codex", StartPlan.ContinuedOn(record, config, "chat", bound: "talker", catalogue: catalogue, place: Machine));
        Assert.Equal("talker", StartPlan.BoundRole("gone", "chat", catalogue, Machine));
    }

    [Fact]
    public void A_helper_takes_the_catalogue_s_role_and_the_seeded_catalogue_changes_nothing()
    {
        var config = Config with
        {
            Actors = new Dictionary<string, IReadOnlyList<ActorChoice>>(Config.Actors)
            {
                ["helper"] = [new ActorChoice("claude", "haiku")], ["second"] = [new ActorChoice("claude", "opus")],
            },
        };
        var ask = new HelperAsk("h1", "s1", "Read this.", "claude", HelperAsk.ByPerson, DateTimeOffset.UnixEpoch);
        var adapters = TestDoors.Registry(new Dictionary<string, Provider>());
        IReadOnlyList<ActorEffective> Order(RoleCatalog? catalogue) => HelperChoice.Order(ask, HelperRule.Default, adapters, null, config, catalogue);

        var today = Order(null);
        Assert.Equal([new ActorEffective("claude", "haiku", null, "1")], today);
        Assert.Equal(today, Order(RoleCatalog.Of([])));

        var added = RoleCatalog.Of([new StoredRole("helper", "removed", "{}", 2), RoleCatalogTests.Stored(RoleCatalogTests.Working("second", "help"), 5)]);
        Assert.Equal([new ActorEffective("claude", "opus", null, "1")], Order(added));
    }

    [Fact]
    public void An_unmarked_quest_starts_on_Build_s_order_though_a_role_takes_design()
    {
        var plan = Plan("build", WithDesigner(), Machine);

        Assert.Equal(("build", 0L), (plan.Role, plan.Revision));
        Assert.Equal([new ActorEffective("claude", "sonnet", null, "1")], plan.Order);
    }

    [Fact]
    public void A_scoped_role_naming_an_agent_its_place_s_rule_does_not_permit_walks_only_the_permitted()
    {
        var catalogue = WithDesigner(scope: new RoleScope(RoleScope.Repository, "engine"));
        var inside = new RolePlace(Repository: "engine");

        var plan = Plan("design", catalogue, inside, fence: ["claude"]);

        Assert.Equal("designer", plan.Role);
        Assert.Equal([new ActorEffective("claude", "opus", null, "2")], plan.Order);
        Assert.Equal("design", Plan("design", catalogue, new RolePlace(Repository: "other")).Role);
    }

    [Fact]
    public void An_older_build_s_continuation_of_a_record_started_on_an_added_role_runs_as_its_seeded_work()
    {
        var plan = Plan("design", WithDesigner(scope: InEngine), InEnginePlace);
        Assert.Contains(plan.Seeded, ActorChoices.Ids);

        // What an older build reads: the record's `actor`, the seeded id the start wrote, never the added role's.
        var record = new PriorSession("s1", null) { Actor = plan.Seeded };
        Assert.Equal("claude", StartPlan.ContinuedOn(record, Config));

        // The newer build reads the binding's role first, and where the role is since removed, the role that takes the work now.
        Assert.Equal("codex", StartPlan.ContinuedOn(record, Config, "design", bound: "designer", catalogue: WithDesigner(scope: InEngine), place: InEnginePlace));
        Assert.Equal("claude", StartPlan.ContinuedOn(record, Config, "design", bound: "designer", catalogue: RoleCatalog.Of([]), place: Machine));
    }

    private static Provider Present(string name, TestAgent toolchain) => TestDoors.Door(name, toolchain, spawn: new TestDoors.RefusingSpawn("nothing spawns here"));

    /// <summary>
    /// A home whose catalogue holds the repository's own Build role and a role the workflow's work step names, each staffed by its own
    /// agent, and the driver over it; nothing spawns, and each agent is present by a file look.
    /// </summary>
    private Daoris.Driver.Driver StaffedDriver(string named)
    {
        Directory.CreateDirectory(_home);
        var command = Path.Combine(_home, "agent-here");
        File.WriteAllText(command, "");
        var settings = new HarnessSettings();
        var present = new TestAgent(Binary: [command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true);
        var adapters = new Dictionary<string, Provider>(StringComparer.OrdinalIgnoreCase);
        foreach (var agent in new[] { "own", "staffed" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, "account-1"));
            settings = settings.WithDefault(agent, "account-1");
            adapters[agent] = Present(agent, present);
        }

        settings.Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
        var set = new ProviderRegistry(adapters);
        var store = DesktopStore.LocationIn(_home);
        RoleCatalog.Write(store, RoleChange.Set(RoleCatalogTests.Working("engine-build", "build", new RoleScope(RoleScope.Repository, "engine"))), () => StoreSettled.Yes);
        RoleCatalog.Write(store, RoleChange.Set(RoleCatalogTests.Working("frontend", "build", new RoleScope(RoleScope.Workspace, "default"))), () => StoreSettled.Yes);
        WorkflowStore.Add(_home, new WorkflowVersionAdded("staffed", "Staffed work", "terminal", "2026-11-01T09:00:00Z", null, JsonNode.Parse(
            $$$"""
            {"schemaVersion":2,"entries":["work"],"end":"push","nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed","options":{"role":"{{{named}}}"}},
              {"id":"review","kind":"review","version":1,"reach":"pressed"},
              {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch","pattern":"feature/{quest}-{slug}"}},
              {"id":"push","kind":"push","version":1,"reach":"performed"}],
             "wires":[{"id":"a","kind":"after","from":"work","to":"review"},{"id":"r","kind":"after","from":"review","to":"landing"},
                      {"id":"b","kind":"after","from":"landing","to":"push"}]}
            """)), kept: []);
        var config = DriverConfig.Parse("""
            {"workflows":{"engine":{"default":"staffed"}}}
            """) with
        {
            Drivable = ["engine"],
            Actors = new Dictionary<string, IReadOnlyList<ActorChoice>>
            {
                ["build"] = [new ActorChoice("own")],
                ["engine-build"] = [new ActorChoice("own", "sonnet")],
                ["frontend"] = [new ActorChoice("staffed", "opus", "high")],
            },
        };
        var roster = new HarnessRoster(set, DaorisHome.Entry(_home, HomeEntry.Harnesses));
        // A saved workflow's bind reads the registry for the remote's platform (WORKFLOWCLEAN1 1a), so the service answers an empty one.
        var service = new ServiceClient("http://127.0.0.1:9", null, new HttpClient(new Registry()));
        return new Daoris.Driver.Driver(service, config, set, _home, harnesses: roster);
    }

    private sealed class Registry : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[]") });
    }

    private static Consideration Engine => new(new QuestView("q1", "platform", "engine", "Split the loader", "Two jobs.", "Open"), StartVerdict.Start, "", Workspace: "default");

    /// <summary>
    /// ROLESTEP1b: a quest whose run's work step names an added role starts on that role's order, not the role its place gives the work,
    /// and its start keeps the role and its record's revision for the session's binding; a role that cannot take it holds the start, said.
    /// </summary>
    [Fact]
    public async Task A_run_whose_work_step_names_an_added_role_starts_on_that_role_s_order_and_records_it()
    {
        var driver = StaffedDriver("frontend");
        RepoView[] registry = [new("engine", true, null)];

        var named = await driver.StepRoleAsync(Engine.Quest, Engine.Workspace, CancellationToken.None);
        Assert.Equal("frontend", named);

        var own = await driver.WalkBuildPassesAsync(Engine, registry, CancellationToken.None, counts: false);
        Assert.Equal(("engine-build", "own"), (own.Role, own.Chosen!.Agent));

        var walked = await driver.WalkBuildPassesAsync(Engine, registry, CancellationToken.None, counts: false, role: named);
        Assert.True(walked.Selection.Allowed, walked.Selection.Refusal);
        Assert.Equal(("frontend", 1L), (walked.Role, walked.Revision));
        Assert.Equal(new ActorEffective("staffed", "opus", "high", "1"), walked.Chosen);
        Assert.Equal(("frontend", 1L), driver.Walk.RoleTaken(Engine, named));
        Assert.Equal(("engine-build", 1L), driver.Walk.RoleTaken(Engine, null));

        var error = await Assert.ThrowsAsync<DriverException>(() => driver.WalkBuildPassesAsync(Engine, registry, CancellationToken.None, counts: false, role: "gone"));
        Assert.StartsWith("No role `gone` is in the catalogue, so the starts naming it hold until it is added", error.Message);
    }
}
