using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREELOCK1 (D51's note): a session's tree locked in its name while it holds it, and what git then refuses. REAL git, since what
/// a lock stops is git's to say: <c>git worktree prune</c> keeps a locked tree whose folder went, and <c>git worktree remove</c>
/// without <c>--force</c> refuses it.
/// </summary>
/// <remarks>The driver's own hold and let-go around a real run are <c>SessionTreeLockTickTests</c>'.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class SessionLocksTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;

    public SessionLocksTests()
    {
        _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "session-locks", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_scratch)) TreeSetAside.DeleteNeverFollowing(_scratch);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>🔴 The row's proof in git: held, the tree survives a prune and an unforced remove; let go, the remove takes it.</summary>
    [Fact]
    public async Task A_held_tree_is_refused_to_a_prune_and_an_unforced_remove_until_its_session_lets_it_go()
    {
        var root = await MakeRepositoryAsync();
        var tree = (await new SessionTrees(_home).OpenAsync(root, "engine", "aurora")).Path;
        Assert.Null(await LockOfAsync(root, tree));

        Assert.Null(await SessionLocks.HoldAsync(tree, "a1b2c3d4", CancellationToken.None));
        Assert.Equal("daoris session a1b2c3d4", await LockOfAsync(root, tree));

        var removed = await RunAsync(root, "worktree", "remove", tree);
        Assert.NotEqual(0, removed.ExitCode);
        Assert.Contains("locked", removed.Stderr);
        Assert.True(Directory.Exists(tree));

        // A prune takes a tree whose folder is gone, unless it is locked: the folder is moved away and back.
        var away = tree + "-away";
        Directory.Move(tree, away);
        await GitAsync(root, "worktree", "prune");
        Assert.Equal("daoris session a1b2c3d4", await LockOfAsync(root, tree));
        Directory.Move(away, tree);

        // Another session's let-go lifts nothing; its own does.
        Assert.False(await SessionLocks.LetGoAsync(tree, "ffffffff", CancellationToken.None));
        Assert.Equal("daoris session a1b2c3d4", await LockOfAsync(root, tree));
        Assert.True(await SessionLocks.LetGoAsync(tree, "a1b2c3d4", CancellationToken.None));
        Assert.Null(await LockOfAsync(root, tree));

        Assert.Equal(0, (await RunAsync(root, "worktree", "remove", tree)).ExitCode);
        Assert.False(Directory.Exists(tree));
    }

    [Fact]
    public async Task A_later_session_takes_an_earlier_ones_lock_over_and_a_persons_lock_is_never_changed()
    {
        var root = await MakeRepositoryAsync();
        var tree = (await new SessionTrees(_home).OpenAsync(root, "engine", "aurora")).Path;

        Assert.Null(await SessionLocks.HoldAsync(tree, "11111111", CancellationToken.None));
        Assert.Null(await SessionLocks.HoldAsync(tree, "22222222", CancellationToken.None));
        Assert.Equal("daoris session 22222222", await LockOfAsync(root, tree));
        Assert.Null(await SessionLocks.HoldAsync(tree, "22222222", CancellationToken.None));
        Assert.Equal("daoris session 22222222", await LockOfAsync(root, tree));

        await GitAsync(root, "worktree", "unlock", tree);
        await GitAsync(root, "worktree", "lock", "--reason", "mine, keep it", tree);
        Assert.Null(await SessionLocks.HoldAsync(tree, "33333333", CancellationToken.None));
        Assert.False(await SessionLocks.LetGoAsync(tree, "33333333", CancellationToken.None));
        Assert.Equal("mine, keep it", await LockOfAsync(root, tree));
    }

    /// <summary>The look's half: a lock whose session no longer holds its tree goes; a held one and a person's stay.</summary>
    [Fact]
    public async Task The_look_lets_go_of_a_session_lock_no_session_holds_and_keeps_every_other_lock()
    {
        var root = await MakeRepositoryAsync();
        var trees = new SessionTrees(_home);
        var held = (await trees.OpenAsync(root, "engine", "aurora")).Path;
        var ended = (await trees.OpenAsync(root, "engine", "aurora")).Path;
        var persons = (await trees.OpenAsync(root, "engine", "aurora")).Path;
        await SessionLocks.HoldAsync(held, "11111111", CancellationToken.None);
        await SessionLocks.HoldAsync(ended, "22222222", CancellationToken.None);
        await GitAsync(root, "worktree", "lock", "--reason", "mine", persons);

        var let = await trees.LetGoUnheldLocksAsync(root, "engine", "aurora", _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { held }));

        Assert.Equal(Path.GetFullPath(ended), Path.GetFullPath(Assert.Single(let)), ignoreCase: true);
        Assert.Equal("daoris session 11111111", await LockOfAsync(root, held));
        Assert.Null(await LockOfAsync(root, ended));
        Assert.Equal("mine", await LockOfAsync(root, persons));
    }

    [Fact]
    public async Task The_look_lets_go_of_nothing_where_the_service_did_not_answer()
    {
        var root = await MakeRepositoryAsync();
        var trees = new SessionTrees(_home);
        var tree = (await trees.OpenAsync(root, "engine", "aurora")).Path;
        await SessionLocks.HoldAsync(tree, "11111111", CancellationToken.None);

        var let = await trees.LetGoUnheldLocksAsync(
            root, "engine", "aurora", _ => throw new DriverException("the service did not answer"));

        Assert.Empty(let);
        Assert.Equal("daoris session 11111111", await LockOfAsync(root, tree));
    }

    /// <summary>The reason git lists the tree locked for; null where it is not locked, or listed bare.</summary>
    internal static async Task<string?> LockOfAsync(string root, string tree)
    {
        var entries = TreeWorktrees.Parse(await GitAsync(root, "worktree", "list", "--porcelain"));
        var entry = Assert.Single(entries, each => TreeGit.SamePath(each.Path, tree));
        return entry.Locked ? entry.LockReason ?? "" : null;
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("declined")]
    [InlineData("stopped")]
    [InlineData("failed")]
    [InlineData("stood-down")]
    public async Task A_terminal_move_releases_the_sessions_own_lock_after_a_park(string state)
    {
        var root = await MakeRepositoryAsync();
        var tree = (await new SessionTrees(_home).OpenAsync(root, "engine", "aurora")).Path;
        using var ledger = new StandInLedger();
        ledger.Publish("q1", "engine");
        ledger.Park("s1", "q1", tree);
        using var service = ledger.Client();
        await SessionLocks.HoldAsync(tree, "s1", CancellationToken.None);

        await SessionMoves.ResolveAsync(_ => false, service, "s1", state, note: null);

        Assert.Null(await LockOfAsync(root, tree));
        Assert.Equal(0, (await RunAsync(root, "worktree", "remove", tree)).ExitCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_sweep_releases_an_ended_lock_at_either_place_but_keeps_a_parked_tree(bool oldPlace)
    {
        var root = await MakeRepositoryAsync();
        var trees = new SessionTrees(_home);
        var ended = (await trees.OpenAsync(root, "engine", "aurora")).Path;
        var parked = (await trees.OpenAsync(root, "engine", "aurora")).Path;
        if (oldPlace)
        {
            ended = await ToOldPlaceAsync(root, ended);
            parked = await ToOldPlaceAsync(root, parked);
        }

        using var ledger = new StandInLedger();
        ledger.Publish("q1", "engine");
        ledger.Publish("q2", "engine");
        ledger.Driven("s1", "q1", ended, state: "completed");
        ledger.Park("s2", "q2", parked);
        using var service = ledger.Client();
        await SessionLocks.HoldAsync(ended, "s1", CancellationToken.None);
        await SessionLocks.HoldAsync(parked, "s2", CancellationToken.None);

        var let = await trees.LetGoUnheldLocksAsync(root, "engine", "aurora", async ct =>
            (await service.ActiveSessionsAsync(ct)).Select(session => session.Tree!).ToHashSet(StringComparer.OrdinalIgnoreCase));

        Assert.True(TreeGit.SamePath(ended, Assert.Single(let)));
        Assert.Null(await LockOfAsync(root, ended));
        Assert.Equal("daoris session s2", await LockOfAsync(root, parked));
        Assert.True((await trees.RemoveAsync(ended, force: true)).Removed);
        Assert.False((await trees.RemoveAsync(parked, force: true)).Removed);
    }

    [Theory]
    [InlineData(false, "session")]
    [InlineData(true, "session")]
    [InlineData(false, "path")]
    [InlineData(true, "path")]
    [InlineData(false, "branch")]
    [InlineData(true, "branch")]
    public async Task A_forced_removal_releases_a_stale_ended_lock(bool oldPlace, string by)
    {
        var root = await MakeRepositoryAsync();
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        var tree = opened.Path;
        if (oldPlace) tree = await ToOldPlaceAsync(root, tree);
        using var ledger = new StandInLedger();
        ledger.Publish("q1", "engine");
        ledger.Driven("s1", "q1", tree, state: "completed");
        using var service = ledger.Client();
        await SessionLocks.HoldAsync(tree, "s1", CancellationToken.None);

        ledger.Register("engine", root);
        var removed = by == "path"
            ? await trees.RemoveAsync(tree, force: true, service: service)
            : await new SessionBranchDiscard(trees).DiscardNamedAsync(service,
                by == "branch" ? opened.Branch : "s1", "engine", force: true);

        Assert.True(removed.Removed, removed.Message);
        Assert.False(Directory.Exists(tree));
    }

    private async Task<string> ToOldPlaceAsync(string root, string tree)
    {
        var old = DaorisHome.Places(_home, HomeEntry.Trees, "aurora", "engine", Path.GetFileName(tree))[^1];
        Directory.CreateDirectory(Path.GetDirectoryName(old)!);
        Assert.Equal(0, (await RunAsync(root, "worktree", "move", tree, old)).ExitCode);
        return old;
    }

    [Theory]
    [InlineData("working", false)]
    [InlineData("working", true)]
    [InlineData("awaiting-person", false)]
    [InlineData("awaiting-person", true)]
    public async Task A_forced_path_removal_keeps_a_tree_a_running_or_parked_session_holds(string state, bool oldPlace)
    {
        var root = await MakeRepositoryAsync();
        var trees = new SessionTrees(_home);
        var tree = (await trees.OpenAsync(root, "engine", "aurora")).Path;
        if (oldPlace) tree = await ToOldPlaceAsync(root, tree);
        using var ledger = new StandInLedger();
        ledger.Publish("q1", "engine");
        ledger.Driven("s1", "q1", tree, state: state);
        using var service = ledger.Client();
        // A stale lock owner's end is not enough: a later live session in the tree still keeps it.
        ledger.Publish("q2", "engine");
        ledger.Driven("s0", "q2", tree, state: "completed");
        await SessionLocks.HoldAsync(tree, "s0", CancellationToken.None);

        var removed = await trees.RemoveAsync(tree, force: true, service: service);

        Assert.False(removed.Removed);
        Assert.True(Directory.Exists(tree));
        Assert.Equal("daoris session s0", await LockOfAsync(root, tree));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_helper_wait_or_setup_continuation_keeps_its_lock(bool helper)
    {
        var root = await MakeRepositoryAsync();
        var tree = (await new SessionTrees(_home).OpenAsync(root, "engine", "aurora")).Path;
        using var ledger = new StandInLedger();
        ledger.Publish("q1", "engine");
        ledger.Driven("s1", "q1", tree);
        using var service = ledger.Client();
        await SessionLocks.HoldAsync(tree, "s1", CancellationToken.None);

        await service.AdvanceAsync("s1", "completed",
            Noted.Of(helper ? NoteCodes.EndedAwaitsHelper : NoteCodes.EndedSetupChanged, "continues"));

        Assert.Equal("daoris session s1", await LockOfAsync(root, tree));
    }

    private async Task<string> MakeRepositoryAsync()
    {
        return await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
    }
}
