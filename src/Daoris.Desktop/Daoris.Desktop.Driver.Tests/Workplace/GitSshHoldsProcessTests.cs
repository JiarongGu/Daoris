using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git-auth design §14 row 1's proof over SSH (GITAUTH1a): Daoris's fetch of the line against an OpenSSH server on the loopback
/// that refuses the key is held once, and no moment after it connects to the server again until <i>Try again</i>; a server whose key
/// changed is held as <c>host-changed</c>. Counted by the server's own log of each connection.
/// </summary>
/// <remarks>
/// <para><b>Its server</b> is an OpenSSH <c>sshd.exe</c> named by <c>DAORIS_TEST_SSHD</c> (Win32-OpenSSH's portable build serves
/// the loopback as an ordinary account), started on a free loopback port with a host key made here; where the variable names none,
/// each test holds nothing, as <c>GitForWindowsProcessTests</c> does on a machine without Git for Windows. Its keys are made by the
/// <c>ssh-keygen.exe</c> beside it, each limited to this account as OpenSSH requires.</para>
/// <para>Every git and ssh here is kept from the person's own: no system or global git configuration, a scratch home, and an ssh
/// handed an empty configuration, a key of its own with <c>IdentitiesOnly</c>, no agent and a known-hosts file of the test's.</para>
/// <para>Alone: it sets the process's environment (the home <c>TreeRefs</c> resolves, the git variables above).</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Collection(Alone.Name)]
public sealed class GitSshHoldsProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-git-ssh-holds-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Dictionary<string, string?> _was = [];

    private Process? _server;

    private readonly List<string> _said = [];

    private string Home => Path.Combine(_root, "home");

    private string Repo => Path.Combine(_root, "engine");

    private string Keys => Path.Combine(_root, "keys");

    private static string? Sshd() =>
        Environment.GetEnvironmentVariable("DAORIS_TEST_SSHD") is { Length: > 0 } named && File.Exists(named) && OperatingSystem.IsWindows() ? named : null;

    public GitSshHoldsProcessTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Repo);
        Directory.CreateDirectory(Keys);
    }

    private void Set(string name, string value)
    {
        if (!_was.ContainsKey(name)) _was[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
    {
        try { _server?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        _server?.Dispose();
        foreach (var (name, value) in _was) Environment.SetEnvironmentVariable(name, value);
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string Slashed(string path) => path.Replace('\\', '/');

    private static async Task Run(string file, params string[] arguments)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"{Path.GetFileName(file)} {string.Join(' ', arguments)}: {await error}{await output}");
    }

    private async Task<string> KeyAsync(string sshd, string name)
    {
        var file = Path.Combine(Keys, name);
        await Run(Path.Combine(Path.GetDirectoryName(sshd)!, "ssh-keygen.exe"), "-q", "-t", "ed25519", "-N", "", "-C", "fixture-" + name, "-f", file);
        // OpenSSH refuses a private key others can read: this account alone.
        await Run("icacls", file, "/inheritance:r", "/grant:r", $"{Environment.UserName}:F");
        return file;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>Its server on a free loopback port, refusing every key, and the known-hosts line ssh is to trust it by.</summary>
    private async Task<(int Port, string Known)> ServeAsync(string sshd)
    {
        var host = await KeyAsync(sshd, "host");
        var authorized = Path.Combine(Keys, "authorized_keys");
        File.WriteAllText(authorized, "");
        var port = FreePort();
        var config = Path.Combine(Keys, "sshd_config");
        File.WriteAllText(config, $"ListenAddress 127.0.0.1\nPort {port}\nHostKey {Slashed(host)}\nAuthorizedKeysFile {Slashed(authorized)}\n"
            + "PasswordAuthentication no\nKbdInteractiveAuthentication no\nPubkeyAuthentication yes\nStrictModes no\nPidFile none\nLogLevel VERBOSE\n");
        var info = new ProcessStartInfo(sshd) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "-D", "-e", "-f", config }) info.ArgumentList.Add(argument);
        _server = Process.Start(info)!;
        var listening = new TaskCompletionSource();
        _server.ErrorDataReceived += (_, line) =>
        {
            if (line.Data is not { } said) return;
            lock (_said) _said.Add(said);
            if (said.Contains("Server listening", StringComparison.Ordinal)) listening.TrySetResult();
        };
        _server.BeginErrorReadLine();
        await listening.Task.WaitAsync(TimeSpan.FromSeconds(20));
        return (port, $"[127.0.0.1]:{port} {string.Join(' ', File.ReadAllText(host + ".pub").Split(' ')[..2])}");
    }

    private int Connections()
    {
        lock (_said) return _said.Count(line => line.Contains("Connection from 127.0.0.1", StringComparison.Ordinal));
    }

    /// <summary>The process's git kept from the person's, and its ssh handed the test's own key, configuration and known hosts.</summary>
    private async Task WireAsync(string sshd, string known)
    {
        var client = await KeyAsync(sshd, "client");
        var config = Path.Combine(Keys, "empty_config");
        File.WriteAllText(config, "");
        var knownHosts = Path.Combine(Keys, "known_hosts");
        File.WriteAllText(knownHosts, known + "\n");
        var scratch = Path.Combine(_root, "user");
        Directory.CreateDirectory(scratch);
        Set(DaorisHome.Variable, Home);
        Set("GIT_CONFIG_NOSYSTEM", "1");
        Set("GIT_CONFIG_GLOBAL", config);
        Set("HOME", scratch);
        Set("XDG_CONFIG_HOME", scratch);
        Set("GIT_SSH_COMMAND", $"ssh -F '{Slashed(config)}' -i '{Slashed(client)}' -o IdentitiesOnly=yes -o IdentityAgent=none -o BatchMode=yes "
            + $"-o UserKnownHostsFile='{Slashed(knownHosts)}' -o GlobalKnownHostsFile=none");
    }

    private async Task Git(params string[] arguments)
    {
        var (code, _, error) = await WorkingTree.GitAsync(Repo, arguments, CancellationToken.None);
        Assert.True(code == 0, error);
    }

    private async Task<(string? Failed, int Fetches)> FetchAsync()
    {
        var fetches = 0;
        WorkingTree.Started.Value = arguments => { if (arguments.Contains("fetch")) Interlocked.Increment(ref fetches); };
        try
        {
            var (failed, _) = await TreeRefs.FetchAsync(Repo, "main", CancellationToken.None, "engine", "work");
            return (failed, fetches);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }
    }

    [Fact]
    public async Task A_key_the_server_refuses_is_held_once_and_no_moment_connects_again_until_try_again()
    {
        if (Sshd() is not { } sshd) return;
        var (port, known) = await ServeAsync(sshd);
        await WireAsync(sshd, known);
        await Git("init", "-q", "-b", "main");
        await Git("remote", "add", "origin", $"ssh://git@127.0.0.1:{port}/org/engine.git");

        var first = await FetchAsync();
        await Task.Delay(500);
        var connected = Connections();

        Assert.Equal(1, first.Fetches);
        Assert.True(connected > 0, "the fetch never reached the server");
        Assert.Contains("Permission denied (publickey)", first.Failed, StringComparison.Ordinal);
        var held = Assert.Single(GitHolds.Read(Home));
        Assert.Equal(("refused", "all", $"127.0.0.1:{port}"), (held.Class, held.Holds, held.Host));

        for (var moment = 0; moment < 3; moment++) Assert.Equal(0, (await FetchAsync()).Fetches);
        await Task.Delay(500);
        Assert.Equal(connected, Connections());

        GitHolds.LiftRepository(Home, "engine", "work");
        Assert.Equal(1, (await FetchAsync()).Fetches);
        await Task.Delay(500);
        Assert.True(Connections() > connected);
    }

    [Fact]
    public async Task A_server_whose_key_changed_is_held_as_changed()
    {
        if (Sshd() is not { } sshd) return;
        var (port, _) = await ServeAsync(sshd);
        var other = await KeyAsync(sshd, "other");
        await WireAsync(sshd, $"[127.0.0.1]:{port} {string.Join(' ', File.ReadAllText(other + ".pub").Split(' ')[..2])}");
        await Git("init", "-q", "-b", "main");
        await Git("remote", "add", "origin", $"ssh://git@127.0.0.1:{port}/org/engine.git");

        var first = await FetchAsync();

        var held = Assert.Single(GitHolds.Read(Home));
        Assert.Equal("host-changed", held.Class);
        Assert.Equal("Host key verification failed.", held.Words);
        Assert.DoesNotContain(_root, first.Failed, StringComparison.OrdinalIgnoreCase);
    }
}
