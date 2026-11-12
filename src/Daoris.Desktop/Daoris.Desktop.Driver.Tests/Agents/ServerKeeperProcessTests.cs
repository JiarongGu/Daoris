using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Through real processes: a server that keeps what every call hands it (a stand-in for a browser server that keeps
/// each page's requests) is started afresh by the keeper once it passes the ceiling, between calls, and the chat sees one server:
/// no call is lost, the new server is initialized as the first is, and its working set never passes the ceiling by more than
/// the call that crossed it.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ServerKeeperProcessTests : IDisposable
{
    private const int CallMb = 40;
    private const int CeilingMb = 180;
    private const int Calls = 24;

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "server keeper", Guid.NewGuid().ToString("N")[..8]);

    public ServerKeeperProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose() => GitSeed.Discard(_root);

    private const string StandIn = """
        import { createInterface } from 'node:readline';
        const kept = [];
        let initialized = false;
        const say = (message) => process.stdout.write(JSON.stringify({ jsonrpc: '2.0', ...message }) + '\n');
        for await (const line of createInterface({ input: process.stdin })) {
          const message = JSON.parse(line);
          if (message.method === 'initialize') say({ id: message.id, result: { protocolVersion: '2024-11-05', capabilities: {}, serverInfo: { name: 'stand-in', version: '1' } } });
          else if (message.method === 'notifications/initialized') initialized = true;
          else if (message.method === 'tools/call') {
            kept.push(Buffer.alloc(Number(message.params.arguments.mb) * 1024 * 1024, 1));
            say({ id: message.id, result: { content: [{ type: 'text', text: JSON.stringify({ pid: process.pid, kept: kept.length, initialized, rss: process.memoryUsage().rss }) }] } });
          }
        }
        """;

    [Fact]
    public async Task A_server_past_the_ceiling_is_started_afresh_between_calls_and_no_call_is_lost()
    {
        var script = Path.Combine(_root, "stand-in.mjs");
        File.WriteAllText(script, StandIn);
        var home = Path.Combine(_root, "home");
        var node = PackageStarts.Node(null, null) ?? throw new InvalidOperationException("node is not on PATH");
        var kept = ServerKeeper.Wrap(home, new("browser", node, [script], new Dictionary<string, string>()), CeilingMb, everyMs: 200);
        Assert.Equal(ServerKeeper.Source, File.ReadAllText(kept.Arguments[0]));

        var start = new ProcessStartInfo(kept.Command) { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in kept.Arguments) start.ArgumentList.Add(argument);
        using var keeper = Process.Start(start)!;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            async Task<JsonElement> Ask(int id, string method, object parameters)
            {
                await keeper.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters }));
                var line = await keeper.StandardOutput.ReadLineAsync(timeout.Token) ?? throw new InvalidOperationException("the server closed");
                var reply = JsonDocument.Parse(line).RootElement;
                Assert.Equal(id, reply.GetProperty("id").GetInt32());
                return reply.GetProperty("result").Clone();
            }

            await Ask(1, "initialize", new { protocolVersion = "2024-11-05", capabilities = new { }, clientInfo = new { name = "test", version = "1" } });
            await keeper.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");

            var pids = new HashSet<int>();
            var peak = 0L;
            for (var call = 1; call <= Calls; call++)
            {
                var result = await Ask(call + 1, "tools/call", new { name = "keep", arguments = new { mb = CallMb } });
                var reply = JsonDocument.Parse(result.GetProperty("content")[0].GetProperty("text").GetString()!).RootElement;
                Assert.True(reply.GetProperty("initialized").GetBoolean(), $"call {call} reached a server that was never initialized");
                pids.Add(reply.GetProperty("pid").GetInt32());
                peak = Math.Max(peak, reply.GetProperty("rss").GetInt64());
                await Task.Delay(500, timeout.Token);
            }

            Assert.True(pids.Count >= 2, "the server was never started afresh");
            Assert.True(peak <= (CeilingMb + 2L * CallMb) * 1024 * 1024, $"the server reached {peak / 1024 / 1024} MB");
        }
        finally
        {
            keeper.Kill(entireProcessTree: true);
        }
    }

    private const string Browser = """
        import { createInterface } from 'node:readline';
        const kept = [];
        const say = (message) => process.stdout.write(JSON.stringify({ jsonrpc: '2.0', ...message }) + '\n');
        const text = (id, value) => say({ id, result: { content: [{ type: 'text', text: value }] } });
        for await (const line of createInterface({ input: process.stdin })) {
          const message = JSON.parse(line);
          if (message.method === 'initialize') say({ id: message.id, result: { protocolVersion: '2024-11-05', capabilities: {}, serverInfo: { name: 'stand-in', version: '1' } } });
          else if (message.method === 'tools/call') {
            const { name, arguments: args } = message.params;
            if (name === 'keep') { kept.push(Buffer.alloc(Number(args.mb) * 1024 * 1024, 1)); text(message.id, 'kept'); }
            else if (name === 'browser_snapshot') text(message.id, `snapshot from ${process.pid}`);
            else text(message.id, `clicked ${args.target}`);
          }
        }
        """;

    /// <summary>
    /// A call that names an element reference from before a restart is answered with a fresh snapshot and a sentence that says the
    /// reference is out of date, never forwarded to a server that does not know it; once answered, a reference from the new snapshot is
    /// forwarded unchanged.
    /// </summary>
    [Fact]
    public async Task A_call_naming_a_reference_from_before_a_restart_is_answered_with_a_fresh_snapshot()
    {
        var script = Path.Combine(_root, "browser.mjs");
        File.WriteAllText(script, Browser);
        var node = PackageStarts.Node(null, null) ?? throw new InvalidOperationException("node is not on PATH");
        var kept = ServerKeeper.Wrap(Path.Combine(_root, "home"), new("browser", node, [script], new Dictionary<string, string>()), CeilingMb, everyMs: 200);
        var start = new ProcessStartInfo(kept.Command) { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in kept.Arguments) start.ArgumentList.Add(argument);
        using var keeper = Process.Start(start)!;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            async Task<string> Call(int id, string method, object parameters)
            {
                await keeper.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters }));
                var line = await keeper.StandardOutput.ReadLineAsync(timeout.Token) ?? throw new InvalidOperationException("the server closed");
                var reply = JsonDocument.Parse(line).RootElement;
                Assert.Equal(id, reply.GetProperty("id").GetInt32());
                var result = reply.GetProperty("result");
                return result.TryGetProperty("content", out var content) ? content[0].GetProperty("text").GetString()! : result.GetRawText();
            }

            await Call(1, "initialize", new { protocolVersion = "2024-11-05", capabilities = new { }, clientInfo = new { name = "test", version = "1" } });
            await keeper.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");

            Assert.Equal("clicked e3", await Call(2, "tools/call", new { name = "browser_click", arguments = new { target = "e3" } }));

            await Call(3, "tools/call", new { name = "keep", arguments = new { mb = CeilingMb + 20 } });
            await Task.Delay(1500, timeout.Token);

            var stale = await Call(4, "tools/call", new { name = "browser_click", arguments = new { target = "e3" } });
            Assert.Contains("out of date", stale, StringComparison.Ordinal);
            Assert.Contains("snapshot from ", stale, StringComparison.Ordinal);
            Assert.DoesNotContain("clicked", stale, StringComparison.Ordinal);

            Assert.Equal("clicked e7", await Call(5, "tools/call", new { name = "browser_click", arguments = new { target = "e7" } }));
        }
        finally
        {
            keeper.Kill(entireProcessTree: true);
        }
    }
}
