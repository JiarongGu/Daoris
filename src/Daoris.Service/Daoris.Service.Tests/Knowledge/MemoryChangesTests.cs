using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWCENTRAL2c (the memory coordinator design §3): a trigger journals every move of a memory row into <c>event_journal</c> as ids and
/// codes; a reader holds a cursor <c>(store, position)</c> that re-validates across stores, below the floor and across a change of scope,
/// taken by a bootstrap that reads the rows and the newest position in one transaction.
/// </summary>
public sealed class MemoryChangesTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = MemoryMachine.Now;

    private readonly StandInMemory _memory = new();

    private MemoryMachine _machine = null!;

    public async Task InitializeAsync() => _machine = await MemoryMachine.OpenAsync("memory-changes");

    public async Task DisposeAsync() => await _machine.DisposeAsync();

    private static MemoryRecheck Holds(string entry, long revision = 1) => new("Owner", entry, revision, Holds: true);

    /// <summary>🔴 Each move of a row is journalled once, in the order it was made, naming ids and codes and never the text.</summary>
    [Fact]
    public async Task Each_move_is_journalled_once_in_order_with_no_text()
    {
        _memory.Engine("Owner");
        var chat = await _machine.ChatAsync(Now);
        var write = await _machine.StageAsync(chat, new(MemoryOpKind.Write, Text: "The pelican nests in tiles."), Now.AddMinutes(1));
        await _machine.Ledger.AdvanceAsync(chat.Id, "completed", null, null, null, Now.AddMinutes(2));
        await new MemoryApplier(_machine.Memory, _memory).ApplyReadyAsync();

        var page = await JournalRows.ReadAsync(_machine.Connection, 0, JournalRows.Most);
        var rows = page.Rows.Where(row => row.Record == write.Id).ToList();

        Assert.Equal(
            [(null, "staged"), ("staged", "ready"), ("ready", "applying"), ("applying", "applied")],
            rows.Select(row => (row.StateOld, row.StateNew)));
        Assert.All(rows, row => Assert.Equal(("memory", "memory", MemoryOpKind.Write, chat.Id), (row.Family, row.Change, row.Op, row.FromSession)));
        Assert.DoesNotContain(rows, row => row.ToString().Contains("pelican", StringComparison.Ordinal));
    }

    /// <summary>🔴 A cursor re-validates below the journal's floor, from another store, and across a change of scope; otherwise it names what moved.</summary>
    [Fact]
    public async Task Cursors_revalidate_below_the_floor_or_across_stores()
    {
        var boot = await _machine.Memory.BootstrapAsync(["Owner"]);
        await _machine.Memory.SweepAsync([Holds("m1")], Now);

        var changes = await _machine.Memory.ChangesAsync(boot.Cursor, ["Owner"]);
        Assert.False(changes.Revalidate);
        Assert.Equal([new MemoryReference("Owner", "m1")], changes.Changed);

        await _machine.ExecuteAsync($"DELETE FROM event_journal WHERE position <= {boot.Cursor.Position + 1}");
        Assert.True((await _machine.Memory.ChangesAsync(boot.Cursor, ["Owner"])).Revalidate);
        Assert.False((await _machine.Memory.ChangesAsync(changes.Cursor, ["Owner"])).Revalidate);

        await using var elsewhere = await MemoryMachine.OpenAsync("memory-changes-elsewhere");
        await elsewhere.Memory.SweepAsync([Holds("m1"), Holds("m2"), Holds("m3")], Now);
        Assert.True((await elsewhere.Memory.ChangesAsync(changes.Cursor, ["Owner"])).Revalidate);
        Assert.True((await _machine.Memory.ChangesAsync(changes.Cursor, ["Owner", "Asker"])).Revalidate);
    }

    /// <summary>
    /// 🔴 The read set: a session is told once that an entry it read moved, and a reference trimmed from its bounded set is never told.
    /// </summary>
    [Fact]
    public async Task A_trimmed_reference_is_never_told()
    {
        var entry = _memory.Engine("Owner").Keep("The map is drawn in tiles.");
        var read = new MemoryRead(new("Owner", entry.Id), entry.Revision);
        await _machine.Memory.KeepReadsAsync("reader", [read]);
        await _machine.Memory.KeepReadsAsync("trimmed", [read]);
        await _machine.Memory.KeepReadsAsync("trimmed", [.. Enumerable.Range(0, MemoryOpStore.ReadsPerSession).Select(i => new MemoryRead(new("Owner", $"x{i}"), 1))]);

        await _machine.Memory.SweepAsync([Holds(entry.Id, entry.Revision)], Now);
        await new MemoryApplier(_machine.Memory, _memory).ApplyReadyAsync();

        Assert.Equal([new MemoryReference("Owner", entry.Id)], await _machine.Memory.TellAsync("reader"));
        Assert.Empty(await _machine.Memory.TellAsync("reader"));
        Assert.Empty(await _machine.Memory.TellAsync("trimmed"));
    }

    /// <summary>
    /// 🔴 A bootstrap misses no row another connection commits: its rows and its newest position are read in one transaction, so a row
    /// committed between the two reads would be in neither, and a second connection cannot commit inside it.
    /// </summary>
    [Fact]
    public async Task A_bootstrap_misses_no_row_a_second_connection_commits()
    {
        await using var second = await _machine.SecondAsync(timeoutSeconds: 1);
        var other = await MemoryOpStore.OpenAsync(second);
        var committed = false;
        _machine.Memory.BetweenReads = async () =>
        {
            try
            {
                await other.SweepAsync([Holds("m1")], Now);
                committed = true;
            }
            catch (SqliteException)
            {
                // The bootstrap's read holds the file; the row is written after it.
            }
        };

        var boot = await _machine.Memory.BootstrapAsync(["Owner"]);
        _machine.Memory.BetweenReads = null;
        if (!committed) await other.SweepAsync([Holds("m1")], Now);
        var changes = await _machine.Memory.ChangesAsync(boot.Cursor, ["Owner"]);

        Assert.Contains("m1", boot.Unsettled.Select(op => op.Change.Target).Concat(changes.Changed.Select(reference => reference.Entry)));
    }
}
