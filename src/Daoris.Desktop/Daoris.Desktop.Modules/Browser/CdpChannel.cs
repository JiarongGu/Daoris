using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// One connection to a browser's own socket that stays open (REVIEWENV1d): calls answered by their id, flattened sessions'
/// events heard as they come, and its closing said. <see cref="EngineCdp"/> makes one call per socket, which is enough to open a
/// window; holding a tab's requests needs a socket that lives as long as the holding, since a client's interception ends with
/// its socket (measured, <c>docs/2026-10-09-review-serving-evidence.md</c> §1 row 4).
/// </summary>
/// <remarks>
/// <b>Heard off the reading loop.</b> Each event is handed to <see cref="Heard"/> on the loop that reads the socket, so a handler
/// that calls back must not wait there for its answer, which that same loop reads: it starts the call and returns.
/// </remarks>
public sealed class CdpChannel : IAsyncDisposable
{
    private static readonly TimeSpan CallLimit = TimeSpan.FromSeconds(15);

    private readonly ClientWebSocket _socket = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _sending = new(1, 1);
    private readonly CancellationTokenSource _closing = new();
    private Task _reading = Task.CompletedTask;
    private int _id;
    private int _closed;

    private CdpChannel()
    {
    }

    /// <summary>An event of the browser's or of a session's: its method, its parameters, and the session it came from, if any.</summary>
    public event Action<string, JsonElement, string?>? Heard;

    /// <summary>Said once, when the socket is gone, by either side.</summary>
    public event Action? Closed;

    /// <summary>Whether the socket still stands.</summary>
    public bool Open => Volatile.Read(ref _closed) == 0 && _socket.State == WebSocketState.Open;

    /// <summary>Connect to the browser on <paramref name="port"/>, by the socket its <c>/json/version</c> names.</summary>
    public static async Task<CdpChannel> ConnectAsync(int port, CancellationToken ct)
    {
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = TimeSpan.FromSeconds(5) };
        using var version = JsonDocument.Parse(await http.GetStringAsync("json/version", ct).ConfigureAwait(false));
        var socketUrl = version.RootElement.TryGetProperty("webSocketDebuggerUrl", out var named) ? named.GetString() : null;
        if (string.IsNullOrEmpty(socketUrl)) throw new InvalidOperationException("the browser named no socket to drive it by.");

        var channel = new CdpChannel();
        try
        {
            await channel._socket.ConnectAsync(new Uri(socketUrl), ct).ConfigureAwait(false);
        }
        catch
        {
            channel._socket.Dispose();
            throw;
        }

        channel._reading = Task.Run(channel.ReadAsync, CancellationToken.None);
        return channel;
    }

    /// <summary>
    /// One call, on the browser or on <paramref name="session"/>, and its result. A refusal throws
    /// <see cref="InvalidOperationException"/> with the browser's words; a socket that went, a <see cref="WebSocketException"/>.
    /// </summary>
    public async Task<JsonElement> CallAsync(string method, object parameters, string? session, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _id);
        var answered = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = answered;
        var frame = session is null
            ? JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters })
            : JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters, sessionId = session });
        try
        {
            if (!Open) throw new WebSocketException("the browser's socket is closed");
            await _sending.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _socket.SendAsync(frame, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
            }
            finally
            {
                _sending.Release();
            }

            return await answered.Task.WaitAsync(CallLimit, ct).ConfigureAwait(false);
        }
        catch (TimeoutException error)
        {
            throw new TimeoutException($"the browser did not answer `{method}` within {CallLimit.TotalSeconds:0} seconds", error);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task ReadAsync()
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (_socket.State == WebSocketState.Open)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult received;
                do
                {
                    received = await _socket.ReceiveAsync(buffer, _closing.Token).ConfigureAwait(false);
                    if (received.MessageType == WebSocketMessageType.Close) return;
                    message.Write(buffer, 0, received.Count);
                }
                while (!received.EndOfMessage);

                Dispatch(message.ToArray());
            }
        }
        catch (Exception error) when (error is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException)
        {
            // The browser went, or this side closed: said below, once.
        }
        finally
        {
            GoneNow();
        }
    }

    private void Dispatch(byte[] frame)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(frame);
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.TryGetProperty("id", out var id) && id.TryGetInt32(out var number))
            {
                if (!_pending.TryRemove(number, out var waiting)) return;
                if (root.TryGetProperty("error", out var error))
                {
                    var said = error.TryGetProperty("message", out var words) ? words.GetString() : error.GetRawText();
                    waiting.TrySetException(new InvalidOperationException(said));
                }
                else
                {
                    waiting.TrySetResult(root.TryGetProperty("result", out var result) ? result.Clone() : default);
                }

                return;
            }

            if (root.TryGetProperty("method", out var method) && method.GetString() is { } name)
            {
                var parameters = root.TryGetProperty("params", out var given) ? given.Clone() : default;
                var session = root.TryGetProperty("sessionId", out var from) ? from.GetString() : null;
                Heard?.Invoke(name, parameters, session);
            }
        }
    }

    private void GoneNow()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0) return;
        foreach (var waiting in _pending.Values) waiting.TrySetException(new WebSocketException("the browser's socket closed"));
        _pending.Clear();
        Closed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        _closing.Cancel();
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, limit.Token).ConfigureAwait(false);
            }
        }
        catch (Exception error) when (error is WebSocketException or OperationCanceledException or IOException or ObjectDisposedException)
        {
            // Closing a socket the browser already dropped is no failure.
        }

        _socket.Abort();
        await _reading.ConfigureAwait(false);
        GoneNow();
        _socket.Dispose();
        _closing.Dispose();
    }
}
