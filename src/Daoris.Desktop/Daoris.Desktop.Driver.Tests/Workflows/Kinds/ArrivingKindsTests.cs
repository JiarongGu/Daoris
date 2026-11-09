using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The arriving mark (<see cref="NodeKindAttribute.Arriving"/>): a kind declared before it is offered. A save holding one is refused with its
/// words, a graph read back from a file is not, and the palette leaves it out.
/// </summary>
public sealed class ArrivingKindsTests
{
    private const string Watched = """
        {"schemaVersion":2,"entries":["claimed"],"end":"monitor",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"monitor","kind":"monitor","version":1,"reach":"observed"}],
         "wires":[{"id":"c>m","kind":"after","from":"claimed","to":"monitor"}]}
        """;

    private static NodeGraph Graph()
    {
        var parts = NodeGraphs.Parts(Watched, out _)!;
        return new NodeGraph(parts.Nodes, parts.Wires, parts.Entries, parts.End ?? "") { SchemaVersion = parts.SchemaVersion };
    }

    [Fact]
    public void A_save_holding_an_arriving_kind_is_refused_naming_it()
    {
        var kit = NodeKit.Built.Arriving(NodeKinds.Monitor);
        Assert.True(kit.IsArriving(NodeKinds.Monitor));

        var refused = NodeGraphs.Problems(Graph(), kit: kit).Where(problem => problem.Code == GraphRefusal.Arriving).ToList();
        var problem = Assert.Single(refused);
        Assert.Equal("monitor", problem.Node);
        Assert.Contains("`monitor`", problem.Words);

        Assert.DoesNotContain(NodeGraphs.Problems(Graph(), onSave: false, kit: kit), each => each.Code == GraphRefusal.Arriving);
        Assert.DoesNotContain(NodeGraphs.Problems(Graph()), each => each.Code == GraphRefusal.Arriving);
    }

    [Fact]
    public void The_built_kit_marks_no_kind_arriving_but_its_own_and_the_palette_offers_none_that_is()
    {
        var arriving = NodeKit.Kinds.Where(NodeKit.Built.IsArriving).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(WorkflowPalette.Build([]).Kinds, kind => arriving.Contains(kind.Kind));
        Assert.False(NodeKit.Built.Without(NodeKinds.Monitor).Arriving(NodeKinds.Monitor).IsArriving(NodeKinds.Claimed));
    }

    private const string Composed = """
        {"schemaVersion":2,"entries":["claimed"],"end":"line",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"check","kind":"check","version":1,"reach":"performed"},
                  {"id":"integrate","kind":"integrate","version":1,"reach":"waited"},{"id":"line","kind":"line","version":1,"reach":"observed"}],
         "wires":[{"id":"a","kind":"after","from":"claimed","to":"check"},{"id":"b","kind":"after","from":"check","to":"integrate"},
                  {"id":"c","kind":"after","from":"integrate","to":"line"},{"id":"d","kind":"data","from":"check.commit","to":"line.commit"},
                  {"id":"e","kind":"back","from":"integrate","on":"conflicted","to":"check"}]}
        """;

    [Fact]
    public void A_save_wiring_an_arriving_port_or_routing_on_an_arriving_outcome_is_refused_naming_it()
    {
        var parts = NodeGraphs.Parts(Composed, out _)!;
        var graph = new NodeGraph(parts.Nodes, parts.Wires, parts.Entries, parts.End ?? "") { SchemaVersion = parts.SchemaVersion };

        var refused = NodeGraphs.Problems(graph).Where(problem => problem.Code == GraphRefusal.Arriving).ToList();
        Assert.Equal(["check", "line", "integrate"], refused.Select(problem => problem.Node));
        Assert.Equal(["d", "d", "e"], refused.Select(problem => problem.Wire));
        Assert.Contains("`check`'s `commit` output is arriving", refused[0].Words);
        Assert.Contains("`line`'s `commit` input is arriving", refused[1].Words);
        Assert.Contains("`integrate`'s `conflicted` outcome is arriving", refused[2].Words);
        Assert.DoesNotContain(NodeGraphs.Problems(graph, onSave: false), each => each.Code == GraphRefusal.Arriving);
    }

    [Fact]
    public void The_kit_declares_the_ports_and_outcomes_the_cut_over_wires_arriving_and_the_palette_offers_none_of_them()
    {
        static NodeArrivals Of(string kind) => ((BuiltInNode)NodeKit.Of(kind)!).Arrives;
        Assert.Equal(WireValues.Ref, Of(NodeKinds.Integrate).Inputs["ref"]);
        Assert.Equal(["conflicted"], Of(NodeKinds.Integrate).Outcomes);
        Assert.Equal(WireValues.Ref, Of(NodeKinds.Push).Inputs["ref"]);
        Assert.Equal(WireValues.Commit, Of(NodeKinds.Line).Inputs["commit"]);
        Assert.Equal(WireValues.Commit, Of(NodeKinds.Check).Inputs["commit"]);
        Assert.Equal(WireValues.Commit, Of(NodeKinds.Check).Outputs["commit"]);
        Assert.Equal([WireValues.Work, WireValues.Commit], Of(NodeKinds.Tidy).Inputs.Values);
        Assert.True(NodeKit.Built.IsArriving(NodeKinds.Resolve) && NodeKit.Built.IsArriving(NodeKinds.Merge));

        var palette = WorkflowPalette.Build([]);
        foreach (var kind in palette.Kinds)
        {
            var arrives = Of(kind.Kind);
            Assert.DoesNotContain(kind.Inputs.Keys, arrives.Inputs.ContainsKey);
            Assert.DoesNotContain(kind.Outputs.Keys, arrives.Outputs.ContainsKey);
            Assert.DoesNotContain(kind.Outcomes, arrives.Outcomes.Contains);
        }
    }
}
