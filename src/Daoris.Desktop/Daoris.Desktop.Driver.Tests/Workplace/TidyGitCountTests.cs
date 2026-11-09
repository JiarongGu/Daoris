using Daoris.Driver;
using Xunit.Abstractions;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The look's tidy asks git of the repository once, not once per tree: on a home of two dozen trees, asking per tree made 32 git starts a
/// look, 5.5 to 22 s where one start costs a third of a second to two seconds, 24 of them each tree's branch asked of the tree. Each
/// tree's branch is read from the one list of the repository's working trees, and the checkout's top, its trees and its branches by
/// date are each read once a look. What is asked per branch is the end's judgement of its own commits, in the process holding the
/// home's lease alone.
/// </summary>
/// <remarks>
/// Every start through the gate is wrapped by <see cref="ProcessStarts.Within"/> while a look runs, the second of two, as a running
/// driver's look reads at every pace after its first; the test's own git runs outside it.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TidyGitCountTests(ITestOutputHelper output) : BranchTidyingFixture
{
    [Fact]
    public async Task A_look_asks_git_of_the_repository_once_however_many_trees_it_holds()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        for (var at = 0; at < 2; at++) await TreeWithWorkAsync(trees, root);
        _ledger.Register("engine", root);
        var few = await WarmLookAsync();

        for (var at = 0; at < 4; at++) await TreeWithWorkAsync(trees, root);
        var many = await WarmLookAsync();

        var report = $"two trees: {few.Count} starts\n{string.Join('\n', few)}\nsix trees: {many.Count} starts\n{string.Join('\n', many)}";
        output.WriteLine(report);
        Assert.DoesNotContain("symbolic-ref --short -q HEAD", many);
        Assert.Single(many, each => each == "rev-parse --show-toplevel");
        // The stages' reading, and the judgement's own, which it takes so a later look's guard reads git afresh.
        Assert.True(many.Count(each => each == "worktree list --porcelain") <= 2, report);
        Assert.All(many.Where(each => each.StartsWith("for-each-ref", StringComparison.Ordinal)).GroupBy(each => each), listed => Assert.Single(listed));
        Assert.True(many.Count - few.Count <= 4 * PerBranch, report);
        Assert.Single(many, each => each.StartsWith("rev-list --branches=daoris/*", StringComparison.Ordinal));
        Assert.DoesNotContain(many, each => each.StartsWith("log --oneline", StringComparison.Ordinal) || each.StartsWith("merge-base --is-ancestor", StringComparison.Ordinal));
        Assert.True(many.Count <= Ceiling, report);
    }

    // The end's judgement of a branch's own commits: where it left the line, which is also whether the line holds its tip, its
    // tree, and the files it changed. What no branch of the person's holds is one walk of every session branch, not a start a branch
    // (separate starts for the line's ancestry, the tree, where it left the line, its files, and its commits no person's branch holds would be 5 a branch).
    private const int PerBranch = 2;

    // On six trees, asking per tree takes 57 git starts and the one list 38: each tree's branch asked of the tree, the checkout's top and its trees
    // asked by each stage, the session branches listed again, and every branch listed by date and each pair's files compared both ways
    // per branch. Measured 26, two trees 14, with the checkout's trees listed twice, by the stages and by the judgement; 20, two trees
    // 12, with each branch's tree the refs' one listing's (`%(tree)`), no start a branch; 17, two trees 9, with that listing the library's.
    private const int Ceiling = 17;

    /// <summary>The git a look asks, its second over the same home: the first reads what a running driver's first look reads.</summary>
    private async Task<List<string>> WarmLookAsync()
    {
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };
        await tidying.LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);
        var starts = new List<string>();
        using (ProcessStarts.Within(info =>
        {
            if (Path.GetFileNameWithoutExtension(info.FileName).Equals("git", StringComparison.OrdinalIgnoreCase))
            {
                var arguments = info.ArgumentList.SkipWhile(each => each is "-c" or "core.longpaths=true" or "--no-optional-locks");
                lock (starts) starts.Add(string.Join(' ', arguments));
            }

            return System.Diagnostics.Process.Start(info);
        }))
        {
            await tidying.LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);
        }

        return starts;
    }
}
