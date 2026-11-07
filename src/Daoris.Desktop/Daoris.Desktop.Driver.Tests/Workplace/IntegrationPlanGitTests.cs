using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris integrate &lt;repository&gt; --plan &lt;branch&gt;</c>, a branch previewed onto its
/// line in git's objects alone over real git. The commits the line lacks, the files the branch changes and each conflict with what would settle
/// it are said, and the repository's refs, objects, index and working files are as they were before.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationPlanGitTests : LandedFixture
{
    private string Quarantine => Path.Combine(Home, "plans");

    /// <summary>What the repository holds that a write would change: every ref, every object, the index's entries and the working tree's state.</summary>
    private static async Task<string> HeldAsync(string root) =>
        string.Join("\n---\n",
            await GitAsync(root, "for-each-ref", "--format=%(refname) %(objectname)"),
            await GitAsync(root, "cat-file", "--batch-all-objects", "--batch-check"),
            await GitAsync(root, "count-objects", "-v"),
            await GitAsync(root, "ls-files", "--stage"),
            await GitAsync(root, "status", "--porcelain=v1", "--untracked-files=all"));

    private static async Task<string> BranchAsync(string root, string name, string file, string content)
    {
        await GitAsync(root, "checkout", "--quiet", "-b", name, "main");
        await CommitAsync(root, file, content, $"work on {name}: {file}");
        var tip = (await GitAsync(root, "rev-parse", name)).Trim();
        await GitAsync(root, "checkout", "--quiet", "main");
        return tip;
    }

    /// <summary>🔴 A clean branch is previewed with its commits and files, and the repository's refs, objects, index and tree are unchanged after it.</summary>
    [Fact]
    public async Task A_plan_previews_the_branch_in_objects_alone_and_writes_nothing_to_the_repository()
    {
        var root = await RepositoryAsync("engine");
        var tip = await BranchAsync(root, "feature/a", "a.txt", "a\n");
        // The line moves too, so the merged tree is one neither side holds: an object only a write would make.
        await CommitAsync(root, "b.txt", "b\n", "the line moves on");
        var before = await HeldAsync(root);

        var plan = await IntegrationPlans.PlanAsync("engine", root, "main", "feature/a", Quarantine);

        Assert.Null(plan.Problem);
        Assert.Equal(tip, plan.BranchTip);
        Assert.Equal(["a.txt"], plan.Changed);
        Assert.Single(plan.Commits);
        Assert.EndsWith("work on feature/a: a.txt", plan.Commits[0], StringComparison.Ordinal);
        Assert.Empty(plan.Conflicts);
        Assert.False(plan.Contained);
        Assert.Equal(before, await HeldAsync(root));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Quarantine));
        Assert.Contains("  it merges with no conflict.", IntegrationPlans.Said(plan));
    }

    /// <summary>🔴 A branch that conflicts with the line names the file as a conflict of meaning, still writing nothing; a branch the line holds says so.</summary>
    [Fact]
    public async Task A_conflict_is_named_with_what_would_settle_it_and_a_branch_the_line_holds_is_said_contained()
    {
        var root = await RepositoryAsync("engine");
        await BranchAsync(root, "feature/a", "README.md", "the branch's words\n");
        await CommitAsync(root, "README.md", "the line's words\n", "the line changes it too");
        var before = await HeldAsync(root);

        var plan = await IntegrationPlans.PlanAsync("engine", root, "main", "feature/a", Quarantine);

        Assert.Null(plan.Problem);
        Assert.Equal([new IntegrationPlanConflict("README.md", IntegrationPlanSettles.Meaning)], plan.Conflicts);
        Assert.Equal(before, await HeldAsync(root));
        Assert.Contains(IntegrationPlans.Said(plan), line => line.Contains("README.md: a conflict of meaning", StringComparison.Ordinal));

        await GitAsync(root, "checkout", "--quiet", "-b", "feature/held", "main");
        await GitAsync(root, "checkout", "--quiet", "main");
        var held = await IntegrationPlans.PlanAsync("engine", root, "main", "feature/held", Quarantine);
        Assert.True(held.Contained);
        Assert.Empty(held.Commits);

        var none = await IntegrationPlans.PlanAsync("engine", root, "main", "feature/none", Quarantine);
        Assert.Contains("no branch or commit", none.Problem, StringComparison.Ordinal);
    }
}
