using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An opinion owed on a landing closes as <c>landed</c> once the line holds the tree's work by its patches, as a landing that
/// replayed the commits and merged the replay leaves them.
/// </summary>
/// <remarks>Real git in a scratch repository: the replay is made by hand as the landing makes it, a cherry-pick and a merge.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class OpinionDueLandedTests : LandedFixture
{
    private static readonly DateTimeOffset Owed = new(2026, 11, 15, 15, 43, 27, TimeSpan.Zero);

    [Fact]
    public async Task A_due_whose_run_landed_by_a_replay_the_line_merged_closes_as_landed_and_one_not_landed_stays_owed()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(Scratch, "engine"), "engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the session's work\n", "the session's work");
        var due = new OpinionDue("s1", "q1", "engine", "aurora", tree.Path, OpinionRules.Landing, Owed);

        Assert.Null(await ClosesAsync(due, trees));

        // The landing's replay: the same patch under a commit of its own, merged into the line.
        await GitAsync(root, "checkout", "--quiet", "-b", "quest/work");
        await GitAsync(root, "cherry-pick", "-x", $"main..{tree.Branch}");
        await GitAsync(root, "checkout", "--quiet", "main");
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "Merge quest/work", "quest/work");
        await GitAsync(root, "branch", "-D", "quest/work");

        Assert.Equal(OpinionDueClosed.Landed, await ClosesAsync(due, trees));
    }

    private Task<string?> ClosesAsync(OpinionDue due, SessionTrees trees)
    {
        var quest = new QuestView("q1", "ask #a1", "engine", "Work q1", "", "Done");
        var config = DriverConfig.Empty with
        {
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["engine"] = new OpinionRule(["codex-acp"], [OpinionRules.Landing], false),
            },
        };
        return OpinionLook.ClosesAsync(due, quest, [quest], config, new LandedBranches(Home), CancellationToken.None, trees: trees);
    }
}
