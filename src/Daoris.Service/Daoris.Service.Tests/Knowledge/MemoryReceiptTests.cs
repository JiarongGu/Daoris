using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWCENTRAL2d with 2j's core (the memory coordinator design §3, §6 C, over Lyntai's MC11): the service applies each row under its own
/// id as the call's key, so after a lost answer the key's receipt says whether it ran. A row whose key left a receipt settles by it, one
/// whose key left none is applied again, and nothing is applied twice; the service's start runs the applier and keeps it running.
/// </summary>
public sealed class MemoryReceiptTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = MemoryMachine.Now;

    private MemoryMachine _machine = null!;
    private LyntaiFile _lyntai = null!;

    public async Task InitializeAsync()
    {
        _machine = await MemoryMachine.OpenAsync("memory-receipts");
        _lyntai = LyntaiFile.Open("memory-receipts");
    }

    public async Task DisposeAsync()
    {
        await _lyntai.DisposeAsync();
        await _machine.DisposeAsync();
    }

    private IMemoryEngine Owner => _lyntai.Engine("Owner");

    private MemoryApplier Applier => new(_machine.Memory, _lyntai.Memory);

    /// <summary>A chat's rows released at its completion: <c>ready</c>, an end approved.</summary>
    private async Task<MemoryOp> ReadyAsync(MemoryChange change)
    {
        var chat = await _machine.ChatAsync(Now);
        var op = await _machine.StageAsync(chat, change, Now.AddMinutes(1));
        await _machine.Ledger.AdvanceAsync(chat.Id, "completed", null, null, null, Now.AddMinutes(2));
        if (change.Kind == MemoryOpKind.End) await _machine.Memory.DecideAsync(op.Id, "person", approve: true, Now.AddMinutes(3));
        return op;
    }

    /// <summary>
    /// 🔴 A write whose answer was lost after the engine kept it, its relation not yet made: the restart settles it by its key's receipt,
    /// relates once, and a second recovery changes nothing.
    /// </summary>
    [Fact]
    public async Task A_lost_answer_repeated_by_its_key_is_applied_once()
    {
        var map = await Owner.RememberAsync("The map is drawn in tiles.");
        var op = await ReadyAsync(new MemoryChange(MemoryOpKind.Write, Text: "The legend sits under the map.", Target: map.Id, Relation: "amends"));
        Assert.True(await _machine.Memory.BeginAsync(op.Id));
        var lost = await Owner.RememberAsync("The legend sits under the map.", key: op.Id);

        await _machine.RestartAsync();
        await Applier.StartAsync();
        await Applier.RecoverAsync();

        var settled = (await _machine.Memory.FindAsync(op.Id))!;
        Assert.Equal((MemoryOpState.Applied, lost.Id), (settled.State, settled.Entry));
        Assert.Equal(2, await _lyntai.CountAsync("Owner"));
        Assert.Equal(
            [$"to {map.Id} weight 1"],
            (await _lyntai.StateAsync("Owner", lost.Id)).Split('\n').Where(line => line.StartsWith("to ")).Select(line => line.TrimEnd()));
    }

    /// <summary>🔴 A row an earlier recovery held for the person is settled by its key's receipt once the engine answers one.</summary>
    [Fact]
    public async Task A_held_row_is_settled_by_its_receipt()
    {
        var op = await ReadyAsync(new MemoryChange(MemoryOpKind.Write, Text: "Tiles are sixteen pixels."));
        Assert.True(await _machine.Memory.BeginAsync(op.Id));
        var kept = await Owner.RememberAsync("Tiles are sixteen pixels.", key: op.Id);
        await _machine.Memory.SettleAsync(op.Id, MemoryOutcome.Held, Now.AddMinutes(4));

        await Applier.StartAsync();

        var settled = (await _machine.Memory.FindAsync(op.Id))!;
        Assert.Equal((MemoryOpState.Applied, kept.Id, kept.Revision), (settled.State, settled.Entry, settled.Applied));
        Assert.Equal(1, await _lyntai.CountAsync("Owner"));
    }

    /// <summary>🔴 A row killed in <c>applying</c> before the engine was called left no receipt, so it is applied again, once.</summary>
    [Fact]
    public async Task A_key_with_no_receipt_is_applied_again()
    {
        var op = await ReadyAsync(new MemoryChange(MemoryOpKind.Write, Text: "Tiles are sixteen pixels."));
        Assert.True(await _machine.Memory.BeginAsync(op.Id));

        await _machine.RestartAsync();
        await Applier.StartAsync();

        var settled = (await _machine.Memory.FindAsync(op.Id))!;
        Assert.Equal(MemoryOpState.Applied, settled.State);
        Assert.Equal("Tiles are sixteen pixels.", (await Owner.GetAsync(settled.Entry!))!.Text);
        Assert.Equal(1, await _lyntai.CountAsync("Owner"));
    }

    /// <summary>
    /// 🔴 An end killed in <c>applying</c> after the engine ended the entry under its key settles <c>applied</c> by the receipt, however its
    /// reason reads.
    /// </summary>
    [Fact]
    public async Task An_end_whose_answer_was_lost_settles_by_its_receipt()
    {
        var map = await Owner.RememberAsync("The map is drawn in tiles.");
        var op = await ReadyAsync(new MemoryChange(MemoryOpKind.End, Text: "Vectors now.", Target: map.Id, Revision: map.Revision));
        Assert.True(await _machine.Memory.BeginAsync(op.Id));
        await Owner.EndAsync(map.Id, map.Revision, "Vectors now.", key: op.Id);

        await _machine.RestartAsync();
        await Applier.StartAsync();

        Assert.Equal(MemoryOpState.Applied, (await _machine.Memory.FindAsync(op.Id))!.State);
        Assert.Equal("Vectors now.", (await Owner.GetAsync(map.Id))!.Ended);
    }

    /// <summary>🔴 The service's start runs the applier: a row ready before it is applied at the start, and one released after within its pace.</summary>
    [Fact]
    public async Task The_service_start_applies_ready_rows_and_keeps_applying()
    {
        var before = await ReadyAsync(new MemoryChange(MemoryOpKind.Write, Text: "Tiles are sixteen pixels."));
        using var stop = new CancellationTokenSource();
        var running = new MemoryCoordinator(_machine.Memory, _lyntai.Memory, null) { Pace = TimeSpan.FromMilliseconds(20) }.RunAsync(stop.Token);

        await AppliedAsync(before);
        var after = await ReadyAsync(new MemoryChange(MemoryOpKind.Write, Text: "The legend sits under the map."));
        await AppliedAsync(after);

        await stop.CancelAsync();
        await running;
        Assert.Equal(2, await _lyntai.CountAsync("Owner"));
    }

    private async Task AppliedAsync(MemoryOp op)
    {
        var until = DateTimeOffset.UtcNow + Poll.Patience;
        while ((await _machine.Memory.FindAsync(op.Id))!.State != MemoryOpState.Applied)
        {
            Assert.True(DateTimeOffset.UtcNow < until, $"operation {op.Id} was never applied");
            await Task.Delay(20);
        }
    }
}
