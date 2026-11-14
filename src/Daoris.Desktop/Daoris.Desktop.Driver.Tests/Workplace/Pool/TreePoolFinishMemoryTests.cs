using Daoris.Driver;
using Daoris.Ports.Repositories;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A line's finish, a retirement, and each stopped at its phases, on a repository kept in memory: the line's commit judged finished, its
/// branch deleted through the journaled ref act with a recovery ref holding the commit, the slot detached at the tip, and a line that is
/// not finished keeping its branch. What stays real-git is the set-aside of the line's own files on the disk.
/// </summary>
public sealed class TreePoolFinishMemoryTests : IDisposable
{
    private const string A = "s-aaaa0001";

    private const string Branch = "daoris/" + A;

    private readonly PoolMemoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static readonly CancellationToken Ct = CancellationToken.None;

    private PoolFinish Finish() => new(_fixture.Site, "quest-1", A, "main");

    private TreePool Looker() => _fixture.Pool("run-b", run => run.Run != "run-a");

    /// <summary>Seams for a retirement: once the pool has moved the slot's folder away, the repository's record of it is a folder that is gone.</summary>
    private PoolSeams Moves(KeptTree slot, bool stops) => new()
    {
        Reached = (reached, _) =>
        {
            if (reached != TreeClaimPhases.Moved) return Task.CompletedTask;
            _fixture.Repo.Vanish(slot.Path);
            return stops ? throw new InvalidOperationException("stopped") : Task.CompletedTask;
        },
    };

    private TreeClaim? Claim(TreePool pool) => pool.ClaimOn(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1);

    private async Task<(TreePool Pool, KeptTree Slot, string Tip)> WorkedAsync()
    {
        var pool = _fixture.Pool();
        var slot = await _fixture.MadeAsync(pool, 1, A);
        _fixture.Repo.Write(slot.Path, "src/Work.cs", "class Work {}\n");
        Assert.True((await _fixture.Repo.CommitAllAsync(slot.Path, "work", Ct)).Ok);
        Assert.True((await pool.EndAsync(new PoolEnd(_fixture.Site, 1, "quest-1", "session-1"))).Ok);
        return (pool, slot, (await _fixture.Repo.HeadAsync(slot.Path, Ct))!);
    }

    [Fact]
    public async Task A_finish_detaches_the_slot_deletes_the_branch_through_the_journal_and_keeps_its_commit_under_a_recovery_ref()
    {
        using var _ = _fixture.Use();
        var (pool, slot, tip) = await WorkedAsync();

        var done = await pool.FinishAsync(Finish());

        Assert.True(done.Ok, done.Refused ?? done.Stuck);
        var row = pool.Slot(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1)!;
        Assert.Equal(("", tip, KeptStates.Kept), (row.Line, row.Tip, row.State));
        Assert.Null(await _fixture.Repo.BranchAsync(slot.Path, Ct));
        Assert.Equal(tip, await _fixture.Repo.HeadAsync(slot.Path, Ct));
        Assert.DoesNotContain(Branch, await _fixture.BranchesAsync());
        Assert.Equal(tip, _fixture.Repo.Ref($"refs/daoris/discarded/{Branch}"));
        Assert.Contains(_fixture.Repo.HeldRuns, run => run.Arguments is ["update-ref", "--stdin"] && run.Input!.Contains($"delete refs/heads/{Branch} {tip}", StringComparison.Ordinal));
        Assert.Null(Claim(pool));
    }

    [Theory]
    [InlineData("moved")]
    [InlineData("checked-out")]
    [InlineData("unknown")]
    public async Task A_line_that_is_not_finished_keeps_its_branch(string why)
    {
        using var _ = _fixture.Use();
        var pool = _fixture.Pool();
        var slot = await _fixture.MadeAsync(pool, 1, A);
        _fixture.Repo.Write(slot.Path, "work.txt", "x\n");
        Assert.True((await _fixture.Repo.CommitAllAsync(slot.Path, "work", Ct)).Ok);
        if (why != "unknown") Assert.True((await pool.EndAsync(new PoolEnd(_fixture.Site, 1, "quest-1", "session-1"))).Ok);
        if (why == "moved")
        {
            _fixture.Repo.Write(slot.Path, "later.txt", "y\n");
            Assert.True((await _fixture.Repo.CommitAllAsync(slot.Path, "later", Ct)).Ok);
        }

        if (why == "checked-out")
        {
            Assert.True((await pool.TakeAsync(_fixture.Take(1, "s-bbbb0001"))).Ok);
            var elsewhere = Path.Combine(Path.GetDirectoryName(_fixture.Root)!, "elsewhere");
            Assert.True((await _fixture.Repo.AddWorktreeAsync(_fixture.Root, new GitWorktreeAdd(elsewhere, _fixture.Start), Ct)).Ok);
            Assert.True((await _fixture.Repo.SwitchAsync(elsewhere, new GitSwitch(Branch), Ct)).Ok);
        }

        var done = await pool.FinishAsync(Finish());

        Assert.NotNull(done.Refused);
        Assert.Contains(Branch, await _fixture.BranchesAsync());
        Assert.Null(Claim(pool));
    }

    [Theory]
    [InlineData(TreeClaimPhases.SettingAside)]
    [InlineData(TreeClaimPhases.SetAside)]
    [InlineData(TreeClaimPhases.Detached)]
    public async Task A_finish_stopped_at_each_of_its_phases_is_settled_by_the_look_and_loses_nothing(string phase)
    {
        using var _ = _fixture.Use();
        var (pool, slot, tip) = await WorkedAsync();
        var stopping = _fixture.Pool(seams: new PoolSeams { Reached = (reached, _) => reached == phase ? throw new InvalidOperationException("stopped") : Task.CompletedTask });
        await Assert.ThrowsAsync<InvalidOperationException>(() => stopping.FinishAsync(Finish()));

        var recovered = await Looker().RecoverAsync(_fixture.Site, 1);

        Assert.True(recovered.Ok, recovered.Refused ?? recovered.Stuck);
        Assert.Null(Claim(pool));
        if (phase == TreeClaimPhases.SettingAside)
        {
            Assert.Contains(Branch, await _fixture.BranchesAsync());
        }
        else
        {
            Assert.DoesNotContain(Branch, await _fixture.BranchesAsync());
            Assert.Equal(tip, _fixture.Repo.Ref($"refs/daoris/discarded/{Branch}"));
            Assert.Null(await _fixture.Repo.BranchAsync(slot.Path, Ct));
        }
    }

    [Fact]
    public async Task A_slot_is_retired_whole_and_git_forgets_it_and_a_retired_slot_leaves_the_pool()
    {
        using var _ = _fixture.Use();
        var pool = _fixture.Pool();
        var slot = await _fixture.LetGoAsync(pool, 1, A);
        var moving = _fixture.Pool(seams: Moves(slot, stops: false));

        var done = await moving.RetireAsync(_fixture.Site, 1, "quest-1");

        Assert.True(done.Ok, done.Refused ?? done.Stuck);
        Assert.Null(pool.Slot(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1));
        Assert.Null(Claim(pool));
        Assert.False(Directory.Exists(slot.Path));
        Assert.DoesNotContain(await _fixture.Repo.WorktreesAsync(_fixture.Root, Ct) ?? [], each => TreeGit.SamePath(each.Path, slot.Path));
        Assert.Contains(Branch, await _fixture.BranchesAsync());
    }

    [Fact]
    public async Task A_slot_holding_work_is_never_retired()
    {
        using var _ = _fixture.Use();
        var pool = _fixture.Pool();
        var slot = await _fixture.LetGoAsync(pool, 1, A);
        _fixture.Repo.Write(slot.Path, "loose.txt", "x\n");

        var done = await pool.RetireAsync(_fixture.Site, 1, "quest-1");

        Assert.Contains(SlotJudgement.Changed, done.Refused);
        Assert.NotNull(pool.Slot(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1));
        Assert.True(Directory.Exists(slot.Path));
    }

    [Fact]
    public async Task A_retirement_stopped_after_its_move_drops_git_s_record_and_the_row()
    {
        using var _ = _fixture.Use();
        var pool = _fixture.Pool();
        var slot = await _fixture.LetGoAsync(pool, 1, A);
        var stopping = _fixture.Pool(seams: Moves(slot, stops: true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => stopping.RetireAsync(_fixture.Site, 1, "quest-1"));

        var recovered = await Looker().RecoverAsync(_fixture.Site, 1);

        Assert.True(recovered.Ok, recovered.Refused ?? recovered.Stuck);
        Assert.Null(pool.Slot(PoolMemoryFixture.Workspace, PoolMemoryFixture.Repository, 1));
        Assert.DoesNotContain(await _fixture.Repo.WorktreesAsync(_fixture.Root, Ct) ?? [], each => TreeGit.SamePath(each.Path, slot.Path));
    }
}
