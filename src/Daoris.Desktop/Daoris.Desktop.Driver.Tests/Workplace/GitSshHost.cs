using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using Daoris.Driver;
using Microsoft.DevTunnels.Ssh;
using Microsoft.DevTunnels.Ssh.Algorithms;
using Microsoft.DevTunnels.Ssh.Events;
using Microsoft.DevTunnels.Ssh.Messages;
using Microsoft.DevTunnels.Ssh.Tcp;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A code host on this machine's loopback serving bare repositories over SSH, in the test process: Microsoft's dev-tunnels
/// SSH library answers OpenSSH's client, so no sshd is installed or started. It presents a host key made here, admits only the public
/// keys a test authorizes, and serves <c>exec</c> of <c>git-upload-pack</c> and <c>git-receive-pack</c> with the test's git over the
/// repositories under <see cref="Projects"/>, by the path git asks. A shell answers a greeting and exits 1, as a hosted service's does.
/// </summary>
/// <remarks>
/// <para>Measured with Git for Windows' OpenSSH 10.2 (the ssh the door finds on git's path) and Windows' 9.5: it signs host keys and
/// verifies client keys as RSA (SHA-2) or ECDSA, never ed25519, whose offer ends the connection unanswered rather than refused; so a key
/// offered here is made as RSA. Its key exchange is ECDH over the NIST curves, which both still offer.</para>
/// <para>One per test class (<see cref="IClassFixture{TFixture}"/>): each test authorizes its own identity's key and serves its own
/// repositories by a path of its own. Every connection, key offered and command is counted, so a test can say a held remote did not
/// connect again and which ssh the door found.</para>
/// </remarks>
public sealed class GitSshHost : IAsyncLifetime
{
    private readonly IKeyPair _hostKey = SshAlgorithms.PublicKey.ECDsaSha2Nistp256.GenerateKeyPair();
    private readonly HashSet<string> _authorized = new(StringComparer.Ordinal);
    private readonly List<GitSshHostOffer> _offers = [];
    private readonly List<GitSshHostRun> _runs = [];
    private readonly Listening _listening = new();
    private SshServer? _server;
    private Task? _accepting;
    private int _connections;
    private string? _git;

    /// <summary>Its own folder under the repository's <c>_fixtures/</c>, removed with it.</summary>
    public string Root { get; } = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-ssh-host", Guid.NewGuid().ToString("N")[..8]);

    /// <summary>The folder whose bare repositories are served, each by its path under it.</summary>
    public string Projects => Path.Combine(Root, "srv");

    public int Port => ((IPEndPoint)_listening.Listener!.LocalEndpoint).Port;

    /// <summary>The host as an identity names it, with its port.</summary>
    public string Host => $"127.0.0.1:{Port}";

    /// <summary>The address a repository under <see cref="Projects"/> is reached at.</summary>
    public string Address(string path) => $"ssh://git@127.0.0.1:{Port}/{path.TrimStart('/')}";

    /// <summary>The host key as a public key line (<c>ecdsa-sha2-nistp256 …</c>), for a known-hosts file or a fingerprint.</summary>
    public string HostKey => KeyLine(_hostKey);

    /// <summary>Every connection accepted, an <c>ssh-keyscan</c>'s included.</summary>
    public int Connections => Volatile.Read(ref _connections);

    /// <summary>
    /// Whether a <c>git-receive-pack</c> is cut off mid-way: the host lists its refs, takes the pack and applies it, then drops the
    /// connection with no report and no exit status, so a client cannot know the host took the push. Recorded as exit <see cref="InterruptedExit"/>.
    /// </summary>
    public bool InterruptReceive { get; set; }

    /// <summary>The exit a run cut off by <see cref="InterruptReceive"/> is recorded with.</summary>
    public const int InterruptedExit = -1;

    /// <summary>Every public key a client offered, in order, with whether the host admitted it.</summary>
    public IReadOnlyList<GitSshHostOffer> Offers
    {
        get { lock (_offers) return [.. _offers]; }
    }

    /// <summary>Every command a client ran, in order, with the client's version and the exit.</summary>
    public IReadOnlyList<GitSshHostRun> Runs
    {
        get { lock (_runs) return [.. _runs]; }
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Projects);
        var empty = Path.Combine(Root, "empty.gitconfig");
        File.WriteAllText(empty, "");
        _git = Tools.Resolve(Root, "git").File ?? throw new InvalidOperationException("no git on this machine's PATH");

        var config = new SshSessionConfiguration();
        config.AuthenticationMethods.Clear();
        config.AuthenticationMethods.Add(AuthenticationMethods.PublicKey);
        _server = new SshServer(config, new TraceSource(nameof(GitSshHost)))
        {
            Credentials = new SshServerCredentials(_hostKey),
            TcpListenerFactory = _listening,
        };
        _server.SessionOpened += (_, session) =>
        {
            Interlocked.Increment(ref _connections);
            session.Authenticating += (_, asked) => Authenticate(asked);
            session.ChannelOpening += (_, opening) => Open(opening.Channel, empty);
        };
        _accepting = _server.AcceptSessionsAsync(0, IPAddress.Loopback);
        await _listening.Ready.Task.WaitAsync(Poll.Patience);
    }

    /// <summary>Admit a public key line (<c>ssh-rsa …</c> or <c>ecdsa-sha2-…</c>); ed25519 is not served, and says so.</summary>
    public void Authorize(string publicKey)
    {
        lock (_authorized) _authorized.Add(Blob(publicKey));
    }

    /// <summary>A fresh public key line no client holds, for a known-hosts file that names another key than the host's.</summary>
    public static string OtherKey() => KeyLine(SshAlgorithms.PublicKey.ECDsaSha2Nistp256.GenerateKeyPair());

    private static string KeyLine(IKeyPair key) => key.KeyAlgorithmName + " " + key.GetPublicKeyBytes(key.KeyAlgorithmName).ToBase64();

    /// <summary>A public key line's key, read back through the library so an offered key compares by the same bytes.</summary>
    private static string Blob(string publicKey)
    {
        var words = publicKey.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var algorithm = words[0] switch
        {
            "ssh-rsa" => SshAlgorithms.PublicKey.RsaWithSha512,
            "ecdsa-sha2-nistp256" => SshAlgorithms.PublicKey.ECDsaSha2Nistp256,
            "ecdsa-sha2-nistp384" => SshAlgorithms.PublicKey.ECDsaSha2Nistp384,
            "ecdsa-sha2-nistp521" => SshAlgorithms.PublicKey.ECDsaSha2Nistp521,
            _ => throw new NotSupportedException($"the stand-in SSH host verifies RSA and ECDSA keys, not {words[0]}"),
        };
        var key = algorithm!.CreateKeyPair();
        key.SetPublicKeyBytes(Microsoft.DevTunnels.Ssh.Buffer.FromBase64(words[1]));
        return Blob(key);
    }

    private static string Blob(IKeyPair key) => key.GetPublicKeyBytes(key.KeyAlgorithmName).ToBase64();

    private void Authenticate(SshAuthenticatingEventArgs asked)
    {
        if (asked.AuthenticationType is not (SshAuthenticationType.ClientPublicKey or SshAuthenticationType.ClientPublicKeyQuery)) return;
        if (asked.PublicKey is null) return;
        var blob = Blob(asked.PublicKey);
        bool admitted;
        lock (_authorized) admitted = _authorized.Contains(blob);
        lock (_offers) _offers.Add(new GitSshHostOffer(asked.Username, asked.PublicKey.KeyAlgorithmName, admitted));
        if (admitted) asked.AuthenticationTask = Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(new ClaimsIdentity("publickey")));
    }

    /// <summary>A session channel: its stream read from the start, and its one <c>exec</c> or <c>shell</c> served once answered.</summary>
    private void Open(SshChannel channel, string emptyConfig)
    {
        var stream = new SshStream(channel);
        channel.Request += (_, asked) =>
        {
            if (asked.RequestType is not (ChannelRequestTypes.Command or ChannelRequestTypes.Shell)) return;
            var command = asked.RequestType == ChannelRequestTypes.Command ? asked.Request.ConvertTo<CommandRequestMessage>().Command : null;
            asked.IsAuthorized = true;
            asked.ResponseContinuation = answered =>
            {
                _ = Task.Run(() => ServeAsync(channel, stream, command, emptyConfig));
                return Task.CompletedTask;
            };
        };
    }

    private async Task ServeAsync(SshChannel channel, SshStream stream, string? command, string emptyConfig)
    {
        var client = channel.Session.RemoteVersion?.ToString() ?? "";
        var exit = 1;
        try
        {
            var (program, folder) = Parse(command);
            if (program is null || folder is null || !Directory.Exists(folder))
            {
                var words = command is null
                    ? "Hi fixture! You've successfully authenticated, but this stand-in host serves git alone.\n"
                    : $"this stand-in host serves git-upload-pack and git-receive-pack of a repository it holds, not: {command}\n";
                await stream.WriteAsync(Encoding.UTF8.GetBytes(words));
                exit = command is null ? 1 : 128;
                return;
            }

            var interrupt = InterruptReceive && program == "git-receive-pack";
            exit = await RunGitAsync(stream, program, folder, emptyConfig, interrupt);
            if (interrupt)
            {
                exit = InterruptedExit;
                await channel.Session.CloseAsync(SshDisconnectReason.ConnectionLost, "interrupted");
            }
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or SshChannelException or SshConnectionException)
        {
            // The client went first.
        }
        finally
        {
            lock (_runs) _runs.Add(new GitSshHostRun(client, command, exit));
            try { await channel.CloseAsync((uint)exit); } catch (Exception error) when (error is ObjectDisposedException or SshConnectionException) { }
        }
    }

    /// <summary>git's own program on the repository, its input the channel's until the client's end, its output the channel's.</summary>
    private async Task<int> RunGitAsync(SshStream stream, string program, string folder, string emptyConfig, bool interrupt)
    {
        var info = new ProcessStartInfo(_git!)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add(program["git-".Length..]);
        info.ArgumentList.Add(folder);
        info.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        info.Environment["GIT_CONFIG_GLOBAL"] = emptyConfig;
        using var git = Process.Start(info)!;
        var errors = git.StandardError.ReadToEndAsync();
        var input = Task.Run(async () =>
        {
            try
            {
                await stream.CopyToAsync(git.StandardInput.BaseStream);
            }
            catch (Exception error) when (error is IOException or ObjectDisposedException or SshChannelException or SshConnectionException)
            {
                // The channel closed under the copy.
            }
            finally
            {
                try { git.StandardInput.Close(); } catch (IOException) { }
            }
        });
        if (interrupt) await ForwardAdvertisementOnlyAsync(git.StandardOutput.BaseStream, stream);
        else await git.StandardOutput.BaseStream.CopyToAsync(stream);
        await stream.FlushAsync();
        await git.WaitForExitAsync();
        await errors;
        return git.ExitCode;
    }

    /// <summary>
    /// What <c>git-receive-pack</c> says is sent on until the ref advertisement's flush-pkt (<c>0000</c>) has gone; everything after it, the
    /// report of what the host took, is read and dropped, so the client has sent its pack and hears nothing back.
    /// </summary>
    private static async Task ForwardAdvertisementOnlyAsync(Stream from, SshStream to)
    {
        var buffer = new byte[8192];
        var advertised = false;
        int read;
        while ((read = await from.ReadAsync(buffer)) > 0)
        {
            if (advertised) continue;
            await to.WriteAsync(buffer.AsMemory(0, read));
            advertised = read >= 4 && buffer.AsSpan(read - 4, 4).SequenceEqual("0000"u8);
        }
    }

    /// <summary>
    /// git's program and the repository's folder from what ssh is asked to run (<c>git-upload-pack '/org/engine.git'</c>): only the two
    /// programs, only a path under <see cref="Projects"/>.
    /// </summary>
    private (string? Program, string? Folder) Parse(string? command)
    {
        if (command is null) return (null, null);
        var space = command.IndexOf(' ');
        if (space < 0) return (null, null);
        var program = command[..space];
        if (program is not ("git-upload-pack" or "git-receive-pack")) return (null, null);
        var path = command[(space + 1)..].Trim().Trim('\'').Replace("'\\''", "'").TrimStart('/');
        if (path.Length == 0 || path.Split('/', '\\').Any(part => part is ".." or "")) return (program, null);
        return (program, Path.Combine(Projects, path.Replace('/', Path.DirectorySeparatorChar)));
    }

    public async Task DisposeAsync()
    {
        _server?.Dispose();
        _listening.Listener?.Stop();
        if (_accepting is not null)
        {
            try { await _accepting.WaitAsync(Poll.Patience); }
            catch (Exception error) when (error is ObjectDisposedException or SocketException or OperationCanceledException or TimeoutException) { }
        }

        GitSeed.Discard(Root);
    }

    /// <summary>The library's listener, made here on loopback's next free port so the port is known once it listens.</summary>
    private sealed class Listening : ITcpListenerFactory
    {
        public TcpListener? Listener { get; private set; }

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<TcpListener> CreateTcpListenerAsync(
            int? remotePort, IPAddress localIPAddress, int localPort, bool canChangeLocalPort, TraceSource trace, CancellationToken cancellation)
        {
            Listener = new TcpListener(IPAddress.Loopback, 0);
            Listener.Start();
            Ready.TrySetResult();
            return Task.FromResult(Listener);
        }
    }
}

/// <summary>A public key a client offered the stand-in SSH host: as which user, of which algorithm, and whether the host admitted it.</summary>
public sealed record GitSshHostOffer(string? User, string Algorithm, bool Admitted);

/// <summary>A command run on the stand-in SSH host: the client's version line (which ssh connected), what it ran (null for a shell) and its exit.</summary>
public sealed record GitSshHostRun(string Client, string? Command, int Exit);
