using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-GRAPH1b (the fabric events design §E7, D157 §2.4, §4.1, §5.2): a named graph as a named workflow's version, schema 2, saved and
/// read from one table, <c>test/fixtures/workflow-graph-versions.json</c>, which the CLI's <c>graphversions.test.ts</c> reads too. A
/// version is its steps or its graph, never both and never in part; an older build, which reads steps alone, reads a graph version as
/// unreadable; the identity a run binds is the graph's own digest; schema-1 versions are written and read as before.
/// </summary>
public sealed class WorkflowGraphVersionsTests
{
    private static readonly string TablePath = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "test", "fixtures", "workflow-graph-versions.json");

    private static JsonObject Table() => JsonNode.Parse(File.ReadAllText(TablePath))!.AsObject();

    /// <summary>A row's JSON with each <c>"$graph"</c> the table's one graph.</summary>
    private static JsonNode? Filled(JsonNode? node, JsonNode graph) => node switch
    {
        JsonValue value when value.TryGetValue<string>(out var text) && text == "$graph" => graph.DeepClone(),
        JsonObject obj => new JsonObject(obj.Select(pair => KeyValuePair.Create(pair.Key, Filled(pair.Value, graph)))),
        JsonArray array => new JsonArray([.. array.Select(each => Filled(each, graph))]),
        _ => node?.DeepClone(),
    };

    private static JsonNode Graph() => Table()["graph"]!;

    public static TheoryData<string> Reads() => new([.. Table()["reads"]!.AsArray().Select(row => (string)row!["name"]!)]);

    public static TheoryData<string> Adds() => new([.. Table()["adds"]!.AsArray().Select(row => (string)row!["name"]!)]);

    public static TheoryData<string> Bound() => new([.. Table()["bound"]!.AsArray().Select(row => (string)row!["name"]!)]);

    private static JsonObject Row(string section, string name) => Table()[section]!.AsArray().Select(row => row!.AsObject()).Single(row => (string)row["name"]! == name);

    [Fact]
    public void The_table_has_its_rows() =>
        Assert.All(new[] { "reads", "adds", "bound" }, section => Assert.NotEmpty(Table()[section]!.AsArray()));

    [Theory, MemberData(nameof(Reads))]
    public void A_version_reads_as_the_table_says(string name)
    {
        var row = Row("reads", name);
        var version = WorkflowNamed.ElementOf(Filled(row["version"], Graph())!);
        var number = version.GetProperty("version").GetInt32();
        var read = WorkflowNamed.ReadVersion(version, number, []);

        Assert.Equal((bool)row["graph"]!, read.Graph is not null);
        Assert.Equal((string?)row["problem"], read.Problem);
        Assert.Equal((string?)row["digest"], read.Digest);
        Assert.Equal(read.Graph is null, read.Steps is not null || read.Problem is not null);
        if (read.Graph is { } graph) Assert.Equal(NodeGraphs.Digest(graph), read.Digest);
    }

    [Theory, MemberData(nameof(Reads))]
    public void An_older_build_reads_the_same_version_as_unreadable_never_in_part(string name)
    {
        // The build before this one reads a version's `steps` alone (ReadSteps) and no other key: a graph version holds none.
        var row = Row("reads", name);
        var version = WorkflowNamed.ElementOf(Filled(row["version"], Graph())!);
        var number = version.GetProperty("version").GetInt32();
        var older = WorkflowNamed.ReadSteps(version.TryGetProperty("steps", out var steps) ? steps : null, number);

        Assert.Equal((string?)row["older"], older.Problem);
        Assert.Equal(older.Problem is null, older.Steps is not null);
    }

    [Fact]
    public void A_file_holding_a_graph_version_reads_its_others_and_the_older_reader_keeps_them()
    {
        var steps = Row("reads", "a schema-1 version, with no schema named")["version"]!;
        var graph = Row("reads", "a graph version")["version"]!;
        var file = new JsonObject
        {
            ["id"] = "docs-to-pr",
            ["name"] = "Docs",
            ["versions"] = new JsonArray(steps.DeepClone(), Filled(graph, Graph()), Filled(Row("reads", "a schema-1 version, with no schema named")["version"], Graph())),
        };
        file["versions"]![2]!["version"] = 3;
        var read = WorkflowNamed.Read(WorkflowNamed.ElementOf(file));

        Assert.Null(read.Problem);
        Assert.Equal([true, false, true], read.Versions.Select(each => each.Steps is not null));
        Assert.NotNull(read.Versions[1].Graph);
        Assert.Equal("v2: it is a graph (schema 2), which this build reads and does not run yet.", read.Versions[1].Unrun);
        Assert.Null(read.Versions[0].Unrun);
    }

    [Theory, MemberData(nameof(Adds))]
    public void A_graph_version_is_added_as_the_table_says(string name)
    {
        var row = Row("adds", name);
        var file = (JsonNode?)Filled(row["file"], Graph());
        var add = new WorkflowVersionAdded((string)row["id"]!, (string?)row["workflowName"], (string)row["door"]!, (string)row["at"]!, null, Filled(row["graph"], Graph()));
        var plan = WorkflowNamed.PlanAdd(file, add, []);

        Assert.Equal((string?)row["problem"], plan.Problem);
        if (row["result"] is { } result)
        {
            Assert.True(JsonNode.DeepEquals(Filled(result, Graph()), plan.Result), plan.Result?.ToJsonString());
            Assert.Equal((int)row["version"]!, plan.Version);
        }
        else
        {
            Assert.Null(plan.Result);
        }
    }

    [Fact]
    public void A_schema_1_version_is_added_as_it_was_with_no_schema_key()
    {
        var steps = JsonNode.Parse("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""")!;
        var plan = WorkflowNamed.PlanAdd(null, new WorkflowVersionAdded("docs-to-pr", "Docs", "terminal", "2026-10-18T09:00:00Z", steps), []);

        Assert.Null(plan.Problem);
        Assert.Equal(["version", "at", "door", "steps"], plan.Result!["versions"]![0]!.AsObject().Select(pair => pair.Key));
    }

    [Fact]
    public void A_graph_added_beside_steps_or_beside_none_writes_nothing()
    {
        var steps = JsonNode.Parse("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""")!;
        var both = WorkflowNamed.PlanAdd(null, new WorkflowVersionAdded("docs-to-pr", "Docs", "terminal", "2026-10-18T09:00:00Z", steps, Graph()), []);
        var neither = WorkflowNamed.PlanAdd(null, new WorkflowVersionAdded("docs-to-pr", "Docs", "terminal", "2026-10-18T09:00:00Z", null), []);

        Assert.Null(both.Result);
        Assert.Equal("v1: a version is its `steps` or its `graph`, never both.", both.Problem);
        Assert.Null(neither.Result);
        Assert.Equal("v1: its `steps` is a list of at least one.", neither.Problem);
    }

    [Fact]
    public void A_runs_first_start_binds_the_newest_version_when_it_is_a_graph_by_its_graph_digest()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-graph-versions-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var steps = JsonNode.Parse("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""")!;
            WorkflowStore.Add(home, new WorkflowVersionAdded("docs-to-pr", "Docs", "terminal", "2026-10-18T09:00:00Z", steps), kept: []);
            WorkflowStore.Add(home, new WorkflowVersionAdded("docs-to-pr", null, "terminal", "2026-10-18T09:05:00Z", null, Graph()), kept: []);
            var config = DriverConfig.Load(DaorisHome.Entry(home, HomeEntry.DriverConfig));
            var binding = WorkflowRunBindings.Plan(config, home, "q1", "engine", "aurora", null, new WorkflowTaskChoice(null, "docs-to-pr"), [], DateTimeOffset.UtcNow);

            Assert.Null(binding.Problem);
            Assert.Equal(2, binding.Version);
            Assert.Equal(NodeGraphs.Digest(NodeGraphs.Read(Graph().ToJsonString()).Graph!), binding.Digest);
            Assert.StartsWith("gph-", binding.Digest);
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [Theory, MemberData(nameof(Bound))]
    public void A_run_bound_to_a_graph_version_binds_its_graph_digest(string name)
    {
        var row = Row("bound", name);
        var file = WorkflowNamed.Read(WorkflowNamed.ElementOf(Filled(row["file"], Graph())!));
        var version = file.Versions.Single(each => each.Version == (int)row["version"]!);
        var digest = (string?)row["digest"] == "$digest" ? version.Digest : (string?)row["digest"];
        var binding = new WorkflowRunBinding("r1", "repo", "ws", "docs-to-pr", "repository", DateTimeOffset.UnixEpoch) { Version = version.Version, Digest = digest };
        var decoded = WorkflowGraphs.ForRun(file, binding)!;

        Assert.Equal((string?)row["identity"] == "$digest" ? version.Digest : (string?)row["identity"], decoded.Identity);
        Assert.Equal((string?)row["unreadable"], decoded.Unreadable);
        if (row["unreadable"] is null && version.Graph is not null)
        {
            Assert.Equal(NodeGraphs.Digest(decoded.Graph!), decoded.GraphDigest);
            Assert.Equal(decoded.GraphDigest, binding.Digest);
        }
    }
}
