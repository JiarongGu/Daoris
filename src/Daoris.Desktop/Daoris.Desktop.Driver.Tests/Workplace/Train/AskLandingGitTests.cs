using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>An ask delivery run's <c>land</c> over real git: the ask's branch, at the tip the person accepted, merged into the line with no session behind it.</summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class AskLandingGitTests : LandedFixture
{
    private static readonly string Run = AskRunKey.Of("a1", "engine")!;

    private async Task<(string Root, string Tip)> AskBranchAsync()
    {
        var root = await RepositoryAsync("engine");
        DriverConfig.Empty.WithLine("engine", "main").Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        GitFolders.Remember(Home, [(root, "engine", "default")]);
        Assert.True(WorkflowRunBindings.Bind(Home, new WorkflowRunBinding(Run, "engine", "default", "ask-delivery", WorkflowLevels.Task, DateTimeOffset.UnixEpoch) { Ask = "a1" }));
        await GitAsync(root, "checkout", "-b", TrainBranch.Of("a1"));
        await CommitAsync(root, "part.txt", "a part\n", "a part of the ask");
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "checkout", "main");
        return (root, tip);
    }

    private static LandStart Start(string tip) =>
        new(RunUnits.IdOf(Run), Run, "landing", AskRunKey.BranchUnit(Run), [], "engine", "default", tip, "gph-1", "review")
        {
            Authority = StepAuthority.OfDoor(RunUnits.IdOf(Run), "landing", 1),
        };

    [Fact]
    public async Task The_ask_branch_at_the_accepted_tip_is_merged_into_the_line()
    {
        var (root, tip) = await AskBranchAsync();

        var said = await new AskLanding(Home).PerformAsync(Start(tip));

        Assert.Contains("merged", said);
        var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, "main"], CancellationToken.None);
        Assert.Equal(0, code);
        Assert.True(LandOutcomes.Of(RunUnits.IdOf(Run), tip)?.Landed);
    }

    [Fact]
    public async Task A_branch_that_moved_since_the_accept_lands_nothing_and_says_so()
    {
        var (root, tip) = await AskBranchAsync();
        await GitAsync(root, "checkout", TrainBranch.Of("a1"));
        await CommitAsync(root, "more.txt", "more\n", "a later part");
        await GitAsync(root, "checkout", "main");

        var said = await new AskLanding(Home).PerformAsync(Start(tip));

        Assert.Contains("moved", said);
        var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, "main"], CancellationToken.None);
        Assert.NotEqual(0, code);
        Assert.Equal(LandingCode.Moved, LandOutcomes.Of(RunUnits.IdOf(Run), tip)?.Refusal);
    }

    [Fact]
    public async Task A_run_that_is_no_ask_delivery_is_not_landed_here()
    {
        var (_, tip) = await AskBranchAsync();

        Assert.Null(await new AskLanding(Home).PerformAsync(Start(tip) with { Run = "q7" }));
    }
}
