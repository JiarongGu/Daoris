using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The workflow writes as routes (AGENTSPLACE1c; D194 point 4): <c>WORKFLOW_SAVE</c>, <c>WORKFLOW_USE</c> and <c>WORKFLOW_KIND</c> run the
/// typed operations the terminal's <c>workflow new|use|kind</c> run, so a version is made under a fresh id or appended against the base the
/// caller read, a choice is one change, a kind is declared, and each answer says the integration design's §17.7 reach.
/// </summary>
public sealed class DriverModuleWorkflowWritesTests : DriverModuleBridge
{
    /// <summary>Two graphs that differ (WORKFLOWCLEAN1 1b: a workflow is saved as a graph; a list of steps is refused).</summary>
    private static readonly string Steps = WorkflowDefaults.Of("merge-after-accept")!.GraphJson;

    private static readonly string OtherSteps = WorkflowDefaults.Of("release")!.GraphJson;

    private async Task<(DriverModule Module, DriverLoop Loop)> UpAsync()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandInService())));
        return (module, loop);
    }

    private static JsonNode Parse(string json) => JsonNode.Parse(json)!;

    private static List<string?> Keys(JsonElement answer) =>
        [.. answer.GetProperty("scopes").EnumerateArray().Select(each => each.GetProperty("key").GetString())];

    private int NewestOnDisk(string id) => WorkflowStore.Load(Home, id)!.Read.Versions[^1].Version;

    [Fact]
    public async Task A_workflow_is_saved_under_a_fresh_id_from_a_preset_and_a_plan_writes_nothing()
    {
        var (module, _) = await UpAsync();

        var plan = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", from = "pull-request", plan = true });
        Assert.Equal((true, false, 1), (plan.GetProperty("done").GetBoolean(), plan.GetProperty("written").GetBoolean(), plan.GetProperty("version").GetInt32()));
        Assert.Null(WorkflowStore.Load(Home, "docs"));

        var saved = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", from = "integrated", withLook = true, name = "Docs" });
        Assert.Equal((true, true, false), (saved.GetProperty("done").GetBoolean(), saved.GetProperty("written").GetBoolean(), saved.GetProperty("appended").GetBoolean()));
        var read = WorkflowStore.Load(Home, "docs")!.Read;
        Assert.Equal(("Docs", 1, "screen"), (read.Name, read.Versions.Count, read.Versions[0].Door));
        Assert.NotNull(read.Versions[0].Graph);

        var again = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", from = "integrated" });
        Assert.Equal("refused", again.GetProperty("refused").GetString());
        Assert.Contains("is a workflow here already", again.GetProperty("message").GetString());
        Assert.Equal(1, NewestOnDisk("docs"));
    }

    /// <summary>WORKFLOWCLEAN1 1b: the window's save refuses a list of steps, and a copy of a step preset, with the one judge's words and writes nothing.</summary>
    [Fact]
    public async Task A_save_of_steps_is_refused_with_the_steps_code_and_writes_nothing()
    {
        var (module, _) = await UpAsync();

        var refused = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", steps = Parse("""[{"id":"work","kind":"work"}]""") });

        Assert.Equal((false, "steps-refused"), (refused.GetProperty("done").GetBoolean(), refused.GetProperty("refused").GetString()));
        Assert.Equal(WorkflowNamed.StepsRefused("docs"), refused.GetProperty("message").GetString());
        Assert.Null(WorkflowStore.Load(Home, "docs"));

        var preset = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", from = "pull-request-no-press" });
        Assert.Equal((false, "refused"), (preset.GetProperty("done").GetBoolean(), preset.GetProperty("refused").GetString()));
        Assert.Contains("is no preset", preset.GetProperty("message").GetString());
        Assert.Null(WorkflowStore.Load(Home, "docs"));
    }

    [Fact]
    public async Task A_save_onto_a_saved_id_names_the_version_it_was_made_against_and_a_stale_one_is_refused()
    {
        var (module, _) = await UpAsync();
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Parse(Steps) });

        var unnamed = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Parse(OtherSteps) });
        Assert.Equal("stale", unnamed.GetProperty("refused").GetString());

        var appended = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Parse(OtherSteps), @base = 1 });
        Assert.Equal((true, true, true, 2), (appended.GetProperty("done").GetBoolean(), appended.GetProperty("written").GetBoolean(),
            appended.GetProperty("appended").GetBoolean(), appended.GetProperty("version").GetInt32()));
        Assert.Empty(appended.GetProperty("scopes").EnumerateArray());
        Assert.Equal(2, NewestOnDisk("docs"));

        var stale = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Parse(Steps), @base = 1 });
        Assert.Equal((false, "stale"), (stale.GetProperty("done").GetBoolean(), stale.GetProperty("refused").GetString()));
        Assert.Contains("`docs` is at v2 now, not v1", stale.GetProperty("message").GetString());
        Assert.Equal(2, NewestOnDisk("docs"));

        var same = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Parse(OtherSteps), @base = 2 });
        Assert.Equal((true, false), (same.GetProperty("done").GetBoolean(), same.GetProperty("written").GetBoolean()));
        Assert.Contains("already says this", same.GetProperty("message").GetString());
        Assert.Equal(2, NewestOnDisk("docs"));
    }

    [Fact]
    public async Task An_append_says_every_place_that_chooses_the_workflow_and_writes_only_what_was_authorized()
    {
        File.WriteAllText(DriverConfigPath, """
            {
              "workflows": { "engine": { "default": "docs" } },
              "workspaceWorkflows": { "aurora": { "kinds": { "docs": { "label": "Documentation", "paths": ["docs/**"], "workflow": "docs" } } } }
            }
            """);
        var (module, _) = await UpAsync();
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Parse(Steps) });

        var asked = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Parse(OtherSteps), @base = 1 });
        Assert.Equal((false, "reach", true), (asked.GetProperty("done").GetBoolean(), asked.GetProperty("refused").GetString(), asked.GetProperty("appended").GetBoolean()));
        Assert.Equal(["repository:engine:default", "workspace:aurora:kind:docs"], Keys(asked));
        Assert.Equal(1, NewestOnDisk("docs"));

        var partial = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Parse(OtherSteps), @base = 1, authorize = new[] { "repository:engine:default" } });
        Assert.Equal("reach", partial.GetProperty("refused").GetString());
        Assert.Equal(1, NewestOnDisk("docs"));

        var planned = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Parse(OtherSteps), @base = 1, plan = true });
        Assert.Equal((true, false), (planned.GetProperty("done").GetBoolean(), planned.GetProperty("written").GetBoolean()));
        Assert.Equal(2, Keys(planned).Count);

        var written = await AnswerAsync(module, "WORKFLOW_SAVE",
            new { id = "docs", graph = Parse(OtherSteps), @base = 1, authorize = new[] { "workspace:aurora:kind:docs", "repository:engine:default" } });
        Assert.Equal((true, true, 2), (written.GetProperty("done").GetBoolean(), written.GetProperty("written").GetBoolean(), written.GetProperty("version").GetInt32()));
        Assert.Equal(2, NewestOnDisk("docs"));
    }

    [Fact]
    public async Task A_workflow_is_chosen_for_a_place_in_one_change_and_cleared()
    {
        var (module, _) = await UpAsync();
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Parse(Steps) });

        var chosen = await AnswerAsync(module, "WORKFLOW_USE", new { repository = "engine", workflow = "docs", version = 1 });
        Assert.Equal((true, true), (chosen.GetProperty("done").GetBoolean(), chosen.GetProperty("changed").GetBoolean()));
        Assert.Contains("now chooses `docs` as its default", chosen.GetProperty("message").GetString());
        Assert.Equal("docs", DriverConfig.Load(DriverConfigPath).Workflows["engine"].Default);
        Assert.Equal("docs", chosen.GetProperty("start").GetProperty("routes")[0].GetProperty("workflow").GetString());

        var already = await AnswerAsync(module, "WORKFLOW_USE", new { repository = "engine", workflow = "docs" });
        Assert.Equal((true, false), (already.GetProperty("done").GetBoolean(), already.GetProperty("changed").GetBoolean()));
        Assert.Contains("nothing was written", already.GetProperty("message").GetString());

        var workspace = await AnswerAsync(module, "WORKFLOW_USE", new { workspace = "aurora", workflow = "docs" });
        Assert.True(workspace.GetProperty("done").GetBoolean());
        Assert.Equal("docs", DriverConfig.Load(DriverConfigPath).WorkspaceWorkflows["aurora"].Default);

        var cleared = await AnswerAsync(module, "WORKFLOW_USE", new { repository = "engine" });
        Assert.Equal((true, true), (cleared.GetProperty("done").GetBoolean(), cleared.GetProperty("changed").GetBoolean()));
        Assert.False(DriverConfig.Load(DriverConfigPath).Workflows.ContainsKey("engine"));
    }

    [Fact]
    public async Task A_choice_of_a_workflow_not_saved_or_moved_is_refused_and_writes_nothing()
    {
        var (module, _) = await UpAsync();
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Parse(Steps) });
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Parse(OtherSteps), @base = 1 });

        var missing = await AnswerAsync(module, "WORKFLOW_USE", new { repository = "engine", workflow = "nope" });
        Assert.Equal((false, "refused"), (missing.GetProperty("done").GetBoolean(), missing.GetProperty("refused").GetString()));
        Assert.Contains("no workflow `nope` is saved here", missing.GetProperty("message").GetString());

        var stale = await AnswerAsync(module, "WORKFLOW_USE", new { repository = "engine", workflow = "docs", version = 1 });
        Assert.Equal("stale", stale.GetProperty("refused").GetString());
        Assert.Contains("`docs` is at v2 now, not v1", stale.GetProperty("message").GetString());

        var undeclared = await AnswerAsync(module, "WORKFLOW_USE", new { workspace = "aurora", workflow = "docs", kind = "docs" });
        Assert.Equal("refused", undeclared.GetProperty("refused").GetString());
        Assert.Contains("Nothing was written.", undeclared.GetProperty("message").GetString());
        Assert.False(File.Exists(DriverConfigPath) && File.ReadAllText(DriverConfigPath).Contains("\"workflows\"", StringComparison.Ordinal));

        Assert.Contains("a workflow is chosen for a `repository` or a `workspace`", await RefusalAsync(module, "WORKFLOW_USE", new { workflow = "docs" }));
    }

    [Fact]
    public async Task A_kind_is_declared_with_its_paths_mapped_to_a_workflow_and_dropped()
    {
        var (module, _) = await UpAsync();
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Parse(Steps) });

        var declared = await AnswerAsync(module, "WORKFLOW_KIND", new { workspace = "aurora", kind = "docs", label = "Documentation", paths = new[] { "docs/**", "**/*.md" } });
        Assert.Equal((true, true), (declared.GetProperty("done").GetBoolean(), declared.GetProperty("changed").GetBoolean()));
        Assert.Contains("declares kind `docs` — Documentation", declared.GetProperty("message").GetString());
        var kind = DriverConfig.Load(DriverConfigPath).WorkspaceWorkflows["aurora"].KindOf("docs")!;
        Assert.Equal("Documentation", kind.Label);
        Assert.Equal(["docs/**", "**/*.md"], kind.Paths);

        var mapped = await AnswerAsync(module, "WORKFLOW_USE", new { workspace = "aurora", workflow = "docs", kind = "docs" });
        Assert.True(mapped.GetProperty("done").GetBoolean());
        var route = mapped.GetProperty("start").GetProperty("routes").EnumerateArray().Single(each => each.GetProperty("kind").GetString() == "docs");
        Assert.Equal(("docs", "Documentation"), (route.GetProperty("workflow").GetString(), route.GetProperty("label").GetString()));

        var dropped = await AnswerAsync(module, "WORKFLOW_KIND", new { workspace = "aurora", kind = "docs", drop = true });
        Assert.True(dropped.GetProperty("done").GetBoolean());
        Assert.False(DriverConfig.Load(DriverConfigPath).WorkspaceWorkflows.ContainsKey("aurora"));

        var none = await AnswerAsync(module, "WORKFLOW_KIND", new { workspace = "aurora", kind = "docs", drop = true });
        Assert.Equal((false, "refused"), (none.GetProperty("done").GetBoolean(), none.GetProperty("refused").GetString()));
        Assert.Contains("a kind is declared with a `label`, or dropped", await RefusalAsync(module, "WORKFLOW_KIND", new { workspace = "aurora", kind = "docs" }));
    }


    [Fact]
    public async Task An_append_whose_graph_cannot_start_where_it_is_chosen_is_refused_with_the_reach_named()
    {
        File.WriteAllText(DriverConfigPath, """
            {
              "landings": { "engine": { "form": "branch", "pattern": "w/{quest}" } },
              "workflows": { "engine": { "default": "line" } }
            }
            """);
        var (module, _) = await UpAsync();
        var first = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "line", graph = WorkflowPresets.GraphOf("integrated")!.Graph() });
        Assert.True(first.GetProperty("done").GetBoolean(), first.GetProperty("message").GetString());
        // An integration with no press asks with `done`, which needs the line's gates, and `engine`'s line policy declares none.
        var noPress = WorkflowPresets.GraphOf("integrated-no-press")!.Graph();

        var refused = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "line", graph = noPress, @base = 1, authorize = new[] { "repository:engine:default" } });

        Assert.Equal((false, "reach"), (refused.GetProperty("done").GetBoolean(), refused.GetProperty("refused").GetString()));
        Assert.Equal("engine", Assert.Single(refused.GetProperty("reach").EnumerateArray()).GetProperty("repository").GetString());
        Assert.Equal(1, NewestOnDisk("line"));
    }

    /// <summary>The documents default's steps with the work step naming <paramref name="role"/>, and a <c>when</c> wire where asked.</summary>
    private static JsonObject Documents(string? role = null, bool when = false)
    {
        var graph = WorkflowPresets.GraphOf("documents")!.Graph();
        if (role is not null) graph["nodes"]![0]!["options"] = new JsonObject { ["role"] = role };
        if (when) graph["wires"]!.AsArray().Add(new JsonObject { ["id"] = "z", ["kind"] = "when", ["from"] = "work", ["outcome"] = "release", ["to"] = "line" });
        return graph;
    }

    private static List<(string? Code, string? Wire, string? Node)> Problems(JsonElement answer) =>
        [.. answer.GetProperty("problems").EnumerateArray().Select(each => (each.GetProperty("code").GetString(), each.GetProperty("wire").GetString(), each.GetProperty("node").GetString()))];

    [Fact]
    public async Task A_save_refuses_a_role_that_cannot_take_its_step_at_that_step_and_writes_nothing()
    {
        var (module, _) = await UpAsync();

        var reviewer = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Documents(role: "opinion"), plan = true });
        Assert.Equal((false, "refused"), (reviewer.GetProperty("done").GetBoolean(), reviewer.GetProperty("refused").GetString()));
        Assert.Equal([("role", null, "work")], Problems(reviewer));
        Assert.Contains("does not take `design`", reviewer.GetProperty("message").GetString());

        var unknown = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Documents(role: "nobody") });
        Assert.Equal([("role", null, "work")], Problems(unknown));
        Assert.Null(WorkflowStore.Load(Home, "docs"));

        var builder = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Documents(role: "build") });
        Assert.True(builder.GetProperty("done").GetBoolean(), builder.GetProperty("message").GetString());
        Assert.Empty(Problems(builder));
        Assert.Equal(1, NewestOnDisk("docs"));
    }

    [Fact]
    public async Task A_plan_of_steps_that_do_not_hold_answers_each_refusal_by_its_code_at_its_wire()
    {
        var (module, _) = await UpAsync();

        var planned = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Documents(when: true), plan = true });

        Assert.Equal((false, "refused"), (planned.GetProperty("done").GetBoolean(), planned.GetProperty("refused").GetString()));
        Assert.Contains(("no-outcomes", "z", (string?)null), Problems(planned).Select(each => (each.Code, each.Wire, (string?)null)));
        Assert.Null(WorkflowStore.Load(Home, "docs"));
    }

    private static string[] Strings(JsonElement list) => [.. list.EnumerateArray().Select(each => each.GetString()!)];

    /// <summary>
    /// SETUPUX1c (D221; the setup UX design §2.3): <c>plan: true</c> reads a choice as a plan of one and writes nothing. It answers the reach, what
    /// lowers the person's part, each place whose workflow changes (a workspace's choice reaching its repositories), and a token.
    /// </summary>
    [Fact]
    public async Task A_plan_of_a_choice_writes_nothing_and_says_the_places_what_it_lowers_and_a_token()
    {
        var (module, _) = await UpAsync();
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "auto", from = "merge-automatic" });
        var before = File.Exists(DriverConfigPath) ? File.ReadAllBytes(DriverConfigPath) : null;

        var plan = await AnswerAsync(module, "WORKFLOW_USE", new { repository = "engine", workflow = "auto", plan = true });

        Assert.True(plan.TryGetProperty("plan", out _), plan.GetRawText());
        Assert.Equal((false, true, true, JsonValueKind.Null), (plan.GetProperty("done").GetBoolean(), plan.GetProperty("plan").GetBoolean(),
            plan.GetProperty("changed").GetBoolean(), plan.GetProperty("refused").ValueKind));
        var place = Assert.Single(plan.GetProperty("places").EnumerateArray());
        Assert.Equal(("engine", "no workflow", "`auto` v1"), (place.GetProperty("repository").GetString(), place.GetProperty("was").GetString(), place.GetProperty("now").GetString()));
        // With no workflow to compare, what the choice lowers cannot be read, and the plan says so.
        Assert.Contains("what no workflow asks of you could not be read here", Assert.Single(Strings(plan.GetProperty("lowers"))));
        Assert.Contains("what no workflow asks of you could not be read here", Assert.Single(Strings(place.GetProperty("lowers"))));
        Assert.False(string.IsNullOrWhiteSpace(plan.GetProperty("expected").GetString()));
        Assert.Contains("now chooses `auto` as its default", plan.GetProperty("message").GetString());
        Assert.Equal(before, File.Exists(DriverConfigPath) ? File.ReadAllBytes(DriverConfigPath) : null);

        var workspace = await AnswerAsync(module, "WORKFLOW_USE", new { workspace = "aurora", workflow = "auto", plan = true });
        Assert.Equal(["engine", "game"], workspace.GetProperty("places").EnumerateArray().Select(each => each.GetProperty("repository").GetString()));
        Assert.Equal(before, File.Exists(DriverConfigPath) ? File.ReadAllBytes(DriverConfigPath) : null);

        var refused = await AnswerAsync(module, "WORKFLOW_USE", new { repository = "engine", workflow = "nope", plan = true });
        Assert.Equal((false, "refused"), (refused.GetProperty("done").GetBoolean(), refused.GetProperty("refused").GetString()));
    }

    /// <summary>
    /// SETUPUX1c: a press carrying the plan's token acts where nothing moved, and is refused <c>moved</c>, writing nothing, where anything did;
    /// a press without one acts as it did, and <c>stale</c> still answers a version that moved.
    /// </summary>
    [Fact]
    public async Task A_press_carrying_the_token_acts_where_nothing_moved_and_is_refused_moved_where_something_did()
    {
        var (module, _) = await UpAsync();
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "auto", from = "merge-automatic" });
        var ask = new { repository = "engine", workflow = "auto" };
        var token = (await AnswerAsync(module, "WORKFLOW_USE", new { ask.repository, ask.workflow, plan = true })).GetProperty("expected").GetString();

        // Something the plan reads moves: a newer version of the workflow, which the choice would bind.
        var appended = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "auto", graph = WorkflowDefaults.Of("merge-after-accept")!.Graph(), @base = 1 });
        Assert.True(appended.GetProperty("written").GetBoolean(), appended.GetRawText());
        var moved = await AnswerAsync(module, "WORKFLOW_USE", new { ask.repository, ask.workflow, expected = token });
        Assert.Equal((false, "moved", false), (moved.GetProperty("done").GetBoolean(), moved.GetProperty("refused").GetString(), moved.GetProperty("changed").GetBoolean()));
        Assert.False(File.Exists(DriverConfigPath) && File.ReadAllText(DriverConfigPath).Contains("\"workflows\"", StringComparison.Ordinal));

        var again = (await AnswerAsync(module, "WORKFLOW_USE", new { ask.repository, ask.workflow, plan = true })).GetProperty("expected").GetString();
        Assert.NotEqual(token, again);
        var stale = await AnswerAsync(module, "WORKFLOW_USE", new { ask.repository, ask.workflow, version = 1, expected = again });
        Assert.Equal("stale", stale.GetProperty("refused").GetString());

        var done = await AnswerAsync(module, "WORKFLOW_USE", new { ask.repository, ask.workflow, version = 2, expected = again });
        Assert.Equal((true, true), (done.GetProperty("done").GetBoolean(), done.GetProperty("changed").GetBoolean()));
        Assert.Equal("auto", DriverConfig.Load(DriverConfigPath).Workflows["engine"].Default);

        var plain = await AnswerAsync(module, "WORKFLOW_USE", new { workspace = "aurora", workflow = "auto" });
        Assert.True(plain.GetProperty("done").GetBoolean());
    }

    /// <summary>SETUPUX1c (the reads' Codex 2): clearing a default says what the kinds the place still maps keep.</summary>
    [Fact]
    public async Task A_plan_that_clears_a_default_says_what_the_kinds_the_place_still_maps_keep()
    {
        var (module, _) = await UpAsync();
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", graph = Parse(Steps) });
        await AnswerAsync(module, "WORKFLOW_KIND", new { workspace = "aurora", kind = "docs", label = "Documentation", paths = new[] { "docs/**" } });
        await AnswerAsync(module, "WORKFLOW_USE", new { repository = "engine", workflow = "docs", kind = "docs" });
        await AnswerAsync(module, "WORKFLOW_USE", new { repository = "engine", workflow = "docs" });
        var before = File.ReadAllBytes(DriverConfigPath);

        var plan = await AnswerAsync(module, "WORKFLOW_USE", new { repository = "engine", plan = true });

        Assert.True(plan.GetProperty("changed").GetBoolean());
        var keeps = plan.GetProperty("keeps");
        var kind = Assert.Single(keeps.GetProperty("kinds").EnumerateArray());
        Assert.Equal(("docs", "docs"), (kind.GetProperty("kind").GetString(), kind.GetProperty("workflow").GetString()));
        Assert.Contains("`engine` still maps kind `docs` to `docs`", keeps.GetProperty("said").GetString());
        Assert.Equal(before, File.ReadAllBytes(DriverConfigPath));

        var clearsAKind = await AnswerAsync(module, "WORKFLOW_USE", new { repository = "engine", workflow = "docs", plan = true });
        Assert.Equal(JsonValueKind.Null, clearsAKind.GetProperty("keeps").ValueKind);
    }

    /// <summary>The three routes are the person's: the census classes them so, no standing press is one of them and no role's act names one.</summary>
    [Theory]
    [InlineData("WORKFLOW_SAVE")]
    [InlineData("WORKFLOW_USE")]
    [InlineData("WORKFLOW_KIND")]
    public void The_workflow_writes_are_the_persons_alone_with_no_role_act(string route)
    {
        Assert.Equal(PipeClass.Person, PipeCensus.Daoris.Of(DriverModule.Name, route));
        Assert.DoesNotContain(route, StewardActs.Table.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.NotNull(PipeTargets.Daoris.Mark(DriverModule.Name, route));
        Assert.Contains(route, DriverModule.Routes);
    }

    /// <summary>A stand-in service holding three repositories, two in one workspace and one in none.</summary>
    private sealed class StandInService : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.AbsolutePath == "/api/registry"
                ? JsonSerializer.Serialize(new object[]
                {
                    new { repository = "game", workspace = "aurora" },
                    new { repository = "engine", workspace = "aurora" },
                    new { repository = "tools" },
                })
                : "[]";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
