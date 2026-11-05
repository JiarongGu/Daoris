using System.Net.WebSockets;
using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// One page of the engine, read over its own CDP socket: the page whose address is given, and what an
/// expression evaluates to in it, and the page held shown. What a test needs of a page the engine shows, and no more.
/// </summary>
internal sealed class PageCdp : IDisposable
{
    private readonly ClientWebSocket _socket = new();
    private int _id;

    private PageCdp() { }

    /// <summary>The page at <paramref name="address"/>, found on the engine's port, or null when none is there yet.</summary>
    public static async Task<PageCdp?> OpenAsync(int port, string address, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var list = JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/json/list", ct));
        foreach (var target in list.RootElement.EnumerateArray())
        {
            if (target.GetProperty("type").GetString() != "page" || target.GetProperty("url").GetString() != address) continue;
            var page = new PageCdp();
            await page._socket.ConnectAsync(new Uri(target.GetProperty("webSocketDebuggerUrl").GetString()!), ct);
            return page;
        }

        return null;
    }

    /// <summary>The value of <paramref name="expression"/> as JSON text, by value.</summary>
    public async Task<string> EvaluateAsync(string expression, CancellationToken ct)
    {
        using var reply = await CallAsync("Runtime.evaluate", new { expression, returnByValue = true }, ct);
        var result = reply.RootElement.GetProperty("result");
        if (result.TryGetProperty("exceptionDetails", out var thrown)) throw new InvalidOperationException(thrown.ToString());
        return result.GetProperty("result").GetProperty("value").GetRawText();
    }

    /// <summary>
    /// The page counts as shown and focused for as long as this socket is open, whatever its window's state. Chrome
    /// loads no media in a hidden page that has played none, and a window opened in the background is hidden whenever
    /// it is covered, or the screen is locked or off: focus emulation holds the page visible, as DevTools' "Emulate a
    /// focused page" does, without moving the window or taking the person's focus.
    /// </summary>
    public async Task ShowAsync(CancellationToken ct)
    {
        using var reply = await CallAsync("Emulation.setFocusEmulationEnabled", new { enabled = true }, ct);
    }

    private async Task<JsonDocument> CallAsync(string method, object parameters, CancellationToken ct)
    {
        var id = ++_id;
        var request = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters });
        await _socket.SendAsync(request, WebSocketMessageType.Text, endOfMessage: true, ct);

        var buffer = new byte[64 * 1024];
        while (true)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult received;
            do
            {
                received = await _socket.ReceiveAsync(buffer, ct);
                if (received.MessageType == WebSocketMessageType.Close) throw new WebSocketException("the page closed its socket");
                message.Write(buffer, 0, received.Count);
            }
            while (!received.EndOfMessage);

            var reply = JsonDocument.Parse(message.ToArray());
            if (!reply.RootElement.TryGetProperty("id", out var answered) || answered.GetInt32() != id)
            {
                reply.Dispose();
                continue;
            }

            if (reply.RootElement.TryGetProperty("error", out var error))
            {
                var said = $"{method}: {error.GetProperty("message").GetString()}";
                reply.Dispose();
                throw new InvalidOperationException(said);
            }

            return reply;
        }
    }

    public void Dispose() => _socket.Dispose();
}
