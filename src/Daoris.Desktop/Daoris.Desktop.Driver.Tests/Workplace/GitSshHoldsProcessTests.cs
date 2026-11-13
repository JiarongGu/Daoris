using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The proof over SSH: Daoris's fetch of the line against an OpenSSH server on the loopback
/// that refuses the key is held once, and no moment after it connects to the server again until <i>Try again</i>; a server whose key
/// changed is held as <c>host-changed</c>. Counted by the server's own log of each connection.
/// </summary>
/// <remarks>
/// <para><b>Its server</b> is <see cref="GitSshHost"/>, in this process on a free loopback port, which admits no key of these tests'.
/// The client's key is made by the <c>ssh-keygen</c> beside the ssh an identity uses, limited to this account as OpenSSH requires.</para>
/// <para>Every git and ssh here is kept from the person's own: no system or global git configuration, a scratch home, and an ssh
/// handed an empty configuration, a key of its own with <c>IdentitiesOnly</c>, no agent and a known-hosts file of the test's.</para>
/// <para>Alone: it sets the process's environment (the home <c>TreeRefs</c> resolves, the git variables above).</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Needs, Needed.Network)]
[Collection(Alone.Name)]
public sealed class GitSshHoldsProcessTests : IClassFixture<GitSshHost>, IDisposable
{
    private readonly GitSshHost _host;

    private readonly string _root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "git-ssh-holds", Guid.NewGuid().ToString("N")[..8]);

    private readonly Dictionary<string, string?> _was = [];

    private string Home => Path.Combine(_root, "home");

    private string Repo => Path.Combine(_root, "engine");

    private string Keys => Path.Combine(_root, "keys");

    public GitSshHoldsProcessTests(GitSshHost host)
    {
        _host = host;
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
        foreach (var (name, value) in _was) Environment.SetEnvironmentVariable(name, value);
        GitSeed.Discard(_root);
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

    /// <summary>A client key no host admits, made by the <c>ssh-keygen</c> beside the ssh an identity uses.</summary>
    private async Task<string> KeyAsync(string name)
    {
        var file = Path.Combine(Keys, name);
        var keygen = Path.Combine(GitIdentityActs.SshTools(Home), OperatingSystem.IsWindows() ? "ssh-keygen.exe" : "ssh-keygen");
        // RSA: the stand-in host verifies RSA and ECDSA keys, and an ed25519 key's offer ends the connection unanswered.
        await Run(keygen, "-q", "-t", "rsa", "-b", "2048", "-N", "", "-C", "fixture-" + name, "-f", file);
        // OpenSSH refuses a private key others can read: this account alone.
        await Run("icacls", file, "/inheritance:r", "/grant:r", $"{Environment.UserName}:F");
        return file;
    }

    /// <summary>The known-hosts line for the stand-in host under <paramref name="key"/>, its own unless another is named.</summary>
    private string Known(string? key = null) => $"[127.0.0.1]:{_host.Port} {key ?? _host.HostKey}";

    private string Address => _host.Address("org/engine.git");

    /// <summary>The process's git kept from the person's, and its ssh handed the test's own key, configuration and known hosts.</summary>
    private async Task WireAsync(string known)
    {
        var client = await KeyAsync("client");
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
        GitFlow.Started.Value = arguments => { if (arguments.Contains("fetch")) Interlocked.Increment(ref fetches); };
        try
        {
            var (failed, _) = await TreeRefs.FetchAsync(Home, Repo, "main", CancellationToken.None, "engine", "work");
            return (failed, fetches);
        }
        finally
        {
            GitFlow.Started.Value = null;
        }
    }

    [Fact]
    public async Task A_key_the_server_refuses_is_held_once_and_no_moment_connects_again_until_try_again()
    {
        var before = _host.Connections;
        await WireAsync(Known());
        await Git("init", "-q", "-b", "main");
        await Git("remote", "add", "origin", Address);

        var first = await FetchAsync();
        await Task.Delay(500);
        var connected = _host.Connections;

        Assert.Equal(1, first.Fetches);
        Assert.True(connected > before, "the fetch never reached the server");
        Assert.True(first.Failed?.Contains("Permission denied (publickey)", StringComparison.Ordinal) == true, first.Failed);
        var held = Assert.Single(GitHolds.Read(Home));
        Assert.Equal(("refused", "all", _host.Host), (held.Class, held.Holds, held.Host));

        for (var moment = 0; moment < 3; moment++) Assert.Equal(0, (await FetchAsync()).Fetches);
        await Task.Delay(500);
        Assert.Equal(connected, _host.Connections);

        GitHolds.LiftRepository(Home, "engine", "work");
        Assert.Equal(1, (await FetchAsync()).Fetches);
        await Task.Delay(500);
        Assert.True(_host.Connections > connected);
    }

    [Fact]
    public async Task A_server_whose_key_changed_is_held_as_changed()
    {
        await WireAsync(Known(GitSshHost.OtherKey()));
        await Git("init", "-q", "-b", "main");
        await Git("remote", "add", "origin", Address);

        var first = await FetchAsync();

        var held = Assert.Single(GitHolds.Read(Home));
        Assert.Equal("host-changed", held.Class);
        Assert.Equal("Host key verification failed.", held.Words);
        Assert.DoesNotContain(_root, first.Failed, StringComparison.OrdinalIgnoreCase);
    }
}
