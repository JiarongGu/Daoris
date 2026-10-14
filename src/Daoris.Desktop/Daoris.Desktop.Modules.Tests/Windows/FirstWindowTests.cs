using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// LOG2b: the browser's first window, made over the engine's port (CHR3), and what it leaves when that
/// fails. The first real log's one <c>error</c> in the <c>browser</c> source was an exception from a task
/// nobody observed: a WebSocket to the engine's port closed without its close handshake, rethrown by the
/// finalizer with nothing to say where. The browser's own tasks are observed now, and a failure is a
/// line naming where it happened, and it stops the browser, as before, rather than leave a process with
/// no window.
/// </summary>
public sealed class FirstWindowTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-log2b-" + Guid.NewGuid().ToString("N")[..8]);

    public FirstWindowTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string[] Lines()
    {
        var folder = Path.Combine(_home, MachineLog.Folder);
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder).SelectMany(path =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }).ToArray();
    }

    /// <summary>The log's own failure, reproduced: the engine takes the socket and drops it before answering.</summary>
    [Fact]
    public async Task An_engine_that_drops_its_socket_is_a_line_saying_where_and_the_browser_stops()
    {
        using var engine = DroppingEngine.Start();
        using var log = new MachineLog(_home, "browser");
        using var cdp = new EngineCdp(engine.Port);
        using var stop = new CancellationTokenSource();

        await log.Observe(
            cdp.FirstWindowAsync(background: true, TimeSpan.FromSeconds(10), stop.Token),
            EngineBrowser.FirstWindowPlace, stop.Cancel);

        Assert.True(engine.Upgraded, "the stand-in engine never took a socket");
        var line = Assert.Single(Lines());
        Assert.Contains("\"where\":\"the browser's first window\"", line);
        Assert.Contains("\"type\":\"System.Net.WebSockets.WebSocketException\"", line);
        Assert.True(stop.IsCancellationRequested, "a browser that could not make its window was left running");
    }

    /// <summary>A port that never answers is a sentence with its limit, not a silence.</summary>
    [Fact]
    public async Task A_port_that_never_answers_is_a_timeout_saying_so()
    {
        using var log = new MachineLog(_home, "browser");
        using var cdp = new EngineCdp(InAppBrowser.FreePort());
        using var stop = new CancellationTokenSource();

        await log.Observe(
            cdp.FirstWindowAsync(background: false, TimeSpan.FromMilliseconds(300), stop.Token),
            EngineBrowser.FirstWindowPlace, stop.Cancel);

        var line = Assert.Single(Lines());
        Assert.Contains("\"type\":\"System.TimeoutException\"", line);
        Assert.Contains("its port did not answer within 0.3 seconds", line);
        Assert.True(stop.IsCancellationRequested);
    }

    /// <summary>The shell went first: nothing failed, so nothing is written and nothing more is stopped.</summary>
    [Fact]
    public async Task A_shell_that_went_first_is_no_failure()
    {
        using var log = new MachineLog(_home, "browser");
        using var cdp = new EngineCdp(InAppBrowser.FreePort());
        using var gone = new CancellationTokenSource();
        await gone.CancelAsync();
        var stopped = false;

        await log.Observe(
            cdp.FirstWindowAsync(background: true, TimeSpan.FromSeconds(10), gone.Token),
            EngineBrowser.FirstWindowPlace, () => stopped = true);

        Assert.Empty(Lines());
        Assert.False(stopped);
    }

    /// <summary>
    /// Held on the browser's own source, because the next task written there will not know to: every
    /// task it starts and does not await is handed to the log's <c>Observe</c>, never discarded bare.
    /// </summary>
    [Fact]
    public void Every_task_the_browser_starts_and_does_not_await_is_observed()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.App", "BrowserProcess.cs"));

        var discarded = Regex.Matches(source, @"^\s*_ = (?<what>.+)$", RegexOptions.Multiline)
            .Select(match => match.Groups["what"].Value.Trim())
            .ToList();

        Assert.NotEmpty(discarded);
        Assert.All(discarded, what => Assert.StartsWith("log.Observe(", what));
    }

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        return at?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }

    /// <summary>
    /// An engine's debug endpoint that answers <c>/json/version</c>, takes the browser's socket with a
    /// proper handshake, reads the call, and then drops the connection without a close frame.
    /// </summary>
    private sealed class DroppingEngine : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stopping = new();
        private int _upgraded;

        private DroppingEngine(TcpListener listener)
        {
            _listener = listener;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        public int Port { get; }

        public bool Upgraded => Volatile.Read(ref _upgraded) > 0;

        public static DroppingEngine Start()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var engine = new DroppingEngine(listener);
            _ = engine.AcceptAsync();
            return engine;
        }

        private async Task AcceptAsync()
        {
            while (!_stopping.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_stopping.Token);
                }
                catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or SocketException)
                {
                    return;
                }

                _ = Task.Run(() => ServeAsync(client)).ContinueWith(static t => t.Exception, TaskScheduler.Default);
            }
        }

        private async Task ServeAsync(TcpClient client)
        {
            using var connection = client;
            var stream = connection.GetStream();
            var head = await ReadHeadAsync(stream);
            if (head is null) return;

            var key = Regex.Match(head, @"^Sec-WebSocket-Key:\s*(?<key>\S+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (key.Success)
            {
                var accept = Convert.ToBase64String(SHA1.HashData(
                    Encoding.ASCII.GetBytes(key.Groups["key"].Value + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                await stream.WriteAsync(Encoding.ASCII.GetBytes(
                    "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
                    + $"Sec-WebSocket-Accept: {accept}\r\n\r\n"));
                Interlocked.Increment(ref _upgraded);

                // The call arrives, and the engine goes without answering it or closing the socket properly.
                await stream.ReadAsync(new byte[64 * 1024]);
                connection.Client.Shutdown(SocketShutdown.Both);
                return;
            }

            var body = $"{{\"Browser\":\"Chrome/140\",\"webSocketDebuggerUrl\":\"ws://127.0.0.1:{Port}/devtools/browser/log2b\"}}";
            var bytes = Encoding.UTF8.GetBytes(body);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=UTF-8\r\n"
                + $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"));
            await stream.WriteAsync(bytes);
        }

        private static async Task<string?> ReadHeadAsync(Stream stream)
        {
            var bytes = new List<byte>();
            var one = new byte[1];
            while (bytes.Count < 16 * 1024)
            {
                if (await stream.ReadAsync(one) == 0) return null;
                bytes.Add(one[0]);
                var n = bytes.Count;
                if (n >= 4 && bytes[n - 4] == '\r' && bytes[n - 3] == '\n' && bytes[n - 2] == '\r' && bytes[n - 1] == '\n')
                {
                    return Encoding.ASCII.GetString(bytes.ToArray());
                }
            }

            return null;
        }

        public void Dispose()
        {
            _stopping.Cancel();
            _listener.Stop();
        }
    }
}
