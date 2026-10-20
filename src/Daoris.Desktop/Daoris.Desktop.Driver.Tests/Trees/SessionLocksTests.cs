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

    private async Task<string> MakeRepositoryAsync()
    {
        var root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# engine\n");
        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "-m", "first");
        return root;
    }
}
