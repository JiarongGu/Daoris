using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STRAYOTHERS1 (D88's note) over real git: what a killed opinion's pass or replay left under a repository's trees is moved aside
/// by the next pass of its kind, once no running process holds it and nothing in it was written for ten minutes; one in use, one
/// written in lately and one a person locked stay. A kill is played by naming a process that ran and ended, and ten quiet minutes
/// by setting every time in the folder an hour back.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PassLeftoversProcessTests : TreeSyncFixture
{
    /// <summary>
    /// 🔴 The row's case for an opinion's copy: a copy whose pass a kill cut off, and one made before copies named their process,
    /// both left quiet, are moved aside whole by the next pass's start; a copy a running pass holds and one written in lately stay,
    /// and the next pass's own copy is made beside them.
    /// </summary>
    [Fact]
    public async Task A_killed_pass_s_copy_goes_aside_at_the_next_pass_s_start_and_a_fresh_or_held_one_stays()
    {
        var root = await RepositoryAsync("engine");
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var ended = await EndedProcessAsync();
        var killed = await CopyAsync(root, tip, PassLeftovers.HeldBy(ended), quiet: true);
        var older = await CopyAsync(root, tip, holder: null, quiet: true);
        var fresh = await CopyAsync(root, tip, PassLeftovers.HeldBy(ended), quiet: false);
        var held = await CopyAsync(root, tip, PassLeftovers.HeldBy(Environment.ProcessId), quiet: true);

        var next = await OpinionTree.OpenAsync(Home, root, "engine", "aurora", tip);

        var aside = DaorisHome.Entry(Home, HomeEntry.Trees,".tidied", "aurora", "engine");
        var asides = Directory.GetDirectories(aside);
        Assert.Equal(
            new[] { killed, older }.Select(Path.GetFileName).Order(StringComparer.Ordinal),
            asides.Select(folder => Path.GetFileName(folder)[..Path.GetFileName(killed)!.Length]).Order(StringComparer.Ordinal));
        Assert.All(asides, folder => Assert.True(File.Exists(Path.Combine(folder, "shared.txt")), folder));
        Assert.False(Directory.Exists(killed));
        Assert.False(Directory.Exists(older));
        Assert.All([fresh, held, next.Path], folder => Assert.True(Directory.Exists(folder), folder));
        Assert.Equal(
            PassLeftovers.HeldBy(Environment.ProcessId),
            (await File.ReadAllTextAsync(Path.Combine(next.Path, ".git", PassLeftovers.CopyHolder))).Trim());

        foreach (var copy in new[] { fresh, held, next.Path }) Assert.Null(await OpinionTree.RemoveAsync(Home, copy));
    }

    /// <summary>
    /// 🔴 The row's case for a replay's tree: bringing a branch with no tree up to date replays it in a tree of its own, and before
    /// making it, moves aside what a killed replay left: a detached tree locked by a process that ended, one from before replays
    /// named their process, and a folder git lists no tree in, each left quiet, git letting go of each tree. A tree a running
    /// process holds, one written in lately and one a person locked stay, locked as they were; the replay's own tree is gone after.
    /// </summary>
    [Fact]
    public async Task A_killed_replay_s_tree_goes_aside_at_the_next_replay_s_start_and_a_fresh_held_or_person_s_one_stays()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var session = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(session.Path, "mine.txt", "the session's work\n", "the session's");
        // Its tree removed, its branch kept: bringing it up to date replays it in a tree made for it.
        await GitAsync(root, "worktree", "remove", session.Path);
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        var engine = DaorisHome.Entry(Home, HomeEntry.Trees,"aurora", "engine");
        var ended = await EndedProcessAsync();
        var killed = await ReplayTreeAsync(root, engine, "r-0dead001", PassLeftovers.HeldBy(ended), quiet: true);
        var older = await ReplayTreeAsync(root, engine, "r-0de1d002", reason: null, quiet: true);
        var fresh = await ReplayTreeAsync(root, engine, "r-0f2e5003", PassLeftovers.HeldBy(ended), quiet: false);
        var held = await ReplayTreeAsync(root, engine, "r-011fe004", PassLeftovers.HeldBy(Environment.ProcessId), quiet: true);
        var persons = await ReplayTreeAsync(root, engine, "r-09e45005", "a person's", quiet: true);
        var folder = Path.Combine(engine, "r-0f01d006");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "half.txt"), "what a removal left\n");
        Age(folder);

        var plan = await trees.SyncPlanAsync(Repositories(root), Nobody);
        Assert.Equal(RebaseKind.Replay, plan.Rebases.Single(item => item.Branch == session.Branch).Kind);
        var done = await trees.SyncAsync(Repositories(root), Nobody);

        var replayed = done.Rebases.Single(result => result.Item.Branch == session.Branch);
        Assert.True(replayed.Replayed, replayed.Message);
        var asides = Directory.GetDirectories(DaorisHome.Entry(Home, HomeEntry.Trees,".tidied", "aurora", "engine"));
        Assert.Equal(
            new[] { "r-0dead001", "r-0de1d002", "r-0f01d006" }.Order(StringComparer.Ordinal),
            asides.Select(each => Path.GetFileName(each)[.."r-0dead001".Length]).Order(StringComparer.Ordinal));
        Assert.True(File.Exists(Path.Combine(asides.Single(each => Path.GetFileName(each).StartsWith("r-0dead001-", StringComparison.Ordinal)), "shared.txt")));
        Assert.Equal(
            new[] { fresh, held, persons }.Order(StringComparer.OrdinalIgnoreCase),
            Directory.GetDirectories(engine).Order(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        var worktrees = (await GitAsync(root, "worktree", "list", "--porcelain")).Replace('\\', '/');
        Assert.DoesNotContain("r-0dead001", worktrees, StringComparison.Ordinal);
        Assert.DoesNotContain("r-0de1d002", worktrees, StringComparison.Ordinal);
        Assert.Contains($"locked {PassLeftovers.HeldBy(ended)}", worktrees, StringComparison.Ordinal);
        Assert.Contains($"locked {PassLeftovers.HeldBy(Environment.ProcessId)}", worktrees, StringComparison.Ordinal);
        Assert.Contains("locked a person's", worktrees, StringComparison.Ordinal);
        Assert.Equal(4, worktrees.Split('\n').Count(line => line.StartsWith("worktree ", StringComparison.Ordinal)));
        Assert.False(Directory.Exists(killed));
        Assert.False(Directory.Exists(older));

        foreach (var tree in new[] { fresh, held, persons }) await GitAsync(root, "worktree", "remove", "--force", "--force", tree);
    }

    /// <summary>An opinion's copy as a pass makes one, named for <paramref name="holder"/> (none: one made before copies named theirs).</summary>
    private async Task<string> CopyAsync(string root, string tip, string? holder, bool quiet)
    {
        var copy = await OpinionTree.OpenAsync(Home, root, "engine", "aurora", tip);
        var mark = Path.Combine(copy.Path, ".git", PassLeftovers.CopyHolder);
        if (holder is null) File.Delete(mark);
        else await File.WriteAllTextAsync(mark, holder + "\n");
        if (quiet) Age(copy.Path);
        return copy.Path;
    }

    /// <summary>A replay's tree as a replay makes one, detached at the line, locked for <paramref name="reason"/> (none: unlocked).</summary>
    private static async Task<string> ReplayTreeAsync(string root, string engine, string name, string? reason, bool quiet)
    {
        var tree = Path.Combine(engine, name);
        string[] locked = reason is null ? [] : ["--lock", "--reason", reason];
        await GitAsync(root, ["worktree", "add", "--quiet", "--detach", .. locked, tree, "main"]);
        if (quiet) Age(tree);
        return tree;
    }

    /// <summary>Every time in <paramref name="folder"/>, the folder's own included, set an hour back: ten quiet minutes passed.</summary>
    private static void Age(string folder)
    {
        var then = DateTime.UtcNow.AddHours(-1);
        foreach (var file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
        {
            File.SetCreationTimeUtc(file, then);
            File.SetLastWriteTimeUtc(file, then);
        }

        foreach (var below in Directory.EnumerateDirectories(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }).Append(folder))
        {
            Directory.SetCreationTimeUtc(below, then);
            Directory.SetLastWriteTimeUtc(below, then);
        }
    }

    /// <summary>The id of a process that ran and ended: one a pass's holder may name, that no longer runs.</summary>
    private static async Task<int> EndedProcessAsync()
    {
        using var process = System.Diagnostics.Process.Start(
            TestGit.Past(new System.Diagnostics.ProcessStartInfo("git", "--version") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true }))!;
        await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return process.Id;
    }
}
