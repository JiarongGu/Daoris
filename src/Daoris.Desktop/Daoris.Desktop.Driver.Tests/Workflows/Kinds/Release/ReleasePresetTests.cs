using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The shipped release preset: the release as its observed steps, then the per-run decision whether a config change is
/// needed, *Change live config* where it is, and *Check release* after either. A run through the preset's after-release steps with "not
/// needed" passes the config step on the record naming both holders, and the check goes on. Seen failing first.
/// </summary>
public sealed class ReleasePresetTests : NodeRunFixture
{
    private const string Preset = "pull-request-release";

    private static readonly string[] After = ["config-needed", "config-change", "check-release"];

    /// <summary>The preset's after-release steps and the wires among them, entered from a claimed step as the release's watch reaches them.</summary>
    private static string Tail()
    {
        var graph = WorkflowDefaults.Of(Preset)!.Graph();
        var nodes = new JsonArray(JsonNode.Parse("""{"id":"claimed","kind":"claimed","version":1,"reach":"observed"}"""));
        foreach (var node in graph["nodes"]!.AsArray().OfType<JsonObject>().Where(node => After.Contains((string?)node["id"]))) nodes.Add(node.DeepClone());
        var wires = new JsonArray(JsonNode.Parse("""{"id":"entry","kind":"after","from":"claimed","to":"config-needed"}"""));
        foreach (var wire in graph["wires"]!.AsArray().OfType<JsonObject>().Where(wire => After.Contains((string?)wire["from"]) && After.Contains((string?)wire["to"]))) wires.Add(wire.DeepClone());
        return new JsonObject { ["schemaVersion"] = 2, ["entries"] = new JsonArray("claimed"), ["end"] = (string?)graph["end"], ["nodes"] = nodes, ["wires"] = wires }.ToJsonString();
    }

    private void Read()
    {
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
    }

    private NodeWorded Decide(string outcome, string? why, string? holder = null) =>
        RunNodeWords.Word(Files, RunId, "config-needed", NodeWordActs.Decide, why, () => _now, outcome: outcome, holder: holder);

    [Fact]
    public void The_preset_releases_by_the_release_managers_acts_beside_the_approval_then_decides_the_config_change_and_checks_the_release()
    {
        var shipped = WorkflowDefaults.Of(Preset);
        Assert.NotNull(shipped);
        var graph = NodeGraphs.Read(shipped!.GraphJson, onSave: true).Graph!;
        Assert.Empty(NodeGraphs.Problems(graph));
        // The graph keeps the role's step and the approval it must meet: no fixed step per act, and no target in its options.
        Assert.Equal(["daoris-azure-devops/release-approve"], graph.Nodes.Where(node => node.Kind.Contains("/release-", StringComparison.Ordinal)).Select(node => node.Kind));
        var release = graph.Nodes.Single(node => node.Id == "release");
        Assert.Equal(("task", "release-path"), (release.Kind, StepReceipts.Knowledge(release)));
        Assert.Equal(["release-created", "stage-started", "stage-ended"], StepReceipts.Mapped(release).Values);
        Assert.Equal(["release", "started", "ended"], TaskEvidence.Required(release));
        Assert.DoesNotContain(release.Options.Keys, name => name is "pipeline" or "releaseDefinition" or "stage" or "action");
        Assert.True(TaskDecisions.Is(graph.Nodes.Single(node => node.Id == "config-needed")));
        Assert.Contains(graph.Wires, wire => wire.From?.Node == "release" && wire.To?.Node == "config-needed");
        Assert.Contains(graph.Wires, wire => wire.From?.Node == "release-approve" && wire.To?.Node == "config-needed");
        // The approval is the approver's act: where an agent holds the role, its tool's `approved` receipt settles it.
        Assert.Equal(new Dictionary<string, string> { ["approval"] = "approved" }, StepReceipts.Mapped(graph.Nodes.Single(node => node.Id == "release-approve")));
        Assert.Contains(graph.Wires, wire => wire.Kind == WireKinds.When && wire.From?.Node == "config-needed" && wire.Outcome == "needed" && wire.To?.Node == "config-change");
        Assert.Equal("check-release", graph.End);
        Assert.Equal(
            [("release", StepRoles.ReleaseManager), ("release-approve", StepRoles.Approver), ("config-needed", StepRoles.ReleaseManager),
             ("config-change", StepRoles.ConfigChanger), ("check-release", StepRoles.Operations)],
            graph.Nodes.SkipWhile(node => node.Id != "release").Select(node => (node.Id, StepRoles.Of(node))));
        // The bundled release step stays the older preset's, so saved versions of it keep it.
        Assert.Contains(NodeGraphs.Read(WorkflowDefaults.Of("release")!.GraphJson).Graph!.Nodes, node => node.Kind == NodeKinds.Release);
    }

    [Fact]
    public void A_run_bound_to_the_fixed_release_steps_keeps_them_its_plugin_still_declares_each()
    {
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "examples", "plugins", "daoris-azure-devops", "plugin.json")))!;
        var steps = manifest["steps"]!.AsArray().Select(step => (string?)step!["id"]).ToList();

        // The reader kept for runs bound before the actor form: each fixed step still performs, until no unended run names them.
        Assert.Subset(steps.ToHashSet(), new HashSet<string?> { "release-target", "release-create", "release-start", "release-approve", "release-watch" });
        var commands = manifest["interfaces"]!["commands"]!.AsArray().Where(command => (bool?)command!["tool"] == true).Select(command => (string?)command!["name"]).ToList();
        Assert.Subset(commands.ToHashSet(), new HashSet<string?> { "create-release", "start-stage", "release-status", "approve-release" });
        // The approval is a tool only as the approver role's act, handed where the place casts the approver to an agent.
        var approve = manifest["interfaces"]!["commands"]!.AsArray().Single(command => (string?)command!["name"] == "approve-release")!;
        Assert.Equal(StepRoles.Approver, (string?)approve["role"]);
    }

    [Fact]
    public async Task Not_needed_passes_the_config_change_with_both_holders_named_and_the_release_check_goes_on()
    {
        BoundTo(Tail());
        Read();
        var runtime = Runtime();
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("config-needed").State);

        // Proposed by the release manager's holder from the work's hand-back, then confirmed by the owner at the marked destination.
        Assert.True(Decide("not-needed", "no setting changes in this release", holder: "release manager").Done);
        Read();
        await RunPassAsync(runtime, whole: false);
        Read();
        await RunPassAsync(runtime, whole: false);
        Assert.True(Decide("not-needed", null).Done);
        Read();
        await RunPassAsync(runtime, whole: false);

        var passed = RunStatus("config-change");
        Assert.Equal((NodeStates.Skipped, NodeSource.Decision, "Not needed: proposed by release manager (no setting changes in this release), confirmed by the owner"),
            (passed.State, passed.By, passed.Words));
        Assert.Equal(NodeStates.Active, RunStatus("check-release").State);
    }
}
