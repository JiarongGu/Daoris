using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The scratch a landed branch's tests share (WSR5): a home whose `engine` lands on branches named
/// <c>feature/{quest}-{slug}</c>, repositories made in a scratch folder, and the moves a platform makes —
/// a squash merge onto the line, a branch deleted on the remote. Split over several classes so they run
/// side by side: each test spawns git dozens of times.
/// </summary>
/// <remarks>
/// 🔴 <b>Nothing here reaches a network.</b> Where a test needs `origin`, it is a bare repository under
/// the scratch folder, and a "platform" that squash-merges is a second clone of it.
/// </remarks>
public abstract class LandedFixture : IDisposable
{
    protected readonly string Scratch;
    protected readonly string Home;

    protected LandedFixture()
    {
        Scratch = Path.Combine(RepoRoot(), "_fixtures", "landed", Guid.NewGuid().ToString("N")[..8]);
        Home = Path.Combine(Scratch, "daoris-home");
        Directory.CreateDirectory(Home);
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"))
            .Save(Path.Combine(Home, "driver.json"));
    }

    public void Dispose()
    {
        // A plugin's process, where a test ran one, stands in its folder a moment after it is told to go (FLAKE1).
        for (var attempt = 0; Directory.Exists(Scratch); attempt++)
        {
            try
            {
                Directory.Delete(Scratch, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 20) return;
                Thread.Sleep(250);
            }
        }

        GC.SuppressFinalize(this);
    }

    protected static LandedItem Landed(SweepPlan plan, string branch) => plan.Landed.Single(item => item.Branch == branch);

    /// <summary>A session's tree, one commit, and the press — the branch form, so a landing makes and records a branch.</summary>
    protected static async Task<TreeLanding> LandAsync(
        SessionTrees trees, string root, string file, string content, LandingSubject subject, string? from = null,
        (string File, string Content)? extra = null)
    {
        var tree = await trees.OpenAsync(root, "engine", "aurora", from: from);
        await File.WriteAllTextAsync(Path.Combine(tree.Path, file), content);
        if (extra is { } more) await File.WriteAllTextAsync(Path.Combine(tree.Path, more.File), more.Content);
        await GitAsync(tree.Path, "add", "-A");
        await GitAsync(tree.Path, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", subject.Title ?? "work");
        var landed = await trees.LandAsync(tree.Path, subject);
        Assert.True(landed.Landed, landed.Message);
        return landed;
    }

    /// <summary>A squash merge onto the checkout's own line, as a platform's "squash and merge" leaves it.</summary>
    protected static async Task SquashAsync(string root, string branch)
    {
        await GitAsync(root, "merge", "--squash", branch);
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", $"{branch} (squashed)");
    }

    /// <summary>The platform's side: a clone of the bare origin squash-merges the branch onto `main`, pushes, and deletes the branch there.</summary>
    protected async Task SquashOnPlatformAsync(string origin, string branch, bool deleteBranch = true)
    {
        var platform = Path.Combine(Scratch, $"platform-{Guid.NewGuid():N}"[..20]);
        await GitAsync(Scratch, "clone", "--quiet", origin, platform);
        await GitAsync(platform, "merge", "--squash", $"origin/{branch}");
        await GitAsync(platform, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "Squashed (#7)");
        await GitAsync(platform, "push", "--quiet", "origin", "main");
        if (deleteBranch) await GitAsync(platform, "push", "--quiet", "origin", "--delete", branch);
    }

    /// <summary>A commit on a branch nobody has checked out, through a worktree made and removed for it.</summary>
    protected async Task CommitOnAsync(string root, string branch, string file, string content, string message)
    {
        var place = Path.Combine(Scratch, $"on-{Guid.NewGuid():N}"[..12]);
        await GitAsync(root, "worktree", "add", "--quiet", place, branch);
        await CommitAsync(place, file, content, message);
        await GitAsync(root, "worktree", "remove", place);
    }

    protected async Task<string> RepositoryAsync(string name)
    {
        var root = Path.Combine(Scratch, name);
        Directory.CreateDirectory(root);
        await GitAsync(root, "init", "--quiet", "-b", "main");
        await GitAsync(root, "config", "user.email", "fixture@example.test");
        await GitAsync(root, "config", "user.name", "Fixture");
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), $"# {name}\n");
        await CommitAsync(root, "shared.txt", "one\n", "first");
        return root;
    }

    /// <summary>A repository whose `origin` is a bare repository beside it — somewhere a push lands without leaving the machine.</summary>
    protected async Task<(string Root, string Origin)> RepositoryWithOriginAsync(string name)
    {
        var origin = Path.Combine(Scratch, $"{name}-origin.git");
        Directory.CreateDirectory(origin);
        await GitAsync(origin, "init", "--quiet", "--bare", "-b", "main");
        var root = await RepositoryAsync(name);
        await GitAsync(root, "remote", "add", "origin", origin);
        await GitAsync(root, "push", "--quiet", "-u", "origin", "main");
        return (root, origin);
    }

    protected static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content);
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }

    protected static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return stdout;
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
