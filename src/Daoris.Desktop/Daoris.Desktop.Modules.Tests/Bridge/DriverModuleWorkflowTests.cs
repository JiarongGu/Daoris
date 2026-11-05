using System.Text.Json;
using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// How work moves, over the bridge (WORKFLOW1b; D157 point 7, the workflow design §2.7, §6.1): `WORKFLOW_CURRENT` answers a
/// repository's or a workspace's routes from this machine's driver file, its plugins and its registry as `daoris driver workflow show`
/// reads them; a repository the registry does not hold read as in no workspace and said so; a workspace's with each of its repositories,
/// following it or not; and the one refusal.
/// </summary>
public sealed class DriverModuleWorkflowTests : DriverModuleBridge
{
    /// <summary>A repository in a workspace, a plugin that may hold a start: its answer names the start holds and draws no steps of its own.</summary>
    [Fact]
    public async Task A_repositorys_answer_is_read_from_the_file_the_plugins_and_the_registry()
    {
        Plugin("example.pull-request", ["work/land", "work/state"]);
        Plugin("example.hold-by-title", ["quest/consider"]);
        var (module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "WORKFLOW_CURRENT", new { repository = "engine" });

        Assert.Equal("engine", answer.GetProperty("repository").GetString());
        Assert.Equal("aurora", answer.GetProperty("workspace").GetString());
        Assert.True(answer.GetProperty("registered").GetBoolean());
        Assert.Equal(["example.hold-by-title"], answer.GetProperty("startHolds").EnumerateArray().Select(each => each.GetString()));
        Assert.Empty(answer.GetProperty("steps").EnumerateArray());
    }

    /// <summary>A repository the registry does not hold is read as in no workspace, and said so.</summary>
    [Fact]
    public async Task A_repository_the_registry_does_not_hold_is_read_as_in_no_workspace()
    {
        var (module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "WORKFLOW_CURRENT", new { repository = "stray" });

        Assert.False(answer.GetProperty("registered").GetBoolean());
        Assert.Equal("default", answer.GetProperty("workspace").GetString());
    }

    /// <summary>Beside a workspace's answer, each repository the registry holds there, whether a choice of its own makes it follow something else.</summary>
    [Fact]
    public async Task A_workspaces_answer_lists_its_repositories_each_following_it_or_its_own()
    {
        File.WriteAllText(DriverConfigPath, """{ "workflows": { "engine": { "default": "release" } } }""");
        var (module, _) = await UpAsync();

        var answer = await AnswerAsync(module, "WORKFLOW_CURRENT", new { workspace = "aurora" });

        Assert.Equal("aurora", answer.GetProperty("workspace").GetString());
        Assert.False(answer.TryGetProperty("repository", out var none) && none.ValueKind != JsonValueKind.Null);
        var repositories = answer.GetProperty("repositories").EnumerateArray()
            .Select(each => (each.GetProperty("repository").GetString(), each.GetProperty("own").GetBoolean())).ToList();
        Assert.Equal([("engine", true), ("game", false)], repositories);
    }

    /// <summary>
    /// SETUPUX1b: with a `route` the answer also carries the graph that route follows, and the read writes nothing; without one the answer is
    /// as it was, and the effective graph is not in it.
    /// </summary>
    [Fact]
    public async Task A_route_asks_for_its_effective_graph_and_a_caller_without_one_is_answered_as_before()
    {
        WorkflowStore.Add(Home, new WorkflowVersionAdded("release", "Release for engine", "terminal", "2026-11-01T09:00:00Z", null,
            WorkflowPresets.GraphTable.First(preset => preset.Id == SetupPresets.Merge).Graph()), kept: []);
        File.WriteAllText(DriverConfigPath, """{"workflows":{"engine":{"default":"release"}}}""");
        var (module, _) = await UpAsync();
        var before = File.ReadAllBytes(DriverConfigPath);

        var plain = await AnswerAsync(module, "WORKFLOW_CURRENT", new { repository = "engine" });
        Assert.False(plain.TryGetProperty("effective", out _));

        var answer = await AnswerAsync(module, "WORKFLOW_CURRENT", new { repository = "engine", route = "default" });

        var found = WorkflowStore.Load(Home, "release")!;
        var effective = answer.GetProperty("effective");
        Assert.Equal("default", effective.GetProperty("route").GetString());
        Assert.Equal("release", effective.GetProperty("workflow").GetString());
        Assert.Equal("Release for engine", effective.GetProperty("name").GetString());
        Assert.Equal(1, effective.GetProperty("version").GetInt32());
        Assert.Equal(found.Read.Versions[^1].Digest, effective.GetProperty("digest").GetString());
        Assert.Equal("repository", effective.GetProperty("source").GetProperty("level").GetString());
        var nodes = effective.GetProperty("graph").GetProperty("nodes").EnumerateArray().Select(each => each.GetProperty("id").GetString()).ToList();
        Assert.Equal(nodes, effective.GetProperty("cells").EnumerateArray().Select(each => each.GetProperty("node").GetString()));
        Assert.Equal("repository", effective.GetProperty("cells")[0].GetProperty("step").GetProperty("source").GetProperty("level").GetString());
        // The old fields are as they were, beside it.
        Assert.Equal(plain.GetProperty("version").GetString(), answer.GetProperty("version").GetString());
        Assert.Equal(plain.GetProperty("steps").GetRawText(), answer.GetProperty("steps").GetRawText());
        Assert.Equal(before, File.ReadAllBytes(DriverConfigPath));
    }

    /// <summary>A route where no workflow is chosen draws nothing and says that its work is held.</summary>
    [Fact]
    public async Task A_route_with_no_workflow_chosen_draws_nothing_and_says_why()
    {
        var (module, _) = await UpAsync();

        var repository = (await AnswerAsync(module, "WORKFLOW_CURRENT", new { repository = "engine", route = "default" })).GetProperty("effective");

        Assert.Equal("current", repository.GetProperty("workflow").GetString());
        Assert.Equal(JsonValueKind.Null, repository.GetProperty("graph").ValueKind);
        Assert.Equal(WorkflowProcess.NoWorkflow("engine"), repository.GetProperty("problem").GetString());
    }

    /// <summary>The landed route under Current is no verification and draws no graph; a route that is none, or a kind the workspace does not declare, is refused.</summary>
    [Fact]
    public async Task The_landed_route_under_Current_draws_nothing_and_an_unknown_route_is_refused()
    {
        var (module, _) = await UpAsync();

        var landed = (await AnswerAsync(module, "WORKFLOW_CURRENT", new { repository = "engine", route = "landed" })).GetProperty("effective");
        Assert.Equal("current", landed.GetProperty("workflow").GetString());
        Assert.False(landed.GetProperty("verifies").GetBoolean());
        Assert.Equal(JsonValueKind.Null, landed.GetProperty("graph").ValueKind);
        Assert.Empty(landed.GetProperty("cells").EnumerateArray());

        Assert.Contains("kind:<name>", await RefusalAsync(module, "WORKFLOW_CURRENT", new { repository = "engine", route = "nonsense" }));
        Assert.Contains("docs", await RefusalAsync(module, "WORKFLOW_CURRENT", new { repository = "engine", route = "kind:docs" }));
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
        WorkflowStore.Add(Home, new WorkflowVersionAdded("docs-to-pr", "Docs", "terminal", "2026-10-21T09:00:00Z", null,
            System.Text.Json.Nodes.JsonNode.Parse(WorkflowDefaults.Of("merge-after-accept")!.GraphJson)), kept: []);
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

    /// <summary>
    /// AGENTSPLACE1a0: the landed route's press may name the blob the person read, as <c>INTEGRATION_GATES</c> does. A blob that is no longer
    /// the tip's is refused <c>moved</c> with nothing written, one that is accepts it, and a press naming none (an older caller) accepts the
    /// tip as it stands.
    /// </summary>
    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task The_landed_route_refuses_a_blob_the_tip_no_longer_holds_and_an_older_caller_naming_none_is_unchanged()
    {
        Verification();
        var root = await CheckoutAsync();
        var (module, _) = await UpAsync(root);
        var blob = (await GitAsync(root, "rev-parse", "main:daoris.gates.json")).Trim();

        var accepted = await AnswerAsync(module, "WORKFLOW_LANDED", new { repository = "engine", workflow = "web-verify", gates = blob });
        Assert.Equal(blob, DriverConfig.Load(DriverConfigPath).Workflows["engine"].Landed!.Gates);
        Assert.Equal("web-verify", accepted.GetProperty("start").GetProperty("landed").GetProperty("workflow").GetString());

        await CommitAsync(root, """{ "integration": { "scratch": ["out/**", "logs/**"] } }""");
        var moved = await RefusalAsync(module, "WORKFLOW_LANDED", new { repository = "engine", workflow = "web-verify", gates = blob });
        Assert.Contains("moved", moved);
        Assert.Contains("read it again", moved);
        Assert.Equal(blob, DriverConfig.Load(DriverConfigPath).Workflows["engine"].Landed!.Gates);

        var newer = (await GitAsync(root, "rev-parse", "main:daoris.gates.json")).Trim();
        await AnswerAsync(module, "WORKFLOW_LANDED", new { repository = "engine", workflow = "web-verify" });
        Assert.Equal(newer, DriverConfig.Load(DriverConfigPath).Workflows["engine"].Landed!.Gates);
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

    /// <summary>
    /// WORKFLOWEDIT1a: the step editor's three reads answer from the home alone, with no service up, and declare no change: the saved
    /// workflows with the places that choose them and the presets, one version as saved with its line, and the palette with a plugin's step.
    /// </summary>
    [Fact]
    public async Task The_step_editors_reads_answer_from_the_home_and_publish_nothing()
    {
        Verification("web-verify");
        File.WriteAllText(DriverConfigPath, """{ "workflows": { "engine": { "default": "web-verify" } } }""");
        var folder = DaorisHome.Entry(Home, HomeEntry.Plugins, "acme.deploy");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName), """
            { "id": "acme.deploy", "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/perform", "work/reconcile"] },
              "steps": [ { "id": "deploy", "version": 1, "reach": "performed" } ] }
            """);
        PluginState.Enable(Home, "acme.deploy");
        var module = Module();

        foreach (var route in new[] { "WORKFLOW_LIST", "WORKFLOW_SHOW", "WORKFLOW_PALETTE" })
        {
            Assert.Null(DriverModule.Declared(new IpcRequest { Id = "t", Module = DriverModule.Name, Type = route }));
        }

        var list = await AnswerAsync(module, "WORKFLOW_LIST", new { });
        var saved = Assert.Single(list.GetProperty("workflows").EnumerateArray());
        Assert.Equal(("web-verify", 2, 1), (saved.GetProperty("id").GetString(), saved.GetProperty("schema").GetInt32(), saved.GetProperty("versions").GetArrayLength()));
        var chosen = Assert.Single(saved.GetProperty("chosen").EnumerateArray());
        Assert.Equal(("repository", "engine", "default"), (chosen.GetProperty("level").GetString(), chosen.GetProperty("name").GetString(), chosen.GetProperty("role").GetString()));
        var presets = list.GetProperty("presets").EnumerateArray().ToList();
        Assert.Equal(WorkflowPresets.GraphTable.Count, presets.Count);
        Assert.All(presets, each => Assert.True(each.GetProperty("preset").GetBoolean()));

        var shown = await AnswerAsync(module, "WORKFLOW_SHOW", new { id = "web-verify" });
        Assert.Equal((1, true, 2), (shown.GetProperty("version").GetInt32(), shown.GetProperty("newest").GetBoolean(), shown.GetProperty("schema").GetInt32()));
        Assert.Equal("gates", shown.GetProperty("body").GetProperty("entries")[0].GetString());
        Assert.Contains("keeps no v9", (await RefusalAsync(module, "WORKFLOW_SHOW", new { id = "web-verify", version = 9 })));

        var palette = await AnswerAsync(module, "WORKFLOW_PALETTE", new { });
        Assert.Equal(NodeKinds.BuiltIn.Count, palette.GetProperty("kinds").GetArrayLength());
        Assert.Equal(["acme.deploy/deploy"], palette.GetProperty("plugins")[0].GetProperty("steps").EnumerateArray().Select(each => each.GetProperty("kind").GetString()));
    }

    /// <summary>
    /// SETUPUX1a: each default the list carries is the shipped data's, with its purpose, its order, whether the set-up offers it and its
    /// delivery's land, names and purpose; SETUPUX1c: and the list announces that a choice can be read as a plan.
    /// </summary>
    [Fact]
    public async Task The_list_carries_each_defaults_purpose_order_set_up_and_delivery_and_announces_the_plan_of_one()
    {
        var module = Module();

        var list = await AnswerAsync(module, "WORKFLOW_LIST", new { });

        Assert.Equal(["plan"], list.GetProperty("use").EnumerateArray().Select(each => each.GetString()));
        var presets = list.GetProperty("presets").EnumerateArray().Where(each => each.TryGetProperty("delivery", out var delivery) && delivery.ValueKind == JsonValueKind.Object).ToList();
        Assert.Equal(WorkflowDefaults.All.Select(each => each.Id), presets.Select(each => each.GetProperty("id").GetString()));
        foreach (var (shipped, preset) in WorkflowDefaults.All.Zip(presets))
        {
            Assert.Equal((shipped.Name, shipped.For), (preset.GetProperty("name").GetString(), preset.GetProperty("for").GetString()));
            Assert.False(preset.TryGetProperty("names", out _), "the list carries no per-language field");
            Assert.Equal((shipped.Order, shipped.SetUp), (preset.GetProperty("order").GetInt32(), preset.GetProperty("setUp").GetBoolean()));
            var delivery = preset.GetProperty("delivery");
            Assert.Equal((shipped.Delivery.Method, shipped.Delivery.Accept), (delivery.GetProperty("method").GetString(), delivery.GetProperty("accept").GetString()));
            Assert.Equal(shipped.Delivery.Plugin, delivery.TryGetProperty("plugin", out var plugin) ? plugin.GetString() : null);
            Assert.Equal(shipped.Delivery.Request, delivery.TryGetProperty("request", out var request) ? request.GetString() : null);
            Assert.Equal(
                (shipped.DeliveryWords.Id, shipped.DeliveryWords.Name, shipped.DeliveryWords.For),
                (delivery.GetProperty("id").GetString(), delivery.GetProperty("name").GetString(), delivery.GetProperty("for").GetString()));
            Assert.False(delivery.TryGetProperty("names", out _), "the delivery carries no per-language field");
        }

        Assert.Equal(WorkflowDefaults.All.Count, presets.Count);
    }

    /// <summary>The module over a loop come up on a stand-in service holding the registry below, <c>engine</c>'s checkout at <paramref name="root"/> where named.</summary>
    private async Task<(DriverModule Module, DriverLoop Loop)> UpAsync(string? root = null)
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(new SessionChanges(new StandInService(root)))));
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

    /// <summary>
    /// <c>WORKFLOW_VERIFY</c>, the page's half of <c>daoris gate verify</c> (FABRIC-PIPELINE1d part 5): a repository with no checkout here is
    /// refused <c>no-checkout</c> in a sentence and binds nothing, and the press names a repository and no more than one of a branch or a commit.
    /// </summary>
    [Fact]
    public async Task A_verification_of_a_repository_with_no_checkout_is_refused_and_binds_nothing()
    {
        var (module, _) = await UpAsync();

        var refused = await AnswerAsync(module, "WORKFLOW_VERIFY", new { repository = "engine", branch = "main" });
        Assert.Equal((false, "no-checkout"), (refused.GetProperty("done").GetBoolean(), refused.GetProperty("refused").GetString()));
        Assert.Contains("`engine` has no checkout on this machine", refused.GetProperty("message").GetString());
        Assert.Empty(VerifyRuns.All(Home));

        Assert.Contains("asked for a `repository`", await RefusalAsync(module, "WORKFLOW_VERIFY", new { branch = "main" }));
        Assert.Contains("a branch or a commit, not both", await RefusalAsync(module, "WORKFLOW_VERIFY", new { repository = "engine", branch = "main", commit = "abc1234" }));
    }

    /// <summary>A checkout whose landed route names no verification pipeline: the library's refusal, in its sentence, and nothing bound.</summary>
    [Fact]
    [Trait(Category.Name, Category.Process)]
    public async Task A_verification_where_the_landed_route_names_no_pipeline_says_so_and_binds_nothing()
    {
        var root = await CheckoutAsync();
        var (module, _) = await UpAsync(root);

        var refused = await AnswerAsync(module, "WORKFLOW_VERIFY", new { repository = "engine", branch = "main" });

        Assert.Equal((false, "refused"), (refused.GetProperty("done").GetBoolean(), refused.GetProperty("refused").GetString()));
        Assert.Contains("names no verification pipeline", refused.GetProperty("message").GetString());
        Assert.Empty(VerifyRuns.All(Home));
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
