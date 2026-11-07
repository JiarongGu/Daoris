using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A code host on this machine's loopback serving bare repositories over git's smart HTTP behind TLS: .NET's
/// <see cref="SslStream"/> with a certificate made at the class's start, each connection relayed in the clear to a
/// <see cref="GitHttpBackendHost"/> over the same repositories. git trusts the certificate only where a test hands it
/// <see cref="Authority"/> as <c>http.sslCAInfo</c> (<see cref="TrustedBy"/>), so no store of the machine's or the person's is touched.
/// </summary>
/// <remarks>
/// <para>Git for Windows (2.53 measured) carries Schannel and OpenSSL, its system file choosing one; the tests read no system file, and
/// Schannel reads <c>http.sslCAInfo</c> only with <c>http.schannelUseSSLCAInfo</c>, so <see cref="TrustedBy"/> names OpenSSL too.
/// Asks no sign-in until a test sets <see cref="Basic"/>, and serves pushes where a repository says <c>http.receivepack</c>: what refuses one is a door.</para>
/// <para>One per test class (<see cref="IClassFixture{TFixture}"/>); each test serves its own repository by a path of its own.</para>
/// </remarks>
public sealed class GitTlsHost : IAsyncLifetime
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private GitHttpBackendHost? _plain;
    private X509Certificate2? _certificate;
    private int _plainPort;
    private Task? _serving;

    /// <summary>Its own folder under the repository's <c>_fixtures/</c>, removed with it.</summary>
    public string Root { get; } = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-tls-host", Guid.NewGuid().ToString("N")[..8]);

    /// <summary>The folder whose bare repositories are served, each by its path under it.</summary>
    public string Projects => Path.Combine(Root, "srv");

    /// <summary>The host's certificate as PEM, the one authority a test's git trusts it by.</summary>
    public string Authority => Path.Combine(Root, "authority.pem");

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>The address a repository under <see cref="Projects"/> is reached at.</summary>
    public string Address(string path) => $"https://127.0.0.1:{Port}/{path.TrimStart('/')}";

    /// <summary>Every request line the backend answered, in order.</summary>
    public IReadOnlyList<string> Requests => _plain?.Requests ?? [];

    /// <summary>The user each request authenticated as (<c>-</c> for none), in order; the token itself is never kept.</summary>
    public IReadOnlyList<string> Users => _plain?.Users ?? [];

    /// <summary>
    /// Ask HTTP Basic with this user and token before anything is served, or ask none (null). A class shares one host, so each test
    /// sets what it needs and clears it at its end.
    /// </summary>
    public (string User, string Token)? Basic
    {
        set => _plain!.Require(value);
    }

    /// <summary>
    /// <paramref name="info"/>'s git told to trust this host's certificate and no other authority, through git's own configuration
    /// variables, so the trust is that command's alone.
    /// </summary>
    public ProcessStartInfo TrustedBy(ProcessStartInfo info)
    {
        string[][] pairs = [["http.sslBackend", "openssl"], ["http.sslCAInfo", Authority.Replace('\\', '/')]];
        var at = info.Environment.TryGetValue("GIT_CONFIG_COUNT", out var said) && int.TryParse(said, out var count) ? count : 0;
        foreach (var pair in pairs)
        {
            info.Environment[$"GIT_CONFIG_KEY_{at}"] = pair[0];
            info.Environment[$"GIT_CONFIG_VALUE_{at}"] = pair[1];
            at++;
        }

        info.Environment["GIT_CONFIG_COUNT"] = at.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return info;
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Projects);
        _certificate = Certificate(Authority);
        _plain = new GitHttpBackendHost(await BackendAsync(), Projects);
        _plainPort = new Uri(_plain.Address("")).Port;
        _listener.Start();
        _serving = ServeAsync();
    }

    /// <summary>A self-signed certificate for the loopback address, its PEM written to <paramref name="pem"/>.</summary>
    private static X509Certificate2 Certificate(string pem)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, false));
        using var made = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllText(pem, made.ExportCertificatePem() + "\n");
        // Schannel serves a key it can open from a store, not an ephemeral one: the certificate is read back with its key.
        return X509CertificateLoader.LoadPkcs12(made.Export(X509ContentType.Pkcs12), null);
    }

    /// <summary>The test's git's own <c>git-http-backend</c>.</summary>
    private async Task<string> BackendAsync()
    {
        var git = Tools.Resolve(Root, "git").File ?? throw new InvalidOperationException("no git on this machine's PATH");
        var info = new ProcessStartInfo(git, "--exec-path") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        using var process = Process.Start(info)!;
        var folder = (await process.StandardOutput.ReadToEndAsync()).Trim();
        await process.WaitForExitAsync();
        return Path.Combine(folder, OperatingSystem.IsWindows() ? "git-http-backend.exe" : "git-http-backend");
    }

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = Task.Run(() => RelayAsync(client));
        }
    }

    /// <summary>One connection: TLS answered here, its plain bytes carried to the backend and its answer carried back until it closes.</summary>
    private async Task RelayAsync(TcpClient client)
    {
        using var _ = client;
        try
        {
            await using var tls = new SslStream(client.GetStream());
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = _certificate }, _stop.Token);
            using var backend = new TcpClient();
            await backend.ConnectAsync(IPAddress.Loopback, _plainPort, _stop.Token);
            var plain = backend.GetStream();
            var asked = tls.CopyToAsync(plain, _stop.Token);
            await plain.CopyToAsync(tls, _stop.Token);
            await tls.ShutdownAsync();
            backend.Close();
            try { await asked; } catch (Exception error) when (error is IOException or ObjectDisposedException or OperationCanceledException) { }
        }
        catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException or AuthenticationException or OperationCanceledException)
        {
            // The client went first, or did not trust the certificate; the next connection is its own.
        }
    }

    public async Task DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        if (_serving is not null) await _serving;
        if (_plain is not null) await _plain.DisposeAsync();
        _certificate?.Dispose();
        _stop.Dispose();
        GitSeed.Discard(Root);
    }
}
