using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1e part e1 over real git (D215's note; the integration design §4.6, §17.4): the landing and its advance write their
/// acceptance's provenance, and the one door's recorded-landing path admits a landing from its record alone, bound to the tip the record
/// holds, its gate proof read as the record keeps it, never twice and never one the line holds. Called from no production path yet.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class IntegrationRequestsGitTests : LandedFixture
{
    private const string Pattern = "feature/{quest}-{slug}";

    private static readonly Func<CancellationToken, Task<IReadOnlySet<string>>> NoneRunning =
        _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    /// <summary><c>engine</c>'s own branch rule, with <paramref name="integrate"/> or none.</summary>
    private void Save(LandingRule rule, LandingIntegrate? integrate) =>
        DriverConfig.Empty.WithLanding("engine", rule).WithLandingIntegrate("engine", integrate)
            .Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));

    /// <summary>The door over this home's own records and settings, read as the driver reads them.</summary>
    private IntegrationRequests Door(InMemoryIntegrationRequests store) =>
        new(new HomeIntegrationRecords(Home), store, () => DriverConfig.Load(DaorisHome.Entry(Home, HomeEntry.DriverConfig)));

    private static IntegrationAsk Card(string root, string branch, string route = IntegrationRoutes.Recorded) =>
        new("engine", "aurora", root, branch, route, IntegrationRequester.Card);

    /// <summary>One session's tree with one commit, landed by the person's press.</summary>
    private async Task<(string Branch, string Tip, string Tree)> LandedAsync(SessionTrees trees, string root, string quest, bool tidy = false)
    {
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, $"{quest}.txt", $"the work of {quest}\n", $"the work of {quest}");
        var landed = tidy
            ? await trees.LandAsync(opened.Path, new LandingSubject($"s-{quest}", quest, "Fix the gap"), inUse: NoneRunning)
            : await trees.LandAsync(opened.Path, new LandingSubject($"s-{quest}", quest, "Fix the gap"));
        Assert.True(landed.Landed, landed.Message);
        return (landed.Branch!, (await GitAsync(root, "rev-parse", landed.Branch!)).Trim(), opened.Path);
    }

    /// <summary>
    /// 🔴 A landing whose tree LAND3's tidy took needs no tree (design §4.6, §6.3 step 5): it is requested from its record alone, its
    /// branch standing at the recorded tip, bound to the unit and session the record holds.
    /// </summary>
    [Fact]
    public async Task A_landing_whose_tree_the_tidy_took_is_requested_from_its_record()
    {
        var root = await RepositoryAsync("engine");
        Save(new LandingRule(LandingForm.Branch, Pattern, Tidy: true), new LandingIntegrate(Request: LandingIntegrate.Owner));
        var trees = new SessionTrees(Home);
        var (branch, tip, tree) = await LandedAsync(trees, root, "q1", tidy: true);
        Assert.False(Directory.Exists(tree), "LAND3's tidy took the session's tree");
        var store = new InMemoryIntegrationRequests();

        var admitted = await Door(store).AdmitAsync(Card(root, branch));

        Assert.True(admitted.Admitted, admitted.Message);
        var entry = trees.Recorded.Of("engine", branch)!;
        Assert.Equal((tip, "s-q1", Assert.Single(entry.Units), "main"), (admitted.Request!.Tip, admitted.Request.Session, admitted.Request.Unit, admitted.Request.Line));
        Assert.Equal(new IntegrationProof(AcceptedBy.Person) { Review = entry.Review, Opinion = entry.Opinion }, admitted.Request.Proof);
        Assert.Single(store.Open());
    }

    /// <summary>
    /// 🔴 A recorded landing whose branch moved after it is refused as moved, its revision being its unit's next landing; put back at its
    /// recorded tip and merged onto the line by hand, it is refused as held, never requested again.
    /// </summary>
    [Fact]
    public async Task A_recorded_landing_whose_branch_moved_is_refused_and_one_the_line_holds_is_never_requested()
    {
        var root = await RepositoryAsync("engine");
        Save(new LandingRule(LandingForm.Branch, Pattern), new LandingIntegrate(Request: LandingIntegrate.Owner));
        var (branch, tip, _) = await LandedAsync(new SessionTrees(Home), root, "q1");
        await CommitOnAsync(root, branch, "later.txt", "a later commit\n", "a later commit");
        var store = new InMemoryIntegrationRequests();

        var moved = await Door(store).AdmitAsync(Card(root, branch));

        Assert.False(moved.Admitted, moved.Message);
        Assert.Equal(IntegrationRefusals.Moved, moved.Refusal);
        Assert.Contains($"its record holds {TextBounds.ShortCommit(tip, 8)}", moved.Message, StringComparison.Ordinal);

        await GitAsync(root, "branch", "--force", branch, tip);
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", $"merge {branch}", branch);
        var held = await Door(store).AdmitAsync(Card(root, branch));

        Assert.False(held.Admitted, held.Message);
        Assert.Equal(IntegrationRefusals.Held, held.Refusal);
        Assert.Empty(store.Open());
    }

    /// <summary>
    /// 🔴 The records-branch door refuses a session's branch (design §4.6: a unit comes only through its own landing or its recorded
    /// landing), and requests a branch of the person's records at the commit it stands at, with no workflow gate.
    /// </summary>
    [Fact]
    public async Task A_session_branch_is_refused_at_the_records_branch_door_and_a_records_branch_is_requested_with_no_gate()
    {
        var root = await RepositoryAsync("engine");
        Save(new LandingRule(LandingForm.Branch, Pattern), new LandingIntegrate(Request: LandingIntegrate.Owner));
        var opened = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the work\n", "the work");
        await GitAsync(root, "branch", "notes/records", "main");
        await CommitOnAsync(root, "notes/records", "notes.md", "the records\n", "the records");
        var store = new InMemoryIntegrationRequests();

        var session = await Door(store).AdmitAsync(Card(root, opened.Branch, IntegrationRoutes.Records));
        var records = await Door(store).AdmitAsync(Card(root, "notes/records", IntegrationRoutes.Records));

        Assert.False(session.Admitted, session.Message);
        Assert.Equal(IntegrationRefusals.SessionBranch, session.Refusal);
        Assert.True(records.Admitted, records.Message);
        Assert.Equal((await GitAsync(root, "rev-parse", "notes/records")).Trim(), records.Request!.Tip);
        Assert.Null(records.Request.Proof);
        Assert.Equal("notes/records", Assert.Single(store.Open()).Branch);
    }

    /// <summary>
    /// 🔴 The come-up sweep under <c>request: done</c> requests each standing landing of the repository once: asked again, it requests
    /// nothing more, since each has a request open.
    /// </summary>
    [Fact]
    public async Task The_sweep_requests_each_standing_landing_once()
    {
        var root = await RepositoryAsync("engine");
        Save(new LandingRule(LandingForm.Branch, Pattern, AutoAccept: true),
            new LandingIntegrate(Request: LandingIntegrate.Done, Gates: LandingIntegrate.Declared));
        var trees = new SessionTrees(Home);
        var first = await LandedAsync(trees, root, "q1");
        var second = await LandedAsync(trees, root, "q2");
        var store = new InMemoryIntegrationRequests();

        var swept = await Door(store).SweepAsync("engine", "aurora", root);
        var again = await Door(store).SweepAsync("engine", "aurora", root);

        Assert.Equal([(first.Branch, first.Tip), (second.Branch, second.Tip)], swept.Select(each => (each.Request!.Branch, each.Request.Tip)));
        Assert.All(again, each => Assert.Equal(IntegrationRefusals.Open, each.Refusal));
        Assert.Equal(2, store.Open().Count);
    }

    /// <summary>
    /// The landing writes, beside who accepted it and the rule, that <c>integrate</c> stood with its <c>request</c> and the rule as its
    /// source; an advance made under the rule as it then stands writes its own, as it writes its own <c>acceptedBy</c>, and the landing's
    /// stays as it was accepted.
    /// </summary>
    [Fact]
    public async Task A_landing_and_its_advance_each_write_the_integrate_that_stood_at_its_acceptance()
    {
        var root = await RepositoryAsync("engine");
        Save(new LandingRule(LandingForm.Branch, Pattern), new LandingIntegrate(Request: LandingIntegrate.Owner));
        var trees = new SessionTrees(Home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "work.txt", "the work\n", "the work");
        var landed = await trees.LandAsync(first.Path, new LandingSubject("s1", "q1", "Fix the gap"));
        Assert.True(landed.Landed, landed.Message);

        var entry = trees.Recorded.Of("engine", landed.Branch!)!;
        Assert.Equal((AcceptedBy.Person, new LandedIntegrate(LandingIntegrate.Owner, LandedIntegrate.Rule)), (entry.AcceptedBy, entry.Integrate));

        Save(new LandingRule(LandingForm.Branch, Pattern, AutoAccept: true),
            new LandingIntegrate(Request: LandingIntegrate.Done, Gates: LandingIntegrate.Declared));
        var next = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(next.Path, "more.txt", "more work\n", "more work");
        var advanced = await trees.LandAsync(next.Path, new LandingSubject("s2", "q1", "Fix the gap"));
        Assert.True(advanced.Landed, advanced.Message);
        Assert.Equal(landed.Branch, advanced.Branch);

        var moved = trees.Recorded.Of("engine", landed.Branch!)!;
        var advance = Assert.Single(moved.Advances);
        Assert.Equal((AcceptedBy.Person, new LandedIntegrate(LandingIntegrate.Done, LandedIntegrate.Rule)), (advance.AcceptedBy, advance.Integrate));
        Assert.Equal(new LandedIntegrate(LandingIntegrate.Owner, LandedIntegrate.Rule), moved.Integrate);
    }
}
