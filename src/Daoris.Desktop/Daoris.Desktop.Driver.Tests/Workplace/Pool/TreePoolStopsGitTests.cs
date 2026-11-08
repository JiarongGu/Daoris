using System.Diagnostics;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The pool's acts stopped at each boundary, as a killed process leaves them: a second pool under a run of its own, the first dead, takes
/// the claim over and reads the disk, never the phase alone. Each case ends in a state the next look reads right, and no record names a slot.
/// </summary>
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

    [Theory]
    [InlineData(TreeClaimPhases.Switched)]
    [InlineData(TreeClaimPhases.Cleared)]
    [InlineData(TreeClaimPhases.Checked)]
    public async Task A_take_stopped_after_its_switch_and_before_any_open_is_detached_and_its_branch_deleted_through_the_journal(string phase)
    {
        var (fixture, slot) = await StoppedTakeAsync(phase);

        var recovered = await Looker(fixture).RecoverAsync(fixture.Site, 1);

        Assert.True(recovered.Ok, recovered.Refused ?? recovered.Stuck);
        var row = Looker(fixture).Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!;
        Assert.Equal(("", fixture.Start, KeptStates.Kept), (row.Line, row.Tip, row.State));
        Assert.DoesNotContain("daoris/s-bbbb0001", await fixture.BranchesAsync());
        Assert.Equal("", (await GitAsync(slot.Path, "branch", "--show-current")).Trim());
        Assert.Null(Looker(fixture).ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
        Assert.Null(Looker(fixture).WhereIs(PoolGitFixture.Workspace, PoolGitFixture.Repository, "s-bbbb0001"));
        Assert.True(Directory.EnumerateFiles(Path.Combine(fixture.Home, "work"), "*", SearchOption.AllDirectories).Any(), "the journal kept the delete's act");
    }

    [Fact]
    public async Task A_take_stopped_before_its_switch_is_dropped_and_the_slot_is_as_it_was()
    {
        var (fixture, slot) = await StoppedTakeAsync(TreeClaimPhases.Judged);

        var recovered = await Looker(fixture).RecoverAsync(fixture.Site, 1);

        Assert.True(recovered.Ok, recovered.Refused ?? recovered.Stuck);
        Assert.Equal("s-aaaa0001", Looker(fixture).Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!.Line);
        Assert.Equal("daoris/s-aaaa0001", (await GitAsync(slot.Path, "branch", "--show-current")).Trim());
    }

    [Fact]
    public async Task A_live_runs_claim_is_never_taken_over()
    {
        var (fixture, _) = await StoppedTakeAsync(TreeClaimPhases.Switched);

        var recovered = await fixture.Pool("run-b", _ => true).RecoverAsync(fixture.Site, 1);

        Assert.Contains("still runs", recovered.Refused);
        Assert.NotNull(fixture.Pool().ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
    }

    [Fact]
    public async Task The_opens_answer_lost_the_takeover_finds_the_open_and_the_slot_serves_the_line()
    {
        var (fixture, _) = await StoppedTakeAsync(TreeClaimPhases.OpeningRecord);
        var claim = Looker(fixture).ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!;
        Assert.NotEmpty(claim.Opens);

        var recovered = await Looker(fixture, new PoolSeams { Opened = (_, _) => Task.FromResult((PoolOpens.Landed, "record-found")) }).RecoverAsync(fixture.Site, 1);

        Assert.True(recovered.Ok, recovered.Refused ?? recovered.Stuck);
        Assert.Equal("record-found", recovered.Record);
        Assert.Equal(1, Looker(fixture).WhereIs(PoolGitFixture.Workspace, PoolGitFixture.Repository, "s-bbbb0001")!.Number);
        Assert.Contains("daoris/s-bbbb0001", await fixture.BranchesAsync());
    }

    [Fact]
    public async Task An_open_that_never_landed_frees_the_slot_and_deletes_the_new_branch()
    {
        var (fixture, _) = await StoppedTakeAsync(TreeClaimPhases.OpeningRecord);

        var recovered = await Looker(fixture, new PoolSeams { Opened = (_, _) => Task.FromResult((PoolOpens.Never, "")) }).RecoverAsync(fixture.Site, 1);

        Assert.True(recovered.Ok, recovered.Refused ?? recovered.Stuck);
        Assert.Null(Looker(fixture).WhereIs(PoolGitFixture.Workspace, PoolGitFixture.Repository, "s-bbbb0001"));
        Assert.DoesNotContain("daoris/s-bbbb0001", await fixture.BranchesAsync());
    }

    [Fact]
    public async Task A_service_that_does_not_answer_holds_the_claim_and_never_frees_the_slot()
    {
        var (fixture, _) = await StoppedTakeAsync(TreeClaimPhases.OpeningRecord);

        var recovered = await Looker(fixture, new PoolSeams { Opened = (_, _) => Task.FromResult((PoolOpens.NoAnswer, "")) }).RecoverAsync(fixture.Site, 1);

        Assert.Contains("did not answer", recovered.Refused);
        Assert.NotNull(Looker(fixture).ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
        Assert.Contains("daoris/s-bbbb0001", await fixture.BranchesAsync());
    }

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

    [Fact]
    public async Task Two_ways_back_of_one_line_let_one_through()
    {
        _fixture = await PoolGitFixture.CreateAsync();
        var pool = _fixture.Pool();
        var first = await _fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        await _fixture.MadeAsync(pool, 2, "s-cccc0001");
        await pool.EndAsync(new PoolEnd(_fixture.Site, 2, "quest-3", "session-3"));
        var tip = (await GitAsync(first.Path, "rev-parse", "HEAD")).Trim();
        await pool.TakeAsync(_fixture.Take(1, "s-bbbb0001"));
        await pool.EndAsync(new PoolEnd(_fixture.Site, 1, "quest-2", "session-2"));
        var reached = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var slow = _fixture.Pool("run-c", seams: new PoolSeams
        {
            Reached = async (phase, _) =>
            {
                if (phase != TreeClaimPhases.Switched) return;
                reached.SetResult();
                await release.Task;
            },
        });

        var one = slow.ReturnAsync(new PoolTake(_fixture.Site, 1, "quest-1", "s-aaaa0001", tip, true, "main", null, ""));
        await reached.Task;
        var two = await _fixture.Pool("run-d").ReturnAsync(new PoolTake(_fixture.Site, 2, "quest-1", "s-aaaa0001", tip, true, "main", null, ""));
        release.SetResult();

        Assert.Contains("claim in flight", two.Refused);
        Assert.True((await one).Ok);
    }
}
