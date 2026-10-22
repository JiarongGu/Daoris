using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Nothing Daoris starts prompts through git (GITAUTH1a, D198 point 6; the git-auth design §4.1, §4.6 and §14 row 1), measured
/// with this machine's real git against a host on the loopback that asks for a sign-in: the empty <c>GIT_ASKPASS</c> Daoris
/// hands survives Windows' environment block into git, and stops git's search before the program <c>core.askPass</c> names.
/// </summary>
/// <remarks>
/// <para>Each git is started as Daoris starts its own (<c>WorkingTree.GitStart</c>), then kept from every configuration of the
/// person's: no system file (where Git for Windows names Git Credential Manager), an empty global file and a scratch home, so
/// nothing here reads a credential store or starts GCM. The control run takes the empty value out, and the same askpass then
/// runs: the measurement bites.</para>
/// <para>In the Process half, beside a listener: never <c>Parallel=Git</c>.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class GitPromptsProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-git-prompts-process-" + Guid.NewGuid().ToString("N")[..8]);

    public GitPromptsProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Marker => Path.Combine(_root, "asked");

    /// <summary>An askpass that says it was asked, and answers a name nobody holds.</summary>
    private string Askpass()
    {
        var file = Path.Combine(_root, "askpass.sh");
        File.WriteAllText(file, $"#!/bin/sh\necho asked >> '{Marker.Replace('\\', '/')}'\necho nobody\n");
        return file.Replace('\\', '/');
    }

    /// <summary>A git started as Daoris starts its own, kept from the person's configuration and home.</summary>
    private ProcessStartInfo Git(params string[] arguments)
    {
        var (info, refusal) = WorkingTree.GitStart(_root, arguments, home: null);
        Assert.Null(refusal);
        var home = Path.Combine(_root, "home");
        Directory.CreateDirectory(home);
        var global = Path.Combine(_root, "global.gitconfig");
        File.WriteAllText(global, "");
        info!.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        info.Environment["GIT_CONFIG_GLOBAL"] = global;
        info.Environment["HOME"] = home;
        info.Environment["USERPROFILE"] = home;
        info.Environment["XDG_CONFIG_HOME"] = home;
        return info;
    }

    private static async Task<(int Exit, string Out, string Err)> RunAsync(ProcessStartInfo info)
    {
        using var process = Process.Start(info)!;
        // Both reads start before the wait (TESTGIT1).
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout, await stderr);
    }

    [Fact]
    public async Task The_empty_GIT_ASKPASS_reaches_git_and_its_child_through_the_environment_block()
    {
        if (!OperatingSystem.IsWindows()) return;
        var info = Git("-c", "alias.e=!cmd", "e", "/d", "/c", "set", "GIT_");

        var (exit, output, error) = await RunAsync(info);

        Assert.True(exit == 0, error);
        var lines = output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("GIT_ASKPASS=", lines);
        Assert.Contains("GIT_TERMINAL_PROMPT=0", lines);
        Assert.Contains("GIT_TRACE_REDACT=1", lines);
    }

    [Fact]
    public async Task A_host_asking_for_a_sign_in_runs_no_askpass_and_fails_at_once()
    {
        await using var host = new RefusingHttpHost(_ => 401);

        var (exit, _, error) = await RunAsync(Git("-c", $"core.askPass={Askpass()}", "ls-remote", host.Address));

        Assert.NotEqual(0, exit);
        Assert.False(File.Exists(Marker), "the askpass core.askPass names ran past the empty GIT_ASKPASS");
        Assert.Contains("terminal prompts disabled", error, StringComparison.Ordinal);
        Assert.Equal(GitFailureClass.Refused, GitFailures.Class(error, GitAct.Read));
    }

    [Fact]
    public async Task Without_the_empty_value_the_same_askpass_runs_so_the_measurement_bites()
    {
        await using var host = new RefusingHttpHost(_ => 401);
        var info = Git("-c", $"core.askPass={Askpass()}", "ls-remote", host.Address);
        info.Environment.Remove("GIT_ASKPASS");

        var (exit, _, error) = await RunAsync(info);

        Assert.NotEqual(0, exit);
        Assert.True(File.Exists(Marker), "the control's askpass never ran: " + error);
        Assert.Contains("Authentication failed", error, StringComparison.Ordinal);
        Assert.Equal(GitFailureClass.Refused, GitFailures.Class(error, GitAct.Read));
    }
}
