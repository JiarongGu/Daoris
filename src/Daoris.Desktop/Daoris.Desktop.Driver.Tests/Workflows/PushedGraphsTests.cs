using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// G8's conversion (D241 point 4; <see cref="PushedGraphs"/>): a saved graph that landed by a push or a pull request becomes a branch landing, a
/// <c>push</c> and, where it opened one, the plugin's <c>pull-request</c> step; a graph already in steps is left as it is. The home's migration
/// step appends it as the next version.
/// </summary>
public sealed class PushedGraphsTests
{
    private const string Work = """{"id":"work","kind":"claimed","version":1,"reach":"observed"}""";

    private const string Pushed = """{"id":"pushed","kind":"pushed","version":1,"reach":"observed"}""";

    private const string Requested = """{"id":"pull-request","kind":"pull-request","version":1,"reach":"observed"}""";

    private const string Steps = """{"id":"push","kind":"push","version":1,"reach":"performed"}""";

    private static string Land(string options) => $$"""{"id":"landing","kind":"land","version":1,"reach":"performed","options":{{options}}}""";

    private static string Wire(string id, string from, string to) => $$"""{"id":"{{id}}","kind":"after","from":"{{from}}","to":"{{to}}"}""";

    private static JsonObject Graph(string end, string[] nodes, string[] wires) =>
        (JsonObject)JsonNode.Parse($$"""{"schemaVersion":2,"entries":["work"],"end":"{{end}}","nodes":[{{string.Join(',', nodes)}}],"wires":[{{string.Join(',', wires)}}]}""")!;

    private static string Kinds(JsonObject graph) => string.Join(',', graph["nodes"]!.AsArray().Select(node => (string?)node!["kind"]));

    private static JsonObject Landing(JsonObject graph) =>
        graph["nodes"]!.AsArray().Select(node => node!.AsObject()).Single(node => (string?)node["kind"] == NodeKinds.Land)["options"]!.AsObject();

    private static JsonObject Old(string options, bool requested) => requested
        ? Graph("pull-request", [Work, Land(options), Pushed, Requested], [Wire("a", "work", "landing"), Wire("b", "landing", "pushed"), Wire("c", "pushed", "pull-request")])
        : Graph("pushed", [Work, Land(options), Pushed], [Wire("a", "work", "landing"), Wire("b", "landing", "pushed")]);

    [Fact]
    public void A_pull_request_land_becomes_a_branch_a_push_and_the_plugins_step()
    {
        var converted = PushedGraphs.Convert(Old("""{"method":"pull-request","plugin":"acme.github","pattern":"x/{quest}"}""", requested: true), out var said)!;

        Assert.Equal("claimed,land,push,acme.github/pull-request", Kinds(converted));
        var land = Landing(converted);
        Assert.Equal(("branch", "x/{quest}", null), ((string?)land["method"], (string?)land["pattern"], (string?)land["plugin"]));
        Assert.Equal("performed", (string?)converted["nodes"]!.AsArray().Single(node => (string?)node!["kind"] == "acme.github/pull-request")!["reach"]);
        Assert.Null(said);
        Assert.Empty(NodeGraphs.Read(converted.ToJsonString(), onSave: true).Problems);
    }

    [Fact]
    public void A_push_land_becomes_a_branch_and_a_push()
    {
        var converted = PushedGraphs.Convert(Old("""{"method":"push","plugin":"acme.github"}""", requested: false), out _)!;

        Assert.Equal("claimed,land,push", Kinds(converted));
        Assert.Equal("branch", (string?)Landing(converted)["method"]);
        Assert.Empty(NodeGraphs.Read(converted.ToJsonString(), onSave: true).Problems);
    }

    [Fact]
    public void A_pull_request_that_named_no_plugin_is_dropped_with_its_wires_joined_and_said()
    {
        var converted = PushedGraphs.Convert(Old("""{"method":"pull-request"}""", requested: true), out var said)!;

        Assert.Equal("claimed,land,push", Kinds(converted));
        Assert.Equal("pushed", (string?)converted["end"]);
        Assert.Contains("name the plugin's step", said, StringComparison.Ordinal);
        Assert.Empty(NodeGraphs.Read(converted.ToJsonString(), onSave: true).Problems);
    }

    [Fact]
    public void A_branch_land_that_wrote_plugin_none_loses_the_option()
    {
        var converted = PushedGraphs.Convert(Graph("landing", [Work, Land("""{"method":"branch","plugin":"none"}""")], [Wire("a", "work", "landing")]), out _)!;

        Assert.False(Landing(converted).ContainsKey("plugin"));
    }

    [Theory]
    [InlineData("""{"method":"merge"}""")]
    [InlineData("""{"method":"branch"}""")]
    [InlineData("""{"method":"integrate"}""")]
    public void A_graph_already_in_steps_is_left_as_it_is(string options) =>
        Assert.Null(PushedGraphs.Convert(Graph("landing", [Work, Land(options)], [Wire("a", "work", "landing")]), out _));

    [Fact]
    public void A_graph_that_holds_a_push_and_a_plugins_step_is_left_as_it_is()
    {
        var graph = Graph("pull-request",
            [Work, Land("""{"method":"branch"}"""), Steps, """{"id":"pull-request","kind":"acme.github/pull-request","version":1,"reach":"performed"}"""],
            [Wire("a", "work", "landing"), Wire("b", "landing", "push"), Wire("c", "push", "pull-request")]);

        Assert.Null(PushedGraphs.Convert(graph, out _));
        Assert.Empty(NodeGraphs.Read(graph.ToJsonString(), onSave: true).Problems);
    }
}
