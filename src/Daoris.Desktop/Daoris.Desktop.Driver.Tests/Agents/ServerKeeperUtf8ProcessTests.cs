using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Through real processes: a server's line whose multibyte character straddles two reads of its pipe reaches the chat whole.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ServerKeeperUtf8ProcessTests : IDisposable
{
    private const string Said = "页面快照 — 好";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "server keeper utf8", Guid.NewGuid().ToString("N")[..8]);

    public ServerKeeperUtf8ProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose() => GitSeed.Discard(_root);

    /// <summary>A server that says its answer in two writes, the first ending inside a three-byte character.</summary>
    private const string Splitting = """
        import { createInterface } from 'node:readline';
        const pause = (ms) => new Promise(resolve => setTimeout(resolve, ms));
        for await (const line of createInterface({ input: process.stdin })) {
          const message = JSON.parse(line);
          if (message.method !== 'tools/call') continue;
          const bytes = Buffer.from(JSON.stringify({ jsonrpc: '2.0', id: message.id, result: { content: [{ type: 'text', text: message.params.arguments.say }] } }) + '\n');
          const inside = bytes.indexOf(Buffer.from(message.params.arguments.say)) + 1;
          process.stdout.write(bytes.subarray(0, inside));
          await pause(300);
          process.stdout.write(bytes.subarray(inside));
        }
        """;

    [Fact]
    public async Task A_character_split_across_two_reads_of_the_servers_pipe_reaches_the_chat_whole()
    {
        var script = Path.Combine(_root, "splitting.mjs");
        File.WriteAllText(script, Splitting);
        var node = PackageStarts.Node(null, null) ?? throw new InvalidOperationException("node is not on PATH");
        var kept = ServerKeeper.Wrap(Path.Combine(_root, "home"), new("browser", node, [script], new Dictionary<string, string>()), 1024, everyMs: 5000);
        var start = new ProcessStartInfo(kept.Command)
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, StandardOutputEncoding = Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true,
        };
        foreach (var argument in kept.Arguments) start.ArgumentList.Add(argument);
        using var keeper = Process.Start(start)!;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            await keeper.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0", id = 7, method = "tools/call", @params = new { name = "say", arguments = new { say = Said } },
            }));
            var line = await keeper.StandardOutput.ReadLineAsync(timeout.Token) ?? throw new InvalidOperationException("the server closed");

            Assert.Equal(Said, JsonDocument.Parse(line).RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
        }
        finally
        {
            keeper.Kill(entireProcessTree: true);
        }
    }
}
