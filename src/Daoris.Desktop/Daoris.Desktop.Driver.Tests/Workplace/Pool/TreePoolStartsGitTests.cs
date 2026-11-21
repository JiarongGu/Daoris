using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a pool act asks git, counted over real git: a line's end and a take read the file rules more than once (the sort of the ignored
/// tops, the plan, the toolchain's key, the check), and each reading lists, searches and reads the commit's files again, which the
/// act asks of git once.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreePoolStartsGitTests : IDisposable
{
    private PoolGitFixture? _fixture;

    public void Dispose() => _fixture?.Dispose();

    private async Task<(PoolGitFixture Fixture, TreePool Pool, KeptTree Slot)> MadeWithIgnoredTopAsync()
    {
        var fixture = _fixture = await PoolGitFixture.CreateAsync();
        var pool = fixture.Pool();
        var slot = await fixture.MadeAsync(pool, 1, "s-aaaa0001");
        Directory.CreateDirectory(Path.Combine(slot.Path, "obj"));
        await File.WriteAllTextAsync(Path.Combine(slot.Path, "obj", "built.dll"), "built\n");
        return (fixture, pool, slot);
    }

    private static void AssertEachObjectReadOnce(IReadOnlyList<GitLaunched> starts, int listings = 1)
    {
        var asked = starts.Select(start => string.Join(' ', GitLaunch.Command(start.Arguments))).ToList();
        var shown = string.Join("\n", asked);
        Assert.True(asked.Count(each => each.StartsWith("ls-tree", StringComparison.Ordinal)) <= listings, shown);
        Assert.True(asked.Count(each => each.StartsWith("grep", StringComparison.Ordinal)) <= 1, shown);
        Assert.True(asked.Count(each => each.StartsWith("cat-file", StringComparison.Ordinal)) <= 2, shown);
    }

    [Fact]
    public async Task A_line_end_asks_git_for_the_commits_files_once_for_the_sort_of_its_ignored_tops_and_for_the_toolchain_key()
    {
        var (fixture, pool, _) = await MadeWithIgnoredTopAsync();

        PoolDone ended;
        IReadOnlyList<GitLaunched> starts;
        using (var count = GitLaunch.Count())
        {
            ended = await pool.EndAsync(new PoolEnd(fixture.Site, 1, "quest-1", "session-1"));
            starts = count.Starts;
        }

        Assert.True(ended.Ok, ended.Refused ?? ended.Stuck);
        AssertEachObjectReadOnce(starts);
    }

    [Fact]
    public async Task A_take_asks_git_for_the_commits_files_once_for_its_judgement_its_plan_its_toolchain_and_its_check()
    {
        var (fixture, pool, _) = await MadeWithIgnoredTopAsync();
        var ended = await pool.EndAsync(new PoolEnd(fixture.Site, 1, "quest-1", "session-1"));
        Assert.True(ended.Ok, ended.Refused ?? ended.Stuck);

        PoolDone taken;
        IReadOnlyList<GitLaunched> starts;
        using (var count = GitLaunch.Count())
        {
            taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));
            starts = count.Starts;
        }

        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        AssertEachObjectReadOnce(starts, listings: 2);
    }
}
