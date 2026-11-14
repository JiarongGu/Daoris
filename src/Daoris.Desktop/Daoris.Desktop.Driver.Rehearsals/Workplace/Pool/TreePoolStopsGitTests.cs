using System.Diagnostics;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A killed process's leavings: a git lock file, and a git still running in the claim's job (the rest: <see cref="TreePoolStopsMemoryTests"/>).</summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreePoolStopsGitTests : IDisposable
{
    private PoolGitFixture? _fixture;

    public void Dispose() => _fixture?.Dispose();

    private sealed class Stopped : Exception;

    private static PoolSeams StopAt(string phase, Func<TreeClaim, CancellationToken, Task<string?>>? open = null) => new()
    {
        Open = open ?? ((claim, _) => Task.FromResult<string?>($"record-{claim.Line}")),
        Reached = (reached, _) => reached == phase ? throw new Stopped() : Task.CompletedTask,
    };

    private async Task<(PoolGitFixture Fixture, KeptTree Slot)> StoppedTakeAsync(string phase)
    {
        _fixture = await PoolGitFixture.CreateAsync();
        var slot = await _fixture.LetGoAsync(_fixture.Pool(), 1, "s-aaaa0001");
        await Assert.ThrowsAsync<Stopped>(() => _fixture.Pool(seams: StopAt(phase)).TakeAsync(_fixture.Take(1, "s-bbbb0001")));
        Assert.NotNull(_fixture.Pool().ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
        return (_fixture, slot);
    }

    private static TreePool Looker(PoolGitFixture fixture, PoolSeams? seams = null) => fixture.Pool("run-b", PoolGitFixture.DeadAre("run-a"), seams);

    [Fact]
    public async Task A_git_lock_file_left_behind_holds_the_slot_stuck_until_a_person_removes_it()
    {
        var (fixture, slot) = await StoppedTakeAsync(TreeClaimPhases.Switched);
        var lockFile = Path.Combine(GitBranches.GitDirectory(slot.Path)!, "index.lock");
        await File.WriteAllTextAsync(lockFile, "");

        var recovered = await Looker(fixture).RecoverAsync(fixture.Site, 1);

        Assert.Contains("index.lock", recovered.Stuck);
        Assert.Equal(KeptStates.Stuck, Looker(fixture).Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!.State);
        Assert.True(File.Exists(lockFile));
        Assert.Null(Looker(fixture).ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
    }

    [Fact]
    public async Task A_git_process_still_running_in_the_claims_job_is_ended_before_the_takeover_reads_anything()
    {
        _fixture = await PoolGitFixture.CreateAsync();
        var slot = await _fixture.LetGoAsync(_fixture.Pool(), 1, "s-aaaa0001");
        var job = PoolGit.NewJobName();
        var claimed = _fixture.Pool().Claim(new TreeClaimOpening(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1, TreeClaimKinds.Take, "quest-2", "s-bbbb0001", "daoris/s-bbbb0001", _fixture.Start, "", job));
        Assert.NotNull(claimed.Fence);
        using var held = ProcessJob.CreateNamed(job).Job!;
        using var running = Process.Start(new ProcessStartInfo("ping", "-n 60 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true })!;
        Assert.True(held.Join(running.Handle));

        await Looker(_fixture).RecoverAsync(_fixture.Site, 1);

        Assert.True(running.WaitForExit(10_000), "the job's process was ended");
        Assert.Equal("s-aaaa0001", Looker(_fixture).WhereIs(PoolGitFixture.Workspace, PoolGitFixture.Repository, "s-aaaa0001")?.Line ?? slot.Line);
    }
}
