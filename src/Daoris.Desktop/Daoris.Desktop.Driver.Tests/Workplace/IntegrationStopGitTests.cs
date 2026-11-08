using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>Over real git: a stop is about the line as it stands, so its open requests form nothing again until the line moves.</summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationStopGitTests : LandedFixture
{
    [Fact]
    public async Task A_line_whose_integration_stopped_forms_nothing_again_until_the_line_moves()
    {
        var root = await RepositoryAsync("engine");
        var @base = (await GitAsync(root, "rev-parse", "main")).Trim();
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/a", "main");
        await CommitAsync(root, "a.txt", "a\n", "work on feature/a");
        var tip = (await GitAsync(root, "rev-parse", "feature/a")).Trim();
        await GitAsync(root, "checkout", "--quiet", "main");
        var store = new IntegrationStore(Home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell);
        Assert.Null(store.Add(new IntegrationRequest(
            "ra", "engine", "aurora", "feature/a", tip, IntegrationRoutes.Recorded, "main", IntegrationRequester.Card,
            new LandingIntegrate(Batch: 8).Resolved(), new DateTimeOffset(2026, 10, 30, 9, 0, 1, TimeSpan.Zero))));
        var claim = store.Form(new IntegrationForming("engine", "aurora", "main", @base, ["ra"])).Claim!;
        store.End(claim, store.OpenStep(claim.Candidate)!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Stopped)
        {
            Said = "the line's integration stopped", Ends = IntegrationCandidateEnds.Stopped,
        });
        var replays = new IntegrationReplays(store, DaorisHome.Entry(Home, HomeEntry.Trees));

        var stopped = await replays.RunAsync(new IntegrationReplayAsk("engine", "aurora", root, "main"));

        Assert.Equal(IntegrationReplayRuns.Idle, stopped.Kind);
        Assert.Null(store.StandingFor("engine", "main"));

        await CommitAsync(root, "moved.txt", "moved\n", "the line moves");
        var moved = await replays.RunAsync(new IntegrationReplayAsk("engine", "aurora", root, "main"));

        Assert.Equal(IntegrationReplayRuns.Replayed, moved.Kind);
    }
}
