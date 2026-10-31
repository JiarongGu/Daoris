using System.Net;
using System.Net.Sockets;

namespace Daoris.Desktop;

/// <summary>
/// The endpoint a session's browser server is handed (BROWSERSTART1): a loopback port the shell holds for its life, which
/// brings the browser up at the first connection and then relays the connection to it. So the endpoint is handed at a
/// session's start while the browser stays down until something drives a page.
/// </summary>
/// <remarks>
/// <para><b>Bytes, not requests.</b> The relay copies the stream both ways and reads none of it, so the HTTP door and the
/// socket of a CDP client pass alike. The browser's own relay (CHR6) names its own port in the sockets it announces, which
/// is the browser's by then.</para>
///
/// <para><b>A failed bring-up closes the connection</b> and the next connection tries again.</para>
/// </remarks>
public sealed class LazyEndpoint : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<int>> _bringUp;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _accepting;

    /// <param name="bringUp">Brings the browser up if it is not, and answers the port it listens on.</param>
    public LazyEndpoint(Func<CancellationToken, Task<int>> bringUp)
    {
        _bringUp = bringUp;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _accepting = AcceptAsync();
    }

    /// <summary>The port clients are given.</summary>
    public int Port { get; }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var port = await _bringUp(_stop.Token).ConfigureAwait(false);
                using var browser = new TcpClient();
                await browser.ConnectAsync(IPAddress.Loopback, port, _stop.Token).ConfigureAwait(false);

                using var ended = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                var inward = Pump(client, browser, ended);
                var outward = Pump(browser, client, ended);
                await Task.WhenAny(inward, outward).ConfigureAwait(false);
                await ended.CancelAsync().ConfigureAwait(false);
                await Task.WhenAll(inward, outward).ConfigureAwait(false);
            }
            catch (Exception error) when (error is OperationCanceledException or IOException or SocketException
                                              or InvalidOperationException or TimeoutException or ObjectDisposedException)
            {
                // The browser would not come up, or a side went away: the client's connection ends, and the next one asks again.
            }
        }
    }

    /// <summary>Copy one way until it ends; on its end both sides close, so the other way does not wait on a peer that left.</summary>
    private static async Task Pump(TcpClient from, TcpClient to, CancellationTokenSource ended)
    {
        try
        {
            await from.GetStream().CopyToAsync(to.GetStream(), ended.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // Ended by the other way, or by a peer that went.
        }
        finally
        {
            await ended.CancelAsync().ConfigureAwait(false);
            from.Close();
            to.Close();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        await _accepting.ConfigureAwait(false);
        _stop.Dispose();
    }
}
