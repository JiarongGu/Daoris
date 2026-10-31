using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWCENTRAL2f (the memory coordinator design §3, §6's presentation): memory is current when it is presented. A session's reads fill its
/// read set and it is told once that an entry it read moved, a replaced one included; what a line read is rechecked before its brief shows
/// it, an invalidation before delivery discards the answer, and an inconclusive recheck is never stamped.
/// </summary>
public sealed class MemoryCurrencyTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = MemoryMachine.Now;

    private readonly StandInMemory _memory = new();

    private MemoryMachine _machine = null!;

    public async Task InitializeAsync() => _machine = await MemoryMachine.OpenAsync("memory-currency");

    public async Task DisposeAsync() => await _machine.DisposeAsync();

    private MemoryTools Tools(Session session) => new(
        _machine.Registry, new AmbientWorkspace(Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures")),
        new IntakeScope(null, session.Id) { Role = "builder", RoleRevision = "3" }, _machine.Ledger, _memory);

    private MemoryPresenter Presenter => new(_machine.Memory, _memory, _machine.Registry);

    /// <summary>The entry ended as the person's approval of a failed recheck ends one.</summary>
    private async Task EndAsync(MemoryEntry entry)
    {
        await _machine.Memory.SweepAsync([new MemoryRecheck("Owner", entry.Id, entry.Revision, Holds: false, "The map moved to vectors.")], Now);
        var end = Assert.Single(await _machine.Memory.ListAsync(MemoryOpState.Awaiting), op => op.Change.Target == entry.Id);
        Assert.NotNull(await _machine.Memory.DecideAsync(end.Id, MemoryDesk.Person, approve: true, Now));
        await new MemoryApplier(_machine.Memory, _memory).ApplyReadyAsync();
    }

    /// <summary>
    /// 🔴 An entry a session read, ended before a hand-over's brief is composed, is shown ended to the part that continues it: the line's
    /// reads are rechecked, never shown as they were read.
    /// </summary>
    [Fact]
    public async Task An_entry_ended_between_a_read_and_a_hand_over_s_brief_is_shown_ended()
    {
        var tiles = _memory.Engine("Owner").Keep("The map is drawn in tiles.");
        var first = await _machine.InTreeAsync(Now);
        Assert.Contains("revision 1", await Tools(first).GetAsync($"Owner:{tiles.Id}"));
        await EndAsync(tiles);
        var next = await _machine.Sessions.CreateAsync(first.Quest, "Owner", "stub", Now.AddMinutes(5), tree: MemoryMachine.Tree, from: first.Id);

        var shown = Assert.Single(await Presenter.PresentAsync(await _machine.Memory.ReadsOfLineAsync(next.Id)));

        Assert.Equal((new MemoryReference("Owner", tiles.Id), MemoryStanding.Ended, (long?)null), (shown.Reference, shown.Standing, shown.Checked));
        Assert.Contains("The map moved to vectors.", shown.Line);
        Assert.DoesNotContain("memory-op", shown.Line);
    }

    /// <summary>
    /// 🔴 An entry that moves after it was rechecked and before the answer leaves discards that answer: the one delivered is read again and
    /// shows the end, never the current entry the first reading found.
    /// </summary>
    [Fact]
    public async Task An_invalidation_before_delivery_discards_the_answer()
    {
        var tiles = _memory.Engine("Owner").Keep("The map is drawn in tiles.");
        var presenter = Presenter;
        var ended = false;
        presenter.BeforeDelivery = async () =>
        {
            if (ended) return;
            ended = true;
            await EndAsync(tiles);
        };

        var shown = Assert.Single(await presenter.PresentAsync([("reader", new MemoryRead(new("Owner", tiles.Id), tiles.Revision))]));

        Assert.True(ended);
        Assert.Equal(MemoryStanding.Ended, shown.Standing);
    }

    /// <summary>🔴 A session that read an entry is told once, at its next memory call, that the entry moved; the call after tells nothing.</summary>
    [Fact]
    public async Task A_reader_is_told_once()
    {
        var tiles = _memory.Engine("Owner").Keep("The map is drawn in tiles.");
        var reader = await _machine.InTreeAsync(Now);
        Assert.Contains(tiles.Id, await Tools(reader).SearchAsync("map"));
        await EndAsync(tiles);

        var told = await Tools(reader).SearchAsync("legend");
        var again = await Tools(reader).SearchAsync("legend");

        Assert.Contains($"`Owner:{tiles.Id}`", told);
        Assert.Contains("moved", told);
        Assert.DoesNotContain($"Owner:{tiles.Id}", again);
    }

    /// <summary>
    /// 🔴 An entry a replacement ended is told moved to the session that read it, once, and the journal's changes name it beside its successor.
    /// </summary>
    [Fact]
    public async Task A_replaced_entry_is_told_moved_once_and_named_by_the_journal()
    {
        await using var lyntai = LyntaiFile.Open("memory-currency");
        var tiles = await lyntai.Engine("Owner").RememberAsync("The map is drawn in tiles.");
        await _machine.Memory.KeepReadsAsync("reader", [new MemoryRead(new("Owner", tiles.Id), tiles.Revision)]);
        var boot = await _machine.Memory.BootstrapAsync(["Owner"]);
        var chat = await _machine.ChatAsync(Now);
        await _machine.StageAsync(
            chat, new MemoryChange(MemoryOpKind.Supersede, Text: "The map is drawn in vectors.", Predecessors: [new(tiles.Id, tiles.Revision)]), Now);
        await _machine.Ledger.AdvanceAsync(chat.Id, "completed", null, null, null, Now.AddMinutes(2));
        var op = Assert.Single(await _machine.Memory.OfSessionAsync(chat.Id));
        await _machine.Memory.DecideAsync(op.Id, MemoryDesk.Person, approve: true, Now.AddMinutes(3));
        await new MemoryApplier(_machine.Memory, lyntai.Memory).ApplyReadyAsync();
        Assert.Equal(MemoryOpState.Applied, (await _machine.Memory.FindAsync(op.Id))!.State);

        Assert.Contains(new MemoryReference("Owner", tiles.Id), (await _machine.Memory.ChangesAsync(boot.Cursor, ["Owner"])).Changed);
        Assert.Equal([new MemoryReference("Owner", tiles.Id)], await _machine.Memory.TellAsync("reader"));
        Assert.Empty(await _machine.Memory.TellAsync("reader"));
    }

    /// <summary>
    /// 🔴 An entry whose cited record cannot be checked now is shown as read, never stamped current; an entry citing nothing, read at the
    /// revision it holds, is stamped checked at that revision.
    /// </summary>
    [Fact]
    public async Task An_inconclusive_recheck_is_never_stamped()
    {
        var engine = _memory.Engine("Owner");
        var plain = engine.Keep("The legend sits under the map.");
        var chat = await _machine.ChatAsync(Now);
        var gone = new Citation("Owner:docs/gone.md#tiles", null, "abc1234", Fed: false, Digest: "d0");
        await _machine.StageAsync(chat, new MemoryChange(MemoryOpKind.Write, Text: "The map is drawn in tiles.", Recheck: MemoryCitations.Of([gone])), Now);
        await _machine.Ledger.AdvanceAsync(chat.Id, "completed", null, null, null, Now.AddMinutes(2));
        await new MemoryApplier(_machine.Memory, _memory).ApplyReadyAsync();
        var tiles = Assert.Single(await _memory.Open("Owner", MemoryReinforcement.None)!.SearchAsync("tiles", 10));

        var shown = await Presenter.PresentAsync(
            [("reader", new MemoryRead(new("Owner", tiles.Id), tiles.Revision)), ("reader", new MemoryRead(new("Owner", plain.Id), plain.Revision))]);

        Assert.Equal((MemoryStanding.Historical, (long?)null), (shown[0].Standing, shown[0].Checked));
        Assert.DoesNotContain("checked at", shown[0].Line);
        Assert.Contains("docs/gone.md", shown[0].Line);
        Assert.Equal((MemoryStanding.Current, (long?)plain.Revision), (shown[1].Standing, shown[1].Checked));
        Assert.Contains($"checked at revision {plain.Revision}", shown[1].Line);
    }
}
