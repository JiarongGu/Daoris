using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A graph's gates are read straight from its nodes' options, with the refusals that a gate chooses
/// only among what the repository declares, and a bad option holds the run naming its node. Pure: no file.
/// </summary>
public sealed class GraphGatesTests
{
    private const string Declared = """
        {"reviews":{"web-app":{"required":true,"environments":[
           {"name":"local","kind":"local","procedure":"README.md","address":"http://localhost:4200"},
           {"name":"dev","kind":"deployed","procedure":"docs/deploying.md"}]}},
         "opinions":{"web-app":{"reviewers":["codex-acp","dsh"],"on":["landing"],"verify":true,"minutes":30}}}
        """;

    private static NodeGraph Graph(params NodeDeclaration[] nodes) => new(nodes, [], [nodes[0].Id], nodes[^1].Id);

    private static NodeDeclaration Node(string kind, params (string Name, string Json)[] options) =>
        new(kind, kind, 1, NodeKit.Row(kind)!.Reach)
        {
            Options = options.ToDictionary(each => each.Name, each => each.Json),
        };

    private static GraphGateReading Read(string config, params NodeDeclaration[] nodes) =>
        GraphGates.Of(Graph(nodes), WorkflowProcess.Declared(DriverConfig.Parse(config), "web-app", "work"));

    [Fact]
    public void A_look_names_a_declared_environment_or_takes_the_first()
    {
        Assert.Equal(("local", null), Pair(Read(Declared, Node(NodeKinds.Look)).Look));
        Assert.Equal(("dev", null), Pair(Read(Declared, Node(NodeKinds.Look, ("environment", "\"dev\""))).Look));

        var beyond = Read(Declared, Node(NodeKinds.Look, ("environment", "\"staging\"")));
        Assert.Equal("staging", beyond.Look.Environment);
        Assert.Contains("does not declare", beyond.Look.Cannot);
    }

    [Fact]
    public void No_look_node_reads_as_no_look()
    {
        Assert.Equal((null, null), Pair(Read(Declared, Node(NodeKinds.Claimed)).Look));
    }

    [Fact]
    public void An_opinion_chooses_among_the_reviewers_declared_and_names_one_beyond_them()
    {
        var chosen = Read(Declared, Node(NodeKinds.Opinion, ("reviewers", "[\"dsh\"]"), ("required", "true"), ("recheck", "false")));
        Assert.Null(chosen.OpinionCannot);
        Assert.Equal(["dsh"], chosen.Opinion!.Rule.Reviewers);
        Assert.True(chosen.Opinion.Rule.Required);
        Assert.False(chosen.Opinion.Rule.Recheck);

        var beyond = Read(Declared, Node(NodeKinds.Opinion, ("reviewers", "[\"someone-else\"]")));
        Assert.Contains("someone-else", beyond.OpinionCannot);

        var none = Read("{}", Node(NodeKinds.Opinion));
        Assert.Contains("declares none", none.OpinionCannot);
    }

    [Fact]
    public void An_option_that_does_not_read_holds_the_run_naming_its_node()
    {
        var notJson = Read(Declared, Node(NodeKinds.Opinion, ("required", "yes")));
        Assert.Contains("node `opinion`", notJson.Problem);

        var wrongKind = Read(Declared, Node(NodeKinds.Look, ("environment", "123")));
        Assert.Contains("node `look`", wrongKind.Problem);
        Assert.Contains("`environment`", wrongKind.Problem);
    }

    [Fact]
    public void The_claimed_options_are_the_sessions_own()
    {
        var read = Read(Declared, Node(NodeKinds.Claimed, ("callMinutes", "20")));
        Assert.Equal("20", read.Claimed["callMinutes"]);
        Assert.Null(read.Problem);
    }

    private static (string? Environment, string? Cannot) Pair(WorkflowLook look) => (look.Environment, look.Cannot);
}
