using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWCENTRAL2d (the memory coordinator design §1, §3): the memory tools over Lyntai's engine and the machine's log of record. A connector
/// stages and reads; a session's commit point releases; the service applies. Only a session and a session continuing it see what it staged.
/// </summary>
public sealed class MemoryEngineTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = MemoryMachine.Now;

    private MemoryMachine _machine = null!;
    private LyntaiFile _lyntai = null!;

    public async Task InitializeAsync()
    {
        _machine = await MemoryMachine.OpenAsync("memory-engine");
        _lyntai = LyntaiFile.Open("memory-engine");
    }

    public async Task DisposeAsync()
    {
        await _lyntai.DisposeAsync();
        await _machine.DisposeAsync();
    }

    /// <summary>The memory tools as <paramref name="session"/>'s connector holds them, started in <paramref name="role"/>.</summary>
    private MemoryTools Tools(Session session, string role = "builder", string revision = "3") => new(
        _machine.Registry, new AmbientWorkspace(Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures")),
        new IntakeScope(null, session.Id) { Role = role, RoleRevision = revision }, _machine.Ledger, _lyntai.Memory);

    private MemoryApplier Applier => new(_machine.Memory, _lyntai.Memory);

    /// <summary>🔴 A connector's write and end are kept as staged rows of its session alone; Lyntai's engine is left as it was.</summary>
    [Fact]
    public async Task A_connector_writes_only_staged_rows()
    {
        var map = await _lyntai.Engine("Owner").RememberAsync("The map is drawn in tiles.");
        var chat = await _machine.ChatAsync(Now);
        var tools = Tools(chat);
        var before = await _lyntai.StateAsync("Owner", map.Id);

        var wrote = await tools.WriteAsync("The legend sits under the map.", "amends", $"Owner:{map.Id}");
        var ended = await tools.EndAsync($"Owner:{map.Id}", map.Revision, "The map is drawn in vectors now.");

        Assert.Contains("staged", wrote);
        Assert.Contains("staged", ended);
        var rows = await _machine.Memory.OfSessionAsync(chat.Id);
        Assert.Equal([(MemoryOpKind.Write, MemoryOpState.Staged), (MemoryOpKind.End, MemoryOpState.Staged)], rows.Select(op => (op.Change.Kind, op.State)));
        Assert.Equal((map.Id, "amends"), (rows[0].Change.Target, rows[0].Change.Relation));
        Assert.Equal(1, await _lyntai.CountAsync("Owner"));
        Assert.Equal(before, await _lyntai.StateAsync("Owner", map.Id));
    }

    /// <summary>
    /// 🔴 A tree's addition is found by its session and by the hand-over continuing it, and by no other session, until the post of the tree's
    /// record naming a landing releases it and the service applies it: then every session finds it in the engine.
    /// </summary>
    [Fact]
    public async Task A_trees_addition_is_seen_by_its_session_and_its_continuation_alone_until_the_post()
    {
        var session = await _machine.InTreeAsync(Now);
        var continuation = await _machine.Sessions.CreateAsync(session.Quest, "Owner", "stub", Now.AddMinutes(1), tree: MemoryMachine.Tree, from: session.Id);
        var other = await _machine.ChatAsync(Now);
        await Tools(session).WriteAsync("The legend sits under the map.");

        Assert.Contains("The legend sits under the map.", await Tools(session).SearchAsync("legend"));
        Assert.Contains("The legend sits under the map.", await Tools(continuation).SearchAsync("legend"));
        Assert.DoesNotContain("legend sits", await Tools(other).SearchAsync("legend"));
        Assert.Equal(0, await _lyntai.CountAsync("Owner"));

        await _machine.Records.PostAsync(MemoryMachine.Post(landing: true), Now.AddHours(1));
        Assert.Equal(1, await Applier.ApplyReadyAsync());

        var found = await Tools(other).SearchAsync("legend");
        Assert.Contains("`Owner:", found);
        Assert.Contains("The legend sits under the map.", found);
        Assert.DoesNotContain("staged", found);
    }

    /// <summary>🔴 A row a session in a tree makes holds no tree path: the rows are kept by session, never by tree (§1).</summary>
    [Fact]
    public async Task No_row_holds_a_tree_path()
    {
        var map = await _lyntai.Engine("Owner").RememberAsync("The map is drawn in tiles.");
        var session = await _machine.InTreeAsync(Now);
        await Tools(session).WriteAsync("The legend sits under the map.", "amends", $"Owner:{map.Id}");
        await Tools(session).EndAsync($"Owner:{map.Id}", map.Revision, "The map is drawn in vectors now.");
        await _machine.Records.PostAsync(MemoryMachine.Post(landing: true), Now.AddHours(1));

        await using var read = _machine.Connection.CreateCommand();
        read.CommandText = "SELECT * FROM memory_ops";
        await using var rows = await read.ExecuteReaderAsync();
        var values = new List<string>();
        while (await rows.ReadAsync())
        {
            for (var i = 0; i < rows.FieldCount; i++) values.Add(rows.IsDBNull(i) ? "" : Convert.ToString(rows.GetValue(i), System.Globalization.CultureInfo.InvariantCulture)!);
        }

        Assert.NotEmpty(values);
        Assert.DoesNotContain(values, value => value.Contains(MemoryMachine.Tree, StringComparison.OrdinalIgnoreCase) || value.Contains("trees/"));
    }

    /// <summary>
    /// 🔴 Who made a row is read at the call (§1): a session's and its continuation's rows each keep the role its own start took, the
    /// predecessor's carried to the commit point by the continuation, with nothing joined later from the driver's bindings.
    /// </summary>
    [Fact]
    public async Task Attribution_is_kept_at_the_call_through_a_continuation()
    {
        var first = await _machine.ChatAsync(Now);
        await Tools(first, "builder", "3").WriteAsync("The legend sits under the map.");
        Assert.Equal(SessionAdvanceRefusal.None, (await _machine.Ledger.AdvanceAsync(first.Id, "stopped", null, null, null, Now.AddMinutes(5))).Refusal);
        var second = await _machine.ChatAsync(Now.AddMinutes(6), from: first.Id);
        await Tools(second, "reviewer", "1").WriteAsync("Tiles are sixteen pixels.");

        Assert.Equal(SessionAdvanceRefusal.None, (await _machine.Ledger.AdvanceAsync(second.Id, "completed", null, null, null, Now.AddMinutes(9))).Refusal);

        var ready = await _machine.Memory.ListAsync(MemoryOpState.Ready);
        Assert.Equal(
            [(first.Id, "builder", "3"), (second.Id, "reviewer", "1")],
            ready.Select(op => (op.By!.Session, op.By.Role!, op.By.RoleRevision!)));
    }
}
