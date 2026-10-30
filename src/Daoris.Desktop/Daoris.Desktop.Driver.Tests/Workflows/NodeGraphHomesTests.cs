using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-GRAPH1 (the fabric events design §E7, §C2, §D7): Current's graph derived from the rules, the landing rule's <c>cycle</c> as a
/// fragment after the landing, <c>workCycles</c> for the kinds that do not land, D164's whole-rule inheritance from workspace to
/// repository, and the doors that set them, read from one table, <c>fixtures/graph-homes.json</c>, row for row (the CLI's twin,
/// <c>cycles.ts</c>, left with the driver family's TypeScript in PUSH1f2b).
/// </summary>
public sealed class NodeGraphHomesTests
{
    private const string QaCycle = """{ "nodes": [ { "id": "qa", "kind": "human", "version": 1, "reach": "pressed" } ], "wires": [ { "id": "a", "kind": "after", "from": "line", "to": "qa" } ], "end": "qa" }""";

    private static readonly LandingRule Pulled = new(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: "acme.pr");

    /// <summary>A graph's shape in words a tuple compares by value: its nodes, its wires, its entries, its end and its cleanup.</summary>
    private static (string Nodes, string Wires, string Entries, string End, GraphCleanup Cleanup) Shape(NodeGraph graph) =>
        (string.Join(" ", graph.Nodes.Select(node => $"{node.Id}:{node.Kind}")),
         string.Join(" ", graph.Wires.Select(wire => $"{wire.From!.Node}-{wire.Kind}->{wire.To.Node}")),
         string.Join(" ", graph.Entries), graph.End, graph.Cleanup);

    /// <summary>
    /// An install's existing <c>driver.json</c>, with no <c>cycle</c> and no <c>workCycles</c>, reads exactly as today: the landing as its
    /// rule says, ended when the line holds it, cleaned up at <i>held</i>, no cycle for a quest or an ask, and the file written back
    /// with neither key.
    /// </summary>
    [Fact]
    public void An_existing_config_with_neither_key_derives_todays_cycle()
    {
        var json = DriverConfig.Empty
            .WithLanding("engine", LandingRule.Merge)
            .WithWorkspaceLanding("shop", Pulled)
            .ToJson();
        var config = DriverConfig.Parse(json);

        var merged = NodeGraphHomes.Current(config, "engine", "shop");
        Assert.Equal((LandingSource.Repository, (string?)null, true), (merged.Source, merged.Cycle, merged.Readable));
        Assert.Equal(("handed:handed line:line", "handed-after->line", "handed", "line", GraphCleanup.Held), Shape(merged.Graph!));

        var pulled = NodeGraphHomes.Current(config, "orders", "shop");
        Assert.Equal(LandingSource.Workspace, pulled.Source);
        Assert.Equal(
            ("handed:handed pushed:pushed pull-request:pull-request line:line", "handed-after->pushed pushed-after->pull-request pull-request-after->line",
             "handed", "line", GraphCleanup.Held),
            Shape(pulled.Graph!));

        var none = NodeGraphHomes.Current(config, "elsewhere", "another");
        Assert.Equal((LandingSource.Default, true), (none.Source, none.Readable));
        Assert.Equal(NodeGraphs.Digest(merged.Graph!), NodeGraphs.Digest(none.Graph!));
        Assert.Equal(NodeGraphs.Digest(NodeGraphHomes.Landing(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"))), NodeGraphs.Digest(none.Graph!));

        foreach (var kind in NodeGraphHomes.WorkCycleKinds)
        {
            var work = NodeGraphHomes.Work(config, "engine", "shop", kind);
            Assert.Equal((kind, LandingSource.Default, (string?)null, (NodeGraph?)null, true), (work.Kind, work.Source, work.Cycle, work.Graph, work.Readable));
        }

        Assert.DoesNotContain("cycle", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("workCycles", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(json, config.ToJson());
    }

    /// <summary>
    /// D164's inheritance: a repository's whole rule replaces its workspace's, its cycle with it, so a rule of its own with no cycle is
    /// the default and never the workspace's cycle; a repository with no rule takes its workspace's, cycle and all.
    /// </summary>
    [Fact]
    public void A_cycle_rides_its_rule_from_workspace_to_repository()
    {
        var config = DriverConfig.Empty
            .WithWorkspaceLanding("shop", Pulled)
            .WithWorkspaceLandingCycle("shop", """{ "end": "pull-request" }""")
            .WithLanding("engine", LandingRule.Merge)
            .WithLanding("shell", LandingRule.Merge)
            .WithLandingCycle("shell", QaCycle);

        var inherited = NodeGraphHomes.Current(config, "orders", "shop");
        Assert.Equal((LandingSource.Workspace, "pull-request", true), (inherited.Source, inherited.Graph!.End, inherited.Readable));
        Assert.Equal("""{"end":"pull-request"}""", inherited.Cycle);

        var own = NodeGraphHomes.Current(config, "engine", "shop");
        Assert.Equal((LandingSource.Repository, (string?)null, "line"), (own.Source, own.Cycle, own.Graph!.End));

        var cycled = NodeGraphHomes.Current(config, "shell", "shop");
        Assert.Equal((LandingSource.Repository, "qa"), (cycled.Source, cycled.Graph!.End));
        Assert.Equal(
            ("handed:handed line:line qa:human", "handed-after->line line-after->qa", "handed", "qa", GraphCleanup.Held),
            Shape(cycled.Graph!));
    }

    /// <summary>
    /// A fragment after the landing: its end may be a landing's node the rule reaches, an entry of its own may start beside the landing
    /// (a <c>wait.unit</c>), and an end the rule cannot reach, or a cleanup waiting on an end at the line, is refused.
    /// </summary>
    [Fact]
    public void A_fragment_ends_where_its_rule_reaches()
    {
        Assert.Equal("pull-request", NodeGraphHomes.OfLanding(Pulled with { Cycle = """{ "end": "pull-request" }""" }, LandingSource.Repository).Graph!.End);

        var waiting = NodeGraphHomes.OfLanding(Pulled with
        {
            Cycle = """
                { "entries": ["dependency"], "end": "deploy", "cleanup": { "until": "end", "days": 14 },
                  "nodes": [ { "id": "dependency", "kind": "wait.unit", "version": 1, "reach": "waited" },
                             { "id": "deploy", "kind": "acme.deploy/deploy", "version": 1, "reach": "performed" } ],
                  "wires": [ { "id": "b", "kind": "wait", "from": { "unit": "u2", "revision": "r2", "end": true }, "to": "dependency" },
                             { "id": "c", "kind": "after", "from": "dependency", "to": "deploy" },
                             { "id": "d", "kind": "after", "from": "line", "to": "deploy" } ] }
                """,
        }, LandingSource.Repository);
        Assert.Empty(waiting.Problems);
        Assert.Equal(new[] { "handed", "dependency" }, waiting.Graph!.Entries);
        Assert.Equal(new GraphCleanup(GraphCleanupUntil.End, 14), waiting.Graph.Cleanup);

        var unreached = NodeGraphHomes.OfLanding(LandingRule.Merge with { Cycle = """{ "end": "pull-request" }""" }, LandingSource.Repository);
        Assert.Equal(new[] { (GraphRefusal.End, (string?)"pull-request") }, unreached.Problems.Select(problem => (problem.Code, problem.Node)));

        var early = NodeGraphHomes.OfLanding(LandingRule.Merge with { Cycle = """{ "cleanup": { "until": "end" } }""" }, LandingSource.Repository);
        Assert.Equal(new[] { (GraphRefusal.Cleanup, (string?)"line") }, early.Problems.Select(problem => (problem.Code, problem.Node)));
    }

    /// <summary>
    /// The kinds that do not land (§D7): <c>workCycles</c> by repository and kind, else <c>workspaceWorkCycles</c> for that kind, else none;
    /// a branch's is the landing rule's alone, so no kind has two homes.
    /// </summary>
    private static readonly LandingRule Integrating = new(LandingForm.Branch, "feature/{quest}-{slug}") { Integrate = new LandingIntegrate() };

    /// <summary>
    /// INTEGRATE1n part n1 (the integration design §17.4, <i>Current draws it</i>): where the effective rule carries <c>integrate</c> Current's
    /// branch graph is <c>handed</c>, <c>integrate</c>, <c>line</c>, the step waited and entered by no wire but the landing's; a rule set nowhere,
    /// or one that carries none, draws as it did, and a rule whose form cannot carry one (a merge, a plugin, a squash: the key's own refusals)
    /// draws today's graph whatever its file says.
    /// </summary>
    [Fact]
    public void A_rule_carrying_integrate_draws_the_step_between_the_landing_and_the_line()
    {
        var config = DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"))
            .WithLandingIntegrate("engine", new LandingIntegrate(Request: LandingIntegrate.Owner));

        var home = NodeGraphHomes.Current(config, "engine", null);
        Assert.True(home.Readable);
        Assert.Equal(
            ("handed:handed integrate:integrate line:line", "handed-after->integrate integrate-after->line", "handed", "line", GraphCleanup.Held),
            Shape(home.Graph!));
        Assert.Equal(NodeReach.Waited, home.Graph!.Nodes.Single(node => node.Kind == NodeKinds.Integrate).Reach);
        Assert.Empty(NodeGraphs.Problems(home.Graph));

        // Without it, today's graph, byte for byte.
        var plain = NodeGraphHomes.Landing(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        Assert.Equal(("handed:handed line:line", "handed-after->line"), (Shape(plain).Nodes, Shape(plain).Wires));
        Assert.NotEqual(NodeGraphs.Digest(plain), NodeGraphs.Digest(home.Graph));
        Assert.Equal(NodeGraphs.Digest(plain), NodeGraphs.Digest(NodeGraphHomes.Landing(Integrating with { Integrate = null })));
        Assert.Equal(NodeGraphs.Digest(NodeGraphHomes.Landing(Integrating)), NodeGraphs.Digest(home.Graph));

        // The forms the key refuses carry none, so a hand-written file naming one draws today's.
        foreach (var rule in new[]
        {
            LandingRule.Merge with { Integrate = new LandingIntegrate() },
            Pulled with { Integrate = new LandingIntegrate() },
            Integrating with { Squash = true },
        })
        {
            Assert.Equal(NodeGraphs.Digest(NodeGraphHomes.Landing(rule with { Integrate = null })), NodeGraphs.Digest(NodeGraphHomes.Landing(rule)));
        }
    }

    /// <summary>
    /// INTEGRATE1n (§17.4): the rule's <c>cycle</c> comes after the landing's nodes as today: it may wire from <c>integrate</c> and never into
    /// it, and may not declare one, as it may not declare <c>pushed</c>.
    /// </summary>
    [Fact]
    public void A_cycle_wires_from_the_integrate_and_never_into_it()
    {
        var after = NodeGraphHomes.OfLanding(Integrating with { Cycle = """{ "nodes": [ { "id": "qa", "kind": "human", "version": 1, "reach": "pressed" } ], "wires": [ { "id": "a", "kind": "after", "from": "integrate", "to": "qa" } ], "end": "qa" }""" }, LandingSource.Repository);
        Assert.True(after.Readable);
        Assert.Equal("qa", after.Graph!.End);
        Assert.Contains("integrate-after->line", Shape(after.Graph).Wires);
        Assert.Contains("integrate-after->qa", Shape(after.Graph).Wires);

        var into = NodeGraphHomes.OfLanding(Integrating with { Cycle = """{ "nodes": [ { "id": "qa", "kind": "human", "version": 1, "reach": "pressed" } ], "wires": [ { "id": "a", "kind": "after", "from": "qa", "to": "integrate" } ], "entries": ["qa"] }""" }, LandingSource.Repository);
        Assert.Equal((false, GraphRefusal.IntoLanding), (into.Readable, Assert.Single(into.Problems).Code));

        var declared = NodeGraphHomes.OfLanding(Pulled with { Cycle = """{ "nodes": [ { "id": "i", "kind": "integrate", "version": 1, "reach": "waited" } ], "wires": [ { "id": "a", "kind": "after", "from": "handed", "to": "i" } ] }""" }, LandingSource.Repository);
        Assert.Contains(GraphRefusal.LandingKind, declared.Problems.Select(problem => problem.Code));
    }

    /// <summary>
    /// INTEGRATE1n (§17.6 point 7, <c>compatible-builds.md</c>): an older build reads a rule carrying <c>integrate</c> as the branch rule it is and
    /// draws <c>handed</c> then <c>line</c>, holding no <c>integrate</c> node, and that graph arms nothing: this build's Current arms nothing
    /// either, since nothing starts the step yet (rows n2 and n3), and a rule whose cycle names a plugin's node arms both builds alike.
    /// </summary>
    [Fact]
    public void An_older_build_draws_no_integrate_for_the_same_rule_and_arms_nothing()
    {
        var config = DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"))
            .WithLandingIntegrate("engine", new LandingIntegrate());
        var rule = config.Landings["engine"];
        Assert.NotNull(rule.Integrate);

        // The older build never reads the key: the rule without it is what it draws, and its graph has no integrate node.
        var older = NodeGraphHomes.OfLanding(rule with { Integrate = null }, LandingSource.Repository);
        Assert.DoesNotContain(older.Graph!.Nodes, node => node.Kind == NodeKinds.Integrate);
        Assert.Equal("handed:handed line:line", Shape(older.Graph).Nodes);

        // The file the newer build wrote keeps the key through a round trip, and reads back as the same rule.
        Assert.Equal(rule.Integrate, DriverConfig.Parse(config.ToJson()).Landings["engine"].Integrate);

        Assert.False(NodeRuntime.Armed(config));
        Assert.False(NodeRuntime.Armed(DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"))));
    }

    [Fact]
    public void Work_cycles_are_inherited_kind_by_kind()
    {
        var quest = """{ "nodes": [ { "id": "qa", "kind": "human", "version": 1, "reach": "pressed" } ], "entries": ["qa"], "end": "qa" }""";
        var ask = """{ "nodes": [ { "id": "go", "kind": "go-ahead", "version": 1, "reach": "pressed" } ], "entries": ["go"], "end": "go" }""";
        var config = DriverConfig.Empty
            .WithWorkCycle("engine", WorkUnitKind.Quest, quest)
            .WithWorkspaceWorkCycle("shop", WorkUnitKind.Quest, ask)
            .WithWorkspaceWorkCycle("shop", WorkUnitKind.Ask, ask);

        var own = NodeGraphHomes.Work(config, "engine", "shop", WorkUnitKind.Quest);
        Assert.Equal((LandingSource.Repository, "qa", true), (own.Source, own.Graph!.End, own.Readable));
        var shared = NodeGraphHomes.Work(config, "engine", "shop", WorkUnitKind.Ask);
        Assert.Equal((LandingSource.Workspace, "go"), (shared.Source, shared.Graph!.End));
        var current = NodeGraphHomes.Current(config, "engine", "shop");
        var branch = NodeGraphHomes.Work(config, "engine", "shop", WorkUnitKind.Branch);
        Assert.Equal((current.Kind, current.Source, current.Cycle, NodeGraphs.Digest(current.Graph!)), (branch.Kind, branch.Source, branch.Cycle, NodeGraphs.Digest(branch.Graph!)));

        var reread = DriverConfig.Parse(config.ToJson());
        Assert.Equal(config.ToJson(), reread.ToJson());
        Assert.Equal(own.Cycle, NodeGraphHomes.Work(reread, "engine", "shop", WorkUnitKind.Quest).Cycle);
        Assert.Equal(DriverConfig.Empty.ToJson(), config.WithWorkCycle("engine", WorkUnitKind.Quest, null).WithWorkspaceWorkCycle("shop", WorkUnitKind.Quest, null)
            .WithWorkspaceWorkCycle("shop", WorkUnitKind.Ask, null).ToJson());
    }

    /// <summary>
    /// The doors: a cycle is set on a rule there is, refused when its graph does not hold (naming the first problem), and kept by a door
    /// with no field for it; a work cycle is refused for a branch or a kind there is none of, and when its graph does not hold.
    /// </summary>
    [Fact]
    public void The_doors_refuse_what_does_not_hold_and_keep_what_they_have_no_field_for()
    {
        var config = DriverConfig.Empty.WithLanding("engine", LandingRule.Merge).WithLandingCycle("engine", QaCycle);
        Assert.Equal(CompactCycle(QaCycle), config.Landings["engine"].Cycle);

        // A rule edited at a door with no field for its cycle (Setup, Ask Daoris, the terminal) keeps it.
        var edited = config.WithLanding("engine", LandingRule.Merge with { Tidy = true });
        Assert.Equal((true, CompactCycle(QaCycle)), (edited.Landings["engine"].Tidy, edited.Landings["engine"].Cycle));
        Assert.Null(edited.WithLandingCycle("engine", null).Landings["engine"].Cycle);
        Assert.False(edited.WithLanding("engine", null).Landings.ContainsKey("engine"));

        var refused = Assert.Throws<DriverException>(() => config.WithLandingCycle("engine", """{ "end": "pull-request" }"""));
        Assert.Contains("`pull-request`", refused.Message);
        Assert.Throws<DriverException>(() => config.WithLandingCycle("elsewhere", QaCycle));
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithWorkspaceLandingCycle("shop", QaCycle));
        const string valid = """{ "nodes": [ { "id": "qa", "kind": "human", "version": 1, "reach": "pressed" } ], "entries": ["qa"], "end": "qa" }""";
        Assert.Equal(CompactCycle(valid), config.WithWorkCycle("engine", WorkUnitKind.Quest, valid).WorkCycles["engine"][WorkUnitKind.Quest]);
        Assert.Throws<DriverException>(() => config.WithWorkCycle("engine", WorkUnitKind.Branch, valid));
        Assert.Throws<DriverException>(() => config.WithWorkCycle("engine", "chore", valid));
        Assert.Throws<DriverException>(() => config.WithWorkspaceWorkCycle("shop", WorkUnitKind.Quest, """{ "entries": ["qa"], "end": "qa" }"""));

        var reread = DriverConfig.Parse(config.ToJson());
        Assert.Equal(config.ToJson(), reread.ToJson());
        Assert.Equal(CompactCycle(QaCycle), reread.Landings["engine"].Cycle);
    }

    /// <summary>
    /// A cycle the file holds that does not read, or does not hold, is kept as written and said, and its rule stands: the landing never
    /// changes because its end was mistyped (§C2, <i>a cycle that later becomes unreachable is said</i>).
    /// </summary>
    [Fact]
    public void A_cycle_that_does_not_hold_in_the_file_is_kept_and_said()
    {
        var config = DriverConfig.Parse("""{ "landings": { "engine": { "form": "merge", "tidy": true, "cycle": { "end": "pull-request", "nodes": 3 } } } }""");

        Assert.True(config.Landings["engine"].Tidy);
        var home = NodeGraphHomes.Current(config, "engine", null);
        Assert.Equal((LandingSource.Repository, (NodeGraph?)null, false), (home.Source, home.Graph, home.Readable));
        Assert.Equal(GraphRefusal.Unreadable, Assert.Single(home.Problems).Code);
        Assert.Contains("\"nodes\":3", config.ToJson().Replace(" ", ""));
    }

    /// <summary>The CLI's twin reads the same table: which cycle governs, from where, row for row.</summary>
    [Theory]
    [MemberData(nameof(Rows))]
    public void The_shared_table_reads_the_same(string name, string config, string repository, string? workspace, string kind, string source, string? cycle)
    {
        var home = NodeGraphHomes.Work(DriverConfig.Parse(config), repository, workspace, kind);
        Assert.Equal((name, source, cycle), (name, home.Source, home.Cycle));
    }

    public static TheoryData<string, string, string, string?, string, string, string?> Rows()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "graph-homes.json")));
        var rows = new TheoryData<string, string, string, string?, string, string, string?>();
        foreach (var row in table.RootElement.GetProperty("rows").EnumerateArray())
        {
            rows.Add(row.GetProperty("name").GetString()!, row.GetProperty("config").GetRawText(), row.GetProperty("repository").GetString()!,
                row.GetProperty("workspace").GetString(), row.GetProperty("kind").GetString()!, row.GetProperty("source").GetString()!,
                row.GetProperty("cycle").ValueKind == JsonValueKind.Null ? null : CompactCycle(row.GetProperty("cycle").GetRawText()));
        }

        return rows;
    }

    /// <summary>
    /// FABRIC-GRAPH1's review, findings 2 and 3: what the file keeps that no unit reads is said, as the CLI's <c>cycleStrays</c> says it: a kind
    /// with no work cycle, and a name an earlier entry of the same map spells in another case (the first written governs).
    /// </summary>
    [Theory]
    [MemberData(nameof(StrayRows))]
    public void The_shared_table_says_the_same_strays(string name, string config, string strays)
    {
        var said = NodeGraphHomes.Strays(DriverConfig.Parse(config));
        Assert.Equal((name, strays), (name, string.Join(" ", said.Select(stray => $"{string.Join("/", stray.At)}:{stray.Code}"))));
        Assert.All(said, stray => Assert.Contains($"`{stray.At[^1]}`", stray.Words));
    }

    public static TheoryData<string, string, string> StrayRows()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "graph-homes.json")));
        var rows = new TheoryData<string, string, string>();
        foreach (var row in table.RootElement.GetProperty("strays").EnumerateArray())
        {
            rows.Add(row.GetProperty("name").GetString()!, row.GetProperty("config").GetRawText(), string.Join(" ", row.GetProperty("strays").EnumerateArray()
                .Select(stray => $"{string.Join("/", stray.GetProperty("at").EnumerateArray().Select(part => part.GetString()))}:{stray.GetProperty("code").GetString()}")));
        }

        return rows;
    }

    /// <summary>
    /// Finding 2: a cycle the file holds that is not an object, and a kind no unit reads, are kept as written by an edit elsewhere and by
    /// one beside them, and the cycle is said where it governs.
    /// </summary>
    [Fact]
    public void A_cycle_that_is_not_an_object_is_kept_and_said()
    {
        var config = DriverConfig.Parse("""{ "landings": { "engine": { "form": "merge", "cycle": [] } }, "workCycles": { "engine": { "quest": 7, "chore": { "end": "done" } } } }""");

        var branch = NodeGraphHomes.Current(config, "engine", null);
        Assert.Equal(("[]", GraphRefusal.Unreadable), (branch.Cycle, Assert.Single(branch.Problems).Code));
        var quest = NodeGraphHomes.Work(config, "engine", null, WorkUnitKind.Quest);
        Assert.Equal(("7", GraphRefusal.Unreadable), (quest.Cycle, Assert.Single(quest.Problems).Code));

        var edited = config.WithLanding("engine", LandingRule.Merge with { Tidy = true }).WithWorkCycle("engine", WorkUnitKind.Quest, null);
        Assert.Equal("[]", edited.Landings["engine"].Cycle);
        Assert.Equal(["chore"], edited.WorkCycles["engine"].Keys);
    }

    private static string CompactCycle(string json) => NodeGraphHomes.Compact(json);
}
