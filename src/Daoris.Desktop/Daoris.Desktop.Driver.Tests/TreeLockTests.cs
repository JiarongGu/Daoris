using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR6's open window, closed (LEFT2): a repository's trees are held while a session starts in one, beside any other
/// start, and while bringing its branches up to date replays them, alone. Files only, so it is the fast half's.
/// </summary>
public sealed class TreeLockTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-tree-lock-" + Guid.NewGuid().ToString("N")[..8]);

    public TreeLockTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Two sessions starting in one repository do not race each other, so neither holds the other.</summary>
    [Fact]
    public void Two_starts_in_one_repository_hold_it_together()
    {
        using var first = TreeLock.TryStarting(_home, "aurora", "engine");
        using var second = TreeLock.TryStarting(_home, "aurora", "engine");

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
        var starting = TreeLock.TryStarting(_home, "aurora", "engine");
        Assert.NotNull(starting);

        Assert.Null(TreeLock.TryReplaying(_home, "aurora", "engine"));

        starting!.Dispose();
        using var replaying = TreeLock.TryReplaying(_home, "aurora", "engine");
        Assert.NotNull(replaying);
    }

    /// <summary>A start while a replay holds the repository is refused, and a second replay with it.</summary>
    [Fact]
    public void A_start_or_a_second_replay_is_refused_while_a_replay_holds_the_repository()
    {
        var replaying = TreeLock.TryReplaying(_home, "aurora", "engine");
        Assert.NotNull(replaying);

        Assert.Null(TreeLock.TryStarting(_home, "aurora", "engine"));
        Assert.Null(TreeLock.TryReplaying(_home, "aurora", "engine"));

        replaying!.Dispose();
        using var starting = TreeLock.TryStarting(_home, "aurora", "engine");
        Assert.NotNull(starting);
    }

    /// <summary>Another repository, or the same name in another workspace, is its own: a replay holds only its own.</summary>
    [Fact]
    public void Another_repository_or_workspace_is_held_on_its_own()
    {
        using var replaying = TreeLock.TryReplaying(_home, "aurora", "engine");

        using var game = TreeLock.TryStarting(_home, "aurora", "game");
        using var elsewhere = TreeLock.TryStarting(_home, "tools", "engine");
        Assert.NotNull(game);
        Assert.NotNull(elsewhere);
    }

    /// <summary>A registry names a repository without regard to case, and an unnamed workspace is the default one.</summary>
    [Fact]
    public void One_repository_is_one_lock_whatever_its_name_s_case_and_no_workspace_is_the_default()
    {
        using var replaying = TreeLock.TryReplaying(_home, null, "Engine");

        Assert.Null(TreeLock.TryStarting(_home, "default", "engine"));
        Assert.Null(TreeLock.TryStarting(_home, " ", "ENGINE"));
    }

    /// <summary>Under the home's own `locks/`, never the trees home, which the clean-up tidies and the tree list reads.</summary>
    [Fact]
    public void The_lock_lives_under_the_home_and_outside_the_trees_home()
    {
        var path = TreeLock.PathOf(_home, "aurora", "engine");

        Assert.StartsWith(Path.Combine(_home, TreeLock.Folder) + Path.DirectorySeparatorChar, path);
        Assert.False(path.StartsWith(new SessionTrees(_home).TreesRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }
}
