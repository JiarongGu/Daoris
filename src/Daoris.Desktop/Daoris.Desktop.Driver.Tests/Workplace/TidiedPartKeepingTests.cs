using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// D245 point 3, the keeper's half over real git: a part a landing's <c>tidy</c> ended at its tip is ended by that record alone (<c>tidied</c>),
/// though no proof of the line holds its commits, and it is kept once. Every later pass over the same repository starts the same git as the
/// one before and writes nothing, so the part is not judged again at each pass; a part that moved on since the tidy is judged as any other.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TidiedPartKeepingTests : LandedFixture
{
    private GrownBranch Entry(string branch) => new SessionBranches(Home).Entries().Last(each => each.Branch == branch);

    /// <summary>A part with a commit the line never takes, landed on a branch, and a tidy record naming the part at its tip.</summary>
    private async Task<(string Root, SessionTrees Trees, string Tree, string Branch, string Tip)> TidiedAsync()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landing = await LandAsync(trees, root, "a.txt", "a\n", new LandingSubject("s1", "q1", "Fix"));
        var part = new LandedBranches(Home).Of("engine", landing.Branch!)!.Units.Single();
        var grown = new SessionBranches(Home).Entries().Single(each => each.Unit == part);
        var tip = (await GitAsync(root, "rev-parse", grown.Branch)).Trim();
        var tree = (await GitAsync(root, "worktree", "list", "--porcelain")).Replace("\r", "", StringComparison.Ordinal).Split("\n\n")
            .Single(block => block.Contains($"branch refs/heads/{grown.Branch}", StringComparison.Ordinal))
            .Split('\n')[0]["worktree ".Length..].Trim();
        new LandedBranches(Home).Tidied("engine", landing.Branch!, new ChainTidied((await GitAsync(root, "rev-parse", landing.Branch!)).Trim(), DateTimeOffset.UtcNow)
        {
            Parts = [new(grown.Branch, tip) { KeptOn = $"refs/daoris/discarded/{grown.Branch}" }],
        });
        return (root, trees, tree, grown.Branch, tip);
    }

    private static async Task<(int Written, List<string> Starts)> PassAsync(SessionTrees trees, string root)
    {
        var starts = new List<string>();
        int written;
        using (ProcessStarts.Within(info =>
        {
            if (Path.GetFileNameWithoutExtension(info.FileName).Equals("git", StringComparison.OrdinalIgnoreCase))
            {
                lock (starts) starts.Add(string.Join(' ', info.ArgumentList.SkipWhile(each => each is "-c" or "core.longpaths=true" or "--no-optional-locks")));
            }

            return System.Diagnostics.Process.Start(info);
        }))
        {
            written = await trees.Ends.KeepAsync(root, "engine", "aurora", "main", CancellationToken.None);
        }

        return (written, starts);
    }

    [Fact]
    public async Task A_tidied_part_is_ended_by_the_record_alone_and_every_later_pass_starts_the_same_git_and_writes_nothing()
    {
        var (root, trees, _, branch, tip) = await TidiedAsync();
        Assert.False(await HoldsOnTheLineAsync(root, tip));
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        var first = await PassAsync(trees, root);
        var held = Entry(branch).Held!;
        Assert.True(first.Written >= 1);
        Assert.Equal((EndBy.Tidied, tip), (held.By, held.Tip));

        var second = await PassAsync(trees, root);
        var third = await PassAsync(trees, root);

        Assert.Equal((0, 0), (second.Written, third.Written));
        Assert.Equal(held, Entry(branch).Held);
        Assert.Equal(second.Starts.Count, third.Starts.Count);
        Assert.Equal(second.Starts.Order(), third.Starts.Order());
    }

    [Fact]
    public async Task A_part_that_moved_on_since_the_tidy_is_not_ended_by_its_record()
    {
        var (root, trees, tree, branch, _) = await TidiedAsync();
        await CommitAsync(tree, "later.txt", "later\n", "after the tidy");
        using var lease = HomeLease.TryAcquire(Home, LeaseKind.Headless, out _);

        await PassAsync(trees, root);

        Assert.NotEqual(EndBy.Tidied, Entry(branch).Held?.By);
    }

    private static async Task<bool> HoldsOnTheLineAsync(string root, string tip)
    {
        var listed = await GitAsync(root, "branch", "--contains", tip, "--format=%(refname:short)");
        return listed.Split('\n').Any(each => each.Trim() == "main");
    }
}
