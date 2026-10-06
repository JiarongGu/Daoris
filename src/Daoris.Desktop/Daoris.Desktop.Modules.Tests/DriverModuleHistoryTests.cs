using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// HIST1c (D153; the history-clearing design §6.3): clearing finished history over the bridge, the driver's door
/// (<c>DriverModule.History.cs</c>). The first press lists each unit with what it takes and why it would stay, by the
/// catalogue's codes, and a workspace's reading; the second sends exactly the units the list held. Nothing machine-local
/// comes back: ids, counts and bytes, never a path or a title.
/// </summary>
public sealed class DriverModuleHistoryTests : DriverModuleBridge
{
    private static byte[] Json(string text) => Encoding.UTF8.GetBytes(text);

    private static string Unit(string kind, string id, string sessions = "[]", string? refusal = null, string kept = "[]", string quests = "[]") =>
        $$"""{"kind":"{{kind}}","id":"{{id}}","workspace":"aurora","clearable":{{(refusal is null ? "true" : "false")}},"quests":{{quests}},"forgotten":[],"asks":[],"sessions":{{sessions}},"teammates":[],"kept":{{kept}}{{(refusal is null ? "" : $",\"refusal\":{refusal}")}}}""";

    /// <summary>The service's doors a clear reads, over a real socket: one closed quest's work, its session, and the listings set.</summary>
    private LoopbackHost Service(IReadOnlyDictionary<string, string> listings, string? cleared = null, string? tree = null)
    {
        var host = new LoopbackHost();
        var at = tree is null ? "" : $",\"tree\":\"{tree.Replace('\\', '/')}\"";
        host.Serve("/api/sessions?includeClosed=true", Json(
            $$"""[{"id":"s1","quest":"q1","repository":"engine","adapter":"claude-code","state":"completed","kind":"driven","workspace":"aurora"{{at}},"created":"2026-10-02T09:00:00Z","updated":"2026-10-02T09:10:00Z"},{"id":"c1","repository":"engine","adapter":"claude-code","state":"completed","kind":"chat","workspace":"aurora","created":"2026-10-02T09:00:00Z","updated":"2026-10-02T09:10:00Z"}]"""));
        host.Serve("/api/sessions", Json("[]"));
        host.Serve("/api/quests?includeClosed=true", Json(
            """[{"id":"q1","from":"game","to":"engine","title":"A secret title","body":"","status":"Done","workspace":"aurora"},{"id":"qt","from":"game","to":"engine","title":"t","body":"","status":"Taken","workspace":"aurora"}]"""));
        host.Serve("/api/quests", Json("[]"));
        host.Serve("/api/asks?includeClosed=true", Json("[]"));
        host.Serve("/api/registry", Json("[]"));
        foreach (var (query, units) in listings) host.Serve($"/api/history?{query}", Json($$"""{"units":[{{units}}]}"""));
        if (cleared is not null) host.Serve("/api/history/clear", Json($$"""{"units":[{{cleared}}]}"""));
        return host;
    }

    private async Task<DriverModule> UpAsync(LoopbackHost service)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient(service.Address, null));
        return new DriverModule(Bus, loop);
    }

    private void Kept(string id)
    {
        Directory.CreateDirectory(Path.Combine(Home, "sessions", id, "files"));
        File.WriteAllText(Path.Combine(Home, "sessions", $"{id}.events.jsonl"), "{\"seq\":1,\"kind\":\"user\",\"text\":\"a secret plan\"}\n");
        File.WriteAllText(Path.Combine(Home, "sessions", $"{id}.log"), "the transcript");
        File.WriteAllText(Path.Combine(Home, "sessions", id, "files", "shot.png"), "png");
    }

    /// <summary>Before the driver's service answers, both presses say the cold-start sentence.</summary>
    [Fact]
    public async Task History_before_the_service_answers_says_so()
    {
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "HISTORY_PLAN", new { quest = "q1" }));
        Assert.Contains(Refusals.DriverNotReady, await RefusalAsync(Module(), "HISTORY_CLEAR", new { quest = "q1", units = Array.Empty<object>() }));
    }

    /// <summary>A scope is one of a workspace, a quest or an ask, named once; a press sends the list it held.</summary>
    [Fact]
    public async Task A_scope_is_named_once_and_a_press_sends_its_list()
    {
        using var service = Service(new Dictionary<string, string>());
        var module = await UpAsync(service);

        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "HISTORY_PLAN", new { }));
        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "HISTORY_PLAN", new { quest = "q1", ask = "a1" }));
        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "HISTORY_PLAN", new { ask = "a1", failed = true }));
        Assert.Contains(Refusals.DriverRefused, await RefusalAsync(module, "HISTORY_CLEAR", new { quest = "q1" }));
    }

    /// <summary>
    /// One quest's first press: its unit with what it takes and what that holds on the disk, by id and by bytes; no path and
    /// no title comes back.
    /// </summary>
    [Fact]
    public async Task A_quests_first_press_lists_its_unit_by_ids_and_bytes()
    {
        Kept("s1");
        using var service = Service(new Dictionary<string, string> { ["quest=q1"] = Unit("quest", "q1", sessions: """["s1"]""", quests: """["q1"]""") });
        var module = await UpAsync(service);

        var plan = await AnswerAsync(module, "HISTORY_PLAN", new { quest = "#q1" });

        Assert.Equal(("quest", "q1"), (plan.GetProperty("scope").GetString(), plan.GetProperty("id").GetString()));
        var unit = Assert.Single(plan.GetProperty("units").EnumerateArray());
        Assert.True(unit.GetProperty("clearable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, unit.GetProperty("keep").ValueKind);
        Assert.Equal(["s1"], unit.GetProperty("sessions").EnumerateArray().Select(each => each.GetString()));
        Assert.True(unit.GetProperty("bytes").GetProperty("total").GetInt64() > 0);
        Assert.Equal(JsonValueKind.Null, plan.GetProperty("reading").ValueKind);
        Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), plan.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", plan.GetRawText());
    }

    /// <summary>A quest this machine does not hold is refused at the first press, in the catalogue's words.</summary>
    [Fact]
    public async Task A_quest_this_machine_does_not_hold_is_refused_as_unknown()
    {
        using var service = Service(new Dictionary<string, string>
        {
            ["quest=q9"] = Unit("quest", "q9", refusal: """{"refusal":"unknown","error":"No quest `#q9` on this machine.","quest":"q9"}"""),
        });

        var refusal = await RefusalAsync(await UpAsync(service), "HISTORY_PLAN", new { quest = "q9" });

        Assert.Contains(Refusals.HistoryUnknown, refusal);
        Assert.Contains("quest=q9", refusal);
        Assert.Contains("context=quest", refusal);
    }

    /// <summary>
    /// A workspace's first press: each unit, a kept one with its code, and the reading (§2.4), counts and bytes; a teammate's
    /// failed session listed and kept carries its code too.
    /// </summary>
    [Fact]
    public async Task A_workspaces_first_press_carries_each_unit_by_code_and_the_reading()
    {
        Kept("s1");
        Kept("c1");
        using var service = Service(new Dictionary<string, string>
        {
            ["workspace=aurora"] = Unit("quest", "q1", sessions: """["s1"]""", quests: """["q1"]""") + ","
                                   + Unit("quest", "qt", refusal: """{"refusal":"open","error":"taken","quest":"qt"}"""),
            ["quest=q1&failed=true"] = Unit("failed", "q1", kept: """[{"refusal":"not-ours","error":"theirs","session":"laptop/f2","origin":"laptop"}]"""),
        });
        var module = await UpAsync(service);

        var plan = await AnswerAsync(module, "HISTORY_PLAN", new { workspace = "aurora" });
        var failed = await AnswerAsync(module, "HISTORY_PLAN", new { quest = "q1", failed = true });

        var units = plan.GetProperty("units").EnumerateArray().ToDictionary(unit => unit.GetProperty("id").GetString()!);
        var keep = units["qt"].GetProperty("keep");
        Assert.Equal((Refusals.HistoryOpen, "taken", "qt"), (keep.GetProperty("code").GetString(), keep.GetProperty("context").GetString(), keep.GetProperty("quest").GetString()));
        var reading = plan.GetProperty("reading");
        Assert.Equal(1, reading.GetProperty("quests").GetInt32());
        Assert.Equal(1, reading.GetProperty("takes").GetProperty("quests").GetInt32());
        Assert.Equal(1, reading.GetProperty("keptBy").GetProperty(Refusals.HistoryOpen).GetInt32());
        Assert.Equal(1, reading.GetProperty("conversations").GetProperty("count").GetInt32());
        Assert.True(reading.GetProperty("bytes").GetProperty("total").GetInt64() > 0);
        Assert.True(reading.TryGetProperty("leftOver", out _));
        Assert.True(reading.TryGetProperty("log", out _));
        var piece = Assert.Single(Assert.Single(failed.GetProperty("units").EnumerateArray()).GetProperty("kept").EnumerateArray());
        Assert.Equal((Refusals.HistoryNotOurs, "laptop"), (piece.GetProperty("code").GetString(), piece.GetProperty("machine").GetString()));
        Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), plan.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// HIST1d (D50; the history-clearing design §6.2): <c>daoris-driver history --workspace --json</c> prints this route's answer
    /// field for field. Both serialize the driver library's one projection (<see cref="HistoryAnswers"/>): here the route's
    /// answer, as the page receives it, is the terminal's for the same home and service, value for value, and each object's
    /// fields are the lists the terminal's own test holds.
    /// </summary>
    [Fact]
    public async Task The_terminals_history_json_is_this_routes_answer_field_for_field()
    {
        Kept("s1");
        Kept("c1");
        using var service = Service(new Dictionary<string, string>
        {
            ["workspace=aurora"] = Unit("quest", "q1", sessions: """["s1"]""", quests: """["q1"]""") + ","
                                   + Unit("quest", "qt", refusal: """{"refusal":"open","error":"taken","quest":"qt"}"""),
        });
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient(service.Address, null));
        var module = new DriverModule(Bus, loop);

        var page = await AnswerAsync(module, "HISTORY_PLAN", new { workspace = "aurora" });
        var output = new StringWriter();
        var exit = await HistoryCommand.RunAsync(
            HistoryCommand.Read(["--workspace", "aurora", "--json"], out _)!,
            new HistoryWorld(new ServiceClient(service.Address, null), loop.Home, loop.ConfigPath, loop.Processes),
            output);

        Assert.Equal(0, exit);
        var terminal = System.Text.Json.Nodes.JsonNode.Parse(output.ToString());
        var route = System.Text.Json.Nodes.JsonNode.Parse(page.GetRawText());
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(route, terminal), $"the route answered {route}, the terminal printed {terminal}");
        Assert.Equal(HistoryAnswers.PlanFields, page.EnumerateObject().Select(field => field.Name));
        var units = page.GetProperty("units").EnumerateArray().ToList();
        Assert.All(units, unit => Assert.Equal(HistoryAnswers.UnitFields, unit.EnumerateObject().Select(field => field.Name)));
        var keep = units.Single(unit => unit.GetProperty("id").GetString() == "qt").GetProperty("keep");
        Assert.Equal(HistoryAnswers.ReasonFields, keep.EnumerateObject().Select(field => field.Name));
        Assert.Equal(HistoryAnswers.ReadingFields, page.GetProperty("reading").EnumerateObject().Select(field => field.Name));
    }

    /// <summary>The catalogue's history codes are the driver library's, which the terminal prints, and its verbatim refusal the shell's.</summary>
    [Fact]
    public void The_history_codes_are_the_driver_librarys()
    {
        Assert.Equal(Refusals.DriverRefused, HistoryCodes.Refused);
        Assert.Equal(
            [
                Refusals.HistoryUnknown, Refusals.HistoryOpen, Refusals.HistoryAsked, Refusals.HistoryLive, Refusals.HistoryNeedsYou,
                Refusals.HistoryAwaited, Refusals.HistoryTreeHere, Refusals.HistoryLandingStands, Refusals.HistoryUnpushed,
                Refusals.HistoryNotOurs,
            ],
            new[]
            {
                HistoryWords.Unknown, HistoryWords.Open, HistoryWords.Asked, HistoryWords.Live, HistoryWords.NeedsYou, HistoryWords.Awaited,
                HistoryWords.TreeHere, HistoryWords.LandingStands, HistoryWords.Unpushed, HistoryWords.NotOurs,
            }.Select(HistoryCodes.Of));
        Assert.Equal(Refusals.DriverRefused, HistoryCodes.Of("a-word-from-later"));
    }

    /// <summary>The service's word for a unit, the code the catalogue says it in, and a fact the sentence names.</summary>
    public static TheoryData<string, string, string> ServiceRefusals => new()
    {
        { """{"refusal":"open","error":"s","quest":"qt"}""", Refusals.HistoryOpen, "context=taken" },
        { """{"refusal":"asked","error":"s","quest":"q1","ask":"a1"}""", Refusals.HistoryAsked, "ask=a1" },
        { """{"refusal":"live","error":"s","session":"laptop/t1","origin":"laptop"}""", Refusals.HistoryLive, "machine=laptop" },
        { """{"refusal":"needs-you","error":"s","quest":"q1"}""", Refusals.HistoryNeedsYou, "context=conflict" },
        { """{"refusal":"awaited","error":"s","quest":"qt"}""", Refusals.HistoryAwaited, "context=chain" },
        { """{"refusal":"unpushed","error":"s","quest":"q1","workspace":"aurora"}""", Refusals.HistoryUnpushed, "workspace=aurora" },
        { """{"refusal":"not-ours","error":"s","session":"laptop/f2","origin":"laptop"}""", Refusals.HistoryNotOurs, "session=laptop/f2" },
        { """{"refusal":"unknown","error":"s","quest":"q1"}""", Refusals.HistoryUnknown, "context=quest" },
        { """{"refusal":"a-word-from-later","error":"the newer service's sentence"}""", Refusals.DriverRefused, "message=the newer service's sentence" },
    };

    /// <summary>
    /// One quest's second press that the service keeps is refused in the catalogue's code, with the facts its sentence names,
    /// read by the word and never the sentence; the service is not asked to clear it, and nothing is removed.
    /// </summary>
    [Theory]
    [MemberData(nameof(ServiceRefusals))]
    public async Task A_clear_the_service_keeps_is_said_in_the_catalogues_words(string refusal, string code, string fact)
    {
        Kept("s1");
        using var service = Service(new Dictionary<string, string> { ["quest=q1"] = Unit("quest", "q1", sessions: """["s1"]""", refusal: refusal) });

        var refused = await RefusalAsync(await UpAsync(service), "HISTORY_CLEAR", new { quest = "q1", units = new[] { new { kind = "quest", id = "q1" } } });

        Assert.Contains(code, refused);
        Assert.Contains(fact, refused);
        Assert.True(File.Exists(Path.Combine(Home, "sessions", "s1.log")));
    }

    /// <summary>This machine's half, said in the catalogue's words: a tree still here, and a landing's branch still standing.</summary>
    [Fact]
    public async Task A_clear_this_machine_keeps_is_said_in_the_catalogues_words()
    {
        var tree = Path.Combine(new SessionTrees(Home).TreesRoot, "aurora", "engine", "s-1a2b3c4d");
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, "work.txt"), "work");
        using var treed = Service(new Dictionary<string, string> { ["quest=q1"] = Unit("quest", "q1", sessions: """["s1"]""") }, tree: tree);

        var here = await RefusalAsync(await UpAsync(treed), "HISTORY_CLEAR", new { quest = "q1", units = new[] { new { kind = "quest", id = "q1" } } });

        Assert.Contains(Refusals.HistoryTreeHere, here);
        Assert.Contains("session=s1", here);
        Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), here, StringComparison.OrdinalIgnoreCase);

        new SessionTrees(Home).Recorded.Record(new LandedBranch(
            "engine", "aurora", "feature/q1-fix", "main", "abc123", "s1", "q1", null, DateTimeOffset.UtcNow));
        using var landed = Service(new Dictionary<string, string> { ["quest=q1"] = Unit("quest", "q1", sessions: """["s1"]""") });

        var stands = await RefusalAsync(await UpAsync(landed), "HISTORY_CLEAR", new { quest = "q1", units = new[] { new { kind = "quest", id = "q1" } } });

        Assert.Contains(Refusals.HistoryLandingStands, stands);
        Assert.Contains("branch=feature/q1-fix", stands);
        Assert.Contains("repository=engine", stands);
    }

    /// <summary>
    /// The second press sends exactly the list, and says what went in counts and bytes; what the home kept of the cleared
    /// session goes, and the answer names no path.
    /// </summary>
    [Fact]
    public async Task A_clear_sends_the_list_and_says_what_went()
    {
        Kept("s1");
        Kept("c1");
        var unit = Unit("quest", "q1", sessions: """["s1"]""", quests: """["q1"]""");
        using var service = Service(
            new Dictionary<string, string> { ["workspace=aurora"] = unit },
            cleared: $$"""{"unit":{{unit}},"cleared":true,"message":"Cleared `#q1` from this machine."}""");
        var module = await UpAsync(service);

        var answer = await AnswerAsync(module, "HISTORY_CLEAR", new { workspace = "aurora", units = new[] { new { kind = "quest", id = "q1" } } });

        Assert.Equal(("workspace", "aurora", 1), (answer.GetProperty("scope").GetString(), answer.GetProperty("id").GetString(), answer.GetProperty("listed").GetInt32()));
        Assert.Equal(["q1"], answer.GetProperty("cleared").EnumerateArray().Select(each => each.GetProperty("id").GetString()));
        Assert.Equal((1, 1), (answer.GetProperty("quests").GetInt32(), answer.GetProperty("sessions").GetInt32()));
        Assert.True(answer.GetProperty("bytes").GetInt64() > 0);
        Assert.Empty(answer.GetProperty("changed").EnumerateArray());
        Assert.False(File.Exists(Path.Combine(Home, "sessions", "s1.log")));
        Assert.True(File.Exists(Path.Combine(Home, "sessions", "c1.log")));
        Assert.DoesNotContain(JsonSerializer.Serialize(Home).Trim('"'), answer.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }
}
