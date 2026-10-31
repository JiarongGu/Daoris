using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWCENTRAL2h (the memory coordinator design §4, §6 A, over Lyntai's MC12): a write that replaces entries is held on its row, awaits the
/// person as an end does, and applies as one change under its row's key, so no read sees the old and the new both current.
/// </summary>
public sealed class MemorySupersessionTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = MemoryMachine.Now;

    private const string Vectors = "The map is drawn in vectors.";

    private MemoryMachine _machine = null!;
    private LyntaiFile _lyntai = null!;

    public async Task InitializeAsync()
    {
        _machine = await MemoryMachine.OpenAsync("memory-supersession");
        _lyntai = LyntaiFile.Open("memory-supersession");
    }

    public async Task DisposeAsync()
    {
        await _lyntai.DisposeAsync();
        await _machine.DisposeAsync();
    }

    private IMemoryEngine Owner => _lyntai.Engine("Owner");

    private MemoryApplier Applier => new(_machine.Memory, _lyntai.Memory);

    private MemoryTools Tools(Session session) => new(
        _machine.Registry, new AmbientWorkspace(Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures")),
        new IntakeScope(null, session.Id) { Role = "builder", RoleRevision = "3" }, _machine.Ledger, _lyntai.Memory);

    /// <summary>A chat's replacement staged through its connector, released at its completion: awaiting the person.</summary>
    private async Task<MemoryOp> AwaitingAsync(string text, params MemoryEntry[] replaced)
    {
        var chat = await _machine.ChatAsync(Now);
        Assert.Contains("staged", await Tools(chat).WriteAsync(text, supersedes: [.. replaced.Select(entry => $"Owner:{entry.Id}@{entry.Revision}")]));
        await _machine.Ledger.AdvanceAsync(chat.Id, "completed", null, null, null, Now.AddMinutes(2));
        var op = Assert.Single(await _machine.Memory.OfSessionAsync(chat.Id));
        Assert.Equal((MemoryOpKind.Supersede, MemoryOpState.Awaiting), (op.Change.Kind, op.State));
        return op;
    }

    /// <summary>The same, approved by the person: <c>ready</c>.</summary>
    private async Task<MemoryOp> ApprovedAsync(string text, params MemoryEntry[] replaced)
    {
        var op = await AwaitingAsync(text, replaced);
        Assert.Equal(MemoryDecisionRefusal.None, (await new MemoryDesk(_machine.Memory).DecideAsync(op.Id, null, approve: true, Now.AddMinutes(3))).Refusal);
        return op;
    }

    /// <summary>The decide answer says a replacement, never an end, whether approved or declined.</summary>
    [Fact]
    public async Task The_decide_answer_for_a_replacement_says_replacement()
    {
        var tiles = await Owner.RememberAsync("The map is drawn in tiles.");
        var approved = await AwaitingAsync(Vectors, tiles);
        var declined = await AwaitingAsync("The map is drawn in hexes.", tiles);

        var yes = await new MemoryDesk(_machine.Memory).DecideAsync(approved.Id, null, approve: true, Now.AddMinutes(3));
        var no = await new MemoryDesk(_machine.Memory).DecideAsync(declined.Id, null, approve: false, Now.AddMinutes(3));

        Assert.Equal(($"The replacement `{approved.Id}` is approved and applies next.", $"The replacement `{declined.Id}` is declined."), (yes.Message, no.Message));
    }

    /// <summary>The successor <paramref name="id"/> names as it ended, read from the store.</summary>
    private async Task<long?> SuccessorOfAsync(string id) => (await _lyntai.Memory.Store.GetAsync("Owner", long.Parse(id)))!.SupersededBy;

    /// <summary>
    /// 🔴 A replacement killed inside the engine's call, its successor stored and one entry ended but the other not yet, leaves nothing; the
    /// next start finds no receipt for its key and applies it whole: the successor current, each entry ended by it.
    /// </summary>
    [Fact]
    public async Task A_replacement_killed_between_its_parts_leaves_all_or_nothing()
    {
        var tiles = await Owner.RememberAsync("The map is drawn in tiles.");
        var pixels = await Owner.RememberAsync("Tiles are sixteen pixels.");
        var op = await ApprovedAsync(Vectors, tiles, pixels);
        var before = await _lyntai.StateAsync("Owner", tiles.Id, pixels.Id);
        Assert.True(await _machine.Memory.BeginAsync(op.Id));

        await LyntaiAsync($"""
            CREATE TRIGGER daoris_killed BEFORE UPDATE OF ended_reason ON lyntai_memory_node
            WHEN NEW.ended_reason IS NOT NULL AND OLD.id = {pixels.Id}
            BEGIN SELECT RAISE(ABORT, 'killed between the parts'); END;
            """);
        await Assert.ThrowsAnyAsync<Exception>(() => Owner.SupersedeAsync(
            Vectors, [new(tiles.Id, tiles.Revision), new(pixels.Id, pixels.Revision)], MemoryApplier.Replaced(op.Id), key: op.Id));
        await LyntaiAsync("DROP TRIGGER daoris_killed;");

        Assert.Equal(before, await _lyntai.StateAsync("Owner", tiles.Id, pixels.Id));
        Assert.Empty(await Owner.SearchAsync("vectors", 10));

        await _machine.RestartAsync();
        await Applier.StartAsync();

        var settled = (await _machine.Memory.FindAsync(op.Id))!;
        Assert.Equal(MemoryOpState.Applied, settled.State);
        Assert.Equal(Vectors, Assert.Single(await Owner.SearchAsync("vectors", 10)).Text);
        long? successor = long.Parse(settled.Entry!);
        Assert.Equal(new[] { successor, successor }, new[] { await SuccessorOfAsync(tiles.Id), await SuccessorOfAsync(pixels.Id) });
    }

    /// <summary>
    /// 🔴 No read sees a replaced entry and its successor both current: not while the replacement is staged, awaits or is approved, not after
    /// any call the service makes to the engine, and not once it applied. Its proposer alone finds the successor staged before.
    /// </summary>
    [Fact]
    public async Task No_read_sees_both_current()
    {
        var tiles = await Owner.RememberAsync("The map is drawn in tiles.");
        var reader = await _machine.InTreeAsync(Now);
        var seen = new List<string>();
        async Task LookAsync(string when)
        {
            var found = await Tools(reader).SearchAsync("map");
            seen.Add($"{when}:{(found.Contains("tiles") ? " tiles" : "")}{(found.Contains("vectors") ? " vectors" : "")}");
        }

        await LookAsync("before");
        var chat = await _machine.ChatAsync(Now);
        await Tools(chat).WriteAsync(Vectors, supersedes: [$"Owner:{tiles.Id}@{tiles.Revision}"]);
        Assert.Contains("staged for `Owner` by this session's work", await Tools(chat).SearchAsync("vectors"));
        await LookAsync("staged");
        await _machine.Ledger.AdvanceAsync(chat.Id, "completed", null, null, null, Now.AddMinutes(2));
        await LookAsync("awaiting");
        var op = Assert.Single(await _machine.Memory.OfSessionAsync(chat.Id));
        await new MemoryDesk(_machine.Memory).DecideAsync(op.Id, null, approve: true, Now.AddMinutes(3));
        await LookAsync("approved");

        await new MemoryApplier(_machine.Memory, new Watched(_lyntai.Memory, () => LookAsync("called"))).ApplyReadyAsync();
        await LookAsync("applied");

        Assert.Equal(
            ["before: tiles", "staged: tiles", "awaiting: tiles", "approved: tiles", "called: vectors", "applied: vectors"], seen);
    }

    /// <summary>
    /// 🔴 A replacement whose answer was lost after the engine applied it: the next start's repeat by its key answers <c>applied</c>, naming the
    /// successor, the key on each entry it ended; repeated again by the key, it changes nothing.
    /// </summary>
    [Fact]
    public async Task A_repeat_by_key_is_applied_naming_the_successor_with_the_key_on_each_predecessor()
    {
        var tiles = await Owner.RememberAsync("The map is drawn in tiles.");
        var pixels = await Owner.RememberAsync("Tiles are sixteen pixels.");
        var op = await ApprovedAsync(Vectors, tiles, pixels);
        var killed = new Watched(_lyntai.Memory, () => throw new OperationCanceledException("killed after the engine answered"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MemoryApplier(_machine.Memory, killed).ApplyReadyAsync());
        Assert.Equal(MemoryOpState.Applying, (await _machine.Memory.FindAsync(op.Id))!.State);

        await _machine.RestartAsync();
        await Applier.StartAsync();

        var settled = (await _machine.Memory.FindAsync(op.Id))!;
        var successor = Assert.Single(await Owner.SearchAsync("vectors", 10));
        Assert.Equal((MemoryOpState.Applied, successor.Id, successor.Revision), (settled.State, settled.Entry, settled.Applied));
        foreach (var replaced in new[] { tiles, pixels })
        {
            var ended = (await Owner.GetAsync(replaced.Id))!.Ended;
            Assert.Equal(op.Id, MemoryEndToken.Of(ended));
            Assert.StartsWith($"superseded by {successor.Id}", ended);
            Assert.Equal(long.Parse(successor.Id), await SuccessorOfAsync(replaced.Id));
        }

        var state = await _lyntai.StateAsync("Owner", tiles.Id, pixels.Id, successor.Id);
        var repeat = await Owner.SupersedeAsync(
            Vectors, [new(tiles.Id, tiles.Revision), new(pixels.Id, pixels.Revision)], MemoryApplier.Replaced(op.Id), key: op.Id);
        Assert.Equal((true, successor.Id), (repeat.Applied, repeat.Successor?.Id));
        Assert.Equal(state, await _lyntai.StateAsync("Owner", tiles.Id, pixels.Id, successor.Id));
    }

    /// <summary>🔴 A replacement one of whose entries moved past the revision read settles <c>stale</c> and writes nothing.</summary>
    [Fact]
    public async Task A_mismatch_writes_nothing()
    {
        var tiles = await Owner.RememberAsync("The map is drawn in tiles.");
        var pixels = await Owner.RememberAsync("Tiles are sixteen pixels.");
        var op = await ApprovedAsync(Vectors, tiles, pixels);
        await Owner.ConfirmAsync(pixels.Id, pixels.Revision);
        var before = await _lyntai.StateAsync("Owner", tiles.Id, pixels.Id);

        await Applier.ApplyReadyAsync();

        Assert.Equal(MemoryOpState.Stale, (await _machine.Memory.FindAsync(op.Id))!.State);
        Assert.Equal(before, await _lyntai.StateAsync("Owner", tiles.Id, pixels.Id));
        Assert.Empty(await Owner.SearchAsync("vectors", 10));
        Assert.Null(await Owner.ReceiptAsync(op.Id));
    }

    /// <summary>
    /// 🔴 A Holds on an entry a replacement awaits to end is kept on the replacement's row and the entry not confirmed, so the revision the
    /// person approves still holds when it applies.
    /// </summary>
    [Fact]
    public async Task A_holds_while_a_replacement_awaits_is_kept_on_its_row()
    {
        var tiles = await Owner.RememberAsync("The map is drawn in tiles.");
        var op = await AwaitingAsync(Vectors, tiles);

        Assert.Contains(("Owner", tiles.Id, MemoryOpKind.End), await _machine.Memory.PendingAsync());
        await _machine.Memory.SweepAsync([new MemoryRecheck("Owner", tiles.Id, tiles.Revision, Holds: true)], Now.AddHours(1));
        await new MemoryDesk(_machine.Memory).DecideAsync(op.Id, null, approve: true, Now.AddHours(2));
        await Applier.ApplyReadyAsync();

        var applied = (await _machine.Memory.FindAsync(op.Id))!;
        Assert.Equal((MemoryOpState.Applied, Now.AddHours(1)), (applied.State, applied.Holds));
        foreach (var state in new[] { MemoryOpState.Ready, MemoryOpState.Applied, MemoryOpState.Stale })
        {
            Assert.DoesNotContain(await _machine.Memory.ListAsync(state), row => row.Change.Kind == MemoryOpKind.Confirm);
        }
    }

    private async Task LyntaiAsync(string sql)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _lyntai.Path, Pooling = false }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Each engine, with <paramref name="after"/> run after every call that changes it.</summary>
    private sealed class Watched(IMemoryEngines inner, Func<Task> after) : IMemoryEngines
    {
        public IMemoryEngine? Open(string repository, MemoryReinforcement reinforceOn) =>
            inner.Open(repository, reinforceOn) is { } engine ? new Engine(engine, after) : null;

        private sealed class Engine(IMemoryEngine inner, Func<Task> after) : IMemoryEngine
        {
            public bool HoldsReceipts => inner.HoldsReceipts;

            public bool HoldsSupersession => inner.HoldsSupersession;

            public Task<IReadOnlyList<MemoryEntry>> SearchAsync(string query, int limit, CancellationToken ct = default) => inner.SearchAsync(query, limit, ct);

            public Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default) => inner.GetAsync(id, ct);

            public Task<IReadOnlyList<MemoryRelated>> ExpandAsync(string id, CancellationToken ct = default) => inner.ExpandAsync(id, ct);

            public Task<MemoryKeyReceipt?> ReceiptAsync(string key, CancellationToken ct = default) => inner.ReceiptAsync(key, ct);

            public Task<IReadOnlyList<MemoryDue>> DueAsync(DateTimeOffset since, int limit, CancellationToken ct = default) => inner.DueAsync(since, limit, ct);

            public Task<MemoryEntry> RememberAsync(string text, CancellationToken ct = default, string? key = null, string? recheck = null) =>
                ThenAsync(inner.RememberAsync(text, ct, key, recheck));

            public Task<MemoryLink> RelateAsync(string from, string to, string kind, CancellationToken ct = default, string? key = null) =>
                ThenAsync(inner.RelateAsync(from, to, kind, ct, key));

            public Task<MemoryEntry?> ConfirmAsync(string id, long revision, CancellationToken ct = default, string? key = null) =>
                ThenAsync(inner.ConfirmAsync(id, revision, ct, key));

            public Task<MemoryEntry?> EndAsync(string id, long revision, string reason, CancellationToken ct = default, string? key = null) =>
                ThenAsync(inner.EndAsync(id, revision, reason, ct, key));

            public Task<MemorySuperseded> SupersedeAsync(
                string text, IReadOnlyList<MemoryPredecessor> predecessors, string reason, CancellationToken ct = default, string? key = null,
                string? recheck = null) =>
                ThenAsync(inner.SupersedeAsync(text, predecessors, reason, ct, key, recheck));

            private async Task<T> ThenAsync<T>(Task<T> call)
            {
                var answer = await call;
                await after();
                return answer;
            }
        }
    }
}
