using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The integration run's form and replay on a repository kept in memory, for a merge rule's press, the route whose containment is read by
/// ancestry alone: a stop is about the line as it stands, a clean merge is replayed in the line's integration tree as a merge commit
/// whose second parent is the frozen tip, a request the line holds is settled at once, and a conflict aborts the merge, puts the tree
/// back at the candidate so far and hands the request back. What stays on real git is the replay by patch and content, and the
/// settling of conflicts by the repository's declared rules.
/// </summary>
public sealed class IntegrationReplayMemoryTests : IDisposable
{
    private readonly IntegrationMemoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static readonly CancellationToken Ct = CancellationToken.None;

    private IntegrationReplayAsk Ask => new("engine", "aurora", _fixture.Root, "main");

    private IntegrationReplays Replays(IntegrationStore store) => new(store, _fixture.Trees);

    private static IntegrationRequest Request(string id, string branch, string tip, DateTimeOffset at, LandingIntegrate? policy = null) =>
        new(id, "engine", "aurora", branch, tip, IntegrationRoutes.Merge, "main", IntegrationRequester.Card, (policy ?? new LandingIntegrate()).Resolved(), at);

    private static readonly DateTimeOffset Arrived = new(2026, 10, 30, 9, 0, 1, TimeSpan.Zero);

    /// <summary>A stop is about the line as it stands: its open requests form nothing again until the line moves.</summary>
    [Fact]
    public async Task A_line_whose_integration_stopped_forms_nothing_again_until_the_line_moves()
    {
        using var _ = _fixture.Use();
        var @base = _fixture.Head;
        var tip = _fixture.Branch("feature/a", @base, "a.txt", "a\n");
        var store = _fixture.Store();
        Assert.Null(store.Add(Request("ra", "feature/a", tip, Arrived, new LandingIntegrate(Batch: 8))));
        var claim = store.Form(new IntegrationForming("engine", "aurora", "main", @base, ["ra"])).Claim!;
        store.End(claim, store.OpenStep(claim.Candidate)!.Id, new IntegrationStepEnd(IntegrationStepOutcomes.Stopped)
        {
            Said = "the line's integration stopped", Ends = IntegrationCandidateEnds.Stopped,
        });
        var replays = Replays(store);

        var stopped = await replays.RunAsync(Ask, Ct);

        Assert.Equal(IntegrationReplayRuns.Idle, stopped.Kind);
        Assert.Null(store.StandingFor("engine", "main"));

        _fixture.Switch("main");
        _fixture.Commit("the line moves", "moved.txt", "moved\n");
        _fixture.Switch(null, _fixture.Head);
        var moved = await replays.RunAsync(Ask, Ct);

        Assert.Equal(IntegrationReplayRuns.Replayed, moved.Kind);
    }

    [Fact]
    public async Task A_clean_press_is_replayed_in_the_lines_tree_as_a_merge_commit_whose_second_parent_is_the_frozen_tip()
    {
        using var _ = _fixture.Use();
        var @base = _fixture.Head;
        var first = _fixture.Branch("feature/a", @base, "a.txt", "a\n");
        var second = _fixture.Branch("feature/b", @base, "b.txt", "b\n");
        var store = _fixture.Store();
        Assert.Null(store.Add(Request("ra", "feature/a", first, Arrived, new LandingIntegrate(Batch: 8))));
        Assert.Null(store.Add(Request("rb", "feature/b", second, Arrived.AddSeconds(1), new LandingIntegrate(Batch: 8))));

        var run = await Replays(store).RunAsync(Ask, Ct);

        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        Assert.All(run.Members, member => Assert.Equal(IntegrationMemberStates.Replayed, member.State));
        var tip = run.Candidate!.Tip!;
        var tree = run.Tree!;
        Assert.Equal(tip, await _fixture.Repo.HeadAsync(tree, Ct));
        Assert.Equal(second, await _fixture.Repo.CommitOfAsync(tree, "HEAD^2", Ct));
        Assert.Equal(first, await _fixture.Repo.CommitOfAsync(tree, "HEAD^1^2", Ct));
        Assert.Equal(@base, await _fixture.Repo.CommitOfAsync(tree, "HEAD^1^1", Ct));
        Assert.Equal("a\n", await _fixture.Repo.TextAtAsync(tree, tip, "a.txt", Ct));
        Assert.Equal("b\n", await _fixture.Repo.TextAtAsync(tree, tip, "b.txt", Ct));
        Assert.Equal(@base, await _fixture.Repo.CommitOfAsync(_fixture.Root, "refs/heads/main", Ct));
        Assert.Contains((tree, true), (await _fixture.Repo.WorktreesAsync(_fixture.Root, Ct))!.Select(each => (each.Path, each.Locked)));
    }

    [Fact]
    public async Task A_request_the_line_holds_by_ancestry_is_settled_at_once_and_nothing_is_merged()
    {
        using var _ = _fixture.Use();
        var held = _fixture.Branch("feature/a", _fixture.Head, "a.txt", "a\n");
        _fixture.Switch("main");
        Assert.True((await _fixture.Repo.FastForwardAsync(_fixture.Root, held, Ct)).Ok);
        var line = _fixture.Head;
        _fixture.Switch(null, line);
        var store = _fixture.Store();
        Assert.Null(store.Add(Request("ra", "feature/a", held, Arrived)));

        var run = await Replays(store).RunAsync(Ask, Ct);

        var member = Assert.Single(run.Members);
        Assert.Equal(IntegrationMemberStates.Contained, member.State);
        Assert.Equal(IntegrationRequestCloses.Settled, store.Request("ra")!.ClosedWhy);
        Assert.Equal(line, await _fixture.Repo.HeadAsync(run.Tree!, Ct));
    }

    [Fact]
    public async Task A_press_that_conflicts_is_aborted_the_tree_put_back_and_the_request_handed_back_while_the_next_goes_on()
    {
        using var _ = _fixture.Use();
        var @base = _fixture.Head;
        _fixture.Switch("main");
        _fixture.Commit("the line's readme", "README.md", "# the line's\n");
        _fixture.Switch(null, _fixture.Head);
        var line = _fixture.Head;
        var clash = _fixture.Branch("feature/clash", @base, "README.md", "# the branch's\n");
        var clean = _fixture.Branch("feature/clean", @base, "clean.txt", "clean\n");
        _fixture.Switch("main");
        _fixture.Switch(null, line);
        var store = _fixture.Store();
        Assert.Null(store.Add(Request("rclash", "feature/clash", clash, Arrived, new LandingIntegrate(Batch: 8))));
        Assert.Null(store.Add(Request("rclean", "feature/clean", clean, Arrived.AddSeconds(1), new LandingIntegrate(Batch: 8))));

        var run = await Replays(store).RunAsync(Ask, Ct);

        Assert.Equal(IntegrationReplayRuns.Replayed, run.Kind);
        Assert.Equal(
            [("rclash", IntegrationMemberStates.HandedBack), ("rclean", IntegrationMemberStates.Replayed)],
            run.Members.Select(each => (each.Request, each.State)));
        Assert.Equal(IntegrationRequestCloses.HandedBack, store.Request("rclash")!.ClosedWhy);
        Assert.Empty(await _fixture.Repo.UnmergedAsync(run.Tree!, Ct));
        Assert.Equal(clean, await _fixture.Repo.CommitOfAsync(run.Tree!, "HEAD^2", Ct));
        Assert.Equal(line, await _fixture.Repo.CommitOfAsync(run.Tree!, "HEAD^1", Ct));
        Assert.Equal("# the line's\n", _fixture.Repo.Read(run.Tree!, "README.md"));
    }
}
