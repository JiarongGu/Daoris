using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDRACE1 (D145 point 2, D173's FABRIC-END7 note and its review): one landing of one session's tree at a time, and one push of one
/// landed branch, whichever door, in this process and beside another; a landing that waited reads what the one it waited behind kept.
/// Files only, so it is the fast half's.
/// </summary>
public sealed class LandingHoldsTests : IDisposable
{
    // TESTCLOCK1: the waits are on the hold's own count, so the bound elapses only on a real failure.
    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(2);

    private static readonly LandingAttempt Landed = new("feature/own", "3f2a9c1e5b7d4a6f8e0c2b4d6f8a0c2e4b6d8f0a", AcceptedBy.Auto)
    {
        Plugin = "example.lands",
        Said = new PluginLanding("example.lands", Pushed: false, PullRequest: null, "gh is not signed in.", Failed: true),
    };

    private readonly string _scratch;
    private readonly string _home;
    private readonly string _tree;

    public LandingHoldsTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "landing-holds", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        _tree = Path.Combine(_scratch, "trees", "aurora", "engine", "s-1");
        Directory.CreateDirectory(_home);
        Directory.CreateDirectory(_tree);
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    /// <summary>
    /// A second landing of one tree waits for the first and takes the hold once it is let go, reading what the first kept; a landing of
    /// another tree goes beside it, and one that waited for nothing reads nothing, whatever an older attempt kept.
    /// </summary>
    [Fact]
    public async Task A_second_landing_of_one_tree_waits_for_the_first_and_reads_what_it_kept()
    {
        var first = await LandingHolds.TakeAsync(_home, _tree);
        Assert.NotNull(first);
        Assert.False(first.Waited);
        Assert.Null(first.Before);

        var second = LandingHolds.TakeAsync(_home, _tree);
        Assert.True(SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, _tree) == 1, Bound), "the second waits for the first");
        Assert.False(second.IsCompleted);
        var other = Path.Combine(_scratch, "trees", "aurora", "engine", "s-2");
        Directory.CreateDirectory(other);
        using (var beside = await LandingHolds.TakeAsync(_home, other).WaitAsync(Bound))
        {
            Assert.False(beside!.Waited);
        }

        first.Keep(Landed);
        first.Dispose();
        using (var took = await second.WaitAsync(Bound))
        {
            Assert.True(took!.Waited);
            Assert.Equal(Landed, took.Before);
        }

        Assert.Equal(0, LandingHolds.Waiting(_home, _tree));
        // LANDRACE1's re-check: a key nobody holds or waits for keeps no gate in this process.
        Assert.False(LandingHolds.Gated(_home, _tree));
        using var later = await LandingHolds.TakeAsync(_home, _tree);
        Assert.Null(later!.Before);
        Assert.True(LandingHolds.Gated(_home, _tree));
    }

    /// <summary>
    /// 🔴 A third landing that waited behind the second, which waited behind the first and kept nothing of its own, reads what the first
    /// kept: the landing the first made is what both later ones must find. A later one that keeps its own outcome replaces it.
    /// </summary>
    [Fact]
    public async Task Landings_that_waited_one_behind_another_read_the_last_kept_outcome()
    {
        var first = (await LandingHolds.TakeAsync(_home, _tree))!;
        var second = LandingHolds.TakeAsync(_home, _tree);
        var third = LandingHolds.TakeAsync(_home, _tree);
        Assert.True(SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, _tree) == 2, Bound));

        first.Keep(Landed);
        first.Dispose();
        var next = await Task.WhenAny(second, third).WaitAsync(Bound);
        var heldNext = (await next)!;
        Assert.Equal(Landed, heldNext.Before);
        heldNext.Dispose();
        using var last = await (next == second ? third : second).WaitAsync(Bound);
        Assert.Equal(Landed, last!.Before);
    }

    /// <summary>
    /// LANDRACE1's review, finding 2: the hold is the tree's, not its spelling. A second name for the same folder (here the long-path
    /// form; a junction or a <c>subst</c> drive likewise) waits on the same hold, so one tree is never landed twice under two names.
    /// </summary>
    [Fact]
    public async Task Two_names_for_one_tree_take_one_hold()
    {
        if (!OperatingSystem.IsWindows()) return;
        var alias = @"\\?\" + _tree;
        Assert.True(Directory.Exists(alias));

        using var first = await LandingHolds.TakeAsync(_home, _tree);
        Assert.Null(await LandingHolds.TakeAsync(_home, alias, within: TimeSpan.FromMilliseconds(200)).WaitAsync(Bound));

        var linked = Path.Combine(_scratch, "linked");
        try
        {
            Directory.CreateSymbolicLink(linked, _tree);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return; // This account may not make links: the long-path name stands for every alias.
        }

        Assert.Null(await LandingHolds.TakeAsync(_home, linked, within: TimeSpan.FromMilliseconds(200)).WaitAsync(Bound));
    }

    /// <summary>
    /// LANDRACE1's review, finding 4: one push of a landed branch at a time, the landing's plugin step and a hand-off alike, and the
    /// one that waited reads the push the other kept. Another branch of the same repository goes beside it.
    /// </summary>
    [Fact]
    public async Task One_push_of_a_landed_branch_at_a_time()
    {
        var root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        var pushing = (await LandingHolds.BranchAsync(_home, root, "feature/x"))!;

        var handing = LandingHolds.BranchAsync(_home, root, "feature/x");
        Assert.True(SpinWait.SpinUntil(() => LandingHolds.WaitingOn(_home, root, "feature/x") == 1, Bound), "the hand-off waits for the push");
        using (var beside = await LandingHolds.BranchAsync(_home, root, "feature/y").WaitAsync(Bound))
        {
            Assert.False(beside!.Waited);
        }

        pushing.Keep(Landed);
        pushing.Dispose();
        using var handed = await handing.WaitAsync(Bound);
        Assert.Equal(Landed, handed!.Before);
    }

    /// <summary>
    /// LANDRACE1's re-check: a branch is held by its repository's common git folder, so a linked worktree (whose <c>.git</c> is a file
    /// naming its git folder, which names the common one in <c>commondir</c>) and the main checkout take one hold for the same branch,
    /// whether the link is written absolute or relative. A landing names the repository by the main checkout and a terminal's
    /// hand-off by the registry's, which may be linked.
    /// </summary>
    [Fact]
    public async Task A_linked_worktree_and_its_main_checkout_take_one_branch_hold()
    {
        var (main, linked, relative) = LinkedWorktrees();
        using var pushing = await LandingHolds.BranchAsync(_home, main, "feature/x");

        Assert.Null(await LandingHolds.BranchAsync(_home, linked, "feature/x", within: TimeSpan.FromMilliseconds(200)).WaitAsync(Bound));
        Assert.Null(await LandingHolds.BranchAsync(_home, relative, "feature/x", within: TimeSpan.FromMilliseconds(200)).WaitAsync(Bound));
        using var other = await LandingHolds.BranchAsync(_home, linked, "feature/y").WaitAsync(Bound);
        Assert.False(other!.Waited);
    }

    /// <summary>LANDRACE1's re-check: two spellings of a linked worktree take one branch hold, as two of a tree take one tree hold.</summary>
    [Fact]
    public async Task Two_names_for_one_linked_worktree_take_one_branch_hold()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (_, linked, _) = LinkedWorktrees();
        using var pushing = await LandingHolds.BranchAsync(_home, linked, "feature/x");

        Assert.Null(await LandingHolds.BranchAsync(_home, @"\\?\" + linked, "feature/x", within: TimeSpan.FromMilliseconds(200)).WaitAsync(Bound));
    }

    /// <summary>
    /// A repository's main checkout and two linked worktrees of it, written as git writes them: a <c>.git</c> file naming the worktree's
    /// git folder (absolute, and relative), and that folder's <c>commondir</c> naming the common one.
    /// </summary>
    private (string Main, string Linked, string Relative) LinkedWorktrees()
    {
        var main = Path.Combine(_scratch, "engine");
        var common = Path.Combine(main, ".git");
        string Link(string name, bool absolute)
        {
            var own = Path.Combine(common, "worktrees", name);
            Directory.CreateDirectory(own);
            File.WriteAllText(Path.Combine(own, "commondir"), "../..\n");
            var linked = Path.Combine(_scratch, name);
            Directory.CreateDirectory(linked);
            File.WriteAllText(Path.Combine(linked, ".git"), $"gitdir: {(absolute ? own : Path.GetRelativePath(linked, own)).Replace('\\', '/')}\n");
            return linked;
        }

        Directory.CreateDirectory(common);
        return (main, Link("engine-linked", absolute: true), Link("engine-relative", absolute: false));
    }

    /// <summary>
    /// Beside another process: the hold is a file under the home's <c>locks/</c>, opened alone, so a landing waits while another
    /// process holds it, and takes it once that process lets go.
    /// </summary>
    [Fact]
    public async Task A_landing_waits_while_another_process_holds_the_tree()
    {
        var path = LandingHolds.PathOf(_home, _tree);
        Assert.StartsWith(DaorisHome.Entry(_home, HomeEntry.Locks) + Path.DirectorySeparatorChar, path, StringComparison.Ordinal);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var other = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var waiting = LandingHolds.TakeAsync(_home, _tree);
        Assert.True(SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, _tree) == 1, Bound));
        await Task.Delay(100);
        Assert.False(waiting.IsCompleted);

        await other.DisposeAsync();
        using var took = await waiting.WaitAsync(Bound);
        Assert.True(took!.Waited);
        Assert.Null(took.Before);
    }

    /// <summary>A landing waits only so long: past its bound it takes nothing, and says so as null, never "land anyway".</summary>
    [Fact]
    public async Task A_landing_held_past_its_bound_takes_nothing()
    {
        using var first = await LandingHolds.TakeAsync(_home, _tree);

        Assert.Null(await LandingHolds.TakeAsync(_home, _tree, within: TimeSpan.FromMilliseconds(200)).WaitAsync(Bound));
        Assert.Equal(0, LandingHolds.Waiting(_home, _tree));

        // The one that gave up let go of what it took: once the first lets go too, no gate is kept, and the tree takes at once.
        first!.Dispose();
        Assert.False(LandingHolds.Gated(_home, _tree));
        using var next = await LandingHolds.TakeAsync(_home, _tree, within: TimeSpan.FromMilliseconds(200)).WaitAsync(Bound);
        Assert.False(next!.Waited);
    }

    /// <summary>
    /// LANDHOLD2: a door that waits for a tree's hold says so before it waits and says it stopped once it has the hold; one that waits
    /// for nothing says nothing; one that gives up at its bound says it stopped too. Branch holds say the same.
    /// </summary>
    [Fact]
    public async Task A_door_that_waits_says_so_once_and_says_when_it_stops_and_one_that_waits_for_nothing_says_nothing()
    {
        var said = new List<bool>();
        using var told = LandingHoldNotice.Told(waiting => { lock (said) said.Add(waiting); });

        using (var alone = await LandingHolds.TakeAsync(_home, _tree)) Assert.False(alone!.Waited);
        Assert.Empty(said);

        var first = (await LandingHolds.TakeAsync(_home, _tree))!;
        var second = LandingHolds.TakeAsync(_home, _tree);
        Assert.True(SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, _tree) == 1, Bound));
        Assert.Equal([true], Snapshot(said));
        first.Dispose();
        using (await second.WaitAsync(Bound)) { }
        Assert.Equal([true, false], Snapshot(said));

        said.Clear();
        using var holding = await LandingHolds.TakeAsync(_home, _tree);
        Assert.Null(await LandingHolds.TakeAsync(_home, _tree, within: TimeSpan.FromMilliseconds(200)).WaitAsync(Bound));
        Assert.Equal([true, false], Snapshot(said));

        said.Clear();
        var root = Path.Combine(_scratch, "engine");
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        using var pushing = await LandingHolds.BranchAsync(_home, root, "feature/x");
        Assert.Null(await LandingHolds.BranchAsync(_home, root, "feature/x", within: TimeSpan.FromMilliseconds(200)).WaitAsync(Bound));
        Assert.Equal([true, false], Snapshot(said));
    }

    /// <summary>LANDHOLD2: a callback is for the call that handed it: a door outside it, and one after it is disposed, tell nobody.</summary>
    [Fact]
    public async Task A_waiting_door_outside_the_call_that_handed_a_callback_tells_nobody()
    {
        var said = new List<bool>();
        var first = (await LandingHolds.TakeAsync(_home, _tree))!;
        Task<LandingHold?> second;
        using (LandingHoldNotice.Told(waiting => { lock (said) said.Add(waiting); })) { }
        second = LandingHolds.TakeAsync(_home, _tree);
        Assert.True(SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, _tree) == 1, Bound));
        first.Dispose();
        using (await second.WaitAsync(Bound)) { }
        Assert.Empty(said);
    }

    private static bool[] Snapshot(List<bool> said)
    {
        lock (said) return [.. said];
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot() => GitSeed.RepoRoot();
}
