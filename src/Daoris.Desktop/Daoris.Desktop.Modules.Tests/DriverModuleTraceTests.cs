using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// TRACE1b (D143, D50): the screen's door to <c>daoris-driver trace</c>. <c>TRACE</c> reads the same stores the terminal
/// reads, through the driver's own <see cref="Trace.ReadChainAsync"/>: the service's session records, quests and asks, and this
/// machine's events, rules files, landings and standing answers under the home. It answers the chain as data, each link with
/// the store it was read from and a missing link with why, and never a machine path (D47 §4): a tree by its folder's name, a
/// file under the home by its store, and what a failed read said stays the terminal's.
/// </summary>
/// <remarks>On stand-in records: the service is a stand-in reached through the real client, the home this test's own.</remarks>
public sealed class DriverModuleTraceTests : DriverModuleBridge
{
    private static DateTimeOffset At(int hour, int minute) => new(2026, 10, 3, hour, minute, 0, TimeSpan.Zero);

    private const string Tree = "C:/machine/trees/dashboards-q1";

    private static JsonObject Record(string id, string state, int opened, string? evidence = null, string? profile = "work") => new()
    {
        ["id"] = id, ["quest"] = "q1", ["repository"] = "dashboards", ["adapter"] = "claude-code", ["state"] = state,
        ["evidence"] = evidence, ["created"] = At(9, opened).ToString("O"), ["updated"] = At(10, opened).ToString("O"),
        ["kind"] = "driven", ["harnessVersion"] = "2.1.4", ["profile"] = profile, ["tree"] = Tree, ["baseCommit"] = "abc1234",
    };

    private static JsonArray Sessions() =>
    [
        Record("s1", "failed", 5, evidence: "no commits landed", profile: "personal"),
        Record("s2", "completed", 40, evidence: "commits landed:\n1a2b3c4 Read the figure through v3"),
    ];

    private static JsonArray Quests() =>
    [
        new JsonObject
        {
            ["id"] = "q1", ["from"] = "ask #a1", ["to"] = "dashboards", ["title"] = "Fix the dashboard figure", ["status"] = "Done",
            ["filed"] = At(9, 2).ToString("O"), ["updated"] = At(11, 0).ToString("O"),
            ["requirements"] = new JsonArray(new JsonObject { ["quote"] = "use the v3 bridge", ["check"] = "reads through v3" }),
            ["answers"] = new JsonArray(new JsonObject { ["requirement"] = 1, ["met"] = "the tile reads through v3" }),
        },
    ];

    private const string AskJson = """
        {"id":"a1","workspace":"work","sentence":"use the v3 bridge","state":"Published","tier":"intake","quests":["q1"],
         "words":[{"kind":"asked","text":"use the v3 bridge","at":"2026-10-03T09:00:00+00:00"}],"goAheads":[]}
        """;

    /// <summary>This machine's half, by the driver's own writers: the events, a landing, a rules file that does not read, a standing answer.</summary>
    private void Machine()
    {
        var events = new SessionEvents(Path.Combine(Home, "sessions"));
        events.Append("s2", new SessionEvent { Kind = SessionEventKind.Note, Text = "carried on from session `s1` on `work`.", At = At(9, 40) });
        events.Append("s2", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = new string('y', 2000), At = At(9, 40) });
        new LandedBranches(Home).Record(new LandedBranch(
            "dashboards", "work", "feature/q1-fix", "main", "1a2b3c4d5e6f", "s2", "q1", "Fix", At(10, 45))
        {
            Plugin = "github", Pushed = true, AcceptedBy = AcceptedBy.Auto, Rule = new LandedRule("github", AutoAccept: true, LandingSource.Workspace),
        });
        // LAND2b's due list: the entry keeps its tree as a machine path, which never reaches the page.
        var due = new AutoLandings(Home);
        due.Due(new AutoLanding("s2", "q1", "dashboards", "work", Tree, At(10, 40)));
        due.Tried("s2", new AutoTry(At(10, 41), AutoLandingCode.Uncommitted) { Uncommitted = 2, Tip = "9f8e7d6c5b4a", Status = "sha256:abcd" }, close: false);
        due.Tried("s2", new AutoTry(At(10, 45), AutoLandingCode.Landed) { Branch = "feature/q1-fix", Commits = 1, Tip = "1a2b3c4d5e6f" }, close: true);
        Directory.CreateDirectory(Path.Combine(Home, SpawnServers.Folder));
        File.WriteAllText(Path.Combine(Home, SpawnServers.Folder, "s1.settings.json"), "{not json");
        DriverConfig.Empty.WithStanding("dashboards", "test on dev first", At(9, 20)).Save(DriverConfigPath);
    }

    private async Task<(DriverModule Module, StandIn Service)> UpAsync(StandIn standIn)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(standIn)));
        return (new DriverModule(Bus, loop), standIn);
    }

    /// <summary>
    /// From a quest: the ask with the person's words, the quest with its requirement and its answer, then both sessions oldest
    /// first with what ran each, its tree by name, its instruction by size, its landing, and what stood when it started.
    /// </summary>
    [Fact]
    public async Task A_quest_answers_its_chain_from_the_stores_the_terminal_reads()
    {
        Machine();
        var (module, _) = await UpAsync(new StandIn(Sessions(), Quests(), id => id == "a1" ? AskJson : null));

        var answer = await AnswerAsync(module, "TRACE", new { kind = "quest", id = "q1" });

        var chain = answer.GetProperty("chain");
        Assert.Equal(("quest", "q1"), (chain.GetProperty("kind").GetString(), chain.GetProperty("id").GetString()));
        var links = chain.GetProperty("links").EnumerateArray().ToList();
        Assert.Equal(["ask", "quest", "session", "session"], links.Select(link => link.GetProperty("kind").GetString()));

        var ask = links[0].GetProperty("ask");
        Assert.Equal(("asks", "a1"), (ask.GetProperty("source").GetString(), ask.GetProperty("id").GetString()));
        Assert.Equal("use the v3 bridge", ask.GetProperty("words")[0].GetProperty("text").GetString());

        var quest = links[1].GetProperty("quest");
        Assert.Equal(("quests", "a1"), (quest.GetProperty("source").GetString(), quest.GetProperty("ask").GetString()));
        var requirement = quest.GetProperty("requirements")[0];
        Assert.Equal(("use the v3 bridge", "met", "the tile reads through v3"),
            (requirement.GetProperty("quote").GetString(), requirement.GetProperty("answer").GetString(), requirement.GetProperty("met").GetString()));

        var first = links[2].GetProperty("session");
        var second = links[3].GetProperty("session");
        Assert.Equal(("s1", "s2"), (first.GetProperty("id").GetString(), second.GetProperty("id").GetString()));
        Assert.Equal(("claude-code", "2.1.4", "work"), (second.GetProperty("agent").GetProperty("adapter").GetString(),
            second.GetProperty("agent").GetProperty("harness").GetString(), second.GetProperty("agent").GetProperty("account").GetString()));
        Assert.Equal("dashboards-q1", second.GetProperty("tree").GetString());
        Assert.Equal(("s1", "same"), (second.GetProperty("before").GetProperty("session").GetString(), second.GetProperty("before").GetProperty("tree").GetString()));
        Assert.Equal("carried on from session `s1` on `work`.", second.GetProperty("events").GetProperty("starts")[0].GetString());
        Assert.Equal(2000, second.GetProperty("events").GetProperty("instructions")[0].GetProperty("chars").GetInt32());
        var landing = second.GetProperty("landing");
        Assert.Equal(("landings", "feature/q1-fix"), (landing.GetProperty("source").GetString(), landing.GetProperty("branches")[0].GetProperty("branch").GetString()));
        // LAND2b: who accepted it and the rule it was made under, and its entry on the due list with each try by its code.
        var branch = landing.GetProperty("branches")[0];
        Assert.Equal(("auto", "workspace", true), (branch.GetProperty("acceptedBy").GetString(),
            branch.GetProperty("rule").GetProperty("source").GetString(), branch.GetProperty("rule").GetProperty("autoAccept").GetBoolean()));
        var due = landing.GetProperty("due");
        Assert.Equal("auto-landings", due.GetProperty("source").GetString());
        Assert.Equal(["uncommitted", "landed"], due.GetProperty("tries").EnumerateArray().Select(tried => tried.GetProperty("code").GetString()));
        Assert.Equal(2, due.GetProperty("tries")[0].GetProperty("uncommitted").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, due.GetProperty("closed").ValueKind);
        Assert.False(due.TryGetProperty("tree", out _));
        Assert.False(due.TryGetProperty("status", out _));
        var standing = second.GetProperty("stood").GetProperty("standing");
        Assert.Equal(("config", "before", "test on dev first"),
            (standing.GetProperty("source").GetString(), standing.GetProperty("state").GetString(), standing.GetProperty("says").GetString()));
    }

    /// <summary>
    /// A missing link is said missing, by its code and where it would have been: an ended session's rules, its record here,
    /// its landing; and a rules file that does not read says so without what the read said, which may name a file.
    /// </summary>
    [Fact]
    public async Task A_missing_link_is_answered_missing_with_why_and_never_guessed()
    {
        Machine();
        var (module, _) = await UpAsync(new StandIn(Sessions(), Quests(), id => id == "a1" ? AskJson : null));

        var answer = await AnswerAsync(module, "TRACE", new { kind = "session", id = "s1" });

        var session = answer.GetProperty("chain").GetProperty("links").EnumerateArray()
            .Single(link => link.GetProperty("kind").GetString() == "session").GetProperty("session");
        Assert.Equal("none-here", session.GetProperty("events").GetProperty("missing").GetString());
        Assert.Equal("unread", session.GetProperty("rules").GetProperty("missing").GetString());
        Assert.False(session.GetProperty("rules").TryGetProperty("problem", out _));
        Assert.Equal("unknown", session.GetProperty("landing").GetProperty("missing").GetString());
        Assert.True(session.GetProperty("before").GetProperty("first").GetBoolean());
        Assert.Equal("after", session.GetProperty("stood").GetProperty("standing").GetProperty("state").GetString());
    }

    /// <summary>An ask the service no longer holds is a link said not found, named by the quest whose sender names it.</summary>
    [Fact]
    public async Task An_ask_nothing_holds_is_answered_not_found()
    {
        var (module, _) = await UpAsync(new StandIn(Sessions(), Quests(), _ => null));

        var answer = await AnswerAsync(module, "TRACE", new { kind = "quest", id = "q1" });

        var ask = answer.GetProperty("chain").GetProperty("links")[0].GetProperty("ask");
        Assert.Equal(("not-found", "quest", "q1"),
            (ask.GetProperty("missing").GetString(), ask.GetProperty("namedBy").GetProperty("kind").GetString(), ask.GetProperty("namedBy").GetProperty("id").GetString()));
    }

    /// <summary>
    /// 🔴 No machine path reaches the page (D47 §4): not the tree's, not the home's, not a file under it, and not what a failed
    /// read said; the stores are named by code.
    /// </summary>
    [Fact]
    public async Task The_answer_carries_no_machine_path()
    {
        Machine();
        var (module, _) = await UpAsync(new StandIn(Sessions(), Quests(), id => id == "a1" ? AskJson : null));

        var raw = (await AnswerAsync(module, "TRACE", new { kind = "quest", id = "q1" })).GetRawText();

        Assert.DoesNotContain("C:/machine", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Home.Replace('\\', '/'), raw.Replace("\\\\", "/"), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("settings.json", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("events.jsonl", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("treePath", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("problem", raw, StringComparison.Ordinal);
    }

    /// <summary>A store that does not answer is named by code, and a session nothing names answers no chain.</summary>
    [Fact]
    public async Task A_store_that_does_not_answer_is_named_and_nothing_found_answers_no_chain()
    {
        var (module, _) = await UpAsync(new StandIn(null, Quests(), _ => null));

        var answer = await AnswerAsync(module, "TRACE", new { kind = "session", id = "s1" });

        Assert.Equal(JsonValueKind.Null, answer.GetProperty("chain").ValueKind);
        Assert.Equal("sessions", answer.GetProperty("unread")[0].GetProperty("store").GetString());
        Assert.DoesNotContain("refused", answer.GetRawText(), StringComparison.Ordinal);
    }

    /// <summary>The route reads, and writes nothing: every request to the service is a GET.</summary>
    [Fact]
    public async Task The_trace_only_reads()
    {
        Machine();
        var (module, standIn) = await UpAsync(new StandIn(Sessions(), Quests(), id => id == "a1" ? AskJson : null));
        standIn.Requests.Clear();

        await AnswerAsync(module, "TRACE", new { kind = "quest", id = "q1" });

        Assert.NotEmpty(standIn.Requests);
        Assert.All(standIn.Requests, request => Assert.StartsWith("GET ", request));
    }

    [Fact]
    public async Task A_kind_that_is_none_of_the_three_is_refused_in_the_drivers_words()
    {
        var (module, _) = await UpAsync(new StandIn(Sessions(), Quests(), _ => null));

        var refusal = await RefusalAsync(module, "TRACE", new { kind = "branch", id = "main" });

        Assert.Contains("a trace starts from a commit, a session or a quest", refusal);
    }

    [Fact]
    public async Task Before_the_loop_is_up_the_trace_says_so()
    {
        var refusal = await RefusalAsync(Module(), "TRACE", new { kind = "quest", id = "q1" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }

    /// <summary>A service standing in: each request recorded with its method, then answered, or 404 where nothing is.</summary>
    private sealed class StandIn(JsonArray? sessions, JsonArray quests, Func<string, string?> asks) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            lock (Requests) Requests.Add($"{request.Method} {uri.PathAndQuery}");
            if (uri.AbsolutePath == "/api/sessions" && sessions is null) throw new HttpRequestException("refused at C:/machine/socket");
            var body = uri.AbsolutePath switch
            {
                "/api/sessions" when uri.Query.Contains("includeClosed=true") => sessions!.ToJsonString(),
                "/api/quests" when uri.Query.Contains("includeClosed=true") => quests.ToJsonString(),
                var path when path.StartsWith("/api/asks/", StringComparison.Ordinal) => asks(path["/api/asks/".Length..]),
                _ => null,
            };
            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"error":"none"}""", Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
