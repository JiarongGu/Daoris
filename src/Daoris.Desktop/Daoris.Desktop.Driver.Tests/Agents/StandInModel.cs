using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A model on loopback with no account behind it: every Messages request kept as the tools it carried, each answered with a short
/// streamed reply; a token count answered with a number.
/// </summary>
internal sealed class StandInModel : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly Task _serving;
    private readonly List<JsonArray?> _requests = [];

    private StandInModel(HttpListener listener, string url)
    {
        _listener = listener;
        Url = url.TrimEnd('/');
        _serving = ServeAsync();
    }

    public string Url { get; }

    public int Requests
    {
        get
        {
            lock (_requests) return _requests.Count;
        }
    }

    public static StandInModel Start()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var url = $"http://127.0.0.1:{port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();
        return new StandInModel(listener, url);
    }

    /// <summary>The parameters of <paramref name="tool"/> in each request from the <paramref name="after"/>th on that carried it.</summary>
    public IReadOnlyList<IReadOnlyList<string>> ToolsSent(string tool, int after = 0)
    {
        lock (_requests)
        {
            return [.. _requests.Skip(after)
                .Select(tools => tools?.OfType<JsonObject>().FirstOrDefault(each => (string?)each["name"] == tool))
                .OfType<JsonObject>()
                .Select(each => (IReadOnlyList<string>)[.. (each["input_schema"]?["properties"] as JsonObject ?? []).Select(property => property.Key)])];
        }
    }

    /// <summary>Every tool name the requests from the <paramref name="after"/>th on carried, whichever tool; empty where they carried none.</summary>
    public IReadOnlyList<string> ToolNames(int after = 0)
    {
        lock (_requests)
        {
            return [.. _requests.Skip(after).SelectMany(tools => tools?.OfType<JsonObject>() ?? []).Select(each => (string?)each["name"] ?? "").Distinct(StringComparer.Ordinal)];
        }
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }

            try
            {
                await AnswerAsync(context);
            }
            catch (Exception error) when (error is HttpListenerException or IOException or ObjectDisposedException)
            {
            }
        }
    }

    private async Task AnswerAsync(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var text = await reader.ReadToEndAsync();
        var path = context.Request.Url!.AbsolutePath;
        if (path.EndsWith("/count_tokens", StringComparison.Ordinal))
        {
            await WriteAsync(context, "application/json", """{"input_tokens":10}""");
            return;
        }

        if (!path.EndsWith("/messages", StringComparison.Ordinal))
        {
            await WriteAsync(context, "application/json", "{}");
            return;
        }

        JsonObject? body = null;
        try { body = JsonNode.Parse(text) as JsonObject; } catch (JsonException) { }
        lock (_requests) _requests.Add(body?["tools"] as JsonArray);
        if (body?["stream"]?.GetValue<bool>() != true)
        {
            await WriteAsync(context, "application/json", """
                {"id":"msg_stand_in","type":"message","role":"assistant","model":"stand-in","content":[{"type":"text","text":"ok"}],"stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":1,"output_tokens":1}}
                """);
            return;
        }

        var events = new StringBuilder();
        void Event(string name, string data) => events.Append("event: ").Append(name).Append("\ndata: ").Append(data).Append("\n\n");
        Event("message_start", """{"type":"message_start","message":{"id":"msg_stand_in","type":"message","role":"assistant","model":"stand-in","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":10,"output_tokens":1}}}""");
        Event("content_block_start", """{"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}""");
        Event("content_block_delta", """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"hello"}}""");
        Event("content_block_stop", """{"type":"content_block_stop","index":0}""");
        Event("message_delta", """{"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":1}}""");
        Event("message_stop", """{"type":"message_stop"}""");
        await WriteAsync(context, "text/event-stream", events.ToString());
    }

    private static async Task WriteAsync(HttpListenerContext context, string type, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        context.Response.StatusCode = 200;
        context.Response.ContentType = type;
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        _listener.Close();
        await _serving;
    }
}

/// <summary>
/// The stand-in server every door's case hands its agent: one tool, <c>ask</c>, whose parameters are read from a file at every listing,
/// so a case can change what the server serves between a start and a resume.
/// </summary>
internal static class StandInServer
{
    /// <summary>A server with one tool, read from the file its second argument names at every listing.</summary>
    public const string Script = """
        import { createInterface } from 'node:readline';
        import { readFileSync } from 'node:fs';
        const say = (message) => process.stdout.write(JSON.stringify(message) + '\n');
        for await (const line of createInterface({ input: process.stdin })) {
          let message;
          try { message = JSON.parse(line); } catch { continue; }
          if (message.id === undefined) continue;
          if (message.method === 'initialize') {
            say({ jsonrpc: '2.0', id: message.id, result: { protocolVersion: message.params?.protocolVersion ?? '2025-06-18', capabilities: { tools: {} }, serverInfo: { name: 'stand-in', version: '1.0.0' } } });
          } else if (message.method === 'tools/list') {
            say({ jsonrpc: '2.0', id: message.id, result: { tools: JSON.parse(readFileSync(process.argv[2], 'utf8')) } });
          } else if (message.method === 'ping') {
            say({ jsonrpc: '2.0', id: message.id, result: {} });
          } else {
            say({ jsonrpc: '2.0', id: message.id, error: { code: -32601, message: 'method not found' } });
          }
        }
        """;

    /// <summary>The tool as the stand-in serves it: <c>question</c>, and <c>site</c> once the build that adds it is in place.</summary>
    public static string Tools(bool site) => new JsonArray(new JsonObject
    {
        ["name"] = "ask",
        ["description"] = "Ask the person a question.",
        ["inputSchema"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = site
                ? new JsonObject { ["question"] = new JsonObject { ["type"] = "string" }, ["site"] = new JsonObject { ["type"] = "string" } }
                : new JsonObject { ["question"] = new JsonObject { ["type"] = "string" } },
            ["required"] = new JsonArray("question"),
        },
    }).ToJsonString();

    /// <summary>The tools sent list <paramref name="server"/>'s tool; none of them, an empty list too, fails.</summary>
    public static void AssertListed(IReadOnlyList<string> tools, string server, string said)
    {
        Assert.True(tools.Count > 0, $"the stand-in model was sent no tools at all, so it cannot say the agent lists `{server}`'s: {said}");
        Assert.True(
            // An agent may spell a server's name its own way (Codex's `mcp__daoris_knowledge`): the hyphen and the underscore are one here.
            tools.Any(tool => tool.Replace('-', '_').Contains(server.Replace('-', '_'), StringComparison.Ordinal) && tool.EndsWith("ask", StringComparison.Ordinal)),
            $"the agent lists none of `{server}`'s tools; it was sent: {string.Join(", ", tools)}");
    }

    /// <summary>The server as a plan hands it: its script and tools file written under <paramref name="root"/>, named <paramref name="name"/>.</summary>
    public static ServerEntry Entry(string root, string name, bool site = false)
    {
        var toolsFile = Path.Combine(root, "tools.json");
        File.WriteAllText(toolsFile, Tools(site));
        var script = Path.Combine(root, "server.mjs");
        File.WriteAllText(script, Script);
        var node = PackageStarts.Node(null, null) ?? throw new InvalidOperationException("node is not on PATH");
        return new ServerEntry(name, node, [script, toolsFile], new Dictionary<string, string>());
    }
}
