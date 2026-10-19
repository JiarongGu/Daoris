namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a copied seed keeps and mends (PROC1h): a copy is its own folder, its origin names its own bare repository, and no
/// linked-worktree metadata comes with it. No git starts here; the seeds' use is held by the Git classes that copy them.
/// </summary>
public sealed class GitSeedTests : IDisposable
{
    private readonly string _scratch;

    public GitSeedTests()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git"))) directory = directory.Parent;
        _scratch = Path.Combine(directory?.FullName ?? AppContext.BaseDirectory, "_fixtures", "git-seed-tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    [Fact]
    public void A_copy_is_its_own_folder_and_carries_no_worktree_metadata()
    {
        var seed = Path.Combine(_scratch, "seed");
        Directory.CreateDirectory(Path.Combine(seed, ".git", "hooks"));
        Directory.CreateDirectory(Path.Combine(seed, ".git", "worktrees", "one"));
        File.WriteAllText(Path.Combine(seed, ".git", "worktrees", "one", "gitdir"), "elsewhere");
        File.WriteAllText(Path.Combine(seed, ".git", "hooks", "pre-push"), "hook");
        File.WriteAllText(Path.Combine(seed, "README.md"), "seed\n");

        var copy = Path.Combine(_scratch, "copy");
        GitSeed.Copy(seed, copy);
        File.WriteAllText(Path.Combine(copy, "README.md"), "changed\n");

        Assert.Equal("seed\n", File.ReadAllText(Path.Combine(seed, "README.md")));
        Assert.Equal("hook", File.ReadAllText(Path.Combine(copy, ".git", "hooks", "pre-push")));
        Assert.False(Directory.Exists(Path.Combine(copy, ".git", "worktrees")));
    }

    [Fact]
    public void The_origin_url_is_written_as_the_copys_own_path_and_nothing_else_changes()
    {
        var root = Path.Combine(_scratch, "repository");
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        var config = Path.Combine(root, ".git", "config");
        File.WriteAllText(config, "[core]\n\tbare = false\n[remote \"origin\"]\n\turl = C:\\\\seed\\\\engine-origin.git\n\tfetch = +refs/heads/*:refs/remotes/origin/*\n[branch \"main\"]\n\tremote = origin\n");

        GitSeed.RepairOrigin(root, @"C:\scratch\engine-origin.git");

        Assert.Equal(
            "[core]\n\tbare = false\n[remote \"origin\"]\n\turl = C:\\\\scratch\\\\engine-origin.git\n\tfetch = +refs/heads/*:refs/remotes/origin/*\n[branch \"main\"]\n\tremote = origin\n",
            File.ReadAllText(config));
    }

    /// <summary>
    /// PROCSPEED1: git writes its objects read-only, a copy keeps that, and <c>Directory.Delete</c> refuses a read-only file on
    /// Windows; the fixtures' cleanup retried that refusal for five seconds after every test and left the folder behind.
    /// </summary>
    [Fact]
    public void A_copy_whose_objects_git_wrote_read_only_is_removed_whole()
    {
        var seed = Path.Combine(_scratch, "seed");
        var objects = Path.Combine(seed, ".git", "objects", "5b");
        Directory.CreateDirectory(objects);
        File.WriteAllText(Path.Combine(objects, "0ea958a50c4c247410bc2aaf79a29ae46d0fbf"), "blob");
        File.SetAttributes(Path.Combine(objects, "0ea958a50c4c247410bc2aaf79a29ae46d0fbf"), FileAttributes.ReadOnly);
        File.WriteAllText(Path.Combine(seed, "README.md"), "seed\n");
        var copy = Path.Combine(_scratch, "copy");
        GitSeed.Copy(seed, copy);
        Assert.True(File.GetAttributes(Path.Combine(copy, ".git", "objects", "5b", "0ea958a50c4c247410bc2aaf79a29ae46d0fbf")).HasFlag(FileAttributes.ReadOnly));

        GitSeed.Remove(copy);
        GitSeed.Remove(seed);

        Assert.False(Directory.Exists(copy));
        Assert.False(Directory.Exists(seed));
    }

    [Fact]
    public async Task A_shape_is_built_once_however_many_ask_at_once()
    {
        var builds = 0;
        var shape = $"test:{Guid.NewGuid():N}";
        var seeds = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => GitSeed.SeedAsync(shape, async folder =>
        {
            Interlocked.Increment(ref builds);
            await Task.Delay(20);
            Directory.CreateDirectory(folder);
        })));

        Assert.Equal(1, builds);
        Assert.Single(seeds.Distinct());
    }
}
