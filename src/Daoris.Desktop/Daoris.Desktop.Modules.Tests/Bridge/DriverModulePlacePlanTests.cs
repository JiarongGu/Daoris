using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>PLACE_PLAN</c> over the bridge, a place's quick set-up planned on a copy of driver.json
/// and read before and after; its terminal twin's lines from the same code; and nothing written, published or probed: the service is asked
/// for its registry and quests alone, and the checkouts are not on this disk, so no git runs.
/// </summary>
public sealed class DriverModulePlacePlanTests : DriverModuleBridge
{
    private const string Config = """
        {
          "adapter": "claude-code-acp",
          "actors": { "build": [ { "agent": "claude-code-acp" } ] },
          "drivable": ["engine"],
          "workspaceOpinions": { "aurora": { "reviewers": ["codex-acp"] } }
        }
        """;

    private static readonly object Ask = new
    {
        repositories = new[] { "atlas", "tools" },
        preset = SetupPresets.Merge,
        opinion = true,
        adoption = true,
        builds = SetupBuilds.Library,
        drive = true,
    };

    private async Task<(DriverModule Module, StandIn Service, ServiceClient Client)> ModuleAsync()
    {
        File.WriteAllText(DriverConfigPath, Config);
        var service = new StandIn();
        var client = new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(service)));
        var loop = Loop();
        await loop.ComeUpAsync(client);
        lock (service.Asked) service.Asked.Clear();
        return (new DriverModule(Bus, loop), service, client);
    }

    private static JsonElement Repository(JsonElement plan, string name) =>
        plan.GetProperty("repositories").EnumerateArray().Single(row => row.GetProperty("repository").GetString() == name);

    [Fact]
    public async Task The_plan_says_each_repository_before_and_after_each_change_once_and_writes_and_probes_nothing()
    {
        var (module, service, _) = await ModuleAsync();
        var written = File.ReadAllBytes(DriverConfigPath);

        var plan = await AnswerAsync(module, "PLACE_PLAN", Ask);

        Assert.Equal(written, File.ReadAllBytes(DriverConfigPath));
        lock (service.Asked) Assert.All(service.Asked, asked => Assert.Contains(asked, new[] { "GET /api/registry", "GET /api/quests" }));
        Assert.True(plan.GetProperty("refusals").GetArrayLength() == 0, plan.GetProperty("refusals").ToString());
        var atlas = Repository(plan, "atlas");
        Assert.False(atlas.GetProperty("before").GetProperty("driven").GetBoolean());
        // A place not driven before the plan matches the not-driven delivery, whatever its workflow says.
        Assert.Equal(SetupPresets.NotDriven, atlas.GetProperty("before").GetProperty("match").GetProperty("preset").GetString());
        Assert.Equal(SetupPresets.Merge, atlas.GetProperty("after").GetProperty("match").GetProperty("preset").GetString());
        Assert.Equal("workspace", atlas.GetProperty("after").GetProperty("values").GetProperty("opinion").GetProperty("source").GetString());
        Assert.Equal(["Fix the map"], atlas.GetProperty("released").EnumerateArray().Select(each => each.GetProperty("title").GetString()));
        Assert.True(atlas.GetProperty("suggested").GetProperty("adoption").GetBoolean());
        Assert.Equal(SuggestionWhy.Default, atlas.GetProperty("suggested").GetProperty("why").GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(atlas.GetProperty("suggested").GetProperty("why").GetProperty("said").GetString()));
        Assert.Contains("has no checkout on this machine to read", atlas.GetProperty("unread").GetString());
        Assert.Equal(JsonValueKind.Null, atlas.GetProperty("declaration").ValueKind);
        var changes = plan.GetProperty("changes").EnumerateArray()
            .Select(each => (each.GetProperty("change").GetProperty("key").GetString(), string.Join(",", each.GetProperty("repositories").EnumerateArray().Select(name => name.GetString()))))
            .ToList();
        Assert.Equal(
            [("WORKFLOW_SAVE", "atlas"), ("WORKFLOW_USE", "atlas,tools"), ("SET_DRIVABLE", "atlas,tools"), ("SETUP_PRESS", "atlas,tools")],
            changes);
        Assert.Equal(
            "daoris place atlas tools --preset merge-after-accept --with opinion --adoption --what-it-builds library --drive --plan",
            plan.GetProperty("terminal").GetString());
    }

    /// <summary>The terminal's twin: asked for its words, the route says the plan the verb says alone over the same world, and its exit code.</summary>
    [Fact]
    public async Task Asked_for_its_words_the_route_says_the_verbs_own_plan()
    {
        var (module, _, client) = await ModuleAsync();

        var said = (await AnswerAsync(module, "PLACE_PLAN", new { repositories = new[] { "atlas", "tools" }, preset = SetupPresets.Merge, opinion = true, adoption = true, builds = SetupBuilds.Library, drive = true, said = true }))
            .GetProperty("said");
        var config = DriverConfig.Load(DriverConfigPath);
        var alone = await PlacePlans.PlanAsync(
            new PlaceWorld(client), config, Home, PluginCatalog.Load(Home, ProviderRegistry.Built().Names), true,
            new PlaceAsk(["atlas", "tools"], SetupPresets.Merge) { Opinion = true, Adoption = true, Builds = SetupBuilds.Library, Drive = true });

        Assert.Equal(PlaceCommand.Lines(alone), said.GetProperty("out").EnumerateArray().Select(line => line.GetString()));
        Assert.Equal(0, said.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task A_refused_plan_is_an_answer_with_every_refusal_and_nothing_written()
    {
        var (module, _, _) = await ModuleAsync();
        var written = File.ReadAllBytes(DriverConfigPath);

        var plan = await AnswerAsync(module, "PLACE_PLAN", new { repositories = new[] { "atlas", "nowhere" }, preset = SetupPresets.PullRequest, look = true });

        var codes = plan.GetProperty("refusals").EnumerateArray().Select(each => each.GetProperty("code").GetString()).ToList();
        Assert.Equal([PlacePlans.NoLandingPlugin, SetupRefusals.NotOnRegistry], codes);
        Assert.Equal(written, File.ReadAllBytes(DriverConfigPath));
    }

    /// <summary>A room asked over the route is judged with the plan's refusals in their codes, and its terminal twin says the verb's own lines.</summary>
    [Fact]
    public async Task A_room_asked_of_a_repository_not_adopted_and_of_one_whose_line_cannot_be_read_is_refused_and_asks_nothing()
    {
        var (module, service, client) = await ModuleAsync();
        var written = File.ReadAllBytes(DriverConfigPath);

        var plan = await AnswerAsync(module, "PLACE_PLAN", new { repositories = new[] { "atlas" }, room = "tools/web", purpose = "the web app needs its own conventions" });
        var unread = await AnswerAsync(module, "PLACE_PLAN", new { repositories = new[] { "tools" }, room = "tools/web", purpose = "why" });
        var outside = await AnswerAsync(module, "PLACE_PLAN", new { repositories = new[] { "engine" }, room = "../elsewhere", purpose = "why" });

        Assert.Equal([SetupRefusals.NotAdopted], plan.GetProperty("refusals").EnumerateArray().Select(each => each.GetProperty("code").GetString()));
        Assert.False(plan.GetProperty("applies").GetBoolean());
        Assert.Equal([PlaceApplyRefusals.Unread], unread.GetProperty("refusals").EnumerateArray().Select(each => each.GetProperty("code").GetString()));
        Assert.Contains(outside.GetProperty("refusals").EnumerateArray(), each => each.GetProperty("code").GetString() == PlaceRoomRefusals.Path);
        Assert.Equal("tools/web", plan.GetProperty("asked").GetProperty("room").GetString());
        Assert.Equal(written, File.ReadAllBytes(DriverConfigPath));
        lock (service.Asked) Assert.All(service.Asked, asked => Assert.Contains(asked, new[] { "GET /api/registry", "GET /api/quests" }));

        var said = (await AnswerAsync(module, "PLACE_PLAN", new { repositories = new[] { "atlas" }, room = "tools/web", purpose = "the web app needs its own conventions", said = true }))
            .GetProperty("said");
        var alone = await PlacePlans.PlanAsync(
            new PlaceWorld(client), DriverConfig.Load(DriverConfigPath), Home, PluginCatalog.Load(Home, ProviderRegistry.Built().Names), true,
            new PlaceAsk(["atlas"], "") { Room = "tools/web", Purpose = "the web app needs its own conventions" });
        Assert.Equal(PlaceCommand.Lines(alone), said.GetProperty("out").EnumerateArray().Select(line => line.GetString()));
        Assert.Equal(1, said.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task A_room_the_plan_refuses_is_an_apply_refused_with_nothing_written()
    {
        var (module, _, _) = await ModuleAsync();
        var written = File.ReadAllBytes(DriverConfigPath);

        var answer = await AnswerAsync(module, "PLACE_APPLY", new { repositories = new[] { "atlas" }, room = "tools/web", purpose = "why", digest = new string('0', 64) });

        Assert.False(answer.GetProperty("done").GetBoolean());
        Assert.Equal(written, File.ReadAllBytes(DriverConfigPath));
    }

    /// <summary>The registry: aurora's engine, driven, and atlas and tools, not, their checkouts elsewhere; one queued quest to atlas.</summary>
    private sealed class StandIn : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            lock (Asked) Asked.Add($"{request.Method} {path}");
            var body = path switch
            {
                "/api/registry" => """
                    [{"repository":"engine","workspace":"aurora","adopted":true,"root":"elsewhere-engine"},
                     {"repository":"atlas","workspace":"aurora","root":"elsewhere-atlas"},
                     {"repository":"tools","workspace":"aurora","adopted":true,"root":"elsewhere-tools"}]
                    """,
                "/api/quests" => """
                    [{"id":"q1","from":"work","to":"atlas","title":"Fix the map","body":"","status":"Open"},
                     {"id":"q2","from":"work","to":"engine","title":"Not atlas's","body":"","status":"Open"}]
                    """,
                _ => "[]",
            };
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
