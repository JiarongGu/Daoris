using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LINESTOGETHER1d (the review-flow design §4): the look's pass over the operations. What the records say of the head moves it: its
/// line carried to a successor its own word opened, stale on a hand-over, its turn running. An operation whose sessions were deleted is
/// dropped, and one at <i>landing</i> after a restart is settled by its git effects, never while a landing of the head's tree is under way.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TogetherWatchTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "together-watch", Guid.NewGuid().ToString("N")[..8]);

    private string Root => Path.Combine(_scratch, "main");

    private string Home => Path.Combine(_scratch, "home");

    private string _base = "";

    public void Dispose() => GitSeed.Discard(_scratch);

    private static async Task<string> TipAsync(string tree) => (await GitFixture.GitAsync(tree, "rev-parse", "HEAD")).Trim();

    private async Task<string> TreeAsync(string name)
    {
        if (_base.Length == 0)
        {
            Directory.CreateDirectory(Root);
            await GitFixture.GitAsync(Root, "init", "-q", "-b", "main");
            await GitFixture.GitAsync(Root, "config", "user.email", "t@example.test");
            await GitFixture.GitAsync(Root, "config", "user.name", "t");
            File.WriteAllText(Path.Combine(Root, "a.txt"), "0\n");
            await GitFixture.GitAsync(Root, "add", "-A");
            await GitFixture.GitAsync(Root, "commit", "-q", "-m", "base");
            _base = await TipAsync(Root);
        }

        var tree = Path.Combine(_scratch, name);
        await GitFixture.GitAsync(Root, "worktree", "add", "-q", "-b", name, tree, _base);
        return tree;
    }

    private static async Task<string> CommitAsync(string tree, string file, string text)
    {
        File.WriteAllText(Path.Combine(tree, file), text);
        await GitFixture.GitAsync(tree, "add", "-A");
        await GitFixture.GitAsync(tree, "commit", "-q", "-m", file);
        return await TipAsync(tree);
    }

    private async Task<(TogetherOperation Operation, string Head)> BoundAsync()
    {
        var head = await TreeAsync("head");
        var member = await TreeAsync("member");
        var memberTip = await CommitAsync(member, "b.txt", "member\n");
        var press = await CommitAsync(head, "c.txt", "head\n");
        var (opened, _) = new TogetherOperations(Home).Open(
            "engine", new TogetherHead("s-head", "s-head", head, "head", press),
            [new TogetherMember("s-member", member, "member", _base, memberTip)], At);
        return (opened!, head);
    }

    private static SessionRecord Record(string id, string state = "completed", string? head = null, string? from = null, string? why = null) =>
        new(id, "engine", state) { Head = head, From = from, FromWhy = why };

    private static Dictionary<string, SessionRecord> Records(params SessionRecord[] records) =>
        records.ToDictionary(each => each.Id, StringComparer.OrdinalIgnoreCase);

    private Task<IReadOnlyList<TogetherOperation>> LookAsync(params SessionRecord[] records) =>
        new TogetherWatch(Home, () => At).LookAsync(records, (_, _, _) => Task.FromResult<string?>("main"), CancellationToken.None);

    [Fact]
    public async Task A_successor_its_own_word_opened_while_asked_carries_it_and_a_hand_over_ends_it_stale()
    {
        var (operation, _) = await BoundAsync();

        var carried = TogetherWatch.FactsOf(operation, Records(Record("s-head", head: "s-next"), Record("s-next", "running", from: "s-head")), "main")!;
        Assert.Equal(("s-next", true, null, true), (carried.HeadRecord, carried.OpenedByOwnWord, carried.MoveWhy, carried.HeadRunning));

        var handed = TogetherWatch.FactsOf(operation, Records(Record("s-head", head: "s-next"), Record("s-next", from: "s-head", why: HandOverWhy.Press)), "main")!;
        Assert.Equal((false, TogetherWhy.HandedOver), (handed.OpenedByOwnWord, handed.MoveWhy));

        var replaying = operation with { Phase = TogetherPhase.Replaying };
        Assert.False(TogetherWatch.FactsOf(replaying, Records(Record("s-head", head: "s-next"), Record("s-next", from: "s-head")), "main")!.OpenedByOwnWord);
        Assert.Null(TogetherWatch.FactsOf(operation, Records(Record("s-other")), "main"));
    }

    [Fact]
    public async Task The_look_steps_an_operation_by_its_heads_record_and_drops_one_whose_member_was_deleted()
    {
        var (operation, _) = await BoundAsync();

        var looked = Assert.Single(await LookAsync(Record("s-head", "running"), Record("s-member")));
        Assert.Equal(TogetherPhase.Replaying, looked.Phase);

        Assert.Empty(await LookAsync(Record("s-head")));
        Assert.Null(new TogetherOperations(Home).Of(operation.Id));
    }

    [Fact]
    public async Task A_landing_after_a_restart_is_settled_by_its_branch_and_waits_while_a_landing_of_the_head_holds_its_tree()
    {
        var (operation, head) = await BoundAsync();
        var store = new TogetherOperations(Home);
        var tip = await TipAsync(head);
        store.TurnEnded(operation.Id, At);
        store.Checked(operation.Id, tip, [], null, At);
        Assert.NotNull(store.Landing(operation.Id, new TogetherAttempt(TogetherForms.Branch, "daoris/landed", null, tip, At)));

        Assert.Equal(TogetherPhase.Ready, Assert.Single(await LookAsync(Record("s-head"), Record("s-member"))).Phase);

        Assert.NotNull(store.Landing(operation.Id, new TogetherAttempt(TogetherForms.Branch, "daoris/landed", null, tip, At)));
        await GitFixture.GitAsync(Root, "branch", "daoris/landed", tip);
        using (await LandingHolds.TakeAsync(Home, head))
        {
            Assert.Equal(TogetherPhase.Landing, Assert.Single(await LookAsync(Record("s-head"), Record("s-member"))).Phase);
        }

        Assert.Equal(TogetherPhase.Landed, Assert.Single(await LookAsync(Record("s-head"), Record("s-member"))).Phase);
    }

    private readonly List<(string Session, SessionEvent Note)> _kept = [];

    private Task<IReadOnlyList<TogetherOperation>> LookKeepingAsync(Func<DriverConfig>? config = null, params SessionRecord[] records) =>
        new TogetherWatch(Home, () => At, config, (session, note) => _kept.Add((session, note)))
            .LookAsync(records, (_, _, _) => Task.FromResult<string?>("main"), CancellationToken.None);

    private async Task<(TogetherOperation Operation, string Head, string Tip)> LandingAsync(string form, string target, string? expected)
    {
        var (operation, head) = await BoundAsync();
        var store = new TogetherOperations(Home);
        var tip = await TipAsync(head);
        store.TurnEnded(operation.Id, At);
        store.Checked(operation.Id, tip, [], null, At);
        Assert.NotNull(store.Landing(operation.Id, new TogetherAttempt(form, target, expected, tip, At)));
        return (operation, head, tip);
    }

    [Fact]
    public async Task A_branch_made_before_its_record_gets_the_record_written_with_its_members_and_each_member_is_noted_once()
    {
        var (_, _, tip) = await LandingAsync(TogetherForms.Branch, "daoris/landed", null);
        await GitFixture.GitAsync(Root, "branch", "daoris/landed", tip);

        Assert.Equal(TogetherPhase.Landed, Assert.Single(await LookKeepingAsync(null, Record("s-head"), Record("s-member"))).Phase);

        var written = new LandedBranches(Home).All().Single(each => each.Branch == "daoris/landed");
        Assert.Equal(("engine", "s-head", tip), (written.Repository, written.Session, written.Tip));
        Assert.Equal(["s-member"], written.Brought.Select(each => each.Session));
        var (session, note) = Assert.Single(_kept);
        Assert.Equal("s-member", session);
        Assert.Equal(NoteCodes.LandingBroughtInto.Code, Assert.Single(note.Parts!).Code);

        await LookKeepingAsync(null, Record("s-head"), Record("s-member"));
        Assert.Single(_kept);
        Assert.Single(new LandedBranches(Home).All());
    }

    [Fact]
    public async Task A_branch_moved_on_before_its_advance_gets_the_advance_written_listing_the_members()
    {
        var (_, _, tip) = await LandingAsync(TogetherForms.Branch, "daoris/landed", _base);
        await GitFixture.GitAsync(Root, "branch", "daoris/landed", tip);
        new LandedBranches(Home).Record(new LandedBranch("engine", RemoteTarget.DefaultWorkspace, "daoris/landed", "main", _base, "s-old", null, null, At.AddHours(-1)));

        Assert.Equal(TogetherPhase.Landed, Assert.Single(await LookKeepingAsync(null, Record("s-head"), Record("s-member"))).Phase);

        var advance = Assert.Single(new LandedBranches(Home).All().Single(each => each.Branch == "daoris/landed").Advances);
        Assert.Equal((_base, tip, "s-head"), (advance.From, advance.To, advance.Session));
        Assert.Equal(["s-member"], advance.Brought.Select(each => each.Session));
    }

    [Fact]
    public async Task A_merge_waits_on_its_lines_run_and_is_landed_once_the_integration_run_settles_its_request()
    {
        var (operation, _, tip) = await LandingAsync(TogetherForms.Merge, "main", _base);
        var integration = new IntegrationStore(Home);
        var request = integration.Add(new IntegrationRequest("rq1", "engine", RemoteTarget.DefaultWorkspace, "head", tip, "merge", "main",
            new IntegrationRequester("person", "accept"), new LandingIntegrate(), At))!;

        Assert.Equal(TogetherPhase.Landing, Assert.Single(await LookKeepingAsync(null, Record("s-head"), Record("s-member"))).Phase);
        Assert.Empty(_kept);

        Assert.True(integration.Close(request.Id, IntegrationRequestCloses.Settled));
        var landed = Assert.Single(await LookKeepingAsync(null, Record("s-head"), Record("s-member")));

        Assert.Equal((TogetherPhase.Landed, request.Id), (landed.Phase, landed.Attempt!.Request));
        var (session, note) = Assert.Single(_kept);
        Assert.Equal(("s-member", NoteCodes.LandingBroughtIntoLine.Code), (session, Assert.Single(note.Parts!).Code));
        Assert.Empty(new LandedBranches(Home).All());
        Assert.Equal(operation.Id, TogetherInclusion.Of(Home, "s-member")!.Operation);
    }

    /// <summary>The line declares a count file its rule settles; the member changed it after the line did, and the head replayed nothing of it.</summary>
    private async Task<string> RuleOperationAsync()
    {
        var head = await TreeAsync("head");
        await CommitAsync(head, "c.txt", "head\n");
        var press = await TipAsync(head);
        File.WriteAllText(Path.Combine(Root, "count.txt"), "n: 1\n");
        File.WriteAllText(Path.Combine(Root, "daoris.gates.json"), """{"integration":{"merge":[{"paths":"count.txt","line":"^n:","run":"echo recount"}]}}""");
        await GitFixture.GitAsync(Root, "add", "-A");
        await GitFixture.GitAsync(Root, "commit", "-q", "-m", "the line declares a count");
        var lineTip = await TipAsync(Root);
        var member = Path.Combine(_scratch, "member");
        await GitFixture.GitAsync(Root, "worktree", "add", "-q", "-b", "member", member, lineTip);
        var memberTip = await CommitAsync(member, "count.txt", "n: 2\n");
        var (opened, _) = new TogetherOperations(Home).Open("engine", new TogetherHead("s-head", "s-head", head, "head", press),
            [new TogetherMember("s-member", member, "member", lineTip, memberTip)], At);
        new TogetherOperations(Home).Replaying(opened!.Id, At);
        new TogetherOperations(Home).TurnEnded(opened.Id, At);
        return (await GitFixture.GitAsync(Root, "rev-parse", "main:daoris.gates.json")).Trim();
    }

    [Fact]
    public async Task A_path_the_lines_accepted_rules_settle_passes_the_check_the_look_runs()
    {
        var blob = await RuleOperationAsync();
        var accepted = DriverConfig.Empty.WithIntegrationGates("engine", blob);

        var looked = Assert.Single(await LookKeepingAsync(() => accepted, Record("s-head"), Record("s-member")));

        Assert.True(looked.Phase == TogetherPhase.Ready, $"{looked.Phase}: {looked.Why}");
        Assert.Contains(looked.Account, each => each.Path == "count.txt" && each.How == TogetherAccounts.Settled);
    }

    [Fact]
    public async Task The_same_path_with_no_rule_accepted_fails_the_check()
    {
        await RuleOperationAsync();

        var looked = Assert.Single(await LookKeepingAsync(null, Record("s-head"), Record("s-member")));

        Assert.Equal(TogetherPhase.Checking, looked.Phase);
        Assert.NotNull(looked.Why);
    }
}
