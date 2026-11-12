using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>What a press's answer says it made, read back from the text the pipe wrote: a department's receipt or a room's, and nothing from any other answer.</summary>
public sealed class PressReceiptsTests
{
    [Fact]
    public void A_departments_receipt_is_read_from_the_answers_data()
    {
        var answer = """
            {"id":"p","success":true,"data":{"said":{"out":["x"],"error":[],"code":0},
             "receipt":{"kind":"opened","repository":"ledger","workspace":"aurora","folder":"/work/aurora/ledger","line":"main","room":null,"quests":["q7"],"done":true}}}
            """;

        var receipt = PressReceipts.Of(answer);

        Assert.Equal(new PressReceipt("opened", "ledger", "aurora", "/work/aurora/ledger", "main", null, ["q7"], true), receipt, new ReceiptComparer());
    }

    [Fact]
    public void A_rooms_receipt_is_its_one_quest()
    {
        var receipt = PressReceipts.Of("""{"success":true,"data":{"done":true,"receipt":{"kind":"room","repository":"engine","room":"tools/web","quests":["q9"],"done":true}}}""");

        Assert.Equal(("room", "engine", "tools/web", true), (receipt!.Kind, receipt.Repository, receipt.Room, receipt.Done));
        Assert.Equal(["q9"], receipt.Quests);
    }

    [Theory]
    [InlineData("""{"success":true,"data":{"done":true}}""")]
    [InlineData("""{"success":true,"data":{"receipt":null}}""")]
    [InlineData("""{"success":true,"data":{"receipt":{"kind":"unknown","repository":"x","quests":[],"done":true}}}""")]
    [InlineData("""{"success":true,"data":{"receipt":{"kind":"opened","quests":[],"done":true}}}""")]
    [InlineData("""{"success":false,"error":{"message":"no"}}""")]
    [InlineData("not json")]
    public void An_answer_that_names_no_receipt_it_can_read_has_none(string answer) => Assert.Null(PressReceipts.Of(answer));

    /// <summary>Records compare their lists by reference, so the receipts are compared field by field.</summary>
    private sealed class ReceiptComparer : IEqualityComparer<PressReceipt?>
    {
        public bool Equals(PressReceipt? x, PressReceipt? y) =>
            x is not null && y is not null && (x.Kind, x.Repository, x.Workspace, x.Folder, x.Line, x.Room, x.Done) == (y.Kind, y.Repository, y.Workspace, y.Folder, y.Line, y.Room, y.Done)
            && x.Quests.SequenceEqual(y.Quests);

        public int GetHashCode(PressReceipt? obj) => obj?.Repository.GetHashCode() ?? 0;
    }
}
