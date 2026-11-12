using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Through real processes: what the keeper does when starting the server afresh goes wrong, and what a call the client gave up on
/// leaves behind. A ceiling of one megabyte puts every idle moment past it, so the keeper restarts whenever nothing is in flight.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ServerKeeperRestartProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "server keeper restart", Guid.NewGuid().ToString("N")[..8]);

    public ServerKeeperRestartProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose() => GitSeed.Discard(_root);

    /// <summary>A server that answers the first start and exits at once on every start after it, as one that cannot start again does.</summary>
    private const string Mortal = """
        import { existsSync, writeFileSync } from 'node:fs';
        import { createInterface } from 'node:readline';
        if (existsSync(process.argv[2])) process.exit(3);
        writeFileSync(process.argv[2], 'started');
        const say = (message) => process.stdout.write(JSON.stringify({ jsonrpc: '2.0', ...message }) + '\n');
        for await (const line of createInterface({ input: process.stdin })) {
          const message = JSON.parse(line);
          if (message.method === 'initialize') say({ id: message.id, result: { protocolVersion: '2024-11-05', capabilities: {}, serverInfo: { name: 'stand-in', version: '1' } } });
        }
        """;

    /// <summary>A server that answers <c>echo</c> with its process id and never answers <c>hang</c>.</summary>
    private const string Silent = """
        import { createInterface } from 'node:readline';
        const say = (message) => process.stdout.write(JSON.stringify({ jsonrpc: '2.0', ...message }) + '\n');
        for await (const line of createInterface({ input: process.stdin })) {
          const message = JSON.parse(line);
          if (message.method === 'initialize') say({ id: message.id, result: { protocolVersion: '2024-11-05', capabilities: {}, serverInfo: { name: 'stand-in', version: '1' } } });
          else if (message.method === 'tools/call' && message.params.name === 'echo') say({ id: message.id, result: { content: [{ type: 'text', text: String(process.pid) }] } });
        }
        """;

    private const string Navigating = """
        import { appendFileSync } from 'node:fs';
        import { createInterface } from 'node:readline';
        const say = (message) => process.stdout.write(JSON.stringify({ jsonrpc: '2.0', ...message }) + '\n');
        for await (const line of createInterface({ input: process.stdin })) {
          const message = JSON.parse(line);
          if (message.method === 'initialize') say({ id: message.id, result: { protocolVersion: '2024-11-05', capabilities: {}, serverInfo: { name: 'stand-in', version: '1' } } });
          else if (message.method === 'tools/call' && message.params.name === 'browser_navigate') {
            appendFileSync(process.argv[2], `${process.pid} ${message.params.arguments.url}\n`);
            say({ id: message.id, result: { content: [{ type: 'text', text: `### Page\n- Page URL: ${message.params.arguments.url}\n- Page Title: work` }] } });
          }
        }
        """;

    private Process Keeper(string source, int everyMs, params string[] arguments)
    {
        var script = Path.Combine(_root, "stand-in.mjs");
        File.WriteAllText(script, source);
        var node = PackageStarts.Node(null, null) ?? throw new InvalidOperationException("node is not on PATH");
        var kept = ServerKeeper.Wrap(Path.Combine(_root, "home"), new("browser", node, [script, .. arguments], new Dictionary<string, string>()), ceilingMb: 1, everyMs: everyMs);
        var start = new ProcessStartInfo(kept.Command) { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in kept.Arguments) start.ArgumentList.Add(argument);
        return Process.Start(start)!;
    }

    private static async Task Send(Process keeper, object message) =>
        await keeper.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message));

    private static async Task<string> Echo(Process keeper, int id, CancellationToken ct)
    {
        await Send(keeper, new { jsonrpc = "2.0", id, method = "tools/call", @params = new { name = "echo", arguments = new { } } });
        var line = await keeper.StandardOutput.ReadLineAsync(ct) ?? throw new InvalidOperationException("the server closed");
        return JsonDocument.Parse(line).RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;
    }

    private static async Task Initialize(Process keeper, CancellationToken ct)
    {
        await Send(keeper, new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { protocolVersion = "2024-11-05", capabilities = new { }, clientInfo = new { name = "test", version = "1" } } });
        Assert.NotNull(await keeper.StandardOutput.ReadLineAsync(ct));
        await keeper.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
    }

    [Fact]
    public async Task A_server_that_cannot_start_again_ends_the_keeper_with_its_exit_code_rather_than_hanging_it()
    {
        using var keeper = Keeper(Mortal, 1000, Path.Combine(_root, "started"));
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await Initialize(keeper, timeout.Token);

            using var waiting = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await keeper.WaitForExitAsync(waiting.Token);

            Assert.Equal(3, keeper.ExitCode);
        }
        finally
        {
            if (!keeper.HasExited) keeper.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public async Task A_call_the_client_cancelled_no_longer_stops_the_keeper_restarting_the_server()
    {
        using var keeper = Keeper(Silent, 200);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await Initialize(keeper, timeout.Token);

            await Send(keeper, new { jsonrpc = "2.0", id = 2, method = "tools/call", @params = new { name = "hang", arguments = new { } } });
            await Send(keeper, new { jsonrpc = "2.0", method = "notifications/cancelled", @params = new { requestId = 2 } });
            await Task.Delay(1000, timeout.Token);

            var pids = new HashSet<string>();
            for (var id = 3; id < 15; id++)
            {
                pids.Add(await Echo(keeper, id, timeout.Token));
                await Task.Delay(300, timeout.Token);
            }

            Assert.True(pids.Count >= 2, "the keeper never started the server afresh after the cancelled call");
        }
        finally
        {
            keeper.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public async Task A_server_started_afresh_is_sent_to_the_page_the_session_was_on()
    {
        var opened = Path.Combine(_root, "opened.txt");
        using var keeper = Keeper(Navigating, 200, opened);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await Initialize(keeper, timeout.Token);
            await Send(keeper, new { jsonrpc = "2.0", id = 2, method = "tools/call", @params = new { name = "browser_navigate", arguments = new { url = "http://127.0.0.1:9/work" } } });
            Assert.Contains("Page URL", await keeper.StandardOutput.ReadLineAsync(timeout.Token), StringComparison.Ordinal);

            string[] Servers() => File.Exists(opened) ? [.. File.ReadAllLines(opened).Select(line => line.Split(' ')[0]).Distinct()] : [];
            while (Servers().Length < 2 && !timeout.IsCancellationRequested) await Task.Delay(200, CancellationToken.None);

            Assert.True(Servers().Length >= 2, "no server started afresh was sent to the session's page");
            Assert.All(File.ReadAllLines(opened), line => Assert.EndsWith(" http://127.0.0.1:9/work", line, StringComparison.Ordinal));

            await Send(keeper, new { jsonrpc = "2.0", id = 3, method = "tools/call", @params = new { name = "browser_navigate", arguments = new { url = "http://127.0.0.1:9/next" } } });
            var next = JsonDocument.Parse(await keeper.StandardOutput.ReadLineAsync(timeout.Token) ?? "{}").RootElement;
            Assert.Equal(3, next.GetProperty("id").GetInt32());
        }
        finally
        {
            keeper.Kill(entireProcessTree: true);
        }
    }
}
