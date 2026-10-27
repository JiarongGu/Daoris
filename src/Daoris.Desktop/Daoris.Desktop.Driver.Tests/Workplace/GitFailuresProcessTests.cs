using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The transport's words each failure class is read from (GITAUTH1a, the git-auth design §5.1), measured with this machine's real git
/// and OpenSSH against hosts on the loopback: a push the host refuses, a connection nobody answers over HTTP and over SSH. The words
/// <c>GitWordsTests</c> holds as measured are these runs'.
/// </summary>
/// <remarks>
/// <para>Each git is started as Daoris starts its own (<c>WorkingTree.GitStart</c>), then kept from every configuration of the
/// person's: no system file, an empty global file, a scratch home, and an ssh given an empty configuration and a scratch known-hosts
/// file, so it reads nothing of the person's <c>~/.ssh</c>. Nothing here reaches a real host.</para>
/// <para>In the Process half, beside a listener: never <c>Parallel=Git</c>.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class GitFailuresProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-git-failures-process-" + Guid.NewGuid().ToString("N")[..8]);

    public GitFailuresProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Repo => Path.Combine(_root, "engine");

    private ProcessStartInfo Git(params string[] arguments)
    {
        Directory.CreateDirectory(Repo);
        var (info, refusal) = WorkingTree.GitStart(Repo, arguments, home: null);
        Assert.Null(refusal);
        var home = Path.Combine(_root, "home");
        Directory.CreateDirectory(home);
        var global = Path.Combine(_root, "global.gitconfig");
        File.WriteAllText(global, "");
        var config = Path.Combine(_root, "ssh_config");
        File.WriteAllText(config, "");
        info!.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        info.Environment["GIT_CONFIG_GLOBAL"] = global;
        info.Environment["HOME"] = home;
        info.Environment["USERPROFILE"] = home;
        info.Environment["XDG_CONFIG_HOME"] = home;
        var knownHosts = Path.Combine(_root, "known_hosts").Replace('\\', '/');
        info.Environment["GIT_SSH_COMMAND"] =
            $"ssh -F '{config.Replace('\\', '/')}' -o BatchMode=yes -o UserKnownHostsFile='{knownHosts}' -o ConnectTimeout=10";
        return info;
    }

    private static async Task<(int Exit, string Err)> RunAsync(ProcessStartInfo info)
    {
        using var process = Process.Start(info)!;
        // Both reads start before the wait (TESTGIT1).
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await stdout;
        return (process.ExitCode, await stderr);
    }

    private static int ClosedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private async Task Committed()
    {
        Assert.Equal(0, (await RunAsync(Git("init", "-q", "-b", "main"))).Exit);
        var commit = await RunAsync(Git("-c", "user.name=Fixture", "-c", "user.email=fixture@example.test", "commit", "--allow-empty", "-q", "-m", "one"));
        Assert.True(commit.Exit == 0, commit.Err);
    }

    [Fact]
    public async Task A_push_the_host_refuses_is_write_refused_and_its_fetch_is_refused()
    {
        await using var host = new RefusingHttpHost(_ => 403);
        await Committed();

        var push = await RunAsync(Git("push", "-q", host.Address, "main"));
        var fetch = await RunAsync(Git("fetch", "-q", host.Address, "main"));

        Assert.NotEqual(0, push.Exit);
        Assert.Contains("The requested URL returned error: 403", push.Err, StringComparison.Ordinal);
        Assert.Equal(GitFailureClass.WriteRefused, GitFailures.Class(push.Err, GitAct.Write));
        Assert.Equal(GitFailureClass.Refused, GitFailures.Class(fetch.Err, GitAct.Read));
    }

    [Fact]
    public async Task A_connection_nobody_answers_over_HTTP_is_unreachable()
    {
        var (exit, error) = await RunAsync(Git("ls-remote", $"http://127.0.0.1:{ClosedPort()}/org/engine.git"));

        Assert.NotEqual(0, exit);
        Assert.Equal(GitFailureClass.Unreachable, GitFailures.Class(error, GitAct.Read));
    }

    [Fact]
    public async Task A_connection_nobody_answers_over_SSH_is_unreachable_in_OpenSSHs_words()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (exit, error) = await RunAsync(Git("ls-remote", $"ssh://git@127.0.0.1:{ClosedPort()}/org/engine.git"));

        Assert.NotEqual(0, exit);
        Assert.Contains("Connection refused", error, StringComparison.Ordinal);
        Assert.Equal(GitFailureClass.Unreachable, GitFailures.Class(error, GitAct.Read));
    }
}
