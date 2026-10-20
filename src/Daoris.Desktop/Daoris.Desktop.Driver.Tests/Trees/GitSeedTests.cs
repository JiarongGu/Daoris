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
        _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "git-seed-tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose() => GitSeed.Discard(_scratch);

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

    /// <summary>
    /// SCRATCHLEAK1: a worktree's <c>.git</c> is a file, and a walk that looked for a directory only passed it and rooted the
    /// scratch in the main checkout's <c>_fixtures</c>.
    /// </summary>
    [Fact]
    public void The_root_walk_stops_at_a_worktrees_git_file_not_the_checkout_above_it()
    {
        var outer = Path.Combine(_scratch, "checkout");
        var worktree = Path.Combine(outer, ".claude", "worktrees", "one");
        var deep = Path.Combine(worktree, "src", "bin");
        Directory.CreateDirectory(Path.Combine(outer, ".git"));
        Directory.CreateDirectory(deep);
        File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: elsewhere\n");

        Assert.Equal(worktree, GitSeed.RootFrom(deep));
        Assert.Equal(outer, GitSeed.RootFrom(Path.Combine(outer, "src")));
    }

    /// <summary>SCRATCHLEAK1: a folder something holds for a moment is removed once it lets go, not left behind.</summary>
    [Fact]
    public void A_folder_a_file_in_it_holds_for_a_moment_is_removed_once_it_is_let_go()
    {
        var folder = Path.Combine(_scratch, "held");
        Directory.CreateDirectory(folder);
        var held = new FileStream(Path.Combine(folder, "held.txt"), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        _ = Task.Run(async () =>
        {
            await Task.Delay(400);
            held.Dispose();
        });

        GitSeed.Remove(folder);

        Assert.False(Directory.Exists(folder));
    }

    /// <summary>SCRATCHLEAK1: <c>Discard</c> is the cleanup a test's <c>Dispose</c> calls: it gives up quietly on a file that stays held.</summary>
    [Fact]
    public void A_cleanup_that_cannot_finish_gives_up_without_failing_the_test()
    {
        var folder = Path.Combine(_scratch, "stuck");
        Directory.CreateDirectory(folder);
        using var held = new FileStream(Path.Combine(folder, "held.txt"), FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        GitSeed.Discard(folder);

        Assert.True(Directory.Exists(folder));
    }

    /// <summary>
    /// SCRATCHLEAK1: about thirty classes each deleted their scratch once and swallowed the refusal of git's read-only objects, so
    /// each test left a folder (29,000 entries in two folders on 2026-10-20), and nineteen walked to a <c>.git</c> directory only.
    /// A test source that roots scratch under <c>_fixtures</c> removes it through <c>GitSeed</c>, and none writes its own walk.
    /// </summary>
    [Fact]
    public void No_test_deletes_its_fixtures_scratch_by_itself_or_walks_to_the_root_by_itself()
    {
        var plainDelete = new System.Text.RegularExpressions.Regex(@"Directory\.Delete\([^;]*recursive: true");
        var ownWalk = new System.Text.RegularExpressions.Regex(@"Exists\(Path\.Combine\(\w+\.FullName, ""\.git""\)\)");
        var breaches = new List<string>();
        foreach (var path in SourceFiles.All(SourceFiles.DriverTestsProject, "*.cs"))
        {
            var name = Path.GetFileName(path);
            if (name == "GitSeed.cs") continue;
            var text = File.ReadAllText(path);
            if (text.Contains("_fixtures", StringComparison.Ordinal) && plainDelete.IsMatch(text)) breaches.Add($"{name}: deletes scratch with Directory.Delete; use GitSeed.Discard or GitSeed.Remove");
            if (ownWalk.IsMatch(text)) breaches.Add($"{name}: walks to .git by itself; use GitSeed.RepoRoot()");
        }

        Assert.True(breaches.Count == 0, string.Join('\n', breaches));
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

    /// <summary>
    /// OVERVIEWNOISE1a: a host killed at a session's timeout never removes its run's folder, so the next run's start sweeps every
    /// run folder whose host no longer runs; a running host's and a folder not named as a run's stay.
    /// </summary>
    [Fact]
    public void A_killed_runs_seed_folder_is_swept_and_a_running_ones_stays()
    {
        var seeds = Path.Combine(_scratch, "seeds");
        var killed = Path.Combine(seeds, "4242-0123456789a", "1a2b3c4d", ".git", "objects");
        var running = Path.Combine(seeds, "5151-0123456789a");
        var other = Path.Combine(seeds, "kept-by-hand");
        Directory.CreateDirectory(killed);
        File.WriteAllText(Path.Combine(killed, "blob"), "blob");
        File.SetAttributes(Path.Combine(killed, "blob"), FileAttributes.ReadOnly);
        Directory.CreateDirectory(running);
        Directory.CreateDirectory(other);

        GitSeed.Sweep(seeds, id => id == 5151);

        Assert.False(Directory.Exists(Path.Combine(seeds, "4242-0123456789a")));
        Assert.True(Directory.Exists(running));
        Assert.True(Directory.Exists(other));
    }
}
