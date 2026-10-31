using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWCENTRAL2c (the memory coordinator design §1, §3): an operation is staged by its session and released in the transaction that
/// records the session's commit point, the ledger's advance to <c>completed</c> for a session in the main tree and the post of a finished
/// tree's record naming a landing for one in a tree; its models are read at that point from the turn lines the driver hands.
/// </summary>
public sealed class MemoryOpsTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = MemoryMachine.Now;

    private MemoryMachine _machine = null!;

    public async Task InitializeAsync() => _machine = await MemoryMachine.OpenAsync("memory-ops");

    public async Task DisposeAsync() => await _machine.DisposeAsync();

    private static MemoryChange Write(string text) => new(MemoryOpKind.Write, Text: text);

    private static MemoryChange End(string entry, long revision) => new(MemoryOpKind.End, Text: "It no longer holds.", Target: entry, Revision: revision);

    /// <summary>
    /// 🔴 A fault inside the advance's transaction leaves the session working and its rows staged; the advance that commits moves a write to
    /// <c>ready</c> and an end to <c>awaiting</c> with it, and a kill after it leaves them so.
    /// </summary>
    [Fact]
    public async Task A_fault_inside_the_advance_leaves_neither_record_nor_move()
    {
        var chat = await _machine.ChatAsync(Now);
        var write = await _machine.StageAsync(chat, Write("The map is drawn in tiles."), Now.AddMinutes(1));
        var end = await _machine.StageAsync(chat, End("m1", 2), Now.AddMinutes(2));

        await _machine.FaultAsync();
        await Assert.ThrowsAsync<SqliteException>(() => _machine.Ledger.AdvanceAsync(chat.Id, "completed", null, null, null, Now.AddMinutes(3)));
        Assert.Equal(SessionState.Working, (await _machine.Sessions.FindAsync(chat.Id))!.State);
        Assert.Equal([MemoryOpState.Staged, MemoryOpState.Staged], await StatesAsync(write, end));

        await _machine.HealAsync();
        Assert.Equal(SessionAdvanceRefusal.None, (await _machine.Ledger.AdvanceAsync(chat.Id, "completed", null, null, null, Now.AddMinutes(4))).Refusal);
        await _machine.RestartAsync();
        Assert.Equal([MemoryOpState.Ready, MemoryOpState.Awaiting], await StatesAsync(write, end));
    }

    /// <summary>🔴 A fault inside <c>PostAsync</c>'s transaction keeps no record and moves no row; the post that commits releases the tree's rows.</summary>
    [Fact]
    public async Task A_fault_inside_the_post_leaves_neither_record_nor_move()
    {
        var session = await _machine.InTreeAsync(Now);
        var write = await _machine.StageAsync(session, Write("Tiles are sixteen pixels."), Now.AddMinutes(1));

        await _machine.FaultAsync();
        await Assert.ThrowsAsync<SqliteException>(() => _machine.Records.PostAsync(MemoryMachine.Post(landing: true), Now.AddHours(2)));
        Assert.Null(await _machine.Records.FindAsync("default", "Owner", "s-1-20261031T100000000Z"));
        Assert.Equal([MemoryOpState.Staged], await StatesAsync(write));

        await _machine.HealAsync();
        Assert.True((await _machine.Records.PostAsync(MemoryMachine.Post(landing: true), Now.AddHours(2))).Kept);
        Assert.Equal([MemoryOpState.Ready], await StatesAsync(write));
    }

    /// <summary>🔴 A post naming no landing is no commit point: its sessions' rows stay staged, and so do a stopped chat's.</summary>
    [Fact]
    public async Task A_post_naming_no_landing_leaves_rows_staged()
    {
        var session = await _machine.InTreeAsync(Now);
        var write = await _machine.StageAsync(session, Write("Tiles are sixteen pixels."), Now.AddMinutes(1));
        var chat = await _machine.ChatAsync(Now);
        var said = await _machine.StageAsync(chat, Write("The map is drawn in tiles."), Now.AddMinutes(1));

        Assert.True((await _machine.Records.PostAsync(MemoryMachine.Post(landing: false), Now.AddHours(2))).Kept);
        await _machine.Ledger.AdvanceAsync(chat.Id, "stopped", null, null, null, Now.AddMinutes(2));

        Assert.Equal([MemoryOpState.Staged, MemoryOpState.Staged], await StatesAsync(write, said));
    }

    /// <summary>🔴 A tree session's advance to <c>completed</c> is not its commit point: its rows wait for the post.</summary>
    [Fact]
    public async Task A_tree_sessions_completion_leaves_its_rows_for_the_post()
    {
        var session = await _machine.InTreeAsync(Now);
        var write = await _machine.StageAsync(session, Write("Tiles are sixteen pixels."), Now.AddMinutes(1));

        await _machine.Ledger.AdvanceAsync(session.Id, "completed", null, null, null, Now.AddMinutes(2));

        Assert.Equal([MemoryOpState.Staged], await StatesAsync(write));
    }

    /// <summary>🔴 A continuation's commit point releases the rows its stopped predecessor staged, which it carries.</summary>
    [Fact]
    public async Task A_continuation_carries_its_predecessors_staged_rows()
    {
        var first = await _machine.ChatAsync(Now);
        var carried = await _machine.StageAsync(first, Write("The map is drawn in tiles."), Now.AddMinutes(1));
        await _machine.Ledger.AdvanceAsync(first.Id, "stopped", null, null, null, Now.AddMinutes(2));
        var next = await _machine.ChatAsync(Now.AddMinutes(3), from: first.Id);

        await _machine.Ledger.AdvanceAsync(next.Id, "completed", null, null, null, Now.AddMinutes(4));

        Assert.Equal([MemoryOpState.Ready], await StatesAsync(carried));
    }

    private static readonly DateTimeOffset T0 = Now.AddMinutes(10);

    private static MemoryTurn Turn(long seq, int started, int ended, string model, bool unstarted = false) =>
        new(seq, unstarted ? null : T0.AddMinutes(started), T0.AddMinutes(ended), model);

    /// <summary>🔴 A row's models are those of the turn whose start and end hold its instant.</summary>
    [Fact]
    public async Task One_turn_names_its_model() =>
        Assert.Equal("claude-opus-4-5", await ModelsOfAsync(5, new([Turn(1, 0, 10, "claude-opus-4-5")], [])));

    /// <summary>🔴 Turns A, B, A: a row of the third names A.</summary>
    [Fact]
    public async Task A_row_of_the_third_of_turns_a_b_a_names_a() =>
        Assert.Equal("model-a", await ModelsOfAsync(25, new([Turn(1, 0, 10, "model-a"), Turn(2, 10, 20, "model-b"), Turn(3, 20, 30, "model-a")], [])));

    /// <summary>
    /// 🔴 A chat stopped mid-turn and continued: the stopped turn kept no line, the continued one names no start, and the person's ask between
    /// the row and that turn says the row was not the continued turn's, so it takes no later turn's model.
    /// </summary>
    [Fact]
    public async Task A_chat_stopped_mid_turn_and_continued_names_unknown() =>
        Assert.Equal(MemoryModels.Unknown, await ModelsOfAsync(3, new([Turn(1, 0, 15, "model-b", unstarted: true)], [T0.AddMinutes(8)])));

    /// <summary>🔴 A turn's line pruned from the history leaves its rows <c>unknown</c>, never the next turn's model.</summary>
    [Fact]
    public async Task A_pruned_line_names_unknown() =>
        Assert.Equal(MemoryModels.Unknown, await ModelsOfAsync(5, new([Turn(2, 12, 20, "model-b")], [])));

    /// <summary>🔴 A restart before the commit point keeps the staged rows and their instants, so the commit point still names their models.</summary>
    [Fact]
    public async Task A_restart_before_the_commit_point_keeps_them()
    {
        var chat = await _machine.ChatAsync(Now);
        var write = await _machine.StageAsync(chat, Write("The map is drawn in tiles."), T0.AddMinutes(5));

        await _machine.RestartAsync();
        await _machine.Ledger.AdvanceAsync(
            chat.Id, "completed", null, null, null, T0.AddMinutes(20), turns: new Dictionary<string, MemoryTurns> { [chat.Id] = new([Turn(1, 0, 10, "model-a")], []) });

        var kept = await _machine.Memory.FindAsync(write.Id);
        Assert.Equal((MemoryOpState.Ready, "model-a"), (kept!.State, kept.Models));
        Assert.Equal((chat.Id, "builder", "r3", "2.1.0", T0.AddMinutes(5)), (kept.By!.Session, kept.By.Role, kept.By.RoleRevision, kept.By.Harness, kept.By.At));
    }

    private async Task<string?> ModelsOfAsync(int minute, MemoryTurns turns)
    {
        var chat = await _machine.ChatAsync(Now);
        var write = await _machine.StageAsync(chat, Write("The map is drawn in tiles."), T0.AddMinutes(minute));
        await _machine.Ledger.AdvanceAsync(chat.Id, "completed", null, null, null, T0.AddMinutes(40), turns: new Dictionary<string, MemoryTurns> { [chat.Id] = turns });
        return (await _machine.Memory.FindAsync(write.Id))!.Models;
    }

    private async Task<string[]> StatesAsync(params MemoryOp[] ops)
    {
        var states = new List<string>();
        foreach (var op in ops) states.Add((await _machine.Memory.FindAsync(op.Id))!.State);
        return [.. states];
    }
}
