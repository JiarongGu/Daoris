using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

[Collection("knowledge review port")]
public sealed class KnowledgeWorkOrderPortsTests
{
    [Fact]
    public void Work_orders_installs_the_reader_and_its_records_keep_their_identity()
    {
        Assert.IsType<KnowledgeWorkOrderReads>(KnowledgeWorkOrders.Current);
        var look = new SessionLook([], [], []);
        IKnowledgeSessionLook read = look;
        Assert.Same(look.Records, read.Records);
        var trees = new Dictionary<string, TreeWork> { ["app/tree"] = new(1, 0) };
        var changed = Assert.IsType<SessionLook>(read.WithTrees(trees));
        Assert.Same(trees, changed.Trees);
        Assert.Same(look.Records, changed.Records);
        Assert.Empty(look.Trees);
        Assert.Equal(SessionGroups.TreesToJudge(look, _ => true), KnowledgeWorkOrders.Current.TreesToJudge(read, _ => true));
        Assert.Equal(SessionGroups.WaitsOnYou("s1234567", "awaiting-person", null),
            KnowledgeWorkOrders.Current.WaitsOnYou("s1234567", "awaiting-person", null));
        Assert.Equal("app/tree", KnowledgeWorkOrders.Current.Normal("app\\tree/"));
        var placed = KnowledgeWorkOrders.Current.Placed(look);
        Assert.Equal((0, 0, 0, 0, 0), (placed.You, placed.Review, placed.Working, placed.Later, placed.Ended));
        Assert.Empty(placed.Listed);
    }

    [Fact]
    public void An_uninstalled_work_order_reader_throws_and_the_test_restores_it()
    {
        var reader = KnowledgeWorkOrders.Current;
        try
        {
            KnowledgeWorkOrders.Current = null!;
            Assert.Throws<InvalidOperationException>(() => KnowledgeWorkOrders.Current);
        }
        finally
        {
            KnowledgeWorkOrders.Current = reader;
        }
        Assert.Same(reader, KnowledgeWorkOrders.Current);
    }
}
