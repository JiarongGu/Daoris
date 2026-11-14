using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Session trees: the worktree half of "the tree is the unit of exclusion, and a
/// repository may have more than one". These tests drive REAL git — worktree semantics are git's, and
/// a mock agreeing with a guess about them is the mock-vs-mock trap the modules tests already paid
/// for once.
/// </summary>
/// <remarks>
/// Fixtures live under the repository's gitignored `_fixtures/` — the family's own scratch rule —
/// except the path-length test's, which needs a root shorter than the checkout's. Each test
/// makes its own repository, so nothing here is order-dependent, and it starts git alone (a git hook the
/// cut-off open waits in included): it runs four at once with the git-only classes.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class SessionTreesTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;

    public SessionTreesTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "session-trees", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose() => Remove(_scratch);

    private static void Remove(string folder)
    {
        try
        {
            // Never following a link, and read-only git objects made writable.
            if (Directory.Exists(folder)) TreeSetAside.DeleteNeverFollowing(folder);
        }
        catch (IOException)
        {
            // A straggling git handle on Windows; the next run's fresh GUID never collides with it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: read-only pack files under .git occasionally refuse the first delete.
        }
    }

    /// <summary>…and commits the canonical line has not taken refuse the same way, naming them.</summary>
    [Fact]
    public async Task A_tree_with_unmerged_commits_refuses_and_names_them()
    {
        var root = await MakeRepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(opened.Path, "landed.txt"), "session work\n");
        await GitAsync(opened.Path, "add", ".");
        await GitAsync(opened.Path, "commit", "-m", "the session's landing");

        var removal = await trees.RemoveAsync(opened.Path);

        Assert.False(removal.Removed);
        Assert.Contains("the session's landing", removal.Message);
        Assert.True(Directory.Exists(opened.Path));

        // The person saying it again, meaning it: force removes the tree AND its branch, work and all.
        var forced = await trees.RemoveAsync(opened.Path, force: true);
        Assert.True(forced.Removed);
        Assert.False(Directory.Exists(opened.Path));
    }

    /// <summary>
    /// 🔴 Windows' 260-character path limit: a
    /// repository's deepest tracked file fitted under its own root and not under a session tree, whose
    /// prefix is longer. `worktree add` failed on every tick, and the quest sat.
    /// </summary>
    /// <remarks>
    /// In a git worktree the repository sits deeper than in the main checkout, so the test makes its
    /// repository under a short root of its own and sizes the path from that root, and proves the same
    /// thing wherever the repository sits.
    /// </remarks>
    [Fact]
    public async Task A_tracked_path_that_fits_the_root_but_not_a_trees_longer_prefix_still_opens()
    {
        // OS temp, as many of this assembly's fixtures are: `_fixtures/` sits as deep as the checkout.
        var shortRoot = Path.Combine(TestHomes.Root, "daoris-lp-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var home = Path.Combine(shortRoot, "daoris-home");
            Directory.CreateDirectory(home);
            var root = await MakeRepositoryAsync("engine", under: shortRoot);
            // At most 240 characters under the root, inside Windows' 248 for a folder and 260 for a file; the tree's prefix is 36
            // longer (`daoris-home/trees/aurora/` and `/s-xxxxxxxx`), so the same path crosses 260 under it.
            const string segment = "component/";
            var room = 240 - (root.Length + 1) - "x.ts".Length;
            Assert.True(room >= 5 * segment.Length, $"the short root is too long to fit a deep path under: {root}");
            var deep = string.Concat(Enumerable.Repeat(segment, room / segment.Length)) + "x.ts";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, deep))!);
            await File.WriteAllTextAsync(Path.Combine(root, deep), "x\n");
            var added = await RunAsync(root, "add", ".");
            Assert.True(added.ExitCode == 0, $"the fixture must fit under its own root: {added.Stderr}");
            await GitAsync(root, "commit", "-m", "a deep file");

            var opened = await new SessionTrees(home).OpenAsync(root, "engine", "aurora");

            Assert.True(Path.Combine(opened.Path, deep).Length > 260, "the fixture must cross the limit to test it");
            Assert.True(File.Exists(Path.Combine(opened.Path, deep)));
        }
        finally
        {
            Remove(shortRoot);
        }
    }

    /// <summary>
    /// A tree that cannot be opened leaves the repository as it found it. `worktree add -b` makes the
    /// branch before it checks anything out, so every failed tick left one: sixteen on the first real
    /// workspace. And the refusal quotes git's error, where it quoted git's progress line
    /// (*"Preparing worktree (new branch …)"*), which said nothing about why.
    /// </summary>
    [Fact]
    public async Task A_tree_that_fails_to_check_out_leaves_no_branch_and_quotes_gits_error()
    {
        var root = await MakeRepositoryAsync("engine");
        // A checkout that fails after the branch exists, on every platform: a required filter that fails.
        await File.WriteAllTextAsync(Path.Combine(root, ".gitattributes"), "*.txt filter=boom\n");
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "x\n");
        await GitAsync(root, "config", "filter.boom.clean", "cat");
        await GitAsync(root, "config", "filter.boom.smudge", "false");
        await GitAsync(root, "config", "filter.boom.required", "true");
        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "-m", "a file only a working filter can check out");

        var error = await Assert.ThrowsAsync<DriverException>(
            () => new SessionTrees(_home).OpenAsync(root, "engine", "aurora"));

        Assert.DoesNotContain("Preparing worktree", error.Message);
        Assert.Contains("boom", error.Message);
        Assert.Equal("", (await GitAsync(root, "branch", "--list", "daoris/*")).Trim());
    }

    /// <summary>
    /// An open cut off while git is still adding its tree, the branch made and the tree checked out, leaves nothing: the add, a write
    /// the scheduler never ends, is waited out by the undo's removal, as a smudge filter's mark at its end shows.
    /// </summary>
    [Fact]
    public async Task An_open_cancelled_while_git_is_still_adding_its_tree_leaves_nothing()
    {
        var root = await MakeRepositoryAsync("engine");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitattributes"), "held.txt filter=held\n");
        await File.WriteAllTextAsync(Path.Combine(root, "held.txt"), "held\n");
        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "-m", "held");
        var ran = Path.Combine(_scratch, "smudge-ran");
        var ended = Path.Combine(_scratch, "smudge-ended");
        await GitAsync(root, "config", "filter.held.smudge", $"echo ran > '{ran.Replace('\\', '/')}'; sleep 3; cat; echo ended > '{ended.Replace('\\', '/')}'");
        using var closing = new CancellationTokenSource();

        var opening = new SessionTrees(_home).OpenAsync(root, "engine", "aurora", closing.Token);
        await Poll.Until(() => File.Exists(ran) || opening.IsCompleted, () => "git never ran the filter", TimeSpan.FromMinutes(2));
        Assert.Single((await GitAsync(root, "branch", "--list", "daoris/*")).Split('\n', StringSplitOptions.RemoveEmptyEntries));
        await closing.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
        Assert.True(File.Exists(ended), "the open's undo ran while git was still adding its tree");
        await AssertNothingLeftAsync(root);
    }

    /// <summary>
    /// A tree opens holding none of its checkout's installed dependencies, marked unworked with its hook held and owed.
    /// </summary>
    [Fact]
    public async Task A_tree_opens_unworked_with_its_hook_held_and_none_of_the_checkout_s_dependencies()
    {
        var root = await MakeNodeRepositoryAsync("engine");
        var hooks = Path.Combine(root, ".git", "hooks");
        Directory.CreateDirectory(hooks);
        await File.WriteAllTextAsync(Path.Combine(hooks, "post-checkout"), "#!/bin/sh\necho \"$3\" > hook-ran.txt\n");
        // The fixture's lockfile names no registry this test may reach: the person's restriction holds Daoris's install.
        Directory.CreateDirectory(Path.GetDirectoryName(DaorisHome.Entry(_home, HomeEntry.Files))!);
        await File.WriteAllTextAsync(DaorisHome.Entry(_home, HomeEntry.Files), """{ "machine": { "permissions": { "deny": ["run(*)"] } } }""");

        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");

        Assert.False(Directory.Exists(Path.Combine(opened.Path, "node_modules")));
        Assert.False(File.Exists(Path.Combine(opened.Path, "hook-ran.txt")), "the hook is held while git makes the tree");
        Assert.Equivalent(TreeFilesState.Made, TreeFilesRecord.Read(_home, opened.Path), strict: true);
        Assert.Contains("nothing git does not track", opened.Sentence);

        await TreeFilesStep.RunAsync(_home, root, opened.Path, "engine", _ => { }, CancellationToken.None);
        Assert.Equal("1", (await File.ReadAllTextAsync(Path.Combine(opened.Path, "hook-ran.txt"))).Trim());
        Assert.False(TreeFilesRecord.Read(_home, opened.Path)!.HookOwed);
        TreeFilesStep.Hand(_home, opened.Path);
        Assert.False(TreeFilesRecord.Read(_home, opened.Path)!.Unworked);
    }

    /// <summary>
    /// A tree whose store placement holds a workspace link is removed as any other, and git's removal takes the link, never
    /// what it leads to.
    /// </summary>
    [Fact]
    public async Task Removing_a_tree_with_placed_dependencies_removes_their_workspace_link_not_what_it_points_at()
    {
        var root = await MakeNodeRepositoryAsync("engine", workspace: true);
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        Directory.CreateDirectory(Path.Combine(opened.Path, "node_modules"));
        DependencyLinks.Link(Path.Combine(opened.Path, "node_modules", "ui"), Path.Combine(opened.Path, "packages", "ui"));

        var removal = await trees.RemoveAsync(opened.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(opened.Path));
        Assert.Equal("module.exports = 'ui';\n", await File.ReadAllTextAsync(Path.Combine(root, "packages", "ui", "index.js")));
        Assert.Equal("module.exports = 'ui';\n", await File.ReadAllTextAsync(Path.Combine(root, "node_modules", "ui", "index.js")));
    }

    /// <summary>
    /// A tree of a repository declaring its private pattern list holds a read-only copy of the checkout's, which
    /// git ignores there as in the checkout, and the repository's own sensitive scan, run in the tree, finds a token only that list
    /// names. Without it the devkit's scan refuses to run, and a scan of a repository's own falls back to its built-in patterns.
    /// </summary>
    [Fact]
    public async Task A_tree_of_a_repository_declaring_its_private_patterns_holds_a_read_only_copy_its_own_scan_reads()
    {
        var root = await MakeLocalRepositoryAsync("engine", """{ "sensitive": { "patternsFile": "local/sensitive-patterns.txt" } }""");
        await File.WriteAllTextAsync(Path.Combine(root, "local", "sensitive-patterns.txt"), "# private names\nquiltmark\n");

        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");
        var said = (await TreeFilesStep.RunAsync(_home, root, opened.Path, "engine", _ => { }, CancellationToken.None)).Line()!.Note;

        Assert.True(said.Contains("copied in from your checkout, read-only: `local/sensitive-patterns.txt`.", StringComparison.Ordinal), said);
        var copy = Path.Combine(opened.Path, "local", "sensitive-patterns.txt");
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(root, "local", "sensitive-patterns.txt")), await File.ReadAllBytesAsync(copy));
        Assert.True(File.GetAttributes(copy).HasFlag(FileAttributes.ReadOnly), "the copy is read-only");
        Assert.Equal("", (await GitAsync(opened.Path, "status", "--porcelain", "--untracked-files=all")).Trim());
        var (clean, _) = await WorkingTree.CleanAsync(opened.Path);
        Assert.True(clean, "the copy is ignored in the tree as in the checkout");

        var (code, scanned) = await DevkitAsync(opened.Path, "scan", "--tree");
        Assert.True(code == 1, $"the scan in the tree exited {code}: {scanned}");
        Assert.Contains("notes.md: private pattern", scanned);
    }

    /// <summary>A declared input the checkout does not hold is said by the files stage, and the tree holds none.</summary>
    [Fact]
    public async Task A_declared_input_the_checkout_lacks_is_said_by_the_tree_s_files_stage()
    {
        var root = await MakeLocalRepositoryAsync("engine", """{ "localInputs": ["local/allowed-hosts.txt"] }""");

        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora");
        var files = await TreeFilesStep.RunAsync(_home, root, opened.Path, "engine", _ => { }, CancellationToken.None);

        Assert.Contains("`local/allowed-hosts.txt` was not copied: the repository's gates read it, and your checkout does not hold it.", files.Line()!.Note);
        // The devkit's default list, implied and absent from the checkout too, says nothing.
        Assert.DoesNotContain("sensitive-patterns", files.Line()!.Note);
        Assert.False(Directory.Exists(Path.Combine(opened.Path, "local")));
        Assert.Null(files.Told);
    }

    /// <summary>A tree holding a read-only copy is removed as any other, the copy with it, and the checkout's file stays.</summary>
    [Fact]
    public async Task Removing_a_tree_holding_a_read_only_machine_local_input_removes_it_and_leaves_the_checkout_s()
    {
        var root = await MakeLocalRepositoryAsync("engine", "{}");
        await File.WriteAllTextAsync(Path.Combine(root, "local", "sensitive-patterns.txt"), "quiltmark\n");
        var trees = new SessionTrees(_home);
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await TreeFilesStep.RunAsync(_home, root, opened.Path, "engine", _ => { }, CancellationToken.None);
        Assert.True(File.GetAttributes(Path.Combine(opened.Path, "local", "sensitive-patterns.txt")).HasFlag(FileAttributes.ReadOnly));

        var removal = await trees.RemoveAsync(opened.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(opened.Path));
        Assert.Equal("quiltmark\n", await File.ReadAllTextAsync(Path.Combine(root, "local", "sensitive-patterns.txt")));
    }

    /// <summary>
    /// A chain's next step to the same repository starts ON the branch the
    /// step before it landed on, so a verify step sees the unmerged work it exists to check.
    /// </summary>
    [Fact]
    public async Task A_tree_grown_from_a_named_branch_holds_that_branchs_work()
    {
        var root = await MakeRepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var develop = await trees.OpenAsync(root, "engine", "aurora");
        await File.WriteAllTextAsync(Path.Combine(develop.Path, "notes.ts"), "export const notes = true;\n");
        await GitAsync(develop.Path, "add", ".");
        await GitAsync(develop.Path, "commit", "-m", "the develop step's work");

        var verify = await trees.OpenAsync(root, "engine", "aurora", from: develop.Branch);

        Assert.True(File.Exists(Path.Combine(verify.Path, "notes.ts")), "the next step's tree lacks the work before it");
        Assert.Contains(develop.Branch, verify.BasedOn);
        Assert.NotEqual(develop.Branch, verify.Branch);
    }

    /// <summary>A branch that is gone — merged and deleted, or discarded — falls back to the canonical line, and says so.</summary>
    [Fact]
    public async Task A_named_branch_that_is_gone_grows_from_the_canonical_line_and_says_why()
    {
        var root = await MakeRepositoryAsync("engine");

        var opened = await new SessionTrees(_home).OpenAsync(root, "engine", "aurora", from: "daoris/s-gone0000");

        Assert.Contains("canonical line", opened.BasedOn);
        Assert.Contains("daoris/s-gone0000", opened.BasedOn);
    }

    // ---------------------------------------------------------------------------------- fixtures

    /// <summary>The repository as it stood before the open: no session branch, no folder under its trees, no tree in git but the root.</summary>
    private async Task AssertNothingLeftAsync(string root)
    {
        Assert.Equal("", (await GitAsync(root, "branch", "--list", "daoris/*")).Trim());
        var folder = DaorisHome.Entry(_home, HomeEntry.Trees,"aurora", "engine");
        Assert.True(!Directory.Exists(folder) || Directory.GetFileSystemEntries(folder).Length == 0, "a folder is left under the trees");
        Assert.Single((await GitAsync(root, "worktree", "list", "--porcelain")).Split('\n'), line => line.StartsWith("worktree ", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(root, ".git", "worktrees")) && Directory.GetDirectories(Path.Combine(root, ".git", "worktrees")).Length > 0,
            "git keeps a record of a tree");
    }

    private async Task<string> MakeRepositoryAsync(string name, string? under = null)
    {
        var root = Path.Combine(under ?? _scratch, name);
        return await GitSeed.MadeAsync(root, $"session-trees:{name}", async made =>
        {
            await GitAsync(made, "init", "--quiet");
            await GitAsync(made, "config", "user.email", "fixture@example.test");
            await GitAsync(made, "config", "user.name", "Fixture");
            await File.WriteAllTextAsync(Path.Combine(made, "README.md"), $"# {name}\n");
            await GitAsync(made, "add", ".");
            await GitAsync(made, "commit", "-m", "first");
        });
    }

    /// <summary>
    /// A repository with an npm project its checkout installed: the lockfile committed, <c>node_modules</c> ignored and
    /// installed, npm's hidden lockfile written after the lockfile; with <paramref name="workspace"/>, a workspace npm links in.
    /// </summary>
    private async Task<string> MakeNodeRepositoryAsync(string name, bool workspace = false)
    {
        var root = await MakeRepositoryAsync(name);
        // The lockfile's bytes are compared: git writes the tree's as committed, so the checkout's is kept as committed too.
        await GitAsync(root, "config", "core.autocrlf", "false");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "node_modules/\n");
        var lockfile = Path.Combine(root, "package-lock.json");
        await File.WriteAllTextAsync(lockfile, workspace
            ? "{\n  \"lockfileVersion\": 3,\n  \"packages\": {\n    \"\": { \"workspaces\": [\"packages/ui\"] },\n"
              + "    \"node_modules/ui\": { \"resolved\": \"packages/ui\", \"link\": true },\n    \"packages/ui\": { \"name\": \"ui\" }\n  }\n}\n"
            : "{\n  \"lockfileVersion\": 3,\n  \"packages\": {\n    \"\": {},\n    \"node_modules/left-pad\": { \"version\": \"1.3.0\" }\n  }\n}\n");
        if (workspace)
        {
            Directory.CreateDirectory(Path.Combine(root, "packages", "ui"));
            await File.WriteAllTextAsync(Path.Combine(root, "packages", "ui", "index.js"), "module.exports = 'ui';\n");
        }

        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "-m", "an npm project");
        foreach (var package in new[] { "a", "b", "left-pad" })
        {
            Directory.CreateDirectory(Path.Combine(root, "node_modules", package));
            await File.WriteAllTextAsync(Path.Combine(root, "node_modules", package, "index.js"), $"module.exports = '{package}';\n");
        }

        var hidden = Path.Combine(root, "node_modules", ".package-lock.json");
        await File.WriteAllTextAsync(hidden, "{}\n");
        File.SetLastWriteTimeUtc(hidden, File.GetLastWriteTimeUtc(lockfile).AddMinutes(1));
        if (workspace) DependencyLinks.Link(Path.Combine(root, "node_modules", "ui"), Path.Combine(root, "packages", "ui"));
        return root;
    }

    /// <summary>
    /// A repository whose gates file is <paramref name="gates"/>: <c>local/</c> ignored and made in the checkout, and a
    /// tracked file holding a token no built-in pattern names.
    /// </summary>
    private async Task<string> MakeLocalRepositoryAsync(string name, string gates)
    {
        var root = await MakeRepositoryAsync(name);
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "local/\n");
        await File.WriteAllTextAsync(Path.Combine(root, "daoris.gates.json"), gates);
        await File.WriteAllTextAsync(Path.Combine(root, "notes.md"), "The quiltmark build is ours.\n");
        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "-m", "gates and notes");
        Directory.CreateDirectory(Path.Combine(root, "local"));
        return root;
    }

    private static readonly SemaphoreSlim DevkitBuilding = new(1, 1);
    private static string? _devkit;

    /// <summary>
    /// The devkit's own CLI run in <paramref name="cwd"/>: built once a run with <c>--artifacts-path</c> under this
    /// project's own <c>obj</c>, never referenced, as the service's host is, so the devkit's gate builds beside it.
    /// </summary>
    private static async Task<(int Code, string Said)> DevkitAsync(string cwd, params string[] arguments)
    {
        await DevkitBuilding.WaitAsync();
        try
        {
            if (_devkit is null || !File.Exists(_devkit))
            {
                // This test's build is <desktop>/<tests>/bin/<configuration>/<framework>/.
                var build = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var tests = build.Parent!.Parent!.Parent!;
                var project = Path.Combine(tests.Parent!.Parent!.FullName, "Daoris.Devkit", "Daoris.Devkit.Cli", "Daoris.Devkit.Cli.csproj");
                var artifacts = Path.Combine(tests.FullName, "obj", "devkit-cli");
                await RealServiceHost.BuildAsync(project, build.Parent.Name, artifacts);
                _devkit = Assert.Single(Directory.GetFiles(Path.Combine(artifacts, "bin"), "daoris-devkit.dll", SearchOption.AllDirectories));
            }
        }
        finally
        {
            DevkitBuilding.Release();
        }

        var info = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            WorkingDirectory = cwd, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        info.ArgumentList.Add(_devkit);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info)!;
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var limit = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(limit.Token);
        return (process.ExitCode, await output + await error);
    }

    /// <summary>Walk up from the test binary to the workspace manifest — the family's own locator shape.</summary>
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json")))
        {
            directory = directory.Parent!;
        }

        return directory!.FullName;
    }
}
