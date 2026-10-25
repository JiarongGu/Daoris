using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Each standing choice both ways (PUSH1f2a, D194 points 4 and 6, the alone-path design §3): <c>daoris-driver driver</c> acting alone,
/// through the library's operation, and the same words' press sent to the window's route, its answer said by the same words function,
/// over one home from the same choices. The bytes written, the text, the refusal's line and the exit are equal: red where a route and
/// its operation disagree, or where the payload the command writes does not read back through the route as the command judged it.
/// </summary>
public sealed partial class DriverChoicesBothWaysTests : DriverModuleBridge
{
    private static readonly DateTimeOffset Moment = new(2026, 10, 21, 9, 30, 0, TimeSpan.Zero);

    private static readonly QuestView Parked = new("q1", "game", "engine", "Cap the chunk budget", "A body.", "Taken");

    /// <summary>The choices every case starts from: something of each, so an edit meets a rule already set.</summary>
    private static DriverConfig Before() =>
        DriverConfig.Empty.WithDrivable("engine", true).WithDrivable("game", true).WithHold("game", true).WithTrees("docs", true)
            .WithLine("engine", "main").WithWorkspaceLine("aurora", "main")
            .WithLanding("engine", LandingRule.Merge).WithWorkspaceLanding("aurora", new LandingRule(LandingForm.Branch, "feature/{quest}"))
            .WithReadAcross("engine", true).WithWorkspaceReadAcross("aurora", false).WithWriteAcross("game", "tools", true)
            .WithStanding("engine", "the old words", Moment).WithLanguage("engine", "en").WithWorkspaceLanguage("aurora", "zh")
            .WithStrikes(3).WithForgiven("q1", 3);

    private DriverChoicesWorld World(StringWriter output, StringWriter error, IReadOnlyList<Consideration>? verdicts = null, ChoicesRegistry? registry = null) => new()
    {
        Verbs = DriverChoiceVerbs.All,
        // No address file is read here (a workspace build), so the command acts alone; the route is asked of the module itself.
        Instance = new InstanceWorld { Home = Home, Environment = _ => null, Out = output, Error = error, Verb = "daoris" },
        ConfigPath = DriverConfigPath,
        Registry = _ => Task.FromResult(registry ?? new ChoicesRegistry(null, "the service did not answer")),
        Verdicts = (_, _) => Task.FromResult(verdicts ?? []),
        Now = () => Moment,
    };

    private sealed record Said(int Exit, string Out, string Error, string? File);

    private string? Written() => File.Exists(DriverConfigPath) ? StandingMoment().Replace(File.ReadAllText(DriverConfigPath), "\"at\": \"<at>\"") : null;

    private async Task<Said> AloneAsync(string text, string[] words, IReadOnlyList<Consideration>? verdicts = null, ChoicesRegistry? registry = null)
    {
        File.WriteAllText(DriverConfigPath, text);
        var (output, error) = (new StringWriter(), new StringWriter());
        var exit = await DriverChoicesCommand.RunAsync(words, World(output, error, verdicts, registry), CancellationToken.None);
        return new(exit, output.ToString(), error.ToString(), Written());
    }

    /// <summary>The same words' press sent to the module's route, its answer said by the command's words, as the pipe's client says it.</summary>
    private async Task<Said> RouteAsync(DriverModule module, string text, string[] words)
    {
        File.WriteAllText(DriverConfigPath, text);
        var (output, error) = (new StringWriter(), new StringWriter());
        var press = DriverChoicesCommand.Plan(words, DriverConfig.Load(DriverConfigPath), World(output, error));
        Assert.NotNull(press.Route);
        var response = await AskAsync(module, press.Route.Route, press.Payload);
        int exit;
        if (response.Success)
        {
            exit = press.Words(press.Fields(JsonSerializer.SerializeToElement(
                response.Data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })));
        }
        else
        {
            // As InstanceDoor.PressAsync says a route's refusal: `{verb}: {message} ({code})`, exit 2.
            error.WriteLine($"daoris: {response.Error!.Message} ({response.Error.Code})");
            exit = 2;
        }

        return new(exit, output.ToString(), error.ToString(), Written());
    }

    private async Task BothWaysAsync(DriverModule module, string words, IReadOnlyList<Consideration>? verdicts = null, ChoicesRegistry? registry = null)
    {
        Before().Save(DriverConfigPath);
        var text = File.ReadAllText(DriverConfigPath);
        var split = words.Split(' ');

        var alone = await AloneAsync(text, split, verdicts, registry);
        var route = await RouteAsync(module, text, split);

        Assert.NotEqual("", alone.Out + alone.Error);
        Assert.Equal(alone.Out, route.Out);
        Assert.Equal(alone.Error, route.Error);
        Assert.Equal(alone.Exit, route.Exit);
        Assert.Equal(alone.File, route.File);
    }

    [Theory]
    [InlineData("drive engine")]
    [InlineData("drive Tools")]
    [InlineData("undrive game")]
    [InlineData("hold engine")]
    [InlineData("resume game")]
    [InlineData("trees engine on")]
    [InlineData("trees docs off")]
    [InlineData("notify off")]
    [InlineData("cap 4")]
    [InlineData("strikes 0")]
    [InlineData("cooloff 30")]
    [InlineData("helper codex-acp")]
    [InlineData("helper off")]
    [InlineData("intake claude-code")]
    [InlineData("intake off")]
    [InlineData("line engine develop")]
    [InlineData("line --workspace aurora --clear")]
    [InlineData("landing engine merge")]
    [InlineData("landing engine branch feature/{ticket} --key TK-\\d+ --squash --auto-accept --tidy")]
    [InlineData("landing --workspace aurora branch work/{quest} --no-squash")]
    [InlineData("landing --workspace aurora --clear")]
    [InlineData("landing engine branch feature/{quest} --plugin acme.pr")]
    [InlineData("across engine read off")]
    [InlineData("across --workspace aurora read --clear")]
    [InlineData("across game write-to engine")]
    [InlineData("across game write-to tools --clear")]
    [InlineData("standing engine dev writes allowed")]
    [InlineData("standing ENGINE --clear")]
    [InlineData("language engine zh")]
    [InlineData("language --workspace aurora --clear")]
    [InlineData("review engine none")]
    [InlineData("opinion engine --reviewers codex-acp --required --verify")]
    [InlineData("opinion --workspace aurora --reviewers codex-acp,dsh --on steps --minutes 15")]
    [InlineData("opinion engine none")]
    [InlineData("helpers engine --mode on --agents codex-acp")]
    [InlineData("helpers --workspace aurora --mode off")]
    [InlineData("handover engine --mode on")]
    [InlineData("handover --workspace aurora --mode off")]
    [InlineData("commits engine --mode ask --convention Imperative.")]
    [InlineData("commits --workspace aurora --mode at-end")]
    public async Task A_standing_choice_says_and_writes_the_same_alone_and_through_its_route(string words) =>
        await BothWaysAsync(Module(), words);

    /// <summary>
    /// *Try again* both ways (RETRY1b, SESSUX1b): from the same verdicts the operation marks a parked quest at its failures, releases a
    /// stop, and refuses a quest neither parked nor held in the catalogue's code, alone as through <c>RETRY_QUEST</c>.
    /// </summary>
    [Theory]
    [InlineData("exhausted")]
    [InlineData("stopped")]
    [InlineData("start")]
    [InlineData("paused")]
    public async Task Try_again_says_and_writes_the_same_both_ways(string verdict)
    {
        Consideration[] considered = [verdict switch
        {
            "exhausted" => new Consideration(Parked, StartVerdict.Exhausted, "parked") { Failures = 6 },
            "stopped" => new Consideration(Parked, StartVerdict.Stopped, "you stopped it") { HeldBy = new PriorSession("s1a2b3c4", "X:/daoris/trees/s1", "stopped", "the person stopped it.") },
            "paused" => new Consideration(Parked, StartVerdict.Paused, "paused") { PausedBy = new PausedBy(WorkScope.Ask, "a1") },
            _ => new Consideration(Parked, StartVerdict.Start, "it may start"),
        }];
        var loop = Loop();
        loop.Look.Record(considered);

        await BothWaysAsync(new DriverModule(Bus, loop), "retry #q1", considered);
    }

    /// <summary>
    /// A review's environment put both ways (REVIEWENV1a): its procedure looked for in the registry's checkouts by the same operation,
    /// a repository's checkout that holds it written and said, a workspace's lacking one named, and one that lacks it refused.
    /// </summary>
    [Theory]
    [InlineData("review engine dev --kind deployed --procedure README.md --required")]
    [InlineData("review --workspace aurora dev --kind deployed --procedure README.md")]
    [InlineData("review game dev --kind deployed --procedure README.md")]
    [InlineData("review tools dev --kind deployed --procedure README.md")]
    public async Task A_review_put_looks_for_its_procedure_the_same_both_ways(string words)
    {
        var checkouts = Path.Combine(Home, "checkouts");
        Directory.CreateDirectory(Path.Combine(checkouts, "engine"));
        Directory.CreateDirectory(Path.Combine(checkouts, "game"));
        File.WriteAllText(Path.Combine(checkouts, "engine", "README.md"), "# Run it against dev\n");
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new Registry(checkouts))));
        IReadOnlyList<(string, string?, string?)> rows =
            [("game", "aurora", Path.Combine(checkouts, "game")), ("engine", "aurora", Path.Combine(checkouts, "engine")), ("tools", null, null)];

        await BothWaysAsync(module, words, registry: new ChoicesRegistry(rows, null));
    }

    /// <summary>
    /// KEEP1d (the cleanup design §9's proof): every keep choice this row adds, both ways, from choices that set none and from choices
    /// that set each kind, so a rule set, replaced and taken back to its default writes the same bytes and says the same words alone as
    /// through <c>SET_KEEP</c>.
    /// </summary>
    [Theory]
    [InlineData(false, "keep set-aside 30")]
    [InlineData(false, "keep set-aside whole")]
    [InlineData(true, "keep set-aside slim")]
    [InlineData(true, "keep set-aside 1")]
    [InlineData(true, "keep set-aside default")]
    [InlineData(false, "keep downloads 0")]
    [InlineData(false, "keep downloads never")]
    [InlineData(true, "keep downloads 365")]
    [InlineData(true, "keep downloads default")]
    [InlineData(false, "keep logs 7")]
    [InlineData(true, "keep logs 30")]
    [InlineData(true, "keep logs default")]
    // KEEP1g: history's age, and `never`, its default again.
    [InlineData(false, "keep history 30")]
    [InlineData(false, "keep history never")]
    [InlineData(true, "keep history 3650")]
    [InlineData(true, "keep history never")]
    [InlineData(true, "keep history default")]
    // KEEP1f: the free-space floor, a size, a share or off, and its default again.
    [InlineData(false, "keep floor 1GB")]
    [InlineData(false, "keep floor 12%")]
    [InlineData(false, "keep floor off")]
    [InlineData(true, "keep floor 50%")]
    [InlineData(true, "keep floor default")]
    public async Task A_keep_rule_says_and_writes_the_same_alone_and_through_its_route(bool set, string words)
    {
        var module = Module();
        var before = set
            ? Before().WithKeep(new KeepSet("set-aside", Days: 30, Whole: true)).WithKeep(new KeepSet("downloads", Never: true))
                .WithKeep(new KeepSet("logs", Days: 14)).WithKeep(new KeepSet("history", Days: 365)).WithKeep(new KeepSet("floor", Percent: 5))
            : Before();
        before.Save(DriverConfigPath);
        var text = File.ReadAllText(DriverConfigPath);
        var split = words.Split(' ');

        var alone = await AloneAsync(text, split);
        var route = await RouteAsync(module, text, split);

        Assert.Equal((0, ""), (alone.Exit, alone.Error));
        Assert.Equal(alone, route);
    }

    /// <summary>
    /// KEEP1d: a value out of its kind's bounds sent to the route is refused in the catalogue's code, naming the kind and its bounds, and
    /// nothing is written; the terminal refuses it before it asks the window (<c>fixtures/driver-choices-text.json</c>).
    /// </summary>
    [Fact]
    public async Task A_keep_rule_out_of_its_bounds_is_refused_through_the_route_and_nothing_is_written()
    {
        var module = Module();
        Before().Save(DriverConfigPath);
        var text = File.ReadAllText(DriverConfigPath);

        var past = await RefusalAsync(module, "SET_KEEP", new { kind = "logs", days = 60 });
        Assert.StartsWith(Refusals.KeepOutOfRange + " ", past, StringComparison.Ordinal);
        Assert.Contains("kind=logs", past, StringComparison.Ordinal);
        Assert.Contains("max=30", past, StringComparison.Ordinal);
        Assert.StartsWith(Refusals.KeepOutOfRange + " ", await RefusalAsync(module, "SET_KEEP", new { kind = "set-aside", never = true }), StringComparison.Ordinal);
        var young = await RefusalAsync(module, "SET_KEEP", new { kind = "history", days = 7 });
        Assert.StartsWith(Refusals.KeepOutOfRange + " ", young, StringComparison.Ordinal);
        Assert.Contains("min=30", young, StringComparison.Ordinal);
        // KEEP1f: a floor's share past half the volume, refused with its unit as the page's context.
        var share = await RefusalAsync(module, "SET_KEEP", new { kind = "floor", percent = 51 });
        Assert.StartsWith(Refusals.KeepOutOfRange + " ", share, StringComparison.Ordinal);
        Assert.Contains("context=percent", share, StringComparison.Ordinal);
        Assert.Equal(text, File.ReadAllText(DriverConfigPath));
    }

    /// <summary>A service whose registry holds two checkouts in one workspace and a repository in none, with no checkout here.</summary>
    private sealed class Registry(string checkouts) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string Root(string name) => JsonSerializer.Serialize(Path.Combine(checkouts, name));
            var body = request.RequestUri!.AbsolutePath == "/api/registry"
                ? $$"""[{"repository":"game","workspace":"aurora","root":{{Root("game")}}},{"repository":"engine","workspace":"aurora","root":{{Root("engine")}}},{"repository":"tools"}]"""
                : "[]";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    [GeneratedRegex("\"at\": ?\"[^\"]+\"")]
    private static partial Regex StandingMoment();
}
