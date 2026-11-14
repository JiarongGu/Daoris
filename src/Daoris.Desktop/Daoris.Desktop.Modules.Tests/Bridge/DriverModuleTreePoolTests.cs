using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

public sealed class DriverModuleTreePoolTests : DriverModuleBridge
{
    private TreePool Pool() => new(Home, () => StoreSettled.Yes);

    private void Slot(string workspace, int number, string line = "")
    {
        var pool = Pool();
        var claim = pool.Claim(new TreeClaimOpening(workspace, "engine", number, TreeClaimKinds.Make, "q1", "", "", "abc123", "abc123", "job"));
        pool.Complete(claim.Fence!, new SlotSettled($"k-{number}", $"trees/k-{number}", line, "abc123", KeptStates.Kept, "", 0, ""));
    }

    [Theory]
    [InlineData(TreeClaimPhases.Undoing)]
    [InlineData(TreeClaimPhases.UndoWaiting)]
    public async Task A_restoring_slot_says_why_it_waits_and_names_no_line_until_its_claim_finishes(string phase)
    {
        Slot("aurora", 2, "s-first");
        Slot("another", 9);
        var pool = Pool();
        var claim = pool.Claim(new TreeClaimOpening("aurora", "engine", 2, TreeClaimKinds.Take, "q2", "s-next", "daoris/s-next", "def456", "abc123", "job")
        {
            ExpectBranch = "daoris/s-first", ExpectLine = "s-first",
        });
        pool.Undoing(claim.Fence!);
        pool.Advance(claim.Fence!, phase);
        var module = Module();

        var read = await AnswerAsync(module, "TREE_POOL", new { workspace = "aurora", repository = "engine" });
        var slot = Assert.Single(read.GetProperty("slots").EnumerateArray());
        Assert.Equal(2, slot.GetProperty("number").GetInt32());
        Assert.Equal("restoring", slot.GetProperty("state").GetString());
        Assert.Equal("tree.kept-restoring", slot.GetProperty("reason").GetString());
        Assert.Equal("", slot.GetProperty("line").GetString());
        Assert.False(slot.TryGetProperty("path", out _));
        Assert.Null(DriverModule.Declared(new() { Module = DriverModule.Name, Type = "TREE_POOL" }));

        pool.Complete(claim.Fence!, new SlotSettled("k-2", "trees/k-2", "s-first", "abc123", KeptStates.Kept, "", 0, ""));
        read = await AnswerAsync(module, "TREE_POOL", new { workspace = "aurora", repository = "engine" });
        slot = Assert.Single(read.GetProperty("slots").EnumerateArray());
        Assert.Equal("held", slot.GetProperty("state").GetString());
        Assert.Equal("s-first", slot.GetProperty("line").GetString());
        Assert.Equal("", slot.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A_claim_in_flight_is_not_presented_as_an_available_slot_or_as_serving_a_line()
    {
        Slot("aurora", 1, "s-first");
        Pool().Claim(new TreeClaimOpening("aurora", "engine", 1, TreeClaimKinds.Take, "q2", "s-next", "daoris/s-next", "def456", "abc123", "job")
        {
            Phase = TreeClaimPhases.Switched,
        });
        var read = await AnswerAsync(Module(), "TREE_POOL", new { workspace = "aurora", repository = "engine" });
        var slot = Assert.Single(read.GetProperty("slots").EnumerateArray());
        Assert.Equal("busy", slot.GetProperty("state").GetString());
        Assert.Equal("", slot.GetProperty("line").GetString());
        Assert.Equal("", slot.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A_repository_without_slots_reads_empty()
    {
        var read = await AnswerAsync(Module(), "TREE_POOL", new { workspace = "aurora", repository = "engine" });
        Assert.Empty(read.GetProperty("slots").EnumerateArray());
    }
}
