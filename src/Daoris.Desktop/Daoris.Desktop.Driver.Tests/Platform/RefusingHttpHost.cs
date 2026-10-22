using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A code host on this machine's loopback that refuses, for the git-auth measurements (GITAUTH1a): every request is answered
/// with one status and the connection closed, and each request is counted. A raw socket rather than <c>HttpListener</c>,
/// which asks Windows for a reservation an ordinary account may not hold. Never a real host, never a credential of anyone's.
/// </summary>
internal sealed class RefusingHttpHost : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serving;
    private readonly Func<string, int> _status;
    private int _requests;

    /// <param name="status">The status a request line is answered with: 401 asks for a sign-in, 403 refuses one.</param>
    public RefusingHttpHost(Func<string, int> status)
    {
        _status = status;
        _listener.Start();
        _serving = ServeAsync();
    }

    /// <summary>The address of a repository on it.</summary>
    public string Address => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/org/engine.git";

    /// <summary>How many requests it has answered.</summary>
    public int Requests => Volatile.Read(ref _requests);

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (Exception) when (_stop.IsCancellationRequested)
            {
                return;
            }

            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    var line = await reader.ReadLineAsync(_stop.Token).ConfigureAwait(false) ?? "";
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync(_stop.Token).ConfigureAwait(false))) { }
                    Interlocked.Increment(ref _requests);
                    var status = _status(line);
                    var reason = status switch { 401 => "Unauthorized", 403 => "Forbidden", _ => "Status" };
                    var answer = $"HTTP/1.1 {status} {reason}\r\nWWW-Authenticate: Basic realm=\"fixture\"\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(answer), _stop.Token).ConfigureAwait(false);
                }
                catch (Exception) when (_stop.IsCancellationRequested)
                {
                    return;
                }
                catch (IOException)
                {
                    // git hung up first: nothing to answer.
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        try { await _serving.ConfigureAwait(false); } catch (OperationCanceledException) { }
        _stop.Dispose();
    }
}
