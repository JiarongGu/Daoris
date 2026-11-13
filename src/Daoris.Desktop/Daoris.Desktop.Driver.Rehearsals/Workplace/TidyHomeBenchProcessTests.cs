using System.Diagnostics;
using Daoris.Driver;
using Xunit.Abstractions;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The look's tidy over a home shaped like a working machine's: thirty registered repositories, two dozen session trees spread over
/// them, and one repository holding twenty more. A pass over it, warm and with nothing moved, starts no git in a repository whose
/// refs, trees and records did not move since the last pass, and takes seconds, not minutes.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TidyHomeBenchProcessTests(ITestOutputHelper output) : BranchTidyingFixture
{
    private const int Repositories = 30;
    private const int Spread = 22;
    private const int Crowded = 20;

    [Fact]
    public async Task A_warm_pass_over_a_home_of_thirty_repositories_starts_git_only_where_something_moved()
    {
        var trees = new SessionTrees(_home);
        var roots = new List<string>();
        for (var at = 0; at < Repositories; at++)
        {
            var name = $"repo-{at:00}";
            var root = await RepositoryAsync(name);
            _ledger.Register(name, root);
            roots.Add(root);
        }

        var moved = "";
        for (var at = 0; at < Spread; at++)
        {
            var tree = await TreeAsync(trees, roots[at]);
            if (at == 3) moved = tree;
        }

        for (var at = 0; at < Crowded; at++) await TreeAsync(trees, roots[^1]);

        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };
        // The first pass writes the records it learns; the second reads them as they now stand; the third is a warm pass.
        var first = await TimedAsync(tidying);
        var settling = await TimedAsync(tidying);
        var warm = await TimedAsync(tidying);
        output.WriteLine($"first pass {first.Ms} ms, {first.Starts.Count} starts\nsettling pass {settling.Ms} ms, {settling.Starts.Count} starts\n"
            + $"warm pass {warm.Ms} ms, {warm.Starts.Count} starts");

        // One repository moves: a commit on its session branch. Only it is read again.
        await CommitAsync(moved, "more.txt", "more work", "more work");
        var after = await TimedAsync(tidying);
        output.WriteLine($"after one repository moved {after.Ms} ms, {after.Starts.Count} starts in {after.Starts.Select(start => start.Folder).Distinct().Count()} folders");

        Assert.Empty(warm.Starts);
        Assert.True(warm.Ms < 3_000, $"{warm.Ms} ms");
        Assert.True(after.Starts.Count <= 12, $"{after.Starts.Count} starts");
    }

    private async Task<string> TreeAsync(SessionTrees trees, string root)
    {
        var tree = await trees.OpenAsync(root, Path.GetFileName(root), "default");
        await CommitAsync(tree.Path, $"{Guid.NewGuid():N}.txt", "the session's work", "the work");
        return tree.Path;
    }

    private sealed record Start(string Folder, string Arguments);

    private async Task<(long Ms, List<Start> Starts)> TimedAsync(BranchTidying tidying)
    {
        var starts = new List<Start>();
        var clock = Stopwatch.StartNew();
        using (ProcessStarts.Within(info =>
        {
            if (Path.GetFileNameWithoutExtension(info.FileName).Equals("git", StringComparison.OrdinalIgnoreCase))
            {
                var arguments = info.ArgumentList.SkipWhile(each => each is "-c" or "core.longpaths=true" or "--no-optional-locks");
                lock (starts) starts.Add(new Start(info.WorkingDirectory, string.Join(' ', arguments)));
            }

            return Process.Start(info);
        }))
        {
            await tidying.LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);
        }

        return (clock.ElapsedMilliseconds, starts);
    }
}
