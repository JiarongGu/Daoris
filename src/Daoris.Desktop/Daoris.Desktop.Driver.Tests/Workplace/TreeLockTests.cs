using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR6's open window, closed (LEFT2): a repository's trees are held while a session starts in one, beside any other
/// start, and while bringing its branches up to date replays them, alone; and its checkout while Daoris writes it, alone
/// (GITLOCK1), one lock per checkout whatever it is registered as. Files only, so it is the fast half's.
/// </summary>
public sealed class TreeLockTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-tree-lock-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _engine;

    public TreeLockTests()
    {
        Directory.CreateDirectory(_home);
        _engine = Checkout("engine");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A checkout as git keeps one: a folder holding its <c>.git</c> folder.</summary>
    private string Checkout(string name)
    {
        var root = Path.Combine(_home, "checkouts", name);
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        return root;
    }

    /// <summary>A linked tree of <paramref name="root"/> as <c>git worktree add</c> leaves it: its <c>.git</c> file, and its folder's <c>commondir</c>.</summary>
    private string TreeOf(string root, string name)
    {
        var admin = Path.Combine(root, ".git", "worktrees", name);
        Directory.CreateDirectory(admin);
        File.WriteAllText(Path.Combine(admin, "commondir"), "../..\n");
        var tree = Path.Combine(_home, "trees", name);
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, ".git"), $"gitdir: {admin}\n");
        return tree;
    }

    /// <summary>Two sessions starting in one repository do not race each other, so neither holds the other.</summary>
    [Fact]
    public void Two_starts_in_one_repository_hold_it_together()
    {
        using var first = TreeLock.TryStarting(_home, _engine);
        using var second = TreeLock.TryStarting(_home, _engine);

        Assert.NotNull(first);
        Assert.NotNull(second);
    }

    /// <summary>
    /// The window itself: a replay while a session is starting in the repository is refused, and takes it once the
    /// start has let go, by which time the session's record is open and the ledger says its tree is in use.
    /// </summary>
    [Fact]
    public void A_replay_is_refused_while_a_start_holds_the_repository_and_takes_it_once_the_start_lets_go()
    {
        var starting = TreeLock.TryStarting(_home, _engine);
        Assert.NotNull(starting);

        Assert.Null(TreeLock.TryReplaying(_home, _engine, "engine", TreeLock.Updating));

        starting!.Dispose();
        using var replaying = TreeLock.TryReplaying(_home, _engine, "engine", TreeLock.Updating);
        Assert.NotNull(replaying);
    }

    /// <summary>A start while a replay holds the repository is refused, and a second replay with it.</summary>
    [Fact]
    public void A_start_or_a_second_replay_is_refused_while_a_replay_holds_the_repository()
    {
        var replaying = TreeLock.TryReplaying(_home, _engine, "engine", TreeLock.Updating);
        Assert.NotNull(replaying);

        Assert.Null(TreeLock.TryStarting(_home, _engine));
        Assert.Null(TreeLock.TryReplaying(_home, _engine, "engine", TreeLock.Tidying));

        replaying!.Dispose();
        using var starting = TreeLock.TryStarting(_home, _engine);
        Assert.NotNull(starting);
    }

    /// <summary>
    /// GITLOCK1: a writer of the checkout holds the repository alone, as a replay does, in one lock with it: refused while a start, a
    /// replay or another writer holds it, and each of those refused while it does.
    /// </summary>
    [Fact]
    public void A_writer_of_the_checkout_holds_the_repository_alone_in_the_one_lock_with_starts_and_replays()
    {
        var starting = TreeLock.TryStarting(_home, _engine);
        Assert.Null(TreeLock.TryWriting(_home, _engine, "engine", TreeLock.Promoting));
        starting!.Dispose();

        var replaying = TreeLock.TryReplaying(_home, _engine, "engine", TreeLock.Updating);
        Assert.Null(TreeLock.TryWriting(_home, _engine, "engine", TreeLock.Promoting));
        replaying!.Dispose();

        using (var writing = TreeLock.TryWriting(_home, _engine, "engine", TreeLock.Promoting))
        {
            Assert.NotNull(writing);
            Assert.True(writing!.Alone);
            Assert.True(RepositoryHolds.Held(_home, "engine"));
            Assert.Null(TreeLock.TryWriting(_home, _engine, "engine", TreeLock.Updating));
            Assert.Null(TreeLock.TryReplaying(_home, _engine, "engine", TreeLock.Updating));
            Assert.Null(TreeLock.TryStarting(_home, _engine));
        }

        using var shared = TreeLock.TryStarting(_home, _engine);
        Assert.False(shared!.Alone);
    }

    /// <summary>
    /// 🔴 GITLOCK1: one lock per checkout, keyed by its common git directory: the same checkout registered twice (another name, another
    /// workspace, its path spelled another way) is one lock, and so is any session tree of it, whose <c>.git</c> file names it.
    /// </summary>
    [Fact]
    public void One_checkout_is_one_lock_however_it_is_named_and_through_any_of_its_trees()
    {
        var tree = TreeOf(_engine, "s-1");
        using var writing = TreeLock.TryWriting(_home, _engine, "engine", TreeLock.Updating);

        Assert.Null(TreeLock.TryStarting(_home, _engine + Path.DirectorySeparatorChar));
        Assert.Null(TreeLock.TryStarting(_home, Path.Combine(_engine, "..", "engine")));
        Assert.Null(TreeLock.TryWriting(_home, _engine, "engine-again", TreeLock.Promoting));
        Assert.Null(TreeLock.TryStarting(_home, tree));
        Assert.Equal(TreeLock.PathOf(_home, _engine), TreeLock.PathOf(_home, tree));
        if (OperatingSystem.IsWindows()) Assert.Null(TreeLock.TryStarting(_home, _engine.ToUpperInvariant()));
    }

    /// <summary>Another checkout is its own, whatever its repository is named: a replay holds only its own.</summary>
    [Fact]
    public void Another_checkout_is_held_on_its_own_even_under_the_same_name()
    {
        using var replaying = TreeLock.TryReplaying(_home, _engine, "engine", TreeLock.Updating);

        using var game = TreeLock.TryStarting(_home, Checkout("game"));
        using var elsewhere = TreeLock.TryStarting(_home, Checkout(Path.Combine("other", "engine")));
        Assert.NotNull(game);
        Assert.NotNull(elsewhere);
    }

    /// <summary>Under the home's own `locks/`, never the trees home, which the clean-up tidies and the tree list reads.</summary>
    [Fact]
    public void The_lock_lives_under_the_home_and_outside_the_trees_home()
    {
        var path = TreeLock.PathOf(_home, _engine);

        Assert.StartsWith(DaorisHome.Entry(_home, HomeEntry.Locks) + Path.DirectorySeparatorChar, path);
        Assert.False(path.StartsWith(new SessionTrees(_home).TreesRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 🔴 GITLOCK1: what holds the checkout alone says so beside the lock, so whoever it refuses names it; let go, it says nothing, and words
    /// a process left that is not the one running under its id now are nothing too. A start says nothing: a writer it refuses names one.
    /// </summary>
    [Fact]
    public void What_holds_the_checkout_alone_is_named_while_it_holds_it_and_never_after()
    {
        using (TreeLock.TryWriting(_home, _engine, "engine", TreeLock.Promoting))
        {
            Assert.Equal(TreeLock.Promoting, TreeLock.HolderOf(_home, _engine));
            Assert.Equal("`engine`'s line was being moved to what its integration promoted", TreeLock.Held("engine", TreeLock.HolderOf(_home, _engine)));
            var start = HeldWords.Holding("engine", TreeLock.HolderOf(_home, _engine));
            Assert.Equal(
                "`engine`'s line is being moved to what its integration promoted right now, so a session does not start in one of its trees until that is done; it starts at the next look.",
                start.Why);
            Assert.Equal(["held.promoting"], NoteAssert.Codes(start.Line.Parts));
            NoteAssert.Holds(start.Line);
        }

        Assert.Null(TreeLock.HolderOf(_home, _engine));
        using (TreeLock.TryStarting(_home, _engine))
        {
            Assert.Null(TreeLock.HolderOf(_home, _engine));
            Assert.Equal("a session was starting in `engine`, or another of Daoris's writes held it", TreeLock.Held("engine", null));
        }

        File.WriteAllText(Path.ChangeExtension(TreeLock.PathOf(_home, _engine), ".by"), $"{TreeLock.Tidying}\n{Environment.ProcessId}\n1\n");
        Assert.Null(TreeLock.HolderOf(_home, _engine));
        Assert.Equal(["held.writing"], NoteAssert.Codes(HeldWords.Holding("engine", null).Line.Parts));
        Assert.Equal(["held.tidying"], NoteAssert.Codes(HeldWords.Holding("engine", TreeLock.Tidying).Line.Parts));
        Assert.Equal(["held.replaying"], NoteAssert.Codes(HeldWords.Holding("engine", TreeLock.Updating).Line.Parts));
    }

    /// <summary>
    /// 🔴 GITLOCK1's order: a flow holding a checkout alone is seen making each wait on another lock, and a flow holding it shared or not at
    /// all is not; a record's lock is one such wait, a tree's landing hold another, which no writer may make under it.
    /// </summary>
    [Fact]
    public async Task Each_wait_a_flow_makes_on_a_lock_while_it_holds_a_checkout_alone_is_seen()
    {
        var seen = new List<string>();
        LockWaits.Seen.Value = what => { lock (seen) seen.Add(what); };
        var record = RecordLock.PathOf(_home, "probe.json");

        using (TreeLock.TryStarting(_home, _engine))
        {
            RecordLock.Take(record).Dispose();
        }

        Assert.Empty(seen);
        using (TreeLock.TryWriting(_home, _engine, "engine", TreeLock.Updating))
        {
            RecordLock.Take(record).Dispose();
            (await LandingHolds.TakeAsync(_home, Path.Combine(_home, "trees", "s-1"), CancellationToken.None))!.Dispose();
        }

        RecordLock.Take(record).Dispose();
        Assert.Equal([LockWaits.Records, "a tree's landing hold"], seen);
    }
}
