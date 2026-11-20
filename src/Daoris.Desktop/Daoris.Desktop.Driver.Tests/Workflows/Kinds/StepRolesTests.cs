using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every step names its role: a declaration's <c>role</c> is in its equality and its
/// version's digest, a version naming none reads each kind's default and keeps its digest, the save refuses a holder named where a role goes
/// (a person and an agent alike), and a going back past the first build that saved one is refused.
/// </summary>
public sealed class StepRolesTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-steproles-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>Each kind this build knows and the role it gives a step that names none: a kind added is placed here before it builds.</summary>
    private static readonly Dictionary<string, string> Defaults = new(StringComparer.Ordinal)
    {
        [NodeKinds.Claimed] = StepRoles.Design,
        [NodeKinds.Resolve] = StepRoles.Design,
        [NodeKinds.Opinion] = StepRoles.Opinion,
        [NodeKinds.Look] = StepRoles.Tester,
        [NodeKinds.Review] = StepRoles.Acceptor,
        [NodeKinds.GoAhead] = StepRoles.Approver,
        [NodeKinds.Task] = StepRoles.Owner,
        [NodeKinds.Isolate] = StepRoles.Operations,
        [NodeKinds.Push] = StepRoles.Operations,
        [NodeKinds.Line] = StepRoles.Operations,
        [NodeKinds.WaitUnit] = StepRoles.Operations,
        [NodeKinds.Parts] = StepRoles.Operations,
        [NodeKinds.QuestClose] = StepRoles.Operations,
        [NodeKinds.All] = StepRoles.Operations,
        [NodeKinds.Stop] = StepRoles.Operations,
        [NodeKinds.Check] = StepRoles.Operations,
        [NodeKinds.Integrate] = StepRoles.Operations,
        [NodeKinds.Apply] = StepRoles.Operations,
        [NodeKinds.Return] = StepRoles.Operations,
        [NodeKinds.WorkRequests] = StepRoles.Operations,
        [NodeKinds.Request] = StepRoles.Operations,
        [NodeKinds.Land] = StepRoles.Operations,
        [NodeKinds.QuestHolds] = StepRoles.AskReader,
        [NodeKinds.ContentHolds] = StepRoles.Operations,
        [NodeKinds.Monitor] = StepRoles.Operations,
        [NodeKinds.Tidy] = StepRoles.Operations,
        [NodeKinds.Release] = StepRoles.Operations,
        [NodeKinds.Rebase] = StepRoles.Operations,
        [NodeKinds.Merge] = StepRoles.Operations,
    };

    [Fact]
    [Trait(Category.Name, Category.Census)]
    public void Every_kind_gives_a_default_role_the_window_words()
    {
        var kinds = NodeKit.Built.Units.Select(unit => unit.Row.Kind).ToList();
        Assert.Equal(Defaults.Keys.Order(StringComparer.Ordinal), kinds.Order(StringComparer.Ordinal));
        foreach (var unit in NodeKit.Built.Units)
        {
            Assert.Equal(Defaults[unit.Row.Kind], unit.Role);
            Assert.Contains(unit.Role, StepRoles.All);
        }

        Assert.Equal(StepRoles.Operations, ((INodeKind)new PluginStep { Kind = "acme.deploy/release" }).Role);
        Assert.Equal(StepRoles.All, WorkflowPalette.Build([]).Roles);
        Assert.All(WorkflowPalette.Build([]).Kinds, kind => Assert.Equal(Defaults[kind.Kind], kind.Role));
    }

    [Fact]
    public void A_step_naming_none_reads_its_kinds_role_and_keeps_its_digest()
    {
        var none = Graph(null);
        var named = Graph("tester");
        Assert.Null(none.Nodes.Single(node => node.Id == "look").Role);
        Assert.Equal(StepRoles.Tester, StepRoles.Of(none.Nodes.Single(node => node.Id == "look")));

        // The default is read, never written: naming the default is another version, and naming none keeps the digest a build before the field took.
        Assert.NotEqual(NodeGraphs.Digest(none), NodeGraphs.Digest(named));
        Assert.Equal(NodeGraphs.Digest(none), NodeGraphs.Digest(Graph(null)));
        Assert.Equal(NodeGraphs.Digest(none), NodeGraphs.Digest(NodeGraphs.Read(Json(null).Replace("\"role\": null, ", "")).Graph!));
        Assert.NotEqual(NodeGraphs.Digest(named), NodeGraphs.Digest(Graph("acceptor")));
    }

    [Fact]
    public void A_role_is_in_a_declarations_equality_and_hash()
    {
        var bare = new NodeDeclaration("look", NodeKinds.Look, 1, NodeReach.Observed);
        var named = bare with { Role = StepRoles.Tester };
        Assert.NotEqual(bare, named);
        Assert.Equal(named, bare with { Role = StepRoles.Tester });
        Assert.Equal(named.GetHashCode(), (bare with { Role = StepRoles.Tester }).GetHashCode());
    }

    [Theory]
    [InlineData("you")]
    [InlineData("person")]
    [InlineData("codex")]
    [InlineData("claude-code")]
    public void The_save_refuses_a_holder_named_where_a_role_goes_alike(string holder)
    {
        var (_, problems) = NodeGraphs.Read(Json(holder), onSave: true);
        var refused = Assert.Single(problems, problem => problem.Node == "look");
        Assert.Equal(StepRoles.HolderRefused, refused.Code);
        Assert.Contains("name the role that takes it", refused.Words);
    }

    [Fact]
    public void The_save_takes_a_census_role_and_refuses_one_neither_daoris_nor_the_catalogue_names()
    {
        Assert.Empty(WorkflowOperations.RoleProblems(_home, Graph(StepRoles.ReleaseManager)));
        var unknown = Assert.Single(WorkflowOperations.RoleProblems(_home, Graph("deployer")));
        Assert.Equal(StepRoles.Unknown, unknown.Code);
        var (_, shape) = NodeGraphs.Read(Json("Release Manager"), onSave: true);
        Assert.Equal(StepRoles.Unknown, Assert.Single(shape, problem => problem.Node == "look").Code);
    }

    [Fact]
    public void An_older_agent_steps_role_option_is_read_as_its_role()
    {
        var older = new NodeDeclaration("work", NodeKinds.Claimed, 1, NodeReach.Observed)
        {
            Options = new Dictionary<string, string> { [NodeOptionFields.Role] = "\"exact\"" },
        };
        Assert.Equal("exact", StepRoles.Of(older));
        Assert.Equal("\"exact\"", StepRoles.Written(older));
        Assert.Equal("\"design\"", StepRoles.Written(older with { Role = StepRoles.Design }));
        Assert.Equal(StepRoles.Design, StepRoles.Of(older with { Options = NodeDeclaration.NoPorts }));
    }

    [Fact]
    public void A_going_back_past_the_first_build_that_saved_a_role_is_refused()
    {
        var file = JsonNode.Parse($$"""{ "versions": [ { "version": 1, "graph": {{Json("tester")}} } ] }""");
        WorkflowRolesSinceStore.Keep(_home, JsonNode.Parse($$"""{ "versions": [ { "version": 1, "graph": {{Json(null)}} } ] }"""), "1.40.0");
        Assert.Null(WorkflowRolesSinceStore.Read(_home));

        WorkflowRolesSinceStore.Keep(_home, file, "1.40.0");
        WorkflowRolesSinceStore.Keep(_home, file, "1.41.0");
        Assert.Equal("1.40.0", WorkflowRolesSinceStore.Read(_home));
        Assert.Equal(WorkflowRolesSince.Code, WorkflowRolesSinceStore.Refused(_home, "1.39.2")?.Code);
        Assert.Null(WorkflowRolesSinceStore.Refused(_home, "1.40.0"));
        Assert.Equal(WorkflowRolesSince.Code, InstallRollback.Refused(_home, "1.39.2", "1.41.0")?.Code);

        // The rule alone, on the kept build as a value: none kept refuses nothing, and a build already kept is never closed again.
        Assert.Null(WorkflowRolesSince.Refused(null, "1.39.2"));
        Assert.Equal(WorkflowRolesSince.Code, WorkflowRolesSince.Refused("1.40.0", "1.39.2")?.Code);
        Assert.Equal("1.40.0", WorkflowRolesSince.Closes(null, file, "1.40.0"));
        Assert.Null(WorkflowRolesSince.Closes("1.40.0", file, "1.41.0"));
    }

    [Fact]
    public void A_floor_is_written_atomically_and_read_beside_its_writer()
    {
        var file = JsonNode.Parse($$"""{ "versions": [ { "version": 1, "graph": {{Json("tester")}} } ] }""");
        WorkflowRolesSinceStore.Keep(_home, file, "1.40.0");

        Assert.Equal(["roles-since.txt"], Directory.GetFiles(WorkflowStore.FolderOf(_home)).Select(Path.GetFileName));
        using var held = AtomicFile.OpenRead(WorkflowRolesSinceStore.PathOf(_home));
        Assert.Equal("1.40.0", WorkflowRolesSinceStore.Read(_home));
    }

    /// <summary>A save writing a <c>task</c> step closes going back below its build, apart from the roles' floor, which a build between the two lies under.</summary>
    [Fact]
    public void A_going_back_past_the_first_build_that_saved_a_task_is_refused_whatever_the_roles_floor()
    {
        WorkflowRolesSinceStore.Keep(_home, JsonNode.Parse($$"""{ "versions": [ { "version": 1, "graph": {{Json("tester")}} } ] }"""), "1.40.0");
        var task = JsonNode.Parse(Json(null))!;
        task["nodes"]![1]!["kind"] = NodeKinds.Task;
        var file = new JsonObject { ["versions"] = new JsonArray(new JsonObject { ["version"] = 2, ["graph"] = task }) };

        Assert.Equal("1.45.0", WorkflowRolesSinceStore.Closes(_home, file, "1.45.0"));
        WorkflowRolesSinceStore.Keep(_home, file, "1.45.0");
        WorkflowRolesSinceStore.Keep(_home, file, "1.46.0");

        Assert.Equal("1.40.0", WorkflowRolesSinceStore.Read(_home));
        Assert.Equal("1.45.0", WorkflowRolesSinceStore.Read(_home, WorkflowRolesSince.Tasks));
        Assert.Contains("`task`", WorkflowRolesSinceStore.Refused(_home, "1.42.0")?.Sentence, StringComparison.Ordinal);
        Assert.Null(WorkflowRolesSinceStore.Refused(_home, "1.45.0"));
        Assert.Null(WorkflowRolesSinceStore.Closes(_home, file, "1.46.0"));
    }

    /// <summary>
    /// A version whose steps name only their kinds' own roles is written naming none, so a build before the field reads it unchanged and no
    /// going back is closed: a preset's second opinion among them. Another role is kept as named, and its save says first that it closes
    /// going back below this build, where none is closed yet.
    /// </summary>
    [Fact]
    public void A_save_naming_only_the_kinds_roles_writes_none_and_closes_no_going_back()
    {
        var shipped = WorkflowPresets.GraphTable.First(preset => preset.Graph()["nodes"]!.AsArray().Any(node => (string?)node!["kind"] == NodeKinds.Land));
        var preset = WorkflowPresets.WithGraphGates(shipped.Graph(), opinion: true, look: true);
        var kinds = JsonNode.Parse(Json(StepRoles.Tester))!;
        kinds["end"] = "ask";
        kinds["nodes"]!.AsArray().Add(new JsonObject { ["id"] = "ask", ["kind"] = NodeKinds.Task, ["version"] = 1, ["reach"] = "pressed", ["role"] = StepRoles.Owner });
        kinds["wires"]!.AsArray().Add(new JsonObject { ["id"] = "w2", ["kind"] = "after", ["from"] = "look", ["to"] = "ask" });

        foreach (var graph in new[] { preset, kinds })
        {
            var file = WorkflowNamed.PlanAdd(null, new WorkflowVersionAdded("wf", "Workflow", "terminal", "2026-11-17T09:00:00Z", null, graph), []).Result!;
            Assert.DoesNotContain(file["versions"]![0]!["graph"]!["nodes"]!.AsArray(), node => node!.AsObject().ContainsKey("role"));
            Assert.Null(WorkflowRolesSince.Closes(null, file, "1.40.0"));
            WorkflowRolesSinceStore.Keep(_home, file, "1.40.0");
            Assert.Null(WorkflowRolesSinceStore.Read(_home));
        }

        // A shipped default names no kind's own role, so a copy saved of it keeps the shipped digest.
        Assert.All(WorkflowDefaults.All, each => Assert.Equal(each.Graph().ToJsonString(), StepRoles.Unwritten(each.Graph())!.ToJsonString()));

        var named = WorkflowNamed.PlanAdd(null, new WorkflowVersionAdded("wf", "Workflow", "terminal", "2026-11-17T09:00:00Z", null, JsonNode.Parse(Json(StepRoles.Operations))), []).Result!;
        Assert.Equal(StepRoles.Operations, (string?)named["versions"]![0]!["graph"]!["nodes"]![1]!["role"]);
        Assert.Equal("1.40.0", WorkflowRolesSinceStore.Closes(_home, named, "1.40.0"));
        WorkflowRolesSinceStore.Keep(_home, named, "1.40.0");
        Assert.Null(WorkflowRolesSinceStore.Closes(_home, named, "1.41.0"));
    }

    private static NodeGraph Graph(string? role) => NodeGraphs.Read(Json(role)).Graph!;

    private static string Json(string? role) => $$"""
        { "schemaVersion": 2, "entries": ["work"], "end": "look",
          "nodes": [
            { "id": "work", "kind": "claimed", "version": 1, "reach": "observed" },
            { "id": "look", "kind": "look", "version": 1, "reach": "observed", "role": {{(role is null ? "null" : $"\"{role}\"")}}, "options": {} } ],
          "wires": [{ "id": "w1", "kind": "after", "from": "work", "to": "look" }] }
        """;
}
