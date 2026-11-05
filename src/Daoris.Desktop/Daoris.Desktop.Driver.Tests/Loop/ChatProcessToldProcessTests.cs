using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PROCESS1d-iii (D164's amendment point 4): a conversation in a tree of its own is told its repository's process once, with its first
/// message and before the person's words, and kept in the record as a note of its own; its next message is told nothing of it again.
/// One in the person's checkout is told nothing of it at all.
/// </summary>
/// <remarks>
/// A real process (node) on the protocol door, with no model and no account, and the in-process ledger of <see cref="ChatGoOnTests"/>:
/// each prompt reaches the file whole, so the assertions are about what the harness heard, not what the driver meant to send.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ChatProcessToldProcessTests : IDisposable
{
    private const string Lead = "This repository's process on this machine, set for its repository:";

    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "chat-process", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public ChatProcessToldProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "process@example.com");
        Git("config", "user.name", "PROCESS1d");
        File.WriteAllText(Path.Combine(_root, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        GitSeed.Discard(_home);
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private string[] Prompts() =>
        [.. StubFile.Lines(Heard).Where(line => line.StartsWith("prompt ", StringComparison.Ordinal))
            .Select(line => JsonSerializer.Deserialize<string>(line["prompt ".Length..])!)];

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_conversation_in_its_own_tree_is_told_its_process_once_before_its_first_words(bool ownTree)
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
            Commits = new Dictionary<string, CommitRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["engine"] = new(CommitRules.Ask, "Subject in the imperative."),
            },
        };
        var events = Events;
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));

        var id = (await runner.StartAsync("engine", "acp-stub", config, ownTree: ownTree)).SessionId!;
        await Poll.Until(() => ledger.State(id) == "working", () => string.Join(" | ", StubFile.Lines(Heard)));
        Assert.True(runner.Say(id, "open the board"));
        await Poll.Until(() => Prompts().Length == 1, () => string.Join(" | ", StubFile.Lines(Heard)));
        Assert.True(runner.Say(id, "and the next page"));
        await Poll.Until(() => Prompts().Length == 2, () => string.Join(" | ", StubFile.Lines(Heard)));

        var prompts = Prompts();
        Assert.Equal(ownTree, prompts[0].Contains(Lead, StringComparison.Ordinal));
        Assert.Equal(ownTree, prompts[0].Contains("- Ask the person before each commit, and commit only on their yes.", StringComparison.Ordinal));
        Assert.Equal(ownTree, prompts[0].Contains("\n> Subject in the imperative.\n", StringComparison.Ordinal));
        if (ownTree) Assert.True(prompts[0].IndexOf(Lead, StringComparison.Ordinal) < prompts[0].IndexOf("open the board", StringComparison.Ordinal));
        Assert.DoesNotContain("This repository's process on this machine", prompts[1]);

        var told = events.Page(id).Events.Where(each => each.Parts?.Any(part => part.Code == "chat.told-process") == true).ToList();
        Assert.Equal(ownTree ? 1 : 0, told.Count);
        Assert.Equal(["open the board", "and the next page"], events.Page(id).Events.Where(each => each.Kind == SessionEventKind.User).Select(each => each.Text));
        Assert.True(runner.Finish(id));
    }

    /// <summary>The protocol door's stand-in: each prompt's text blocks reach the file as one JSON line, newlines kept.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-process-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard] = process.argv.slice(2);
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'conv-1' } });
              } else if (frame.method === 'session/prompt') {
                const raw = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                const text = raw.replace(/(\n\n| \| )?Daoris's standing block for this session: [\s\S]*$/, '');
                appendFileSync(heard, 'prompt ' + JSON.stringify(text) + '\n');
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var stderr = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        _ = stderr.Result;
    }
}
