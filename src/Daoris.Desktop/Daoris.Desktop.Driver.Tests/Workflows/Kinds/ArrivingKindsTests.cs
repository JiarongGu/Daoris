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
}
