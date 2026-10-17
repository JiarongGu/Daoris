using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDRACE1 (D145 point 2, D173's FABRIC-END7 note): one landing of one session's tree at a time, whichever door lands it, in this
/// process and beside another, and a landing that waited knows when the landings it waited behind began. Files only, so it is the
/// fast half's.
/// </summary>
public sealed class LandingHoldsTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _scratch;
    private readonly string _home;
    private readonly string _tree;

    public LandingHoldsTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "landing-holds", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        _tree = Path.Combine(_scratch, "trees", "aurora", "engine", "s-1");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// A second landing of one tree waits for the first and takes the hold once it is let go, knowing when the first began; a landing
    /// of another tree goes beside it, and one that waited for nothing knows of nothing.
    /// </summary>
    [Fact]
    public async Task A_second_landing_of_one_tree_waits_for_the_first_and_knows_when_it_began()
    {
        var began = DateTimeOffset.UtcNow;
        var first = await LandingHolds.TakeAsync(_home, _tree);
        Assert.NotNull(first);
        Assert.Null(first.Since);

        var second = LandingHolds.TakeAsync(_home, _tree);
        Assert.True(SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, _tree) == 1, Bound), "the second waits for the first");
        Assert.False(second.IsCompleted);
        using (var beside = await LandingHolds.TakeAsync(_home, Path.Combine(_scratch, "trees", "aurora", "engine", "s-2")).WaitAsync(Bound))
        {
            Assert.Null(beside!.Since);
        }

        var letGo = DateTimeOffset.UtcNow;
        first.Dispose();
        using var took = await second.WaitAsync(Bound);
        Assert.NotNull(took!.Since);
        Assert.InRange(took.Since.Value, began, letGo);
        Assert.Equal(0, LandingHolds.Waiting(_home, _tree));
    }

    /// <summary>
    /// 🔴 A third landing that waited behind the second, which itself waited behind the first, knows when the first began: the landing
    /// the first recorded is what both later ones must find.
    /// </summary>
    [Fact]
    public async Task Landings_that_waited_one_behind_another_each_know_when_the_first_began()
    {
        var first = (await LandingHolds.TakeAsync(_home, _tree))!;
        var began = DateTimeOffset.UtcNow;
        var second = LandingHolds.TakeAsync(_home, _tree);
        var third = LandingHolds.TakeAsync(_home, _tree);
        Assert.True(SpinWait.SpinUntil(() => LandingHolds.Waiting(_home, _tree) == 2, Bound));

        first.Dispose();
        var next = await Task.WhenAny(second, third).WaitAsync(Bound);
        var heldNext = (await next)!;
        Assert.True(heldNext.Since <= began);
        heldNext.Dispose();
        using var last = await (next == second ? third : second).WaitAsync(Bound);
        Assert.True(last!.Since <= began, "the third knows when the first began, not when the second did");
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
        Assert.NotNull(took!.Since);
    }

    /// <summary>A landing waits only so long: past its bound it takes nothing, and says so as null, never "land anyway".</summary>
    [Fact]
    public async Task A_landing_held_past_its_bound_takes_nothing()
    {
        using var first = await LandingHolds.TakeAsync(_home, _tree);

        Assert.Null(await LandingHolds.TakeAsync(_home, _tree, within: TimeSpan.FromMilliseconds(200)).WaitAsync(Bound));
        Assert.Equal(0, LandingHolds.Waiting(_home, _tree));
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git"))
               && !File.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
