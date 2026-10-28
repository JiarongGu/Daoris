using System.Diagnostics;
using System.Security.Cryptography;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1a (D199 point 5; the co-agent design §2.5, §8.3): a helper's copy holds the asker's uncommitted bytes and nothing returns
/// from it, with real git. The capture reads the asker's tree with no write to its index, refs or files; a copy laid with it shows
/// the work in progress to git's own status and holds no ignored file; what the helper writes in its copy never reaches the asker;
/// a second opinion's copy, made as before, holds the last commit alone; and an unresolved merge and a link out of the tree are
/// refused by the capture as git and the file system show them.
/// </summary>
/// <remarks>
/// Real git, and a junction made by <c>mklink</c>, so the class is in the real-process half's git classes (MOD8, PROC1b): a branch's
/// worktree runs it alone with <c>git.process.runsettings</c>, the parent in its set. The repository is a copy of the run's seed
/// (<see cref="GitSeed"/>): no test here reads the first commit's id or time.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class HelperCaptureProcessTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "helper-capture", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _home;

    public HelperCaptureProcessTests()
    {
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose() => GitSeed.Discard(_scratch);

    /// <summary>A copy of the seed: a README, a source file and an ignore rule for logs, committed on <c>main</c>.</summary>
    private async Task<string> RepositoryAsync()
    {
        var seed = await GitSeed.SeedAsync("helper-capture", async folder =>
        {
            var root = Path.Combine(folder, "reports");
            Directory.CreateDirectory(Path.Combine(root, "src"));
            await GitAsync(root, "init", "--quiet", "-b", "main");
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# reports\n");
            await File.WriteAllTextAsync(Path.Combine(root, "src", "report.ts"), "export const days = 6;\n");
            await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "*.log\n");
            await CommitAsync(root, "first");
        });
        var copy = Path.Combine(_scratch, "reports");
        GitSeed.Copy(Path.Combine(seed, "reports"), copy);
        return copy;
    }

    [Fact]
    public async Task A_helpers_copy_holds_the_askers_uncommitted_bytes_and_nothing_returns_from_it()
    {
        var root = await RepositoryAsync();
        await File.WriteAllTextAsync(Path.Combine(root, "src", "report.ts"), "export const days = 7;\n");
        File.Delete(Path.Combine(root, "README.md"));
        await File.WriteAllTextAsync(Path.Combine(root, "src", "window.ts"), "export const window = 'inclusive';\n");
        byte[] binary = [0, 1, 2, 255, 0, 13, 10];
        await File.WriteAllBytesAsync(Path.Combine(root, "icon.bin"), binary);
        await File.WriteAllTextAsync(Path.Combine(root, "debug.log"), "ignored\n");
        var index = Hash(Path.Combine(root, ".git", "index"));
        var refs = await GitAsync(root, "for-each-ref");
        var head = (await GitAsync(root, "rev-parse", "HEAD")).Trim();

        var read = await HelperCapture.TakeAsync(root);

        var capture = Assert.IsType<HelperCaptured>(read.Captured);
        Assert.Equal(head, capture.Commit);
        Assert.Equal(["icon.bin", "src/report.ts", "src/window.ts"], capture.Files.Select(file => file.Path));
        Assert.Equal(["README.md"], capture.Deleted);
        // It wrote nothing of the asker's: its index, its refs and its files are as they were.
        Assert.Equal(index, Hash(Path.Combine(root, ".git", "index")));
        Assert.Equal(refs, await GitAsync(root, "for-each-ref"));

        var copy = await OpinionTree.OpenAsync(_home, root, "reports", null, capture.Commit);
        try
        {
            Assert.Null(HelperCapture.LayOver(copy.Path, capture));

            Assert.Equal("export const days = 7;\n", await File.ReadAllTextAsync(Path.Combine(copy.Path, "src", "report.ts")));
            Assert.Equal(binary, await File.ReadAllBytesAsync(Path.Combine(copy.Path, "icon.bin")));
            Assert.False(File.Exists(Path.Combine(copy.Path, "README.md")));
            Assert.False(File.Exists(Path.Combine(copy.Path, "debug.log")));
            var status = await GitAsync(copy.Path, "status", "--porcelain", "--untracked-files=all");
            Assert.Contains(" M src/report.ts", status);
            Assert.Contains(" D README.md", status);
            Assert.Contains("?? src/window.ts", status);

            // What a helper writes in its copy stays there.
            await File.WriteAllTextAsync(Path.Combine(copy.Path, "src", "report.ts"), "the helper's own edit\n");
            await File.WriteAllTextAsync(Path.Combine(copy.Path, "helper-note.md"), "mine\n");
        }
        finally
        {
            Assert.Null(await OpinionTree.RemoveAsync(_home, copy.Path));
        }

        Assert.Equal("export const days = 7;\n", await File.ReadAllTextAsync(Path.Combine(root, "src", "report.ts")));
        Assert.False(File.Exists(Path.Combine(root, "helper-note.md")));
        Assert.Equal(index, Hash(Path.Combine(root, ".git", "index")));
        Assert.Equal(refs, await GitAsync(root, "for-each-ref"));
    }

    /// <summary>A second opinion's copy is made as before: the last commit alone, whatever the tree holds uncommitted (D155 point 6).</summary>
    [Fact]
    public async Task A_second_opinions_copy_holds_the_last_commit_alone()
    {
        var root = await RepositoryAsync();
        await File.WriteAllTextAsync(Path.Combine(root, "src", "report.ts"), "export const days = 7;\n");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "window.ts"), "uncommitted\n");

        var copy = await OpinionTree.OpenAsync(_home, root, "reports", null, (await GitAsync(root, "rev-parse", "HEAD")).Trim());
        try
        {
            // As git checks it out on this machine, whatever its line endings.
            Assert.Equal("export const days = 6;\n", (await File.ReadAllTextAsync(Path.Combine(copy.Path, "src", "report.ts"))).ReplaceLineEndings("\n"));
            Assert.False(File.Exists(Path.Combine(copy.Path, "src", "window.ts")));
        }
        finally
        {
            Assert.Null(await OpinionTree.RemoveAsync(_home, copy.Path));
        }
    }

    [Fact]
    public async Task An_unresolved_merge_is_refused()
    {
        var root = await RepositoryAsync();
        await GitAsync(root, "checkout", "--quiet", "-b", "other");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "report.ts"), "export const days = 8;\n");
        await CommitAsync(root, "other");
        await GitAsync(root, "checkout", "--quiet", "main");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "report.ts"), "export const days = 9;\n");
        await CommitAsync(root, "main");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "merge", "other");

        var read = await HelperCapture.TakeAsync(root);

        Assert.Equal((HelperWhy.CaptureMerge, "src/report.ts"), (read.Code, read.Detail));
    }

    /// <summary>A junction in the work that leads out of the tree is refused, never followed; one that stays inside is left out, said.</summary>
    [Fact]
    public async Task A_link_out_of_the_tree_is_refused_and_one_inside_is_left_out()
    {
        var root = await RepositoryAsync();
        var outside = Path.Combine(_scratch, "outside");
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "secret.txt"), "not the work\n");
        await JunctionAsync(Path.Combine(root, "linked"), outside);

        var escaping = await HelperCapture.TakeAsync(root);

        Assert.Equal(HelperWhy.CaptureLink, escaping.Code);

        Directory.Delete(Path.Combine(root, "linked"));
        await JunctionAsync(Path.Combine(root, "inside"), Path.Combine(root, "src"));

        var inside = await HelperCapture.TakeAsync(root);

        // git may list the link itself or the files it finds through it; either is left out, said, and nothing is read through it.
        var capture = Assert.IsType<HelperCaptured>(inside.Captured);
        Assert.NotEmpty(capture.LeftOut);
        Assert.All(capture.LeftOut, left => Assert.Equal(HelperCaptures.LeftLink, left.Why));
        Assert.All(capture.LeftOut, left => Assert.StartsWith("inside", left.Path, StringComparison.Ordinal));
        Assert.Empty(capture.Files);
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static async Task CommitAsync(string root, string message)
    {
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "--quiet", "-m", message);
    }

    private static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        // Both streams read at once, before the wait, so a git that writes a pipe's worth to one never blocks on it.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        return await stdout;
    }

    /// <summary>A link at <paramref name="link"/> to <paramref name="target"/>: a junction on Windows, which needs no privilege.</summary>
    private static async Task JunctionAsync(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        var info = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"mklink failed: {await output} {await error}");
    }
}
