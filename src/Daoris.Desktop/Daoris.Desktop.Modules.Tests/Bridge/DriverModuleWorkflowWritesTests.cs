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
    private const string Steps = """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""";

    private const string OtherSteps = """[{"id":"work","kind":"work"},{"id":"look","kind":"look"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""";

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

        var plan = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", from = "pull-request-no-press", plan = true });
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

    [Fact]
    public async Task A_save_onto_a_saved_id_names_the_version_it_was_made_against_and_a_stale_one_is_refused()
    {
        var (module, _) = await UpAsync();
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", steps = Parse(Steps) });

        var unnamed = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", steps = Parse(OtherSteps) });
        Assert.Equal("stale", unnamed.GetProperty("refused").GetString());

        var appended = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", steps = Parse(OtherSteps), @base = 1 });
        Assert.Equal((true, true, true, 2), (appended.GetProperty("done").GetBoolean(), appended.GetProperty("written").GetBoolean(),
            appended.GetProperty("appended").GetBoolean(), appended.GetProperty("version").GetInt32()));
        Assert.Empty(appended.GetProperty("scopes").EnumerateArray());
        Assert.Equal(2, NewestOnDisk("docs"));

        var stale = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", steps = Parse(Steps), @base = 1 });
        Assert.Equal((false, "stale"), (stale.GetProperty("done").GetBoolean(), stale.GetProperty("refused").GetString()));
        Assert.Contains("`docs` is at v2 now, not v1", stale.GetProperty("message").GetString());
        Assert.Equal(2, NewestOnDisk("docs"));

        var same = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", steps = Parse(OtherSteps), @base = 2 });
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
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", steps = Parse(Steps) });

        var asked = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", steps = Parse(OtherSteps), @base = 1 });
        Assert.Equal((false, "reach", true), (asked.GetProperty("done").GetBoolean(), asked.GetProperty("refused").GetString(), asked.GetProperty("appended").GetBoolean()));
        Assert.Equal(["repository:engine:default", "workspace:aurora:kind:docs"], Keys(asked));
        Assert.Equal(1, NewestOnDisk("docs"));

        var partial = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", steps = Parse(OtherSteps), @base = 1, authorize = new[] { "repository:engine:default" } });
        Assert.Equal("reach", partial.GetProperty("refused").GetString());
        Assert.Equal(1, NewestOnDisk("docs"));

        var planned = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", steps = Parse(OtherSteps), @base = 1, plan = true });
        Assert.Equal((true, false), (planned.GetProperty("done").GetBoolean(), planned.GetProperty("written").GetBoolean()));
        Assert.Equal(2, Keys(planned).Count);

        var written = await AnswerAsync(module, "WORKFLOW_SAVE",
            new { id = "docs", steps = Parse(OtherSteps), @base = 1, authorize = new[] { "workspace:aurora:kind:docs", "repository:engine:default" } });
        Assert.Equal((true, true, 2), (written.GetProperty("done").GetBoolean(), written.GetProperty("written").GetBoolean(), written.GetProperty("version").GetInt32()));
        Assert.Equal(2, NewestOnDisk("docs"));
    }

    [Fact]
    public async Task A_workflow_is_chosen_for_a_place_in_one_change_and_cleared()
    {
        var (module, _) = await UpAsync();
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", steps = Parse(Steps) });

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
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", steps = Parse(Steps) });
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", steps = Parse(OtherSteps), @base = 1 });

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
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "docs", steps = Parse(Steps) });

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
    public async Task A_choice_says_where_a_repository_integrates_with_no_gates_and_refuses_one_that_cannot_start()
    {
        File.WriteAllText(DriverConfigPath, """{"landings":{"engine":{"form":"branch","pattern":"w/{quest}"},"game":{"form":"merge"}}}""");
        var (module, _) = await UpAsync();
        await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "line", from = "integrated" });

        var noGates = await AnswerAsync(module, "WORKFLOW_USE", new { repository = "engine", workflow = "line" });
        Assert.True(noGates.GetProperty("done").GetBoolean());
        var row = Assert.Single(noGates.GetProperty("reach").EnumerateArray());
        Assert.Equal(("engine", true, 0), (row.GetProperty("repository").GetString(), row.GetProperty("noGates").GetBoolean(), row.GetProperty("cannots").GetArrayLength()));
        Assert.Contains("`engine` integrates with no gates", row.GetProperty("said").GetString());

        var cannot = await AnswerAsync(module, "WORKFLOW_USE", new { workspace = "aurora", workflow = "line" });
        Assert.Equal((false, "reach"), (cannot.GetProperty("done").GetBoolean(), cannot.GetProperty("refused").GetString()));
        var refused = cannot.GetProperty("reach").EnumerateArray().Single(each => each.GetProperty("cannots").GetArrayLength() > 0);
        Assert.Equal("game", refused.GetProperty("repository").GetString());
        Assert.Contains("cannot start", refused.GetProperty("said").GetString());
        Assert.False(DriverConfig.Load(DriverConfigPath).WorkspaceWorkflows.ContainsKey("aurora"));
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
        // A pull request needs a plugin that lands, which `engine`'s branch rule names none of.
        var pullRequest = WorkflowPresets.GraphOf("pull-request")!.Graph();

        var refused = await AnswerAsync(module, "WORKFLOW_SAVE", new { id = "line", graph = pullRequest, @base = 1, authorize = new[] { "repository:engine:default" } });

        Assert.Equal((false, "reach"), (refused.GetProperty("done").GetBoolean(), refused.GetProperty("refused").GetString()));
        Assert.Equal("engine", Assert.Single(refused.GetProperty("reach").EnumerateArray()).GetProperty("repository").GetString());
        Assert.Equal(1, NewestOnDisk("line"));
    }

    /// <summary>The three routes are the person's: the census classes them so, no standing press is one of them and no role's act names one.</summary>
    [Theory]
    [InlineData("WORKFLOW_SAVE")]
    [InlineData("WORKFLOW_USE")]
    [InlineData("WORKFLOW_KIND")]
    public void The_workflow_writes_are_the_persons_alone_with_no_standing_cover_and_no_role_act(string route)
    {
        Assert.Equal(PipeClass.Person, PipeCensus.Daoris.Of(DriverModule.Name, route));
        Assert.DoesNotContain(route, StandingPresses.Built, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(route, StewardActs.Offered, StringComparer.OrdinalIgnoreCase);
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
