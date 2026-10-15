using System.Text.Json;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the look's tidy classes over real git share (AUTOTIDY1, D88's note): a home under the scratch folder, the stand-in
/// ledger read through the real client, repositories on <c>main</c>, session trees with work, landed or landed by Accept's
/// branch form, and the machine log's events. One class until PROC1g split it four ways by the time each takes: three run four
/// at once with the git-only classes (<see cref="BranchTidyingProcessTests"/>, <see cref="BranchTidyingFolderTests"/> and
/// <see cref="BranchTidyingLandingTests"/>), and <see cref="BranchTidyingWatchTests"/>, whose watch ticks, one at a time.
/// </summary>
public abstract class BranchTidyingFixture : IDisposable
{
    // A look judges each branch the line holds with several git calls, twice: a dozen took over a minute while other worktrees
    // built (FLAKE1), and 22 seconds alone.
    protected static readonly TimeSpan Bound = TimeSpan.FromMinutes(4);

    protected readonly string _scratch;
    protected readonly string _home;
    private protected readonly StandInLedger _ledger = new();
    protected readonly CancellationTokenSource _closing = new(TimeSpan.FromMinutes(10));
    protected DateTimeOffset _now = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    protected BranchTidyingFixture()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "autotidy", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        // Git leaves read-only objects and tree links on Windows, as GitTree's cleanup says; a failed cleanup is not a failed test.
        try
        {
            // The links a test made go first, alone, so the delete below never meets one.
            var walk = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var link in Directory.EnumerateDirectories(_scratch, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 })
                         .Where(folder => File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint)).ToList())
            {
                Directory.Delete(link);
            }

            foreach (var file in Directory.EnumerateFiles(_scratch, "*", walk))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_scratch, recursive: true);
        }
        catch (IOException) { /* a straggling git handle */ }
        catch (UnauthorizedAccessException) { /* a file something still holds */ }

        GC.SuppressFinalize(this);
    }

    /// <summary>The id of a process that ran and ended: one an opening's lock may name, that no longer runs.</summary>
    protected static async Task<int> EndedProcessAsync()
    {
        using var process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("git", "--version") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true })!;
        await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return process.Id;
    }

    /// <summary>A link at <paramref name="link"/> to <paramref name="target"/>: a junction on Windows, which needs no privilege.</summary>
    protected static async Task JunctionAsync(string link, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        var info = new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"mklink failed: {await output} {await error}");
    }

    /// <summary>How a removal by the look opens: the folder moved aside follows, named for the moment it moved.</summary>
    protected static string Removed(TreeOpened tree) =>
        $"tidy  engine: removed `{tree.Branch}`, which held nothing beyond `main`; its tree's folder was moved aside to ";

    protected MachineLog Log() => new(_home, "driver", () => _now);

    /// <summary>The data of every line of <paramref name="event"/> the machine log holds.</summary>
    protected JsonElement[] Events(string @event)
    {
        var folder = Path.Combine(_home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return
        [
            .. Directory.EnumerateFiles(folder, "*.jsonl").SelectMany(StubFile.Lines)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone())
                .Where(line => line.GetProperty("event").GetString() == @event)
                .Select(line => line.GetProperty("data")),
        ];
    }

    protected async Task<TreeOpened> TreeWithWorkAsync(SessionTrees trees, string root)
    {
        var tree = await trees.OpenAsync(root, "engine", "default");
        await CommitAsync(tree.Path, $"{Guid.NewGuid():N}.txt", "the session's work", "the work");
        return tree;
    }

    /// <summary>A session tree whose work a merge took onto the line, as a pull request completed with a merge commit leaves one.</summary>
    protected async Task<TreeOpened> LandedTreeAsync(SessionTrees trees, string root)
    {
        var tree = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "merge", "--no-ff", "--no-edit", tree.Branch);
        return tree;
    }

    /// <summary>This home's trees, with `engine`'s landing rule the branch form: Accept makes a branch of the person's and records it.</summary>
    protected SessionTrees LandingTrees()
    {
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"))
            .Save(Path.Combine(_home, "driver.json"));
        return new SessionTrees(_home);
    }

    /// <summary>A session's tree with one commit, landed by Accept on a branch the record keeps (the branch form's rule).</summary>
    protected async Task<(TreeOpened Tree, string Branch)> LandingAsync(SessionTrees trees, string root, LandingSubject subject)
    {
        var tree = await TreeWithWorkAsync(trees, root);
        var landed = await trees.LandAsync(tree.Path, subject);
        Assert.True(landed.Landed, landed.Message);
        return (tree, landed.Branch!);
    }

    /// <summary>A squash merge onto the checkout's own line, as a platform's "squash and merge" leaves it.</summary>
    protected static async Task SquashAsync(string root, string branch)
    {
        await GitAsync(root, "merge", "--squash", branch);
        await GitAsync(root, "commit", "--quiet", "-m", $"{branch} (squashed)");
    }

    /// <summary>A commit on a branch nobody has checked out, through a worktree made and removed for it.</summary>
    protected async Task CommitOnAsync(string root, string branch)
    {
        var place = Path.Combine(_scratch, $"on-{Guid.NewGuid():N}"[..12]);
        await GitAsync(root, "worktree", "add", "--quiet", place, branch);
        await CommitAsync(place, "after-the-push.txt", "after the push\n", "after the push");
        await GitAsync(root, "worktree", "remove", place);
    }

    /// <summary>A repository on <c>main</c> that ignores a database and a build's output, and holds a build's output itself.</summary>
    protected async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(_scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "*.db\nbuild/\n");
        await CommitAsync(root, "README.md", $"# {name}\n", "first");
        Directory.CreateDirectory(Path.Combine(root, "build"));
        await File.WriteAllTextAsync(Path.Combine(root, "build", "out.txt"), "the checkout's own build\n");
        return root;
    }

    protected static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content);
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--quiet", "-m", message);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
