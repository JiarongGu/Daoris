using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDNODE1e (the land node design §3, §8 row 6): a delivery's plugin phases through its record, with a stand-in plugin. The frame carries
/// the delivery key and the mode; a key already answered makes no second effect; a lost reply is settled by <c>work/state</c> asked with the
/// key, or by the remote-tracking ref for a push (never for a pull request); a dispatch left without its answer is reconciled, never sent
/// again; a keyless plugin is reconciled by reading alone; <c>push</c> goes only to a plugin that declares it.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class DeliveryPluginPhasesTests : IDisposable
{
    private const string Named = "feature/ENG-12-tidy";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";
    private const string Address = "https://example.test/example-org/engine/pull/7";

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "delivery-plugin", Guid.NewGuid().ToString("N")[..8]);
    private readonly string _home;

    public DeliveryPluginPhasesTests()
    {
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_scratch);
    }

    /// <summary>The whole landing: the plugin is sent the key and the mode, and the delivery is delivered; a second landing of it sends nothing.</summary>
    [Fact]
    public async Task A_pull_request_delivery_tells_its_plugin_the_key_and_a_second_landing_of_it_sends_nothing()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "engine"), "engine");
        var wire = new Wire { Declares = new(true, []), Land = _ => new PluginLanding("forge", true, Address, "opened") };
        var trees = new SessionTrees(_home, wire);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "work.txt"), "the work\n");
        await GitFixture.GitAsync(tree.Path, "add", "-A");
        await GitFixture.GitAsync(tree.Path, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "the work");
        var tip = (await GitFixture.RunAsync(tree.Path, "rev-parse", "HEAD")).Stdout.Trim();
        var (store, claim) = Start(tip, DeliveryMethods.PullRequest);
        var subject = new LandingSubject("s1", "0fda18", "Tidy the readme");
        Func<CancellationToken, Task<IReadOnlySet<string>>> none = _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

        var landed = await trees.LandAsync(tree.Path, subject, inUse: none, delivery: new TreeDelivery(store, claim));

        Assert.True(landed.Landed, landed.Message);
        var sent = Assert.Single(wire.Lands);
        Assert.Equal((claim.Key, LandingModes.PullRequest), (sent.Key, sent.Mode));
        Assert.Equal(Address, landed.Plugin?.PullRequest);
        Assert.Equal(
            [DeliveryPhases.Prepared, DeliveryPhases.RefWritten, DeliveryPhases.Recorded, DeliveryPhases.PluginDispatched, DeliveryPhases.PluginAnswered],
            store.Phases(claim.Key).Select(each => each.Phase));
        Assert.Equal(DeliverySettlements.Delivered, store.Delivery(claim.Key)?.Settled);

        var again = await trees.LandAsync(tree.Path, subject, inUse: none, delivery: new TreeDelivery(store, claim));

        Assert.False(again.Landed, again.Message);
        Assert.Single(wire.Lands);
    }

    /// <summary>Success before the reply: the dispatch stands without its answer, the plugin is asked with the key and never sent again.</summary>
    [Fact]
    public async Task A_dispatch_without_its_answer_is_settled_by_work_state_with_the_key_and_the_plugin_is_not_sent_again()
    {
        var (delivering, store, claim) = Reached(DeliveryMethods.PullRequest);
        store.Begin(claim, DeliveryPhases.PluginDispatched, "{}");
        var wire = new Wire { Declares = new(true, []), State = _ => new PullRequestState(PullRequestStates.Open) { PullRequest = Address } };

        var said = await delivering.DispatchAsync(wire, Frame(), default);

        Assert.True(said.Pushed);
        Assert.Equal(Address, said.PullRequest);
        Assert.Empty(wire.Lands);
        Assert.Equal(claim.Key, Assert.Single(wire.Asked).Frame.Key);
        var phases = store.Phases(claim.Key);
        Assert.Equal((DeliveryOutcomes.Done, DeliveryOutcomes.Done), (phases[^2].Outcome, phases[^1].Outcome));
        Assert.Equal(DeliverySettlements.Delivered, Close(delivering, store, claim));
    }

    /// <summary>A lost reply with the plugin saying nothing of it: unknown, held, and a second dispatch is refused.</summary>
    [Fact]
    public async Task A_lost_reply_work_state_cannot_settle_is_held_as_unknown_and_never_resent()
    {
        var (delivering, store, claim) = Reached(DeliveryMethods.PullRequest);
        var wire = new Wire
        {
            Declares = new(true, []),
            Land = _ => new PluginLanding("forge", false, null, "no reply", Failed: true),
            State = _ => new PullRequestState(PullRequestStates.Unknown),
        };

        var first = await delivering.DispatchAsync(wire, Frame(), default);
        var second = await delivering.DispatchAsync(wire, Frame(), default);

        Assert.True(first.Failed);
        Assert.Contains("hand-off", first.Message);
        Assert.True(second.Failed);
        Assert.Single(wire.Lands);
        Assert.Equal(DeliveryOutcomes.Unknown, store.Phases(claim.Key)[^1].Outcome);
    }

    /// <summary>A keyless plugin is reconciled by reading alone: work/state is not asked, and a pull request has no proof but its answer.</summary>
    [Fact]
    public async Task A_keyless_plugin_is_never_asked_with_a_key_and_a_pull_request_it_did_not_answer_is_unknown()
    {
        var (delivering, store, claim) = Reached(DeliveryMethods.PullRequest);
        store.Begin(claim, DeliveryPhases.PluginDispatched, "{}");
        var wire = new Wire { Declares = PluginLandDeclares.None, State = _ => new PullRequestState(PullRequestStates.Open) { PullRequest = Address } };

        var said = await delivering.DispatchAsync(wire, Frame(), default);

        Assert.False(said.Pushed);
        Assert.Empty(wire.Asked);
        Assert.Empty(wire.Lands);
        Assert.Equal(DeliveryOutcomes.Unknown, store.Phases(claim.Key)[^1].Outcome);
    }

    /// <summary>A push is proved by the remote-tracking ref holding the tip, and by nothing else.</summary>
    [Fact]
    public async Task A_remote_tracking_ref_at_the_tip_proves_a_push()
    {
        var root = await GitSeed.PlainAsync(Path.Combine(_scratch, "pushed"), "engine");
        var tip = (await GitFixture.RunAsync(root, "rev-parse", "HEAD")).Stdout.Trim();
        var (delivering, store, claim) = Reached(DeliveryMethods.Push, tip);
        store.Begin(claim, DeliveryPhases.PluginDispatched, "{}");
        var wire = new Wire { Declares = new(true, [LandingModes.Push]), State = _ => new PullRequestState(PullRequestStates.Open) { PullRequest = Address } };

        var unproved = await delivering.DispatchAsync(wire, Frame(root), default);

        Assert.False(unproved.Pushed);
        Assert.Empty(wire.Asked);

        await GitFixture.GitAsync(root, "update-ref", $"refs/remotes/origin/{Named}", tip);
        var (again, againStore, againClaim) = Reached(DeliveryMethods.Push, tip, unit: "unit-2");
        againStore.Begin(againClaim, DeliveryPhases.PluginDispatched, "{}");
        var proved = await again.DispatchAsync(wire, Frame(root), default);

        Assert.True(proved.Pushed);
        Assert.Null(proved.PullRequest);
        Assert.Empty(wire.Lands);
    }

    /// <summary>The push mode goes only to a plugin that declares it: nothing is written ahead and nothing is sent.</summary>
    [Fact]
    public async Task Push_is_offered_only_to_a_plugin_that_declares_it()
    {
        var (delivering, store, claim) = Reached(DeliveryMethods.Push);
        var wire = new Wire { Declares = new(true, []) };

        var said = await delivering.DispatchAsync(wire, Frame(), default);

        Assert.True(said.Failed);
        Assert.Contains("does not declare the `push` mode", said.Message);
        Assert.Empty(wire.Lands);
        Assert.DoesNotContain(store.Phases(claim.Key), each => each.Phase == DeliveryPhases.PluginDispatched);
    }

    /// <summary>The reconciler's plugin seam settles a dispatch left without its answer from work/state, and sends nothing.</summary>
    [Fact]
    public async Task The_reconcilers_plugin_seam_settles_a_dispatch_without_its_answer_and_sends_nothing()
    {
        var (_, store, claim) = Reached(DeliveryMethods.PullRequest);
        store.Begin(claim, DeliveryPhases.PluginDispatched, "{}");
        var wire = new Wire { Declares = new(true, []), State = _ => new PullRequestState(PullRequestStates.Open) { PullRequest = Address } };
        var reconciler = new DeliveryReconciler(store, _home, _ => _scratch) { Plugin = TreeDelivering.PluginSettlement(store, _home, wire, _ => _scratch) };

        var settled = Assert.Single(await reconciler.StartAsync(default));

        Assert.Equal(DeliveryStandings.Delivered, settled.Standing);
        Assert.Empty(wire.Lands);
        Assert.Equal(DeliverySettlements.Delivered, store.Delivery(claim.Key)?.Settled);
    }

    // ——— The fixture.

    private DeliveryStore Reopened() => new(_home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell);

    private (DeliveryStore Store, DeliveryClaim Claim) Start(string tip, string method, string unit = "unit-1")
    {
        var store = Reopened();
        var intent = new DeliveryIntent(unit, "engine", tip, method, "main", "main", Named, "ENG-12", "Tidy the readme (ENG-12)", "forge", "epoch-7");
        var start = store.Start(intent, DeliveryAuthorization.Person("accept-3", "r3", "graph-digest", """{"method":"branch"}""", "[]"));
        Assert.True(start.Started, start.Refused ?? start.Held);
        return (store, start.Claim!);
    }

    /// <summary>A delivery whose branch is prepared, written and recorded, so its plugin phases come next.</summary>
    private (TreeDelivering Delivering, DeliveryStore Store, DeliveryClaim Claim) Reached(string method, string tip = Tip, string unit = "unit-1")
    {
        var (store, claim) = Start(tip, method, unit);
        foreach (var phase in new[] { DeliveryPhases.Prepared, DeliveryPhases.RefWritten, DeliveryPhases.Recorded })
        {
            store.Begin(claim, phase, "intended");
            store.End(claim, phase, DeliveryOutcomes.Done, "read", "done");
        }

        var (delivering, refused) = TreeDelivering.Read(new TreeDelivery(store, claim), _home, "engine");
        Assert.Null(refused);
        return (delivering!, store, claim);
    }

    private static string? Close(TreeDelivering delivering, DeliveryStore store, DeliveryClaim claim)
    {
        delivering.Close(new TreeLanding(true, "landed"));
        return store.Delivery(claim.Key)?.Settled;
    }

    private LandingFrame Frame(string? root = null) => new(
        "engine", "default", root ?? Path.Combine(_scratch, "engine"), Named, "main", "Tidy the readme", "0fda18", "s1", []);

    internal sealed class Wire : IWorkplacePluginWire
    {
        public PluginLandDeclares Declares { get; init; } = PluginLandDeclares.None;

        public Func<LandingFrame, PluginLanding> Land { get; init; } = _ => new PluginLanding("forge", true, null, "pushed");

        public Func<StateAsk, PullRequestState?> State { get; init; } = _ => null;

        public List<LandingFrame> Lands { get; } = [];

        public List<StateAsk> Asked { get; } = [];

        public DateTimeOffset Now => DateTimeOffset.UtcNow;

        public string? Problem(string plugin) => null;

        public PluginLandDeclares LandDeclared(string plugin) => Declares;

        public Task<PluginLanding> LandAsync(string plugin, LandingFrame frame, CancellationToken ct = default)
        {
            Lands.Add(frame);
            return Task.FromResult(Land(frame));
        }

        public Task<PluginLanding> HandAsync(string plugin, LandingFrame frame, CancellationToken ct = default) => LandAsync(plugin, frame, ct);

        public Task<IReadOnlyList<StateAnswer>> AskStatesAsync(IReadOnlyList<StateAsk> asks, CancellationToken ct = default)
        {
            Asked.AddRange(asks);
            return Task.FromResult<IReadOnlyList<StateAnswer>>([.. asks.Select(ask => new StateAnswer(ask, State(ask), null))]);
        }
    }
}
