using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The shipped release preset (CONFIGSTEP1): the release as its observed steps, then the per-run decision whether a config change is
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
    public void The_preset_releases_in_observed_steps_then_decides_the_config_change_and_checks_the_release()
    {
        var shipped = WorkflowDefaults.Of(Preset);
        Assert.NotNull(shipped);
        var graph = NodeGraphs.Read(shipped!.GraphJson, onSave: true).Graph!;
        Assert.Empty(NodeGraphs.Problems(graph));
        Assert.Equal(
            ["daoris-azure-devops/release-target", "daoris-azure-devops/release-create", "daoris-azure-devops/release-start",
             "daoris-azure-devops/release-approve", "daoris-azure-devops/release-watch"],
            graph.Nodes.Where(node => node.Kind.Contains("/release-", StringComparison.Ordinal)).Select(node => node.Kind));
        Assert.True(HumanDecisions.Is(graph.Nodes.Single(node => node.Id == "config-needed")));
        Assert.Contains(graph.Wires, wire => wire.From?.Node == "release-watch" && wire.To?.Node == "config-needed");
        Assert.Contains(graph.Wires, wire => wire.Kind == WireKinds.When && wire.From?.Node == "config-needed" && wire.Outcome == "needed" && wire.To?.Node == "config-change");
        Assert.Equal("check-release", graph.End);
        // The preset declares the role of each step after the merge, so a run says whom it waits on without the kind's default.
        Assert.Equal(
            [("release-target", StepRoles.ReleaseManager), ("release-create", StepRoles.ReleaseManager), ("release-start", StepRoles.ReleaseManager),
             ("release-approve", StepRoles.Approver), ("release-watch", StepRoles.Operations), ("config-needed", StepRoles.ReleaseManager),
             ("config-change", StepRoles.ConfigChanger), ("check-release", StepRoles.Operations)],
            graph.Nodes.SkipWhile(node => node.Id != "release-target").Select(node => (node.Id, node.Role)));
        // The bundled release step stays the older preset's, so saved versions of it keep it.
        Assert.Contains(NodeGraphs.Read(WorkflowDefaults.Of("release")!.GraphJson).Graph!.Nodes, node => node.Kind == NodeKinds.Release);
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
