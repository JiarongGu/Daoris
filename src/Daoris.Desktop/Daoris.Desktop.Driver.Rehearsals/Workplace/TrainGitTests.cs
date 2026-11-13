using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The train over real git: an ask's parts land on the ask's branch in the plan's order, one candidate at a time on the line's one queue;
/// a part a green run cannot take goes back to its own request while the others land; a conflict is told to the part's worker and the
/// part's branch stays untouched. A part's landing is the promotion of its member, never its quest's Done.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TrainGitTests : LandedFixture
{
    private const string Ask = "a1";

    private IntegrationStore Store => _store ??= new IntegrationStore(Home, () => StoreSettled.Yes);

    private IntegrationStore? _store;

    private string TreesHome => DaorisHome.Entry(Home, HomeEntry.Trees);

    private static string Line => TrainBranch.Of(Ask);

    /// <summary>The gate proof a request carries so its authority stands (<see cref="IntegrationAuthority"/>); the train's own judgement is what is under test.</summary>
    private static IntegrationProof Proof => new(AcceptedBy.Person);

    private sealed class Plans(TrainPlan plan) : ITrainPlans
    {
        public Task<TrainPlan?> OfAsync(string ask, CancellationToken ct) => Task.FromResult<TrainPlan?>(plan);
    }

    private static TrainPlanPart Part(string id, string[]? after = null, string[]? orders = null) =>
        new(id, new HashSet<string>(after ?? []), new HashSet<string>(orders ?? []));

    private sealed record Train(string Root, TrainSelection Selection, string Base);

    /// <summary>The repository with the ask's branch at <c>main</c>, and the plan the train reads.</summary>
    private async Task<Train> StartAsync(params TrainPlanPart[] parts)
    {
        var root = await RepositoryAsync("engine");
        var @base = (await GitAsync(root, "rev-parse", "main")).Trim();
        Assert.True(await TrainBranch.EnsureAsync(root, Ask, @base, default));
        return new Train(root, new TrainSelection(new TrainParts(Store), new Plans(new TrainPlan(Ask, 1, parts))), @base);
    }

    /// <summary>A part's branch from <c>main</c> with one file, queued for the ask's branch and tagged with its part.</summary>
    private async Task<string> QueueAsync(Train train, string part, int second, string file, string content, string? session = null)
    {
        var branch = $"feature/{part}";
        await GitAsync(train.Root, "checkout", "--quiet", "-b", branch, "main");
        await CommitAsync(train.Root, file, content, $"work on {part}");
        var tip = (await GitAsync(train.Root, "rev-parse", branch)).Trim();
        await GitAsync(train.Root, "checkout", "--quiet", "main");
        var id = $"r-{part}";
        Assert.Null(Store.Add(new IntegrationRequest(
            id, "engine", "aurora", branch, tip, IntegrationRoutes.Node, Line, IntegrationRequester.NodeStart,
            new LandingIntegrate(Batch: 1, Gates: LandingIntegrate.NoGates).Resolved(), new DateTimeOffset(2026, 11, 14, 9, 0, second, TimeSpan.Zero))
        {
            Session = session, Proof = Proof,
        }));
        new TrainParts(Store).Tag(id, Ask, part);
        return tip;
    }

    private async Task<IntegrationReplayRun> NextAsync(Train train, RebaseTelling.Say? say = null) =>
        await new IntegrationReplays(Store, TreesHome).RunAsync(
            new IntegrationReplayAsk("engine", "aurora", train.Root, Line) { Config = DriverConfig.Empty, Train = train.Selection, Say = say });

    private async Task<IntegrationPromoteRun> PromoteAsync(Train train, IntegrationReplayRun run) =>
        await new IntegrationPromotes(Store, TreesHome).RunAsync(run.Claim!, new IntegrationPromoteAsk("engine", "aurora", train.Root, Line, DriverConfig.Empty, Home));

    private IReadOnlyList<string> Members(IntegrationReplayRun run) => [.. Store.Members(run.Candidate!.Id).Select(each => each.Request)];

    private IReadOnlySet<string> Landed => new TrainParts(Store).Landed(Ask);

    [Fact]
    public async Task Three_parts_land_on_the_asks_branch_in_the_plans_order_one_candidate_each()
    {
        var train = await StartAsync(Part("p1"), Part("p2", after: ["p1"]), Part("p3", orders: ["p2"]));
        // They arrive the other way round: p3 first, p1 last.
        await QueueAsync(train, "p3", 1, "c.txt", "c\n");
        await QueueAsync(train, "p2", 2, "b.txt", "b\n");
        await QueueAsync(train, "p1", 3, "a.txt", "a\n");

        var order = new List<string>();
        for (var next = 0; next < 3; next++)
        {
            var run = await NextAsync(train);
            Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
            order.AddRange(Members(run));
            Assert.Equal(IntegrationPromoteRuns.Settled, (await PromoteAsync(train, run)).Kind);
        }

        Assert.Equal(["r-p1", "r-p2", "r-p3"], order);
        Assert.Equal(["p1", "p2", "p3"], Landed.Order());
        var files = (await GitAsync(train.Root, "ls-tree", "--name-only", Line)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.All(new[] { "a.txt", "b.txt", "c.txt" }, file => Assert.Contains(file, files));
        Assert.Equal(train.Base, (await GitAsync(train.Root, "rev-parse", "main")).Trim());

        // Each promoted batch is kept on its own numbered ref, the last at the branch's tip.
        Assert.Equal(
            [$"{TrainRefs.Prefix}{Ask}/1", $"{TrainRefs.Prefix}{Ask}/2", $"{TrainRefs.Prefix}{Ask}/3"],
            (await GitAsync(train.Root, "for-each-ref", "--format=%(refname)", $"{TrainRefs.Prefix}{Ask}/")).Split('\n', StringSplitOptions.RemoveEmptyEntries).Order());
        Assert.Equal((await GitAsync(train.Root, "rev-parse", Line)).Trim(), (await GitAsync(train.Root, "rev-parse", $"{TrainRefs.Prefix}{Ask}/3")).Trim());
    }

    [Fact]
    public async Task The_lines_tip_is_merged_into_the_asks_branch_from_objects_alone_and_a_branch_already_holding_it_is_left()
    {
        var train = await StartAsync(Part("p1"));
        await CommitAsync(train.Root, "line.txt", "the line moved\n", "the line moves");
        var tip = (await GitAsync(train.Root, "rev-parse", "main")).Trim();
        var before = (await GitAsync(train.Root, "rev-parse", Line)).Trim();

        var merged = await TrainBases.PrepareAsync(train.Root, Ask, "main", default);

        Assert.Equal(TrainBaseKinds.Merged, merged.Kind);
        Assert.Equal([before, tip], (await GitAsync(train.Root, "rev-list", "--parents", "-n1", Line)).Trim().Split(' ')[1..]);
        Assert.Equal("the line moved\n", await GitAsync(train.Root, "show", $"{Line}:line.txt"));
        Assert.Equal(TrainBaseKinds.Current, (await TrainBases.PrepareAsync(train.Root, Ask, "main", default)).Kind);
    }

    [Fact]
    public async Task An_asks_branch_that_does_not_stand_is_made_at_the_lines_tip_and_a_conflict_leaves_it_as_it_was()
    {
        var root = await RepositoryAsync("engine");
        var tip = (await GitAsync(root, "rev-parse", "main")).Trim();

        Assert.Equal(TrainBaseKinds.Current, (await TrainBases.PrepareAsync(root, Ask, "main", default)).Kind);
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", Line)).Trim());

        await GitAsync(root, "checkout", "--quiet", Line);
        await CommitAsync(root, "shared.txt", "on the ask\n", "the ask's change");
        await GitAsync(root, "checkout", "--quiet", "main");
        await CommitAsync(root, "shared.txt", "on the line\n", "the line's change");
        var asked = (await GitAsync(root, "rev-parse", Line)).Trim();

        Assert.Equal(TrainBaseKinds.Conflict, (await TrainBases.PrepareAsync(root, Ask, "main", default)).Kind);
        Assert.Equal(asked, (await GitAsync(root, "rev-parse", Line)).Trim());
    }

    [Fact]
    public async Task A_part_whose_parts_it_builds_on_have_not_landed_is_not_formed_and_a_part_sent_back_does_not_count_as_landed()
    {
        var train = await StartAsync(Part("p1"), Part("p2", after: ["p1"]));
        await QueueAsync(train, "p2", 1, "b.txt", "b\n");

        Assert.Equal(IntegrationReplayRuns.Idle, (await NextAsync(train)).Kind);

        await QueueAsync(train, "p1", 2, "a.txt", "a\n");
        var first = await NextAsync(train);
        Assert.Equal(["r-p1"], Members(first));

        // p1's candidate stands, not promoted: p2 waits behind it and nothing has landed.
        Assert.Equal(IntegrationReplayRuns.Standing, (await NextAsync(train)).Kind);
        Assert.Empty(Landed);
    }

    private sealed class Verdict(IntegrationVerdict answer) : IIntegrationVerdicts
    {
        public Task<IntegrationVerdict> JudgeAsync(IntegrationVerdictAsk ask) => Task.FromResult(answer);
    }

    [Fact]
    public async Task A_red_names_its_part_which_goes_back_while_the_others_land()
    {
        const string gates = """{ "gates": [ { "name": "unit", "run": "node check.mjs" } ] }""" + "\n";
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "daoris.gates.json", gates, "the declaration");
        var blob = (await GitAsync(root, "rev-parse", "HEAD:daoris.gates.json")).Trim();
        var @base = (await GitAsync(root, "rev-parse", "main")).Trim();
        Assert.True(await TrainBranch.EnsureAsync(root, Ask, @base, default));
        var train = new Train(root, new TrainSelection(new TrainParts(Store), new Plans(new TrainPlan(Ask, 1, [Part("p1"), Part("p2"), Part("p3")]))), @base);
        var config = DriverConfig.Empty.WithIntegrationGates("engine", blob);
        var policy = new LandingIntegrate(Batch: 8, Gates: LandingIntegrate.Declared).Resolved();
        foreach (var (part, second) in new[] { ("p1", 1), ("p2", 2), ("p3", 3) })
        {
            await GitAsync(root, "checkout", "--quiet", "-b", $"feature/{part}", "main");
            await CommitAsync(root, $"{part}.txt", $"{part}\n", $"work on {part}");
            var tip = (await GitAsync(root, "rev-parse", $"feature/{part}")).Trim();
            await GitAsync(root, "checkout", "--quiet", "main");
            Assert.Null(Store.Add(new IntegrationRequest(
                $"r-{part}", "engine", "aurora", $"feature/{part}", tip, IntegrationRoutes.Node, Line, IntegrationRequester.NodeStart, policy,
                new DateTimeOffset(2026, 11, 14, 9, 0, second, TimeSpan.Zero)) { Proof = Proof }));
            new TrainParts(Store).Tag($"r-{part}", Ask, part);
        }

        await GitAsync(root, "checkout", "--quiet", "--detach");
        {
            // The batch is judged red at p2: the verdict hands p2 back and keeps the others.
            IntegrationVerdicts.Within = new Verdict(new IntegrationVerdict(VerdictActions.HandBack)
            {
                HandedBack = [new VerdictHandedBack("feature/p2", "pinned", "unit > fails on p2", [], [], null)],
                Retain = ["feature/p1", "feature/p3"],
            });
            var red = await new IntegrationReplays(Store, TreesHome).RunAsync(
                new IntegrationReplayAsk("engine", "aurora", root, Line) { Config = config, Train = train.Selection });
            Assert.Equal(["r-p1", "r-p2", "r-p3"], Members(red));
            var judged = await new IntegrationPromotes(Store, TreesHome).RunAsync(red.Claim!, new IntegrationPromoteAsk("engine", "aurora", root, Line, config, Home));
            Assert.Equal(IntegrationPromoteRuns.HandedBack, judged.Kind);
            Assert.Equal(IntegrationRequestCloses.HandedBack, Store.Request("r-p2")!.ClosedWhy);
            Assert.Empty(Landed);

            // The rest are formed again and land green; p2 stays unlanded, so the ask's parts are not all there.
            IntegrationVerdicts.Within = new Verdict(new IntegrationVerdict(VerdictActions.Green));
            var again = await new IntegrationReplays(Store, TreesHome).RunAsync(
                new IntegrationReplayAsk("engine", "aurora", root, Line) { Config = config, Train = train.Selection });
            Assert.Equal(["r-p1", "r-p3"], Members(again));
            Assert.Equal(IntegrationPromoteRuns.Settled,
                (await new IntegrationPromotes(Store, TreesHome).RunAsync(again.Claim!, new IntegrationPromoteAsk("engine", "aurora", root, Line, config, Home))).Kind);
            Assert.Equal(["p1", "p3"], Landed.Order());
            Assert.False(new PartsReport(Ask, 1, ["p1", "p2", "p3"], Landed).Reached);
        }
    }

    [Fact]
    public async Task A_conflict_between_parts_is_told_to_the_worker_of_the_part_and_its_branch_is_left_as_it_was()
    {
        var train = await StartAsync(Part("p1"), Part("p2", orders: ["p1"]));
        await QueueAsync(train, "p1", 1, "shared.txt", "from p1\n", session: "s1");
        var tip = await QueueAsync(train, "p2", 2, "shared.txt", "from p2\n", session: "s2");

        // One at a time: p1 lands, then p2 meets it.
        Assert.Equal(IntegrationPromoteRuns.Settled, (await PromoteAsync(train, await NextAsync(train))).Kind);
        var told = new List<string>();
        var run = await NextAsync(train, (session, _, _, _) =>
        {
            told.Add(session);
            return Task.FromResult<SayAnswer?>(new SayAnswer(true, "kept"));
        });

        Assert.Contains("s2", told);
        Assert.Equal(tip, (await GitAsync(train.Root, "rev-parse", "feature/p2")).Trim());
        Assert.NotEqual(IntegrationMemberStates.Promoted, Store.Members(run.Candidate!.Id).Single().State);
        Assert.Equal(["p1"], Landed.Order());
    }
}
