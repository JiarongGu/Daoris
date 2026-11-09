using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A tree handed to a resolver, by its own row (§13, §29): written once per tree, read by every start inside its hold, by the trees'
/// holders, and reserved under the repository's lock alone, so a start and a reservation each see the other. No git runs here: the trees
/// are folders and the ledger a stand-in.
/// </summary>
public sealed class TreeHandOffsTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-hand-offs-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>The tree linked to the checkout as git links a worktree, so both name one repository's lock.</summary>
    public TreeHandOffsTests()
    {
        var linked = Path.Combine(Checkout, ".git", "worktrees", "s-1a2b3c4d");
        Directory.CreateDirectory(linked);
        File.WriteAllText(Path.Combine(linked, "commondir"), "../..");
        Directory.CreateDirectory(Tree);
        File.WriteAllText(Path.Combine(Tree, ".git"), $"gitdir: {linked}");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_home, recursive: true);
        }
        catch (DirectoryNotFoundException) { /* nothing written */ }
    }

    private string Checkout => Path.Combine(_home, "engine");

    private string Tree => DaorisHome.Entry(_home, HomeEntry.Trees, "aurora", "engine", "s-1a2b3c4d");

    private TreeHandOffs Offs() => new(_home, () => StoreSettled.Yes);

    private TreeHandOff Row(string? tree = null) => new(
        tree ?? Tree, "engine", "daoris/s-1a2b3c4d", "0123abc", "r-12", "n3", 1, "stop-1", "session", TreeHandOff.AskKey("r-12", "n3", 1, 1),
        TreeHandOffPhases.Reserved, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30));

    [Fact]
    public void A_row_reserves_its_tree_once_until_its_restoration_closes_it()
    {
        var offs = Offs();
        var row = offs.Write(Row())!;

        Assert.Equal(row.Id, offs.Of(Tree)!.Id);
        Assert.Null(offs.Write(Row()));
        Assert.True(offs.Advance(row, TreeHandOffPhases.Asked, "s9"));
        Assert.Equal(("asked", "s9"), (offs.Of(Tree)!.Phase, offs.Of(Tree)!.Session));
        Assert.Single(offs.Open());

        Assert.True(offs.Close(row));

        Assert.Null(offs.Of(Tree));
        Assert.Empty(offs.Open());
        Assert.False(offs.Advance(row, TreeHandOffPhases.Restoring));
        Assert.NotNull(offs.Write(Row()));
    }

    [Fact]
    public void The_resolvers_own_step_is_the_rows_attempt_under_its_ask_key() =>
        Assert.Equal(new StepAuthority("r-12", "n3", 1, "resolve/r-12/n3/1/1"), TreeHandOffs.AuthorityFor(Row()));

    /// <summary>A start in the reserved tree is held, said, but for the one carrying the ask's key; a start elsewhere, fresh or of none is not.</summary>
    [Fact]
    public void A_start_in_a_reserved_tree_is_held_unless_it_carries_the_asks_key()
    {
        Offs().Write(Row());

        Assert.Null(TreeLock.TryStarting(_home, Checkout, StartIn.In(Tree)));
        Assert.Null(TreeLock.TryStarting(_home, Checkout, StartIn.In(Tree + Path.DirectorySeparatorChar, "resolve/r-12/n3/1/2")));
        Assert.Contains("run `r-12`'s `resolve` holds `s-1a2b3c4d` in `engine` until",
            TreeLock.StartHeld(_home, Checkout, StartIn.In(Tree), "engine").Why, StringComparison.Ordinal);
        foreach (var free in new[] { StartIn.In(Tree, "resolve/r-12/n3/1/1"), StartIn.In(Checkout), StartIn.Fresh, StartIn.None })
        {
            using var held = TreeLock.TryStarting(_home, Checkout, free);
            Assert.NotNull(held);
        }
    }

    /// <summary>The start holding first: the reservation cannot hold the lock alone, writes nothing and waits, said.</summary>
    [Fact]
    public async Task A_start_holding_first_leaves_the_reservation_to_the_next_pass()
    {
        var ledger = new DiscardLedger();
        using (var starting = TreeLock.TryStarting(_home, Checkout, StartIn.In(Tree)))
        {
            Assert.NotNull(starting);
            var reserved = await TreeReserving.ReserveAsync(_home, ledger.Client(), Offs(), Row(), CancellationToken.None);

            Assert.Null(reserved.Row);
            Assert.Equal("a session was starting in `engine`, or another of Daoris's writes held it", reserved.Waits);
            Assert.Null(Offs().Of(Tree));
        }

        // Once its record is open the holders name its session, and the hand-off does not take the tree.
        ledger.Live = [("s7", "working", Tree)];
        var held = await TreeReserving.ReserveAsync(_home, ledger.Client(), Offs(), Row(), CancellationToken.None);

        Assert.Equal("s7", held.Holder!.Session);
        Assert.Null(Offs().Of(Tree));
    }

    /// <summary>The reservation first: a start at that look is held and, once its row is written, waits on it, said.</summary>
    [Fact]
    public async Task A_reservation_first_holds_the_start_then_keeps_it_waiting_on_the_row()
    {
        using (var alone = TreeLock.TryWriting(_home, Checkout, "engine", HeldBy.Reserving))
        {
            Assert.Null(TreeLock.TryStarting(_home, Checkout, StartIn.In(Tree)));
            Assert.Contains("reserving one of `engine`'s trees", TreeLock.StartHeld(_home, Checkout, StartIn.In(Tree), "engine").Why, StringComparison.Ordinal);
        }

        var reserved = await TreeReserving.ReserveAsync(_home, new DiscardLedger().Client(), Offs(), Row(), CancellationToken.None);

        Assert.NotNull(reserved.Row);
        Assert.Null(TreeLock.TryStarting(_home, Checkout, StartIn.In(Tree)));
        Assert.Contains("`resolve` holds", TreeLock.StartHeld(_home, Checkout, StartIn.In(Tree), "engine").Why, StringComparison.Ordinal);
    }

    /// <summary>Every door that reads the holders keeps off a reserved tree, named by its step.</summary>
    [Fact]
    public async Task The_holders_name_a_reserved_tree_by_its_step()
    {
        Offs().Write(Row());
        var client = new DiscardLedger().Client();

        var holder = Assert.Single(await TidyHolders.HoldersAsync(_home, client, CancellationToken.None));
        var beside = Assert.Single(await TidyHolders.BesideAsync(_home, client, CancellationToken.None));

        Assert.Equal("run `r-12`'s `resolve`", holder.Step);
        Assert.Equal(TreeHandOffs.KeyOf(Tree), TreeHandOffs.KeyOf(holder.Tree));
        Assert.Equal(holder, beside);
        Assert.True((await TidyHolders.HeldTreesAsync(_home, client, CancellationToken.None)).Contains(Tree));
        Assert.True((await TidyHolders.InUseAsync(_home, client, null, CancellationToken.None)).Contains(Tree));
    }

    /// <summary>The together press's shared hold is a value its writes ask, live while held and refused once let go.</summary>
    [Fact]
    public async Task The_together_locks_carry_their_shared_hold_as_a_value()
    {
        var (locks, refused) = await TogetherLocks.TakeAsync(_home, "engine", [Tree], TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.Null(refused);
        var hold = locks!.Hold;
        hold.Assert("a press's write", Tree);

        locks.Dispose();

        Assert.Throws<InvalidOperationException>(() => hold.Assert("a press's write", Tree));
    }
}
