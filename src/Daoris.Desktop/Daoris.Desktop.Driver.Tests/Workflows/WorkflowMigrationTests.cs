using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The workflow step of the release's migration: one fixture home holding each old shape, converted once, every place following a
/// saved graph after it, and the old keys gone from the file.
/// </summary>
public sealed class WorkflowMigrationTests : IDisposable
{
    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "migrate-" + Guid.NewGuid().ToString("N")[..8]);

    private const string QaCycle = """{ "nodes": [ { "id": "qa", "kind": "human", "version": 1, "reach": "pressed" } ], "wires": [ { "id": "a", "kind": "after", "from": "handed", "to": "qa" } ], "end": "qa" }""";

    private static readonly RepoView[] Registry =
    [
        new("engine", true, "/f/engine", "aurora"), new("web", true, "/f/web", "aurora"), new("docs", true, "/f/docs", "aurora"),
        new("auto", true, "/f/auto", "default"), new("atlas", true, "/f/atlas", "default"), new("solo", true, "/f/solo", "default"),
        new("legacy", true, "/f/legacy", "default"), new("plain", true, "/f/plain", "default"),
    ];

    private string ConfigPath => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static Dictionary<string, T> Map<T>(params (string Key, T Value)[] entries) =>
        new(entries.ToDictionary(each => each.Key, each => each.Value), StringComparer.OrdinalIgnoreCase);

    /// <summary>Every old shape, in one driver.json as an older build wrote it and one folder of workflows.</summary>
    private void Seed()
    {
        Directory.CreateDirectory(_home);
        (DriverConfig.Empty with
        {
            Access = Map(("atlas", new AccessSetting { Pauses = new Dictionary<string, string> { [AccessKey.Landing] = AccessKey.Rule } })),
            SessionChecks = Map(("solo", "light")),
            Workflows = Map(
                ("legacy", new WorkflowChoice("current", [])), ("web", new WorkflowChoice("old-handed", [])), ("docs", new WorkflowChoice("old-handed", [])),
                ("plain", new WorkflowChoice("land-you", []))),
        }).Save(ConfigPath);
        var file = JsonNode.Parse(File.ReadAllText(ConfigPath))!.AsObject();
        file["landings"] = JsonNode.Parse($$"""
            {
              "engine": { "form": "branch", "pattern": "feat/{ticket}-{slug}", "key": "[A-Z]+-\\d+", "plugin": "example-pr", "tidy": true, "squash": true, "cycle": {{QaCycle}} },
              "auto": { "form": "merge", "autoAccept": true },
              "docs": { "form": "merge", "autoAccept": true },
              "atlas": { "form": "branch", "pattern": "work/{quest}-{slug}" }
            }
            """);
        file["workspaceLandings"] = JsonNode.Parse($$"""
            { "aurora": { "form": "branch", "pattern": "{{LandNodes.DefaultPattern}}", "integrate": { "batch": 2, "gates": "declared", "conflicts": "session" } } }
            """);
        file["workCycles"] = JsonNode.Parse("""{ "cyc": { "quest": { "nodes": [], "wires": [] } } }""");
        File.WriteAllText(ConfigPath, file.ToJsonString());

        var store = WorkflowPorts.Store(_home);
        const string at = "2026-10-10T09:00:00Z";
        StoredSteps.AddGraph(store, "old-handed", "Old handed", at, JsonNode.Parse("""
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"handed","version":1,"reach":"observed"}],
             "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
            """)!);
        StoredSteps.AddGraph(store, "land-you", "Land you", at, LandWithAccept("you"));
        StoredSteps.AddGraph(store, "land-auto", "Land auto", at, LandWithAccept("automatic"));
    }

    private static JsonNode LandWithAccept(string accept) => JsonNode.Parse($$$"""
        {"schemaVersion":2,"entries":["claimed"],"end":"landing",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge","accept":"{{{accept}}}"}}],
         "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"}]}
        """)!;

    private static JsonArray Nodes(IWorkflowStore store, string id) => store.Load(id)!.File!["versions"]!.AsArray()[^1]!["graph"]!["nodes"]!.AsArray();

    private static JsonObject LandOf(IWorkflowStore store, string id) =>
        Nodes(store, id).Single(node => (string?)node!["kind"] == NodeKinds.Land)!["options"]!.AsObject();

    [Fact]
    public void The_migration_converts_each_shape_and_every_place_follows_a_saved_graph()
    {
        Seed();

        var migrated = WorkflowMigration.Convert(_home, DriverConfig.Load(ConfigPath), Registry);
        migrated.Config.Save(ConfigPath);

        var store = WorkflowPorts.Store(_home);
        // The saved workflows: the next version of each id, a handed open to two rules in one version each.
        var second = migrated.Saved.Single(each => each.StartsWith("old-handed-", StringComparison.Ordinal));
        Assert.Equal(["branch", "merge"], new[] { "old-handed", second }.Select(id => LandOf(store, id)["method"]!.ToString()).Order());
        // `land` with accept: `you` and `automatic` both get the review, and the option is gone.
        foreach (var id in new[] { "land-you", "land-auto" })
        {
            Assert.Contains(Nodes(store, id), node => (string?)node!["kind"] == NodeKinds.Review);
            Assert.All(Nodes(store, id), node => Assert.Null(node!["options"]?["accept"]));
        }

        var config = DriverConfig.Load(ConfigPath);
        // Places: the home default is written, the automatic merge matches its shipped copy, the branch with everything a workflow of its own.
        Assert.Equal("merge-after-accept", config.DefaultWorkflow);
        Assert.Equal("merge-automatic", WorkflowSelection.Resolve(config, "auto", "default", null).Workflow);
        Assert.Equal("engine-workflow", WorkflowSelection.Resolve(config, "engine", "aurora", null).Workflow);
        var land = LandOf(store, "engine-workflow");
        // A rule that named a plugin lands a branch, then the push node and the plugin's own pull-request step.
        Assert.Equal(("branch", "feat/{ticket}-{slug}", "[A-Z]+-\\d+", true),
            (land["method"]!.ToString(), land["pattern"]!.ToString(), land["key"]!.ToString(), (bool)land["squash"]!));
        Assert.Contains(Nodes(store, "engine-workflow"), node => (string?)node!["kind"] == NodeKinds.Push);
        Assert.Contains(Nodes(store, "engine-workflow"), node => (string?)node!["kind"] == "example-pr/pull-request");
        Assert.Contains(Nodes(store, "engine-workflow"), node => (string?)node!["id"] == "qa");
        // The workspace's rule becomes the workspace's choice, and its integrate a line policy.
        Assert.Equal(2, config.WorkspaceLinePolicies["aurora"].Batch);
        Assert.Null(config.WorkspaceLinePolicies["aurora"].Request);
        // sessionChecks becomes claimed.checks; landing: rule over a branch converts with no review, as it landed.
        Assert.Equal("light", Nodes(store, "solo-workflow").Single(node => (string?)node!["kind"] == NodeKinds.Claimed)!["options"]!["checks"]!.ToString());
        Assert.False(config.SessionChecks.ContainsKey("solo"));
        Assert.DoesNotContain(Nodes(store, "atlas-workflow"), node => (string?)node!["kind"] == NodeKinds.Review);
        Assert.False(config.Access.ContainsKey("atlas"));
        // An explicit current with no rule follows the home default again.
        Assert.Equal(WorkflowLevels.Home, WorkflowSelection.Resolve(config, "legacy", "default", null).Level);
        // The old keys are gone from the file.
        var written = JsonNode.Parse(File.ReadAllText(ConfigPath))!.AsObject();
        Assert.All(new[] { "landings", "workspaceLandings", "workCycles" }, key => Assert.False(written.ContainsKey(key), key));

        // Again: nothing is left to convert.
        Assert.True(WorkflowMigration.Convert(_home, DriverConfig.Load(ConfigPath), Registry).Empty);
    }

    [Fact]
    public void A_handed_lift_reproduces_the_old_reading_over_the_rule()
    {
        // A merge-form `handed` under a `merge --auto-accept` rule: the press read `you` (a merge is always pressed).
        var graph = (JsonObject)JsonNode.Parse("""
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"handed","version":1,"reach":"observed"}],
             "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
            """)!;
        var merge = new MigratedPlace("docs", false, "default", new MigratedRule(LandingForm.Merge, null, null, true, null, false, null, null), true, false, false, false, null, false, false, "old");

        var lifted = MigratedGraphs.LiftHanded(graph, HandedResolution.Of(merge), out var refusal, out _)!;

        Assert.Null(refusal);
        Assert.Equal("merge", lifted["nodes"]!.AsArray().Single(node => (string?)node!["kind"] == NodeKinds.Land)!["options"]!["method"]!.ToString());
        Assert.Contains(lifted["nodes"]!.AsArray(), node => (string?)node!["kind"] == NodeKinds.Review);
        // The same under a branch rule that accepts automatically is a branch with no review.
        var branch = merge with { Rule = new MigratedRule(LandingForm.Branch, null, null, true, null, false, null, null) };
        var auto = MigratedGraphs.LiftHanded(graph, HandedResolution.Of(branch), out _, out _)!;
        Assert.DoesNotContain(auto["nodes"]!.AsArray(), node => (string?)node!["kind"] == NodeKinds.Review);
        Assert.Equal("branch", auto["nodes"]!.AsArray().Single(node => (string?)node!["kind"] == NodeKinds.Land)!["options"]!["method"]!.ToString());
        // A merge written with no press never ran, so it is left, not lowered.
        var written = (JsonObject)graph.DeepClone();
        written["nodes"]!.AsArray()[1]!["options"] = new JsonObject { ["form"] = "merge", ["accept"] = "automatic" };
        Assert.Null(MigratedGraphs.LiftHanded(written, HandedResolution.Default, out var kept, out _));
        Assert.Contains("never ran", kept);
    }

    [Fact]
    public void A_drawn_graph_matches_a_shipped_copy_by_its_shape_not_its_ids()
    {
        var plain = new MigratedPlace("plain", false, "default", null, false, false, false, false, null, false, false, null);
        var drawn = MigratedGraphs.OfRules(plain);
        var shipped = WorkflowDefaults.Of("merge-after-accept")!.Graph();

        Assert.Equal(MigratedGraphs.Shape(shipped), MigratedGraphs.Shape(drawn));
        Assert.NotEqual(NodeGraphs.Digest(NodeGraphs.Read(shipped.ToJsonString()).Graph!), NodeGraphs.Digest(NodeGraphs.Read(drawn.ToJsonString()).Graph!));
        var gated = plain with { Opinion = true, Look = true };
        Assert.Equal(MigratedGraphs.Shape(WorkflowPresets.WithGraphGates(shipped, true, true)), MigratedGraphs.Shape(MigratedGraphs.OfRules(gated)));
    }
}
