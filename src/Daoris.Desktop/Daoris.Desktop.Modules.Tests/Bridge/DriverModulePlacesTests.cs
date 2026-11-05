using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// AGENTSPLACE1a (the Agents-place design §2, §3 and §7 row 4): <c>PLACES</c>, each place's driving, its works and the values of its head's
/// line with where each was set, equal to the readers a start uses; and <c>ROLE_STRUCTURE</c> asked for a chosen place's chain alone.
/// </summary>
public sealed class DriverModulePlacesTests : DriverModuleBridge
{
    private const string Config = """
        {
          "adapter": "claude-code",
          "drivable": ["engine", "game", "site"],
          "holds": ["game"],
          "trees": ["engine"],
          "lines": { "engine": "release" },
          "workspaceLines": { "aurora": "develop" },
          "landings": { "engine": { "form": "branch", "pattern": "feature/{quest}-{slug}" }, "broken": { "tidy": true } },
          "workspaceLandings": { "aurora": { "form": "merge" } },
          "workspaceLanguages": { "aurora": "zh" },
          "workspaceOpinions": { "aurora": { "reviewers": ["codex-acp"] } },
          "workspaceHelpers": { "borealis": { "mode": "off" } },
          "workflows": { "engine": { "default": "feature-review" } },
          "workspaceWorkflows": { "aurora": { "default": "web-flow" } }
        }
        """;

    private async Task<DriverModule> ModuleAsync(StandIn service)
    {
        File.WriteAllText(DriverConfigPath, Config);
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(service))));
        return new DriverModule(Bus, loop);
    }

    private static string Of(JsonElement value, string name) => value.GetProperty(name).GetProperty("source").GetString()!;

    private static JsonElement Repository(JsonElement answer, string name) =>
        answer.GetProperty("repositories").EnumerateArray().Single(row => row.GetProperty("repository").GetString() == name);

    [Fact]
    public async Task Every_value_and_its_source_equals_the_reader_a_start_uses_at_each_level()
    {
        var service = new StandIn();
        var module = await ModuleAsync(service);

        var answer = await AnswerAsync(module, "PLACES");

        var config = DriverConfig.Load(DriverConfigPath);
        var engine = Repository(answer, "engine");
        var values = engine.GetProperty("values");
        Assert.Equal(("release", "here"), (values.GetProperty("line").GetProperty("value").GetString(), Of(values, "line")));
        // The landing is the workflow's landing step's, and the engine's saved workflow is not in the store.
        Assert.Equal((null, "here"), (values.GetProperty("landing").GetProperty("value").GetString(), Of(values, "landing")));
        Assert.Equal(("feature-review", "here"), (values.GetProperty("workflow").GetProperty("value").GetString(), Of(values, "workflow")));
        Assert.Equal(("current", "default"), (values.GetProperty("landed").GetProperty("value").GetString(), Of(values, "landed")));
        Assert.Equal(("zh", "workspace"), (values.GetProperty("language").GetProperty("value").GetString(), Of(values, "language")));
        Assert.Equal("workspace", Of(values, "opinion"));
        Assert.Equal("default", Of(values, "commits"));
        Assert.True(engine.GetProperty("driven").GetBoolean() && engine.GetProperty("trees").GetBoolean() && !engine.GetProperty("held").GetBoolean());

        // The game inherits its workspace's line, landing and workflow, and is held.
        var game = Repository(answer, "game").GetProperty("values");
        Assert.Equal(("develop", "workspace"), (game.GetProperty("line").GetProperty("value").GetString(), Of(game, "line")));
        Assert.Equal((null, "workspace"), (game.GetProperty("landing").GetProperty("value").GetString(), Of(game, "landing")));
        Assert.Equal(("web-flow", "workspace"), (game.GetProperty("workflow").GetProperty("value").GetString(), Of(game, "workflow")));
        Assert.True(Repository(answer, "game").GetProperty("held").GetBoolean());

        // The site sets and inherits nothing in its workspace: every value is the default.
        var site = Repository(answer, "site").GetProperty("values");
        Assert.All(new[] { "line", "landing", "workflow", "landed", "commits", "opinion", "handOver", "language" }, name => Assert.Equal("default", Of(site, name)));

        // The workspace's own page says what it sets as set here.
        var aurora = answer.GetProperty("workspaces").EnumerateArray().Single(row => row.GetProperty("place").GetProperty("workspace").GetString() == "aurora");
        var own = aurora.GetProperty("place").GetProperty("values");
        Assert.Equal(("develop", "here"), (own.GetProperty("line").GetProperty("value").GetString(), Of(own, "line")));
        Assert.Equal("here", Of(own, "language"));

        // Each work's role and rule equal RoleFor and WorkPolicies.Of for the same place.
        var catalogue = RoleCatalog.ReadHome(Home);
        foreach (var (name, workspace, repository) in new[] { ("engine", "aurora", "engine"), ("site", "borealis", "site") })
        {
            foreach (var work in Repository(answer, name).GetProperty("works").EnumerateArray())
            {
                var code = work.GetProperty("work").GetString()!;
                var found = catalogue.RoleFor(code, new RolePlace(workspace, repository));
                var policy = WorkPolicies.Of(config, code, repository, workspace);
                Assert.Equal(found.Role?.Id, work.GetProperty("role").GetString());
                Assert.Equal(policy.Consent, work.GetProperty("consent").GetString());
                Assert.Equal(PlaceSource.At(policy.Source, "repository"), work.GetProperty("source").GetString());
            }
        }

        var borealisHelp = answer.GetProperty("workspaces").EnumerateArray().Single(row => row.GetProperty("place").GetProperty("workspace").GetString() == "borealis")
            .GetProperty("place").GetProperty("works").EnumerateArray().Single(work => work.GetProperty("work").GetString() == "help");
        Assert.Equal(("off", "here"), (borealisHelp.GetProperty("consent").GetString(), borealisHelp.GetProperty("source").GetString()));
    }

    [Fact]
    public async Task Opening_asks_only_the_registry_and_the_undriven_come_as_counts_with_a_page_when_the_fold_opens()
    {
        var service = new StandIn();
        var module = await ModuleAsync(service);

        var answer = await AnswerAsync(module, "PLACES");

        // The registry, and the quests only to see whether a set-up runs for a repository not adopted.
        Assert.Equal(["/api/quests", "/api/registry"], service.Asked.Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(["engine", "game", "site"], answer.GetProperty("repositories").EnumerateArray().Select(row => row.GetProperty("repository").GetString()));
        var borealis = answer.GetProperty("workspaces").EnumerateArray().Single(row => row.GetProperty("place").GetProperty("workspace").GetString() == "borealis");
        Assert.Equal((1, 41), (borealis.GetProperty("driven").GetInt32(), borealis.GetProperty("notDriven").GetInt32()));
        Assert.Equal(JsonValueKind.Null, answer.GetProperty("undriven").ValueKind);
        Assert.DoesNotContain("many-01", answer.GetRawText());

        var page = (await AnswerAsync(module, "PLACES", new { fold = "borealis", limit = 15 })).GetProperty("undriven");
        Assert.Equal(41, page.GetProperty("total").GetInt32());
        Assert.Equal(15, page.GetProperty("rows").GetArrayLength());
        var next = page.GetProperty("next").GetString();
        var second = (await AnswerAsync(module, "PLACES", new { fold = "borealis", limit = 15, foldAfter = next })).GetProperty("undriven");
        Assert.Equal("many-15", second.GetProperty("rows")[0].GetProperty("repository").GetString());
        var found = (await AnswerAsync(module, "PLACES", new { fold = "borealis", foldSearch = "many-39" })).GetProperty("undriven");
        Assert.Equal(["many-39"], found.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("repository").GetString()));
    }

    [Fact]
    public async Task Needs_a_look_is_judged_from_the_configuration_and_the_driven_list_pages_with_its_search()
    {
        var module = await ModuleAsync(new StandIn());

        var needing = await AnswerAsync(module, "PLACES", new { needsLook = true });
        // The game has no checkout here; the site is not adopted; nothing driven reads fine otherwise.
        var codes = needing.GetProperty("repositories").EnumerateArray()
            .ToDictionary(row => row.GetProperty("repository").GetString()!, row => row.GetProperty("needs").EnumerateArray().Select(need => need.GetProperty("code").GetString()).ToArray());
        Assert.Equal(["game", "site"], codes.Keys.Order(StringComparer.Ordinal));
        Assert.Contains("no-checkout", codes["game"]);
        Assert.Contains("not-adopted", codes["site"]);

        var aurora = (await AnswerAsync(module, "PLACES")).GetProperty("workspaces").EnumerateArray()
            .Single(row => row.GetProperty("place").GetProperty("workspace").GetString() == "aurora");
        Assert.Empty(aurora.GetProperty("place").GetProperty("needs").EnumerateArray().Where(need => need.GetProperty("code").GetString() == "landing-unread"));

        var paged = await AnswerAsync(module, "PLACES", new { limit = 2 });
        Assert.Equal(2, paged.GetProperty("repositories").GetArrayLength());
        Assert.Equal(3, paged.GetProperty("matching").GetInt32());
        var rest = await AnswerAsync(module, "PLACES", new { limit = 2, after = paged.GetProperty("next").GetString() });
        Assert.Equal(["site"], rest.GetProperty("repositories").EnumerateArray().Select(row => row.GetProperty("repository").GetString()));
        Assert.Equal(JsonValueKind.Null, rest.GetProperty("next").ValueKind);
        var searched = await AnswerAsync(module, "PLACES", new { search = "ENG" });
        Assert.Equal(["engine"], searched.GetProperty("repositories").EnumerateArray().Select(row => row.GetProperty("repository").GetString()));
    }

    [Fact]
    public async Task The_structure_asked_for_a_chain_answers_the_place_its_workspace_and_this_machine_alone()
    {
        File.WriteAllText(DriverConfigPath, Config);

        var answer = await AnswerAsync(Module(), "ROLE_STRUCTURE", new { chain = new { workspace = "aurora", repository = "engine" }, works = new[] { "build" } });

        var places = answer.GetProperty("places").EnumerateArray()
            .Select(place => (Name(place, "workspace"), Name(place, "repository"))).ToList();
        Assert.Equal([("aurora", "engine"), ("aurora", null), (null, null)], places);

        var workspaceOnly = await AnswerAsync(Module(), "ROLE_STRUCTURE", new { chain = new { workspace = "aurora" }, works = new[] { "build" } });
        Assert.Equal(2, workspaceOnly.GetProperty("places").GetArrayLength());

        static string? Name(JsonElement place, string name) => place.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static string SetUpQuest(string repository, string status) =>
        $$"""{"id":"q{{repository}}","from":"work","to":"{{repository}}","title":"{{SetupQuests.Title(SetupQuests.SetUp, new DateOnly(2026, 11, 1))}}","body":"","status":"{{status}}"}""";

    /// <summary>SETUPUX1d: <c>detail</c> answers a place whole, driven or not, by the readers a start uses, and asks nothing beyond the registry and quests.</summary>
    [Fact]
    public async Task A_detail_answers_one_place_whole_equal_to_its_row_and_its_readers_and_probes_nothing()
    {
        var service = new StandIn(quests: [SetUpQuest("many-05", "Open")]);
        var module = await ModuleAsync(service);
        var config = DriverConfig.Load(DriverConfigPath);

        var listed = await AnswerAsync(module, "PLACES");
        var driven = (await AnswerAsync(module, "PLACES", new { detail = new { workspace = "aurora", repository = "engine" } })).GetProperty("detail");
        Assert.Equal(Repository(listed, "engine").GetRawText(), driven.GetRawText());
        Assert.Equal(JsonValueKind.Null, listed.GetProperty("detail").ValueKind);

        // Not driven: the same row, built by the same readers.
        var tools = (await AnswerAsync(module, "PLACES", new { detail = new { workspace = "aurora", repository = "tools" } })).GetProperty("detail");
        Assert.False(tools.GetProperty("driven").GetBoolean());
        var expected = PlaceProjection.ValuesAt(config, "tools", "aurora", PlaceSource.RepositoryLevel);
        Assert.Equal(expected.Landing.Value, tools.GetProperty("values").GetProperty("landing").GetProperty("value").GetString());
        Assert.Null(expected.Lands);
        Assert.Equal(JsonValueKind.Null, tools.GetProperty("values").GetProperty("lands").ValueKind);
        Assert.Equal("adopted", tools.GetProperty("adoption").GetString());
        Assert.Equal(RoleWork.Codes, tools.GetProperty("works").EnumerateArray().Select(work => work.GetProperty("work").GetString()));

        // The site has no workflow and the engine's saved workflow is not in the store, so neither lands as anything known.
        var site = (await AnswerAsync(module, "PLACES", new { detail = new { repository = "site" } })).GetProperty("detail").GetProperty("values").GetProperty("lands");
        Assert.Equal(JsonValueKind.Null, site.ValueKind);
        Assert.Equal(JsonValueKind.Null, driven.GetProperty("values").GetProperty("lands").ValueKind);

        // Each work names its role and where it sits, beside the id it always had.
        var catalogue = RoleCatalog.ReadHome(Home);
        foreach (var work in driven.GetProperty("works").EnumerateArray())
        {
            var role = catalogue.RoleFor(work.GetProperty("work").GetString()!, new RolePlace("aurora", "engine")).Role!;
            Assert.Equal((role.Id, role.Name), (work.GetProperty("role").GetString(), AuthoredText.Read(work.GetProperty("roleName"))));
            Assert.Equal(role.Scope.Kind, work.GetProperty("roleScope").GetProperty("kind").GetString());
        }

        var workspace = (await AnswerAsync(module, "PLACES", new { detail = new { workspace = "borealis" } })).GetProperty("detail");
        Assert.Equal(("borealis", JsonValueKind.Null), (workspace.GetProperty("workspace").GetString(), workspace.GetProperty("repository").ValueKind));
        Assert.Equal(JsonValueKind.Null, (await AnswerAsync(module, "PLACES", new { detail = new { workspace = "aurora", repository = "nowhere" } })).GetProperty("detail").ValueKind);

        Assert.Equal(["/api/quests", "/api/registry"], service.Asked.Distinct().Order(StringComparer.Ordinal));
    }

    /// <summary>SETUPUX1d: an adoption running is read from its quest and listed with the driven; <c>members</c> is the workspace's whole list.</summary>
    [Fact]
    public async Task An_adoption_running_is_listed_with_the_driven_and_the_members_are_the_whole_workspace()
    {
        var module = await ModuleAsync(new StandIn(quests: [SetUpQuest("many-05", "Open"), SetUpQuest("many-06", "Done")]));

        var answer = await AnswerAsync(module, "PLACES", new { members = "borealis" });

        Assert.Equal(["engine", "game", "many-05", "site"], answer.GetProperty("repositories").EnumerateArray().Select(row => row.GetProperty("repository").GetString()).Order(StringComparer.Ordinal));
        var running = Repository(answer, "many-05");
        Assert.Equal((false, "running"), (running.GetProperty("driven").GetBoolean(), running.GetProperty("adoption").GetString()));
        Assert.Equal("adopted", Repository(answer, "engine").GetProperty("adoption").GetString());
        var borealis = answer.GetProperty("workspaces").EnumerateArray().Single(row => row.GetProperty("place").GetProperty("workspace").GetString() == "borealis");
        Assert.Equal((1, 41, 1), (borealis.GetProperty("driven").GetInt32(), borealis.GetProperty("notDriven").GetInt32(), borealis.GetProperty("running").GetInt32()));

        var members = answer.GetProperty("members");
        Assert.Equal(("borealis", 42), (members.GetProperty("workspace").GetString(), members.GetProperty("rows").GetArrayLength()));
        Assert.Equal(["site"], members.GetProperty("rows").EnumerateArray().Where(row => row.GetProperty("driven").GetBoolean()).Select(row => row.GetProperty("repository").GetString()));
        Assert.Equal("running", members.GetProperty("rows").EnumerateArray().Single(row => row.GetProperty("repository").GetString() == "many-05").GetProperty("adoption").GetString());
        Assert.Equal(JsonValueKind.Null, (await AnswerAsync(module, "PLACES")).GetProperty("members").ValueKind);

        // The fold no longer holds it.
        var fold = (await AnswerAsync(module, "PLACES", new { fold = "borealis" })).GetProperty("undriven");
        Assert.Equal(40, fold.GetProperty("total").GetInt32());
    }

    /// <summary>The registry a driver reads: aurora's engine, game and tools; borealis' site, docs and forty more not driven.</summary>
    private sealed class StandIn(string? driven = null, string[]? quests = null) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            lock (Asked) Asked.Add(path);
            var rows = new List<string>
            {
                """{"repository":"engine","workspace":"aurora","adopted":true,"root":"checkout"}""",
                """{"repository":"game","workspace":"aurora","adopted":true}""",
                """{"repository":"tools","workspace":"aurora","adopted":true,"root":"checkout"}""",
                """{"repository":"site","workspace":"borealis","root":"checkout"}""",
                """{"repository":"docs","workspace":"borealis","adopted":true,"root":"checkout"}""",
            };
            rows.AddRange(Enumerable.Range(1, 40).Select(number => $$"""{"repository":"many-{{number:00}}","workspace":"borealis","root":"checkout"}"""));
            if (driven is not null) rows.Add($$"""{"repository":"{{driven}}","workspace":"aurora","adopted":true,"root":"checkout"}""");
            var body = path == "/api/registry" ? "[" + string.Join(",", rows) + "]"
                : path == "/api/quests" && quests is not null ? "[" + string.Join(",", quests) + "]"
                : "[]";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
