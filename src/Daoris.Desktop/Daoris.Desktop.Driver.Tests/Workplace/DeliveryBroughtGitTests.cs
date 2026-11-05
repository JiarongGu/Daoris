using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDNODE1b3 (the land node design §9's first question, decided): one Accept carrying several sessions, an entry's or LINESTOGETHER1's
/// combined Accept, is one delivery. The head's unit performs it under one fence; each member is reached as <i>brought into</i> that
/// delivery, with no delivery of its own, and while the operation binding it is open it is bound and starts none. The operation is set up
/// as <see cref="TogetherAcceptsTests"/> sets it up.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class DeliveryBroughtGitTests : LandedFixture
{
    private const string Named = "feature/q1-bring-the-work-together";

    private static readonly DateTimeOffset At = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The combined Accept under the head's delivery lands once, on the intent's branch, carrying both members: the head's delivery is the one
    /// delivered, no member has one, each member reads <i>brought into</i> it, a second Accept and a member's own landing land nothing more.
    /// </summary>
    [Fact]
    public async Task An_Accept_carrying_several_sessions_performs_one_delivery_and_its_members_are_reached_as_brought_into_it()
    {
        var (trees, root, _, beside, operation) = await ReadyAsync();
        var store = Store();
        var claim = Start(store, operation.Checked!, DeliveryMethods.Branch);

        var landed = await trees.LandTogetherAsync(operation.Id, Subject, new Dictionary<string, IWorkplaceGate?>(), delivery: new TreeDelivery(store, claim));

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(Named, landed.Branch);
        var record = Assert.Single(new LandedBranches(Home).All());
        Assert.Equal(Named, record.Branch);
        Assert.Equal(["s-head", "s-beside", "s-other"], record.Work().Select(each => each.Session));
        Assert.Equal(operation.Members.Select(each => each.Tip), record.Brought.Select(each => each.Tip));
        Assert.Equal(DeliverySettlements.Delivered, store.Delivery(claim.Key)?.Settled);
        Assert.Equal([DeliveryPhases.Prepared, DeliveryPhases.RefWritten, DeliveryPhases.Recorded], store.Phases(claim.Key).Select(each => each.Phase));
        Assert.Empty(store.Unresolved());
        Assert.Empty(store.OfUnit("unit-beside"));

        Assert.Equal((DeliveryReaches.Delivered, claim.Key), Reach(store, "unit-head"));
        Assert.Equal((DeliveryReaches.BroughtInto, claim.Key), Reach(store, "unit-beside"));
        Assert.Equal((DeliveryReaches.BroughtInto, claim.Key), Reach(store, "unit-other"));
        Assert.Null(DeliveryReach.Of(Home, store, "unit-elsewhere"));

        var again = await trees.LandTogetherAsync(operation.Id, Subject, new Dictionary<string, IWorkplaceGate?>(), delivery: new TreeDelivery(store, claim));
        Assert.False(again.Landed, again.Message);
        var own = await trees.LandAsync(beside.Path, new LandingSubject("s-beside", "q2", "The work beside it"));
        Assert.Equal(LandingCode.Already, own.Refusal);
        Assert.Single(new LandedBranches(Home).All());
        Assert.Equal([Named], (await GitAsync(root, "for-each-ref", "--format=%(refname:short)", "refs/heads/feature/")).Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>A delivery whose tip is not the one the sessions were checked together at lands nothing, and the operation stays ready.</summary>
    [Fact]
    public async Task A_head_s_delivery_at_another_tip_than_the_checked_one_lands_nothing()
    {
        var (trees, _, _, _, operation) = await ReadyAsync();
        var store = Store();
        var claim = Start(store, operation.Head.PressTip, DeliveryMethods.Branch);

        var landed = await trees.LandTogetherAsync(operation.Id, Subject, new Dictionary<string, IWorkplaceGate?>(), delivery: new TreeDelivery(store, claim));

        Assert.False(landed.Landed, landed.Message);
        Assert.Contains("checked together", landed.Message);
        Assert.Equal(TogetherPhase.Ready, new TogetherOperations(Home).Of(operation.Id)!.Phase);
        Assert.Empty(new LandedBranches(Home).All());
        Assert.Empty(store.Phases(claim.Key));
    }

    /// <summary>While the operation binding a member is open, the member is bound to its head's Accept: no delivery of its own starts for it.</summary>
    [Fact]
    public async Task A_member_of_an_open_operation_is_bound_and_starts_no_delivery_of_its_own()
    {
        var (_, _, _, _, operation) = await ReadyAsync();

        var bound = DeliveryReach.Of(Home, Store(), "unit-beside");

        Assert.Equal(DeliveryReaches.Bound, bound?.How);
        Assert.Null(bound?.Delivery);
        Assert.Contains(operation.Id, bound?.Said);
    }

    // ——— The fixture.

    private static readonly LandingSubject Subject = new("s-head", "q1", "Bring the work together");

    private DeliveryStore Store() => new(Home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell);

    private (string? How, string? Delivery) Reach(DeliveryStore store, string unit) =>
        DeliveryReach.Of(Home, store, unit) is { } reached ? (reached.How, reached.Delivery) : default;

    private static DeliveryClaim Start(DeliveryStore store, string tip, string method)
    {
        var start = store.Start(
            new DeliveryIntent("unit-head", "engine", tip, method, "main", "main", Named, "q1", "Bring the work together", null, "epoch-1"),
            DeliveryAuthorization.Person("accept-1", "r1", "graph-digest", """{"method":"branch"}""", "[]"));
        Assert.True(start.Started, start.Refused ?? start.Held);
        return start.Claim!;
    }

    private static async Task<string> TipAsync(string tree) => (await GitAsync(tree, "rev-parse", "HEAD")).Trim();

    /// <summary>The head with both members replayed onto it, the line one commit on, and the operation checked and ready; each member names its unit.</summary>
    private async Task<(SessionTrees Trees, string Root, TreeOpened Head, TreeOpened Beside, TogetherOperation Operation)> ReadyAsync()
    {
        var root = await RepositoryAsync("engine");
        var line = await TipAsync(root);
        var trees = new SessionTrees(Home);
        var head = await trees.OpenAsync(root, "engine", "aurora");
        var beside = await trees.OpenAsync(root, "engine", "aurora");
        var other = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(head.Path, "head.txt", "head\n", "the head's work");
        await CommitAsync(beside.Path, "beside.txt", "beside\n", "the work beside it");
        await CommitAsync(other.Path, "other.txt", "other\n", "another session's work");
        var press = await TipAsync(head.Path);
        var (opened, refused) = new TogetherOperations(Home).Open("engine", new TogetherHead("s-head", "s-head", head.Path, head.Branch, press),
            [
                new TogetherMember("s-beside", beside.Path, beside.Branch, line, await TipAsync(beside.Path)) { Quest = "q2", Unit = "unit-beside" },
                new TogetherMember("s-other", other.Path, other.Branch, line, await TipAsync(other.Path)) { Quest = "q3", Unit = "unit-other" },
            ], At);
        Assert.Null(refused);
        foreach (var member in opened!.Members) await GitAsync(head.Path, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "cherry-pick", member.Tip);
        await CommitAsync(root, "line.txt", "the line moved\n", "the line moves after the head's turn");

        new TogetherOperations(Home).TurnEnded(opened.Id, At);
        var ready = await new TogetherLook(Home, () => At).StepAsync(
            root, opened.Id, new TogetherFacts("s-head", false, null, false, "main"), null, CancellationToken.None);
        Assert.True(ready!.Phase == TogetherPhase.Ready, $"{ready.Phase}: {ready.Why}");
        return (trees, root, head, beside, ready);
    }
}
