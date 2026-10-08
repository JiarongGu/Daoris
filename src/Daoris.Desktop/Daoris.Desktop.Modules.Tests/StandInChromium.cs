using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A Chromium's debug endpoint, standing in for the review's serving (REVIEWENV1d): its pages, the browser's socket with
/// flattened sessions, <c>Fetch</c> interception per session, and a page's request walked through every interception that holds
/// it before it reaches the network, a real loopback socket. What it models is what was measured on a real one
/// (<c>docs/2026-10-09-review-serving-evidence.md</c>): interceptions chain on one tab, the one attached last first, a
/// client's own ending with its socket; one that fulfils answers, one that lets the request go on hands it to the next.
/// </summary>
internal sealed class StandInChromium : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();
    private readonly List<Page> _pages = [];
    private readonly List<Client> _clients = [];
    private readonly HttpClient _network = new(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(10) };
    private int _next;

    public StandInChromium()
    {
        _listener.Start();
        _ = Task.Run(AcceptAsync);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>A tab: its id, where it is, where it was sent, and how often it was brought forward.</summary>
    public sealed class Page(string id, string url)
    {
        public string Id { get; } = id;

        public string Url { get; set; } = url;

        public List<string> Navigations { get; } = [];

        public int Activated { get; set; }
    }

    /// <summary>What a page's request met: its status, body and type, and whether an interception or the network answered it.</summary>
    public sealed record Loaded(int Status, string Body, string? Type, string By);

    public IReadOnlyList<Page> Pages
    {
        get
        {
            lock (_gate) return [.. _pages];
        }
    }

    /// <summary>The patterns each live client holds on <paramref name="target"/>, one list per session.</summary>
    public IReadOnlyList<IReadOnlyList<string>> PatternsOn(string target)
    {
        lock (_gate)
        {
            return [.. _clients.SelectMany(client => client.Sessions
                .Where(session => session.Value == target && client.Patterns.ContainsKey(session.Key))
                .Select(session => (IReadOnlyList<string>)[.. client.Patterns[session.Key].Select(pattern => pattern.Spelled)]))];
        }
    }

    /// <summary>A tab opened at <paramref name="url"/>, as the person or another agent opens one.</summary>
    public string Open(string url)
    {
        lock (_gate)
        {
            var page = new Page($"T{++_next}", url);
            _pages.Add(page);
            return page.Id;
        }
    }

    /// <summary>The person closes a tab: each session attached to it is told it went.</summary>
    public async Task CloseAsync(string target)
    {
        List<(Client Client, string Session)> told;
        lock (_gate)
        {
            _pages.RemoveAll(page => page.Id == target);
            told = [.. _clients.SelectMany(client => client.Sessions.Where(session => session.Value == target).Select(session => (client, session.Key)))];
            foreach (var (client, session) in told)
            {
                client.Sessions.Remove(session);
                client.Patterns.Remove(session);
            }
        }

        foreach (var (client, session) in told)
        {
            await client.SendAsync(new { method = "Target.detachedFromTarget", @params = new { sessionId = session, targetId = target } });
        }
    }

    /// <summary>
    /// A request the page in <paramref name="target"/> makes: paused at each session holding a pattern that matches it, the one
    /// attached last first, and fetched from the network where none fulfils it.
    /// </summary>
    public async Task<Loaded> LoadAsync(string target, string url, string resourceType = "Document")
    {
        List<(Client Client, string Session)> holding;
        lock (_gate)
        {
            holding =
            [
                .. _clients
                    .SelectMany(client => client.Sessions
                        .Where(session => session.Value == target && client.Patterns.TryGetValue(session.Key, out var patterns)
                                          && patterns.Any(pattern => pattern.Matches(url)))
                        .Select(session => (Client: client, Session: session.Key, At: client.AttachedAt[session.Key])))
                    .OrderByDescending(each => each.At)
                    .Select(each => (each.Client, each.Session)),
            ];
        }

        foreach (var (client, session) in holding)
        {
            var requestId = $"R{Interlocked.Increment(ref _next)}";
            var reply = new TaskCompletionSource<(string Method, JsonElement Params)>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate) client.Paused[requestId] = reply;
            if (!await client.SendAsync(new
                {
                    method = "Fetch.requestPaused",
                    @params = new { requestId, request = new { url, method = "GET", headers = new { } }, frameId = "F1", resourceType },
                    sessionId = session,
                }))
            {
                continue;
            }

            var (method, parameters) = await reply.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (method == "Fetch.fulfillRequest")
            {
                var type = parameters.TryGetProperty("responseHeaders", out var headers)
                    ? headers.EnumerateArray()
                        .Where(header => string.Equals(header.GetProperty("name").GetString(), "content-type", StringComparison.OrdinalIgnoreCase))
                        .Select(header => header.GetProperty("value").GetString())
                        .FirstOrDefault()
                    : null;
                var body = parameters.TryGetProperty("body", out var encoded) ? Encoding.UTF8.GetString(Convert.FromBase64String(encoded.GetString()!)) : "";
                return new Loaded(parameters.GetProperty("responseCode").GetInt32(), body, type, "fulfilled");
            }
        }

        using var response = await _network.GetAsync(url);
        return new Loaded((int)response.StatusCode, await response.Content.ReadAsStringAsync(), response.Content.Headers.ContentType?.MediaType, "network");
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        lock (_gate)
        {
            foreach (var client in _clients) client.Socket.Abort();
        }

        _network.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient connection;
            try
            {
                connection = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = Task.Run(() => ServeAsync(connection));
        }
    }

    private async Task ServeAsync(TcpClient connection)
    {
        using var held = connection;
        var stream = connection.GetStream();
        var head = await ReadHeadAsync(stream);
        if (head is null) return;
        var path = head.Split(' ', 3)[1];

        var key = Regex.Match(head, @"^Sec-WebSocket-Key:\s*(?<key>\S+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (key.Success)
        {
            var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key.Groups["key"].Value + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" + $"Sec-WebSocket-Accept: {accept}\r\n\r\n"));
            var socket = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = TimeSpan.Zero });
            var client = new Client(socket);
            lock (_gate) _clients.Add(client);
            try
            {
                await ListenAsync(client);
            }
            finally
            {
                // A client's interceptions end with its socket, as a session's do with its server (measured, §1 row 4).
                lock (_gate) _clients.Remove(client);
                foreach (var waiting in client.Paused.Values) waiting.TrySetResult(("Fetch.continueRequest", default));
            }

            return;
        }

        string body;
        lock (_gate)
        {
            body = path switch
            {
                "/json/version" => JsonSerializer.Serialize(new
                {
                    Browser = "Chrome/140.0.0.0",
                    webSocketDebuggerUrl = $"ws://127.0.0.1:{Port}/devtools/browser/stand-in",
                }),
                "/json/list" => JsonSerializer.Serialize(_pages.Select(page => new { id = page.Id, type = "page", url = page.Url, title = "" })),
                _ when path.StartsWith("/json/activate/", StringComparison.Ordinal) => Activate(path["/json/activate/".Length..]),
                _ => "",
            };
        }

        var bytes = Encoding.UTF8.GetBytes(body);
        await stream.WriteAsync(Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {(body.Length == 0 ? "404 Not Found" : "200 OK")}\r\nContent-Type: application/json; charset=UTF-8\r\n"
            + $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"));
        await stream.WriteAsync(bytes);
    }

    private string Activate(string target)
    {
        var page = _pages.FirstOrDefault(each => each.Id == Uri.UnescapeDataString(target));
        if (page is null) return "";
        page.Activated++;
        return "\"Target activated\"";
    }

    private async Task ListenAsync(Client client)
    {
        var buffer = new byte[256 * 1024];
        while (client.Socket.State == WebSocketState.Open)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult received;
            try
            {
                do
                {
                    received = await client.Socket.ReceiveAsync(buffer, _stop.Token);
                    if (received.MessageType == WebSocketMessageType.Close)
                    {
                        // The close answered, as a browser answers it, so the client's own close completes.
                        await client.CloseAsync();
                        return;
                    }

                    message.Write(buffer, 0, received.Count);
                }
                while (!received.EndOfMessage);
            }
            catch (Exception error) when (error is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException)
            {
                return;
            }

            using var call = JsonDocument.Parse(message.ToArray());
            var root = call.RootElement;
            var id = root.GetProperty("id").GetInt32();
            var method = root.GetProperty("method").GetString()!;
            var parameters = root.TryGetProperty("params", out var given) ? given.Clone() : default;
            var session = root.TryGetProperty("sessionId", out var named) ? named.GetString() : null;
            await client.SendAsync(Answer(client, id, method, parameters, session));
        }
    }

    private object Answer(Client client, int id, string method, JsonElement parameters, string? session)
    {
        lock (_gate)
        {
            switch (method)
            {
                case "Target.createTarget":
                {
                    var page = new Page($"T{++_next}", parameters.GetProperty("url").GetString()!);
                    _pages.Add(page);
                    return new { id, result = new { targetId = page.Id } };
                }

                case "Target.attachToTarget":
                {
                    var target = parameters.GetProperty("targetId").GetString()!;
                    if (_pages.All(page => page.Id != target)) return new { id, error = new { code = -32602, message = "No target with given id found" } };
                    var attached = $"S{++_next}";
                    client.Sessions[attached] = target;
                    client.AttachedAt[attached] = _next;
                    return new { id, result = new { sessionId = attached } };
                }

                case "Fetch.enable" when session is not null && client.Sessions.ContainsKey(session):
                    client.Patterns[session] =
                    [
                        .. parameters.GetProperty("patterns").EnumerateArray().Select(pattern => new Pattern(pattern.GetProperty("urlPattern").GetString()!)),
                    ];
                    return new { id, result = new { } };

                case "Fetch.disable" when session is not null:
                    client.Patterns.Remove(session);
                    return new { id, result = new { } };

                case "Page.navigate" when session is not null && client.Sessions.TryGetValue(session, out var navigated):
                {
                    var page = _pages.First(each => each.Id == navigated);
                    page.Url = parameters.GetProperty("url").GetString()!;
                    page.Navigations.Add(page.Url);
                    return new { id, result = new { frameId = "F1" } };
                }

                case "Fetch.fulfillRequest" or "Fetch.continueRequest":
                {
                    var requestId = parameters.GetProperty("requestId").GetString()!;
                    if (client.Paused.Remove(requestId, out var waiting)) waiting.TrySetResult((method, parameters));
                    return new { id, result = new { } };
                }

                default:
                    return new { id, error = new { code = -32601, message = $"'{method}' wasn't found" } };
            }
        }
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
                return Encoding.ASCII.GetString([.. bytes]);
            }
        }

        return null;
    }

    /// <summary>A CDP <c>urlPattern</c>: <c>*</c> any run of characters, <c>?</c> one, everything else itself.</summary>
    private sealed class Pattern(string spelled)
    {
        private readonly Regex _matcher = new(
            "^" + Regex.Escape(spelled).Replace(@"\*", ".*", StringComparison.Ordinal).Replace(@"\?", ".", StringComparison.Ordinal) + "$");

        public string Spelled => spelled;

        public bool Matches(string url) => _matcher.IsMatch(url);
    }

    /// <summary>One socket onto the browser: its sessions, the patterns each holds, and the requests paused at it.</summary>
    private sealed class Client(WebSocket socket)
    {
        private readonly SemaphoreSlim _send = new(1, 1);

        public WebSocket Socket { get; } = socket;

        public Dictionary<string, string> Sessions { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, int> AttachedAt { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<Pattern>> Patterns { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, TaskCompletionSource<(string Method, JsonElement Params)>> Paused { get; } = new(StringComparer.Ordinal);

        /// <summary>Answer the client's close.</summary>
        public async Task CloseAsync()
        {
            await _send.WaitAsync();
            try
            {
                if (Socket.State == WebSocketState.CloseReceived)
                {
                    await Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                }
            }
            catch (Exception error) when (error is WebSocketException or IOException or ObjectDisposedException)
            {
                // Gone already.
            }
            finally
            {
                _send.Release();
            }
        }

        /// <summary>Send one frame; false where the socket is gone.</summary>
        public async Task<bool> SendAsync(object frame)
        {
            await _send.WaitAsync();
            try
            {
                if (Socket.State != WebSocketState.Open) return false;
                await Socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(frame), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
                return true;
            }
            catch (Exception error) when (error is WebSocketException or IOException or ObjectDisposedException)
            {
                return false;
            }
            finally
            {
                _send.Release();
            }
        }
    }
}

/// <summary>
/// The person's own dev server, standing in: it answers every path as theirs and counts each hit, so a test can say it was never
/// reached for the paths Daoris serves.
/// </summary>
internal sealed class PersonServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly List<string> _hits = [];

    public PersonServer()
    {
        _listener.Start();
        _ = Task.Run(AcceptAsync);
    }

    /// <summary>Where it answers: an origin with no last slash, as a review rule's address is.</summary>
    public string Address => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

    /// <summary>The paths it was asked for, in order.</summary>
    public IReadOnlyList<string> Hits
    {
        get
        {
            lock (_hits) return [.. _hits];
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient connection;
            try
            {
                connection = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                using var held = connection;
                var stream = connection.GetStream();
                var reader = new StreamReader(stream, Encoding.ASCII);
                var line = await reader.ReadLineAsync();
                while (await reader.ReadLineAsync() is { Length: > 0 })
                {
                }

                var path = line?.Split(' ')[1] ?? "/";
                lock (_hits) _hits.Add(path);
                var body = Encoding.UTF8.GetBytes($"PERSON {path}");
                await stream.WriteAsync(Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n"));
                await stream.WriteAsync(body);
            });
        }
    }
}
