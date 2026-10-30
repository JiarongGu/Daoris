using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
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

    /// <summary>INTEGRATE1r (F43): a rule carrying <c>integrate</c> draws the step after the landing, declared, with its limit said in the driver's words.</summary>
    [Fact]
    public async Task A_rule_carrying_integrate_is_answered_with_the_integrate_step_and_its_limit()
    {
        File.WriteAllText(DriverConfigPath, """{"landings":{"engine":{"form":"branch","pattern":"work/{session}","integrate":{"request":"owner"}}}}""");
        var (module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "WORKFLOW_CURRENT", new { repository = "engine" });

        var steps = answer.GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(["work", "landing", "integrate"], steps.Select(each => each.GetProperty("kind").GetString()));
        Assert.Equal("declared", steps[2].GetProperty("runtime").GetString());
        Assert.Equal("owner", steps[2].GetProperty("settings").GetProperty("request").GetString());
        Assert.Equal(WorkflowLimits.NotIntegratedYet, steps[2].GetProperty("limit").GetString());
        Assert.Equal(WorkflowLimits.Says(WorkflowLimits.NotIntegratedYet), answer.GetProperty("limits").GetProperty(WorkflowLimits.NotIntegratedYet).GetString());
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

    /// <summary>The module over a loop come up on a stand-in service holding the registry below, <c>engine</c>'s checkout at <paramref name="root"/> where named.</summary>
    private async Task<(DriverModule Module, DriverLoop Loop)> UpAsync(string? root = null)
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new StandInService(root))));
        return (module, loop);
    }

    // ---- INTEGRATE1d: the integration's own acceptance, the person's door (D215 points 2 and 10, the integration design §4.1, §4.3) ----

    private const string Declares = """{ "integration": { "scratch": ["out/**"] } }""";

    /// <summary>
    /// The person's press accepts the declaration at the line's tip by its blob, in <c>integrationGates</c> apart from the landed route's,
    /// and the start node says where it stands: unaccepted, with the text to read, before; accepted after; changed, with the new text, once
    /// the line edits it. A blob named that is no longer the tip's is refused <c>moved</c> and nothing is written, since the person accepts
    /// what they read; and the acceptance can be withdrawn.
    /// </summary>
    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task The_integration_acceptance_takes_the_tips_blob_and_reads_changed_once_the_line_edits_its_declaration()
    {
        var root = await CheckoutAsync();
        var (module, _) = await UpAsync(root);
        var blob = (await GitAsync(root, "rev-parse", "main:daoris.gates.json")).Trim();

        var before = await IntegrationAsync(module);
        Assert.Equal((IntegrationAcceptance.Unaccepted, blob), (before.GetProperty("state").GetString(), before.GetProperty("tip").GetProperty("gates").GetString()));
        Assert.Equal("main", before.GetProperty("tip").GetProperty("line").GetString());
        Assert.Equal(Declares, before.GetProperty("declaration").GetString());
        Assert.Equal(JsonValueKind.Null, before.GetProperty("gates").ValueKind);

        var accepted = await AnswerAsync(module, "INTEGRATION_GATES", new { repository = "engine", gates = blob });
        Assert.True(accepted.GetProperty("done").GetBoolean(), accepted.GetRawText());
        var now = accepted.GetProperty("start").GetProperty("integration");
        Assert.Equal((IntegrationAcceptance.Accepted, blob), (now.GetProperty("state").GetString(), now.GetProperty("gates").GetString()));
        Assert.Equal(JsonValueKind.Null, now.GetProperty("declaration").ValueKind);
        Assert.Equal(blob, DriverConfig.Load(DriverConfigPath).IntegrationGates["engine"]);
        Assert.False(DriverConfig.Load(DriverConfigPath).Workflows.ContainsKey("engine"), "the landed route's acceptance is apart");

        await CommitAsync(root, """{ "integration": { "scratch": ["out/**", "logs/**"] } }""");
        var changed = await IntegrationAsync(module);
        Assert.Equal((IntegrationAcceptance.Changed, blob), (changed.GetProperty("state").GetString(), changed.GetProperty("gates").GetString()));
        Assert.Contains("logs/**", changed.GetProperty("declaration").GetString());

        var stale = await AnswerAsync(module, "INTEGRATION_GATES", new { repository = "engine", gates = blob });
        Assert.Equal((false, "moved"), (stale.GetProperty("done").GetBoolean(), stale.GetProperty("refused").GetString()));
        Assert.Contains("read it again", stale.GetProperty("message").GetString());
        Assert.Equal(blob, DriverConfig.Load(DriverConfigPath).IntegrationGates["engine"]);

        var withdrawn = await AnswerAsync(module, "INTEGRATION_GATES", new { repository = "engine", clear = true });
        Assert.True(withdrawn.GetProperty("done").GetBoolean());
        Assert.Equal(IntegrationAcceptance.Unaccepted, withdrawn.GetProperty("start").GetProperty("integration").GetProperty("state").GetString());
        Assert.Empty(DriverConfig.Load(DriverConfigPath).IntegrationGates);
    }

    /// <summary>
    /// A repository with no checkout here has its tip unread, so nothing is accepted and the answer says why; a workspace's start node
    /// holds no integration, which is accepted repository by repository; and the press names a repository.
    /// </summary>
    [Fact]
    public async Task An_integration_acceptance_with_no_tip_to_read_is_refused_unread_and_a_workspace_holds_none()
    {
        var (module, _) = await UpAsync();

        var unread = await IntegrationAsync(module);
        Assert.Equal(IntegrationAcceptance.Unread, unread.GetProperty("state").GetString());
        Assert.Equal("`engine` has no checkout on this machine to read.", unread.GetProperty("problem").GetString());

        var refused = await AnswerAsync(module, "INTEGRATION_GATES", new { repository = "engine" });
        Assert.Equal((false, "unread"), (refused.GetProperty("done").GetBoolean(), refused.GetProperty("refused").GetString()));
        Assert.Empty(DriverConfig.Load(DriverConfigPath).IntegrationGates);

        var workspace = (await AnswerAsync(module, "WORKFLOW_CURRENT", new { workspace = "aurora" })).GetProperty("start");
        Assert.False(workspace.TryGetProperty("integration", out var none) && none.ValueKind != JsonValueKind.Null);
        Assert.Contains("an integration's declaration is accepted for a `repository`", await RefusalAsync(module, "INTEGRATION_GATES", new { workspace = "aurora" }));
        Assert.Contains("name `gates` or `clear`", await RefusalAsync(module, "INTEGRATION_GATES", new { repository = "engine", gates = Blob, clear = true }));
    }

    private async Task<JsonElement> IntegrationAsync(DriverModule module) =>
        (await AnswerAsync(module, "WORKFLOW_CURRENT", new { repository = "engine" })).GetProperty("start").GetProperty("integration");

    /// <summary><c>engine</c>'s checkout under the home, on <c>main</c>, its declaration committed.</summary>
    private async Task<string> CheckoutAsync()
    {
        var root = Path.Combine(Home, "checkouts", "engine");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await CommitAsync(root, Declares);
        return root;
    }

    private static async Task CommitAsync(string root, string declaration)
    {
        await File.WriteAllTextAsync(Path.Combine(root, "daoris.gates.json"), declaration);
        await GitAsync(root, "add", "daoris.gates.json");
        await GitAsync(root, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.test", "commit", "--quiet", "-m", "the declaration");
    }

    private static async Task<string> GitAsync(string root, params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git")
        { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(TestGit.Past(info))!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(output, error);
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await error);
        return await output;
    }

    /// <summary>A stand-in service holding three repositories, two in one workspace and one in none; <c>engine</c>'s checkout at <paramref name="root"/> where named.</summary>
    private sealed class StandInService(string? root = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.AbsolutePath == "/api/registry"
                ? JsonSerializer.Serialize(new object[]
                {
                    new { repository = "game", workspace = "aurora" },
                    root is null ? (object)new { repository = "engine", workspace = "aurora" } : new { repository = "engine", workspace = "aurora", root },
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
