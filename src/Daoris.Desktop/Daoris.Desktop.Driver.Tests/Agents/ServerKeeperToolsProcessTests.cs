using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Through real processes: the browser's server is offered only the tools its plugin declares, a call to another is answered without
/// reaching it, and a call past its bound is ended and said, the server told to cancel it.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class ServerKeeperToolsProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "server keeper tools", Guid.NewGuid().ToString("N")[..8]);

    public ServerKeeperToolsProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose() => GitSeed.Discard(_root);

    /// <summary>
    /// A server offering three tools: `browser_snapshot` answers at once, `browser_wait_for` never answers, `browser_run_code_unsafe`
    /// says it ran. Each cancellation it is told is said back as a `cancelled` line.
    /// </summary>
    private const string Server = """
        import { createInterface } from 'node:readline';
        const say = (message) => process.stdout.write(JSON.stringify(message) + '\n');
        for await (const line of createInterface({ input: process.stdin })) {
          const message = JSON.parse(line);
          if (message.method === 'tools/list') {
            say({ jsonrpc: '2.0', id: message.id, result: { tools: ['browser_snapshot', 'browser_wait_for', 'browser_run_code_unsafe'].map(name => ({ name })) } });
          } else if (message.method === 'notifications/cancelled') {
            say({ jsonrpc: '2.0', method: 'cancelled', params: { id: message.params.requestId } });
          } else if (message.method === 'tools/call' && message.params.name !== 'browser_wait_for') {
            say({ jsonrpc: '2.0', id: message.id, result: { content: [{ type: 'text', text: `ran ${message.params.name}` }] } });
          }
        }
        """;

    [Fact]
    public async Task Only_the_declared_tools_are_offered_or_called_and_a_call_past_its_bound_is_ended_and_said()
    {
        var script = Path.Combine(_root, "server.mjs");
        File.WriteAllText(script, Server);
        var node = PackageStarts.Node(null, null) ?? throw new InvalidOperationException("node is not on PATH");
        var kept = ServerKeeper.Wrap(
            Path.Combine(_root, "home"), new("browser", node, [script], new Dictionary<string, string>()), 1024, everyMs: 5000,
            tools: ["browser_snapshot", "browser_wait_for"], boundMs: 1500);
        var start = new ProcessStartInfo(kept.Command)
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, StandardOutputEncoding = Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true,
        };
        foreach (var argument in kept.Arguments) start.ArgumentList.Add(argument);
        using var keeper = Process.Start(start)!;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            async Task<JsonElement> Next() =>
                JsonDocument.Parse(await keeper.StandardOutput.ReadLineAsync(timeout.Token) ?? throw new InvalidOperationException("the keeper closed")).RootElement;
            Task Send(object message) => keeper.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message));

            await Send(new { jsonrpc = "2.0", id = 1, method = "tools/list" });
            var listed = (await Next()).GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()).ToList();
            Assert.Equal(["browser_snapshot", "browser_wait_for"], listed);

            await Send(new { jsonrpc = "2.0", id = 2, method = "tools/call", @params = new { name = "browser_run_code_unsafe", arguments = new { } } });
            var refused = (await Next()).GetProperty("result");
            Assert.True(refused.GetProperty("isError").GetBoolean());
            Assert.Contains("not among the tools", refused.GetProperty("content")[0].GetProperty("text").GetString());

            await Send(new { jsonrpc = "2.0", id = 3, method = "tools/call", @params = new { name = "browser_wait_for", arguments = new { } } });
            var lines = new[] { await Next(), await Next() };
            var ended = lines.Single(line => line.TryGetProperty("id", out var id) && id.GetInt32() == 3).GetProperty("result");
            Assert.True(ended.GetProperty("isError").GetBoolean());
            Assert.Contains("ran past its bound of 1.5 seconds", ended.GetProperty("content")[0].GetProperty("text").GetString());
            Assert.Contains(lines, line => line.TryGetProperty("method", out var method) && method.GetString() == "cancelled");

            await Send(new { jsonrpc = "2.0", id = 4, method = "tools/call", @params = new { name = "browser_snapshot", arguments = new { } } });
            Assert.Equal("ran browser_snapshot", (await Next()).GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
        }
        finally
        {
            keeper.Kill(entireProcessTree: true);
        }
    }
}
