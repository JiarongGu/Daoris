using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-WORKFLOW2 (the fabric events design §E7, §E4; canaries 49 and 50): D157's stored versions read as graphs by the exact migration
/// contract, never rewritten; a version that does not decode read as unreadable with its reason, never in part; a bound run's identity
/// unchanged; and the CLI's twin read from one table, <c>test/fixtures/workflow-graphs.json</c>, row for row.
/// </summary>
public sealed class WorkflowGraphsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-workflow-graphs-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static JsonElement Table()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "test", "fixtures", "workflow-graphs.json")));
        return table.RootElement.Clone();
    }

    public static TheoryData<string> Rows() => new([.. Table().GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("name").GetString()!)]);

    /// <summary>A decoded graph as the table writes it: its nodes' options as JSON values, and nothing a decoded node or wire never declares.</summary>
    internal static JsonObject ToJson(NodeGraph graph)
    {
        JsonNode Node(NodeDeclaration node)
        {
            Assert.Equal((0, 0, null, null, null, false, true), (node.Inputs.Count, node.Outputs.Count, node.Evidence, node.Retry, node.TimeoutSeconds, node.Rejectable, node.Skippable));
            var json = new JsonObject { ["id"] = node.Id, ["kind"] = node.Kind, ["version"] = node.Version, ["reach"] = node.Reach };
            if (node.Synthetic) json["synthetic"] = true;
            if (node.Options.Count > 0) json["options"] = new JsonObject(node.Options.Select(pair => KeyValuePair.Create(pair.Key, JsonNode.Parse(pair.Value))));
            return json;
        }

        JsonNode Wire(Wire wire)
        {
            Assert.Equal((null, null, null, null, null), (wire.From!.Port, wire.To.Port, wire.From.Unit, wire.Waits, wire.Group));
            return new JsonObject { ["id"] = wire.Id, ["kind"] = wire.Kind, ["from"] = wire.From.Node, ["to"] = wire.To.Node };
        }

        return new JsonObject
        {
            ["schemaVersion"] = graph.SchemaVersion,
            ["entries"] = new JsonArray([.. graph.Entries.Select(entry => (JsonNode?)entry)]),
            ["end"] = graph.End,
            ["cleanup"] = new JsonObject { ["until"] = graph.Cleanup.Until },
            ["nodes"] = new JsonArray([.. graph.Nodes.Select(Node)]),
            ["wires"] = new JsonArray([.. graph.Wires.Select(Wire)]),
        };
    }

    /// <summary>A row's version decoded as the row says: as the stored version reads in this build, or from its steps' shape.</summary>
    private static DecodedVersion Decoded(JsonElement row)
    {
        var version = row.GetProperty("version");
        var number = version.GetProperty("version").GetInt32();
        if (row.GetProperty("read").GetString() == "version") return WorkflowGraphs.Decode(WorkflowNamed.ReadVersion(version, number, []));
        var (steps, problem) = WorkflowNamed.ReadShape(version.GetProperty("steps"), number);
        Assert.Null(problem);
        return WorkflowGraphs.DecodeSteps(number, steps!);
    }

    /// <summary>§E7's contract, row for row, as the CLI's twin reads the same table: each graph, its identity and its digest, or the reason it is unreadable.</summary>
    [Theory]
    [MemberData(nameof(Rows))]
    public void Each_row_of_the_shared_table_decodes_as_the_CLI_decodes_it(string name)
    {
        var table = Table();
        Assert.Equal(WorkflowGraphs.DecoderVersion, table.GetProperty("decoderVersion").GetInt32());
        var row = table.GetProperty("rows").EnumerateArray().Single(each => each.GetProperty("name").GetString() == name);
        var decoded = Decoded(row);
        Assert.Equal(row.GetProperty("version").GetProperty("version").GetInt32(), decoded.Version);
        if (row.TryGetProperty("unreadable", out var unreadable))
        {
            Assert.Equal((unreadable.GetString(), false, null, null, null), (decoded.Unreadable, decoded.Readable, decoded.Graph, decoded.GraphDigest, decoded.Identity));
            return;
        }

        Assert.Null(decoded.Unreadable);
        Assert.Empty(NodeGraphs.Problems(decoded.Graph!));
        var compact = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        Assert.Equal(JsonNode.Parse(row.GetProperty("graph").GetRawText())!.ToJsonString(compact), ToJson(decoded.Graph!).ToJsonString(compact));
        Assert.Equal((row.GetProperty("identity").GetString(), row.GetProperty("graphDigest").GetString()), (decoded.Identity, decoded.GraphDigest));
    }

    private static NamedStep Step(string id, string kind, params (string Name, object? Value)[] fields)
    {
        var row = WorkflowKindTable.Of(kind)!;
        return new NamedStep(id, kind, [.. row.Fields.Select(field => new WorkflowSetting(field.Name, fields.FirstOrDefault(each => each.Name == field.Name) is { Name: not null } named ? named.Value : field.Default))]);
    }

    private static NamedStep Landing(string id = "land") => Step(id, WorkflowKinds.Landing, ("form", LandingForm.Branch), ("accept", "you"), ("plugin", "acme.ci"));

    private static NamedStep Check(string id, string failed) => Step(id, WorkflowKinds.Check, ("plugin", "acme.ci"), ("failed", failed));

    private static NamedStep Stage(string id, string failed, string start = "automatic") =>
        Step(id, WorkflowKinds.Stage, ("plugin", "acme.deploy"), ("stage", id), ("start", start), ("failed", failed));

    private static NamedStep Group(string id, string failed, params NamedStep[] members) => Step(id, WorkflowKinds.All, ("steps", members.ToList()), ("failed", failed));

    private static readonly string[] Policies = ["stop", "wait", "send-back"];

    /// <summary>
    /// Canary 50 and §E7's boundaries: a maximal version of 24 steps, members counted, with groups and every <c>failed</c> policy, decodes
    /// within its synthetic bound and holds, both as many small groups (the most synthetic nodes to an authored one) and as wide ones.
    /// </summary>
    [Theory]
    [InlineData(1, 11)]
    [InlineData(2, 7)]
    [InlineData(21, 1)]
    public void A_maximal_version_with_groups_and_every_policy_decodes_within_its_synthetic_bound(int width, int groups)
    {
        var steps = new List<NamedStep> { Step("work", WorkflowKinds.Work), Landing() };
        for (var group = 0; group < groups; group++)
        {
            var members = Enumerable.Range(0, width).Select(member => member % 2 == 0
                ? Check($"c{group}-{member}", Policies[member % 3]) : Stage($"s{group}-{member}", Policies[member % 3], member % 4 == 1 ? "you" : "automatic"));
            steps.Add(Group($"g{group}", Policies[group % 3], [.. members]));
        }

        while (steps.Count + steps.Sum(step => step.Members.Count) < WorkflowKindTable.MaxSteps) steps.Add(Stage($"tail{steps.Count}", Policies[steps.Count % 3]));
        Assert.Equal(WorkflowKindTable.MaxSteps, steps.Count + steps.Sum(step => step.Members.Count));

        var decoded = WorkflowGraphs.DecodeSteps(1, steps);
        Assert.Null(decoded.Unreadable);
        var graph = decoded.Graph!;
        Assert.Empty(NodeGraphs.Problems(graph));
        var authored = graph.Nodes.Count(node => !node.Synthetic);
        Assert.Equal(WorkflowKindTable.MaxSteps - groups, authored);
        Assert.InRange(graph.Nodes.Count - authored, groups, NodeGraphs.SyntheticPerNode * authored);
        Assert.Equal(groups, graph.Nodes.Count(node => node.Kind == NodeKinds.All && node.Synthetic));
    }

    /// <summary>§E4: each of D157's kinds is its node, with its reach; a group is a synthetic join with the group's id.</summary>
    [Theory]
    [InlineData(WorkflowKinds.Work, NodeKinds.Claimed, NodeReach.Observed)]
    [InlineData(WorkflowKinds.Opinion, NodeKinds.Opinion, NodeReach.Observed)]
    [InlineData(WorkflowKinds.Look, NodeKinds.Look, NodeReach.Observed)]
    [InlineData(WorkflowKinds.Landing, NodeKinds.Handed, NodeReach.Observed)]
    [InlineData(WorkflowKinds.PullRequest, NodeKinds.PullRequest, NodeReach.Observed)]
    [InlineData(WorkflowKinds.GoAhead, NodeKinds.GoAhead, NodeReach.Pressed)]
    [InlineData(WorkflowKinds.Check, "acme.ci/work.checks", NodeReach.Observed)]
    [InlineData(WorkflowKinds.Stage, "acme.deploy/x", NodeReach.Performed)]
    [InlineData(WorkflowKinds.All, NodeKinds.All, NodeReach.Observed)]
    public void Each_kind_decodes_to_its_node(string kind, string node, string reach)
    {
        var step = kind switch
        {
            WorkflowKinds.Work => Step("work", kind),
            WorkflowKinds.Landing => Landing("x"),
            WorkflowKinds.GoAhead => Step("x", kind, ("act", "Ship"), ("on", "the line")),
            WorkflowKinds.Check => Check("x", "wait"),
            WorkflowKinds.Stage => Stage("x", "wait"),
            WorkflowKinds.All => Group("x", "wait", Check("m", "wait")),
            _ => Step("x", kind),
        };
        List<NamedStep> steps = kind switch
        {
            WorkflowKinds.Work => [step, Landing()],
            WorkflowKinds.Opinion or WorkflowKinds.Look => [Step("work", WorkflowKinds.Work), step, Landing()],
            WorkflowKinds.Landing => [Step("work", WorkflowKinds.Work), step],
            _ => [Step("work", WorkflowKinds.Work), Landing(), step],
        };

        var graph = WorkflowGraphs.Graph(steps);
        var decoded = graph.Nodes.Single(each => each.Id == step.Id);
        Assert.Equal((node, reach, kind == WorkflowKinds.All), (decoded.Kind, decoded.Reach, decoded.Synthetic));
        Assert.Empty(NodeGraphs.Problems(graph));
    }

    /// <summary>
    /// Canary 49, the skip's half (§E7's last row, NODE1c's choice 3 kept for decoding): the person's skip on a step is its node's, and on a
    /// group its members', since a join is never skipped. The kinds D157's person skips are skippable nodes; the work and the landing,
    /// git's and the ledger's, are not, as D157's run never took the person's skip on them.
    /// </summary>
    [Fact]
    public void A_skip_on_each_kind_is_its_nodes_and_a_groups_its_members()
    {
        List<NamedStep> steps =
        [
            Step("work", WorkflowKinds.Work), Step("ask", WorkflowKinds.GoAhead, ("act", "Ship"), ("on", "the line")), Step("second", WorkflowKinds.Opinion),
            Step("see", WorkflowKinds.Look), Landing(), Step("pr", WorkflowKinds.PullRequest), Check("lint", "stop"), Stage("deploy", "wait"),
            Group("both", "wait", Check("unit", "wait"), Stage("smoke", "stop")),
        ];
        var graph = WorkflowGraphs.Graph(steps);
        Assert.Equal(new[] { "unit", "smoke" }, WorkflowGraphs.Skipped(steps, "both"));
        foreach (var step in steps.Where(step => step.Kind != WorkflowKinds.All))
        {
            Assert.Equal(new[] { step.Id }, WorkflowGraphs.Skipped(steps, step.Id));
            var skips = NodeAuthorities.Of(graph.Nodes.Single(node => node.Id == step.Id))!.PersonSkips;
            Assert.Equal((step.Id, step.Kind is not (WorkflowKinds.Work or WorkflowKinds.Landing)), (step.Id, skips));
        }

        Assert.Empty(WorkflowGraphs.Skipped(steps, "nothing"));
    }

    /// <summary>A graph that would not hold is no graph: the version reads as unreadable with the first problem, never decoded in part.</summary>
    [Fact]
    public void A_version_whose_graph_does_not_hold_is_unreadable_never_decoded_in_part()
    {
        // Steps handed in whole, as no stored version reads: the look after the landing breaks D157 §2.2's order.
        var decoded = WorkflowGraphs.DecodeSteps(3, [Step("work", WorkflowKinds.Work), Landing(), Step("see", WorkflowKinds.Look)]);
        Assert.Equal((false, null, null), (decoded.Readable, decoded.Graph, decoded.GraphDigest));
        Assert.Equal("v3: its graph does not hold: the look `see` comes before the landing `land` (D157 §2.2's order: holds, opinion, look, landing).", decoded.Unreadable);
    }

    /// <summary>§E7: the identity is the original digest; the graph's digest is derived from it and the decoder's version, the same each time.</summary>
    [Fact]
    public void Decoding_twice_gives_the_same_graph_and_digest_and_the_identity_is_the_versions_digest()
    {
        List<NamedStep> steps = [Step("work", WorkflowKinds.Work), Landing(), Group("both", "send-back", Check("unit", "wait"), Stage("smoke", "stop"))];
        var first = WorkflowGraphs.DecodeSteps(1, steps);
        var second = WorkflowGraphs.DecodeSteps(1, [.. steps]);
        Assert.Equal(WorkflowNamed.Digest(steps), first.Identity);
        Assert.Equal((first.Identity, first.GraphDigest, NodeGraphs.Digest(first.Graph!)), (second.Identity, second.GraphDigest, NodeGraphs.Digest(second.Graph!)));
        Assert.Matches("^gph-[0-9a-f]{32}$", first.GraphDigest!);
        Assert.NotEqual(NodeGraphs.Digest(first.Graph!), first.GraphDigest);
        Assert.NotEqual(first.GraphDigest, WorkflowGraphs.GraphDigest("000000000000", first.Graph!));
    }

    /// <summary>
    /// FABRIC-GRAPH1h (D157 §2.2; GRAPH1's choice 5, WORKFLOW2's choice 10): the protected order held by wires, not by reachability alone.
    /// A skip satisfies its <c>after</c> wire (§E2), so a chain alone lets the landing past an unsettled gate once a step between is
    /// skipped. Each gate before the landing (the work, whose node carries the holds; the opinion; the look) is wired into every gate after
    /// it, once, beside the step order; a go-ahead is no gate, and nothing after the landing gains a wire.
    /// </summary>
    [Fact]
    public void The_protected_order_wires_each_gate_into_every_gate_after_it()
    {
        List<NamedStep> steps =
        [
            Step("work", WorkflowKinds.Work), Step("ask", WorkflowKinds.GoAhead, ("act", "Ship"), ("on", "the line")), Step("second", WorkflowKinds.Opinion),
            Step("see", WorkflowKinds.Look), Landing(), Step("pr", WorkflowKinds.PullRequest), Check("lint", "stop"),
        ];
        var graph = WorkflowGraphs.Graph(steps);

        Assert.Equal(
            ["work>ask", "ask>second", "work>second", "second>see", "work>see", "see>land", "work>land", "second>land", "land>pr", "pr>lint"],
            graph.Wires.Where(wire => wire.Kind == WireKinds.After).Select(wire => wire.Id));
        Assert.Equal(graph.Wires.Count, graph.Wires.Select(wire => wire.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(NodeGraphs.Problems(graph));
        Assert.Equal(["work>land"], WorkflowGraphs.Graph([Step("work", WorkflowKinds.Work), Landing()]).Wires.Select(wire => wire.Id));
    }

    /// <summary>
    /// FABRIC-GRAPH1h: why a decoder change moves nothing stored. No record binds a decoded graph's digest yet: outside the decoder, only
    /// the read-only projection (<c>WorkflowRunNodes</c>) reads it, and nothing else in the driver, whose runtime writes every node record,
    /// reads the projection; the runtime governs the rules' graphs and kept closures by their own digest (<c>NodeRuntime.Govern</c>), and a
    /// graph version is not decoded. When the cutover binds a run to a decoded graph this fails, and a decoder change is then a stored
    /// binding's to keep (D173's FABRIC-GRAPH1h note).
    /// </summary>
    [Fact]
    public void Nothing_outside_the_read_only_projection_reads_the_decoder()
    {
        var driver = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");
        var separator = Path.DirectorySeparatorChar;
        var readers = Directory.GetFiles(driver, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal) && !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            .Where(file => Path.GetFileName(file) is not ("WorkflowGraphs.cs" or "WorkflowRunNodes.cs"))
            .Where(file => File.ReadAllText(file) is var text
                && (text.Contains("WorkflowGraphs.", StringComparison.Ordinal) || text.Contains("WorkflowRunNodes.", StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(driver, file))
            .ToList();
        Assert.Empty(readers);
    }

    private const string WorkflowFile = """
        {
          "id": "careful",
          "name": "Careful",
          "versions": [
            { "version": 1, "at": "2026-10-09T09:12:00Z", "door": "screen",
              "steps": [ { "id": "work", "kind": "work" }, { "id": "land", "kind": "landing", "form": "merge", "accept": "you" } ] },
            { "version": 2, "at": "2026-10-10T09:12:00Z", "door": "terminal",
              "steps": [ { "id": "work", "kind": "work" }, { "id": "see", "kind": "look" }, { "id": "land", "kind": "landing", "form": "merge", "accept": "you" } ] }
          ]
        }
        """;

    private WorkflowRunBinding Bound(string run, int version, string? digest) =>
        new(run, "engine", "aurora", "careful", WorkflowLevels.Repository, At) { Version = version, Digest = digest };

    /// <summary>
    /// §E7, <i>the facts stay where they are</i>: reading a stored version and the run bound to it, and decoding it, leaves both files byte
    /// for byte; the bound run's identity is its binding's digest, unchanged; a binding the file no longer holds as bound is unreadable.
    /// </summary>
    [Fact]
    public void A_bound_run_keeps_its_identity_and_reading_leaves_the_stored_files_byte_for_byte()
    {
        var path = WorkflowStore.PathOf(_home, "careful");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, WorkflowFile.Replace("\r\n", "\n") + "\n");
        var store = new WorkflowFiles(_home);
        var read = store.Load("careful")!.Read;
        var runs = new WorkflowRunFiles(_home);
        runs.Bind(Bound("run1", 1, read.Versions[0].Digest));
        runs.Bind(Bound("run2", 2, read.Versions[1].Digest));
        var before = Directory.GetFiles(_home, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(file => (file, File.ReadAllBytes(file))).ToList();

        foreach (var run in new[] { "run1", "run2" })
        {
            var binding = runs.Read(run)!;
            var decoded = WorkflowGraphs.ForRun(store.Load("careful")!.Read, binding)!;
            Assert.Equal((binding.Version!.Value, binding.Digest, (string?)null), (decoded.Version, decoded.Identity, decoded.Unreadable));
            Assert.Empty(NodeGraphs.Problems(decoded.Graph!));
        }

        Assert.Null(WorkflowGraphs.ForRun(read, new WorkflowRunBinding("run3", "engine", "aurora", WorkflowSelection.Current, WorkflowLevels.Current, At)));
        Assert.Equal(
            "the run `run4` is bound to v1 at `000000000000`, and the file's v1 reads as `" + read.Versions[0].Digest + "`: a version is never edited, so its file is not the one the run started on.",
            WorkflowGraphs.ForRun(read, Bound("run4", 1, "000000000000"))!.Unreadable);
        Assert.Equal("the run `run5` is bound to v7, which the workflow's file no longer holds.", WorkflowGraphs.ForRun(read, Bound("run5", 7, "abc"))!.Unreadable);

        var after = Directory.GetFiles(_home, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(file => (file, File.ReadAllBytes(file))).ToList();
        Assert.Equal(before.Select(each => each.file), after.Select(each => each.file));
        Assert.All(before.Zip(after), pair => Assert.Equal(pair.First.Item2, pair.Second.Item2));
    }
}
