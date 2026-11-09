using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Beside a session: with trees off a driven session works in the person's
/// checkout itself, and the line's pull and a promotion each wait for it, read from the ledger inside the checkout's hold, said with the
/// session and where to find it; once its quest is done they go on at the next look. A ledger that cannot be read holds them too. The
/// ledger is the stand-in the looks read (<see cref="StandInLedger"/>), through the real client and <see cref="BranchTidying.BesideAsync"/>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class CheckoutBesideGitTests : TreeSyncFixture
{
    private string TreesHome => DaorisHome.Entry(Home, HomeEntry.Trees);

    /// <summary>The ledger holding driven session <c>s1</c> on quest <c>q1</c>, working in <paramref name="root"/>, as trees off leaves it.</summary>
    private static (StandInLedger Ledger, ServiceClient Client) WorkingIn(string root)
    {
        var ledger = new StandInLedger().Register("engine", root);
        ledger.Publish("q1", "engine");
        ledger.Driven("s1", "q1", root);
        return (ledger, ledger.Client());
    }

    /// <summary>The session's quest done and its record ended, as its run's end leaves them.</summary>
    private static async Task EndedAsync(StandInLedger ledger, ServiceClient client)
    {
        await client.AdvanceAsync("s1", "completed");
        ledger.Move("q1", "Done");
    }

    private const string Waits = "Session `s1` works in `engine`'s checkout (Repositories → the repository's page), so";

    /// <summary>🔴 Bring up to date's pull waits for the driven session in the checkout, and pulls at the next press once it ends.</summary>
    [Fact]
    public async Task A_line_s_pull_waits_for_a_driven_session_in_the_checkout_and_pulls_once_it_ends()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        var (ledger, client) = WorkingIn(root);
        var trees = new SessionTrees(Home);
        var before = await RevAsync(root, "main");

        var waited = (await trees.SyncAsync(Repositories(root), Nobody, fetch: true, scope: SyncScope.Everything,
            beside: token => BranchTidying.BesideAsync(Home, client, token))).Lines.Single();

        Assert.False(waited.Moved);
        Assert.Equal($"{Waits} `main`'s pull waits for it and goes on at the next look once it ends.", waited.Message);
        Assert.Equal(before, await RevAsync(root, "main"));

        await EndedAsync(ledger, client);
        var pulled = (await trees.SyncAsync(Repositories(root), Nobody, scope: SyncScope.Everything,
            beside: token => BranchTidying.BesideAsync(Home, client, token))).Lines.Single();

        Assert.True(pulled.Moved, pulled.Message);
        Assert.Equal(await RevAsync(root, "origin/main"), await RevAsync(root, "main"));
    }

    /// <summary>A session that ended, its quest still open, holds no checkout: the pull goes through.</summary>
    [Fact]
    public async Task A_pull_beside_an_ended_session_whose_quest_is_still_open_goes_through()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        var (_, client) = WorkingIn(root);
        await client.AdvanceAsync("s1", "completed");

        var pulled = (await new SessionTrees(Home).SyncAsync(Repositories(root), Nobody, fetch: true, scope: SyncScope.Everything,
            beside: token => BranchTidying.BesideAsync(Home, client, token))).Lines.Single();

        Assert.True(pulled.Moved, pulled.Message);
    }

    /// <summary>🔴 A promotion onto the line the checkout has out waits for the driven session there, and promotes at the next look once it ends.</summary>
    [Fact]
    public async Task A_promotion_waits_for_a_driven_session_in_the_checkout_and_promotes_once_it_ends()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/a");
        await CommitAsync(root, "a.txt", "a\n", "work on feature/a");
        var tip = await RevAsync(root, "feature/a");
        await GitAsync(root, "checkout", "--quiet", "main");
        var (ledger, client) = WorkingIn(root);
        var store = new IntegrationStore(Home, () => StoreSettled.Yes, null, "run-a", StoreProcess.Shell);
        Assert.Null(store.Add(new IntegrationRequest(
            "ra", "engine", "aurora", "feature/a", tip, IntegrationRoutes.Recorded, "main", IntegrationRequester.Card,
            new LandingIntegrate(Gates: LandingIntegrate.NoGates).Resolved(), DateTimeOffset.UtcNow)));
        var asks = new IntegrationSweepAsks(
            new IntegrationReplayAsk("engine", "aurora", root, "main") { Config = DriverConfig.Empty },
            new IntegrationPromoteAsk("engine", "aurora", root, "main", DriverConfig.Empty, Home) { Beside = token => BranchTidying.BesideAsync(Home, client, token) });
        var lines = new IntegrationLines(store, TreesHome);
        var before = await RevAsync(root, "main");

        var waited = await lines.RunAsync(asks);

        Assert.Null(store.Request("ra")!.ClosedAt);
        Assert.Contains(waited.Said, said => said.Contains($"{Waits} the promotion onto `main` waits for it and goes on at the next look once it ends: nothing was moved.", StringComparison.Ordinal));
        Assert.Equal(before, await RevAsync(root, "main"));

        await EndedAsync(ledger, client);
        await lines.RunAsync(asks);

        Assert.Equal(IntegrationRequestCloses.Settled, store.Request("ra")!.ClosedWhy);
        Assert.Equal("a", (await File.ReadAllTextAsync(Path.Combine(root, "a.txt"))).Trim());
    }

    private static readonly LandingSubject Subject = new("s9", "q9", "Cap the work per frame");

    private async Task<(string Root, TreeOpened Tree, SessionTrees Trees)> MergeRuleAsync()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "budget.ts", "cap the work per frame\n", "cap hydration");
        return (root, opened, trees);
    }

    /// <summary>A merge rule's Accept waits for the driven session in the checkout, and lands at the next look once it ends.</summary>
    [Fact]
    public async Task A_merge_rule_s_accept_waits_for_a_driven_session_in_the_checkout_and_lands_once_it_ends()
    {
        var (root, opened, trees) = await MergeRuleAsync();
        var (ledger, client) = WorkingIn(root);
        var before = await RevAsync(root, "main");

        var waited = await trees.LandAsync(opened.Path, Subject, beside: token => BranchTidying.BesideAsync(Home, client, token));

        Assert.False(waited.Landed, waited.Message);
        Assert.Contains($"{Waits} the promotion onto `main` waits for it and goes on at the next look once it ends: nothing was moved.", waited.Message, StringComparison.Ordinal);
        Assert.Equal(before, await RevAsync(root, "main"));

        await EndedAsync(ledger, client);
        var landed = await trees.LandAsync(opened.Path, Subject, beside: token => BranchTidying.BesideAsync(Home, client, token));

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal("cap the work per frame", (await File.ReadAllTextAsync(Path.Combine(root, "budget.ts"))).Trim());
    }

    /// <summary>A landing out of a session's own tree is no session in the checkout: it lands with the ledger holding that tree.</summary>
    [Fact]
    public async Task A_merge_rule_s_accept_out_of_a_sessions_own_tree_is_unaffected_by_the_ledger_holding_it()
    {
        var (root, opened, trees) = await MergeRuleAsync();
        var ledger = new StandInLedger().Register("engine", root);
        ledger.Publish("q9", "engine");
        ledger.Driven("s9", "q9", opened.Path);
        var client = ledger.Client();

        var landed = await trees.LandAsync(opened.Path, Subject, beside: token => BranchTidying.BesideAsync(Home, client, token));

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal("cap the work per frame", (await File.ReadAllTextAsync(Path.Combine(root, "budget.ts"))).Trim());
    }

    /// <summary>🔴 A ledger that cannot be read is no free checkout: the pull waits, said, and nothing moves.</summary>
    [Fact]
    public async Task A_ledger_that_cannot_be_read_holds_the_pull()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        var before = await RevAsync(root, "main");

        var waited = (await new SessionTrees(Home).SyncAsync(Repositories(root), Nobody, fetch: true, scope: SyncScope.Everything,
            beside: _ => throw new HttpRequestException("the service did not answer"))).Lines.Single();

        Assert.False(waited.Moved);
        Assert.Equal(
            "The ledger could not be read (the service did not answer), so whether a session works in `engine`'s checkout is not known and `main`'s pull waits for the next look.",
            waited.Message);
        Assert.Equal(before, await RevAsync(root, "main"));
    }
}
