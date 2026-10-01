using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A host on this machine for the tools' routes (TOOLS7): it answers each path it is given with its bytes, a 404 for
/// any other, and a path marked stalling with one byte and then nothing until the reader goes. A resource list may name
/// an <c>http://</c> address on this machine (D121 §3.2), so the routes are driven over a real socket, through the
/// driver's own client, and never over the network.
/// </summary>
internal sealed class LoopbackHost : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly Dictionary<string, byte[]> _served = new(StringComparer.Ordinal);
    private readonly HashSet<string> _stalling = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stop = new();

    public LoopbackHost()
    {
        _listener.Start();
        _ = Task.Run(AcceptAsync);
    }

    /// <summary>Where the host answers, with no trailing slash.</summary>
    public string Address => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

    /// <summary>Completes once a stalling path has sent its one byte.</summary>
    public TaskCompletionSource Stalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Serve(string path, byte[] bytes)
    {
        lock (_served) _served[path] = bytes;
        return Address + path;
    }

    public string Stall(string path)
    {
        lock (_served) _stalling.Add(path);
        return Address + path;
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception) when (_stop.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            _ = Task.Run(() => AnswerAsync(client));
        }
    }

    private async Task AnswerAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var request = new StringBuilder();
                var buffer = new byte[1];
                while (!request.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
                {
                    if (await stream.ReadAsync(buffer, _stop.Token) == 0) return;
                    request.Append((char)buffer[0]);
                }

                var path = request.ToString().Split(' ')[1];
                byte[]? body;
                bool stalling;
                lock (_served)
                {
                    _served.TryGetValue(path, out body);
                    stalling = _stalling.Contains(path);
                }

                if (stalling)
                {
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 1000000\r\nConnection: close\r\n\r\nP"), _stop.Token);
                    await stream.FlushAsync(_stop.Token);
                    Stalled.TrySetResult();
                    await Task.Delay(Timeout.Infinite, _stop.Token);
                    return;
                }

                var head = body is null
                    ? "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                    : $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head), _stop.Token);
                if (body is not null) await stream.WriteAsync(body, _stop.Token);
                await stream.FlushAsync(_stop.Token);
            }
            catch (Exception) when (_stop.IsCancellationRequested)
            {
                // The host is going.
            }
            catch (IOException)
            {
                // The reader went: a stopped download closes its end.
            }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        _stop.Dispose();
    }

    /// <summary>A zip holding one file at <paramref name="name"/>, as a maker's archive holds a program.</summary>
    public static byte[] Zip(string name, string content)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = new StreamWriter(archive.CreateEntry(name).Open());
            entry.Write(content);
        }

        return buffer.ToArray();
    }

    public static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
