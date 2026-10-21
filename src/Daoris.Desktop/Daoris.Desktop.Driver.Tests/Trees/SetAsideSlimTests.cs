using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEP1a's slim after the move and the purge after the slim (D195 point 3; the cleanup design §5, steps 1 to 4): over folders already
/// set aside with a manifest beside each, as the look leaves them, so no git and no process is needed. What only real git can show (the
/// judgement, the move, the manifest written before it, a junction) is <see cref="KeepingSlimTests"/>'.
/// </summary>
public sealed class SetAsideSlimTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "slim", Guid.NewGuid().ToString("N")[..8]);
    private readonly SessionTrees _trees;

    public SetAsideSlimTests()
    {
        Directory.CreateDirectory(_home);
        _trees = new SessionTrees(_home);
    }

    public void Dispose()
    {
        try { TreeSetAside.DeleteNeverFollowing(_home); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(1);

    /// <summary>
    /// A folder as the look sets it aside: <paramref name="files"/> made under it, every entry dated an hour back, then renamed into
    /// <c>.tidied/default/engine/&lt;name&gt;-&lt;moment&gt;</c> at this moment, which a rename leaves its entries' times as they were.
    /// </summary>
    private string Aside(string name, params string[] files)
    {
        var staging = Path.Combine(_home, "staging", name);
        foreach (var file in files)
        {
            var at = Path.Combine(staging, file);
            Directory.CreateDirectory(Path.GetDirectoryName(at)!);
            File.WriteAllText(at, $"{file}\n");
        }

        var old = DateTime.UtcNow.AddHours(-1);
        foreach (var entry in new DirectoryInfo(staging).EnumerateFileSystemInfos("*", SearchOption.AllDirectories).Append(new DirectoryInfo(staging)))
        {
            entry.CreationTimeUtc = old;
            entry.LastWriteTimeUtc = old;
        }

        Thread.Sleep(20);
        var moved = DateTimeOffset.UtcNow;
        var folder = Path.Combine(_trees.SetAsideRoot, "default", "engine",
            $"{name}-{moved.ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture)}");
        Directory.CreateDirectory(Path.GetDirectoryName(folder)!);
        // Tried again while the virus scanner holds a file just written in it, as the install's own folder moves are (FIX-LOG 2026-10-07).
        AtomicFile.MoveFolder(staging, folder);
        return folder;
    }

    private static DateTimeOffset MovedOf(string folder) => TreeSetAside.MovedAt(Path.GetFileName(folder))!.Value;

    private static void Manifest(string folder, string state, params string[] paths) =>
        Manifest(folder, state, [.. paths.Select(path => new SlimPath(path, SlimStates.Pending))]);

    private static void Manifest(string folder, string state, IReadOnlyList<SlimPath> paths) =>
        new SlimManifest(state, MovedOf(folder).AddSeconds(-1), state == SlimStates.Moved ? MovedOf(folder) : null, "engine", "daoris/s-1", paths)
            .Write(SlimManifest.PathFor(folder));

    private static SlimManifest Read(string folder) => SlimManifest.Read(SlimManifest.PathFor(folder)).Manifest!;

    private SetAsideRun Pass(string folder, TimeSpan after, KeepPolicy? policy = null, TimeSpan? budget = null) =>
        _trees.KeepSetAside(policy ?? KeepPolicy.Default, MovedOf(folder) + after, budget ?? Budget, CancellationToken.None);

    private static readonly string[] Package =
    [
        "package.json", "node_modules/left-pad/index.js", "web/package.json", "web/node_modules/react/index.js", "bin/app.dll",
        "obj/project.assets.json", "dist/out.js", ".env", "local/notes.md", "README.md",
    ];

    /// <summary>
    /// 🔴 §5: nothing goes before the hour; after it exactly the manifest's package folders go, each written <c>removed</c> with when its
    /// removal ended, and everything else stays for the folder's days: the session's files, <c>bin/</c>, <c>obj/</c>, a <c>.env</c>.
    /// </summary>
    [Fact]
    public void Nothing_goes_before_the_hour_and_only_the_manifest_s_package_folders_after_it()
    {
        var folder = Aside("s-0a", Package);
        Manifest(folder, SlimStates.Moved, "node_modules", "web/node_modules");

        var early = Pass(folder, TimeSpan.FromMinutes(59));

        Assert.Empty(early.Slims);
        Assert.True(Directory.Exists(Path.Combine(folder, "node_modules")));

        var later = Pass(folder, TimeSpan.FromHours(1));

        var slim = Assert.Single(later.Slims);
        Assert.Equal(["node_modules", "web/node_modules"], slim.Removed);
        Assert.True(slim.Ended);
        Assert.True(slim.Bytes > 0);
        Assert.False(Directory.Exists(Path.Combine(folder, "node_modules")));
        Assert.False(Directory.Exists(Path.Combine(folder, "web", "node_modules")));
        Assert.All(Package.Where(file => !file.Contains("node_modules", StringComparison.Ordinal)),
            file => Assert.True(File.Exists(Path.Combine(folder, file)), file));
        Assert.All(Read(folder).Paths, path => Assert.Equal((SlimStates.Removed, true), (path.State, path.Ended is not null)));
        Assert.Empty(Pass(folder, TimeSpan.FromHours(2)).Slims);
    }

    /// <summary>
    /// 🔴 §5 step 4 (the steward's S1): removing a folder changes the time of the folder above it, which the purge would read as a write
    /// after the move and keep for ever. It reads that folder against when the removal ended, so the slimmed folder goes at its days.
    /// </summary>
    [Fact]
    public void A_slimmed_folder_is_purged_at_its_days_not_kept_as_written()
    {
        var folder = Aside("s-0b", Package);
        Manifest(folder, SlimStates.Moved, "node_modules", "web/node_modules");
        Assert.Single(Pass(folder, TimeSpan.FromHours(1)).Slims);
        // The removal changed `web`'s own time; NTFS shows that in its parent's listing only later (measured), so it is made so here.
        var ended = Read(folder).Paths.Single(path => path.Path == "web/node_modules").Ended!.Value;
        Directory.SetLastWriteTimeUtc(Path.Combine(folder, "web"), ended.UtcDateTime.AddMilliseconds(-1));
        Assert.True(Directory.GetLastWriteTimeUtc(Path.Combine(folder, "web")) > MovedOf(folder).UtcDateTime);

        var purged = Assert.Single(Pass(folder, TimeSpan.FromDays(15)).Purged);

        Assert.True(purged.Purged, purged.Kept);
        Assert.False(Directory.Exists(folder));
        Assert.False(File.Exists(SlimManifest.PathFor(folder)));
    }

    /// <summary>
    /// 🔴 §5 step 4 (Fable F1.1): a copy keeps its source's write time and takes a new creation time, and a move changes the time of the
    /// folder it lands in: a file copied, and one moved, into the folder above a slimmed path after its removal ended each keep the
    /// folder at the purge, as a write.
    /// </summary>
    [Fact]
    public void A_file_copied_or_moved_into_the_folder_above_after_the_removal_keeps_it()
    {
        var copied = Aside("s-0c", Package);
        var moved = Aside("s-0d", Package);
        Manifest(copied, SlimStates.Moved, "web/node_modules");
        Manifest(moved, SlimStates.Moved, "web/node_modules");
        Assert.Equal(2, Pass(moved, TimeSpan.FromHours(1)).Slims.Count(slim => slim.Removed.Count == 1));
        var source = Path.Combine(_home, "outside.txt");
        File.WriteAllText(source, "the person's\n");
        File.SetCreationTimeUtc(source, DateTime.UtcNow.AddDays(-30));
        File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddDays(-30));
        Thread.Sleep(20);
        File.Copy(source, Path.Combine(copied, "web", "copied.txt"));
        File.Move(source, Path.Combine(moved, "web", "moved.txt"));

        var run = Pass(copied, TimeSpan.FromDays(15));

        Assert.All(run.Purged, folder => Assert.False(folder.Purged, folder.Folder));
        Assert.Equal(2, run.Purged.Count);
        Assert.All(run.Purged, folder => Assert.Equal(SetAsideKept.Written, folder.Code));
        Assert.True(File.Exists(Path.Combine(copied, "web", "copied.txt")));
        Assert.True(File.Exists(Path.Combine(moved, "web", "moved.txt")));
    }

    /// <summary>§5 step 4 (Fable F1.1), with no manifest at all: a file copied in after the move keeps the folder by its creation time.</summary>
    [Fact]
    public void A_file_copied_into_a_folder_set_aside_keeps_it_by_its_creation_time()
    {
        var folder = Aside("s-0e", "README.md");
        var source = Path.Combine(_home, "old.txt");
        File.WriteAllText(source, "old\n");
        File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddDays(-30));
        Thread.Sleep(20);
        File.Copy(source, Path.Combine(folder, "old.txt"));

        var purged = Assert.Single(Pass(folder, TimeSpan.FromDays(15)).Purged);

        Assert.Equal((false, SetAsideKept.Written), (purged.Purged, purged.Code));
    }

    /// <summary>
    /// 🔴 §5 step 1: a write in the folder after the move, by an install or a program still running from it, keeps it whole for its
    /// days: nothing is removed, and each path is written <c>kept</c>, <c>written</c>.
    /// </summary>
    [Fact]
    public void A_write_after_the_move_keeps_the_folder_whole_its_paths_kept_written()
    {
        var folder = Aside("s-0f", Package);
        Manifest(folder, SlimStates.Moved, "node_modules", "web/node_modules");
        File.WriteAllText(Path.Combine(folder, "late.db"), "a write after the move\n");

        var slim = Assert.Single(Pass(folder, TimeSpan.FromHours(1)).Slims);

        Assert.Empty(slim.Removed);
        Assert.True(slim.Ended);
        Assert.True(Directory.Exists(Path.Combine(folder, "node_modules")));
        Assert.All(Read(folder).Paths, path => Assert.Equal((SlimStates.Kept, SlimKept.Written), (path.State, path.Why)));
        Assert.Empty(Pass(folder, TimeSpan.FromHours(2)).Slims);
    }

    /// <summary>
    /// 🔴 §5 (Codex 2.4): a manifest left at <c>judged</c>, where the process stopped between the move and that write, is unknown: its
    /// folder is kept whole for its days and the manifest dropped by the next pass. One whose folder is gone is dropped too.
    /// </summary>
    [Fact]
    public void A_manifest_left_at_judged_keeps_its_folder_whole_and_is_dropped()
    {
        var folder = Aside("s-10", Package);
        Manifest(folder, SlimStates.Judged, "node_modules");
        var gone = Aside("s-11", Package);
        Manifest(gone, SlimStates.Moved, "node_modules");
        TreeSetAside.DeleteNeverFollowing(gone);

        var run = Pass(folder, TimeSpan.FromHours(1));

        Assert.Empty(run.Slims);
        Assert.True(Directory.Exists(Path.Combine(folder, "node_modules")));
        Assert.False(File.Exists(SlimManifest.PathFor(folder)));
        Assert.False(File.Exists(SlimManifest.PathFor(gone)));
    }

    /// <summary>§5 (D195 point 3): with the person's <c>whole</c>, read at the slim, every folder stays whole and its manifest waits.</summary>
    [Fact]
    public void The_person_s_whole_read_at_the_slim_keeps_every_folder_whole()
    {
        var folder = Aside("s-12", Package);
        Manifest(folder, SlimStates.Moved, "node_modules");

        var run = Pass(folder, TimeSpan.FromHours(1), new KeepPolicy(new SetAsidePolicy(TimeSpan.FromDays(14), Whole: true)));

        Assert.Empty(run.Slims);
        Assert.True(Directory.Exists(Path.Combine(folder, "node_modules")));
        Assert.Equal(SlimStates.Pending, Assert.Single(Read(folder).Paths).State);
    }

    /// <summary>
    /// §5 *What is not slimmed*: a folder set aside with no manifest, as a stray's, a cut-off opening's, an opinion's copy and a
    /// replay's tree are, and the folders set aside before KEEP1a, never loses a path before its days.
    /// </summary>
    [Fact]
    public void A_folder_with_no_manifest_is_never_slimmed()
    {
        var folder = Aside("stray", Package);

        Assert.Empty(Pass(folder, TimeSpan.FromDays(2)).Slims);
        Assert.True(Directory.Exists(Path.Combine(folder, "node_modules")));
    }

    /// <summary>
    /// 🔴 §5 step 2 (Codex 2.5): each path is checked again right before it goes: one with <c>..</c>, one rooted, one not a package
    /// folder, and one with a parent's name, are each refused and written <c>kept</c>; nothing outside the folder is touched.
    /// </summary>
    [Fact]
    public void A_manifest_path_outside_the_folder_rooted_or_no_package_folder_is_refused()
    {
        var folder = Aside("s-13", Package);
        var sibling = Aside("s-14", Package);
        var rooted = Path.Combine(sibling, "node_modules").Replace('\\', '/');
        Manifest(folder, SlimStates.Moved,
            $"../{Path.GetFileName(sibling)}/node_modules", rooted, "bin", "web/node_modules/..", "node_modules");

        var slim = Assert.Single(Pass(folder, TimeSpan.FromHours(1)).Slims);

        Assert.Equal(["node_modules"], slim.Removed);
        Assert.True(Directory.Exists(Path.Combine(sibling, "node_modules")));
        Assert.True(Directory.Exists(Path.Combine(folder, "bin")));
        Assert.True(Directory.Exists(Path.Combine(folder, "web")));
        Assert.Equal([SlimKept.Refused, SlimKept.Refused, SlimKept.Refused, SlimKept.Refused],
            Read(folder).Paths.Where(path => path.State == SlimStates.Kept).Select(path => path.Why));
        Assert.Null(TreeSlims.Refusal("web/node_modules"));
        Assert.All(new[] { "", "/node_modules", "C:/node_modules", "a\\node_modules", "a//node_modules", "./node_modules", "web/bin" },
            path => Assert.NotNull(TreeSlims.Refusal(path)));
    }

    /// <summary>
    /// 🔴 §5 step 3: a slim stopped midway leaves its path <c>removing</c>; the next pass takes it up from step 1, and neither it nor the
    /// purge after it reads what the removal had changed as a write.
    /// </summary>
    [Fact]
    public void A_slim_stopped_midway_is_taken_up_by_the_next_pass_and_never_read_as_a_write()
    {
        var folder = Aside("s-15", [.. Package, "web/node_modules/react/lib/a.js", "web/node_modules/vue/index.js"]);
        Manifest(folder, SlimStates.Moved, [new SlimPath("web/node_modules", SlimStates.Removing)]);
        TreeSetAside.DeleteNeverFollowing(Path.Combine(folder, "web", "node_modules", "react"));

        var slim = Assert.Single(Pass(folder, TimeSpan.FromHours(1)).Slims);

        Assert.Equal(["web/node_modules"], slim.Removed);
        Assert.False(Directory.Exists(Path.Combine(folder, "web", "node_modules")));
        Assert.True(Assert.Single(Pass(folder, TimeSpan.FromDays(15)).Purged).Purged);
    }

    /// <summary>
    /// §3.1 (Codex 3.2): a pass removes within its budget, hearing a stop between paths and never inside one: none left, it removes
    /// nothing and the next pass takes up what waits.
    /// </summary>
    [Fact]
    public void A_pass_past_its_budget_removes_nothing_more_and_the_next_takes_it_up()
    {
        var folder = Aside("s-16", Package);
        Manifest(folder, SlimStates.Moved, "node_modules");

        var spent = Pass(folder, TimeSpan.FromHours(1), budget: TimeSpan.Zero);

        Assert.True(spent.Stopped);
        Assert.Empty(spent.Slims);
        Assert.True(Directory.Exists(Path.Combine(folder, "node_modules")));
        Assert.Equal(["node_modules"], Assert.Single(Pass(folder, TimeSpan.FromHours(1)).Slims).Removed);
    }

    /// <summary>
    /// §3.1: a path whose removal failed for the same reason as at its last try is tried again the next day, not each pass: here a file
    /// in it held open, on Windows, where that refuses a delete.
    /// </summary>
    [Fact]
    public void A_removal_that_fails_the_same_way_twice_waits_a_day()
    {
        if (!OperatingSystem.IsWindows()) return;
        var folder = Aside("s-17", Package);
        Manifest(folder, SlimStates.Moved, "node_modules");

        using (new FileStream(Path.Combine(folder, "node_modules", "left-pad", "index.js"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Empty(Assert.Single(Pass(folder, TimeSpan.FromHours(1)).Slims).Removed);
            var first = Assert.Single(Read(folder).Paths);
            Assert.Equal((SlimStates.Removing, (DateTimeOffset?)null), (first.State, first.Tried));
            Assert.NotNull(first.Why);

            Pass(folder, TimeSpan.FromHours(2));
            Assert.Equal(MovedOf(folder) + TimeSpan.FromHours(2), Assert.Single(Read(folder).Paths).Tried);

            Pass(folder, TimeSpan.FromHours(3));
            Assert.Equal(MovedOf(folder) + TimeSpan.FromHours(2), Assert.Single(Read(folder).Paths).Tried);
        }

        Assert.Empty(Pass(folder, TimeSpan.FromHours(4)).Slims);
        Assert.Equal(["node_modules"], Assert.Single(Pass(folder, TimeSpan.FromHours(27)).Slims).Removed);
    }

    /// <summary>§5: the folder's line in the trees' list, which the work inventory and <c>work state</c> read, names what was dropped.</summary>
    [Fact]
    public async Task The_list_names_the_package_folders_a_folder_dropped()
    {
        var folder = Aside("s-18", Package);
        Manifest(folder, SlimStates.Moved, "node_modules", "web/node_modules");
        Pass(folder, TimeSpan.FromHours(1));

        var listed = Assert.Single(await _trees.ListAsync());

        Assert.Equal(["node_modules", "web/node_modules"], listed.Slimmed);
    }
}
