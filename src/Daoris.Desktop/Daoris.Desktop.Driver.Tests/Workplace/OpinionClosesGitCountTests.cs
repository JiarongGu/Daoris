using Daoris.Driver;
using Xunit.Abstractions;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 An owed opinion judged again over what did not move starts no git: its tree's work, which no branch holds, is kept in the home with
/// the content proof's key (the tip, the line, the holders and the refs' version), so a second judgment, in this process or after a
/// restart, asks nothing; a moved tip, line or ref is another key and asks git again.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class OpinionClosesGitCountTests(ITestOutputHelper output) : LandedFixture
{
    private static readonly DateTimeOffset Owed = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    private static readonly DriverConfig Config = DriverConfig.Empty with
    {
        Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["engine"] = new OpinionRule(["codex-acp"], [OpinionRules.Landing], false),
        },
    };

    private async Task<(string? Closed, List<string> Starts)> JudgeAsync(OpinionDue due, SessionTrees trees)
    {
        var quest = new QuestView("q1", "ask #a1", "engine", "Work q1", "", "Done");
        using var count = GitLaunch.Count();
        var closed = await OpinionLook.ClosesAsync(due, quest, [quest], Config, new LandedBranches(Home), CancellationToken.None, trees: trees);
        var starts = count.Starts.Select(start => string.Join(' ', start.Arguments.Where(each => !each.StartsWith("--no-optional", StringComparison.Ordinal)))).ToList();
        // The library's answers count as git's asks too: a read the memo spares is neither a start nor one of these.
        starts.AddRange(count.Reads.Select(read => "library " + read.Question));
        output.WriteLine($"{starts.Count} asks: {string.Join(" | ", starts)}");
        return (closed, starts);
    }

    [Fact]
    public async Task A_second_judgment_of_an_unchanged_owed_opinion_starts_no_git_in_this_process_or_after_a_restart_and_a_moved_tip_asks_again()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(Scratch, "engine"), "engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the session's work\n", "the session's work");
        var due = new OpinionDue("s1", "q1", "engine", "aurora", tree.Path, OpinionRules.Landing, Owed);

        var (first, firstStarts) = await JudgeAsync(due, trees);
        var (second, secondStarts) = await JudgeAsync(due, trees);

        Assert.Null(first);
        Assert.Null(second);
        Assert.NotEmpty(firstStarts);
        Assert.Empty(secondStarts);

        TreeReadMemo.Release(Home);
        var (restarted, restartStarts) = await JudgeAsync(due, new SessionTrees(Home));
        Assert.Null(restarted);
        Assert.Contains("library Walks", firstStarts);
        Assert.DoesNotContain("library Walks", restartStarts);
        Assert.DoesNotContain("library Names", restartStarts);

        await CommitAsync(tree.Path, "more.txt", "more\n", "more of the work");
        var (moved, movedStarts) = await JudgeAsync(due, trees);
        Assert.Null(moved);
        Assert.Contains("library Walks", movedStarts);
    }
}
