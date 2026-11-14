using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A take or a return refused after its switch, put back as it found the slot, over real git and at different tips: a held output at the
/// clearing, the previous line's branch out in another checkout or moved on meanwhile, a return cut off before and after its clearing, a
/// refused switch, and the clearing's move-back. Each case has a scratch home and repository of its own.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PoolUndoGitTests : IDisposable
{
    private const string A = "s-aaaa0001";

    private const string B = "s-bbbb0001";

    private const string Rules = """{ "rules": [ { "kind": "output", "paths": ["gen/", "obj/"] } ] }""";

    private PoolGitFixture? _fixture;

    public void Dispose()
    {
        PoolAsides.RecordGoes = _ => true;
        _fixture?.Dispose();
    }

    private sealed class Stopped : Exception;

    /// <summary>A repository whose <c>gen/</c> and <c>obj/</c> are outputs, a slot let go on A's branch at A's own commit, and both outputs built in it.</summary>
    private async Task<(PoolGitFixture Fixture, KeptTree Slot, string Tip)> LetGoWithOutputsAsync()
    {
        var fixture = _fixture = await PoolGitFixture.CreateAsync(Rules);
        await File.AppendAllTextAsync(Path.Combine(fixture.Root, ".gitignore"), "gen/\n");
        await GitAsync(fixture.Root, "commit", "--quiet", "-am", "gen");
        fixture.Start = (await GitAsync(fixture.Root, "rev-parse", "HEAD")).Trim();
        var slot = await fixture.LetGoAsync(fixture.Pool(), 1, A);
        await BuildAsync(slot);
        var tip = await HeadAsync(slot);
        Assert.NotEqual(fixture.Start, tip);
        return (fixture, slot, tip);
    }

    private static async Task BuildAsync(KeptTree slot)
    {
        Directory.CreateDirectory(Path.Combine(slot.Path, "gen"));
        Directory.CreateDirectory(Path.Combine(slot.Path, "obj"));
        await File.WriteAllTextAsync(Path.Combine(slot.Path, "gen", "made.txt"), "generated\n");
        await File.WriteAllTextAsync(Path.Combine(slot.Path, "obj", "Engine.dll"), "built\n");
    }

    private static FileStream Hold(KeptTree slot) => new(Path.Combine(slot.Path, "obj", "Engine.dll"), FileMode.Open, FileAccess.Read, FileShare.None);

    private static async Task<string> HeadAsync(KeptTree slot) => (await GitAsync(slot.Path, "rev-parse", "HEAD")).Trim();

    private static async Task<string> BranchAsync(KeptTree slot) => (await GitAsync(slot.Path, "branch", "--show-current")).Trim();

    private static KeptTree Row(TreePool pool) => pool.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!;

    private static TreeClaim? ClaimOn(TreePool pool) => pool.ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1);

    private static LineWhere WhereA(PoolGitFixture fixture) =>
        new LinePlaces(fixture.Home).Of(PoolGitFixture.Workspace, PoolGitFixture.Repository, Path.Combine(fixture.Site.Trees, A));

    private static string AsideRoot(PoolGitFixture fixture) =>
        Path.Combine(DaorisHome.Entry(fixture.Home, HomeEntry.Trees), TreeSetAside.SetAsideFolder, PoolGitFixture.Workspace, PoolGitFixture.Repository);

    private static string? LockOf(KeptTree slot)
    {
        var locked = Path.Combine(GitBranches.GitDirectory(slot.Path)!, "locked");
        return File.Exists(locked) ? File.ReadAllText(locked).Trim() : null;
    }

    private static PoolSeams Seams(Func<string, string, Task>? reached = null, Func<string, string, Task>? renaming = null, Func<string, Task>? beforeSwitch = null) => new()
    {
        Open = PoolGitFixture.Landing().Open,
        Reached = reached,
        Renaming = renaming,
        BeforeSwitch = beforeSwitch,
    };

    [Fact]
    public async Task A_held_output_at_the_clearing_refuses_the_take_busy_and_puts_the_slot_back_on_the_previous_lines_branch_at_its_tip()
    {
        var (fixture, slot, tip) = await LetGoWithOutputsAsync();
        var before = await GitAsync(slot.Path, "status", "--porcelain=v1", "--ignored");
        var pool = fixture.Pool();

        PoolDone taken;
        using (Hold(slot)) taken = await pool.TakeAsync(fixture.Take(1, B));

        Assert.Equal("busy: obj/ is held by a running process", taken.Refused);
        Assert.Equal(($"daoris/{A}", tip), (await BranchAsync(slot), await HeadAsync(slot)));
        Assert.Equal(before, await GitAsync(slot.Path, "status", "--porcelain=v1", "--ignored"));
        Assert.Equal("generated\n", await File.ReadAllTextAsync(Path.Combine(slot.Path, "gen", "made.txt")));
        Assert.Equal("built\n", await File.ReadAllTextAsync(Path.Combine(slot.Path, "obj", "Engine.dll")));
        Assert.DoesNotContain($"daoris/{B}", await fixture.BranchesAsync());
        Assert.Null(ClaimOn(pool));
        Assert.Equal(PoolLocks.Free, LockOf(slot));
        Assert.Equal((A, KeptStates.Kept), (Row(pool).Line, Row(pool).State));
        var where = WhereA(fixture);
        Assert.Equal((LineStand.Stands, 1), (where.Stand, where.Slot?.Number));
        Assert.False(Directory.Exists(AsideRoot(fixture)) && Directory.EnumerateFileSystemEntries(AsideRoot(fixture)).Any(), "a full move-back leaves no aside folder and no mark");
    }

    [Fact]
    public async Task A_held_output_over_a_finished_lines_detached_slot_puts_it_back_detached_at_its_tip()
    {
        var (fixture, slot, tip) = await LetGoWithOutputsAsync();
        var pool = fixture.Pool();
        var finished = await pool.FinishAsync(new PoolFinish(fixture.Site, "quest-1", A, "main"));
        Assert.True(finished.Ok, finished.Refused ?? finished.Stuck);
        await BuildAsync(slot);
        Assert.Equal(("", tip), (Row(pool).Line, Row(pool).Tip));

        PoolDone taken;
        using (Hold(slot)) taken = await pool.TakeAsync(fixture.Take(1, B));

        Assert.StartsWith("busy: obj/", taken.Refused);
        Assert.Equal(("", tip), (await BranchAsync(slot), await HeadAsync(slot)));
        Assert.Equal(("", tip, KeptStates.Kept), (Row(pool).Line, Row(pool).Tip, Row(pool).State));
        Assert.DoesNotContain($"daoris/{B}", await fixture.BranchesAsync());
        Assert.Null(ClaimOn(pool));
    }

    [Fact]
    public async Task A_return_refused_busy_puts_the_slot_back_on_the_line_it_served_and_keeps_the_returning_lines_branch()
    {
        var (fixture, slot, tipA) = await LetGoWithOutputsAsync();
        var pool = fixture.Pool();
        Assert.True((await pool.TakeAsync(fixture.Take(1, B))).Ok);
        await BuildAsync(slot);
        await pool.EndAsync(new PoolEnd(fixture.Site, 1, "quest-2", "session-2"));
        var tipB = await HeadAsync(slot);

        PoolDone back;
        using (Hold(slot)) back = await pool.ReturnAsync(new PoolTake(fixture.Site, 1, "quest-1", A, tipA, true, "main", null, ""));

        Assert.StartsWith("busy: obj/", back.Refused);
        Assert.Equal(($"daoris/{B}", tipB), (await BranchAsync(slot), await HeadAsync(slot)));
        Assert.Equal((B, KeptStates.Kept), (Row(pool).Line, Row(pool).State));
        Assert.Contains($"daoris/{A}", await fixture.BranchesAsync());
        Assert.Null(ClaimOn(pool));
        Assert.Equal(PoolLocks.Free, LockOf(slot));
    }

    [Fact]
    public async Task The_previous_lines_branch_out_in_another_checkout_keeps_the_claim_waiting_naming_no_line_until_the_runs_own_look_restores_it()
    {
        var (fixture, slot, tip) = await LetGoWithOutputsAsync();
        var other = Path.Combine(Path.GetDirectoryName(fixture.Root)!, "other");
        var pool = fixture.Pool(seams: Seams(reached: (phase, _) => phase == TreeClaimPhases.Switched
            ? GitAsync(fixture.Root, "worktree", "add", "--quiet", other, $"daoris/{A}")
            : Task.CompletedTask));
        await fixture.MadeAsync(fixture.Pool(), 2, "s-cccc0001");
        await fixture.Pool().EndAsync(new PoolEnd(fixture.Site, 2, "quest-3", "session-3"));

        PoolDone taken;
        using (Hold(slot)) taken = await pool.TakeAsync(fixture.Take(1, B));

        Assert.StartsWith("busy: obj/", taken.Refused);
        Assert.Equal(TreeClaimPhases.UndoWaiting, ClaimOn(pool)?.Phase);
        Assert.Equal("", Row(pool).Line);
        Assert.Equal(LineStand.CanBeBroughtBack, WhereA(fixture).Stand);
        Assert.Equal(1, pool.ClaimOf(PoolGitFixture.Workspace, PoolGitFixture.Repository, A)?.Number);
        Assert.Contains("line", fixture.Pool().Claim(new TreeClaimOpening(
            PoolGitFixture.Workspace, PoolGitFixture.Repository, 2, TreeClaimKinds.Return, "quest-1", A, $"daoris/{A}", tip, "", PoolGit.NewJobName())).Refused);
        Assert.NotNull(pool.Visit(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1, SlotVisitKinds.Terminal, "terminal-1"));

        await GitAsync(fixture.Root, "worktree", "remove", "--force", other);
        var looked = await fixture.Pool().RecoverAsync(fixture.Site, 1);

        Assert.True(looked.Ok, looked.Refused ?? looked.Stuck);
        Assert.Equal(($"daoris/{A}", tip), (await BranchAsync(slot), await HeadAsync(slot)));
        Assert.Equal((A, KeptStates.Kept), (Row(pool).Line, Row(pool).State));
        Assert.Equal(LineStand.Stands, WhereA(fixture).Stand);
        Assert.Null(ClaimOn(pool));
        Assert.DoesNotContain($"daoris/{B}", await fixture.BranchesAsync());
    }

    [Fact]
    public async Task The_previous_lines_branch_moved_on_meanwhile_is_not_switched_onto_and_the_slot_is_freed_detached_at_its_tip()
    {
        var (fixture, slot, tip) = await LetGoWithOutputsAsync();
        var pool = fixture.Pool(seams: Seams(reached: async (phase, _) =>
        {
            if (phase != TreeClaimPhases.Switched) return;
            var moved = (await GitAsync(fixture.Root, "commit-tree", $"{tip}^{{tree}}", "-p", tip, "-m", "moved on")).Trim();
            await GitAsync(fixture.Root, "update-ref", $"refs/heads/daoris/{A}", moved);
        }));

        PoolDone taken;
        using (Hold(slot)) taken = await pool.TakeAsync(fixture.Take(1, B));

        Assert.StartsWith("busy: obj/", taken.Refused);
        Assert.Equal(("", tip), (await BranchAsync(slot), await HeadAsync(slot)));
        Assert.Equal(("", tip, KeptStates.Kept), (Row(pool).Line, Row(pool).Tip, Row(pool).State));
        Assert.Equal(LineStand.CanBeBroughtBack, WhereA(fixture).Stand);
        Assert.Null(ClaimOn(pool));
        Assert.DoesNotContain($"daoris/{B}", await fixture.BranchesAsync());
    }

    [Fact]
    public async Task A_move_back_that_fails_leaves_its_tops_in_a_folder_marked_and_held_for_the_previous_line()
    {
        var (fixture, slot, tip) = await LetGoWithOutputsAsync();
        var root = AsideRoot(fixture);
        var pool = fixture.Pool(seams: Seams(renaming: (source, _) =>
            Path.GetFileName(source) == "obj" ? throw new IOException("held")
            : Path.GetFileName(source) == "gen" && source.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? throw new IOException("held on the way back")
            : Task.CompletedTask));

        var taken = await pool.TakeAsync(fixture.Take(1, B));

        Assert.StartsWith("busy: obj/", taken.Refused);
        Assert.Equal(($"daoris/{A}", tip), (await BranchAsync(slot), await HeadAsync(slot)));
        var aside = Directory.EnumerateDirectories(root).Single();
        Assert.True(File.Exists(Path.Combine(aside, "gen", "made.txt")));
        Assert.Equal(A, PoolAsides.LineOf(aside));
        Assert.True(PoolAsides.Holds(aside));
    }

    [Fact]
    public async Task A_file_top_refused_as_unauthorized_is_refused_busy_the_same_way()
    {
        var (fixture, slot, tip) = await LetGoWithOutputsAsync();
        var pool = fixture.Pool(seams: Seams(renaming: (source, _) => Path.GetFileName(source) == "obj" ? throw new UnauthorizedAccessException("read-only") : Task.CompletedTask));

        var taken = await pool.TakeAsync(fixture.Take(1, B));

        Assert.StartsWith("busy: obj/", taken.Refused);
        Assert.Equal(($"daoris/{A}", tip), (await BranchAsync(slot), await HeadAsync(slot)));
        Assert.True(File.Exists(Path.Combine(slot.Path, "gen", "made.txt")));
        Assert.Null(ClaimOn(pool));
    }

    [Theory]
    [InlineData(TreeClaimPhases.Switched, false)]
    [InlineData(TreeClaimPhases.Cleared, true)]
    public async Task A_return_cut_off_is_kept_as_the_returning_lines_only_from_its_clearing_on(string phase, bool kept)
    {
        var (fixture, slot, tipA) = await LetGoWithOutputsAsync();
        Assert.True((await fixture.Pool().TakeAsync(fixture.Take(1, B))).Ok);
        await fixture.Pool().EndAsync(new PoolEnd(fixture.Site, 1, "quest-2", "session-2"));
        var tipB = await HeadAsync(slot);
        var cut = fixture.Pool(seams: Seams(reached: (reached, _) => reached == phase ? throw new Stopped() : Task.CompletedTask));
        await Assert.ThrowsAsync<Stopped>(() => cut.ReturnAsync(new PoolTake(fixture.Site, 1, "quest-1", A, tipA, true, "main", null, "")));

        var looked = await fixture.Pool("run-b", PoolGitFixture.DeadAre("run-a")).RecoverAsync(fixture.Site, 1);

        Assert.True(looked.Ok, looked.Refused ?? looked.Stuck);
        var pool = fixture.Pool("run-b");
        Assert.Equal(kept ? ($"daoris/{A}", tipA, A) : ($"daoris/{B}", tipB, B), (await BranchAsync(slot), await HeadAsync(slot), Row(pool).Line));
        Assert.Contains($"daoris/{A}", await fixture.BranchesAsync());
        Assert.Null(ClaimOn(pool));
    }

    [Theory]
    [InlineData(TreeClaimPhases.Judged)]
    [InlineData(TreeClaimPhases.Checked)]
    public async Task A_return_in_place_cut_off_is_kept_at_any_phase(string phase)
    {
        var (fixture, slot, tipA) = await LetGoWithOutputsAsync();
        var cut = fixture.Pool(seams: Seams(reached: (reached, _) => reached == phase ? throw new Stopped() : Task.CompletedTask));
        await Assert.ThrowsAsync<Stopped>(() => cut.ReturnAsync(new PoolTake(fixture.Site, 1, "quest-1", A, tipA, true, "main", null, "")));

        var looked = await fixture.Pool("run-b", PoolGitFixture.DeadAre("run-a")).RecoverAsync(fixture.Site, 1);

        Assert.True(looked.Ok, looked.Refused ?? looked.Stuck);
        Assert.Equal(($"daoris/{A}", tipA, A), (await BranchAsync(slot), await HeadAsync(slot), Row(fixture.Pool("run-b")).Line));
    }

    [Fact]
    public async Task A_run_cut_off_once_its_undo_put_the_slot_back_is_settled_by_the_takeover_naming_the_line_and_deleting_the_takes_branch()
    {
        var fixture = _fixture = await PoolGitFixture.CreateAsync();
        var slot = await fixture.LetGoAsync(fixture.Pool(), 1, A);
        var tip = await HeadAsync(slot);
        var claimed = fixture.Pool().Claim(new TreeClaimOpening(
            PoolGitFixture.Workspace, PoolGitFixture.Repository, 1, TreeClaimKinds.Take, "quest-2", B, $"daoris/{B}", fixture.Start, tip, PoolGit.NewJobName())
        {
            ExpectBranch = $"daoris/{A}",
            ExpectLine = A,
        });
        fixture.Pool().Undoing(claimed.Fence!);
        await GitAsync(fixture.Root, "branch", $"daoris/{B}", fixture.Start);
        Assert.Equal("", Row(fixture.Pool()).Line);

        var looked = await fixture.Pool("run-b", PoolGitFixture.DeadAre("run-a")).RecoverAsync(fixture.Site, 1);

        Assert.True(looked.Ok, looked.Refused ?? looked.Stuck);
        var pool = fixture.Pool("run-b");
        Assert.Equal((A, KeptStates.Kept), (Row(pool).Line, Row(pool).State));
        Assert.Equal(1, WhereA(fixture).Slot?.Number);
        Assert.DoesNotContain($"daoris/{B}", await fixture.BranchesAsync());
        Assert.Null(ClaimOn(pool));
    }

    [Fact]
    public async Task An_undo_that_throws_leaves_its_claim_waiting_for_its_runs_look_which_puts_the_slot_back()
    {
        var (fixture, slot, tip) = await LetGoWithOutputsAsync();
        var pool = fixture.Pool(seams: Seams(
            reached: (phase, _) => phase == TreeClaimPhases.Undoing ? throw new InvalidOperationException("the step broke") : Task.CompletedTask,
            renaming: (source, _) => Path.GetFileName(source) == "obj" ? throw new IOException("held") : Task.CompletedTask));

        var taken = await pool.TakeAsync(fixture.Take(1, B));

        Assert.Contains("waits", taken.Refused);
        Assert.Equal(TreeClaimPhases.UndoWaiting, ClaimOn(pool)?.Phase);
        Assert.Equal("", Row(pool).Line);

        var looked = await fixture.Pool().RecoverAsync(fixture.Site, 1);

        Assert.True(looked.Ok, looked.Refused ?? looked.Stuck);
        Assert.Equal(($"daoris/{A}", tip, A), (await BranchAsync(slot), await HeadAsync(slot), Row(pool).Line));
        Assert.Null(ClaimOn(pool));
    }

    [Fact]
    public async Task A_slot_bound_to_another_checkout_once_put_back_is_detached_and_settled_stuck_naming_no_line()
    {
        var (fixture, slot, tip) = await LetGoWithOutputsAsync();
        var other = Path.Combine(Path.GetDirectoryName(fixture.Root)!, "elsewhere");
        Directory.CreateDirectory(other);
        var pool = fixture.Pool(seams: Seams(reached: async (phase, _) =>
        {
            if (phase != TreeClaimPhases.Switched) return;
            await GitAsync(fixture.Root, "config", "extensions.worktreeConfig", "true");
            await GitAsync(slot.Path, "config", "--worktree", "core.worktree", other);
        }));

        var taken = await pool.TakeAsync(fixture.Take(1, B));

        Assert.NotNull(taken.Stuck);
        Assert.Equal(("", KeptStates.Stuck), (Row(pool).Line, Row(pool).State));
        Assert.Equal(("", tip), (await BranchAsync(slot), await HeadAsync(slot)));
        Assert.Null(ClaimOn(pool));
    }

    [Fact]
    public async Task A_switch_git_refuses_releases_the_slot_as_it_was_not_stuck()
    {
        var (fixture, slot, tip) = await LetGoWithOutputsAsync();
        var lockFile = Path.Combine(GitBranches.GitDirectory(slot.Path)!, "index.lock");
        var pool = fixture.Pool(seams: Seams(beforeSwitch: _ => File.WriteAllTextAsync(lockFile, "")));

        var taken = await pool.TakeAsync(fixture.Take(1, B));
        File.Delete(lockFile);

        Assert.NotNull(taken.Refused);
        Assert.Null(taken.Stuck);
        Assert.Equal(($"daoris/{A}", tip), (await BranchAsync(slot), await HeadAsync(slot)));
        Assert.Equal((A, KeptStates.Kept), (Row(pool).Line, Row(pool).State));
        Assert.Null(ClaimOn(pool));
        Assert.Equal(PoolLocks.Free, LockOf(slot));
    }
}
