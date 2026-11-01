using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The git-auth design §14 row 3's proof against a real OpenSSH server (GITAUTH1c, D198 point 4): an SSH identity Daoris generated, its
/// host key trusted at a test's press, a fetch and a push through Daoris's built ssh door, and a fetch in another repository by
/// <c>git -C</c>; Windows' OpenSSH, chosen as the ssh, reading the key under its protected folder; and under <c>own</c>, a repository's
/// <c>BatchMode=no</c> losing to the door's first value.
/// </summary>
/// <remarks>
/// <para><b>Its server</b> is the <c>sshd.exe</c> <c>DAORIS_TEST_SSHD</c> names (Win32-OpenSSH's portable build serves the loopback as
/// an ordinary account), on a free loopback port with a host key made here, every session forced to one script that serves the one bare
/// repository whatever path git asks, since the server's shell is <c>cmd</c>, which would keep git's quotes. Where the variable names
/// none, each case is skipped and says why.</para>
/// <para>Every git is started as Daoris starts its own, then kept from the person's configuration: no system file, an empty global file
/// and a scratch home. Every key is generated here, under the repository's <c>_fixtures/</c>, and removed with it; no key, agent or
/// known-hosts file of the person's is read.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class GitSshdProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-sshd", Guid.NewGuid().ToString("N")[..8]);

    private Process? _server;

    private string Home => Path.Combine(_root, "data");

    private string Server => Path.Combine(_root, "server");

    private string Bare => Path.Combine(_root, "srv", "engine.git");

    private string Work => Path.Combine(_root, "work");

    private int _port;

    private string Address => $"ssh://git@127.0.0.1:{_port}/org/engine.git";

    private string Host => $"127.0.0.1:{_port}";

    public GitSshdProcessTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Server);
        Directory.CreateDirectory(Path.Combine(_root, "scratch-home"));
        File.WriteAllText(Path.Combine(_root, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        File.WriteAllText(Path.Combine(_root, "global.gitconfig"), "[user]\n\tname = Fixture\n\temail = fixture@example.invalid\n");
    }

    public void Dispose()
    {
        try { _server?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        _server?.Dispose();
        GitSeed.Discard(_root);
    }

    // ── The server ─────────────────────────────────────────────────────────────────────────────────

    private static string Sshd => SshdFactAttribute.Named()!;

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task<(int Exit, string Out, string Err)> RunAsync(ProcessStartInfo info, TimeSpan? bound = null)
    {
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.RedirectStandardInput = true;
        using var process = Process.Start(info)!;
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var stop = new CancellationTokenSource(bound ?? TimeSpan.FromSeconds(90));
        try
        {
            await process.WaitForExitAsync(stop.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{Path.GetFileName(info.FileName)} {string.Join(' ', info.ArgumentList)} did not end within its bound");
        }

        return (process.ExitCode, await output, await error);
    }

    /// <summary>
    /// The server on a free port: its host key made here, the identity's public key its one authorized key, and every session forced to
    /// a script serving <see cref="Bare"/>. The host key's fingerprint, for the test's press.
    /// </summary>
    private async Task<string> ServeAsync(string authorized)
    {
        var hostKey = Path.Combine(Server, "host");
        var (made, _, why) = await RunAsync(Info(Path.Combine(Path.GetDirectoryName(Sshd)!, "ssh-keygen.exe"), "-q", "-t", "ed25519", "-N", "", "-f", hostKey));
        Assert.True(made == 0, why);
        // OpenSSH refuses a host key others can read: this account alone.
        Assert.Equal(0, (await RunAsync(Info("icacls", hostKey, "/inheritance:r", "/grant:r", $"{Environment.UserName}:F"))).Exit);
        File.WriteAllText(Path.Combine(Server, "authorized_keys"), authorized);

        var git = Tools.Resolve(Home, "git").File ?? throw new InvalidOperationException("no git on this machine's PATH");
        var serve = Path.Combine(Server, "serve.cmd");
        File.WriteAllText(serve,
            "@echo off\r\n"
            + "set \"ASKED=%SSH_ORIGINAL_COMMAND%\"\r\n"
            + "if \"%ASKED:~0,15%\"==\"git-upload-pack\" goto upload\r\n"
            + "if \"%ASKED:~0,16%\"==\"git-receive-pack\" goto receive\r\n"
            + "echo this fixture serves git alone 1>&2\r\n"
            + "exit /b 1\r\n"
            + $":upload\r\n\"{git}\" upload-pack \"{Bare}\"\r\nexit /b %errorlevel%\r\n"
            + $":receive\r\n\"{git}\" receive-pack \"{Bare}\"\r\nexit /b %errorlevel%\r\n");

        var config = Path.Combine(Server, "sshd_config");
        File.WriteAllText(config,
            $"ListenAddress 127.0.0.1\nPort {_port}\nHostKey {Slashed(hostKey)}\nAuthorizedKeysFile {Slashed(Path.Combine(Server, "authorized_keys"))}\n"
            + "PasswordAuthentication no\nKbdInteractiveAuthentication no\nPubkeyAuthentication yes\nStrictModes no\nPidFile none\nLogLevel VERBOSE\n"
            + $"ForceCommand \"{serve}\"\n");
        var info = Info(Sshd, "-D", "-e", "-f", config);
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardError = true;
        info.RedirectStandardOutput = true;
        _server = Process.Start(info)!;
        var listening = new TaskCompletionSource();
        _server.ErrorDataReceived += (_, line) =>
        {
            if (line.Data?.Contains("Server listening", StringComparison.Ordinal) == true) listening.TrySetResult();
        };
        _server.BeginErrorReadLine();
        await listening.Task.WaitAsync(Poll.Patience);
        return GitKeys.Describe(File.ReadAllText(hostKey + ".pub").Trim())!.Value.Fingerprint;
    }

    private static ProcessStartInfo Info(string file, params string[] arguments)
    {
        var info = new ProcessStartInfo(file);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return info;
    }

    private static string Slashed(string path) => path.Replace('\\', '/');

    // ── Daoris's side ──────────────────────────────────────────────────────────────────────────────

    /// <summary>The doors' build beside this test's own, laid out as the install's <c>app/bin/</c>: one program, two names.</summary>
    private void LayDoors()
    {
        var build = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var desktop = build.Parent!.Parent!.Parent!.Parent!.FullName;
        var built = Path.Combine(desktop, "Daoris.Desktop.GitDoor", "bin", build.Parent.Name, build.Name);
        Assert.True(File.Exists(Path.Combine(built, GitWiring.SshDoor)), "the doors are built before these tests: " + built);
        var bin = Path.Combine([_root, .. Tools.InstallBinLayout]);
        Directory.CreateDirectory(bin);
        foreach (var file in Directory.EnumerateFiles(built)) File.Copy(file, Path.Combine(bin, Path.GetFileName(file)), overwrite: true);
        File.Copy(Path.Combine(built, GitWiring.SshDoor), Path.Combine(bin, GitWiring.RemoteHelper), overwrite: true);
    }

    /// <summary>A git started as Daoris starts its own, kept from the person's configuration and home.</summary>
    private ProcessStartInfo Git(string folder, params string[] arguments)
    {
        var (info, refusal) = WorkingTree.GitStart(folder, arguments, Home);
        Assert.Null(refusal);
        var scratch = Path.Combine(_root, "scratch-home");
        info!.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        info.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(_root, "global.gitconfig");
        info.Environment["HOME"] = scratch;
        info.Environment["USERPROFILE"] = scratch;
        info.Environment["XDG_CONFIG_HOME"] = scratch;
        info.Environment.Remove(GitRouting.GrantVariable);
        return TestGit.Ceiling(info);
    }

    private async Task<string> Ok(ProcessStartInfo info)
    {
        var (exit, output, error) = await RunAsync(info);
        Assert.True(exit == 0, $"git {string.Join(' ', info.ArgumentList)}: {error}");
        return output.Trim();
    }

    /// <summary>
    /// The bare repository with one commit, the identity generated as <c>git identity add-ssh</c> makes it, the server, and the host's
    /// key trusted as <c>git identity test --trust-host</c> trusts it.
    /// </summary>
    private async Task<GitIdentity> ReadyAsync()
    {
        _port = FreePort();
        Directory.CreateDirectory(Bare);
        await Ok(Git(Bare, "init", "--bare", "--initial-branch=main"));
        var seed = Path.Combine(_root, "seed");
        Directory.CreateDirectory(seed);
        await Ok(Git(seed, "init", "--initial-branch=main"));
        File.WriteAllText(Path.Combine(seed, "a.txt"), "a\n");
        await Ok(Git(seed, "add", "a.txt"));
        await Ok(Git(seed, "commit", "-m", "first"));
        await Ok(Git(seed, "push", Bare, "main"));

        var identity = await GitIdentityActs.AddSshAsync(Home, Host, "Fixture", null, null, null, rsa: false, CancellationToken.None);
        Assert.True(GitKeys.IsPrivate(GitKeys.Folder(Home, identity.Id)));
        var fingerprint = await ServeAsync(GitKeys.PublicKey(Home, identity.Id)! + "\n");
        var tested = await GitIdentityActs.TestAsync(Home, identity.Id, fingerprint, CancellationToken.None);
        Assert.True(tested.Result == "reached", $"{tested.Result}: {tested.Words}");
        LayDoors();
        return identity;
    }

    private Task<string> TipAsync() => Ok(Git(Bare, "rev-parse", "refs/heads/main"));

    // ── The cases ──────────────────────────────────────────────────────────────────────────────────

    [SshdFact]
    public async Task An_identity_fetches_and_pushes_through_the_door_and_fetches_in_another_repository_by_git_C()
    {
        var identity = await ReadyAsync();

        await Ok(Git(_root, "clone", Address, Work));
        Assert.Equal(await TipAsync(), await Ok(Git(Work, "rev-parse", "HEAD")));
        Assert.NotNull(GitSshFiles.LastUsed(Home, identity.Id));

        File.AppendAllText(Path.Combine(Work, "a.txt"), "landed\n");
        await Ok(Git(Work, "commit", "-am", "landed"));
        var push = Git(Work, "push", "origin", "main");
        push.Environment[GitRouting.GrantVariable] = GitGrants.Write(Home, Address, DateTimeOffset.UtcNow);
        await Ok(push);
        Assert.Equal(await Ok(Git(Work, "rev-parse", "HEAD")), await TipAsync());

        var other = Path.Combine(_root, "other");
        await Ok(Git(_root, "clone", Address, other));
        await Ok(Git(_root, "-C", other, "fetch", "origin"));
        Assert.Equal(await TipAsync(), await Ok(Git(other, "rev-parse", "refs/remotes/origin/main")));
    }

    [SshdFact]
    public async Task Windows_OpenSSH_chosen_as_the_ssh_reads_the_key_under_its_protected_folder()
    {
        var windows = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe");
        var ssh = File.Exists(windows) ? windows : Path.Combine(Path.GetDirectoryName(Sshd)!, "ssh.exe");
        Tools.SetGit(Home, "core.sshCommand", ssh);
        await ReadyAsync();

        await Ok(Git(_root, "clone", Address, Work));

        Assert.Equal(await TipAsync(), await Ok(Git(Work, "rev-parse", "HEAD")));
    }

    [SshdFact]
    public async Task Under_own_a_repositorys_BatchMode_no_loses_to_the_doors_first_value()
    {
        var identity = await ReadyAsync();
        await Ok(Git(_root, "clone", Address, Work));
        GitIdentityActs.UseMachine(Home, Host, own: true);
        var empty = Path.Combine(_root, "empty");
        File.WriteAllText(empty, "");
        // Its own command knows no host key and asks: with BatchMode=no ssh would wait on the console for a yes, so an answer at once,
        // refused, is BatchMode=yes standing first.
        await Ok(Git(Work, "config", "core.sshCommand",
            $"ssh -o BatchMode=no -o StrictHostKeyChecking=ask -F '{Slashed(empty)}' -i '{Slashed(GitKeys.KeyPath(Home, identity.Id))}' "
            + $"-o IdentitiesOnly=yes -o IdentityAgent=none -o UserKnownHostsFile='{Slashed(empty)}' -o GlobalKnownHostsFile=none"));

        var (exit, _, error) = await RunAsync(Git(Work, "fetch", "origin"), TimeSpan.FromSeconds(60));

        Assert.NotEqual(0, exit);
        Assert.Contains("Host key verification failed", error, StringComparison.Ordinal);
    }
}

/// <summary>A fact that runs where <c>DAORIS_TEST_SSHD</c> names an OpenSSH <c>sshd.exe</c> on Windows, and is skipped saying so elsewhere.</summary>
public sealed class SshdFactAttribute : FactAttribute
{
    public SshdFactAttribute()
    {
        if (Named() is null)
            Skip = "DAORIS_TEST_SSHD names no sshd.exe here (Win32-OpenSSH's portable build on Windows serves these), so no server runs";
    }

    /// <summary>The sshd the variable names, where it is a file on Windows; else null.</summary>
    public static string? Named() =>
        Environment.GetEnvironmentVariable("DAORIS_TEST_SSHD") is { Length: > 0 } named && File.Exists(named) && OperatingSystem.IsWindows() ? named : null;
}
