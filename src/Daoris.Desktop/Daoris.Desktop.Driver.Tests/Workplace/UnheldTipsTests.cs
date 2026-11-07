using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The question of every session branch is one walk of the repository: which session branches no branch of the person's holds,
/// answered for each tip the judgement's refs hold a session branch at, and asked of git on its own for any other commit.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class UnheldTipsTests : LandedFixture
{
    [Fact]
    public async Task Every_session_branch_is_asked_in_one_walk_and_read_as_the_per_branch_log_reads_it()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var held = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(held.Path, "held.txt", "held\n", "held");
        var open = await trees.OpenAsync(root, "engine", "borealis");
        await CommitAsync(open.Path, "open.txt", "open\n", "open");
        var moreOpen = await trees.OpenAsync(root, "engine", "corona");
        await CommitAsync(moreOpen.Path, "more.txt", "more\n", "more");
        await GitAsync(root, "branch", "feature/keeps-aurora", held.Branch);

        var refs = RefSnapshot.Take(root);
        var tips = new Dictionary<string, string>();
        foreach (var tree in new[] { held, open, moreOpen }) tips[tree.Branch] = (await GitAsync(root, "rev-parse", tree.Branch)).Trim();
        List<string> starts;
        bool?[] answers;
        using (var count = GitLaunch.Count())
        {
            answers =
            [
                await UnheldTips.HeldAsync(root, tips[held.Branch], refs, default),
                await UnheldTips.HeldAsync(root, tips[open.Branch], refs, default),
                await UnheldTips.HeldAsync(root, tips[moreOpen.Branch], refs, default),
            ];
            starts = [.. count.Starts.Select(start => string.Join(' ', start.Arguments))];
        }

        Assert.Equal<bool?>([true, false, false], answers);
        foreach (var tree in new[] { held, open, moreOpen })
        {
            var counted = await TreeFollows.UnlandedAsync(root, tips[tree.Branch]);
            Assert.Equal(counted == 0, answers[Array.IndexOf([held.Branch, open.Branch, moreOpen.Branch], tree.Branch)]);
        }

        Assert.Single(starts.Where(each => each.StartsWith("rev-list --branches=daoris/*", StringComparison.Ordinal)));
        Assert.DoesNotContain(starts, each => each.StartsWith("log --oneline", StringComparison.Ordinal));

        // A commit that is no session branch's tip is the log's to count.
        var main = (await GitAsync(root, "rev-parse", "main")).Trim();
        Assert.True(await UnheldTips.HeldAsync(root, main, refs, default));
    }
}
