using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// How work moves, over the bridge (WORKFLOW1b; D157 point 7, the workflow design §2.7, §6.1): `WORKFLOW_CURRENT` answers
/// <see cref="WorkflowCurrent.Derive"/> read from this machine's driver file, its plugins and its registry as `daoris driver
/// workflow show` reads them, each step the shared table's cell; a repository the registry does not hold read as in no
/// workspace and said so; a workspace's Current with each of its repositories, following it or not; and the one refusal.
/// </summary>
public sealed class DriverModuleWorkflowTests : DriverModuleBridge
{
    /// <summary>
    /// A repository in a workspace whose branch rule names a plugin that lands and answers its state, a plugin that may hold a
    /// start, its own required review and a standing answer: the answer is the driver's derivation, cell for cell.
    /// </summary>
    [Fact]
    public async Task A_repositorys_Current_is_the_drivers_derivation_read_from_the_file_the_plugins_and_the_registry()
    {
        Plugin("example.pull-request", ["work/land", "work/state"]);
        Plugin("example.hold-by-title", ["quest/consider"]);
        // The file as the person's doors write it, read by the driver's own reader.
        File.WriteAllText(DriverConfigPath, """
            {
              "workspaceLandings": { "aurora": { "form": "branch", "pattern": "work/{quest}-{slug}", "plugin": "example.pull-request" } },
              "reviews": { "engine": { "required": true, "environments": [{ "name": "local", "kind": "local", "procedure": "README.md", "address": "http://localhost:4200" }] } }
            }
            """);
        var (module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "WORKFLOW_CURRENT", new { repository = "engine" });

        var expected = WorkflowCurrent.Derive(
            DriverConfig.Load(DriverConfigPath), "engine", "aurora", WorkflowCurrent.PluginsOf(PluginCatalog.Load(Home, AdapterSet.Built().Names)));
        Assert.Equal("engine", answer.GetProperty("repository").GetString());
        Assert.Equal("aurora", answer.GetProperty("workspace").GetString());
        Assert.True(answer.GetProperty("registered").GetBoolean());
        Assert.Equal(["example.hold-by-title"], answer.GetProperty("startHolds").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal(expected.Steps.Select(WorkflowCurrent.ToJson), answer.GetProperty("steps").EnumerateArray().Select(each => each.GetRawText()));
        Assert.Equal(["work", "look", "landing", "pull-request"], answer.GetProperty("steps").EnumerateArray().Select(each => each.GetProperty("kind").GetString()));
        Assert.Equal(expected.Version, answer.GetProperty("version").GetString());
        // Each limit a step carries, in the driver's words, for a page with none of its own for it.
        var limits = answer.GetProperty("limits");
        Assert.Equal(WorkflowLimits.Says(WorkflowLimits.LookPartial), limits.GetProperty(WorkflowLimits.LookPartial).GetString());
        Assert.Equal(WorkflowLimits.Says(WorkflowLimits.PullRequestAtCleanUp), limits.GetProperty(WorkflowLimits.PullRequestAtCleanUp).GetString());
        Assert.Equal(2, limits.EnumerateObject().Count());
    }

    /// <summary>A repository the registry does not hold is read as in no workspace, the `default` one's rules reaching it, and said so.</summary>
    [Fact]
    public async Task A_repository_the_registry_does_not_hold_is_read_as_in_no_workspace()
    {
        DriverConfig.Empty.WithWorkspaceLanding("default", new LandingRule(LandingForm.Branch, "work/{quest}", false, null, false)).Save(DriverConfigPath);
        var (module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "WORKFLOW_CURRENT", new { repository = "stray" });

        Assert.False(answer.GetProperty("registered").GetBoolean());
        Assert.Equal("default", answer.GetProperty("workspace").GetString());
        var landing = answer.GetProperty("steps")[1];
        Assert.Equal("workspace", landing.GetProperty("source").GetProperty("level").GetString());
        Assert.Equal("work/{quest}", landing.GetProperty("settings").GetProperty("pattern").GetString());
    }

    /// <summary>
    /// A workspace's Current is the file's alone, and beside it each repository the registry holds there: one with a rule of
    /// its own follows its own, one with only a standing answer follows the workspace's, and one in another workspace is not
    /// listed.
    /// </summary>
    [Fact]
    public async Task A_workspaces_Current_lists_its_repositories_each_following_it_or_its_own()
    {
        File.WriteAllText(DriverConfigPath, """
            {
              "workspaceOpinions": { "aurora": { "reviewers": ["codex-acp"], "required": true } },
              "landings": { "engine": { "form": "merge", "tidy": true } },
              "standing": { "game": { "says": "dev writes allowed", "at": "2026-10-09T09:00:00Z" } }
            }
            """);
        var (module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "WORKFLOW_CURRENT", new { workspace = "aurora" });

        var expected = WorkflowCurrent.Derive(DriverConfig.Load(DriverConfigPath), null, "aurora", []);
        Assert.Equal(expected.Version, answer.GetProperty("version").GetString());
        Assert.Equal("aurora", answer.GetProperty("workspace").GetString());
        Assert.False(answer.TryGetProperty("repository", out var none) && none.ValueKind != JsonValueKind.Null);
        Assert.Equal(["work", "opinion", "landing"], answer.GetProperty("steps").EnumerateArray().Select(each => each.GetProperty("kind").GetString()));
        var repositories = answer.GetProperty("repositories").EnumerateArray()
            .Select(each => (each.GetProperty("repository").GetString(), each.GetProperty("own").GetBoolean())).ToList();
        Assert.Equal([("engine", true), ("game", false)], repositories);
    }

    /// <summary>Before the driver's service is up a workspace is still drawn from the file, its repositories unread.</summary>
    [Fact]
    public async Task A_workspace_is_drawn_before_the_driver_comes_up_and_its_repositories_are_not_read()
    {
        var answer = await AnswerAsync(Module(), "WORKFLOW_CURRENT", new { workspace = "aurora" });

        Assert.Equal(["work", "landing"], answer.GetProperty("steps").EnumerateArray().Select(each => each.GetProperty("kind").GetString()));
        Assert.False(answer.TryGetProperty("repositories", out var unread) && unread.ValueKind != JsonValueKind.Null);
    }

    private const string Blob = "3b18e512dba79e4c8300dd08aeb37f8e728b8dad";

    /// <summary>A verification pipeline saved here, made so at a terminal (FABRIC-PIPELINE1a).</summary>
    private void Verification(string id = "web-verify") =>
        WorkflowStore.Add(Home, new WorkflowVersionAdded(id, "Verify the line", "terminal", "2026-10-21T09:00:00Z", null, System.Text.Json.Nodes.JsonNode.Parse(
            """{"schemaVersion":2,"entries":["gates"],"end":"gates","nodes":[{"id":"gates","kind":"check","version":1,"reach":"performed"}],"wires":[]}"""))
        {
            Pipeline = WorkflowPipelines.Verification,
        }, kept: []);

    /// <summary>
    /// FABRIC-PIPELINE1a (the pipeline design §1.3): a repository's answer carries its start node, drawn first on its Workflow tab: the
    /// default and each kind with the pipeline it names and what chose it, the landed route with its pipeline, the gates accepted and
    /// where they stand against the line's tip, and the verification pipelines Setup's row offers.
    /// </summary>
    [Fact]
    public async Task A_repositorys_answer_carries_its_start_node_and_its_landed_route()
    {
        Verification();
        File.WriteAllText(DriverConfigPath, $$"""
            {
              "workflows": { "engine": { "landed": { "workflow": "web-verify", "gates": "{{Blob}}" } } },
              "workspaceWorkflows": { "aurora": { "kinds": { "docs": { "label": "Documentation", "paths": ["docs/**"] } } } }
            }
            """);
        var (module, _) = await UpAsync();

        var start = (await AnswerAsync(module, "WORKFLOW_CURRENT", new { repository = "engine" })).GetProperty("start");

        var routes = start.GetProperty("routes").EnumerateArray().ToList();
        Assert.Equal([(null, "current", "current"), ("docs", "current", "current")],
            routes.Select(each => (each.GetProperty("kind").GetString(), each.GetProperty("workflow").GetString(), each.GetProperty("level").GetString())));
        Assert.Equal(["docs/**"], routes[1].GetProperty("paths").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal("Documentation", routes[1].GetProperty("label").GetString());
        var landed = start.GetProperty("landed");
        Assert.Equal(("web-verify", WorkflowLevels.RepositoryLanded, Blob, "Verify the line", LandedGates.Unread),
            (landed.GetProperty("workflow").GetString(), landed.GetProperty("level").GetString(), landed.GetProperty("gates").GetString(),
             landed.GetProperty("name").GetString(), landed.GetProperty("state").GetString()));
        Assert.Equal(JsonValueKind.Null, landed.GetProperty("tip").ValueKind);
        Assert.Equal("`engine` has no checkout on this machine to read.", landed.GetProperty("problem").GetString());
        var offered = Assert.Single(start.GetProperty("verifications").EnumerateArray());
        Assert.Equal(("web-verify", "Verify the line"), (offered.GetProperty("id").GetString(), offered.GetProperty("name").GetString()));
    }

    /// <summary>
    /// FABRIC-PIPELINE1a: Setup's route row and the tab's <i>Accept the gates</i> press <c>WORKFLOW_LANDED</c>, the person's door: it sets
    /// a repository's or a workspace's landed route, refuses a development pipeline there in the twins' words, and answers the start node
    /// as it now stands with what the change lowers, said.
    /// </summary>
    [Fact]
    public async Task The_landed_route_is_set_by_the_persons_press_and_a_development_pipeline_is_refused()
    {
        Verification();
        WorkflowStore.Add(Home, new WorkflowVersionAdded("docs-to-pr", "Docs", "terminal", "2026-10-21T09:00:00Z", System.Text.Json.Nodes.JsonNode.Parse(
            """[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""")), kept: []);
        var (module, _) = await UpAsync();

        var set = await AnswerAsync(module, "WORKFLOW_LANDED", new { repository = "engine", workflow = "web-verify" });
        Assert.Equal("web-verify", set.GetProperty("start").GetProperty("landed").GetProperty("workflow").GetString());
        Assert.Empty(set.GetProperty("lowered").EnumerateArray());
        Assert.Equal(new WorkflowLanded("web-verify", null), DriverConfig.Load(DriverConfigPath).Workflows["engine"].Landed);

        var refused = await RefusalAsync(module, "WORKFLOW_LANDED", new { repository = "engine", workflow = "docs-to-pr" });
        Assert.Contains(WorkflowSelection.NotVerification("docs-to-pr"), refused);

        var shared = await AnswerAsync(module, "WORKFLOW_LANDED", new { workspace = "aurora", workflow = "web-verify" });
        Assert.Equal(WorkflowLevels.WorkspaceLanded, shared.GetProperty("start").GetProperty("landed").GetProperty("level").GetString());

        var cleared = await AnswerAsync(module, "WORKFLOW_LANDED", new { repository = "engine", workflow = (string?)null });
        Assert.Equal(WorkflowLevels.WorkspaceLanded, cleared.GetProperty("start").GetProperty("landed").GetProperty("level").GetString());
        Assert.Empty(cleared.GetProperty("lowered").EnumerateArray());

        var none = await AnswerAsync(module, "WORKFLOW_LANDED", new { workspace = "aurora", workflow = "current" });
        Assert.Equal(["it leaves the work that lands unverified"], none.GetProperty("lowered").EnumerateArray().Select(each => each.GetString()));
        Assert.Contains("a landed route is set for a `repository` or a `workspace`", await RefusalAsync(module, "WORKFLOW_LANDED", new { workflow = "web-verify" }));
    }

    [Fact]
    public async Task A_refusal_names_what_to_name_and_a_repository_waits_for_the_driver()
    {
        var neither = await RefusalAsync(Module(), "WORKFLOW_CURRENT", new { });
        var both = await RefusalAsync(Module(), "WORKFLOW_CURRENT", new { repository = "engine", workspace = "aurora" });
        var early = await RefusalAsync(Module(), "WORKFLOW_CURRENT", new { repository = "engine" });

        Assert.Contains("a workflow is drawn for a `repository` or a `workspace` — name one of them.", neither);
        Assert.Contains("name one of them", both);
        Assert.Contains("still coming up", early);
    }

    /// <summary>A plugin switched on, speaking on the points named.</summary>
    private void Plugin(string id, string[] points)
    {
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins,id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), JsonSerializer.Serialize(new
        {
            id,
            hooks = new { command = new[] { "node", "${plugin}/hook.mjs" }, points },
        }));
        PluginState.Enable(Home, id);
    }

    /// <summary>The module over a loop come up on a stand-in service holding the registry below.</summary>
    private async Task<(DriverModule Module, DriverLoop Loop)> UpAsync()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandInService())));
        return (module, loop);
    }

    /// <summary>A stand-in service holding three repositories, two in one workspace and one in none.</summary>
    private sealed class StandInService : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.AbsolutePath == "/api/registry"
                ? """[{"repository":"game","workspace":"aurora"},{"repository":"engine","workspace":"aurora"},{"repository":"tools"}]"""
                : "[]";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
