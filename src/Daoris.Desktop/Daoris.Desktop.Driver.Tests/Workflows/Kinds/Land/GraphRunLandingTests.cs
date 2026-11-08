using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run bound to a graph version lands where its record says, and only the commit its <c>land</c> is bound to. A real pass of the runtime makes
/// <c>land</c> active on the session's branch at its tip, read from git, and the door lands that commit. A commit made between the gate's
/// judgement and the landing lands nothing, on the branch path and on the merge path; the pass after it judges the new tip, and the door lands that.
/// </summary>
/// <remarks>Real git, as the other landing tests, a stand-in for the service's quests and records, and no plugin: nothing reaches a network.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GraphRunLandingTests : ReviewLandingFixture
{
    /// <summary>The least a graph run lands by: <c>claimed</c>, then a <c>land</c> by <paramref name="land"/>'s options.</summary>
    private static string ClaimedThenLand(string land) => $$"""
        {"schemaVersion":2,"entries":["claimed"],"end":"landing",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"landing","kind":"land","version":1,"reach":"performed","options":{{land}}}],
         "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"}]}
        """;

    private static readonly LandingSubject Work = new("s1", "q1", "Fix the gap");

    /// <summary>No plugin's process: a graph of built-ins sends, asks and tells nothing that answers.</summary>
    private sealed class NoPlugins : IWorkRuntimePort
    {
        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct) => throw new DriverException("no plugin runs here");

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct) =>
            throw new DriverException("no plugin runs here");

        public Task<(WorkCleanupAnswer Answer, int Generation)> CleanupAsync(string plugin, WorkCleanupCall call, CancellationToken ct) =>
            throw new DriverException("no plugin runs here");

        public int? GenerationOf(string plugin) => null;

        public void Owe(IReadOnlySet<string> plugins)
        {
        }

        public void Watch(IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> awaited)
        {
        }

        public IReadOnlyList<WorkStepHeard> TakeSteps() => [];
    }

    /// <summary>Run q1 bound to a graph version landing by <paramref name="land"/>'s options, its session s1's tree done with one commit.</summary>
    private async Task<(string Root, string Tree, GrownBranch Branch)> BoundRunAsync(string land)
    {
        var root = await RepositoryAsync("engine");
        var tree = await DoneSessionAsync(root, "s1", "q1");
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, ClaimedThenLand(land)), DateTimeOffset.UtcNow);
        return (root, tree, Assert.Single(new SessionBranches(_home).All()));
    }

    /// <summary>
    /// One whole pass of the runtime over the run's quest, done (held for the person's yes where <paramref name="held"/>), and its session's branch,
    /// whose tip is read from git; what the line holds of it read in <paramref name="tree"/>, where given.
    /// </summary>
    private async Task PassAsync(string root, GrownBranch branch, string? tree = null, bool held = false, LandingDoor? door = null)
    {
        var runtime = new NodeRuntime(new NodeRecordFiles(_home, () => true), new NoPlugins(), () => DateTimeOffset.UtcNow, () => true) { Doors = NodeDoors.None.With(NodeKinds.Land, door) };
        var pass = await runtime.PassAsync(new NodeRuntimeLook(Config(), Whole: true)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead(
                [RunHome.QuestUnit(RunHome.Quest("q1", "Done") with { Held = held }), RunHome.Branch(branch.Unit!, branch.Branch, branch.At, ["q1"])],
                new Dictionary<string, string> { ["engine"] = root })),
            Tips = NodeTips.ReadAsync,
            Content = (_, tip, ct) => tree is null ? Task.FromResult<HoldContent?>(null) : HoldReads.ContentAsync(new SessionTrees(_home), tree, tip, ct),
        }, CancellationToken.None);
        Assert.True(pass.Armed);
    }

    /// <summary>The runtime's door, landing as the performer does: over the fixture's world, trees and faked plugin.</summary>
    private LandingDoor Door() => new(() => new LandPerformer(
        _home, _world, new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _lines.Add) { HoldsLease = () => true, Told = _told.Add });

    // What the performer told at `work/event`.
    private readonly List<WorkEvent> _told = [];

    /// <summary>A branch made for the person, no plugin asked.</summary>
    private const string BranchLand = """{"method":"branch","pattern":"feature/{quest}-{slug}"}""";

    private NodeStatus Status(string node) => new NodeRecordFiles(_home, () => false).Read(RunUnits.IdOf("q1"))!.Statuses[node];

    /// <summary>A version copied from the default <paramref name="from"/>, as <c>new --from</c> copies it, saved, and the run bound to it.</summary>
    private async Task<NodeGraph> CopyAsync(string from)
    {
        var source = await WorkflowOperations.SourceAsync(Config(), _home, _ => Task.FromResult(new ChoicesRegistry(null, "unread")),
            from, opinion: false, look: false, CancellationToken.None);
        var copied = WorkflowOperations.PlanGraph(_home, null, "docs", null, source.Name, "terminal", "2026-11-01T09:00:00Z", source.Graph, source.From);
        WorkflowPorts.Store(_home).Write("docs", copied.Plan.Result!);
        RunHome.Bind(_home, "q1", copied.Next!.Digest!, DateTimeOffset.UtcNow);
        return copied.Next.Graph!;
    }

    /// <summary>
    /// The quest's own holds are a step of the default, which the run's pass keeps active while the done is held for the person's yes;
    /// the landing waits behind it and lands nothing. The look chooses nothing for the run, and closes the entry it holds from
    /// before the update as the run's own to land.
    /// </summary>
    [Fact]
    public async Task A_held_done_holds_a_defaults_run_at_its_quest_holds_step_and_the_look_says_it()
    {
        var root = await RepositoryAsync("engine");
        var tree = await DoneSessionAsync(root, "s1", "q1", held: true);
        await CopyAsync("merge-automatic");
        var line = await HeadAsync(root);

        await PassAsync(root, Assert.Single(new SessionBranches(_home).All()), tree, held: true);
        Assert.Equal((NodeStates.Active, NodeStates.Blocked), (Status("holds").State, Status("landing").State));

        Assert.Equal(line, await HeadAsync(root));
    }

    /// <summary>A version that holds no `quest.holds` lands a done held for the person's yes, as it is configured to: the look judges none.</summary>
    [Fact]
    public async Task A_version_without_the_holds_lands_a_held_done_with_no_press()
    {
        var root = await RepositoryAsync("engine");
        var tree = await DoneSessionAsync(root, "s1", "q1", held: true);
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, """
            {"schemaVersion":2,"entries":["claimed"],"end":"landing",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}}],
             "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"landing"}]}
            """), DateTimeOffset.UtcNow);

        var door = Door();
        await PassAsync(root, Assert.Single(new SessionBranches(_home).All()), tree, held: true, door: door);
        var commit = await HeadAsync(tree);
        await door.IdleAsync();

        Assert.Equal(commit, (await GitAsync(root, "merge-base", "main", commit)).Trim());
    }

    [Fact]
    public async Task The_door_lands_where_the_pass_made_land_active_and_holds_before_any_pass()
    {
        var (root, tree, branch) = await BoundRunAsync(BranchLand);
        var trees = new SessionTrees(_home);

        var before = await trees.GateAsync(tree, "q1", _world, "s1");
        Assert.Equal((GraphGateStates.NotJudged, true), (before.Graph!.State, before.GraphPart()!.PressWaits));
        var held = await trees.LandAsync(tree, Work, gate: before);
        Assert.False(held.Landed, held.Message);
        Assert.Equal(LandingCode.Refused, held.Refusal);
        Assert.StartsWith("Daoris has not judged run q1's graph at", held.Message);
        Assert.False(await BranchAsync(root, PushBranch));

        await PassAsync(root, branch);
        var commit = await HeadAsync(tree);
        var landing = Status("landing");
        Assert.Equal((NodeStates.Active, branch.Unit, commit), (landing.State, landing.Binding!.Observes, landing.Binding!.SubjectRevision));

        var gate = await trees.GateAsync(tree, "q1", _world, "s1");
        Assert.Equal((GraphGateStates.Lands, commit), (gate.Graph!.State, gate.Tip));
        var landed = await trees.LandAsync(tree, Work, gate: gate);
        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(commit, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
    }

    /// <summary>
    /// A version copied from <i>Merge automatically</i>, as <c>new --from</c> copies it, holds no review, so once its work's
    /// requests are answered a pass readies its landing with no Accept, and the door merges the session's work into the line.
    /// </summary>
    [Fact]
    public async Task A_version_copied_from_merge_automatically_lands_the_session_with_no_press()
    {
        var root = await RepositoryAsync("engine");
        var tree = await DoneSessionAsync(root, "s1", "q1");
        Assert.DoesNotContain((await CopyAsync("merge-automatic")).Nodes, node => node.Kind == NodeKinds.Review);
        var branch = Assert.Single(new SessionBranches(_home).All());
        var trees = new SessionTrees(_home);

        // Its holds are steps the pass judges, what the line holds read in the session's tree.
        await PassAsync(root, branch, tree);
        var commit = await HeadAsync(tree);
        var gate = await trees.GateAsync(tree, "q1", _world, "s1");
        Assert.Equal((GraphGateStates.Lands, commit), (gate.Graph!.State, gate.Tip));
        var landed = await trees.LandAsync(tree, Work, gate: gate);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(commit, (await GitAsync(root, "merge-base", "main", commit)).Trim());
    }

    /// <summary>
    /// A saved copy of <i>Merge automatically</i> chosen for the repository, whose session's done its conclusion makes due under the
    /// version, is landed by the look once a pass readies its <c>land</c>: through a delivery, the standing authority's, bound to the commit the
    /// pass judged and to the version's digest, as Current's look lands through its own.
    /// </summary>
    [Fact]
    public async Task A_saved_version_s_land_lands_its_work_at_done_through_its_delivery()
    {
        var root = await RepositoryAsync("engine");
        await CopyAsync("merge-automatic");
        var tree = await NamedDoneAsync(root, "s1", "q1");
        var door = Door();
        await PassAsync(root, Assert.Single(new SessionBranches(_home).All()), tree, door: door);
        var commit = await HeadAsync(tree);
        await door.IdleAsync();

        Assert.Equal(commit, (await GitAsync(root, "merge-base", "main", commit)).Trim());
        Assert.True(LandOutcomes.Of(RunUnits.IdOf("q1"), commit)?.Landed);
        var delivery = Assert.Single(new DeliveryStore(_home).OfUnit("s1"));
        Assert.Equal((DeliveryMethods.Merge, commit, "main", DeliverySettlements.Delivered),
            (delivery.Intent.Method, delivery.Intent.Tip, delivery.Intent.Line, delivery.Settled));
        Assert.Equal((DeliveryAuthorities.Automatic, Status("landing").Binding!.GraphDigest),
            (delivery.Authorization.Authority, delivery.Authorization.Graph));
    }

    /// <summary>
    /// After the look lands a saved version's work, the run read from the home's own records, as the loop's pass reads them once the
    /// line's move is kept, says its landing reached and the line's step passed, every step before them reached; also where the line moved
    /// after the pass readies the landing, so the work is replayed onto it and its tree follows the replay to another commit.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_saved_version_s_run_reads_its_landing_done_and_its_line_passed_once_the_look_lands_it(bool lineMoved)
    {
        // The loop's process holds the home's lease, whose holder alone keeps what the line holds.
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Headless, out _);
        var root = await RepositoryAsync("engine");
        await CopyAsync("merge-automatic");
        await NamedDoneAsync(root, "s1", "q1");
        await InventoryPassAsync(root);
        Assert.True(Status("landing").State == NodeStates.Active,
            string.Join(", ", new NodeRecordFiles(_home, () => false).Read(RunUnits.IdOf("q1"))!.Statuses.Select(each => $"{each.Key} {each.Value.State}")));
        if (lineMoved) await CommitAsync(root, "other.txt", "another session's work", "another session's work");

        // The next pass starts the `land` on the door, which lands what the first judged.
        var door = Door();
        await InventoryPassAsync(root, door);
        await door.IdleAsync();
        // A pass may come before the ref watch keeps what the line holds, and the one after reads it.
        await InventoryPassAsync(root);
        await new SessionTrees(_home).KeepEndsAsync(root, "engine", "aurora");
        await InventoryPassAsync(root);

        string[] steps = ["work", "requests", "holds", "content", "landing", "line"];
        Assert.Equal(string.Join(", ", steps.Select(node => $"{node} {NodeStates.Reached}")),
            string.Join(", ", steps.Select(node => $"{node} {Status(node).State}")));
    }

    /// <summary>
    /// A run whose work is merged into the line outside its `land`, before the land starts, and whose tree and branch are tidied after: its
    /// branch's tip no longer reads, and its steps bind the commit its session branch records the line holding, so the landing and the line read
    /// reached and the run ends, rather than holding on a branch nothing can read.
    /// </summary>
    [Fact]
    public async Task A_run_merged_by_hand_before_its_land_started_reads_its_landing_and_line_reached_once_its_tree_is_tidied()
    {
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Headless, out _);
        var root = await RepositoryAsync("engine");
        await CopyAsync("merge-automatic");
        var tree = await NamedDoneAsync(root, "s1", "q1");
        var branch = Assert.Single(new SessionBranches(_home).All()).Branch;
        var commit = await HeadAsync(tree);
        await GitAsync(root, "merge", "--no-ff", "-m", "merged by hand", branch);
        await new SessionTrees(_home).KeepEndsAsync(root, "engine", "aurora");
        await GitAsync(root, "worktree", "remove", "--force", tree);
        await GitAsync(root, "branch", "-D", branch);

        // The plan of a tree gone names the line its branch grew from, never an empty one.
        var plan = await new SessionTrees(_home).PlanAsync(tree, Work);
        Assert.Equal((LandingForm.Merge, "main", (string?)null), (plan.Form, plan.Target, plan.Problem));

        await InventoryPassAsync(root);
        await InventoryPassAsync(root);

        Assert.Equal(commit, (await GitAsync(root, "merge-base", "main", commit)).Trim());
        string[] steps = ["work", "requests", "holds", "content", "landing", "line"];
        Assert.Equal(string.Join(", ", steps.Select(node => $"{node} {NodeStates.Reached}")),
            string.Join(", ", steps.Select(node => $"{node} {Status(node).State}")));
    }

    /// <summary>A graph holding the person's review before a <c>land</c> by <paramref name="options"/>.</summary>
    private static string ReviewedLand(string options) => $$"""
        {"schemaVersion":2,"entries":["claimed"],"end":"landing",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{{options}}}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"review"},{"id":"r>l","kind":"after","from":"review","to":"landing"}]}
        """;

    /// <summary>The same, with the tail a pull request brings (<c>pushed</c> then <c>pull-request</c>, which wait on the platform).</summary>
    private static string ReviewedPullRequest(string options) => $$"""
        {"schemaVersion":2,"entries":["claimed"],"end":"pr",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed","options":{{options}}},
                  {"id":"pushed","kind":"pushed","version":1,"reach":"observed"},{"id":"pr","kind":"pull-request","version":1,"reach":"observed"}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"review"},{"id":"r>l","kind":"after","from":"review","to":"landing"},
                  {"id":"l>p","kind":"after","from":"landing","to":"pushed"},{"id":"p>pr","kind":"after","from":"pushed","to":"pr"}]}
        """;

    /// <summary>A run bound to <paramref name="graph"/>, its session s1's tree done with one commit; the pass that holds the review begun.</summary>
    private async Task<(string Root, string Tree, GrownBranch Branch, LandingDoor Door)> ReviewedRunAsync(string graph)
    {
        var root = await RepositoryAsync("engine");
        var tree = await DoneSessionAsync(root, "s1", "q1");
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, graph), DateTimeOffset.UtcNow);
        var branch = Assert.Single(new SessionBranches(_home).All());
        var door = Door();
        await PassAsync(root, branch, tree, door: door);
        return (root, tree, branch, door);
    }

    /// <summary>
    /// A merge behind a review lands only once the person's Accept is the review's word; the pass that applies the word readies the
    /// <c>land</c> and the runtime starts it on its door, authorized by that Accept, through a delivery. Nothing lands before the word.
    /// </summary>
    [Fact]
    public async Task A_merge_after_a_review_lands_at_the_pass_that_applies_the_accept_and_not_before()
    {
        var (root, tree, branch, door) = await ReviewedRunAsync(ReviewedLand("""{"method":"merge"}"""));
        var commit = await HeadAsync(tree);
        await door.IdleAsync();
        Assert.Equal((NodeStates.Active, NodeStates.Blocked), (Status("review").State, Status("landing").State));
        Assert.Empty(new DeliveryStore(_home).OfUnit("s1"));

        Assert.True(RunNodeWords.Word(new NodeRecordFiles(_home, () => true), "q1", "review", NodeWordActs.Accept, null).Done);
        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();

        Assert.Equal(commit, (await GitAsync(root, "merge-base", "main", commit)).Trim());
        var delivery = Assert.Single(new DeliveryStore(_home).OfUnit("s1"));
        Assert.Equal((DeliveryMethods.Merge, DeliveryAuthorities.Person, DeliverySettlements.Delivered),
            (delivery.Intent.Method, delivery.Authorization.Authority, delivery.Settled));
        Assert.True(LandOutcomes.Of(RunUnits.IdOf("q1"), commit)?.Landed);
    }

    /// <summary>
    /// A press and a pass at once land once: the door started twice for one node and tip makes one landing, and a pass after the node is reached
    /// starts nothing, so a restart's fresh door finds one delivery and no second.
    /// </summary>
    [Fact]
    public async Task A_pass_and_a_second_start_at_once_land_one_delivery()
    {
        var (root, tree, branch, door) = await ReviewedRunAsync(ReviewedLand("""{"method":"merge"}"""));
        Assert.True(RunNodeWords.Word(new NodeRecordFiles(_home, () => true), "q1", "review", NodeWordActs.Accept, null).Done);
        var other = Door();
        await Task.WhenAll(PassAsync(root, branch, tree, door: door), PassAsync(root, branch, tree, door: other));
        await Task.WhenAll(door.IdleAsync(), other.IdleAsync());

        Assert.Single(new DeliveryStore(_home).OfUnit("s1"));
        // A restart: a fresh door at the next pass lands nothing again.
        await PassAsync(root, branch, tree, door: Door());
        Assert.Single(new DeliveryStore(_home).OfUnit("s1"));
    }

    /// <summary>
    /// A restart at each phase of the performer: the Accept kept and applied but the process gone before the node started (a fresh door starts it
    /// once); the landing made and the process gone before the pass reached the node (a fresh door finds the delivery and lands nothing again).
    /// </summary>
    [Fact]
    public async Task A_restart_before_the_start_and_after_the_landing_lands_once()
    {
        var (root, tree, branch, _) = await ReviewedRunAsync(ReviewedLand("""{"method":"merge"}"""));
        var commit = await HeadAsync(tree);
        Assert.True(RunNodeWords.Word(new NodeRecordFiles(_home, () => true), "q1", "review", NodeWordActs.Accept, null).Done);

        // The pass that applies the word readies the `land`, and the process ends before any door starts it.
        await PassAsync(root, branch, tree, door: null);
        Assert.Equal(NodeStates.Active, Status("landing").State);
        Assert.Empty(new DeliveryStore(_home).OfUnit("s1"));

        // A fresh process at its next pass starts it, once.
        var restarted = Door();
        await PassAsync(root, branch, tree, door: restarted);
        await restarted.IdleAsync();
        Assert.Single(new DeliveryStore(_home).OfUnit("s1"));
        Assert.Equal(commit, (await GitAsync(root, "merge-base", "main", commit)).Trim());

        // And again after the landing, whatever the node's status says: no second delivery, no second landing.
        var after = Door();
        await PassAsync(root, branch, tree, door: after);
        await PassAsync(root, branch, tree, door: after);
        await after.IdleAsync();
        var delivery = Assert.Single(new DeliveryStore(_home).OfUnit("s1"));
        Assert.Equal(DeliverySettlements.Delivered, delivery.Settled);
    }

    /// <summary>A landed run's tree tidied afterwards is tried no more: the log holds its one <c>landing</c> line and no <c>gone</c>.</summary>
    [Fact]
    public async Task A_landed_run_whose_tree_is_tidied_is_not_tried_again_at_the_next_looks()
    {
        var (root, tree, branch, door) = await ReviewedRunAsync(ReviewedLand("""{"method":"merge"}"""));
        Assert.True(RunNodeWords.Word(new NodeRecordFiles(_home, () => true), "q1", "review", NodeWordActs.Accept, null).Done);
        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();
        Assert.Equal([LandingCode.Landed], _lines.Where(line => line.Event == "landing.auto").Select(CodeOf));

        await GitAsync(root, "worktree", "remove", "--force", tree);
        var after = Door();
        await PassAsync(root, branch, door: after);
        await PassAsync(root, branch, door: after);
        await after.IdleAsync();

        Assert.Equal([LandingCode.Landed], _lines.Where(line => line.Event == "landing.auto").Select(CodeOf));
    }

    private static string? CodeOf(LandingLine line) => line.Data.First(each => each.Key == "code").Value as string;

    /// <summary>A branch method makes its branch from the intent alone, with no plugin asked, and the landing record names it.</summary>
    [Fact]
    public async Task A_branch_land_makes_its_branch_through_the_performer()
    {
        var (root, tree, branch, door) = await ReviewedRunAsync(ReviewedLand("""{"method":"branch","pattern":"feature/{quest}-{slug}"}"""));
        Assert.True(RunNodeWords.Word(new NodeRecordFiles(_home, () => true), "q1", "review", NodeWordActs.Accept, null).Done);
        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();

        Assert.Equal(await HeadAsync(tree), (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        // The landing told once at `work/event`, with its code.
        var landed = Assert.Single(_told);
        Assert.Equal((WorkEventKinds.WorkLanded, LandingCode.Landed, "q1"), (landed.Kind, landed.Code, landed.Quest));
    }

    /// <summary>
    /// Across the update, a tree holding uncommitted work is not landed past the person's Accept. The session is carried on, the Accept
    /// kept as the review's repair on the run's record (no CarriedAccepts file), and once the tip moves only by its own commits the next pass accepts
    /// the review again and the runtime lands it.
    /// </summary>
    [Fact]
    public async Task A_dirty_tree_carries_the_accept_as_the_reviews_repair_and_lands_once_the_session_commits()
    {
        var (root, tree, branch, door) = await ReviewedRunAsync(ReviewedLand("""{"method":"merge"}"""));
        await File.WriteAllTextAsync(Path.Combine(tree, "wip.txt"), "unfinished\n");
        var files = new NodeRecordFiles(_home, () => true);
        Assert.True(RunNodeWords.Word(files, "q1", "review", NodeWordActs.Accept, null).Done);
        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();

        var repair = Assert.Single(files.Read(RunUnits.IdOf("q1"))!.Repairs);
        Assert.Equal("review", repair.Node);
        Assert.Single(_world.Said);
        Assert.Empty(new CarriedAccepts(_home).All());

        // The session commits what it left, and the tip moves by its own commit only.
        await CommitAsync(tree, "wip.txt", "finished", "finish wip");
        var commit = await HeadAsync(tree);
        await PassAsync(root, branch, tree, door: door);
        await PassAsync(root, branch, tree, door: door);
        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();

        Assert.Empty(files.Read(RunUnits.IdOf("q1"))!.Repairs);
        Assert.Equal(commit, (await GitAsync(root, "merge-base", "main", commit)).Trim());
    }

    /// <summary>A landing refused for the checkout's own work is tried again only once the checkout's status differs, and lands once it is clean.</summary>
    [Fact]
    public async Task A_landing_refused_for_a_dirty_checkout_lands_at_the_next_pass_once_the_checkout_is_clean()
    {
        var (root, tree, branch) = await BoundRunAsync("""{"method":"merge"}""");
        var commit = await HeadAsync(tree);
        var scratch = Path.Combine(root, "scratch.txt");
        await File.WriteAllTextAsync(scratch, "in flight\n");
        var door = Door();
        IEnumerable<string?> Codes() => _lines.Where(line => line.Event == "landing.auto").Select(CodeOf);

        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();
        Assert.Equal([LandingCode.RootDirty], Codes());
        Assert.NotEqual(commit, (await GitAsync(root, "merge-base", "main", commit)).Trim());

        // Nothing moved in the checkout: no pass tries again.
        await PassAsync(root, branch, tree, door: door);
        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();
        Assert.Equal([LandingCode.RootDirty], Codes());

        // Its status differs and it is still dirty: tried again once, and kept at the new status.
        await File.WriteAllTextAsync(Path.Combine(root, "more.txt"), "also in flight\n");
        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();
        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();
        Assert.Equal([LandingCode.RootDirty, LandingCode.RootDirty], Codes());

        // Clean: the next pass lands it, with no press.
        File.Delete(scratch);
        File.Delete(Path.Combine(root, "more.txt"));
        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();
        Assert.Equal([LandingCode.RootDirty, LandingCode.RootDirty, LandingCode.Landed], Codes());
        Assert.Equal(commit, (await GitAsync(root, "merge-base", "main", commit)).Trim());
    }

    /// <summary>
    /// One whole pass of the runtime over the units the home's records and the stand-in service name, linked as the loop's pass reads them
    /// (<see cref="WorkUnits.Linked"/>), and what the line holds read in a unit's newest session's tree, as the loop's gates read it.
    /// </summary>
    private async Task InventoryPassAsync(string root, LandingDoor? door = null)
    {
        var sessions = _world.Records;
        // The sessions' trees are joined to their units as the loop's gather joins them: no due entry stands in for it.
        var branches = WorkInventory.Units(await WorkInventories.GatherAsync(
            _home, new Dictionary<string, string>(), new HashSet<string>(), new List<string>(), recorded: WorkInventories.RecordedOf(sessions)));
        var units = WorkUnits.Linked(branches, QuestUnits.Read([.. _world.Quests.Values], sessions), [], sessions);
        var runtime = new NodeRuntime(new NodeRecordFiles(_home, () => true), new NoPlugins(), () => DateTimeOffset.UtcNow, () => true) { Doors = NodeDoors.None.With(NodeKinds.Land, door) };
        var pass = await runtime.PassAsync(new NodeRuntimeLook(Config(), Whole: true)
        {
            Units = _ => Task.FromResult(new NodeUnitsRead(units, new Dictionary<string, string> { ["engine"] = root })),
            Tips = NodeTips.ReadAsync,
            Content = (unit, tip, ct) => sessions.LastOrDefault(each => unit.Executors.Contains(each.Id, StringComparer.Ordinal) && Directory.Exists(each.Tree))?.Tree is { } tree
                ? HoldReads.ContentAsync(new SessionTrees(_home), tree, tip, ct)
                : Task.FromResult<HoldContent?>(null),
        }, CancellationToken.None);
        Assert.True(pass.Armed);
    }

    [Theory]
    [InlineData(LandingForm.Branch)]
    [InlineData(LandingForm.Merge)]
    public async Task A_commit_inserted_between_the_gates_judgement_and_the_landing_lands_nothing(string form)
    {
        var (root, tree, branch) = await BoundRunAsync(form == LandingForm.Branch ? BranchLand : """{"method":"merge"}""");
        var trees = new SessionTrees(_home);
        await PassAsync(root, branch);
        var judged = await HeadAsync(tree);
        var gate = await trees.GateAsync(tree, "q1", _world, "s1");
        Assert.Equal((GraphGateStates.Lands, judged), (gate.Graph!.State, gate.Tip));
        var line = await HeadAsync(root);

        await CommitAsync(tree, "late.txt", "made after the judgement", "a late change");
        var moved = await trees.LandAsync(tree, Work, gate: gate);

        Assert.False(moved.Landed, moved.Message);
        Assert.Equal(LandingCode.Refused, moved.Refusal);
        Assert.Contains("moved after its landing was judged", moved.Message);
        Assert.False(await BranchAsync(root, PushBranch), "nothing is made from a commit nobody judged");
        Assert.Equal(line, await HeadAsync(root));

        // The next pass judges the new tip; the door holds until it has, then lands that commit.
        var late = await HeadAsync(tree);
        Assert.Equal(GraphGateStates.NotJudged, (await trees.GateAsync(tree, "q1", _world, "s1")).Graph!.State);
        await PassAsync(root, branch);
        var again = await trees.GateAsync(tree, "q1", _world, "s1");
        Assert.Equal((GraphGateStates.Lands, late), (again.Graph!.State, again.Tip));
        var landed = await trees.LandAsync(tree, Work, gate: again);
        Assert.True(landed.Landed, landed.Message);
        if (form == LandingForm.Branch) Assert.Equal(late, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        else Assert.Equal(late, (await GitAsync(root, "merge-base", "main", late)).Trim());
    }

    /// <summary>
    /// A session whose tree heads an operation bringing sessions together lands by its combined Accept where the run's pass readies
    /// its <c>land</c>: the door lands the head with its member brought in (the member's commit stands on the line), and the operation reads landed.
    /// </summary>
    [Fact]
    public async Task A_land_ready_on_a_combined_head_lands_the_head_with_its_members_brought_into_it()
    {
        var root = await RepositoryAsync("engine");
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var branch = Assert.Single(new SessionBranches(_home).All());
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, ClaimedThenLand("""{"method":"merge"}""")), DateTimeOffset.UtcNow);
        var line = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var beside = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(beside.Path, "beside.txt", "beside\n", "the work beside it");
        var member = (await GitAsync(beside.Path, "rev-parse", "HEAD")).Trim();
        var at = new DateTimeOffset(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);
        var (opened, refused) = new TogetherOperations(_home).Open(
            "engine", new TogetherHead("s1", "s1", tree, branch.Branch, (await GitAsync(tree, "rev-parse", "HEAD")).Trim()),
            [new TogetherMember("s-beside", beside.Path, beside.Branch, line, member)], at);
        Assert.Null(refused);
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "cherry-pick", member);
        new TogetherOperations(_home).TurnEnded(opened!.Id, at);
        var ready = await new TogetherLook(_home, () => at).StepAsync(
            root, opened.Id, new TogetherFacts("s1", false, null, false, "main"), null, CancellationToken.None);
        Assert.True(ready!.Phase == TogetherPhase.Ready, $"{ready.Phase}: {ready.Why}");

        var door = Door();
        await PassAsync(root, branch, tree, door: door);
        var head = await HeadAsync(tree);
        await door.IdleAsync();

        Assert.Equal(head, (await GitAsync(root, "merge-base", "main", head)).Trim());
        Assert.Equal(TogetherPhase.Landed, new TogetherOperations(_home).Of(opened.Id)!.Phase);
        Assert.True((await GitAsync(root, "show", "main:beside.txt")).Contains("beside", StringComparison.Ordinal), "the member's work stands on the line");
    }
}
