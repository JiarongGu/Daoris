using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Over real git: the commits made on a branch after its request froze, judged contained, superseded or new against the promoted line,
/// which reads blame and diffs of real files. The run's crash recovery is <see cref="IntegrationResumeMemoryTests"/>'s, on a repository
/// kept in memory.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationRunGitTests : LandedFixture
{
    private static async Task<string> CommitOfAsync(string root, string reference) => (await GitAsync(root, "rev-parse", reference)).Trim();

    private static string Lines(params (int Line, string Text)[] changes)
    {
        var lines = Enumerable.Range(1, 20).Select(each => $"line {each}").ToArray();
        foreach (var (line, text) in changes) lines[line - 1] = text;
        return string.Join("\n", lines) + "\n";
    }

    /// <summary>
    /// The commits a unit's branch made after its request froze, judged against the promoted line (never the branch's
    /// ancestry): one whose patch the line holds, and one whose replay is empty, are contained; one whose every conflicted hunk the line changed
    /// after the frozen base by a commit outside the unit's work is superseded by that commit; one that adds work, and one that conflicts
    /// only with the unit's own landed work, are new.
    /// </summary>
    [Fact]
    public async Task Commits_after_the_frozen_tip_are_judged_contained_superseded_or_new_against_the_promoted_line()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "lines.txt", Lines(), "twenty lines");
        var @base = await CommitOfAsync(root, "main");

        await GitAsync(root, "checkout", "--quiet", "-b", "feature/unit");
        await CommitAsync(root, "lines.txt", Lines((2, "two, the unit's")), "the unit's work");
        var frozen = await CommitOfAsync(root, "HEAD");

        // The line meanwhile: three other units' commits, then the unit's own landed work, resolved to other words than its branch's.
        await GitAsync(root, "checkout", "--quiet", "main");
        await CommitAsync(root, "lines.txt", Lines((15, "fifteen, z's")), "z");
        await CommitAsync(root, "lines.txt", Lines((8, "eight, x's"), (15, "fifteen, z's")), "x");
        var x = await CommitOfAsync(root, "HEAD");
        await CommitAsync(root, "lines.txt", Lines((8, "eight, x's"), (12, "twelve, y's"), (15, "fifteen, z's"), (18, "eighteen, y's")), "y");
        await CommitAsync(root, "lines.txt", Lines((2, "two, resolved"), (8, "eight, x's"), (12, "twelve, y's"), (15, "fifteen, z's"), (18, "eighteen, y's")), "the unit, landed");
        var landed = await CommitOfAsync(root, "HEAD");

        // The branch after its freeze.
        await GitAsync(root, "checkout", "--quiet", "feature/unit");
        await CommitAsync(root, "lines.txt", Lines((2, "two, the unit's"), (15, "fifteen, z's")), "z's change, again");
        var patch = await CommitOfAsync(root, "HEAD");
        await CommitAsync(root, "more.txt", "more work\n", "more work");
        var more = await CommitOfAsync(root, "HEAD");
        await CommitAsync(root, "lines.txt", Lines((2, "two, the unit's"), (8, "eight, the unit's"), (15, "fifteen, z's")), "line 8, the unit's");
        var superseded = await CommitOfAsync(root, "HEAD");
        await CommitAsync(root, "lines.txt", Lines((2, "two, the unit's"), (8, "eight, the unit's"), (12, "twelve, y's"), (15, "fifteen, z's")), "y's change, by hand");
        var empty = await CommitOfAsync(root, "HEAD");
        await CommitAsync(root, "lines.txt", Lines((2, "two, again"), (8, "eight, the unit's"), (12, "twelve, y's"), (15, "fifteen, z's")), "line 2 again");
        var own = await CommitOfAsync(root, "HEAD");
        await GitAsync(root, "checkout", "--quiet", "--detach", "main");

        var judged = await IntegrationAfterLanding.JudgeAsync(root, "main", landed, @base, frozen, own, [landed], CancellationToken.None);

        Assert.NotNull(judged);
        Assert.Equal(
            [
                (patch, AfterLandingDispositions.Contained, (string?)null),
                (more, AfterLandingDispositions.New, null),
                (superseded, AfterLandingDispositions.Superseded, x),
                (empty, AfterLandingDispositions.Contained, null),
                (own, AfterLandingDispositions.New, null),
            ],
            judged.Select(each => (each.Sha, each.Disposition, each.By)));
        Assert.Equal(["lines.txt"], judged.Single(each => each.Sha == superseded).Files);
    }
}
