using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDNODE1b3 (the land node design §3, <i>A restart settles by phase</i>; §8 row 5): a delivery stopped at each git boundary and taken up
/// after a restart, settled by phase before any new start: a recorded outcome goes on from the next phase, an entry written ahead without
/// its outcome is read from the ref and the landing record, and anything else is unknown and held for the person. Each ends in one branch,
/// never two, and never none where the effect happened; a press and a pass at once land once. Real git, as <see cref="LandDeliveryGitTests"/>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class DeliveryReconcileGitTests : IDisposable
{
    private static readonly Func<CancellationToken, Task<IReadOnlySet<string>>> NoneRunning =
        _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    private const string Named = "feature/ENG-12-tidy";

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "delivery-reconcile", Guid.NewGuid().ToString("N")[..8]);
    private readonly string _home;

    public DeliveryReconcileGitTests()
    {
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_scratch);
    }

    /// <summary>
    /// Two starters of one delivery, a press and the runtime's pass, land at once: the tree's hold lets one land it, and the other, reading the
    /// delivery again under the hold, finds it delivered and lands nothing more. One branch, one write of each phase.
    /// </summary>
    [Fact]
    public async Task A_press_and_a_pass_at_once_land_their_one_delivery_once()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Branch);
        var again = Store("run-a").Start(Intent(tip, DeliveryMethods.Branch), Accepted());
        Assert.False(again.Started);
        Assert.Equal(claim, again.Claim);

        var both = await Task.WhenAll(
            Task.Run(() => trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim))),
            Task.Run(() => new SessionTrees(_home).LandAsync(tree.Path, Subject, inUse: NoneRunning, acceptedBy: AcceptedBy.Auto,
                delivery: new TreeDelivery(Store("run-a"), again.Claim!))));

        Assert.Single(both, each => each.Landed);
        var other = Assert.Single(both, each => !each.Landed);
        Assert.Equal(AutoLandingCode.Already, other.Refusal);
        Assert.Contains("delivered", other.Message);
        Assert.Equal([Named], await BranchesAsync(root));
        Assert.Equal(tip, await RevAsync(root, $"refs/heads/{Named}"));
        Assert.Equal([DeliveryPhases.Prepared, DeliveryPhases.RefWritten, DeliveryPhases.Recorded], Store("run-a").Phases(claim.Key).Select(each => each.Phase));
        Assert.Equal(DeliverySettlements.Delivered, Store("run-a").Delivery(claim.Key)?.Settled);
    }

    /// <summary>
    /// A stop after each git effect and before its outcome, then a restart: the dead run's delivery taken up by the next and settled by phase.
    /// Cut while prepared, nothing stands and the preparation is withdrawn; cut after its ref, the ref read at the intended tip is done and the
    /// landing goes on to its record; cut after its record, the record read is done and the delivery delivered. One branch at the tip, written once.
    /// </summary>
    [Theory]
    [InlineData(DeliveryPhases.Prepared, DeliveryPhases.Prepared)]
    [InlineData(DeliveryPhases.RefWritten, DeliveryPhases.Recorded)]
    [InlineData(DeliveryPhases.Recorded, null)]
    public async Task A_stop_at_each_git_boundary_and_a_restart_end_in_one_branch(string cut, string? next)
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Branch);
        await Assert.ThrowsAsync<Crash>(() => trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: Stopping(store, claim, cut)));

        var reconciled = Assert.Single(await Reconciler("run-b", root, DeadAre("run-a")).StartAsync(CancellationToken.None));

        Assert.Equal(next is null ? DeliveryStandings.Delivered : DeliveryStandings.GoesOn, reconciled.Standing);
        Assert.Equal(next, reconciled.Next);
        Assert.Equal(("run-b", 2L), (reconciled.Claim?.Run, reconciled.Claim?.Generation));
        if (next is not null)
        {
            var landed = await new SessionTrees(_home).LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(Store("run-b"), reconciled.Claim!));
            Assert.True(landed.Landed, landed.Message);
            Assert.Equal(Named, landed.Branch);
        }

        Assert.Equal([Named], await BranchesAsync(root));
        Assert.Equal(tip, await RevAsync(root, $"refs/heads/{Named}"));
        Assert.Equal(tip, new SessionTrees(_home).Recorded.Of("engine", Named)?.Tip);
        var phases = Store("run-b").Phases(claim.Key);
        Assert.All(phases, each => Assert.False(each.Open));
        Assert.Single(phases, each => each.Phase == DeliveryPhases.RefWritten);
        Assert.Equal(DeliveryOutcomes.Done, phases[^1].Outcome);
        Assert.Equal(cut == DeliveryPhases.Prepared ? DeliveryOutcomes.Withdrawn : DeliveryOutcomes.Done, phases[0].Outcome);
        Assert.Equal(DeliverySettlements.Delivered, Store("run-b").Delivery(claim.Key)?.Settled);
        Assert.Empty(await Reconciler("run-b", root).StartAsync(CancellationToken.None));
    }

    /// <summary>
    /// A later delivery stopped after its advance's ref: the restart reads the branch at the new tip, done, and its landing goes on to record it
    /// as an advance from the tip the first recorded, never as a branch made anew, and the branch is not moved twice.
    /// </summary>
    [Fact]
    public async Task An_advance_stopped_after_its_ref_is_recorded_as_an_advance_from_the_recorded_tip_after_a_restart()
    {
        var (root, trees, tree, first) = await TreeAsync();
        var (store, claim) = Start(first, DeliveryMethods.Branch);
        Assert.True((await trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(store, claim))).Landed);
        await CommitAsync(tree.Path, "more.txt", "more work", "more work");
        var second = (await RevAsync(tree.Path, "HEAD"))!;
        var (_, later) = Start(second, DeliveryMethods.Branch);
        await Assert.ThrowsAsync<Crash>(() => trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: Stopping(store, later, DeliveryPhases.RefWritten)));

        var reconciled = await Reconciler("run-b", root, DeadAre("run-a")).BeforeStartAsync("unit-1", CancellationToken.None);
        var landed = await new SessionTrees(_home).LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(Store("run-b"), reconciled!.Claim!));

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(first, landed.AdvancedFrom);
        Assert.Equal(second, await RevAsync(root, $"refs/heads/{Named}"));
        Assert.Equal(second, new SessionTrees(_home).Recorded.Of("engine", Named)?.Tip);
        Assert.Equal([Named], await BranchesAsync(root));
        Assert.Single(Store("run-b").Phases(later.Key), each => each.Phase == DeliveryPhases.RefWritten);
        Assert.Equal(DeliverySettlements.Delivered, Store("run-b").Delivery(later.Key)?.Settled);
    }

    /// <summary>
    /// A merge stopped at each git boundary, then a restart: cut while prepared, nothing was pressed, the preparation is withdrawn and the
    /// merge goes on; cut after the promotion moved its line, or after its record, the line read holding the commit settles it delivered. Merged once.
    /// </summary>
    [Theory]
    [InlineData(DeliveryPhases.Prepared)]
    [InlineData(DeliveryPhases.RefWritten)]
    [InlineData(DeliveryPhases.Recorded)]
    public async Task A_merge_stopped_at_each_git_boundary_is_merged_once_after_a_restart(string cut)
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Merge);
        await Assert.ThrowsAsync<Crash>(() => trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: Stopping(store, claim, cut), beside: NoSessions));

        var reconciled = Assert.Single(await Reconciler("run-b", root, DeadAre("run-a")).StartAsync(CancellationToken.None));

        if (cut == DeliveryPhases.Prepared)
        {
            Assert.Equal((DeliveryStandings.GoesOn, DeliveryPhases.Prepared), (reconciled.Standing, reconciled.Next));
            Assert.Equal(1, (await GitFixture.RunAsync(root, "merge-base", "--is-ancestor", tip, "refs/heads/main")).ExitCode);
            var landed = await new SessionTrees(_home).LandAsync(tree.Path, Subject, inUse: NoneRunning,
                delivery: new TreeDelivery(Store("run-b"), reconciled.Claim!), beside: NoSessions);
            Assert.True(landed.Landed, landed.Message);
        }
        else
        {
            Assert.Equal(DeliveryStandings.Delivered, reconciled.Standing);
        }

        Assert.Equal(0, (await GitFixture.RunAsync(root, "merge-base", "--is-ancestor", tip, "refs/heads/main")).ExitCode);
        Assert.Equal("1", (await GitFixture.GitAsync(root, "rev-list", "--merges", "--count", "main")).Trim());
        Assert.Empty(await BranchesAsync(root));
        var phases = Store("run-b").Phases(claim.Key);
        Assert.Equal([DeliveryPhases.Prepared, DeliveryPhases.RefWritten, DeliveryPhases.Recorded], phases.TakeLast(3).Select(each => each.Phase));
        Assert.All(phases.TakeLast(3), each => Assert.Equal(DeliveryOutcomes.Done, each.Outcome));
        Assert.Single(phases, each => each.Phase == DeliveryPhases.RefWritten);
        Assert.Equal(await RevAsync(root, "refs/heads/main"), phases[^2].Evidence);
        Assert.Equal(DeliverySettlements.Delivered, Store("run-b").Delivery(claim.Key)?.Settled);
    }

    /// <summary>Two restarts at once over one stopped delivery: one takes it up and settles it, the other holds it, and each phase is written once.</summary>
    [Fact]
    public async Task Two_restarts_at_once_settle_a_stopped_delivery_once()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Branch);
        await Assert.ThrowsAsync<Crash>(() => trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: Stopping(store, claim, DeliveryPhases.Recorded)));
        var dead = DeadAre("run-a");

        var both = await Task.WhenAll(
            Task.Run(() => Reconciler("run-b", root, dead).StartAsync(CancellationToken.None)),
            Task.Run(() => Reconciler("run-c", root, dead).StartAsync(CancellationToken.None)));

        var answers = both.SelectMany(each => each).ToList();
        Assert.Single(answers, each => each.Standing == DeliveryStandings.Delivered);
        Assert.All(answers.Where(each => each.Standing != DeliveryStandings.Delivered),
            each => Assert.Contains(each.Standing, new[] { DeliveryStandings.Held, DeliveryStandings.Settled }));
        var phases = Store("run-b").Phases(claim.Key);
        Assert.Equal(3, phases.Count);
        Assert.All(phases, each => Assert.Equal(DeliveryOutcomes.Done, each.Outcome));
        Assert.Equal(DeliverySettlements.Delivered, Store("run-b").Delivery(claim.Key)?.Settled);
        Assert.Equal([Named], await BranchesAsync(root));
    }

    /// <summary>
    /// A merge press whose request was left waiting, its line's entry withdrawn as 1b2 writes it: while the request waits, nothing is pressed again;
    /// once the line takes it, the restart's settlement reads the line holding the commit and settles it reached.
    /// </summary>
    [Fact]
    public async Task A_merge_whose_request_waits_is_held_for_its_line_and_reached_once_the_line_holds_its_tip()
    {
        var (root, _, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Merge);
        store.Begin(claim, DeliveryPhases.Prepared, $"merge {tip} into `main`");
        store.End(claim, DeliveryPhases.Prepared, DeliveryOutcomes.Done, tip, $"the commit merged into `main` is {tip}");
        store.Begin(claim, DeliveryPhases.RefWritten, $"`refs/heads/main` to hold {tip}");
        store.End(claim, DeliveryPhases.RefWritten, DeliveryOutcomes.Withdrawn, null, $"`{tree.Branch}` waits to be merged into `main`, and is tried again at each look");
        var admitted = new IntegrationRequests(new HomeIntegrationRecords(_home), new IntegrationStore(_home), () => DriverConfig.Empty, new WorkflowFiles(_home))
            .Press("engine", "aurora", tree.Branch, tip, "main", delivered: true);
        Assert.True(admitted.Admitted, admitted.Message);

        var waits = Assert.Single(await Reconciler("run-a", root).StartAsync(CancellationToken.None));

        Assert.Equal(DeliveryStandings.Waits, waits.Standing);
        Assert.Contains("waits", waits.Said);
        Assert.Equal(2, Store("run-a").Phases(claim.Key).Count);

        await GitFixture.GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "merge", "--no-ff", "-m", "the line takes it", tip);
        var reached = Assert.Single(await Reconciler("run-a", root).StartAsync(CancellationToken.None));

        Assert.Equal(DeliveryStandings.Delivered, reached.Standing);
        var phases = Store("run-a").Phases(claim.Key);
        Assert.Equal([DeliveryPhases.Prepared, DeliveryPhases.RefWritten, DeliveryPhases.RefWritten, DeliveryPhases.Recorded], phases.Select(each => each.Phase));
        Assert.Equal([DeliveryOutcomes.Done, DeliveryOutcomes.Withdrawn, DeliveryOutcomes.Done, DeliveryOutcomes.Done], phases.Select(each => each.Outcome));
        Assert.Equal(DeliverySettlements.Delivered, Store("run-a").Delivery(claim.Key)?.Settled);
    }

    /// <summary>
    /// The branch found at another commit after a stop, neither where its delivery wrote it ahead nor where it stood: unknown, held for the person,
    /// and neither a restart again nor a landing again writes anything more.
    /// </summary>
    [Fact]
    public async Task A_ref_found_elsewhere_after_a_stop_is_unknown_and_nothing_repeats_its_effect()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Branch);
        var main = (await RevAsync(root, "refs/heads/main"))!;
        var moving = new TreeDelivery(store, claim)
        {
            Seam = async phase =>
            {
                if (phase != DeliveryPhases.RefWritten) return;
                await GitFixture.GitAsync(root, "update-ref", $"refs/heads/{Named}", main);
                throw new Crash();
            },
        };
        await Assert.ThrowsAsync<Crash>(() => trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: moving));

        var reconciled = Assert.Single(await Reconciler("run-b", root, DeadAre("run-a")).StartAsync(CancellationToken.None));

        Assert.Equal(DeliveryStandings.Unknown, reconciled.Standing);
        Assert.Contains(Named, reconciled.Said);
        var written = Store("run-b").Phases(claim.Key);
        Assert.Equal((DeliveryPhases.RefWritten, DeliveryOutcomes.Unknown, main), (written[^1].Phase, written[^1].Outcome, written[^1].Evidence));

        Assert.Equal(DeliveryStandings.Unknown, Assert.Single(await Reconciler("run-b", root).StartAsync(CancellationToken.None)).Standing);
        var again = await new SessionTrees(_home).LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: new TreeDelivery(Store("run-b"), reconciled.Claim!));
        Assert.False(again.Landed, again.Message);
        Assert.Contains("unknown", again.Message);
        Assert.Equal(main, await RevAsync(root, $"refs/heads/{Named}"));
        Assert.Equal(written.Count, Store("run-b").Phases(claim.Key).Count);
        Assert.True(Store("run-b").Delivery(claim.Key)!.Unresolved);
    }

    /// <summary>A delivery whose run still lives is that run's to land: held, its claim not taken, nothing written.</summary>
    [Fact]
    public async Task A_delivery_whose_run_still_lives_is_held_and_nothing_is_written()
    {
        var (root, _, _, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Branch);
        store.Begin(claim, DeliveryPhases.Prepared, $"branch: `{Named}` at {tip}");

        var held = Assert.Single(await Reconciler("run-b", root).StartAsync(CancellationToken.None));

        Assert.Equal(DeliveryStandings.Held, held.Standing);
        Assert.Contains("still runs", held.Said);
        Assert.Null(held.Claim);
        Assert.True(Assert.Single(Store("run-b").Phases(claim.Key)).Open);
        Assert.Equal(("run-a", 1L), (Store("run-b").Delivery(claim.Key)!.ClaimRun, Store("run-b").Delivery(claim.Key)!.ClaimGeneration));
    }

    /// <summary>A push stopped after its record: the git phases are done, and what follows is its plugin's (row 1e), left as it stands.</summary>
    [Fact]
    public async Task A_push_stopped_after_its_record_leaves_its_plugin_s_phases_to_the_plugin_frame()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Push);
        // LANDNODE1e: a push delivery tells its plugin, so the landing needs one the stand-in wire answers for.
        var told = new SessionTrees(_home, new DeliveryPluginPhasesTests.Wire());
        await Assert.ThrowsAsync<Crash>(() => told.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: Stopping(store, claim, DeliveryPhases.Recorded)));

        var reconciled = Assert.Single(await Reconciler("run-b", root, DeadAre("run-a")).StartAsync(CancellationToken.None));

        Assert.Equal(DeliveryStandings.Plugin, reconciled.Standing);
        Assert.Equal([Named], await BranchesAsync(root));
        Assert.All(Store("run-b").Phases(claim.Key), each => Assert.Equal(DeliveryOutcomes.Done, each.Outcome));
        Assert.True(Store("run-b").Delivery(claim.Key)!.Unresolved);
    }

    /// <summary>A stop that leaves its unit's delivery unresolved blocks a new tip's start until its settlement before the start delivers it.</summary>
    [Fact]
    public async Task A_stopped_delivery_is_settled_before_its_unit_s_next_start()
    {
        var (root, trees, tree, tip) = await TreeAsync();
        var (store, claim) = Start(tip, DeliveryMethods.Branch);
        await Assert.ThrowsAsync<Crash>(() => trees.LandAsync(tree.Path, Subject, inUse: NoneRunning, delivery: Stopping(store, claim, DeliveryPhases.Recorded)));
        await CommitAsync(tree.Path, "more.txt", "more work", "more work");
        var next = Intent((await RevAsync(tree.Path, "HEAD"))!, DeliveryMethods.Branch);
        Assert.NotNull(Store("run-b").Start(next, Accepted()).Refused);

        var before = await Reconciler("run-b", root, DeadAre("run-a")).BeforeStartAsync("unit-1", CancellationToken.None);

        Assert.Equal((claim.Key, DeliveryStandings.Delivered), (before?.Key, before?.Standing));
        Assert.True(Store("run-b").Start(next, Accepted()).Started);
        Assert.Null(await Reconciler("run-b", root).BeforeStartAsync("unit-2", CancellationToken.None));
    }

    // ——— The fixture.

    private static readonly LandingSubject Subject = new("s1", "0fda18", "Tidy the readme");

    private static readonly Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>> NoSessions = _ => Task.FromResult<IReadOnlyList<TreeHolder>>([]);

    private static TreeDelivery Stopping(DeliveryStore store, DeliveryClaim claim, string cut) =>
        new(store, claim) { Seam = phase => phase == cut ? Task.FromException(new Crash()) : Task.CompletedTask };

    private DeliveryReconciler Reconciler(string run, string root, Func<StoreRun, bool>? alive = null) => new(Store(run, alive), _home, _ => root);

    private sealed class Crash : Exception;

    private static Func<StoreRun, bool> DeadAre(params string[] runs) => run => !runs.Contains(run.Run);

    private DeliveryStore Store(string run, Func<StoreRun, bool>? alive = null) => new(_home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    private static DeliveryIntent Intent(string tip, string method) => new(
        "unit-1", "engine", tip, method, "main", "main", method == DeliveryMethods.Merge ? null : Named, method == DeliveryMethods.Merge ? null : "ENG-12",
        "Tidy the readme (ENG-12)", method is DeliveryMethods.Push or DeliveryMethods.PullRequest ? "forge" : null, "epoch-7");

    private static DeliveryAuthorization Accepted() => DeliveryAuthorization.Person("accept-3", "r3", "graph-digest", """{"method":"branch"}""", "[]");

    private (DeliveryStore Store, DeliveryClaim Claim) Start(string tip, string method)
    {
        var store = Store("run-a");
        var start = store.Start(Intent(tip, method), Accepted());
        Assert.True(start.Started, start.Refused ?? start.Held);
        return (store, start.Claim!);
    }

    private async Task<(string Root, SessionTrees Trees, TreeOpened Tree, string Tip)> TreeAsync()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "the session's work", "the work");
        return (root, trees, tree, (await RevAsync(tree.Path, "HEAD"))!);
    }

    /// <summary>The branches under <c>feature/</c>: a landing makes one, never two.</summary>
    private static async Task<IReadOnlyList<string>> BranchesAsync(string root) =>
        (await GitFixture.GitAsync(root, "for-each-ref", "--format=%(refname:short)", "refs/heads/feature/"))
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static async Task<string?> RevAsync(string cwd, string what)
    {
        var run = await GitFixture.RunAsync(cwd, "rev-parse", "--verify", "--quiet", what);
        return run.ExitCode == 0 ? run.Stdout.Trim() : null;
    }

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitFixture.GitAsync(tree, "add", "-A");
        await GitFixture.GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }
}
