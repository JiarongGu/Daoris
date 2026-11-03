using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// AGENTSPLACE1k: a place's set-up proposal over the bridge. The card carries the ask, the workspace and the plan's changes, judged by the
/// place's plan read from the service's registry and quests; its Apply settles as a go to the Agents place's Setup with the ask seeded and
/// writes nothing, neither the configuration nor the service.
/// </summary>
public sealed class HelpPlaceDoorTests : DriverModuleBridge
{
    private const string Config = """{ "adapter": "claude-code-acp", "drivable": ["engine"], "trees": ["engine"] }""";

    private const string Proposed = """
        {"id":"p1a2b3c4","kind":"setup","door":"place","target":"atlas","workspace":null,"value":null,"sentence":null,"why":"set both up","state":"proposed",
         "by":{"session":"h1"},"place":{"preset":"merge-after-accept","repositories":["atlas","tools"],"opinion":false,"look":false,"adoption":true,"builds":"library","drive":false}}
        """;

    private readonly Registry _service = new();

    [Fact]
    public void A_place_is_judged_by_its_own_plan_and_nothing_else_is()
    {
        var place = new HelpProposal("p1", "setup", "place", "atlas", null, null, null, "why", "h1", "proposed") { Place = new PlaceAsk(["atlas"], SetupPresets.Merge) };
        var press = place with { Door = "setup", Place = null };

        Assert.True(HelpRoutes.JudgedAgainstPlaces([place]));
        Assert.False(HelpRoutes.JudgedAgainstPlaces([press, place with { Place = null }]));
    }

    [Fact]
    public async Task A_place_is_drawn_with_its_plan_and_applied_as_a_go_that_writes_nothing()
    {
        File.WriteAllText(DriverConfigPath, Config);
        var module = await UpAsync();
        Proposal(Proposed);
        var written = File.ReadAllBytes(DriverConfigPath);

        var drawn = await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1" });

        var card = Assert.Single(drawn.GetProperty("proposals").EnumerateArray());
        Assert.Equal("daoris place atlas tools --preset merge-after-accept --adoption --what-it-builds library --plan", card.GetProperty("terminal").GetString());
        var place = card.GetProperty("place");
        Assert.Equal("aurora", place.GetProperty("workspace").GetString());
        Assert.Equal(["atlas", "tools"], place.GetProperty("asked").GetProperty("repositories").EnumerateArray().Select(each => each.GetString()));
        Assert.NotEmpty(place.GetProperty("changes").EnumerateArray());
        Assert.NotEmpty(place.GetProperty("digest").GetString()!);
        Assert.False(card.TryGetProperty("setup", out _));

        var applied = await AnswerAsync(module, "HELP_APPLY", new { id = "p1a2b3c4", revision = card.GetProperty("revision").GetString() });

        Assert.True(applied.GetProperty("applied").GetBoolean());
        var go = applied.GetProperty("go");
        Assert.Equal(("agents", "roles", "aurora"), (go.GetProperty("view").GetString(), go.GetProperty("part").GetString(), go.GetProperty("workspace").GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, go.GetProperty("repository").ValueKind);
        var seeded = go.GetProperty("setup");
        Assert.Equal(["atlas", "tools"], seeded.GetProperty("repositories").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal(("merge-after-accept", true, "library"), (seeded.GetProperty("preset").GetString(), seeded.GetProperty("adoption").GetBoolean(), seeded.GetProperty("builds").GetString()));
        Assert.Equal(written, File.ReadAllBytes(DriverConfigPath));
        Assert.Equal("applied", HelpProposals.Find(Home, "p1a2b3c4")!.State);
        lock (_service.Asked) Assert.All(_service.Asked, asked => Assert.StartsWith("GET ", asked));
    }

    /// <summary>A plan that refuses is never drawn: the card is settled refused in the plan's words, and the ask stays unopened.</summary>
    [Fact]
    public async Task A_place_the_plan_refuses_is_never_drawn()
    {
        File.WriteAllText(DriverConfigPath, Config);
        var module = await UpAsync();
        Proposal(Proposed.Replace("merge-after-accept", "bogus", StringComparison.Ordinal));

        var drawn = await AnswerAsync(module, "HELP_PROPOSALS", new { session = "h1" });

        Assert.Empty(drawn.GetProperty("proposals").EnumerateArray());
        Assert.Equal("refused", HelpProposals.Find(Home, "p1a2b3c4")!.State);
    }

    private void Proposal(string json)
    {
        var folder = HelpProposals.FolderOf(Home);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "p1a2b3c4.json"), json);
    }

    private async Task<DriverModule> UpAsync()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(_service)));
        lock (_service.Asked) _service.Asked.Clear();
        return module;
    }

    /// <summary>The registry: aurora's engine, driven, and atlas and tools, not, their checkouts elsewhere.</summary>
    private sealed class Registry : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            lock (Asked) Asked.Add($"{request.Method} {path}");
            var body = path == "/api/registry"
                ? """
                    [{"repository":"engine","workspace":"aurora","adopted":true,"root":"elsewhere-engine"},
                     {"repository":"atlas","workspace":"aurora","root":"elsewhere-atlas"},
                     {"repository":"tools","workspace":"aurora","adopted":true,"root":"elsewhere-tools"}]
                    """
                : "[]";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
