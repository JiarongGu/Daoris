using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWCENTRAL2d (the memory coordinator design §1, §3, §4): what the person decides through a row. An end awaits its approver across a
/// restart and is decided through its row; its proposer, and any session of the proposer's lineage, is refused; a stopped session's
/// staged rows wait for the person's Keep or Drop; and a reader never sees the row's token in an end's reason.
/// </summary>
public sealed class MemoryDecisionTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = MemoryMachine.Now;

    private MemoryMachine _machine = null!;
    private LyntaiFile _lyntai = null!;

    public async Task InitializeAsync()
    {
        _machine = await MemoryMachine.OpenAsync("memory-decisions");
        _lyntai = LyntaiFile.Open("memory-decisions");
    }

    public async Task DisposeAsync()
    {
        await _lyntai.DisposeAsync();
        await _machine.DisposeAsync();
    }

    private IMemoryEngine Owner => _lyntai.Engine("Owner");

    private MemoryApplier Applier => new(_machine.Memory, _lyntai.Memory);

    private MemoryDesk Desk => new(_machine.Memory);

    private MemoryTools Tools(Session session) => new(
        _machine.Registry, new AmbientWorkspace(Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures")), new IntakeScope(null, session.Id),
        _machine.Ledger, _lyntai.Memory);

    /// <summary>A chat that proposes the end of <paramref name="entry"/> and completes, its end then awaiting the person.</summary>
    private async Task<(Session Chat, MemoryOp End)> AwaitingAsync(MemoryEntry entry)
    {
        var chat = await _machine.ChatAsync(Now);
        await Tools(chat).EndAsync($"Owner:{entry.Id}", entry.Revision, "The map is drawn in vectors now.");
        await _machine.Ledger.AdvanceAsync(chat.Id, "completed", null, null, null, Now.AddMinutes(2));
        var end = Assert.Single(await _machine.Memory.OfSessionAsync(chat.Id));
        Assert.Equal(MemoryOpState.Awaiting, end.State);
        return (chat, end);
    }

    /// <summary>🔴 An end awaits across a restart, untouched by the applier's start, and the person's approval through its row applies it.</summary>
    [Fact]
    public async Task An_end_awaits_across_a_restart_and_is_decided_through_its_row()
    {
        var map = await Owner.RememberAsync("The map is drawn in tiles.");
        var (_, end) = await AwaitingAsync(map);

        await _machine.RestartAsync();
        await Applier.StartAsync();
        Assert.Equal(MemoryOpState.Awaiting, (await _machine.Memory.FindAsync(end.Id))!.State);
        Assert.Null((await Owner.GetAsync(map.Id))!.Ended);

        var decided = await Desk.DecideAsync(end.Id, session: null, approve: true, Now.AddMinutes(5));
        await Applier.ApplyReadyAsync();

        Assert.Equal((MemoryDecisionRefusal.None, MemoryDesk.Person), (decided.Refusal, decided.Op!.Approver));
        Assert.Equal(MemoryOpState.Applied, (await _machine.Memory.FindAsync(end.Id))!.State);
        Assert.NotNull((await Owner.GetAsync(map.Id))!.Ended);
    }

    /// <summary>
    /// 🔴 The proposer approving its own end is refused, and so is its subagent (which calls as the proposer), a session continuing it
    /// and its helper: none is the person. The end still awaits.
    /// </summary>
    [Fact]
    public async Task A_proposer_or_its_lineage_approving_its_own_end_is_refused()
    {
        var map = await Owner.RememberAsync("The map is drawn in tiles.");
        var (chat, end) = await AwaitingAsync(map);
        var continuation = await _machine.ChatAsync(Now.AddMinutes(3), from: chat.Id);
        var helper = await _machine.Sessions.CreateAsync(null, "Owner", "stub", Now.AddMinutes(3), kind: SessionKind.Chat, serves: continuation.Id);

        foreach (var approver in new[] { chat.Id, continuation.Id, helper.Id })
        {
            var refused = await Desk.DecideAsync(end.Id, approver, approve: true, Now.AddMinutes(4));
            Assert.Equal(MemoryDecisionRefusal.Own, refused.Refusal);
            Assert.Contains("its own end", refused.Message);
        }

        Assert.Equal(MemoryOpState.Awaiting, (await _machine.Memory.FindAsync(end.Id))!.State);
    }

    /// <summary>
    /// 🔴 A chat's addition is released when it completes; a stopped chat's stays staged, applied by nothing, until the person keeps it
    /// (then it is applied) or drops it (then it never is).
    /// </summary>
    [Fact]
    public async Task A_chats_addition_is_released_at_completed_and_kept_after_a_stop_until_the_person_decides()
    {
        var done = await _machine.ChatAsync(Now);
        await Tools(done).WriteAsync("Tiles are sixteen pixels.");
        await _machine.Ledger.AdvanceAsync(done.Id, "completed", null, null, null, Now.AddMinutes(1));
        var kept = await _machine.ChatAsync(Now.AddMinutes(2));
        await Tools(kept).WriteAsync("The legend sits under the map.");
        await _machine.Ledger.AdvanceAsync(kept.Id, "stopped", null, null, null, Now.AddMinutes(3));
        var dropped = await _machine.ChatAsync(Now.AddMinutes(4));
        await Tools(dropped).WriteAsync("The map is drawn in vectors.");
        await _machine.Ledger.AdvanceAsync(dropped.Id, "stopped", null, null, null, Now.AddMinutes(5));

        Assert.Equal(1, await Applier.ApplyReadyAsync());
        Assert.Equal(MemoryOpState.Staged, Assert.Single(await _machine.Memory.OfSessionAsync(kept.Id)).State);

        Assert.Equal(1, (await Desk.KeepAsync(kept.Id, keep: true, Now.AddMinutes(6))).Moved);
        Assert.Equal(1, (await Desk.KeepAsync(dropped.Id, keep: false, Now.AddMinutes(6))).Moved);
        Assert.Equal(1, await Applier.ApplyReadyAsync());

        Assert.Equal(MemoryOpState.Applied, Assert.Single(await _machine.Memory.OfSessionAsync(kept.Id)).State);
        Assert.Equal(MemoryOpState.Dropped, Assert.Single(await _machine.Memory.OfSessionAsync(dropped.Id)).State);
        Assert.Equal(2, await _lyntai.CountAsync("Owner"));
    }

    /// <summary>
    /// 🔴 What waits on the person, by session, oldest first: an end awaiting, a replacement awaiting, and a stopped session's staged rows; a
    /// running session's staged rows, a kept or dropped row and a decided end wait on nothing of theirs.
    /// </summary>
    [Fact]
    public async Task The_waits_are_the_sessions_with_an_end_a_replacement_or_stopped_rows_waiting()
    {
        var map = await Owner.RememberAsync("The map is drawn in tiles.");
        var pixels = await Owner.RememberAsync("Tiles are sixteen pixels.");
        var (ender, _) = await AwaitingAsync(map);
        var replacer = await _machine.ChatAsync(Now.AddMinutes(10));
        await Tools(replacer).WriteAsync("Tiles are drawn at thirty-two pixels.", supersedes: [$"Owner:{pixels.Id}@{pixels.Revision}"]);
        await _machine.Ledger.AdvanceAsync(replacer.Id, "completed", null, null, null, Now.AddMinutes(11));
        var stopped = await _machine.ChatAsync(Now.AddMinutes(12));
        await Tools(stopped).WriteAsync("The legend sits under the map.");
        await Tools(stopped).WriteAsync("The legend names the tiles.");
        await _machine.Ledger.AdvanceAsync(stopped.Id, "stopped", null, null, null, Now.AddMinutes(13));
        var running = await _machine.ChatAsync(Now.AddMinutes(14));
        await Tools(running).WriteAsync("A note still being written.");

        var waits = await _machine.Memory.WaitsAsync();

        Assert.Equal(
            [(ender.Id, 1, 0, 0), (replacer.Id, 0, 1, 0), (stopped.Id, 0, 0, 2)],
            waits.Select(wait => (wait.Session, wait.Ends, wait.Replacements, wait.Staged)));
        Assert.All(waits, wait => Assert.True(wait.Since > default(DateTimeOffset)));

        await Desk.KeepAsync(stopped.Id, keep: false, Now.AddMinutes(15));
        var end = Assert.Single(await _machine.Memory.OfSessionAsync(ender.Id));
        await Desk.DecideAsync(end.Id, session: null, approve: false, Now.AddMinutes(15));

        Assert.Equal([replacer.Id], (await _machine.Memory.WaitsAsync()).Select(wait => wait.Session));
    }

    /// <summary>
    /// 🔴 KNOWCENTRAL2e: while an end awaits, a hit on its entry says who proposed it and that it awaits the person, in a search and a read;
    /// once decided, the hit says nothing of it.
    /// </summary>
    [Fact]
    public async Task A_hit_says_an_end_awaits_its_approver()
    {
        var map = await Owner.RememberAsync("The map is drawn in tiles.");
        var (chat, end) = await AwaitingAsync(map);
        var reader = Tools(await _machine.ChatAsync(Now.AddMinutes(3)));
        var says = $"end proposed by session `{chat.Id}`, awaiting the person";

        Assert.Contains(says, await reader.SearchAsync("map"));
        Assert.Contains(says, await reader.GetAsync($"Owner:{map.Id}"));

        await Desk.DecideAsync(end.Id, session: null, approve: false, Now.AddMinutes(4));
        Assert.DoesNotContain("end proposed", await reader.SearchAsync("map"));
    }

    /// <summary>🔴 Keep or Drop waits for the session to end: a running session's staged rows are its own still.</summary>
    [Fact]
    public async Task Keep_or_drop_of_a_running_session_is_refused()
    {
        var chat = await _machine.ChatAsync(Now);
        await Tools(chat).WriteAsync("Tiles are sixteen pixels.");

        var refused = await Desk.KeepAsync(chat.Id, keep: true, Now.AddMinutes(1));

        Assert.Equal((MemoryDecisionRefusal.Running, 0), (refused.Refusal, refused.Moved));
        Assert.Equal(MemoryOpState.Staged, Assert.Single(await _machine.Memory.OfSessionAsync(chat.Id)).State);
    }

    /// <summary>🔴 The token an applied end's reason carries for recovery is never shown: a reader sees the reason as its proposer wrote it.</summary>
    [Fact]
    public async Task No_token_reaches_a_reader()
    {
        var map = await Owner.RememberAsync("The map is drawn in tiles.");
        var (chat, end) = await AwaitingAsync(map);
        await Desk.DecideAsync(end.Id, session: null, approve: true, Now.AddMinutes(5));
        await Applier.ApplyReadyAsync();
        Assert.Contains("[memory-op", (await Owner.GetAsync(map.Id))!.Ended);

        var read = await Tools(chat).GetAsync($"Owner:{map.Id}");
        var page = await Desk.PageAsync(chat.Id);

        Assert.Contains("ended: The map is drawn in vectors now.", read);
        Assert.DoesNotContain("memory-op", read);
        Assert.Equal("The map is drawn in vectors now.", Assert.Single(page).Change.Text);
    }
}
