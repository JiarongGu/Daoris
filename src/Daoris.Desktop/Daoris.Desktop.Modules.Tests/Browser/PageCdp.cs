using System.Net.WebSockets;
using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// One page of the engine, read over its own CDP socket: the page whose address is given, and what an
/// expression evaluates to in it. What a test needs of a page the engine shows, and no more.
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
        var id = ++_id;
        var request = JsonSerializer.SerializeToUtf8Bytes(new { id, method = "Runtime.evaluate", @params = new { expression, returnByValue = true } });
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

            using var reply = JsonDocument.Parse(message.ToArray());
            if (!reply.RootElement.TryGetProperty("id", out var answered) || answered.GetInt32() != id) continue;
            if (reply.RootElement.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.GetProperty("message").GetString());
            var result = reply.RootElement.GetProperty("result");
            if (result.TryGetProperty("exceptionDetails", out var thrown)) throw new InvalidOperationException(thrown.ToString());
            return result.GetProperty("result").GetProperty("value").GetRawText();
        }
    }

    public void Dispose() => _socket.Dispose();
}
