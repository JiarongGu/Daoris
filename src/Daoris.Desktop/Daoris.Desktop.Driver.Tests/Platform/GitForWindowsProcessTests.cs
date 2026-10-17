using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Git for Windows' launcher, started past for real (GITSTART1): against this machine's install, the real git started
/// directly hands its children what the launcher's would, and a hook and a <c>!</c> alias still find <c>sh</c>. Where
/// the machine has no such install, each test holds nothing.
/// </summary>
/// <remarks>
/// A child's environment is read by a <c>!cmd</c> alias, which git starts without a shell (its first word holds no
/// shell character), so the dump is the block git handed its child, not one a shell rewrote. In the serial half: it
/// reads this process's environment, which the Git classes beside each other may not.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class GitForWindowsProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-git-for-windows-process-" + Guid.NewGuid().ToString("N")[..8]);

    public GitForWindowsProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>This machine's launcher: the one <c>PATH</c> finds, the one beside the real git it finds, or the default install's.</summary>
    private static string? Launcher()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var found = CommandPresence.Resolve("git", startable: true);
        List<string> candidates = [];
        if (found is not null) candidates.AddRange([found, Path.Combine(Path.GetFullPath(Path.Combine(found, "..", "..", "..")), "cmd", "git.exe")]);
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"));
        return candidates.FirstOrDefault(candidate => File.Exists(candidate) && GitForWindows.Behind(candidate) is not null);
    }

    private static string BarePath =>
        $"{Environment.SystemDirectory};{Path.GetDirectoryName(Environment.SystemDirectory)}";

    private ProcessStartInfo Start(string file, string cwd, string environment, params string[] arguments)
    {
        var info = new ProcessStartInfo(file) { WorkingDirectory = cwd };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        switch (environment)
        {
            case "this process's":
                break;
            case "no HOME, MSYSTEM or PLINK_PROTOCOL":
                info.Environment.Remove("HOME");
                info.Environment.Remove("MSYSTEM");
                info.Environment.Remove("PLINK_PROTOCOL");
                break;
            case "HOME, MSYSTEM and PLINK_PROTOCOL set":
                info.Environment["HOME"] = Path.Combine(_root, "home");
                info.Environment["MSYSTEM"] = "PROBE";
                info.Environment["PLINK_PROTOCOL"] = "probe";
                break;
            case "a bare PATH and no MSYSTEM":
                info.Environment["PATH"] = BarePath;
                info.Environment.Remove("MSYSTEM");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(environment), environment, null);
        }

        return info;
    }

    private static async Task<(int Exit, string Out, string Err)> RunAsync(ProcessStartInfo info)
    {
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.StandardOutputEncoding = Encoding.UTF8;
        info.StandardErrorEncoding = Encoding.UTF8;
        using var process = Process.Start(info)!;
        // Both reads start before the wait (TESTGIT1).
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout, await stderr);
    }

    private static readonly string[] Dump = ["-c", "alias.e=!cmd", "e", "/d", "/c", "set"];

    private static string[] Lines(string text) =>
        [.. text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.OrdinalIgnoreCase)];

    [Theory]
    [InlineData("this process's")]
    [InlineData("no HOME, MSYSTEM or PLINK_PROTOCOL")]
    [InlineData("HOME, MSYSTEM and PLINK_PROTOCOL set")]
    [InlineData("a bare PATH and no MSYSTEM")]
    public async Task The_real_git_started_directly_hands_its_child_what_the_launchers_git_does(string environment)
    {
        if (Launcher() is not { } launcher) return;
        var throughLauncher = Start(launcher, _root, environment, Dump);
        var direct = Start(launcher, _root, environment, Dump);

        Assert.True(GitForWindows.Direct(direct));
        Assert.NotEqual(launcher, direct.FileName);
        var (launched, started) = (await RunAsync(throughLauncher), await RunAsync(direct));

        Assert.Equal(0, launched.Exit);
        Assert.Equal(0, started.Exit);
        Assert.Equal(Lines(launched.Out), Lines(started.Out));
    }

    [Fact]
    public async Task A_hook_and_a_bang_alias_still_find_sh_through_the_direct_start()
    {
        if (Launcher() is not { } launcher) return;
        var repo = Path.Combine(_root, "repo");
        Directory.CreateDirectory(repo);
        // A bare PATH and no MSYSTEM: git repairs neither once MSYSTEM is set, so only the launcher's variables find sh.
        ProcessStartInfo Git(params string[] arguments)
        {
            var info = Start(launcher, repo, "a bare PATH and no MSYSTEM", arguments);
            Assert.True(GitForWindows.Direct(info));
            return info;
        }

        Assert.Equal(0, (await RunAsync(Git("init", "-q"))).Exit);
        File.WriteAllText(Path.Combine(repo, ".git", "hooks", "pre-commit"), "#!/bin/sh\necho hook-ran > hook.out\n");
        var commit = await RunAsync(Git("-c", "user.name=Fixture", "-c", "user.email=fixture@example.test", "commit", "--allow-empty", "-q", "-m", "probe"));
        var alias = await RunAsync(Git("-c", "alias.probe=!echo bang-alias-ran", "probe"));

        Assert.True(commit.Exit == 0, commit.Err);
        Assert.Equal("hook-ran", File.ReadAllText(Path.Combine(repo, "hook.out")).Trim());
        Assert.True(alias.Exit == 0, alias.Err);
        Assert.Equal("bang-alias-ran", alias.Out.Trim());
    }
}
