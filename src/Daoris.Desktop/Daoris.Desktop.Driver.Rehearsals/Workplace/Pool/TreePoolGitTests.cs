using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The pool's acts over real git and files: what only the disk or git shows; the rest runs in <c>TreePoolMemoryTests</c>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreePoolGitTests : IDisposable
{
    private PoolGitFixture? _fixture;

    public void Dispose() => _fixture?.Dispose();

    private async Task<PoolGitFixture> FixtureAsync() => _fixture = await PoolGitFixture.CreateAsync();

    [Fact]
    public async Task A_line_lets_its_slot_go_and_another_line_takes_it_over_real_files()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var first = await fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        Assert.Equal(LineEndKinds.Output, pool.LastEnd(PoolGitFixture.Workspace, PoolGitFixture.Repository, "s-aaaa0001")!.Kind);
        Assert.True(File.Exists(Path.Combine(first.Path, "work.txt")));

        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));

        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        Assert.False(File.Exists(Path.Combine(first.Path, "work.txt")));
        Assert.Equal("daoris/s-bbbb0001", (await GitAsync(first.Path, "branch", "--show-current")).Trim());
        Assert.Equal(PoolLocks.Free, LockReasonAsync(first.Path));
    }

    [Theory]
    [InlineData("scratch", SlotJudgement.LinesOwn)]
    [InlineData("repository", SlotJudgement.RepositoryInside)]
    [InlineData("half-done", SlotJudgement.HalfDone)]
    public async Task A_slot_holding_work_is_never_taken_and_nothing_is_changed(string work, string code)
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        await PlantAsync(fixture, slot, work);
        var before = await GitAsync(slot.Path, "status", "--porcelain=v1", "--ignored");
        var branch = (await GitAsync(slot.Path, "branch", "--show-current")).Trim();

        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));

        Assert.Contains(code, taken.Refused);
        Assert.Equal("s-aaaa0001", pool.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!.Line);
        Assert.Null(pool.ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
        Assert.Equal(branch, (await GitAsync(slot.Path, "branch", "--show-current")).Trim());
        Assert.Equal(before, await GitAsync(slot.Path, "status", "--porcelain=v1", "--ignored"));
        Assert.DoesNotContain("daoris/s-bbbb0001", await fixture.BranchesAsync());
    }

    [Fact]
    public async Task A_sessions_lock_is_put_on_the_slot_and_never_on_the_lines_path()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await fixture.LetGoAsync(pool, 1, "s-aaaa0001");

        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001", session: "session-2"));

        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        Assert.Equal("session-2", SessionLocks.HolderOf(slot.Path));
        Assert.False(Directory.Exists(Path.Combine(fixture.Site.Trees, "s-bbbb0001")));
    }

    [Fact]
    public async Task The_switch_holds_the_repositorys_hooks_and_the_hook_runs_in_the_files_stage_after_the_check()
    {
        var fixture = await FixtureAsync();
        var hook = Path.Combine(fixture.Root, ".git", "hooks", "post-checkout");
        await File.WriteAllTextAsync(hook, "#!/bin/sh\nprintf hooked > hook.log\n");
        var pool = fixture.Pool();
        var slot = await fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        File.Delete(Path.Combine(slot.Path, "hook.log"));

        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));

        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        Assert.False(File.Exists(Path.Combine(slot.Path, "hook.log")));
        Assert.True(TreeFilesRecord.Read(fixture.Home, slot.Path)!.HookOwed);
        await new TreeFilesStage(fixture.Home, fixture.Root, slot.Path, PoolGitFixture.Repository).RunAsync(CancellationToken.None);
        Assert.Equal("hooked", await File.ReadAllTextAsync(Path.Combine(slot.Path, "hook.log")));
    }

    [Fact]
    public async Task A_write_planted_after_the_judgement_fails_the_check_the_slot_is_put_back_and_released_and_no_record_opens()
    {
        var fixture = await FixtureAsync();
        var opened = 0;
        var pool = fixture.Pool(seams: new PoolSeams
        {
            Open = (claim, _) => { opened++; return Task.FromResult<string?>("record"); },
            BeforeSwitch = folder => File.WriteAllTextAsync(Path.Combine(folder, "planted.txt"), "late\n"),
        });
        var slot = await fixture.LetGoAsync(fixture.Pool(), 1, "s-aaaa0001");
        var tip = (await GitAsync(slot.Path, "rev-parse", "HEAD")).Trim();

        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));

        Assert.Contains(SlotJudgement.Changed, taken.Refused);
        Assert.Null(taken.Stuck);
        Assert.Equal(0, opened);
        var row = pool.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!;
        Assert.Equal(("s-aaaa0001", KeptStates.Kept), (row.Line, row.State));
        Assert.Equal(("daoris/s-aaaa0001", tip), ((await GitAsync(slot.Path, "branch", "--show-current")).Trim(), (await GitAsync(slot.Path, "rev-parse", "HEAD")).Trim()));
        Assert.Null(pool.ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
        Assert.DoesNotContain("daoris/s-bbbb0001", await fixture.BranchesAsync());
        Assert.Equal(PoolLocks.Free, LockReasonAsync(slot.Path));
    }

    [Fact]
    public async Task A_line_returns_to_its_own_slot_in_place_and_to_another_switched_to_its_branch()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var first = await fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        var tip = (await GitAsync(first.Path, "rev-parse", "HEAD")).Trim();

        var own = await pool.ReturnAsync(new PoolTake(fixture.Site, 1, "quest-1", "s-aaaa0001", tip, true, "main", null, ""));
        Assert.True(own.Ok, own.Refused ?? own.Stuck);
        Assert.Equal("s-aaaa0001", own.Slot!.Line);
        Assert.Equal(tip, (await GitAsync(first.Path, "rev-parse", "HEAD")).Trim());

        await pool.EndAsync(new PoolEnd(fixture.Site, 1, "quest-1", "session-1"));
        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));
        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        await pool.EndAsync(new PoolEnd(fixture.Site, 1, "quest-2", "session-2"));
        await fixture.MadeAsync(pool, 2, "s-cccc0001");

        var back = await pool.ReturnAsync(new PoolTake(fixture.Site, 1, "quest-1", "s-aaaa0001", tip, true, "main", null, ""));

        Assert.True(back.Ok, back.Refused ?? back.Stuck);
        Assert.Equal(1, pool.WhereIs(PoolGitFixture.Workspace, PoolGitFixture.Repository, "s-aaaa0001")!.Number);
        Assert.Equal(tip, (await GitAsync(first.Path, "rev-parse", "HEAD")).Trim());
        Assert.True(File.Exists(Path.Combine(first.Path, "work.txt")));
    }

    [Theory]
    [InlineData(LineEndKinds.Output)]
    [InlineData(LineEndKinds.NoOutput)]
    [InlineData(LineEndKinds.Uncommitted)]
    [InlineData(LineEndKinds.Held)]
    public async Task A_lines_end_is_recorded_by_what_the_slot_holds(string kind)
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await fixture.MadeAsync(pool, 1, "s-aaaa0001");
        switch (kind)
        {
            case LineEndKinds.Output:
                await File.WriteAllTextAsync(Path.Combine(slot.Path, "out.txt"), "x\n");
                await GitAsync(slot.Path, "add", "out.txt");
                await GitAsync(slot.Path, "commit", "--quiet", "-m", "out");
                break;
            case LineEndKinds.Uncommitted:
                await File.WriteAllTextAsync(Path.Combine(slot.Path, "loose.txt"), "x\n");
                break;
            case LineEndKinds.Held:
                await GitAsync(slot.Path, "switch", "--quiet", "--detach");
                break;
        }

        var ended = await pool.EndAsync(new PoolEnd(fixture.Site, 1, "quest-1", "session-1"));

        Assert.True(ended.Ok, ended.Refused ?? ended.Stuck);
        var end = pool.LastEnd(PoolGitFixture.Workspace, PoolGitFixture.Repository, "s-aaaa0001")!;
        Assert.Equal(kind, end.Kind);
        Assert.Equal((await GitAsync(slot.Path, "rev-parse", "HEAD")).Trim(), end.Tip);
        Assert.Equal(PoolLocks.Free, LockReasonAsync(slot.Path));
        Assert.Null(pool.ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
    }

    private static string? LockReasonAsync(string slot)
    {
        var git = GitBranches.GitDirectory(slot)!;
        var locked = Path.Combine(git, "locked");
        return File.Exists(locked) ? File.ReadAllText(locked).Trim() : null;
    }

    private static async Task PlantAsync(PoolGitFixture fixture, KeptTree slot, string work)
    {
        switch (work)
        {
            case "scratch":
                Directory.CreateDirectory(Path.Combine(slot.Path, "_fixtures"));
                await File.WriteAllTextAsync(Path.Combine(slot.Path, "_fixtures", "x.txt"), "x\n");
                break;
            case "repository":
                Directory.CreateDirectory(Path.Combine(slot.Path, "_fixtures", "inner"));
                await GitAsync(Path.Combine(slot.Path, "_fixtures", "inner"), "init", "--quiet");
                break;
            case "half-done":
                await File.WriteAllTextAsync(Path.Combine(GitBranches.GitDirectory(slot.Path)!, "MERGE_HEAD"), fixture.Start + "\n");
                break;
        }
    }
}
