using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The dependency store over real git and real processes: a repository whose own rule names a dependency `deps/`, installed by a tracked script
/// whose behaviour its tracked `lock.txt` chooses, so each case is a commit; and npm's own `npm ci` over a `file:` package, read from the shipped
/// signature. Fixtures live under the repository's gitignored `_fixtures/`, never OS temp.
/// </summary><remarks>Alone: it plants variables in the process's environment, which every class beside it would read.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Collection(Alone.Name)]
public sealed class DependencyStoreTests : IDisposable
{
    private const string Script = """
        const fs = require('fs'), path = require('path');
        const how = fs.readFileSync('lock.txt', 'utf8').trim();
        fs.mkdirSync('deps/pkg', { recursive: true });
        fs.writeFileSync('deps/pkg/index.js', 'module.exports = ' + JSON.stringify(how) + ';\n');
        fs.writeFileSync('deps/env.txt', (process.env.DEPS_STRAY || 'absent') + ' ' + (process.env.DEPS_NAMED || 'absent'));
        fs.writeFileSync('deps/.state', 'last');
        if (how === 'beside') fs.mkdirSync('out', { recursive: true }), fs.writeFileSync('out/made.txt', 'x');
        if (how === 'names') fs.writeFileSync('deps/where.json', JSON.stringify({ at: process.cwd() }));
        if (how === 'link-out') fs.symlinkSync(path.resolve('..'), 'deps/up', 'junction');
        if (how === 'link-in') fs.symlinkSync(path.resolve('src'), 'deps/src', 'junction');
        if (how === 'tracked') fs.writeFileSync('README.md', 'changed by an install\n');
        if (how === 'hard') { fs.mkdirSync('../hard-store', { recursive: true }); fs.writeFileSync('../hard-store/a.txt', 'shared'); fs.linkSync('../hard-store/a.txt', 'deps/hard.txt'); }
        if (how === 'fail') { console.log('the install could not reach its registry'); process.exit(3); }
        """;

    private const string Rules = """
        { "rules": [ { "kind": "dependency", "paths": ["deps/"], "install": "node install.cjs", "key": ["lock.txt"], "env": ["DEPS_NAMED"], "caches": [".cache/"] }, { "kind": "output", "paths": ["built/"] } ] }
        """;

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "deps-store", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "home");
    private string Root => Path.Combine(_scratch, "engine");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DEPS_STRAY", null);
        Environment.SetEnvironmentVariable("DEPS_NAMED", null);
        DependencyCommands.Installing.Value = null;
        GitSeed.Discard(_scratch);
    }

    /// <summary>The repository, a copy of the run's seed of this `how`: its commit is made once and never read for its id.</summary>
    private Task MakeAsync(string how) => GitSeed.MadeAsync(Root, $"deps-store:{how}", async folder =>
    {
        Directory.CreateDirectory(Path.Combine(folder, "src"));
        await GitAsync(folder, "init", "-q", "-b", "main");
        await GitAsync(folder, "config", "user.email", "test@example.com");
        await GitAsync(folder, "config", "user.name", "Test");
        // A file written back is compared byte for byte: git checks it out as committed.
        await GitAsync(folder, "config", "core.autocrlf", "false");
        await File.WriteAllTextAsync(Path.Combine(folder, ".gitignore"), "deps/\nout/\nbuilt/\nstray/\n");
        // CommonJS by its name: the fixture sits under this repository, whose package says its scripts are modules.
        await File.WriteAllTextAsync(Path.Combine(folder, "install.cjs"), Script);
        await File.WriteAllTextAsync(Path.Combine(folder, "daoris.files.json"), Rules);
        await File.WriteAllTextAsync(Path.Combine(folder, "README.md"), "# engine\n");
        await File.WriteAllTextAsync(Path.Combine(folder, "src", "main.js"), "1\n");
        await File.WriteAllTextAsync(Path.Combine(folder, "lock.txt"), how + "\n");
        await GitAsync(folder, "add", ".");
        await GitAsync(folder, "commit", "-q", "-m", "first");
    });

    /// <summary>A fresh tree of the repository, marked unworked as an opening marks it.</summary>
    private async Task<string> TreeAsync(string name, bool mark = true)
    {
        var tree = Path.Combine(_scratch, name);
        await GitAsync(Root, "worktree", "add", "-q", "-b", $"daoris/{name}", tree, "main");
        if (mark) TreeFilesRecord.Mark(Home, tree);
        return tree;
    }

    private Task<TreeFilesOutcome> StageAsync(string tree) => new TreeFilesStage(Home, Root, tree, "engine").RunAsync(CancellationToken.None);

    private static IReadOnlyList<string?> Codes(TreeFilesOutcome outcome) => [.. outcome.Said.SelectMany(each => each.Parts).Select(part => part.Code)];

    private async Task<string> KeyAsync(string tree, FileRule rule, IReadOnlyList<string>? before = null, string repository = "engine", string path = "deps/") =>
        await DependencyKeys.KeyAsync(Home, tree, (await WorkingTree.HeadAsync(tree))!, repository, new StoreDependency(path, rule), before ?? [], CancellationToken.None);

    [Fact]
    public async Task Each_key_input_moves_the_key_and_an_absent_key_file_is_keyed_as_absent()
    {
        await MakeAsync("plain");
        var tree = await TreeAsync("t1");
        var rule = new FileRule(FileKind.Dependency, ["deps/"]) { Install = "node install.js", Key = ["lock.txt", ".npmrc"], Env = ["DEPS_NAMED"], Toolchain = ["node -e \"console.log(process.env.DEPS_NAMED || '')\""] };
        var first = await KeyAsync(tree, rule);

        Assert.Equal(first, await KeyAsync(tree, rule));
        Assert.NotEqual(first, await KeyAsync(tree, rule with { Install = "node install.js --again" }));
        Assert.NotEqual(first, await KeyAsync(tree, rule, before: ["an-earlier-key"]));
        Assert.NotEqual(first, await KeyAsync(tree, rule, repository: "other"));
        Assert.NotEqual(first, await KeyAsync(tree, rule, path: "web/deps/"));
        Environment.SetEnvironmentVariable("DEPS_NAMED", "one");
        var named = await KeyAsync(tree, rule);
        Assert.NotEqual(first, named);
        Assert.NotEqual(named, await KeyAsync(tree, rule with { Toolchain = [] }));
        // A variable no `env` names is not in the key.
        Environment.SetEnvironmentVariable("DEPS_STRAY", "stray");
        Assert.Equal(named, await KeyAsync(tree, rule));

        // A project `.npmrc` added at the commit, keyed as absent before.
        await File.WriteAllTextAsync(Path.Combine(tree, ".npmrc"), "omit=dev\n");
        await GitAsync(tree, "add", ".npmrc");
        await GitAsync(tree, "commit", "-q", "-m", "an npmrc");
        Assert.NotEqual(named, await KeyAsync(tree, rule));
        Assert.Contains("key\t.npmrc\tabsent", await DependencyKeys.KeyedAsync(Root, "main", [".npmrc"], CancellationToken.None));
    }

    [Fact]
    public async Task A_fresh_tree_s_install_is_sealed_and_the_next_tree_is_placed_by_copy_writable_and_link_safe()
    {
        await MakeAsync("link-in");
        Environment.SetEnvironmentVariable("DEPS_STRAY", "stray");
        Environment.SetEnvironmentVariable("DEPS_NAMED", "named");
        var one = await TreeAsync("t1");

        var installed = await StageAsync(one);

        Assert.True(Codes(installed).Contains(StartNoteCodes.StartedDepsInstalled.Code), string.Join(" | ", installed.Said.Select(each => each.Note)));
        Assert.Equal("absent named", await File.ReadAllTextAsync(Path.Combine(one, "deps", "env.txt")));
        var entry = Assert.Single(DependencyStore.Entries(Home));
        Assert.True(File.GetAttributes(Path.Combine(DependencyStore.EntryOf(Home, entry.Key), "files", "pkg", "index.js")).HasFlag(FileAttributes.ReadOnly));
        Assert.Contains("do not run their install", installed.Told);

        var two = await TreeAsync("t2");
        var placed = await StageAsync(two);

        Assert.Contains(StartNoteCodes.StartedDepsPlaced.Code, Codes(placed));
        var index = Path.Combine(two, "deps", "pkg", "index.js");
        Assert.False(File.GetAttributes(index).HasFlag(FileAttributes.ReadOnly), "a placement is writable");
        await File.WriteAllTextAsync(index, "changed in the tree\n");
        Assert.Equal("module.exports = \"link-in\";\n", await File.ReadAllTextAsync(Path.Combine(one, "deps", "pkg", "index.js")));
        var link = new DirectoryInfo(Path.Combine(two, "deps", "src"));
        Assert.True(link.Attributes.HasFlag(FileAttributes.ReparsePoint));
        Assert.Equal(Path.Combine(two, "src"), DependencyLinks.Leads(link));
        var (clean, _) = await WorkingTree.CleanAsync(two);
        Assert.True(clean);

        // The placement's record holds; a changed file fails it, and a worked tree's next stage leaves it as the line's own.
        TreeFilesStep.Hand(Home, two);
        var resumed = await StageAsync(two);
        Assert.Contains(StartNoteCodes.StartedDepsLeft.Code, Codes(resumed));
        Assert.Equal("changed in the tree\n", await File.ReadAllTextAsync(index));
    }

    [Theory]
    [InlineData("beside", "its install wrote `out/` outside its folder")]
    [InlineData("names", "it names the folder it was installed in")]
    [InlineData("link-out", "its link `up` leads outside the tree")]
    public async Task Nothing_is_sealed_from_an_install_that_writes_beside_its_folder_names_its_tree_or_links_outside(string how, string why)
    {
        await MakeAsync(how);
        var tree = await TreeAsync("t1");

        var outcome = await StageAsync(tree);

        var line = Assert.Single(outcome.Said, each => each.Parts.Any(part => part.Code == StartNoteCodes.StartedDepsUnstored.Code));
        Assert.Contains(why, line.Note);
        Assert.Empty(DependencyStore.Entries(Home));
        Assert.True(Directory.Exists(Path.Combine(tree, "deps", "pkg")), "the tree keeps its own install");
    }

    [Theory]
    [InlineData("built/", true)]
    [InlineData("stray/", false)]
    public async Task A_fill_seals_in_a_tree_holding_a_declared_build_output_and_not_one_holding_an_ignored_path_no_rule_declares(string held, bool sealedHere)
    {
        await MakeAsync("plain");
        var tree = await TreeAsync("t1");
        Directory.CreateDirectory(Path.Combine(tree, held));
        await File.WriteAllTextAsync(Path.Combine(tree, held, "made.bin"), "a build's own");

        var outcome = await StageAsync(tree);

        Assert.Equal(sealedHere, Codes(outcome).Contains(StartNoteCodes.StartedDepsInstalled.Code));
        Assert.Equal(sealedHere ? 1 : 0, DependencyStore.Entries(Home).Count);
        if (!sealedHere) Assert.Equal(DepsWhy.HeldBefore, Assert.Single(outcome.Said, each => each.Parts[0].Code == StartNoteCodes.StartedDepsUnstored.Code).Parts[0].Values.Single(value => value.Key == "why").Value);
    }

    [Fact]
    public async Task A_seal_the_disk_refuses_is_said_as_not_stored_and_the_record_stays_installed()
    {
        await MakeAsync("link-in");
        var tree = await TreeAsync("t1");
        DependencyStore.Copying.Value = _ => throw new IOException("the disk is full");
        TreeFilesOutcome outcome;
        try
        {
            outcome = await StageAsync(tree);
        }
        finally
        {
            DependencyStore.Copying.Value = null;
        }

        Assert.DoesNotContain(StartNoteCodes.StartedDepsInstalled.Code, Codes(outcome));
        var line = Assert.Single(outcome.Said, each => each.Parts.Any(part => part.Code == StartNoteCodes.StartedDepsUnstored.Code));
        Assert.Equal(DepsWhy.Unsealed, line.Parts[0].Values.Single(value => value.Key == "why").Value);
        Assert.Equal("installed", TreeFilesRecord.Read(Home, tree)!.Of("deps/")!.How);
        Assert.Empty(DependencyStore.Entries(Home));
    }

    [Fact]
    public async Task A_failed_install_leaves_the_folder_absent_and_a_tracked_change_is_written_back()
    {
        await MakeAsync("fail");
        var tree = await TreeAsync("t1");
        var failed = await StageAsync(tree);
        Assert.Contains("failed: the install could not reach its registry", Assert.Single(failed.Said, each => each.Parts.Any(part => part.Code == StartNoteCodes.StartedDepsUnplaced.Code)).Note);
        Assert.False(Directory.Exists(Path.Combine(tree, "deps")));
        Assert.Null(TreeFilesRecord.Read(Home, tree)!.Of("deps/"));

        await File.WriteAllTextAsync(Path.Combine(tree, "lock.txt"), "tracked\n");
        await GitAsync(tree, "commit", "-q", "-am", "an install that writes a tracked file");
        TreeFilesRecord.Mark(Home, tree);
        var tracked = await StageAsync(tree);
        Assert.Contains("`README.md`", Assert.Single(tracked.Said, each => each.Parts.Any(part => part.Code == StartNoteCodes.StartedDepsUnplaced.Code)).Note);
        Assert.Equal("# engine\n", await File.ReadAllTextAsync(Path.Combine(tree, "README.md")));
        Assert.False(Directory.Exists(Path.Combine(tree, "deps")));
    }

    [Fact]
    public async Task A_hard_linked_install_is_sealed_by_copy_its_store_s_file_untouched()
    {
        await MakeAsync("hard");
        var tree = await TreeAsync("t1");

        await StageAsync(tree);

        var entry = Assert.Single(DependencyStore.Entries(Home));
        var shared = Path.Combine(_scratch, "hard-store", "a.txt");
        Assert.False(File.GetAttributes(shared).HasFlag(FileAttributes.ReadOnly), "the read-only mark is the entry's copy's alone");
        await File.WriteAllTextAsync(Path.Combine(tree, "deps", "hard.txt"), "written through the tree's link");
        Assert.Equal("shared", await File.ReadAllTextAsync(Path.Combine(DependencyStore.EntryOf(Home, entry.Key), "files", "hard.txt")));
    }

    [Fact]
    public async Task A_worked_tree_installs_nothing_and_a_tree_with_no_record_counts_as_worked()
    {
        await MakeAsync("plain");
        var unrecorded = await TreeAsync("t1", mark: false);

        var left = await StageAsync(unrecorded);

        Assert.Contains(StartNoteCodes.StartedDepsLeft.Code, Codes(left));
        Assert.False(Directory.Exists(Path.Combine(unrecorded, "deps")));
        Assert.Empty(DependencyStore.Entries(Home));

        // With a hit, a worked tree's absent folder is placed, nothing run.
        var fresh = await TreeAsync("t2");
        await StageAsync(fresh);
        var placed = await StageAsync(unrecorded);
        Assert.Contains(StartNoteCodes.StartedDepsPlaced.Code, Codes(placed));
    }

    [Fact]
    public async Task A_visit_taking_the_mark_during_an_install_stops_the_stage_and_leaves_the_folder_as_the_line_s_own()
    {
        await MakeAsync("plain");
        var tree = await TreeAsync("t1");
        DependencyCommands.Installing.Value = _ => Assert.True(TreeFilesRecord.Unmark(Home, tree));

        var stopped = await StageAsync(tree);

        Assert.True(stopped.Stopped);
        Assert.Contains(StartNoteCodes.StartedFilesStopped.Code, Codes(stopped));
        Assert.True(Directory.Exists(Path.Combine(tree, "deps")), "the install's folder is left as it stands");
        var record = TreeFilesRecord.Read(Home, tree)!;
        Assert.False(record.Unworked);
        Assert.Null(record.Of("deps/"));
        Assert.Empty(DependencyStore.Entries(Home));
    }

    [Fact]
    public async Task A_cut_off_install_is_removed_and_installed_again_at_the_carry_on()
    {
        await MakeAsync("plain");
        var tree = await TreeAsync("t1");
        Directory.CreateDirectory(Path.Combine(tree, "deps", "half"));
        TreeFilesRecord.WhileUnworked(Home, tree, record => record.With(new TreePlacement("deps/", "cut", "installed", null) { Installing = true }));

        var again = await StageAsync(tree);

        Assert.Contains(StartNoteCodes.StartedDepsInstalled.Code, Codes(again));
        Assert.False(Directory.Exists(Path.Combine(tree, "deps", "half")));
        Assert.False(TreeFilesRecord.Read(Home, tree)!.Of("deps/")!.Installing);
    }

    [Fact]
    public async Task A_real_npm_ci_by_the_shipped_signature_is_sealed_and_placed_into_the_next_tree()
    {
        Directory.CreateDirectory(Path.Combine(Root, "pkgs", "a"));
        await GitAsync(Root, "init", "-q", "-b", "main");
        await GitAsync(Root, "config", "user.email", "test@example.com");
        await GitAsync(Root, "config", "user.name", "Test");
        await File.WriteAllTextAsync(Path.Combine(Root, ".gitignore"), "node_modules/\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "pkgs", "a", "package.json"), """{ "name": "a", "version": "1.0.0", "main": "index.js" }""");
        await File.WriteAllTextAsync(Path.Combine(Root, "pkgs", "a", "index.js"), "module.exports = 'a';\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "package.json"), """{ "name": "engine", "version": "1.0.0", "dependencies": { "a": "file:pkgs/a" } }""");
        Assert.Equal(0, (await DependencyCommands.RunAsync(Home, Root, "npm install --package-lock-only --no-audit --no-fund", Root, InstallEnvironment.Of(new FileRule(FileKind.Dependency, ["node_modules/"])), DependencyCommands.InstallBound, CancellationToken.None)).Code);
        await GitAsync(Root, "add", ".");
        await GitAsync(Root, "commit", "-q", "-m", "an npm project");
        var one = await TreeAsync("t1");

        var installed = await StageAsync(one);

        Assert.True(Codes(installed).Contains(StartNoteCodes.StartedDepsInstalled.Code), string.Join(" ", installed.Said.Select(each => each.Note)));
        var two = await TreeAsync("t2");
        var placed = await StageAsync(two);
        Assert.Contains(StartNoteCodes.StartedDepsPlaced.Code, Codes(placed));
        var link = new DirectoryInfo(Path.Combine(two, "node_modules", "a"));
        Assert.Equal(Path.Combine(two, "pkgs", "a"), DependencyLinks.Leads(link));
        Assert.True(File.Exists(Path.Combine(two, "node_modules", ".package-lock.json")));
    }
}

/// <summary>The store's pieces with no process: the placement record's read, and eviction.</summary>
public sealed class DependencyStorePiecesTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "deps-pieces", Guid.NewGuid().ToString("N")[..8]);
    private string Tree => Path.Combine(_scratch, "tree");
    private string Home => Path.Combine(_scratch, "home");

    public void Dispose() => TreeSetAside.DeleteNeverFollowing(_scratch);

    private void Write(string relative, string text)
    {
        var path = Path.Combine(Tree, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public void A_file_changed_removed_or_added_fails_the_record_except_under_a_cache()
    {
        Write("deps/a/index.js", "a");
        Write("deps/b.js", "b");
        IReadOnlyList<string> caches = [".cache/"];
        var record = DependencyManifest.Read(Tree, "deps", caches, out var outside);
        Assert.Null(outside);
        Assert.Null(record.Differs(Tree, "deps", caches));

        Write("deps/.cache/made.bin", "a tool's own");
        Assert.Null(record.Differs(Tree, "deps", caches));
        Write("deps/c.js", "added");
        Assert.Equal("`c.js` was added", record.Differs(Tree, "deps", caches));
        File.Delete(Path.Combine(Tree, "deps", "c.js"));
        File.WriteAllText(Path.Combine(Tree, "deps", "b.js"), "B");
        Assert.Equal("`b.js` was changed", record.Differs(Tree, "deps", caches));
        File.Delete(Path.Combine(Tree, "deps", "b.js"));
        Assert.Equal("a file it placed was removed", record.Differs(Tree, "deps", caches));
    }

    [Fact]
    public void Eviction_goes_by_days_since_last_placed_and_then_by_size()
    {
        Write("deps/a.bin", new string('a', 1000));
        var manifest = DependencyManifest.Read(Tree, "deps", [], out _);
        foreach (var key in new[] { "old", "mid", "new" }) Assert.True(DependencyStore.Seal(Home, key, Tree, "deps", manifest, CancellationToken.None));
        var now = DateTimeOffset.UtcNow;
        File.SetLastWriteTimeUtc(Path.Combine(DependencyStore.EntryOf(Home, "old"), "placed"), now.AddDays(-40).UtcDateTime);
        File.SetLastWriteTimeUtc(Path.Combine(DependencyStore.EntryOf(Home, "mid"), "placed"), now.AddDays(-2).UtcDateTime);

        Assert.Equal(["old"], DependencyStore.Evict(Home, TimeSpan.FromDays(30), long.MaxValue, now));
        Assert.Equal(["mid"], DependencyStore.Evict(Home, TimeSpan.FromDays(30), 1500, now));
        Assert.Equal(["new"], DependencyStore.Entries(Home).Select(each => each.Key));
    }

    /// <summary>The placement's last copy is about to run, so the files before it stand in the making folder: one is held open as a scanner holds it.</summary>
    private FileStream HoldMaking(string from)
    {
        if (!from.EndsWith("top.json", StringComparison.Ordinal)) return null!;
        var making = Assert.Single(Directory.EnumerateDirectories(Tree, DependencyStore.MakingPrefix + "*"));
        return new FileStream(Path.Combine(making, "sub", "a.bin"), FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    [Fact]
    public void A_placement_whose_folder_a_program_holds_open_for_a_moment_is_renamed_once_it_lets_go()
    {
        Write("deps/sub/a.bin", "a");
        Write("deps/top.json", "{}");
        var manifest = DependencyManifest.Read(Tree, "deps", [], out _);
        Assert.True(DependencyStore.Seal(Home, "key", Tree, "deps", manifest, CancellationToken.None));
        FileStream? held = null;
        var waits = 0;
        DependencyStore.Copying.Value = from => held ??= HoldMaking(from);
        FolderRename.Waiting.Value = _ =>
        {
            waits++;
            held?.Dispose();
        };
        try
        {
            Assert.NotNull(DependencyStore.Place(Home, "key", Tree, "placed", CancellationToken.None));
        }
        finally
        {
            DependencyStore.Copying.Value = null;
            FolderRename.Waiting.Value = null;
            held?.Dispose();
        }

        Assert.Equal(1, waits);
        Assert.True(File.Exists(Path.Combine(Tree, "placed", "sub", "a.bin")));
        Assert.Equal(["deps", "placed"], Directory.EnumerateDirectories(Tree).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void A_placement_whose_folder_stays_held_says_once_what_held_it_and_what_it_could_not_do()
    {
        Write("deps/sub/a.bin", "a");
        Write("deps/top.json", "{}");
        var manifest = DependencyManifest.Read(Tree, "deps", [], out _);
        Assert.True(DependencyStore.Seal(Home, "key", Tree, "deps", manifest, CancellationToken.None));
        FileStream? held = null;
        var waits = 0;
        DependencyStore.Copying.Value = from => held ??= HoldMaking(from);
        FolderRename.Waiting.Value = _ => waits++;
        try
        {
            var failure = Assert.Throws<IOException>(() => DependencyStore.Place(Home, "key", Tree, "placed", CancellationToken.None));
            Assert.Contains("could not be renamed to `placed`", failure.Message);
            Assert.Contains("kept a file in it open", failure.Message);
        }
        finally
        {
            DependencyStore.Copying.Value = null;
            FolderRename.Waiting.Value = null;
            held?.Dispose();
        }

        Assert.Equal(FolderRename.Tries - 1, waits);
        Assert.False(Directory.Exists(Path.Combine(Tree, "placed")));
    }

    [Fact]
    public void A_seal_that_cannot_copy_a_file_says_so_and_leaves_no_half_folder_in_the_store()
    {
        Write("deps/a.bin", "a");
        var manifest = DependencyManifest.Read(Tree, "deps", [], out _);
        File.Delete(Path.Combine(Tree, "deps", "a.bin"));

        Assert.False(DependencyStore.Seal(Home, "key", Tree, "deps", manifest, CancellationToken.None));
        Assert.Empty(Directory.EnumerateFileSystemEntries(DependencyStore.RootOf(Home)));
    }

    [Fact]
    public void A_placement_that_cannot_copy_a_file_throws_the_io_failure_the_stage_says_and_leaves_no_half_folder()
    {
        Write("deps/sub/a.bin", "a");
        var manifest = DependencyManifest.Read(Tree, "deps", [], out _);
        Assert.True(DependencyStore.Seal(Home, "key", Tree, "deps", manifest, CancellationToken.None));
        DependencyStore.Copying.Value = _ => throw new IOException("the disk is full");
        try
        {
            Assert.Throws<IOException>(() => DependencyStore.Place(Home, "key", Tree, "placed", CancellationToken.None));
        }
        finally
        {
            DependencyStore.Copying.Value = null;
        }

        Assert.Equal(["deps"], Directory.EnumerateDirectories(Tree).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void The_daily_eviction_sweeps_the_folders_a_cut_off_seal_or_a_failed_eviction_left_once_a_day_old()
    {
        Write("deps/a.bin", "a");
        var manifest = DependencyManifest.Read(Tree, "deps", [], out _);
        Assert.True(DependencyStore.Seal(Home, "kept", Tree, "deps", manifest, CancellationToken.None));
        var root = DependencyStore.RootOf(Home);
        var now = DateTimeOffset.UtcNow;
        var stale = new[] { DependencyStore.MakingPrefix + "cut", "half" };
        foreach (var name in stale.Append(DependencyStore.MakingPrefix + "now"))
        {
            Directory.CreateDirectory(Path.Combine(root, name, "files"));
            File.WriteAllText(Path.Combine(root, name, "files", "x.bin"), "x");
        }

        foreach (var name in stale) Directory.SetLastWriteTimeUtc(Path.Combine(root, name), now.AddDays(-2).UtcDateTime);

        Assert.Empty(DependencyStore.Evict(Home, TimeSpan.FromDays(30), long.MaxValue, now));
        Assert.Equal([DependencyStore.MakingPrefix + "now", "kept"], Directory.EnumerateDirectories(root).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_daily_eviction_takes_an_entry_past_the_kind_s_thirty_days_and_waits_a_day_before_the_next()
    {
        Write("deps/a.bin", new string('a', 1000));
        var manifest = DependencyManifest.Read(Tree, "deps", [], out _);
        foreach (var key in new[] { "old", "new" }) Assert.True(DependencyStore.Seal(Home, key, Tree, "deps", manifest, CancellationToken.None));
        var now = DateTimeOffset.UtcNow;
        File.SetLastWriteTimeUtc(Path.Combine(DependencyStore.EntryOf(Home, "old"), "placed"), now.AddDays(-31).UtcDateTime);
        var at = now;
        var keeping = new DepsKeeping(() => at);

        Assert.True(keeping.Due(Home));
        Assert.Equal((1, 1000L), keeping.Keep(Home, KeepPolicy.Default));
        Assert.Equal(["new"], DependencyStore.Entries(Home).Select(each => each.Key));
        Assert.False(keeping.Due(Home));
        at = now.AddDays(1);
        Assert.True(keeping.Due(Home));
    }
}
