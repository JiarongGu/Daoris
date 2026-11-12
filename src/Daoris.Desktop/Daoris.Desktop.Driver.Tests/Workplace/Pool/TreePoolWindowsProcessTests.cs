using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What Windows does to the pool, over real git and real file handles: a switch a file something holds open refuses puts the slot back
/// or leaves it <c>stuck</c> detached, a retirement's rename refused by a handle is <c>busy</c> and the slot stays, a tracked path of 250
/// characters is switched in, and a repository that tracks a submodule is never pooled.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class TreePoolWindowsProcessTests : IDisposable
{
    private PoolGitFixture? _fixture;

    public void Dispose() => _fixture?.Dispose();

    [Fact]
    public async Task A_switch_refused_by_a_held_file_puts_the_slot_back_or_leaves_it_stuck_naming_no_line_and_the_new_branch_deleted()
    {
        if (!OperatingSystem.IsWindows()) return;
        _fixture = await PoolGitFixture.CreateAsync();
        var pool = _fixture.Pool();
        var slot = await _fixture.LetGoAsync(pool, 1, "s-aaaa0001", "README.md");
        var tip = (await GitAsync(slot.Path, "rev-parse", "HEAD")).Trim();
        using var held = new FileStream(Path.Combine(slot.Path, "README.md"), FileMode.Open, FileAccess.Read, FileShare.None);

        var taken = await pool.TakeAsync(_fixture.Take(1, "s-bbbb0001"));

        var row = pool.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!;
        var standing = ((await GitAsync(slot.Path, "branch", "--show-current")).Trim(), (await GitAsync(slot.Path, "rev-parse", "HEAD")).Trim());
        if (taken.Stuck is null)
        {
            Assert.NotNull(taken.Refused);
            Assert.Equal(("s-aaaa0001", KeptStates.Kept), (row.Line, row.State));
            Assert.Equal(("daoris/s-aaaa0001", tip), standing);
        }
        else
        {
            Assert.Equal(("", KeptStates.Stuck), (row.Line, row.State));
            Assert.Equal("", standing.Item1);
        }

        Assert.Null(pool.ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
        Assert.DoesNotContain("daoris/s-bbbb0001", await _fixture.BranchesAsync());
        Assert.Equal(PoolLocks.Free, Reason(slot.Path));
    }

    [Fact]
    public async Task A_retirement_whose_rename_a_handle_refuses_is_busy_and_the_slot_is_unchanged()
    {
        if (!OperatingSystem.IsWindows()) return;
        _fixture = await PoolGitFixture.CreateAsync();
        var pool = _fixture.Pool();
        var slot = await _fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        using var held = new FileStream(Path.Combine(slot.Path, "README.md"), FileMode.Open, FileAccess.Read, FileShare.None);

        var done = await pool.RetireAsync(_fixture.Site, 1, "quest-1");

        Assert.Contains("busy", done.Refused);
        Assert.Equal("s-aaaa0001", pool.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!.Line);
        Assert.Null(pool.ClaimOn(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1));
        Assert.True(Directory.Exists(slot.Path));
        Assert.Equal(PoolLocks.Free, Reason(slot.Path));
        Assert.Contains(Path.GetFileName(slot.Path), await GitAsync(_fixture.Root, "worktree", "list", "--porcelain"));
    }

    [Fact]
    public async Task A_tracked_path_of_250_characters_is_switched_in()
    {
        if (!OperatingSystem.IsWindows()) return;
        _fixture = await PoolGitFixture.CreateAsync();
        var deep = string.Join('/', Enumerable.Range(0, 5).Select(each => new string((char)('a' + each), 48))) + "/leaf.txt";
        Assert.InRange(deep.Length, 245, 255);
        await GitAsync(_fixture.Root, "config", "core.longpaths", "true");
        var path = Path.GetFullPath(Path.Combine(@"\\?\" + _fixture.Root, deep.Replace('/', '\\')));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "deep\n");
        await GitAsync(_fixture.Root, "add", "-A");
        await GitAsync(_fixture.Root, "commit", "--quiet", "-m", "deep");
        _fixture.Start = (await GitAsync(_fixture.Root, "rev-parse", "HEAD")).Trim();
        var pool = _fixture.Pool();
        var slot = await _fixture.LetGoAsync(pool, 1, "s-aaaa0001");

        var taken = await pool.TakeAsync(_fixture.Take(1, "s-bbbb0001"));

        Assert.True(taken.Ok, taken.Refused ?? taken.Stuck);
        Assert.Contains(deep, await GitAsync(slot.Path, "ls-files"));
    }

    [Fact]
    public async Task A_repository_that_tracks_a_submodule_is_never_pooled()
    {
        if (!OperatingSystem.IsWindows()) return;
        _fixture = await PoolGitFixture.CreateAsync();
        var pool = _fixture.Pool();
        await _fixture.LetGoAsync(pool, 1, "s-aaaa0001");
        await GitAsync(_fixture.Root, "update-index", "--add", "--cacheinfo", $"160000,{_fixture.Start},vendor/lib");
        await GitAsync(_fixture.Root, "commit", "--quiet", "-m", "a submodule");
        var withSubmodule = (await GitAsync(_fixture.Root, "rev-parse", "HEAD")).Trim();

        var taken = await pool.TakeAsync(_fixture.Take(1, "s-bbbb0001", withSubmodule));
        var made = await pool.MakeAsync(_fixture.Make(2, "s-cccc0001"));

        Assert.Contains("submodule", taken.Refused);
        Assert.Contains("submodule", made.Refused);
        Assert.Equal("s-aaaa0001", pool.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 1)!.Line);
        Assert.Null(pool.Slot(PoolGitFixture.Workspace, PoolGitFixture.Repository, 2));
    }

    private static string? Reason(string slot)
    {
        var locked = Path.Combine(GitBranches.GitDirectory(slot)!, "locked");
        return File.Exists(locked) ? File.ReadAllText(locked).Trim() : null;
    }
}
