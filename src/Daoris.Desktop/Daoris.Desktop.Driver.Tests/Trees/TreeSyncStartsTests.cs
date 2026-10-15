using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A sync's list asks git for a checkout's line once, and its git starts are counted (PROC1d-i): the product's own starts are
/// most of the git starts of the slowest Process tests (PROC1a), and the list guessed the line three times a repository.
/// </summary>
/// <remarks>
/// The counter is <see cref="WorkingTree.Started"/>, an async local that is null in production, so classes running in
/// parallel never see each other's starts.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeSyncStartsTests : TreeSyncFixture
{
    private const int Branches = 3;

    [Fact]
    public async Task A_sync_plan_asks_for_the_line_once_and_its_starts_are_counted()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var guessed = 0;
        var trees = new SessionTrees(Home)
        {
            CheckoutLine = (at, ct) =>
            {
                Interlocked.Increment(ref guessed);
                return WorkingTree.DefaultBranchAsync(at, ct);
            },
        };
        for (var each = 0; each < Branches; each++)
        {
            var tree = await trees.OpenAsync(root, "engine", "aurora");
            await CommitAsync(tree.Path, $"work{each}.txt", "work\n", $"session {each}");
        }
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        Interlocked.Exchange(ref guessed, 0);

        var starts = new List<string>();
        WorkingTree.Started.Value = arguments => { lock (starts) starts.Add(string.Join(' ', arguments)); };
        try
        {
            await trees.SyncPlanAsync(Repositories(root), Nobody);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }

        var report = $"{starts.Count} git starts: {string.Join(" | ", starts.Where(each => each.StartsWith("symbolic-ref") || each.StartsWith("rev-parse --verify refs/heads/ma")))}";
        Assert.True(guessed == 1, $"the line was guessed {guessed} times; {report}");
        // 44 starts before the look (two guesses of the line), 42 after, over three session branches; PROC1d-ii and -iii cut the rest.
        Assert.True(starts.Count is > 0 and <= 42, report);
    }

    [Fact]
    public async Task Without_a_counter_nothing_is_recorded_and_a_start_still_answers()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");

        Assert.Null(WorkingTree.Started.Value);
        Assert.Equal("main", await WorkingTree.DefaultBranchAsync(root, CancellationToken.None));
    }
}
