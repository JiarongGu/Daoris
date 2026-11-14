using Daoris.Driver;
using Daoris.Ports.Repositories;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The pool's takes on a repository kept in memory: a slot let go and taken by another line with nothing of the first line's files,
/// refused for each kind of work it holds with nothing changed, its lock read by the pool's own rules, a visit holding a take back,
/// a line returned in place and to another slot, a line's end told by what its slot holds, a waiting claim left to its own run. The
/// slots are made by the real tree opening, which asks the port. What stays in the real-git classes is git's own and the disk's: a hook,
/// a nested repository, a half-done operation, the lines' scratch folder, the sessions' lock read from the checkout's files, and every
/// undo of a take or finish that deletes its branch through the journaled ref act, whose git is a held, process-owned start.
/// </summary>
public sealed class TreePoolMemoryTests : IDisposable
{
    private readonly PoolMemoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static readonly CancellationToken Ct = CancellationToken.None;

    private static TreePool Pool(PoolMemoryFixture fixture) => fixture.Pool();

    [Fact]
    public async Task A_line_lets_its_slot_go_and_another_line_takes_it_with_nothing_of_the_first_lines_files()
    {
        using var _ = _fixture.Use();
        var pool = Pool(_fixture);
        var first = await _fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        Assert.Equal(("s-aaaa0001", KeptStates.Kept), (first.Line, first.State));
        Assert.Equal("s-aaaa0001\n", _fixture.Repo.Read(first.Path, "work.txt"));

        var taken = await pool.TakeAsync(_fixture.Take(1, "s-bbbb0001"));

        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        Assert.Equal("record-s-bbbb0001", taken.Record);
        Assert.Equal(1, pool.WhereIs(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, "s-bbbb0001")!.Number);
        Assert.Null(pool.WhereIs(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, "s-aaaa0001"));
        Assert.Null(_fixture.Repo.Read(first.Path, "work.txt"));
        Assert.Equal("daoris/s-bbbb0001", await _fixture.Repo.BranchAsync(first.Path, Ct));
        Assert.Contains("daoris/s-aaaa0001", await _fixture.BranchesAsync());
        Assert.Null(pool.ClaimOn(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1));
        Assert.Equal(PoolLocks.Free, await _fixture.LockReasonAsync(first.Path));
        Assert.True(TreeFilesRecord.Read(_fixture.Home, first.Path)!.Unworked);
    }

    [Theory]
    [InlineData("untracked", SlotJudgement.Changed)]
    [InlineData("tracked", SlotJudgement.Changed)]
    [InlineData("hidden", SlotJudgement.Linked)]
    [InlineData("person-lock", SlotJudgement.Locked)]
    [InlineData("moved-head", SlotJudgement.HeadMoved)]
    public async Task A_slot_holding_work_is_never_taken_and_nothing_is_changed(string work, string code)
    {
        using var _ = _fixture.Use();
        var pool = Pool(_fixture);
        var slot = await _fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        await PlantAsync(slot, work);
        var before = await _fixture.Repo.StatusAsync(slot.Path, true, Ct);
        var branch = await _fixture.Repo.BranchAsync(slot.Path, Ct);

        var taken = await pool.TakeAsync(_fixture.Take(1, "s-bbbb0001"));

        Assert.Contains(code, taken.Refused);
        Assert.Equal("s-aaaa0001", pool.Slot(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1)!.Line);
        Assert.Null(pool.ClaimOn(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1));
        Assert.Equal(branch, await _fixture.Repo.BranchAsync(slot.Path, Ct));
        Assert.Equal(before.OrderBy(each => each.Key), (await _fixture.Repo.StatusAsync(slot.Path, true, Ct)).OrderBy(each => each.Key));
        Assert.DoesNotContain("daoris/s-bbbb0001", await _fixture.BranchesAsync());
    }

    [Fact]
    public async Task The_pools_own_reasons_and_no_lock_pass_and_a_persons_lock_does_not()
    {
        using var _ = _fixture.Use();
        var pool = Pool(_fixture);
        var slot = await _fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        Assert.True((await pool.JudgeAsync(_fixture.Site, 1)).Clean);

        await _fixture.Repo.UnlockAsync(slot.Path, slot.Path, Ct);
        Assert.True((await pool.JudgeAsync(_fixture.Site, 1)).Clean);

        await _fixture.Repo.LockAsync(slot.Path, slot.Path, "daoris session s-old", Ct);
        Assert.True((await pool.JudgeAsync(_fixture.Site, 1)).Clean);

        await _fixture.Repo.UnlockAsync(slot.Path, slot.Path, Ct);
        await _fixture.Repo.LockAsync(slot.Path, slot.Path, "the person's", Ct);
        Assert.Equal(SlotJudgement.Locked, (await pool.JudgeAsync(_fixture.Site, 1)).Code);
    }

    [Fact]
    public async Task A_visit_holds_a_take_back()
    {
        using var _ = _fixture.Use();
        var pool = Pool(_fixture);
        await _fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        Assert.Null(await pool.VisitAsync(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1, SlotVisitKinds.Terminal, "terminal-1"));

        var taken = await pool.TakeAsync(_fixture.Take(1, "s-bbbb0001"));

        Assert.Contains("terminal visit", taken.Refused);
    }

    [Fact]
    public async Task A_line_returns_to_its_own_slot_in_place_and_to_another_switched_to_its_branch()
    {
        using var _ = _fixture.Use();
        var pool = Pool(_fixture);
        var first = await _fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        var tip = (await _fixture.Repo.HeadAsync(first.Path, Ct))!;

        var own = await pool.ReturnAsync(new PoolTake(_fixture.Site, 1, "quest-1", "s-aaaa0001", tip, true, "main", null, ""));
        Assert.True(own.Ok, own.Refused ?? own.Stuck);
        Assert.Equal("s-aaaa0001", own.Slot!.Line);
        Assert.Equal(tip, await _fixture.Repo.HeadAsync(first.Path, Ct));

        await pool.EndAsync(new PoolEnd(_fixture.Site, 1, "quest-1", "session-1"));
        var taken = await pool.TakeAsync(_fixture.Take(1, "s-bbbb0001"));
        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        await pool.EndAsync(new PoolEnd(_fixture.Site, 1, "quest-2", "session-2"));
        await _fixture.MadeAsync(pool, 2, "s-cccc0001");

        var back = await pool.ReturnAsync(new PoolTake(_fixture.Site, 1, "quest-1", "s-aaaa0001", tip, true, "main", null, ""));

        Assert.True(back.Ok, back.Refused ?? back.Stuck);
        Assert.Equal(1, pool.WhereIs(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, "s-aaaa0001")!.Number);
        Assert.Equal(tip, await _fixture.Repo.HeadAsync(first.Path, Ct));
        Assert.Equal("s-aaaa0001\n", _fixture.Repo.Read(first.Path, "work.txt"));
    }

    [Theory]
    [InlineData(LineEndKinds.Output)]
    [InlineData(LineEndKinds.NoOutput)]
    [InlineData(LineEndKinds.Uncommitted)]
    [InlineData(LineEndKinds.Held)]
    public async Task A_lines_end_is_recorded_by_what_the_slot_holds(string kind)
    {
        using var _ = _fixture.Use();
        var pool = Pool(_fixture);
        var slot = await _fixture.MadeAsync(pool, 1, "s-aaaa0001");
        var repo = _fixture.Repo;
        switch (kind)
        {
            case LineEndKinds.Output:
                repo.Write(slot.Path, "out.txt", "x\n");
                Assert.True((await repo.CommitAllAsync(slot.Path, "out", Ct)).Ok);
                break;
            case LineEndKinds.Uncommitted:
                repo.Write(slot.Path, "loose.txt", "x\n");
                break;
            case LineEndKinds.Held:
                Assert.True((await repo.SwitchAsync(slot.Path, new GitSwitch(null), Ct)).Ok);
                break;
        }

        var ended = await pool.EndAsync(new PoolEnd(_fixture.Site, 1, "quest-1", "session-1"));

        Assert.True(ended.Ok, ended.Refused ?? ended.Stuck);
        var end = pool.LastEnd(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, "s-aaaa0001")!;
        Assert.Equal(kind, end.Kind);
        Assert.Equal(await repo.HeadAsync(slot.Path, Ct), end.Tip);
        Assert.Equal(PoolLocks.Free, await _fixture.LockReasonAsync(slot.Path));
        Assert.Null(pool.ClaimOn(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1));
    }

    [Fact]
    public async Task A_waiting_claim_is_never_taken_by_its_own_run_while_its_step_runs()
    {
        using var _ = _fixture.Use();
        await _fixture.LetGoAsync(Pool(_fixture), 1, "s-aaaa0001");
        var pool = Pool(_fixture);
        var claimed = pool.Claim(new TreeClaimOpening(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1, TreeClaimKinds.Take, "quest-2", "s-bbbb0001", "daoris/s-bbbb0001", _fixture.Start, "", PoolGit.NewJobName()));
        pool.Undoing(claimed.Fence!);
        Assert.Equal("", pool.Slot(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1)!.Line);

        Assert.Contains("own", pool.TakeOver(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1, claimed.Claim!.ClaimGeneration).Refused);

        pool.Advance(claimed.Fence!, TreeClaimPhases.UndoWaiting);
        var taken = pool.TakeOver(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1, claimed.Claim!.ClaimGeneration);
        Assert.Equal(claimed.Claim.ClaimGeneration + 1, taken.Claim?.ClaimGeneration);
        Assert.Contains("took it first", pool.TakeOver(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1, claimed.Claim.ClaimGeneration).Refused);
    }

    private async Task PlantAsync(KeptTree slot, string work)
    {
        var repo = _fixture.Repo;
        switch (work)
        {
            case "untracked":
                repo.Write(slot.Path, "loose.txt", "x\n");
                break;
            case "tracked":
                repo.Write(slot.Path, "README.md", "edited\n");
                break;
            case "hidden":
                repo.Hide(slot.Path, "README.md");
                break;
            case "person-lock":
                await repo.UnlockAsync(slot.Path, slot.Path, Ct);
                await repo.LockAsync(slot.Path, slot.Path, "the person's", Ct);
                break;
            case "moved-head":
                await repo.SwitchAsync(slot.Path, new GitSwitch(null), Ct);
                repo.Write(slot.Path, "by-hand.txt", "x\n");
                await repo.CommitAllAsync(slot.Path, "by hand", Ct);
                break;
        }
    }
}
