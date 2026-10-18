using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The ref watch over the machine's own watcher and real git (FABRIC-END3, the design §A5's open questions and canary 10's first
/// numbers): each of git's ref writes judged once, within a second of git's exit, by the real end keeper; a ref deleted is judged and is
/// no end; and what the watchers hold for the most repositories watched. A handful of git operations on one scratch repository under
/// the repository's <c>_fixtures/</c>, never a load and never the owner's.
/// </summary>
/// <remarks>Real watchers, whose events arrive on the machine's own time, so a class of its own, run one at a time.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class RefWatchProcessTests(ITestOutputHelper output) : LandedFixture
{
    /// <summary>How long after git's exit its move must be judged: the quiet window, and the rest for a loaded machine.</summary>
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(1);

    private readonly List<(long Began, long Ended, int Written)> _judged = [];

    private (long Began, long Ended, int Written)[] Judged()
    {
        lock (_judged) return [.. _judged];
    }

    /// <summary>
    /// A commit in a session's tree, its ref deleted and made again, a merge onto the line, a <c>pack-refs</c> and the tree's switch are
    /// each one judgement within a second of git's exit, by the real keeper: the merge writes the branch's end, the deletion writes none.
    /// </summary>
    [Fact]
    public async Task Each_ref_write_is_judged_once_within_a_second_and_a_deletion_is_no_end()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);
        RefRepository[] engine = [new("engine", "aurora", root)];
        await using var watch = new RefWatch(Home, _ => Task.FromResult<IReadOnlyList<RefRepository>>(engine), async (repository, ct) =>
        {
            var began = Stopwatch.GetTimestamp();
            var written = await trees.KeepEndsAsync(repository.Root, repository.Repository, repository.Workspace, ct);
            lock (_judged) _judged.Add((began, Stopwatch.GetTimestamp(), written));
            return written;
        });
        watch.Start();
        await Poll.Until(() => watch.Watched.Count == 1, () => "the repository was never watched", Poll.Patience);
        await Task.Delay(ChangeWatch.Cap);
        var before = Judged().Length;

        await OnceAsync("a commit in the tree", () => CommitAsync(opened.Path, "a.txt", "a\n", "work"));
        var tip = (await GitAsync(opened.Path, "rev-parse", "HEAD")).Trim();
        await OnceAsync("the branch deleted", () => GitAsync(root, "update-ref", "-d", $"refs/heads/{opened.Branch}"));
        Assert.Null(Entry().Held);
        Assert.Equal(0, Judged()[^1].Written);
        await OnceAsync("the branch made again", () => GitAsync(root, "update-ref", $"refs/heads/{opened.Branch}", tip));
        Assert.Null(Entry().Held);

        await OnceAsync("a merge onto the line", () => GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "merge the work", opened.Branch));
        var held = Entry().Held;
        Assert.NotNull(held);
        Assert.Equal((EndBy.Merged, tip), (held.By, held.Tip));
        Assert.Equal(1, Judged()[^1].Written);

        await OnceAsync("pack-refs", () => GitAsync(root, "pack-refs", "--all"));
        await OnceAsync("the tree's switch", () => GitAsync(opened.Path, "switch", "--quiet", "--detach"));
        Assert.Equal(before + 6, Judged().Length);

        GrownBranch Entry() => new SessionBranches(Home).All().Single(entry => entry.Branch == opened.Branch);
    }

    /// <summary>
    /// Canary 10's numbers for the ref watch: the process's handles and nonpaged memory before, with <see cref="ChangeWatch.MostRoots"/>
    /// repositories' common dirs watched (subfolders and all), and once disposed. Folders, not repositories: a watcher's cost is its folder's.
    /// </summary>
    [Fact]
    public async Task Handles_and_nonpaged_memory_for_the_most_repositories_watched_are_measured_and_given_back()
    {
        const int Repositories = ChangeWatch.MostRoots;
        RefRepository[] watched =
        [
            .. Enumerable.Range(1, Repositories).Select(each =>
            {
                var root = Path.Combine(Scratch, $"repo{each:00}");
                Directory.CreateDirectory(Path.Combine(root, ".git", "refs", "heads"));
                return new RefRepository($"repo{each:00}", "aurora", root);
            }),
        ];
        using var self = Process.GetCurrentProcess();
        var before = Measure(self);

        var watch = new RefWatch(Home, _ => Task.FromResult<IReadOnlyList<RefRepository>>(watched), (_, _) => Task.FromResult(0));
        watch.Start();
        await Poll.Until(() => watch.Watched.Count == Repositories, () => $"{watch.Watched.Count} watched", Poll.Patience);
        var open = Measure(self);

        await watch.DisposeAsync();
        var after = Measure(self);

        output.WriteLine(
            $"handles {before.Handles} -> {open.Handles} with {Repositories} repositories watched -> {after.Handles} disposed; "
            + $"nonpaged {before.Nonpaged} -> {open.Nonpaged} -> {after.Nonpaged} bytes; "
            + $"{(open.Handles - before.Handles) / (double)Repositories:0.##} handles and "
            + $"{(open.Nonpaged - before.Nonpaged) / (double)Repositories:0} nonpaged bytes a repository");
        Assert.True(open.Handles - before.Handles <= 8 * Repositories, $"{open.Handles - before.Handles} handles for {Repositories} repositories");
        Assert.True(after.Handles < open.Handles, $"{after.Handles} handles after, {open.Handles} open");
    }

    /// <summary>One git write: judged once, its judgement begun within <see cref="Within"/> of git's exit, and none after it.</summary>
    private async Task OnceAsync(string what, Func<Task> write)
    {
        var before = Judged().Length;
        await write();
        var exited = Stopwatch.GetTimestamp();
        await Poll.Until(() => Judged().Length > before, () => $"{what} was never judged", Poll.Patience);
        var (began, ended, written) = Judged()[before];
        await Task.Delay(ChangeWatch.Cap + ChangeWatch.Quiet);

        var waited = Stopwatch.GetElapsedTime(exited, began);
        output.WriteLine(
            $"{what}: judged {waited.TotalMilliseconds:0} ms after git exited, for {Stopwatch.GetElapsedTime(began, ended).TotalMilliseconds:0} ms, "
            + $"{written} written, {Judged().Length - before} judgement(s)");
        Assert.Equal(before + 1, Judged().Length);
        Assert.True(waited < Within, $"{what} judged {waited.TotalMilliseconds:0} ms after git exited");
    }

    private static (int Handles, long Nonpaged) Measure(Process self)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        self.Refresh();
        return (self.HandleCount, self.NonpagedSystemMemorySize64);
    }
}
