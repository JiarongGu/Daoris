using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDSYNC2b (D87's 2026-10-15 amendment point 7) with a real conversation: a chat in a tree of its own commits, ends, and the
/// platform squash-merges its commit onto the line while it is away. Written to again, it goes on in its own conversation from the
/// line, and is told so after the person's words, as a driven session's words going on are (<see cref="StartInSyncTickTests"/>).
/// </summary>
/// <remarks>
/// A real process (node) on the protocol door, with no model and no account, and the in-process ledger of
/// <see cref="ChatGoOnTests"/>; the words reach the record as <see cref="ChatGoesOnProcessTests"/> says them. The repository has no
/// <c>origin</c>, so nothing is fetched and nothing is said of a fetch: the line here is the line.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ChatGoesOnInSyncProcessTests : IDisposable
{
    private const string BroughtUp = "Before this turn Daoris brought this tree up to date with `main`: the line already holds 1 of its commit(s), "
                                     + "which were left out, and the tree now stands on `main` with nothing of its own beyond it.";

    private readonly string _home = Path.Combine(RepoRoot(), "_fixtures", "chat-in-sync", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public ChatGoesOnInSyncProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "sync@example.com");
        Git("config", "user.name", "LANDSYNC2b");
        File.WriteAllText(Path.Combine(_root, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        GitSeed.Discard(_home);
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private string[] HeardLines() => StubFile.Lines(Heard);

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private string Seen(ChatLedger ledger, string id) => $"heard [{string.Join(" | ", HeardLines())}], state {ledger.State(id)}";

    /// <summary>
    /// 🔴 The owner's case for a conversation: its first commit reached the line by a squash while it was away; written to, it
    /// resumes its own conversation on the line's tip, its words first and what its start did after them, and its tree holds nothing
    /// the line already has.
    /// </summary>
    [Fact]
    public async Task A_conversation_going_on_in_its_own_tree_starts_from_the_line_that_took_its_commit_and_is_told_after_the_words()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var events = Events;
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));

        var id = (await runner.StartAsync("engine", "acp-stub", config, ownTree: true)).SessionId!;
        await Poll.Until(() => ledger.State(id) == "working", () => Seen(ledger, id));
        Assert.True(runner.Say(id, "first"));
        await Poll.Until(() => HeardLines().Any(line => line.StartsWith("committed", StringComparison.Ordinal)), () => Seen(ledger, id));
        Assert.True(runner.Finish(id));
        await Poll.Until(() => ledger.State(id) == "completed", () => Seen(ledger, id));
        var tree = HeardLines().First(line => line.StartsWith("cwd: ", StringComparison.Ordinal))["cwd: ".Length..];
        Assert.False(string.Equals(Path.GetFullPath(tree), Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase));
        SquashOntoLine(tree);

        ledger.For(id).Say("w1", "Also log the port.");
        events.Append(id, new SessionEvent
        {
            Kind = SessionEventKind.User, Origin = "person", Id = "w1", Text = "Also log the port.", Reaches = "resume", Door = "screen",
        });
        await Poll.Until(() => HeardLines().Any(line => line.StartsWith("prompt: Also log the port.", StringComparison.Ordinal)), () => Seen(ledger, id));
        Assert.True(runner.Finish(id));
        await Poll.Until(() => ledger.State(id) == "completed" && ledger.Moves(id).Count(move => move == "completed") == 2, () => Seen(ledger, id));

        Assert.Contains("session/resume: conv-1", HeardLines());
        Assert.Contains($"prompt: Also log the port. | {BroughtUp} Read the log before you go on: the commits you made have new ids.", HeardLines());
        var head = Rev("main");
        Assert.Equal($"head: {head}", HeardLines().Last(line => line.StartsWith("head: ", StringComparison.Ordinal)));
        Assert.Equal(head, GitIn(tree, "rev-parse", "HEAD"));
        Assert.Equal(1, ledger.Count);
    }

    /// <summary>
    /// The protocol door's stand-in: it names its conversation <c>conv-1</c> and resumes it; each prompt goes to the file with the
    /// commit its tree stood on, and its first one commits the fix in its tree and says where it ran.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-in-sync-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, writeFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            import { execFileSync } from 'node:child_process';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard] = process.argv.slice(2);
            const note = (text) => appendFileSync(heard, text + '\n');
            const git = (...args) => execFileSync('git', args, { encoding: 'utf8' }).trim();
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let conversation = null;
            let resumed = false;
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-1';
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                note(`${frame.method}: ${frame.params.sessionId}`);
                conversation = frame.params.sessionId;
                resumed = true;
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                const text = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                note('prompt: ' + text);
                note('head: ' + git('rev-parse', 'HEAD'));
                if (!resumed) {
                  note('cwd: ' + process.cwd());
                  writeFileSync('fix.txt', 'the fix\n');
                  git('add', '-A');
                  git('commit', '-qm', 'the fix');
                  note('committed');
                }
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'heard ' + text } } } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.method === 'session/close') {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            // stdin closed: the ending. Never process.exit (STUB1): the process ends when the loop drains.
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    /// <summary>The platform's squash merge of the conversation's one commit onto the line, as its pull request would land it.</summary>
    private void SquashOntoLine(string tree)
    {
        var branch = GitIn(tree, "rev-parse", "--abbrev-ref", "HEAD");
        Assert.StartsWith("daoris/", branch);
        Git("merge", "--squash", branch);
        Git("commit", "-qm", $"{branch} (squashed)");
        Assert.Equal("", GitIn(_root, "status", "--porcelain"));
        Assert.NotEqual(Rev("main"), GitIn(tree, "rev-parse", "HEAD"));
    }

    private string Rev(string revision) => GitIn(_root, "rev-parse", revision);

    private void Git(params string[] arguments) => GitIn(_root, arguments);

    private static string GitIn(string place, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = place, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var stderr = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        _ = stderr.Result;
        return output.Trim();
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot() => GitSeed.RepoRoot();
}
