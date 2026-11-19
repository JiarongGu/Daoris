using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A resumed Claude Code session is sent the tools its servers serve now (TOOLSCHEMA1): the real <c>claude</c> on the path, started and
/// resumed by the native door's own recipes, a stand-in server whose one tool gains a parameter between the start and the resume, and a
/// keyless stand-in model on loopback that keeps the tools each request carried. Claude Code records the system prompt and the tools in
/// the conversation (<c>prompt_snapshot</c>) and, left to itself, sends that record again at every resume, a new tool name added and an
/// existing tool's new parameters never; the door turns the record off, so the resumed request carries the parameter.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class ClaudeResumedToolsProcessTests : IAsyncDisposable
{
    private const string Tool = "mcp__stand-in__ask";

    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(2);

    /// <summary>A server with one tool, read from the file its second argument names at every listing.</summary>
    private const string Server = """
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

    private readonly string _root = Path.Combine(TestHomes.Root, "resumed-tools-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInModel _model = StandInModel.Start();

    public ClaudeResumedToolsProcessTests() => Directory.CreateDirectory(_root);

    public async ValueTask DisposeAsync()
    {
        await _model.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The tool as the stand-in serves it: <c>question</c>, and <c>site</c> once the build that adds it is in place.</summary>
    private static string Tools(bool site) => new JsonArray(new JsonObject
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

    [HarnessOnPathFact("claude")]
    public async Task A_resumed_session_is_sent_the_parameter_its_server_gained_after_the_start()
    {
        var tree = Path.Combine(_root, "tree");
        var home = Path.Combine(_root, "home");
        Directory.CreateDirectory(tree);
        Directory.CreateDirectory(home);
        var toolsFile = Path.Combine(_root, "tools.json");
        File.WriteAllText(toolsFile, Tools(site: false));
        var script = Path.Combine(_root, "server.mjs");
        File.WriteAllText(script, Server);
        var node = PackageStarts.Node(null, null) ?? throw new InvalidOperationException("node is not on PATH");
        var servers = Path.Combine(_root, "servers.mcp.json");
        File.WriteAllText(servers, new JsonObject
        {
            ["mcpServers"] = new JsonObject
            {
                ["stand-in"] = new JsonObject { ["command"] = node, ["args"] = new JsonArray(script, toolsFile) },
            },
        }.ToJsonString());

        var spawn = new ClaudeCodeSpawn();
        var request = new SpawnRequest("test", tree, "http://127.0.0.1:1") { Command = [HarnessOnPathFactAttribute.Located("claude")!] };
        var started = await RunAsync(spawn, spawn.Start(request), servers, tree, home, "Say hello.");
        var conversation = started.Conversation ?? throw new InvalidOperationException($"no session id in the start's output: {started.Output}");
        Assert.All(_model.ToolsSent(Tool), properties => Assert.DoesNotContain("site", properties));
        var before = _model.Requests;

        File.WriteAllText(toolsFile, Tools(site: true));
        var resumed = await RunAsync(spawn, spawn.Resume(request with { Resume = conversation })!, servers, tree, home, "Say hello again.");

        var sent = _model.ToolsSent(Tool, after: before);
        Assert.True(sent.Count > 0, $"the resume sent no request carrying {Tool}: {resumed.Output}");
        Assert.All(sent, properties => Assert.Contains("site", properties));
    }

    private sealed record Ran(string? Conversation, string Output);

    /// <summary>One run of a recipe under a scratch home, the stand-in model and the servers file, the prompt on its input; bounded.</summary>
    private async Task<Ran> RunAsync(ClaudeCodeSpawn spawn, ProcessRecipe recipe, string servers, string tree, string home, string prompt)
    {
        var info = new ProcessStartInfo(recipe.Program)
        {
            WorkingDirectory = tree, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true,
        };
        foreach (var argument in recipe.Arguments) info.ArgumentList.Add(argument);
        spawn.HandServers(info, servers);
        foreach (var name in new[] { "ANTHROPIC_AUTH_TOKEN", "CLAUDE_CODE_OAUTH_TOKEN", "CLAUDE_CODE_USE_BEDROCK", "CLAUDE_CODE_USE_VERTEX" })
        {
            info.Environment.Remove(name);
        }

        info.Environment["CLAUDE_CONFIG_DIR"] = home;
        info.Environment["ANTHROPIC_BASE_URL"] = _model.Url;
        info.Environment["ANTHROPIC_API_KEY"] = "placeholder-not-a-key";
        info.Environment["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1";
        foreach (var (name, value) in ClaudeCodeSpawn.StayPinned) info.Environment[name] = value;
        foreach (var (name, value) in spawn.ToolsUpFront) info.Environment[name] = value;

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();
        using var bound = new CancellationTokenSource(Bound);
        try
        {
            await process.WaitForExitAsync(bound.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"claude ran past {Bound}: {await error}");
        }

        var said = await output;
        string? conversation = null;
        foreach (var line in said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                using var frame = JsonDocument.Parse(line);
                if (frame.RootElement.TryGetProperty("session_id", out var id) && id.ValueKind == JsonValueKind.String) conversation ??= id.GetString();
            }
            catch (JsonException)
            {
            }
        }

        Assert.True(process.ExitCode == 0, $"claude exited {process.ExitCode}: {said}\n{await error}");
        return new Ran(conversation, said);
    }

    /// <summary>
    /// A model on loopback with no account behind it: every Messages request kept as the tools it carried, each answered with a short
    /// streamed reply; a token count answered with a number.
    /// </summary>
    private sealed class StandInModel : IAsyncDisposable
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
}

/// <summary>A test that runs a harness's program with no account behind it: skipped, saying why, where the program is not on the path.</summary>
internal sealed class HarnessOnPathFactAttribute : FactAttribute
{
    /// <param name="program">The program's file name as the path names it, without its extension.</param>
    public HarnessOnPathFactAttribute(string program)
    {
        if (Located(program) is null) Skip = $"`{program}` is not on this machine's path";
    }

    /// <summary>The program's file on the path (<see cref="LiveAgentFactAttribute.Located"/>).</summary>
    public static string? Located(string program) => LiveAgentFactAttribute.Located(program);
}
