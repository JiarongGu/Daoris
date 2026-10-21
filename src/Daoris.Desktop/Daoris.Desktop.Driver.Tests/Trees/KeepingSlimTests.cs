using System.Text.Json;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEP1a over real git (D195 point 3; the cleanup design §5 and §9): a tree the look sets aside drops its package folders once it has
/// settled. The judgement before the hold and its check inside it by the last look's own reads, the manifest written before the move,
/// the slim an hour after it in the Keeping pass under the run lock, and the purge at the folder's days that knows what the slim removed.
/// What needs no git (the manifest's states, the purge's read after a slim, the budget and the backoff) is <see cref="SetAsideSlimTests"/>'.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class KeepingSlimTests : BranchTidyingFixture
{
    private string Outside(string name, string file)
    {
        var folder = Path.Combine(_scratch, "elsewhere", name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, file), $"{name}'s own\n");
        return folder;
    }

    /// <summary>
    /// A repository on <c>main</c> ignoring what an install and a build write, with a <c>package.json</c> tracked at its top, under
    /// <c>web/</c> and under <c>linked/</c>; and a session tree landed on it holding every kind §9's proof names: a <c>node_modules</c>
    /// beside each tracked <c>package.json</c> (the top one with a junction inside it to a folder outside), one under <c>tools/</c> with
    /// no tracked <c>package.json</c> above it, one under <c>linked/</c> that is itself a junction, <c>bin/</c>, <c>obj/</c>,
    /// <c>dist/</c>, a <c>.env</c> and <c>local/notes.md</c>.
    /// </summary>
    private async Task<(string Root, TreeOpened Tree, string Library, string Linked)> PackagedTreeAsync()
    {
        var root = await RepositoryAsync("engine");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "*.db\nbuild/\nnode_modules/\nbin/\nobj/\ndist/\n.env\nlocal/\n");
        foreach (var manifest in new[] { "package.json", "web/package.json", "linked/package.json" })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, manifest))!);
            await File.WriteAllTextAsync(Path.Combine(root, manifest), "{ \"name\": \"fixture\" }\n");
        }

        await GitAsync(root, "add", "-A");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--quiet", "-m", "packages");
        var tree = await LandedTreeAsync(new SessionTrees(_home), root);
        foreach (var file in new[]
                 {
                     "node_modules/left-pad/index.js", "web/node_modules/react/index.js", "tools/node_modules/y/index.js", "bin/app.dll",
                     "obj/project.assets.json", "dist/out.js", ".env", "local/notes.md",
                 })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(tree.Path, file))!);
            await File.WriteAllTextAsync(Path.Combine(tree.Path, file), $"{file}\n");
        }

        var library = Outside("library", "index.js");
        await JunctionAsync(Path.Combine(tree.Path, "node_modules", "@fixture", "library"), library);
        var linked = Outside("linked", "index.js");
        await JunctionAsync(Path.Combine(tree.Path, "linked", "node_modules"), linked);
        _ledger.Register("engine", root);
        return (root, tree, library, linked);
    }

    private BranchTidying Tidying(TimeSpan after) => new(_home) { Pace = TimeSpan.Zero, Clock = () => DateTimeOffset.UtcNow + after };

    private string AsideFolder() => Assert.Single(Directory.GetDirectories(DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine")));

    /// <summary>
    /// 🔴 §5 and §9, whole: the look judges the tree's package folders before its hold, checks them inside it, writes the manifest
    /// before the move and marks it <c>moved</c> after. Before the hour nothing goes; a pass meeting the run lock held removes nothing and
    /// names its holder; after the hour exactly the two <c>node_modules</c> beside a tracked <c>package.json</c> go, the junction inside
    /// one taken as a link and its target untouched, and everything else stays, the junction <c>node_modules</c> and its target among
    /// them; the log says <c>tidied.slimmed</c> and <c>keeping.json</c> the run; and at its days the folder is purged, not kept as written.
    /// </summary>
    [Fact]
    public async Task A_tree_set_aside_drops_only_its_package_folders_after_the_hour_and_is_purged_at_its_days()
    {
        var (_, tree, library, linked) = await PackagedTreeAsync();
        using var log = Log();

        await new BranchTidying(_home).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        var aside = AsideFolder();
        var manifest = SlimManifest.Read(SlimManifest.PathFor(aside)).Manifest;
        Assert.NotNull(manifest);
        Assert.Equal((SlimStates.Moved, "engine", tree.Branch), (manifest.State, manifest.Repository, manifest.Branch));
        Assert.Equal(["node_modules", "web/node_modules"], manifest.Paths.Select(path => path.Path));
        Assert.True(Directory.Exists(Path.Combine(aside, "node_modules")));

        Assert.Empty((await Tidying(TimeSpan.Zero).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound))
            .Where(line => line.Contains("package folders", StringComparison.Ordinal)));
        IReadOnlyList<string> held;
        using (var run = KeepingRun.TryTake(_home, KeepingRun.Terminal))
        {
            Assert.NotNull(run);
            held = await Tidying(TimeSpan.FromHours(1)).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);
        }

        Assert.Contains(held, line => line.Contains($"process {Environment.ProcessId}", StringComparison.Ordinal) && line.Contains("terminal", StringComparison.Ordinal));
        Assert.True(Directory.Exists(Path.Combine(aside, "node_modules")));

        var said = await Tidying(TimeSpan.FromHours(1)).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.Contains(said, line => line.StartsWith($"tidy  dropped the package folders of the folder moved aside to {aside}: node_modules, web/node_modules", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(aside, "node_modules")));
        Assert.False(Directory.Exists(Path.Combine(aside, "web", "node_modules")));
        foreach (var kept in new[] { "tools/node_modules/y/index.js", "bin/app.dll", "obj/project.assets.json", "dist/out.js", ".env", "local/notes.md", "package.json", "web/package.json", "README.md" })
        {
            Assert.True(File.Exists(Path.Combine(aside, kept)), kept);
        }

        Assert.True(File.GetAttributes(Path.Combine(aside, "linked", "node_modules")).HasFlag(FileAttributes.ReparsePoint));
        Assert.Equal("library's own\n", await File.ReadAllTextAsync(Path.Combine(library, "index.js")));
        Assert.Equal("linked's own\n", await File.ReadAllTextAsync(Path.Combine(linked, "index.js")));
        var slimmed = Assert.Single(Events("tidied.slimmed"));
        Assert.Equal((2, 0), (slimmed.GetProperty("removed").GetInt32(), slimmed.GetProperty("kept").GetInt32()));
        Assert.True(slimmed.GetProperty("bytes").GetInt64() > 0);
        Assert.Equal(Path.GetRelativePath(_home, aside).Replace('\\', '/'), slimmed.GetProperty("folder").GetString());
        using (var record = JsonDocument.Parse(await File.ReadAllTextAsync(KeepingRecord.PathOf(_home))))
        {
            var last = record.RootElement.GetProperty("last").GetProperty("set-aside");
            Assert.Equal(("look", 2), (last.GetProperty("door").GetString(), last.GetProperty("removed").GetProperty("count").GetInt32()));
        }

        Assert.Equal(["node_modules", "web/node_modules"], Assert.Single(await new SessionTrees(_home).ListAsync()).Slimmed);

        var purged = await Tidying(TimeSpan.FromDays(15)).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        Assert.Contains(purged, line => line.StartsWith($"tidy  deleted the folder moved aside to {aside} on ", StringComparison.Ordinal));
        Assert.False(Directory.Exists(aside));
        Assert.False(File.Exists(SlimManifest.PathFor(aside)));
        Assert.Empty(Events("tidied.kept"));
        Assert.Equal("library's own\n", await File.ReadAllTextAsync(Path.Combine(library, "index.js")));
        Assert.Equal("linked's own\n", await File.ReadAllTextAsync(Path.Combine(linked, "index.js")));
    }

    /// <summary>
    /// 🔴 §5 (Fable F1.5): the judgement is made before the hold and checked inside it by the last look's own reads: a
    /// <c>package.json</c> no longer in the index by then leaves its <c>node_modules</c> out of what the move may take, and the walk
    /// finds the <c>node_modules</c> git judged as a folder to be a link, which stays too.
    /// </summary>
    [Fact]
    public async Task A_package_json_untracked_after_the_judgement_leaves_its_node_modules_out()
    {
        var (root, tree, _, _) = await PackagedTreeAsync();
        var trees = new SessionTrees(_home);

        var judged = await TreeSlims.JudgeAsync(tree.Path, _closing.Token);
        await GitAsync(tree.Path, "rm", "--cached", "--quiet", "web/package.json");
        var confirmed = new List<string>();
        var kept = await trees.LastLookAsync(root, new SweepItem("engine", "default", tree.Branch, tree.Path, SweepKind.Landed, 1, "main", null),
            _closing.Token, judged, confirmed);

        // git reads the junction `linked/node_modules` as a folder it ignores whole: the last look's walk is what finds it a link.
        Assert.Equal(["linked/node_modules", "node_modules", "web/node_modules"], judged);
        Assert.Null(kept);
        Assert.Equal(["node_modules"], confirmed);
    }

    /// <summary>
    /// 🔴 §5 (Codex 2.4): where git will not let go of the tree's record, the folder is put back and the manifest written before the
    /// move goes with the attempt, so nothing is ever slimmed of a tree git still knows.
    /// </summary>
    [Fact]
    public async Task A_folder_git_would_not_let_go_of_is_put_back_with_no_manifest()
    {
        var (_, tree, _, _) = await PackagedTreeAsync();
        WorkingTree.Answered.Value = arguments =>
            arguments is ["worktree", "remove", ..] ? (128, "", "fatal: refused by the fixture\n") : ((int, string, string)?)null;
        IReadOnlyList<string> said;
        try
        {
            said = await new BranchTidying(_home).LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }

        Assert.Contains(said, line => line.Contains("its folder was put back", StringComparison.Ordinal));
        Assert.True(Directory.Exists(Path.Combine(tree.Path, "node_modules")));
        var aside = DaorisHome.Entry(_home, HomeEntry.Trees, ".tidied", "default", "engine");
        Assert.Empty(Directory.Exists(aside) ? Directory.GetFileSystemEntries(aside) : []);
    }

    /// <summary>
    /// 🔴 §5 step 3 (Codex 2.5): <c>DeleteNeverFollowing</c> checks its own argument: handed a junction it refuses, and what the
    /// junction points at is untouched.
    /// </summary>
    [Fact]
    public async Task Deleting_never_following_refuses_a_junction_handed_to_it()
    {
        var target = Outside("target", "keep.txt");
        var link = Path.Combine(_scratch, "a-link");
        await JunctionAsync(link, target);

        Assert.Throws<IOException>(() => TreeSetAside.DeleteNeverFollowing(link));

        Assert.Equal("target's own\n", await File.ReadAllTextAsync(Path.Combine(target, "keep.txt")));
        Assert.True(Directory.Exists(link));
    }

    /// <summary>§3.1, §3.3 (Fable F3.3): the run lock's holder file a process left that has ended reads as a run a crash cut short.</summary>
    [Fact]
    public async Task A_holder_file_an_ended_process_left_reads_as_a_run_cut_short()
    {
        var ended = await EndedProcessAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(KeepingRun.HolderPathOf(_home))!);
        await File.WriteAllTextAsync(KeepingRun.HolderPathOf(_home), $"{{\"pid\":{ended},\"since\":\"2026-10-21T08:00:00.000Z\",\"door\":\"terminal\"}}\n");

        var holder = KeepingRun.Holder(_home);

        Assert.NotNull(holder);
        Assert.Equal((ended, KeepingRun.Terminal, true), (holder.Pid, holder.Door, holder.Gone));
    }
}
