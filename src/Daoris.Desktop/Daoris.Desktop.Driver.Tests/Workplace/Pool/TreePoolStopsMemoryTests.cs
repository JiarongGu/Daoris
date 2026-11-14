using Daoris.Driver;
using Daoris.Ports.Repositories;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The pool's acts stopped at each boundary, as a killed process leaves them, on a repository kept in memory: a second pool under a run of
/// its own, the first dead, takes the claim over and reads the repository, never the phase alone. The take's own branch is deleted by the
/// journaled ref act, whose held git the memory repository answers, so each case that ends in that delete runs the act's journal for real.
/// What stays a real-git class is a process in the claim's job and a lock file's disk.
/// </summary>
public sealed class TreePoolStopsMemoryTests : IDisposable
{
    private const string A = "s-aaaa0001";

    private const string B = "s-bbbb0001";

    private readonly PoolMemoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private sealed class Stopped : Exception;

    private static PoolSeams StopAt(string phase) => new()
    {
        Open = (claim, _) => Task.FromResult<string?>($"record-{claim.Line}"),
        Reached = (reached, _) => reached == phase ? throw new Stopped() : Task.CompletedTask,
    };

    private static Func<StoreRun, bool> DeadAre(params string[] runs) => run => !runs.Contains(run.Run);

    private TreePool Looker(PoolSeams? seams = null) => _fixture.Pool("run-b", DeadAre("run-a"), seams);

    private TreeClaim? Claim(TreePool pool) => pool.ClaimOn(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1);

    private async Task<KeptTree> StoppedTakeAsync(string phase)
    {
        var slot = await _fixture.LetGoAsync(_fixture.Pool(), 1, A);
        await Assert.ThrowsAsync<Stopped>(() => _fixture.Pool(seams: StopAt(phase)).TakeAsync(_fixture.Take(1, B)));
        Assert.NotNull(Claim(_fixture.Pool()));
        return slot;
    }

    [Theory]
    [InlineData(TreeClaimPhases.Switched)]
    [InlineData(TreeClaimPhases.Cleared)]
    [InlineData(TreeClaimPhases.Checked)]
    public async Task A_take_stopped_after_its_switch_and_before_any_open_is_put_back_on_the_previous_lines_branch_and_its_own_deleted_through_the_journal(string phase)
    {
        using var _ = _fixture.Use();
        var slot = await StoppedTakeAsync(phase);
        var tip = Looker().LastEnd(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, A)!.Tip;
        Assert.NotEqual(_fixture.Start, tip);

        var recovered = await Looker().RecoverAsync(_fixture.Site, 1);

        Assert.True(recovered.Ok, recovered.Refused ?? recovered.Stuck);
        var row = Looker().Slot(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1)!;
        Assert.Equal((A, KeptStates.Kept), (row.Line, row.State));
        Assert.DoesNotContain($"daoris/{B}", await _fixture.BranchesAsync());
        Assert.Equal(($"daoris/{A}", tip), (await _fixture.Repo.BranchAsync(slot.Path, default), await _fixture.Repo.HeadAsync(slot.Path, default)));
        Assert.Null(Claim(Looker()));
        Assert.Null(Looker().WhereIs(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, B));
        Assert.Contains(_fixture.Repo.HeldRuns, run => run.Arguments is ["update-ref", "--stdin"] && run.Input!.Contains($"refs/heads/daoris/{B}", StringComparison.Ordinal));
        Assert.True(Directory.EnumerateFiles(Path.Combine(_fixture.Home, "work"), "*", SearchOption.AllDirectories).Any(), "the journal kept the delete's act");
    }

    [Fact]
    public async Task A_take_stopped_before_its_switch_is_dropped_and_the_slot_is_as_it_was()
    {
        using var _ = _fixture.Use();
        var slot = await StoppedTakeAsync(TreeClaimPhases.Judged);

        var recovered = await Looker().RecoverAsync(_fixture.Site, 1);

        Assert.True(recovered.Ok, recovered.Refused ?? recovered.Stuck);
        Assert.Equal(A, Looker().Slot(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1)!.Line);
        Assert.Equal($"daoris/{A}", await _fixture.Repo.BranchAsync(slot.Path, default));
    }

    [Fact]
    public async Task A_take_that_throws_leaves_its_claim_to_its_own_runs_look_which_puts_the_slot_back()
    {
        using var _ = _fixture.Use();
        var slot = await _fixture.LetGoAsync(_fixture.Pool(), 1, A);
        var seams = new PoolSeams { BeforeSwitch = _ => throw new IOException("a process holds a copy open") };
        await Assert.ThrowsAsync<IOException>(() => _fixture.Pool(seams: seams).TakeAsync(_fixture.Take(1, B)));
        Assert.NotNull(Claim(_fixture.Pool()));

        var recovered = await _fixture.Pool().RecoverAsync(_fixture.Site, 1);

        Assert.True(recovered.Ok, recovered.Refused ?? recovered.Stuck);
        Assert.Null(Claim(_fixture.Pool()));
        Assert.Equal(A, _fixture.Pool().Slot(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1)!.Line);
        Assert.Equal($"daoris/{A}", await _fixture.Repo.BranchAsync(slot.Path, default));
    }

    [Fact]
    public async Task A_live_runs_claim_is_never_taken_over()
    {
        using var _ = _fixture.Use();
        await StoppedTakeAsync(TreeClaimPhases.Switched);

        var recovered = await _fixture.Pool("run-b", _ => true).RecoverAsync(_fixture.Site, 1);

        Assert.Contains("still runs", recovered.Refused);
        Assert.NotNull(Claim(_fixture.Pool()));
    }

    [Fact]
    public async Task The_opens_answer_lost_the_takeover_finds_the_open_and_the_slot_serves_the_line()
    {
        using var _ = _fixture.Use();
        await StoppedTakeAsync(TreeClaimPhases.OpeningRecord);
        Assert.NotEmpty(Claim(Looker())!.Opens);

        var recovered = await Looker(new PoolSeams { Opened = (_, _) => Task.FromResult((PoolOpens.Landed, "record-found")) }).RecoverAsync(_fixture.Site, 1);

        Assert.True(recovered.Ok, recovered.Refused ?? recovered.Stuck);
        Assert.Equal("record-found", recovered.Record);
        Assert.Equal(1, Looker().WhereIs(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, B)!.Number);
        Assert.Contains($"daoris/{B}", await _fixture.BranchesAsync());
    }

    [Fact]
    public async Task An_open_that_never_landed_frees_the_slot_and_deletes_the_new_branch()
    {
        using var _ = _fixture.Use();
        await StoppedTakeAsync(TreeClaimPhases.OpeningRecord);

        var recovered = await Looker(new PoolSeams { Opened = (_, _) => Task.FromResult((PoolOpens.Never, "")) }).RecoverAsync(_fixture.Site, 1);

        Assert.True(recovered.Ok, recovered.Refused ?? recovered.Stuck);
        Assert.Null(Looker().WhereIs(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, B));
        Assert.DoesNotContain($"daoris/{B}", await _fixture.BranchesAsync());
    }

    [Fact]
    public async Task A_service_that_does_not_answer_holds_the_claim_and_never_frees_the_slot()
    {
        using var _ = _fixture.Use();
        await StoppedTakeAsync(TreeClaimPhases.OpeningRecord);

        var recovered = await Looker(new PoolSeams { Opened = (_, _) => Task.FromResult((PoolOpens.NoAnswer, "")) }).RecoverAsync(_fixture.Site, 1);

        Assert.Contains("did not answer", recovered.Refused);
        Assert.NotNull(Claim(Looker()));
        Assert.Contains($"daoris/{B}", await _fixture.BranchesAsync());
    }

    [Fact]
    public async Task Two_ways_back_of_one_line_let_one_through()
    {
        using var _ = _fixture.Use();
        var pool = _fixture.Pool();
        var first = await _fixture.LetGoAsync(pool, 1, A);
        await _fixture.MadeAsync(pool, 2, "s-cccc0001");
        await pool.EndAsync(new PoolEnd(_fixture.Site, 2, "quest-3", "session-3"));
        var tip = (await _fixture.Repo.HeadAsync(first.Path, default))!;
        await pool.TakeAsync(_fixture.Take(1, B));
        await pool.EndAsync(new PoolEnd(_fixture.Site, 1, "quest-2", "session-2"));
        var reached = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var slow = _fixture.Pool("run-c", seams: new PoolSeams
        {
            Open = PoolGitFixture.Landing().Open,
            Reached = async (phase, _) =>
            {
                if (phase != TreeClaimPhases.Switched) return;
                reached.SetResult();
                await release.Task;
            },
        });

        var one = slow.ReturnAsync(new PoolTake(_fixture.Site, 1, "quest-1", A, tip, true, "main", null, ""));
        await reached.Task;
        var two = await _fixture.Pool("run-d").ReturnAsync(new PoolTake(_fixture.Site, 2, "quest-1", A, tip, true, "main", null, ""));
        release.SetResult();

        Assert.Contains("claim in flight", two.Refused);
        Assert.True((await one).Ok);
    }
}
