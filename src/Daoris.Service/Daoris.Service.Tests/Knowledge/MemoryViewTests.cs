using Daoris.Knowledge;
using Daoris.Knowledge.Mcp;

namespace Daoris.Service.Tests;

/// <summary>
/// KNOWCENTRAL2g (the memory coordinator design §6 B): every read of another repository's memory goes through Lyntai's read-only view
/// (MC7), never its owner's engine. The view moves nothing of the owner's, asks the caller's scope about an entry before it reads it, and a
/// foreign entry an entry cites is rechecked through it; an engine that gives no view says so and reads nothing.
/// </summary>
public sealed class MemoryViewTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = MemoryMachine.Now;

    private MemoryMachine _machine = null!;

    public async Task InitializeAsync()
    {
        _machine = await MemoryMachine.OpenAsync("memory-views");
        await _machine.Registry.RegisterAsync(new Registration("Asker", Adopted: true, "A repo.", [], [], [], Entries: 0, Root: "/trees/asker"), Now);
    }

    public async Task DisposeAsync() => await _machine.DisposeAsync();

    private MemoryTools Tools(Session session, IMemoryEngines engines) => new(
        _machine.Registry, new AmbientWorkspace(Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures")), new IntakeScope(null, session.Id),
        _machine.Ledger, engines, quests: _machine.Quests);

    private Task<Session> AskerAsync() => _machine.Sessions.CreateAsync(null, "Asker", "stub", Now);

    /// <summary>
    /// 🔴 Over Lyntai's engine opened as its owner opens it, learning from its own recalls: another repository's search, read and expansion
    /// leave each entry's decay state, revision, links and the review rows as they were, since they go through the view. The owner's own
    /// search through the same engine moves them, which the reading sees.
    /// </summary>
    [Fact]
    public async Task A_foreign_read_leaves_the_owners_counters_as_they_were()
    {
        await using var lyntai = LyntaiFile.Open("memory-views");
        var owner = lyntai.Engine("Owner");
        var map = await owner.RememberAsync("Owner draws the map in tiles.");
        var legend = await owner.RememberAsync("The legend sits under the map.");
        await owner.RelateAsync(legend.Id, map.Id, "amends");
        var learning = new Learning(lyntai.Memory);
        var tools = Tools(await AskerAsync(), learning);
        var before = await lyntai.StateAsync("Owner", map.Id, legend.Id);

        var found = await tools.SearchAsync("map");
        var read = await tools.GetAsync($"Owner:{map.Id}");
        var expanded = await tools.GetAsync($"Owner:{legend.Id}", expand: true);

        Assert.Contains($"`Owner:{legend.Id}`", found);
        Assert.Contains("Owner draws the map in tiles.", read);
        Assert.Contains($"amends `Owner:{map.Id}`", expanded);
        Assert.Equal(before, await lyntai.StateAsync("Owner", map.Id, legend.Id));

        await Tools(await _machine.InTreeAsync(Now), learning).SearchAsync("map");
        Assert.NotEqual(before, await lyntai.StateAsync("Owner", map.Id, legend.Id));
    }

    /// <summary>
    /// 🔴 A foreign expansion asks the caller's scope again before it reads: a repository the registry moved out of the workspace after the
    /// entry was read is refused, and the expansion looks nothing up. The view's check records each reference it was asked about.
    /// </summary>
    [Fact]
    public async Task A_foreign_expansion_outside_the_scope_is_refused_before_lookup()
    {
        var memory = new StandInMemory();
        var map = memory.Engine("Asker").Keep("Asker draws the map in tiles.");
        var legend = memory.Engine("Asker").Keep("The legend sits under the map.");
        await memory.Open("Asker", MemoryReinforcement.None)!.RelateAsync(legend.Id, map.Id, "amends");
        var tools = Tools(await _machine.InTreeAsync(Now), memory);
        var asker = (await _machine.Registry.RegistryAsync()).Single(row => row.Repository == "Asker");
        memory.Looked = async _ => await _machine.Registry.RegisterAsync(asker with { Workspace = "elsewhere" }, Now);

        var answer = await tools.GetAsync($"Asker:{legend.Id}", expand: true);

        Assert.StartsWith("Refused", answer);
        Assert.Contains("is not in workspace", answer);
        Assert.DoesNotContain("draws the map", answer);
        Assert.Equal([(new MemoryReference("Asker", legend.Id), true), (new MemoryReference("Asker", legend.Id), false)], memory.Checks);
        Assert.Equal(1, memory.Engine("Asker").Reads);
    }

    /// <summary>🔴 Lyntai's view, as the coordinator opens it, asks the check about the named entry first, and reads nothing it refuses.</summary>
    [Fact]
    public async Task Lyntais_view_asks_the_check_before_it_reads()
    {
        await using var lyntai = LyntaiFile.Open("memory-views");
        var checker = lyntai.Engine("Checker");
        var map = await checker.RememberAsync("Checker draws the map in tiles.");
        var before = await lyntai.StateAsync("Checker", map.Id);
        var refusing = new Recording(permits: false);
        var permitting = new Recording(permits: true);

        Assert.True(checker.HoldsView);
        Assert.Null(await checker.ViewOf(refusing).GetAsync(map.Id));
        Assert.Empty(await checker.ViewOf(refusing).ExpandAsync(map.Id));
        var read = await checker.ViewOf(permitting).GetAsync(map.Id);

        Assert.Equal([new MemoryReference("Checker", map.Id), new MemoryReference("Checker", map.Id)], refusing.Asked);
        Assert.Equal(("Checker draws the map in tiles.", map.Revision), (read?.Text, read?.Revision));
        Assert.Equal(map.Revision, await checker.ViewOf(permitting).RevisionOfAsync(map.Id));
        Assert.Equal(before, await lyntai.StateAsync("Checker", map.Id));
    }

    /// <summary>
    /// 🔴 A write may cite another repository's entry at the revision it read, read through its view: the citation is kept with the write,
    /// and an entry that moved, ended or is the caller's own is refused.
    /// </summary>
    [Fact]
    public async Task A_write_cites_another_repositorys_entry_at_the_revision_it_read()
    {
        var memory = new StandInMemory();
        var tiles = memory.Engine("Owner").Keep("Owner draws the map in tiles.");
        var legend = memory.Engine("Owner").Keep("Owner's legend sits under the map.");
        var own = memory.Engine("Asker").Keep("Asker reads maps.");
        Assert.NotNull(await memory.Open("Owner", MemoryReinforcement.None)!.EndAsync(legend.Id, legend.Revision, "The legend moved."));
        var tools = Tools(await AskerAsync(), memory);

        var moved = await tools.WriteAsync("Asker reads Owner's map as tiles.", cites: [$"Owner:{tiles.Id}@{tiles.Revision + 1}"]);
        var ended = await tools.WriteAsync("Asker reads Owner's map as tiles.", cites: [$"Owner:{legend.Id}@{legend.Revision}"]);
        var mine = await tools.WriteAsync("Asker reads Owner's map as tiles.", cites: [$"Asker:{own.Id}@{own.Revision}"]);
        var kept = await tools.WriteAsync("Asker reads Owner's map as tiles.", cites: [$"Owner:{tiles.Id}@{tiles.Revision}"]);

        Assert.Contains("moved since you read it", moved);
        Assert.Contains("already ended", ended);
        Assert.Contains("own entry", mine);
        Assert.Contains("staged", kept);
        var op = Assert.Single(await _machine.Memory.ListAsync(MemoryOpState.Staged));
        var cited = Assert.Single(MemoryCitations.Read(op.Change.Recheck));
        Assert.Equal(($"Owner:{tiles.Id}", (long?)tiles.Revision), (cited.Id, cited.Revision));
        Assert.Contains((new MemoryReference("Owner", tiles.Id), true), memory.Checks);
    }

    /// <summary>
    /// 🔴 The sweep rechecks an entry's citation of another repository's entry through that repository's view, never its engine: one whose
    /// cited entry stands, though confirmed since, is written as a <c>ready</c> confirmation; one whose cited entry its owner ended, as an
    /// <c>awaiting</c> end naming it and why.
    /// </summary>
    [Fact]
    public async Task A_foreign_citation_is_rechecked_through_the_view()
    {
        await using var lyntai = LyntaiFile.Open("memory-views");
        var owner = lyntai.Engine("Owner");
        var tiles = await owner.RememberAsync("Owner draws the map in tiles.");
        var vectors = await owner.RememberAsync("Owner keeps its legend in vectors.");
        var asker = lyntai.Engine("Asker");
        var holds = await asker.RememberAsync(
            "Asker reads Owner's map as tiles.", recheck: MemoryCitations.Of([Citation.OfEntry(new("Owner", tiles.Id), tiles.Revision)]));
        var fails = await asker.RememberAsync(
            "Asker reads Owner's legend as vectors.", recheck: MemoryCitations.Of([Citation.OfEntry(new("Owner", vectors.Id), vectors.Revision)]));
        Assert.NotNull(await owner.ConfirmAsync(tiles.Id, tiles.Revision));
        Assert.NotNull(await owner.EndAsync(vectors.Id, vectors.Revision, "Owner's legend is drawn in tiles now."));

        await new MemorySweep(_machine.Memory, new ViewedOnly(lyntai.Memory, "Owner"), _machine.Registry).RunAsync(DateTimeOffset.UtcNow);

        var confirm = Assert.Single(await _machine.Memory.ListAsync(MemoryOpState.Ready));
        Assert.Equal((MemoryOpKind.Confirm, "Asker", holds.Id), (confirm.Change.Kind, confirm.Repository, confirm.Change.Target));
        var end = Assert.Single(await _machine.Memory.ListAsync(MemoryOpState.Awaiting));
        Assert.Equal((MemoryOpKind.End, "Asker", fails.Id), (end.Change.Kind, end.Repository, end.Change.Target));
        Assert.Contains($"`Owner:{vectors.Id}`", end.Change.Text);
        Assert.Contains("Owner's legend is drawn in tiles now.", end.Change.Text);
    }

    /// <summary>
    /// 🔴 Where another repository's engine gives no view, a search, a read, a proposal and a presentation each say its memory cannot be read
    /// without moving it, and read nothing of it; the caller's own memory is read as before.
    /// </summary>
    [Fact]
    public async Task An_engine_without_views_says_so_and_reads_nothing()
    {
        var memory = new StandInMemory { Views = false };
        memory.Engine("Owner").Keep("Owner draws the map in tiles.");
        memory.Engine("Asker").Keep("Asker draws the map in tiles.");
        var tools = Tools(await _machine.InTreeAsync(Now), memory);

        var found = await tools.SearchAsync("map");
        var read = await tools.GetAsync("Asker:m1", expand: true);
        var proposed = await tools.ProposeAsync("Asker:m1", "disputes", "Asker draws it in vectors.", 1);
        var shown = Assert.Single(await new MemoryPresenter(_machine.Memory, memory) { Own = "Owner" }
            .PresentAsync([("reader", new MemoryRead(new("Asker", "m1"), 1))]));

        Assert.Contains("`Owner:m1`", found);
        Assert.Contains("`Asker`'s memory cannot be read from here without moving it", found);
        Assert.DoesNotContain("Asker draws", found);
        Assert.All([read, proposed], answer =>
        {
            Assert.StartsWith("Refused", answer);
            Assert.Contains("`Asker`'s memory cannot be read from here without moving it", answer);
            Assert.DoesNotContain("Asker draws", answer);
        });
        Assert.Equal((MemoryStanding.Historical, (long?)null), (shown.Standing, shown.Checked));
        Assert.Contains("cannot be read from here without moving it", shown.Line);
        Assert.Equal(0, memory.Engine("Asker").Reads);
        Assert.NotEqual(0, memory.Engine("Owner").Reads);
    }

    /// <summary>Each engine as Lyntai opens one by default, learning from its own recalls.</summary>
    private sealed class Learning(IMemoryEngines inner) : IMemoryEngines
    {
        public IMemoryEngine? Open(string repository, MemoryReinforcement reinforceOn) => inner.Open(repository, MemoryReinforcement.OnRecall);
    }

    /// <summary>The memories <paramref name="inner"/> keeps, <paramref name="owner"/>'s engine answering no read: it is read through its view.</summary>
    private sealed class ViewedOnly(IMemoryEngines inner, string owner) : IMemoryEngines
    {
        public IMemoryEngine? Open(string repository, MemoryReinforcement reinforceOn) =>
            inner.Open(repository, reinforceOn) is { } engine && repository == owner ? new Unread(engine) : inner.Open(repository, reinforceOn);

        private sealed class Unread(IMemoryEngine inner) : IMemoryEngine
        {
            public bool HoldsView => inner.HoldsView;

            public IMemoryView ViewOf(IMemoryViewCheck? check) => inner.ViewOf(check);

            public Task<IReadOnlyList<MemoryDue>> DueAsync(DateTimeOffset since, int limit, CancellationToken ct = default) => inner.DueAsync(since, limit, ct);

            public Task<IReadOnlyList<MemoryEntry>> SearchAsync(string query, int limit, CancellationToken ct = default) => throw Read();

            public Task<MemoryEntry?> GetAsync(string id, CancellationToken ct = default) => throw Read();

            public Task<IReadOnlyList<MemoryRelated>> ExpandAsync(string id, CancellationToken ct = default) => throw Read();

            public Task<MemoryEntry> RememberAsync(string text, CancellationToken ct = default, string? key = null, string? recheck = null) => throw Read();

            public Task<MemoryLink> RelateAsync(string from, string to, string kind, CancellationToken ct = default, string? key = null) => throw Read();

            public Task<MemoryEntry?> ConfirmAsync(string id, long revision, CancellationToken ct = default, string? key = null) => throw Read();

            public Task<MemoryEntry?> EndAsync(string id, long revision, string reason, CancellationToken ct = default, string? key = null) => throw Read();

            private static InvalidOperationException Read() => new("The owner's engine was called, not its view.");
        }
    }

    /// <summary>A view's check answering every reference alike, and recording each it was asked about.</summary>
    private sealed class Recording(bool permits) : IMemoryViewCheck
    {
        public List<MemoryReference> Asked { get; } = [];

        public Task<bool> PermitsAsync(MemoryReference reference, CancellationToken ct = default)
        {
            Asked.Add(reference);
            return Task.FromResult(permits);
        }
    }
}
