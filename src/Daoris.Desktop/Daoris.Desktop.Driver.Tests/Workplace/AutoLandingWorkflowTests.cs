using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The automatic landing under a named workflow (WORKFLOW1f), split from the one class by PROCSPLIT1: whether done work lands with no
/// press is its run's bound version's landing step, a kind's paths hold the look, and keeping the workflow releases it.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class AutoLandingWorkflowTests : AutoLandingFixture
{
    /// <summary>
    /// WORKFLOW1f (the workflow design §2.3, §4.5): whether done work lands with no press is its run's bound version's landing step. A
    /// version that accepts automatically makes the done due and the look lands it, where Current's rule waits for the person's
    /// press; the record keeps the version, the rule as the version made it, and who accepted it.
    /// </summary>
    [Fact]
    public async Task A_version_that_accepts_automatically_lands_at_the_look_where_Current_waits_for_your_press()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"));
        Follow("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic"}]""");
        var tree = await NamedDoneAsync(root, "s1", "q1");

        var said = await LookAsync();

        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"), string.Join("\n", said));
        var record = new LandedBranches(_home).Landing("s1")!;
        Assert.Equal(AcceptedBy.Auto, record.AcceptedBy);
        Assert.Equal(new LandedRule(null, AutoAccept: true, LandingSource.Repository), record.Rule);
        Assert.Equal(new LandingWorkflow("docs-to-pr", 1, WorkflowLevels.Repository), record.Workflow);
        Assert.Contains("It landed as `docs-to-pr` v1 says, `engine`'s default.", Assert.Single(said));
        Assert.True(Directory.Exists(tree));
    }

    /// <summary>
    /// WORKFLOW1f (design §2.3, §4.6): a version requiring a second opinion holds the look's landing where Current, whose rule reads
    /// only before each next step, would land it; the session stays due as <c>opinion</c>, said once.
    /// </summary>
    [Fact]
    public async Task A_version_requiring_an_opinion_holds_where_Current_would_land()
    {
        var root = await RepositoryAsync("engine");
        (DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true)) with
        {
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase) { ["engine"] = new(["codex-acp"], [OpinionRules.Steps]) },
        }).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        Follow("""[{"id":"work","kind":"work"},{"id":"opinion","kind":"opinion","required":true},{"id":"land","kind":"landing","form":"branch","accept":"automatic"}]""");
        await NamedDoneAsync(root, "s1", "q1");

        var first = await LookAsync();

        Assert.StartsWith("landing  session s1 (#q1 → engine): not accepted automatically: its work waits for a second opinion", Assert.Single(first));
        Assert.Equal(AutoLandingCode.Opinion, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Empty(await LookAsync());
    }

    /// <summary>
    /// WORKFLOW1f (design §4.4): a kind's workflow that lands with no press, where the work would otherwise wait for the person's,
    /// checks the paths the chain changed against the kind's. Outside them the look holds it, said once, until the person keeps the
    /// workflow for this work (`workflow keep`); then the next look lands it. Nothing switches by itself.
    /// </summary>
    [Fact]
    public async Task A_kinds_paths_hold_the_look_until_you_keep_its_workflow()
    {
        var root = await RepositoryAsync("engine");
        var config = WorkflowSelection.Apply(
            DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}")),
            new WorkflowDeclare("docs", "aurora", "Documentation", ["docs/**"]));
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        Follow("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic"}]""", kind: "docs");
        var tree = await NamedDoneAsync(root, "s1", "q1");

        var held = Assert.Single(await LookAsync());
        Assert.StartsWith("landing  session s1 (#q1 → engine): not accepted automatically: the landing was refused, as follows. It waits for "
            + "your review. Holds: this work changed 1 path outside Documentation's (`docs/**`): `s1.txt`; and `docs-to-pr` v1, chosen for it, "
            + "asks less of you than Current would without the kind (it lands with no press of yours).", held);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));
        var logged = Assert.Single(_lines, line => line.Event == "workflow.held");
        Assert.Equal((WorkflowGateStates.KindPaths, (object?)1), (Value(logged, "code"), Value(logged, "outside")));
        Assert.Empty(await LookAsync());

        var (done, message) = await WorkflowKeepCommand.KeepAsync(
            _home, "s1", tree, "q1", _world, "the gap is in the docs build", ReviewDoors.Terminal, DateTimeOffset.UtcNow);
        Assert.True(done, message);
        Assert.Contains("you kept it for this work, though it changed paths outside its kind's, saying: \"the gap is in the docs build\"", message);

        Assert.Single(await LookAsync());
        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Equal(new LandingWorkflow("docs-to-pr", 1, WorkflowLevels.WorkspaceKind) { Kind = "docs" }, new LandedBranches(_home).Landing("s1")!.Workflow);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Keep_refuses_when_the_binding_disappears_or_breaks_after_the_gate_read(bool corrupt)
    {
        var root = await RepositoryAsync("engine");
        WorkflowSelection.Apply(
            DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}")),
            new WorkflowDeclare("docs", "aurora", "Documentation", ["docs/**"]))
            .Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        Follow("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"automatic"}]""", kind: "docs");
        var tree = await NamedDoneAsync(root, "s1", "q1");
        var binding = WorkflowRunBindings.PathOf(_home, "q1");
        var reads = 0;
        _world.ReadingQuests = () =>
        {
            // Process read first; the review reads the chain after the workflow's path hold was judged.
            if (++reads != 2) return;
            if (corrupt) File.WriteAllText(binding, "{broken");
            else File.Delete(binding);
        };

        var (done, message) = await WorkflowKeepCommand.KeepAsync(
            _home, "s1", tree, "q1", _world, "keep these paths", ReviewDoors.Terminal, DateTimeOffset.UtcNow);

        Assert.False(done, message);
        Assert.Contains("nothing was kept", message);
        Assert.Contains("Holds: this work changed 1 path outside Documentation's", message);
        Assert.Null(WorkflowRunBindings.Read(_home, "q1"));
        Assert.Equal(corrupt, File.Exists(binding));
        if (corrupt) Assert.Equal("{broken", File.ReadAllText(binding));
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));
    }
}
