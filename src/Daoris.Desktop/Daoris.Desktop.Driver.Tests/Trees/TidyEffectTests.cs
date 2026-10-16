using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-END4a's link 2 over the records alone (the over-git half is <see cref="TidyEffectGitTests"/>): the tidy pending from the
/// kept facts through the inventory, recovered at the start (canary 16), refused for a busy tree with no git and run when the session
/// leaves it (canary 17), never pending for an unknown owner (canary 27), and a branch's removal leaving its unit open (canary 35).
/// </summary>
public sealed class TidyEffectTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-tidy-effect-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly HashSet<string> _inUse = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _tidied = [];
    private DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    // Whether the stand-in tidy removes what it is handed, or keeps it as a dirty tree would.
    private bool _keeps;

    public TidyEffectTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_home, recursive: true);
        }
        catch (IOException) { /* a straggling handle */ }
    }

    private SessionBranches Grown => new(_home);

    private HeldFact Held(string tip, string by = EndBy.Merged) => new(_now, _now, by, tip, "main", "main", "c0ffee", "identity");

    /// <summary>A session branch recorded with its unit, and its kept fact where one is given.</summary>
    private void Record(string name, HeldFact? held, string? unit = null)
    {
        Grown.Record(new GrownBranch("engine", "default", $"daoris/{name}", "main", "base", null, _now) { Unit = unit ?? $"unit-{name}" });
        if (held is not null) Grown.Hold("engine", $"daoris/{name}", held);
    }

    private string TreeOf(string name)
    {
        var path = Path.GetFullPath(Path.Combine(new SessionTrees(_home).TreesRoot, "default", "engine", name));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>The effect over a stand-in tidy, which removes every held branch it is handed as the real one leaves it: a trace.</summary>
    private TidyEffect Effect() => new(
        _home,
        (name, _) => Task.FromResult<RefRepository?>(new RefRepository(name, "default", _home)),
        _ => Task.FromResult<IReadOnlySet<string>>(_inUse),
        (repository, _, _) =>
        {
            _tidied.Add(repository.Repository);
            var going = TidyEffect.Pending(_home, _now).SelectMany(unit => unit.Branches).ToList();
            if (_keeps)
            {
                return Task.FromResult(new TidyPass(
                    [.. going.Select(branch => new SweepResult(new SweepItem("engine", "default", branch, null, SweepKind.Dirty, 0, "main", null), false, "kept"))]));
            }

            Grown.Forget(repository.Repository, going);
            return Task.FromResult(new TidyPass(
                [.. going.Select(branch => new SweepResult(new SweepItem("engine", "default", branch, null, SweepKind.Empty, 0, "main", null), true, "removed"))]));
        })
    {
        Clock = () => _now,
    };

    private Chain ChainOf(TidyEffect effect) => new Chain(new ChangeWatch(), clock: () => _now).Bind(ChainRanks.Tidy, effect);

    /// <summary>🔴 Canary 15: a unit the line is not known to hold is never pending; a held one is, by its repository.</summary>
    [Fact]
    public async Task Only_a_unit_whose_end_is_held_is_pending()
    {
        Record("a", held: null);
        Record("b", Held("tip-b"));

        var units = TidyEffect.Pending(_home, _now);

        Assert.Equal(["unit-b"], units.Select(unit => unit.Unit));
        Assert.Equal(["daoris/b"], units.Single().Branches);
        Assert.Equal(["engine"], (await Effect().PendingAsync(CancellationToken.None)).Select(each => each.Unit));
    }

    /// <summary>🔴 Canary 16: the fact recorded, the process gone before the tidy; the start's reconciliation tidies it once.</summary>
    [Fact]
    public async Task The_starts_reconciliation_performs_a_pending_tidy_once()
    {
        Record("a", Held("tip-a"));
        var chain = ChainOf(Effect());

        Assert.Equal(1, await chain.RecoverAsync(CancellationToken.None));
        Assert.Equal(0, await chain.RecoverAsync(CancellationToken.None));

        Assert.Equal(["engine"], _tidied);
        var trace = Grown.Entries().Single();
        Assert.NotNull(trace.GoneAt);
        Assert.Empty(TidyEffect.Pending(_home, _now));
    }

    /// <summary>
    /// 🔴 Canary 17: refused for a tree a session holds, with no tidy started and the guard written as the unit's disposition; then run
    /// on the session's leaving, a <c>ledger</c> wake, with no fact having changed.
    /// </summary>
    [Fact]
    public async Task A_busy_tree_stays_pending_and_is_tidied_when_the_session_leaves()
    {
        Record("a", Held("tip-a"));
        _inUse.Add(TreeOf("a"));
        var chain = ChainOf(Effect());
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Headless, out _);

        Assert.Equal(0, await chain.FireAsync([new ChainEvent(ChainEvents.WorkHeld, "engine", ChangeTopics.Refs)], CancellationToken.None));
        Assert.Empty(_tidied);
        Assert.Equal(SweepKind.InUse, Grown.Of("engine", "daoris/a")!.Blocked?.Why);
        var entry = Milestone("unit-a").Entries.Single(each => each.Kind == InventoryKind.SessionBranch);
        Assert.Equal(InventoryDisposition.Blocked, entry.Disposition);
        Assert.Contains("a session running or waiting holds its tree", entry.Detail, StringComparison.Ordinal);

        _inUse.Clear();
        Assert.Equal(1, await chain.FireAsync([new ChainEvent(ChainEvents.Ledger, "sessions", ChangeTopics.Ledger)], CancellationToken.None));

        Assert.Equal(["engine"], _tidied);
        Assert.Equal("completed", Milestone("unit-a").Cleanup.State);
    }

    /// <summary>
    /// 🔴 The review's P2s: an unchanged blocker is one try, whatever wakes come, until the <c>refs</c> deadline passes; and a process
    /// that does not hold the home's lease writes no <c>blocked</c>.
    /// </summary>
    [Fact]
    public async Task An_unchanged_blocker_is_tried_once_until_the_deadline_and_only_the_lease_writes_it()
    {
        Record("a", Held("tip-a"));
        _keeps = true;
        var chain = ChainOf(Effect());

        foreach (var wake in RefWatch.Unblocking) await chain.FireAsync([new ChainEvent(wake, wake, wake)], CancellationToken.None);

        Assert.Equal(["engine"], _tidied);
        Assert.Null(Grown.Of("engine", "daoris/a")!.Blocked);

        _now += RefWatch.Deadline;
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Headless, out _);
        await chain.FireAsync([new ChainEvent(ChainEvents.Ledger, "sessions", ChangeTopics.Ledger)], CancellationToken.None);

        Assert.Equal(["engine", "engine"], _tidied);
        Assert.Equal(SweepKind.Dirty, Grown.Of("engine", "daoris/a")!.Blocked?.Why);
    }

    /// <summary>🔴 The review's P2: a removed branch whose tree moved onto a branch of its own is a trace at once, keeping where it went, and no longer pending.</summary>
    [Fact]
    public void A_removed_branch_whose_tree_moved_on_is_a_trace_at_once()
    {
        Grown.Record(new GrownBranch("engine", "default", "daoris/a", "main", "base", null, _now, Now: "feature/x") { Unit = "unit-a" });
        Grown.Hold("engine", "daoris/a", Held("tip-a"));

        Grown.Gone("engine", ["daoris/a"]);

        var trace = Grown.Entries().Single();
        Assert.NotNull(trace.GoneAt);
        Assert.Equal("feature/x", trace.Now);
        Assert.Empty(TidyEffect.Pending(_home, _now));
    }

    /// <summary>🔴 Canary 27: a tree no record names is its own unit, blocked and said so, and never pending; nothing is tried for it.</summary>
    [Fact]
    public async Task An_unknown_owner_blocks_and_is_never_tried()
    {
        TreeOf("nobody");
        var chain = ChainOf(Effect());

        await chain.RecoverAsync(CancellationToken.None);

        Assert.Empty(TidyEffect.Pending(_home, _now));
        Assert.Empty(_tidied);
        var unknown = (await WorkInventories.MilestonesAsync(_home, new Dictionary<string, string>(), new HashSet<string>(), new List<string>())).Single();
        Assert.Equal("unknown", unknown.End.State);
        var blocked = unknown.Entries.Single();
        Assert.Equal(InventoryDisposition.Blocked, blocked.Disposition);
        Assert.Contains("its owner is unknown", blocked.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 Canary 35: the tidy removing a held session branch whose unit a standing landing still carries leaves the unit open, its trace
    /// kept; it retires once the line holds the landing.
    /// </summary>
    [Fact]
    public async Task A_branch_removed_leaves_its_unit_open_until_its_landing_is_held()
    {
        Record("a", Held("tip-a"), unit: "unit-a");
        var landings = new LandedBranches(_home);
        landings.Record(new LandedBranch("engine", "default", "feature/q1-work", "main", "tip-l", "s1", "q1", "Work", _now) { Units = ["unit-a"] });

        await ChainOf(Effect()).RecoverAsync(CancellationToken.None);

        var trace = Grown.Entries().Single();
        Assert.NotNull(trace.GoneAt);
        Assert.Contains("unit-a", SessionBranches.OpenUnits(_home));
        Assert.Equal(InventoryDisposition.Removed, Milestone("unit-a").Entries.Single(each => each.Kind == InventoryKind.SessionBranch).Disposition);

        landings.Hold(landings.All().Single(), Held("tip-l"));
        Assert.DoesNotContain("unit-a", SessionBranches.OpenUnits(_home));
    }

    private WorkMilestone Milestone(string unit) =>
        WorkInventory.Derive(new InventoryFacts(
            Grown.Entries(), new LandedBranches(_home).Entries(), [], [], [], new HashSet<string>(), _ => false, _now)).Single(each => each.Unit == unit);
}
