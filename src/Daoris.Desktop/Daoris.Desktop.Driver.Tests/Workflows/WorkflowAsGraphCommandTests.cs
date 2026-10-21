using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-GRAPH1j (D190 point 8, the cutover design §7.2): <c>daoris driver workflow apply &lt;id&gt; --as-graph</c> on shape-level input.
/// The newest version, of steps, is read by its shape, since one holding a kind schema 1 does not run reads as no steps; decoded, its
/// synthetic nodes authored, judged as a saved graph is and saved as the next version, the steps version kept as it was. Two observed
/// nodes of one plugin step are refused naming both, and nothing is written.
/// </summary>
public sealed class WorkflowAsGraphCommandTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "workflow-as-graph", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public WorkflowAsGraphCommandTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string Config => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private string FileOf(string id) => WorkflowStore.PathOf(_home, id);

    private DriverChoicesWorld World() => new()
    {
        Instance = new InstanceWorld { Home = _home, Environment = _ => null, Out = _out, Error = _error, Verb = "daoris" },
        ConfigPath = Config,
        Folder = _home,
        Now = () => new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
    };

    private Task<int> Run(string words) => DriverChoicesCommand.RunAsync(words.Split(' '), World(), CancellationToken.None);

    /// <summary>A workflow whose one version is the steps given, as a file holds it.</summary>
    private void Stored(string steps)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileOf("careful"))!);
        File.WriteAllText(FileOf("careful"), $$"""
            {
              "id": "careful",
              "name": "Careful",
              "versions": [
                { "version": 1, "at": "2026-10-09T09:12:00Z", "door": "screen", "steps": {{steps}} }
              ]
            }

            """.Replace("\r\n", "\n"));
    }

    /// <summary>A go-ahead, a branch landing and a group of a check and a stage: a kind schema 1 does not run, so v1 reads as no steps.</summary>
    private const string Grouped = """
        [ { "id": "work", "kind": "work" }, { "id": "ask", "kind": "go-ahead", "act": "Ship", "on": "the line" },
          { "id": "land", "kind": "landing", "form": "branch", "accept": "you", "plugin": "acme.ci" },
          { "id": "both", "kind": "all", "steps": [ { "id": "unit", "kind": "check", "plugin": "acme.ci" },
            { "id": "smoke", "kind": "stage", "plugin": "acme.deploy", "stage": "smoke", "start": "automatic" } ] } ]
        """;

    private static IReadOnlyList<string> Lines(StringWriter writer) => writer.ToString().Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

    [Fact]
    public async Task A_steps_version_with_a_group_is_saved_as_the_next_version_its_join_authored_and_the_steps_kept()
    {
        Stored(Grouped);
        var before = JsonNode.Parse(File.ReadAllText(FileOf("careful")))!;
        Assert.NotNull(new WorkflowFiles(_home).Load("careful")!.Read.Versions[0].Problem);

        var exit = await Run("workflow apply careful --as-graph");

        Assert.Equal((0, ""), (exit, _error.ToString()));
        var lines = Lines(_out);
        Assert.Equal("daoris: `careful` v1 → v2 — Careful, v1's steps as a graph: a graph (schema 2) of 8 nodes and 9 wires, ending at `both`, "
            + "digest gph-e336ed86bc06526dccbcc95a8da3add3, which this build reads and does not run yet.", lines[0]);
        Assert.Equal("  The go-ahead `ask` now holds what follows it until your yes, or your skip with your words: the steps went on past one that "
            + "could not start.", lines[1]);
        Assert.StartsWith("  Written to ", lines[2], StringComparison.Ordinal);

        var after = JsonNode.Parse(File.ReadAllText(FileOf("careful")))!;
        Assert.True(JsonNode.DeepEquals(before["versions"]![0], after["versions"]![0]), "the steps version is kept as it was");
        var saved = after["versions"]![1]!;
        Assert.Equal((2, 2), (saved["version"]!.GetValue<int>(), saved["schema"]!.GetValue<int>()));
        Assert.DoesNotContain("synthetic", saved.ToJsonString(), StringComparison.Ordinal);
        var read = new WorkflowFiles(_home).Load("careful")!.Read.Versions[1];
        Assert.Equal((null, "gph-e336ed86bc06526dccbcc95a8da3add3"), (read.Problem, read.Digest));
        Assert.Equal(["work", "ask", "ask:rejected", "land", "unit", "smoke", "both", "both:failed"], read.Graph!.Nodes.Select(node => node.Id));
        Assert.Equal((NodeKinds.All, false), read.Graph.Nodes.Single(node => node.Id == "both") is var join ? (join.Kind, join.Synthetic) : default);
    }

    [Fact]
    public async Task Two_checks_of_one_plugin_are_refused_naming_both_and_nothing_is_written()
    {
        Stored("""
            [ { "id": "work", "kind": "work" }, { "id": "land", "kind": "landing", "form": "branch", "accept": "you", "plugin": "acme.ci" },
              { "id": "lint", "kind": "check", "plugin": "acme.ci" }, { "id": "unit", "kind": "check", "plugin": "acme.ci" } ]
            """);
        var before = File.ReadAllBytes(FileOf("careful"));

        var exit = await Run("workflow apply careful --as-graph");

        Assert.Equal((1, ""), (exit, _out.ToString()));
        Assert.Equal("daoris: v2: its graph does not hold: node `unit` and `lint` are both observed nodes of `acme.ci/work.checks`: an event of that "
            + "step names one node, so neither would be reached.", _error.ToString().TrimEnd());
        Assert.Equal(before, File.ReadAllBytes(FileOf("careful")));
    }

    [Fact]
    public async Task With_plan_it_is_said_and_nothing_is_written()
    {
        Stored(Grouped);
        var before = File.ReadAllBytes(FileOf("careful"));

        Assert.Equal(0, await Run("workflow apply careful --as-graph --plan"));

        Assert.Contains("  --plan: nothing was written.", Lines(_out));
        Assert.Equal(before, File.ReadAllBytes(FileOf("careful")));
    }

    [Fact]
    public async Task A_graph_version_is_a_graph_already_and_a_file_beside_the_switch_is_the_usage()
    {
        Stored(Grouped);
        Assert.Equal(0, await Run("workflow apply careful --as-graph"));
        var before = File.ReadAllBytes(FileOf("careful"));
        _error.GetStringBuilder().Clear();

        Assert.Equal(1, await Run("workflow apply careful --as-graph --base 2"));
        Assert.Equal("daoris: `careful` v2 is a graph already: --as-graph saves a version of steps as one.", _error.ToString().TrimEnd());
        _error.GetStringBuilder().Clear();

        Assert.Equal(2, await Run("workflow apply careful steps.json --as-graph"));
        Assert.Equal($"daoris: {DriverWorkflowsCommand.Usage}", _error.ToString().TrimEnd());
        Assert.Equal(before, File.ReadAllBytes(FileOf("careful")));
    }
}
