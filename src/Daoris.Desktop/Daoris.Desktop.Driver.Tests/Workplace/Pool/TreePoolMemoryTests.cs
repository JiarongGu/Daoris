using Daoris.Driver;
using Daoris.Ports.Repositories;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The pool's takes on a repository kept in memory: a slot let go and taken by another line with nothing of the first line's files,
/// refused for each kind of work it holds with nothing changed, its lock read by the pool's own rules, a visit holding a take back.
/// What stays in the real-git class <c>TreePoolGitTests</c> is git's own and the disk's: a hook, a nested repository, a half-done
/// operation, the lines' scratch folder, the sessions' lock, and the undo of a take, which deletes its branch through the journaled
/// ref transaction, not yet a port.
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
