using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A line's finish over a kept slot, a retirement, the size bound's renewal, a take's set-aside held as its line's own until the line
/// finishes, and the files record's copy hashes and hook origin, over real git. Each case has a scratch home and repository of its own.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreePoolFinishGitTests : IDisposable
{
    private const string Rules = """{ "rules": [ { "kind": "intermediate", "paths": ["obj/"], "toolchain": ["git show HEAD:tc.txt"] } ] }""";

    private PoolGitFixture? _fixture;

    public void Dispose()
    {
        PoolAsides.RecordGoes = _ => true;
        _fixture?.Dispose();
    }

    private async Task<PoolGitFixture> FixtureAsync(string? rules = null) => _fixture = await PoolGitFixture.CreateAsync(rules);

    private static Task WriteAsync(KeptTree slot, string file, string text)
    {
        var path = Path.Combine(slot.Path, file.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.WriteAllTextAsync(path, text);
    }

    private static PoolFinish Finish(PoolGitFixture fixture, string line) => new(fixture.Site, "quest-1", line, "main");

    [Fact]
    public async Task A_finish_does_what_each_paths_finish_says_its_kinds_default_where_the_rule_says_none()
    {
        var fixture = await FixtureAsync("""
            { "form": 2, "rules": [
              { "kind": "intermediate", "paths": ["/obj/"] },
              { "kind": "scratch", "paths": ["/notes.log"], "finish": "keep" },
              { "kind": "intermediate", "paths": ["/build.log"], "finish": "aside" }
            ] }
            """);
        var pool = fixture.Pool();
        var slot = await fixture.MadeAsync(pool, 1, "s-aaaa0001");
        await WriteAsync(slot, "src/Work.cs", "class Work {}\n");
        await GitAsync(slot.Path, "add", "src/Work.cs");
        await GitAsync(slot.Path, "commit", "--quiet", "-m", "work");
        foreach (var file in new[] { "_fixtures/scratch.txt", "obj/Engine.dll", "notes.log", "build.log" }) await WriteAsync(slot, file, "the line's\n");
        Assert.True((await pool.EndAsync(new PoolEnd(fixture.Site, 1, "quest-1", "session-1"))).Ok);

        var done = await pool.FinishAsync(Finish(fixture, "s-aaaa0001"));

        Assert.True(done.Ok, done.Refused ?? done.Stuck);
        Assert.True(File.Exists(Path.Combine(slot.Path, "obj", "Engine.dll")));
        Assert.True(File.Exists(Path.Combine(slot.Path, "notes.log")));
        Assert.False(File.Exists(Path.Combine(slot.Path, "build.log")));
        Assert.False(Directory.Exists(Path.Combine(slot.Path, "_fixtures")));
    }

    [Fact]
    public async Task A_finish_sets_the_lines_own_paths_aside_with_its_record_detaches_the_slot_and_deletes_the_branch_through_the_journal()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await fixture.MadeAsync(pool, 1, "s-aaaa0001");
        await WriteAsync(slot, "src/Work.cs", "class Work {}\n");
        await GitAsync(slot.Path, "add", "src/Work.cs");
        await GitAsync(slot.Path, "commit", "--quiet", "-m", "work");
        await WriteAsync(slot, "_fixtures/scratch.txt", "scratch\n");
        await WriteAsync(slot, "obj/Engine.dll", "built\n");
        var tip = (await GitAsync(slot.Path, "rev-parse", "HEAD")).Trim();
        Assert.True((await pool.EndAsync(new PoolEnd(fixture.Site, 1, "quest-1", "session-1"))).Ok);

        var done = await pool.FinishAsync(Finish(fixture, "s-aaaa0001"));

        Assert.True(done.Ok, done.Refused ?? done.Stuck);
        var row = pool.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!;
        Assert.Equal(("", tip, KeptStates.Kept), (row.Line, row.Tip, row.State));
        Assert.Equal("", (await GitAsync(slot.Path, "branch", "--show-current")).Trim());
        Assert.False(File.Exists(Path.Combine(slot.Path, "_fixtures", "scratch.txt")));
        Assert.True(File.Exists(Path.Combine(slot.Path, "obj", "Engine.dll")));
        Assert.True(File.Exists(Path.Combine(slot.Path, "src", "Work.cs")));
        Assert.DoesNotContain("daoris/s-aaaa0001", await fixture.BranchesAsync());
        Assert.Equal(tip, (await GitAsync(fixture.Root, "rev-parse", "refs/daoris/discarded/daoris/s-aaaa0001")).Trim());
        Assert.Null(pool.ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));

        var posted = new List<FinishedPost>();
        var steps = await new SessionTrees(fixture.Home).RecordSetAsideAsync(
            (_, _) => fixture.Root, (post, _) => { posted.Add(post); return Task.FromResult<RecordRefusal?>(null); }, () => DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.NotEmpty(steps);
        var record = Assert.Single(posted);
        Assert.Equal(Path.Combine(fixture.Site.Trees, "s-aaaa0001"), record.Tree.Tree);
        Assert.Equal("daoris/s-aaaa0001", record.Manifest.Branch);
        Assert.Equal(tip, record.Tree.Tip);
        Assert.Contains("_fixtures", record.Tree.Left.Names);
        Assert.True(File.Exists(Path.Combine(TreePoolFinishes.SetAsideRoot(fixture.Site), record.Folder, "_fixtures", "scratch.txt")));

        var next = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));
        Assert.True(next.Ok, next.Refused ?? next.Stuck);
        Assert.True(File.Exists(Path.Combine(slot.Path, "obj", "Engine.dll")));
        Assert.False(File.Exists(Path.Combine(slot.Path, "_fixtures", "scratch.txt")));
    }

    [Theory]
    [InlineData("no-output")]
    [InlineData("moved")]
    [InlineData("checked-out")]
    [InlineData("unknown")]
    public async Task A_line_that_is_not_finished_keeps_its_branch(string why)
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await fixture.MadeAsync(pool, 1, "s-aaaa0001");
        if (why != "no-output")
        {
            await WriteAsync(slot, "work.txt", "x\n");
            await GitAsync(slot.Path, "add", "work.txt");
            await GitAsync(slot.Path, "commit", "--quiet", "-m", "work");
        }

        if (why != "unknown") await pool.EndAsync(new PoolEnd(fixture.Site, 1, "quest-1", "session-1"));
        if (why == "moved")
        {
            await GitAsync(slot.Path, "commit", "--quiet", "--allow-empty", "-m", "later");
        }

        if (why == "checked-out")
        {
            await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));
            await GitAsync(fixture.Root, "worktree", "add", "--quiet", Path.Combine(Path.GetDirectoryName(fixture.Root)!, "elsewhere"), "daoris/s-aaaa0001");
        }

        var done = await pool.FinishAsync(Finish(fixture, "s-aaaa0001"));

        Assert.NotNull(done.Refused);
        Assert.Contains("daoris/s-aaaa0001", await fixture.BranchesAsync());
        Assert.Null(pool.ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
    }

    [Fact]
    public async Task A_write_landing_as_the_paths_are_set_aside_puts_them_back_and_keeps_the_branch()
    {
        var fixture = await FixtureAsync();
        var slot = await fixture.LetGoAsync(fixture.Pool(), 1, "s-aaaa0001");
        await WriteAsync(slot, "_fixtures/scratch.txt", "scratch\n");
        var pool = fixture.Pool(seams: new PoolSeams
        {
            Reached = (phase, folder) => phase == TreeClaimPhases.SetAside ? File.WriteAllTextAsync(Path.Combine(folder, "late.txt"), "late\n") : Task.CompletedTask,
        });

        var done = await pool.FinishAsync(Finish(fixture, "s-aaaa0001"));

        Assert.Contains("put back", done.Refused);
        Assert.True(File.Exists(Path.Combine(slot.Path, "_fixtures", "scratch.txt")));
        Assert.Contains("daoris/s-aaaa0001", await fixture.BranchesAsync());
        Assert.Equal("s-aaaa0001", pool.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!.Line);
        Assert.Null(pool.ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
        Assert.Empty(Directory.EnumerateFiles(TreePoolFinishes.SetAsideRoot(fixture.Site), "*" + SlimManifest.Suffix));
    }

    [Fact]
    public async Task A_line_finished_after_its_slot_served_another_gets_its_facts_from_its_let_go_and_an_empty_folder()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        var tip = (await GitAsync(slot.Path, "rev-parse", "HEAD")).Trim();
        Assert.True((await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"))).Ok);

        var done = await pool.FinishAsync(Finish(fixture, "s-aaaa0001"));

        Assert.True(done.Ok, done.Refused ?? done.Stuck);
        Assert.DoesNotContain("daoris/s-aaaa0001", await fixture.BranchesAsync());
        Assert.Equal("s-bbbb0001", pool.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!.Line);
        var posted = new List<FinishedPost>();
        await new SessionTrees(fixture.Home).RecordSetAsideAsync(
            (_, _) => fixture.Root, (post, _) => { posted.Add(post); return Task.FromResult<RecordRefusal?>(null); }, () => DateTimeOffset.UtcNow, CancellationToken.None);
        var record = Assert.Single(posted);
        Assert.Equal(tip, record.Tree.Tip);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(TreePoolFinishes.SetAsideRoot(fixture.Site), record.Folder)));
    }

    [Fact]
    public async Task A_take_from_a_line_that_may_come_back_sets_its_outputs_aside_held_as_the_lines_own_until_the_line_finishes()
    {
        var fixture = await FixtureAsync();
        var open = PoolGitFixture.Landing().Open;
        var before = fixture.Pool(seams: new PoolSeams { Open = open, Toolchain = (_, _) => Task.FromResult("tc-a") });
        var slot = await fixture.LetGoAsync(before, 1, "s-aaaa0001");
        await WriteAsync(slot, "obj/Engine.dll", "built\n");
        await before.EndAsync(new PoolEnd(fixture.Site, 1, "quest-1", "session-1"));
        var moving = fixture.Pool(seams: new PoolSeams { Open = open, Toolchain = (_, _) => Task.FromResult("tc-b") });

        var taken = await moving.TakeAsync(fixture.Take(1, "s-bbbb0001"));

        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        Assert.False(Directory.Exists(Path.Combine(slot.Path, "obj")));
        var aside = Directory.EnumerateDirectories(Path.Combine(DaorisHome.Entry(fixture.Home, HomeEntry.Trees), ".tidied", PoolGitFixture.Workspace, PoolGitFixture.Repository)).Single();
        Assert.Equal("s-aaaa0001", PoolAsides.LineOf(aside));
        Assert.Equal(SetAsideClass.Line, SetAsideEligibility.Of(aside, DateTimeOffset.UtcNow.AddDays(-90), TimeSpan.FromDays(1), false).Class);
        PoolAsides.RecordGoes = _ => false;
        Assert.Equal(SetAsideClass.Unmarked, SetAsideEligibility.Of(aside, DateTimeOffset.UtcNow.AddDays(-90), TimeSpan.FromDays(1), false).Class);
        PoolAsides.RecordGoes = _ => true;

        var done = await moving.FinishAsync(Finish(fixture, "s-aaaa0001"));

        Assert.True(done.Ok, done.Refused ?? done.Stuck);
        Assert.False(Directory.Exists(aside));
        Assert.False(File.Exists(PoolAsides.MarkerOf(aside)));
        var finished = Directory.EnumerateDirectories(Path.Combine(DaorisHome.Entry(fixture.Home, HomeEntry.Trees), ".tidied", PoolGitFixture.Workspace, PoolGitFixture.Repository)).Single();
        Assert.True(File.Exists(Path.Combine(finished, "obj", "Engine.dll")));
    }

    [Fact]
    public async Task A_slot_past_the_size_bound_is_renewed_at_its_next_take_and_one_under_it_keeps_its_builds()
    {
        var fixture = await FixtureAsync();
        var slot = await fixture.MadeAsync(fixture.Pool(), 1, "s-aaaa0001");
        await WriteAsync(slot, "obj/Engine.dll", new string('x', 4096));
        var small = fixture.Pool(seams: new PoolSeams { Open = PoolGitFixture.Landing().Open, RenewBytes = 1_000_000 });
        await small.EndAsync(new PoolEnd(fixture.Site, 1, "quest-1", "session-1"));
        Assert.True(small.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!.Bytes >= 4096);
        Assert.True((await small.TakeAsync(fixture.Take(1, "s-bbbb0001"))).Ok);
        Assert.True(File.Exists(Path.Combine(slot.Path, "obj", "Engine.dll")));
        await small.EndAsync(new PoolEnd(fixture.Site, 1, "quest-2", "session-2"));

        var past = fixture.Pool(seams: new PoolSeams { Open = PoolGitFixture.Landing().Open, RenewBytes = 100 });
        await past.EndAsync(new PoolEnd(fixture.Site, 1, "quest-2", "session-2"));
        var renewed = await past.TakeAsync(fixture.Take(1, "s-cccc0001"));

        Assert.True(renewed.Ok, renewed.Refused ?? renewed.Stuck);
        Assert.False(Directory.Exists(Path.Combine(slot.Path, "obj")));
    }

    [Fact]
    public async Task The_toolchain_key_the_rules_read_moves_the_intermediates_aside_at_a_take()
    {
        var fixture = await FixtureAsync(Rules);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "tc.txt"), "one\n");
        await GitAsync(fixture.Root, "add", "tc.txt");
        await GitAsync(fixture.Root, "commit", "--quiet", "-m", "tc");
        fixture.Start = (await GitAsync(fixture.Root, "rev-parse", "HEAD")).Trim();
        var pool = fixture.Pool();
        var slot = await fixture.MadeAsync(pool, 1, "s-aaaa0001");
        await WriteAsync(slot, "tc.txt", "two\n");
        await GitAsync(slot.Path, "add", "tc.txt");
        await GitAsync(slot.Path, "commit", "--quiet", "-m", "tc two");
        await WriteAsync(slot, "obj/Engine.dll", "built\n");
        await pool.EndAsync(new PoolEnd(fixture.Site, 1, "quest-1", "session-1"));
        var key = pool.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!.Toolchain;
        Assert.NotEmpty(key);

        var taken = await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"));

        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        Assert.NotEqual(key, taken.Slot!.Toolchain);
        Assert.False(Directory.Exists(Path.Combine(slot.Path, "obj")));
    }

    [Fact]
    public async Task A_slot_is_retired_whole_and_git_forgets_it_and_a_retired_slot_leaves_the_pool()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await fixture.LetGoAsync(pool, 1, "s-aaaa0001");

        var done = await pool.RetireAsync(fixture.Site, 1, "quest-1");

        Assert.True(done.Ok, done.Refused ?? done.Stuck);
        Assert.Null(pool.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
        Assert.Null(pool.ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
        Assert.False(Directory.Exists(slot.Path));
        Assert.DoesNotContain(Path.GetFileName(slot.Path), await GitAsync(fixture.Root, "worktree", "list", "--porcelain"));
        var aside = Directory.EnumerateDirectories(Path.Combine(DaorisHome.Entry(fixture.Home, HomeEntry.Trees), ".tidied", PoolGitFixture.Workspace, PoolGitFixture.Repository)).Single();
        Assert.True(File.Exists(Path.Combine(aside, "work.txt")));
        Assert.True(File.Exists(SlimManifest.PathFor(aside)));
        Assert.Contains("daoris/s-aaaa0001", await fixture.BranchesAsync());
    }

    [Fact]
    public async Task A_slot_holding_work_is_never_retired()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        await WriteAsync(slot, "loose.txt", "x\n");

        var done = await pool.RetireAsync(fixture.Site, 1, "quest-1");

        Assert.Contains(SlotJudgement.Changed, done.Refused);
        Assert.NotNull(pool.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
        Assert.True(Directory.Exists(slot.Path));
    }

    [Theory]
    [InlineData(TreeClaimPhases.SettingAside)]
    [InlineData(TreeClaimPhases.SetAside)]
    [InlineData(TreeClaimPhases.Detached)]
    public async Task A_finish_stopped_at_each_of_its_phases_is_settled_by_the_look_and_loses_nothing(string phase)
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await fixture.MadeAsync(pool, 1, "s-aaaa0001");
        await WriteAsync(slot, "work.txt", "x\n");
        await GitAsync(slot.Path, "add", "work.txt");
        await GitAsync(slot.Path, "commit", "--quiet", "-m", "work");
        await WriteAsync(slot, "_fixtures/scratch.txt", "scratch\n");
        await pool.EndAsync(new PoolEnd(fixture.Site, 1, "quest-1", "session-1"));
        var stopping = fixture.Pool(seams: new PoolSeams { Reached = (reached, _) => reached == phase ? throw new InvalidOperationException("stopped") : Task.CompletedTask });
        await Assert.ThrowsAsync<InvalidOperationException>(() => stopping.FinishAsync(Finish(fixture, "s-aaaa0001")));

        var recovered = await fixture.Pool("run-b", PoolGitFixture.DeadAre("run-a")).RecoverAsync(fixture.Site, 1);

        Assert.True(recovered.Ok, recovered.Refused ?? recovered.Stuck);
        Assert.Null(pool.ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
        var tip = (await GitAsync(slot.Path, "rev-parse", "HEAD")).Trim();
        if (phase == TreeClaimPhases.SettingAside)
        {
            Assert.True(File.Exists(Path.Combine(slot.Path, "_fixtures", "scratch.txt")), "what moved was put back");
            Assert.Contains("daoris/s-aaaa0001", await fixture.BranchesAsync());
        }
        else
        {
            Assert.False(File.Exists(Path.Combine(slot.Path, "_fixtures", "scratch.txt")));
            Assert.DoesNotContain("daoris/s-aaaa0001", await fixture.BranchesAsync());
            Assert.Equal(tip, (await GitAsync(fixture.Root, "rev-parse", "refs/daoris/discarded/daoris/s-aaaa0001")).Trim());
            Assert.Equal("", (await GitAsync(slot.Path, "branch", "--show-current")).Trim());
        }
    }

    [Fact]
    public async Task A_retirement_stopped_after_its_move_drops_git_s_record_and_the_row()
    {
        var fixture = await FixtureAsync();
        var pool = fixture.Pool();
        var slot = await fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        var stopping = fixture.Pool(seams: new PoolSeams { Reached = (reached, _) => reached == TreeClaimPhases.Moved ? throw new InvalidOperationException("stopped") : Task.CompletedTask });
        await Assert.ThrowsAsync<InvalidOperationException>(() => stopping.RetireAsync(fixture.Site, 1, "quest-1"));

        var recovered = await fixture.Pool("run-b", PoolGitFixture.DeadAre("run-a")).RecoverAsync(fixture.Site, 1);

        Assert.True(recovered.Ok, recovered.Refused ?? recovered.Stuck);
        Assert.Null(pool.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
        Assert.DoesNotContain(Path.GetFileName(slot.Path), await GitAsync(fixture.Root, "worktree", "list", "--porcelain"));
    }

    [Fact]
    public async Task The_hook_is_run_with_the_commit_the_switch_left_and_a_copy_is_Daoris_s_own_only_while_its_hash_holds()
    {
        var fixture = await FixtureAsync();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, ".git", "hooks", "post-checkout"), "#!/bin/sh\nprintf \"$1\" > hook.log\n");
        var pool = fixture.Pool();
        var slot = await fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        var from = (await GitAsync(slot.Path, "rev-parse", "HEAD")).Trim();
        Assert.True((await pool.TakeAsync(fixture.Take(1, "s-bbbb0001"))).Ok);
        Assert.Equal(from, TreeFilesRecord.Read(fixture.Home, slot.Path)!.HookFrom);
        await new TreeFilesStage(fixture.Home, fixture.Root, slot.Path, PoolGitFixture.Repository).RunAsync(CancellationToken.None);
        Assert.Equal(from, await File.ReadAllTextAsync(Path.Combine(slot.Path, "hook.log")));

        var file = Path.Combine(slot.Path, "local.cfg");
        await File.WriteAllTextAsync(file, "copy\n");
        TreeFilesRecord.Change(fixture.Home, slot.Path, state => state with { Copies = new Dictionary<string, string> { ["local.cfg"] = TreeCopies.HashOf(file)! } });
        Assert.True(TreeCopies.IsOwn(TreeFilesRecord.Read(fixture.Home, slot.Path), slot.Path, "local.cfg"));
        await File.WriteAllTextAsync(file, "edited by the line\n");
        Assert.False(TreeCopies.IsOwn(TreeFilesRecord.Read(fixture.Home, slot.Path), slot.Path, "local.cfg"));
    }

    [Fact]
    public void The_one_build_folder_list_keeps_the_removals_thirteen_and_adds_the_web_root()
    {
        string[] thirteen = ["node_modules", "bin", "obj", "dist", "build", "out", ".angular", ".next", "coverage", "target", "__pycache__", ".venv", ".gradle"];
        Assert.True(BuildFolderList.Removal.SetEquals(thirteen));
        Assert.Equal(13, BuildFolderList.Removal.Count);
        Assert.Contains("wwwroot", BuildFolderList.All);
        Assert.DoesNotContain("wwwroot", BuildFolderList.Removal);
    }
}
