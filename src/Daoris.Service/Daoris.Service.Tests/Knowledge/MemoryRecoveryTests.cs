using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWCENTRAL2c (the memory coordinator design §3), over 2b's stand-in: only the service marks <c>applying</c>, calls the engine and
/// settles; at its start it applies what is <c>ready</c> and reads each <c>applying</c> row observationally, settling an end by its token
/// and holding an addition or relation, which nothing says ran.
/// </summary>
public sealed class MemoryRecoveryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = MemoryMachine.Now;

    private readonly StandInMemory _memory = new();

    private MemoryMachine _machine = null!;

    private StandInMemory.Store Owner => _memory.Engine("Owner");

    private IMemoryEngine Engine => _memory.Open("Owner", MemoryReinforcement.None)!;

    public async Task InitializeAsync()
    {
        _memory.Engine("Owner");
        _machine = await MemoryMachine.OpenAsync("memory-recovery");
    }

    public async Task DisposeAsync() => await _machine.DisposeAsync();

    private MemoryApplier Applier => new(_machine.Memory, _memory);

    private static MemoryChange End(string entry, long revision, string reason = "It no longer holds.") =>
        new(MemoryOpKind.End, Text: reason, Target: entry, Revision: revision);

    /// <summary>The operations a chat staged and released at its completion, each end approved.</summary>
    private async Task<MemoryOp[]> ReadyAsync(params MemoryChange[] changes)
    {
        var chat = await _machine.ChatAsync(Now);
        var staged = new List<MemoryOp>();
        foreach (var change in changes) staged.Add(await _machine.StageAsync(chat, change, Now.AddMinutes(1)));
        await _machine.Ledger.AdvanceAsync(chat.Id, "completed", null, null, null, Now.AddMinutes(2));
        foreach (var end in staged.Where(op => op.Change.Kind == MemoryOpKind.End))
        {
            Assert.Equal(MemoryOpState.Ready, (await _machine.Memory.DecideAsync(end.Id, "person", approve: true, Now.AddMinutes(3)))!.State);
        }

        return [.. staged];
    }

    private async Task<string> StateAsync(MemoryOp op) => (await _machine.Memory.FindAsync(op.Id))!.State;

    /// <summary>🔴 A kill leaves a staged row and an awaiting end as they were: the next start applies neither and touches no engine.</summary>
    [Fact]
    public async Task Staged_and_awaiting_rows_are_unchanged_by_a_kill()
    {
        var entry = Owner.Keep("The map is drawn in tiles.");
        var session = await _machine.InTreeAsync(Now);
        var staged = await _machine.StageAsync(session, new(MemoryOpKind.Write, Text: "Tiles are sixteen pixels."), Now.AddMinutes(1));
        var chat = await _machine.ChatAsync(Now);
        var end = await _machine.StageAsync(chat, End(entry.Id, entry.Revision), Now.AddMinutes(1));
        await _machine.Ledger.AdvanceAsync(chat.Id, "completed", null, null, null, Now.AddMinutes(2));
        var before = _memory.State();

        await _machine.RestartAsync();
        await Applier.StartAsync();

        Assert.Equal((MemoryOpState.Staged, MemoryOpState.Awaiting), (await StateAsync(staged), await StateAsync(end)));
        Assert.Equal(before, _memory.State());
    }

    /// <summary>🔴 A kill after the commit point leaves its rows <c>ready</c>, and the next start applies them, naming what the engine answered.</summary>
    [Fact]
    public async Task A_kill_after_the_commit_point_leaves_ready_rows_the_next_start_applies()
    {
        var write = (await ReadyAsync(new MemoryChange(MemoryOpKind.Write, Text: "Tiles are sixteen pixels.")))[0];

        await _machine.RestartAsync();
        await Applier.StartAsync();

        var applied = await _machine.Memory.FindAsync(write.Id);
        var kept = await Engine.GetAsync(applied!.Entry!);
        Assert.Equal((MemoryOpState.Applied, "Tiles are sixteen pixels.", kept!.Revision), (applied.State, kept.Text, applied.Applied));
    }

    /// <summary>🔴 After a kill in <c>applying</c>, an end the engine kept under this row's token is <c>applied</c>, and nothing ends it again.</summary>
    [Fact]
    public async Task After_a_kill_in_applying_an_end_kept_under_its_token_is_applied()
    {
        var entry = Owner.Keep("The map is drawn in tiles.");
        var end = (await ReadyAsync(End(entry.Id, entry.Revision)))[0];
        Assert.True(await _machine.Memory.BeginAsync(end.Id));
        await Engine.EndAsync(entry.Id, entry.Revision, MemoryEndToken.Append("It no longer holds.", end.Id));
        var before = _memory.State();

        await _machine.RestartAsync();
        await Applier.StartAsync();

        Assert.Equal(MemoryOpState.Applied, await StateAsync(end));
        Assert.Equal(before, _memory.State());
        Assert.Equal("It no longer holds.", MemoryEndToken.Strip((await Engine.GetAsync(entry.Id))!.Ended!));
    }

    /// <summary>🔴 After a kill in <c>applying</c>, an end whose entry another end ended is <c>stale</c>.</summary>
    [Fact]
    public async Task After_a_kill_in_applying_an_end_under_anothers_token_is_stale()
    {
        var entry = Owner.Keep("The map is drawn in tiles.");
        var end = (await ReadyAsync(End(entry.Id, entry.Revision)))[0];
        Assert.True(await _machine.Memory.BeginAsync(end.Id));
        await Engine.EndAsync(entry.Id, entry.Revision, MemoryEndToken.Append("Vectors now.", "0123456789abcdef"));

        await _machine.RestartAsync();
        await Applier.StartAsync();

        Assert.Equal(MemoryOpState.Stale, await StateAsync(end));
    }

    /// <summary>🔴 After a kill in <c>applying</c> before the engine was called, the entry unended at the revision read is ended again, once.</summary>
    [Fact]
    public async Task After_a_kill_in_applying_an_end_at_the_revision_read_is_ended_again()
    {
        var entry = Owner.Keep("The map is drawn in tiles.");
        var end = (await ReadyAsync(End(entry.Id, entry.Revision)))[0];
        Assert.True(await _machine.Memory.BeginAsync(end.Id));

        await _machine.RestartAsync();
        await Applier.StartAsync();

        Assert.Equal(MemoryOpState.Applied, await StateAsync(end));
        Assert.Equal(end.Id, MemoryEndToken.Of((await Engine.GetAsync(entry.Id))!.Ended));
    }

    /// <summary>
    /// 🔴 After a kill in <c>applying</c>, a write whose same content the engine holds and a relation the engine holds are each <c>held</c>
    /// for the person: nothing says which operation ran, and recovery makes no second effect.
    /// </summary>
    [Fact]
    public async Task A_concurrent_same_content_write_and_a_repeated_relation_are_each_held()
    {
        var first = Owner.Keep("The map is drawn in tiles.");
        var second = Owner.Keep("Tiles are sixteen pixels.");
        var ops = await ReadyAsync(
            new MemoryChange(MemoryOpKind.Write, Text: "The map is drawn in tiles."),
            new MemoryChange(MemoryOpKind.Relate, Target: first.Id, Source: second.Id, Relation: "disputes"));
        foreach (var op in ops) Assert.True(await _machine.Memory.BeginAsync(op.Id));
        Owner.Keep("The map is drawn in tiles.");
        await Engine.RelateAsync(second.Id, first.Id, "disputes");
        var before = _memory.State();

        await _machine.RestartAsync();
        await Applier.StartAsync();

        Assert.Equal((MemoryOpState.Held, MemoryOpState.Held), (await StateAsync(ops[0]), await StateAsync(ops[1])));
        Assert.Equal(before, _memory.State());
    }

    /// <summary>
    /// 🔴 A confirmation is guarded by the revision read: after a kill in <c>applying</c>, an unrelated later confirmation leaves it
    /// <c>stale</c>, and the next sweep's Holds writes a confirmation of its own, which applies.
    /// </summary>
    [Fact]
    public async Task An_unrelated_later_confirmation_is_stale_and_the_next_sweep_rechecks_it()
    {
        var entry = Owner.Keep("The map is drawn in tiles.");
        await _machine.Memory.SweepAsync([new MemoryRecheck("Owner", entry.Id, entry.Revision, Holds: true)], Now);
        var confirm = Assert.Single(await _machine.Memory.ListAsync(MemoryOpState.Ready));
        Assert.True(await _machine.Memory.BeginAsync(confirm.Id));
        var later = await Engine.ConfirmAsync(entry.Id, entry.Revision);

        await _machine.RestartAsync();
        await Applier.StartAsync();
        Assert.Equal(MemoryOpState.Stale, await StateAsync(confirm));

        await _machine.Memory.SweepAsync([new MemoryRecheck("Owner", entry.Id, later!.Revision, Holds: true)], Now.AddDays(1));
        await Applier.ApplyReadyAsync();
        var rechecked = Assert.Single(await _machine.Memory.ListAsync(MemoryOpState.Applied));
        Assert.Equal((MemoryOpKind.Confirm, later.Revision + 1), (rechecked.Change.Kind, rechecked.Applied));
    }

    /// <summary>🔴 A Holds on an entry whose end awaits its approver is kept on the end's row, and the entry is not confirmed.</summary>
    [Fact]
    public async Task A_holds_during_an_awaiting_end_is_kept_on_the_row()
    {
        var entry = Owner.Keep("The map is drawn in tiles.");
        var chat = await _machine.ChatAsync(Now);
        var end = await _machine.StageAsync(chat, End(entry.Id, entry.Revision), Now.AddMinutes(1));
        await _machine.Ledger.AdvanceAsync(chat.Id, "completed", null, null, null, Now.AddMinutes(2));

        await _machine.Memory.SweepAsync([new MemoryRecheck("Owner", entry.Id, entry.Revision, Holds: true)], Now.AddHours(1));
        await Applier.ApplyReadyAsync();

        var awaiting = await _machine.Memory.FindAsync(end.Id);
        Assert.Equal((MemoryOpState.Awaiting, Now.AddHours(1)), (awaiting!.State, awaiting.Holds));
        Assert.Empty(await _machine.Memory.ListAsync(MemoryOpState.Ready));
        Assert.Equal(entry.Revision, (await Engine.GetAsync(entry.Id))!.Revision);
    }

    /// <summary>🔴 Two ends of one entry at one revision: the first applied, the second <c>stale</c>.</summary>
    [Fact]
    public async Task Two_ends_of_one_entry_one_applied_one_stale()
    {
        var entry = Owner.Keep("The map is drawn in tiles.");
        var ends = await ReadyAsync(End(entry.Id, entry.Revision, "Vectors now."), End(entry.Id, entry.Revision, "Hexes now."));

        await Applier.ApplyReadyAsync();

        Assert.Equal((MemoryOpState.Applied, MemoryOpState.Stale), (await StateAsync(ends[0]), await StateAsync(ends[1])));
        Assert.Equal(ends[0].Id, MemoryEndToken.Of((await Engine.GetAsync(entry.Id))!.Ended));
    }
}
