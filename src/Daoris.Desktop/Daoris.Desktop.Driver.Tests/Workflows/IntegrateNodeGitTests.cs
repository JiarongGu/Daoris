using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1n part n2 over real git and the home's own request store (D215's note; the integration design §17.4, round 5): the runtime's
/// pass over a landed branch of a real repository asks the real door through <see cref="HomeIntegrationDoor"/>. A person's Accept under
/// <c>owner</c> is requested and found by its key after a restart; a rule gaining <c>integrate</c> over a standing landing requests none; an
/// automatic landing under <c>owner</c> waits for the card, and under <c>done</c> is the request; a branch that moved after its landing is
/// refused by git's own word; a second landing tip waits for the first request's outcome in the store. Nothing runs the integration.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrateNodeGitTests : LandedFixture
{
    private const string Pattern = "feature/{quest}-{slug}";
    private const string Unit = "unit-a";

    private readonly ObservedPort _port = new();

    private void Save(LandingRule rule, LandingIntegrate? integrate) =>
        DriverConfig.Empty.WithLine("engine", "main").WithLanding("engine", rule).WithLandingIntegrate("engine", integrate)
            .Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));

    private NodeRecordFiles Files => new(Home, () => true);

    private NodeRuntime Runtime() => new(Files, _port, () => DateTimeOffset.UtcNow, () => true) { Integration = NodeRuntime.HomeDoor(Home) };

    private IntegrationStore Requests() => new(Home, () => StoreSettled.Yes);

    /// <summary>One session's tree with one commit, landed by <paramref name="acceptedBy"/>.</summary>
    private async Task<(string Branch, string Tip)> LandedAsync(SessionTrees trees, string root, string quest, string acceptedBy = AcceptedBy.Person)
    {
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, $"{quest}.txt", $"the work of {quest}\n", $"the work of {quest}");
        var landed = await trees.LandAsync(opened.Path, new LandingSubject($"s-{quest}", quest, "Fix the gap"), acceptedBy: acceptedBy);
        Assert.True(landed.Landed, landed.Message);
        return (landed.Branch!, (await GitAsync(root, "rev-parse", landed.Branch!)).Trim());
    }

    /// <summary>The branch unit of a landed branch as the inventory gives it, the record's own landing among its records.</summary>
    private WorkUnit UnitOf(SessionTrees trees, string branch)
    {
        var entry = trees.Recorded.Of("engine", branch)!;
        return new WorkUnit(Unit, WorkUnitKind.Branch, new WorkOwner("engine", "aurora", "session-branches.json", WorkRecord.DriverWriter),
            new WorkSubject(branch, entry.Tip), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
        {
            Records = new WorkUnitRecords(null, [entry], null),
        };
    }

    private Task<NodeRuntimePass> PassAsync(NodeRuntime runtime, string root, WorkUnit unit, bool whole = true) =>
        runtime.PassAsync(new NodeRuntimeLook(DriverConfig.Load(DaorisHome.Entry(Home, HomeEntry.DriverConfig)), whole)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead([unit], new Dictionary<string, string> { ["engine"] = root })),
        }, CancellationToken.None);

    private NodeStatus Status(string node = "integrate") => Files.Read(Unit)!.Statuses[node];

    /// <summary>
    /// A person's Accept under <c>owner</c>, where <c>integrate</c> stood, is the request: the real door admits it by the landing's record and
    /// real git, kept by the unit and the tip, and a runtime started again finds that request by its key and makes no second.
    /// </summary>
    [Fact]
    public async Task A_persons_accept_under_owner_is_requested_by_the_node_and_a_restart_finds_it_by_its_key()
    {
        var root = await RepositoryAsync("engine");
        Save(new LandingRule(LandingForm.Branch, Pattern), new LandingIntegrate(Request: LandingIntegrate.Owner));
        var trees = new SessionTrees(Home);
        var (branch, tip) = await LandedAsync(trees, root, "q1");

        await PassAsync(Runtime(), root, UnitOf(trees, branch));

        var request = Assert.Single(Requests().Open());
        Assert.Equal((branch, tip, IntegrationRoutes.Node, Unit, "main"), (request.Branch, request.Tip, request.Route, request.Unit, request.Line));
        Assert.Equal(IntegrationRequester.NodeStart, request.Requester);
        Assert.Equal(NodeStates.Active, Status().State);

        await PassAsync(Runtime(), root, UnitOf(trees, branch));

        Assert.Equal(request.Id, Assert.Single(Requests().Open()).Id);
        Assert.Equal(request.Id, Requests().FindByKey(Unit, tip)!.Id);
        Assert.Equal((NodeStates.Active, 1), (Status().State, Assert.Single(Files.Read(Unit)!.Attempts).Attempt));
    }

    /// <summary>
    /// A rule gaining <c>integrate</c> over a standing landing requests none of them: its record says neither that <c>integrate</c> stood nor
    /// what it asked, so the node waits for the owner's card and nothing is written to the store.
    /// </summary>
    [Fact]
    public async Task A_rule_gaining_integrate_over_a_standing_landing_requests_none_of_them()
    {
        var root = await RepositoryAsync("engine");
        Save(new LandingRule(LandingForm.Branch, Pattern), null);
        var trees = new SessionTrees(Home);
        var (branch, _) = await LandedAsync(trees, root, "q1");
        Assert.Null(trees.Recorded.Of("engine", branch)!.Integrate);
        Save(new LandingRule(LandingForm.Branch, Pattern), new LandingIntegrate(Request: LandingIntegrate.Owner));

        var pass = await PassAsync(Runtime(), root, UnitOf(trees, branch));

        Assert.Equal((NodeStates.Reached, NodeStates.Ready), (Status("handed").State, Status().State));
        Assert.Empty(Requests().Open());
        Assert.Empty(Files.Read(Unit)!.Attempts);
        Assert.Contains(pass.Said, line => line.Contains("its integration waits", StringComparison.Ordinal));
    }

    /// <summary>
    /// An automatic landing under <c>owner</c> with <c>gates: none</c> makes the branch and waits for the card: the composition never moves the
    /// line by itself. Under <c>done</c>, an automatic landing is the request.
    /// </summary>
    [Fact]
    public async Task An_automatic_landing_under_owner_waits_for_the_card_and_under_done_is_requested()
    {
        var root = await RepositoryAsync("engine");
        Save(new LandingRule(LandingForm.Branch, Pattern, AutoAccept: true), new LandingIntegrate(Request: LandingIntegrate.Owner, Gates: LandingIntegrate.NoGates));
        var trees = new SessionTrees(Home);
        var (waiting, _) = await LandedAsync(trees, root, "q1", AcceptedBy.Auto);
        Assert.Equal((AcceptedBy.Auto, LandingIntegrate.Owner), (trees.Recorded.Of("engine", waiting)!.AcceptedBy, trees.Recorded.Of("engine", waiting)!.Integrate!.Request));

        await PassAsync(Runtime(), root, UnitOf(trees, waiting));
        Assert.Equal(NodeStates.Ready, Status().State);
        Assert.Empty(Requests().Open());

        Save(new LandingRule(LandingForm.Branch, Pattern, AutoAccept: true), new LandingIntegrate(Request: LandingIntegrate.Done, Gates: LandingIntegrate.Declared));
        var (done, tip) = await LandedAsync(trees, root, "q2", AcceptedBy.Auto);
        await PassAsync(Runtime(), root, UnitOf(trees, done) with { Id = "unit-b" });

        var request = Assert.Single(Requests().Open());
        Assert.Equal((done, tip, "unit-b"), (request.Branch, request.Tip, request.Unit));
    }

    /// <summary>A branch that moved after its landing is refused by git's own word and fails the node with the door's code, said.</summary>
    [Fact]
    public async Task A_branch_that_moved_after_its_landing_fails_the_node_as_moved()
    {
        var root = await RepositoryAsync("engine");
        Save(new LandingRule(LandingForm.Branch, Pattern), new LandingIntegrate(Request: LandingIntegrate.Owner));
        var trees = new SessionTrees(Home);
        var (branch, _) = await LandedAsync(trees, root, "q1");
        await CommitOnAsync(root, branch, "later.txt", "a later commit\n", "a later commit");

        var pass = await PassAsync(Runtime(), root, UnitOf(trees, branch));

        Assert.Equal((NodeStates.Failed, IntegrationRefusals.Moved), (Status().State, Status().Code));
        Assert.Contains(pass.Said, line => line.Contains("(moved)", StringComparison.Ordinal));
        Assert.Empty(Requests().Open());
    }

    /// <summary>
    /// F41 over the real store: a second landing tip (the unit's next Accept, an advance) invalidates the node, whose first request is still
    /// open, so no second is made; once the first closes, the second is requested for the new tip.
    /// </summary>
    [Fact]
    public async Task A_second_landing_tip_waits_for_the_first_requests_outcome_in_the_store()
    {
        var root = await RepositoryAsync("engine");
        Save(new LandingRule(LandingForm.Branch, Pattern), new LandingIntegrate(Request: LandingIntegrate.Owner));
        var trees = new SessionTrees(Home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "work.txt", "the work\n", "the work");
        var landed = await trees.LandAsync(first.Path, new LandingSubject("s1", "q1", "Fix the gap"));
        Assert.True(landed.Landed, landed.Message);
        var tip1 = (await GitAsync(root, "rev-parse", landed.Branch!)).Trim();
        var runtime = Runtime();
        await PassAsync(runtime, root, UnitOf(trees, landed.Branch!));
        var opened = Assert.Single(Requests().Open());
        Assert.Equal(tip1, opened.Tip);

        var next = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(next.Path, "more.txt", "more work\n", "more work");
        var advanced = await trees.LandAsync(next.Path, new LandingSubject("s2", "q1", "Fix the gap"));
        Assert.True(advanced.Landed, advanced.Message);
        var tip2 = (await GitAsync(root, "rev-parse", landed.Branch!)).Trim();
        Assert.NotEqual(tip1, tip2);

        await PassAsync(runtime, root, UnitOf(trees, landed.Branch!));
        Assert.Equal(opened.Id, Assert.Single(Requests().Open()).Id);
        Assert.Equal(1, Status().Unreconciled);

        Assert.True(Requests().Close(opened.Id, IntegrationRequestCloses.Settled));
        await PassAsync(runtime, root, UnitOf(trees, landed.Branch!));

        var second = Assert.Single(Requests().Open());
        Assert.Equal((tip2, NodeStates.Active, 2), (second.Tip, Status().State, Status().Attempt));
        Assert.Equal(tip1, Requests().FindByKey(Unit, tip1)!.Tip);
    }
}
