using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDSYNC2 (D87's 2026-10-15 amendment point 7) through REAL ticks, one case per door a session goes on by in a tree that stands:
/// a resume (D79), a further part gone into its session's tree (SESSONE1d), a carry-on after a cut-off (D80), and the person's words
/// going on in the session's own conversation (ANSWER1a, MSG1b). Each time the session's first commit reached the line by a squash
/// while it was away; its next run starts on the line, and is told so. A real process (node) and a stand-in service on a loopback
/// port, the shapes <see cref="AskAndWaitTickTests"/> and <see cref="AnswerContinuesTickTests"/> use; no model and no account.
/// </summary>
/// <remarks>The repository has no <c>origin</c>, so nothing is fetched and nothing is said of a fetch: the line here is the line.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class StartInSyncTickTests : IDisposable
{
    private const string BroughtUp = "Before this turn Daoris brought this tree up to date with `main`: the line already holds 1 of its commit(s), "
                                     + "which were left out, and the tree now stands on `main` with nothing of its own beyond it.";

    private readonly string _home = Path.Combine(RepoRoot(), "_fixtures", "start-in-sync", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public StartInSyncTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "sync@example.com");
        Git("config", "user.name", "LANDSYNC2");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        GitSeed.Discard(_home);
    }

    private string Log => Path.Combine(_home, "agent.log");

    private JsonObject[] Runs() => [.. File.ReadAllLines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    /// <summary>A resume (D79): the session asked and waited; its commit was squash-merged meanwhile; resumed, it starts on the line.</summary>
    [Fact]
    public async Task A_resumed_session_starts_on_the_line_that_took_its_commit_and_is_told()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var driver = Driver(service, "ask");

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var tree = service.Session("s1")["tree"]!.GetValue<string>();
        SquashOntoLine(tree);
        service.Close("q2", "Done", "POST /notes takes { text }.");
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var resumed = Runs().Last();
        Assert.Contains("resuming quest `#q1`", resumed["target"]!.GetValue<string>());
        Assert.Contains(BroughtUp, resumed["target"]!.GetValue<string>());
        Assert.Equal(Rev("main"), resumed["head"]!.GetValue<string>());
        Assert.Equal("Done", service.Status("q1"));
    }

    /// <summary>A further part (SESSONE1d) gone into its session's tree, whose first part's commit the line came to hold, starts on the line.</summary>
    [Fact]
    public async Task A_further_part_gone_into_its_sessions_tree_starts_on_the_line_and_is_told()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Entries["q1"] = "ask:a1/engine";
        var driver = Driver(service, "ask");

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var tree = service.Session("s1")["tree"]!.GetValue<string>();
        SquashOntoLine(tree);
        service.Keep(new JsonObject { ["id"] = "b1", ["repository"] = "backend", ["state"] = "completed", ["entry"] = "ask:a1/backend" });
        service.Add(new JsonObject
        {
            ["id"] = "q3", ["from"] = "backend", ["to"] = "engine", ["title"] = "Keep the note's author too",
            ["body"] = "The notes endpoint takes an author.", ["status"] = "Open", ["publishedBy"] = "b1",
        });
        service.Entries["q3"] = "ask:a1/engine";
        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        Assert.Equal("s1", Assert.Single(look.Considerations, c => c.Quest.Id == "q3").Entry?.Into?.Session);
        var part = Runs().Last();
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(part["cwd"]!.GetValue<string>()), ignoreCase: true);
        Assert.Contains("This quest continues a line of work already under way in `engine`", part["target"]!.GetValue<string>());
        Assert.Contains(BroughtUp, part["target"]!.GetValue<string>());
        Assert.Equal(Rev("main"), part["head"]!.GetValue<string>());
    }

    /// <summary>A carry-on (D80): the session took the quest, committed, and failed before closing it; carried on, it starts on the line.</summary>
    [Fact]
    public async Task A_carry_on_after_a_cut_off_starts_on_the_line_and_is_told()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var driver = Driver(service, "fail");

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal("failed", service.Session("s1")["state"]!.GetValue<string>());
        var tree = service.Session("s1")["tree"]!.GetValue<string>();
        SquashOntoLine(tree);
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var carried = Runs().Last();
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(carried["cwd"]!.GetValue<string>()), ignoreCase: true);
        Assert.Contains("You are carrying on quest `#q1`", carried["target"]!.GetValue<string>());
        Assert.Contains(BroughtUp, carried["target"]!.GetValue<string>());
        Assert.Equal(Rev("main"), carried["head"]!.GetValue<string>());
        Assert.Equal("Done", service.Status("q1"));
    }

    /// <summary>
    /// SYNCMERGE1b (D178 point 3): a carry-on of a session whose take lost, once the person brought its work forward. The session that
    /// carried the cut-off on stood down in its tree when its take lost, and the line came to hold the tree's commit meanwhile; once the
    /// claim names the bring-forward, that stand-down goes on in the tree, from the line, told the press and never its stop's words.
    /// </summary>
    [Fact]
    public async Task A_bring_forwards_carry_on_starts_on_the_line_in_the_stood_down_tree_and_is_told_the_press()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var driver = Driver(service, "fail");

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal("failed", service.Session("s1")["state"]!.GetValue<string>());
        var tree = service.Session("s1")["tree"]!.GetValue<string>();
        SquashOntoLine(tree);
        var lostAt = DateTimeOffset.UtcNow;
        service.Keep(new JsonObject
        {
            ["id"] = "s2", ["quest"] = "q1", ["repository"] = "engine", ["state"] = "stood-down", ["kind"] = "drive", ["tree"] = tree,
            ["note"] = Daoris.Driver.Driver.LostClaim, ["created"] = lostAt.ToString("O"),
        });
        service.Claim = "held";
        service.ClaimForward = new JsonObject
        {
            ["machine"] = "desk", ["sequence"] = 7, ["at"] = lostAt.AddSeconds(1).ToString("O"), ["standing"] = "held",
        };
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var carried = Runs().Last();
        var told = carried["target"]!.GetValue<string>();
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(carried["cwd"]!.GetValue<string>()), ignoreCase: true);
        Assert.Contains("You are carrying on quest `#q1`", told);
        Assert.Contains("the person brought that work forward", told);
        Assert.DoesNotContain("double someone else's", told);
        Assert.Contains(BroughtUp, told);
        Assert.Equal(Rev("main"), carried["head"]!.GetValue<string>());
        Assert.Equal("Done", service.Status("q1"));
    }

    /// <summary>
    /// The person's words going on in the session's own conversation (ANSWER1a, MSG1b): no instruction is handed again, so what the
    /// start did with the tree is said after the words, in the same prompt, and the conversation goes on from the line.
    /// </summary>
    [Fact]
    public async Task Words_going_on_in_the_sessions_own_conversation_start_on_the_line_and_say_so_after_the_words()
    {
        await using var service = AnswerContinuesTickTests.ParkStandIn.Start(_repository);
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TimeoutMinutes = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", AcpAgent(), Log] },
        };
        var driver = new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var parked = service.Session("s1");
        Assert.Equal("awaiting-person", parked["state"]!.GetValue<string>());
        SquashOntoLine(parked["tree"]!.GetValue<string>());
        var (answered, message) = await new ServiceClient(service.Url, null).AnswerSessionAsync("s1", "Port 8080.");
        Assert.True(answered, message);
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var prompt = Runs().Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.True(prompt["resumed"]!.GetValue<bool>());
        Assert.StartsWith("Port 8080.", prompt["prompt"]!.GetValue<string>());
        Assert.Contains(BroughtUp, prompt["prompt"]!.GetValue<string>());
        Assert.Equal(Rev("main"), prompt["head"]!.GetValue<string>());
        Assert.Equal(1, service.SessionCount);
    }

    /// <summary>
    /// HEAVYRUN1b2a2: a driven start looks this machine's heavy gates up under the repository's name the quest carries (the name
    /// <c>DAORIS_REPOSITORY</c> hands <c>run-heavy</c>), not the checkout's folder, which a repository registered under another name
    /// does not share.
    /// </summary>
    [Fact]
    public async Task A_start_is_told_the_heavy_gates_mapped_under_the_quests_repository_not_its_checkouts_folder()
    {
        var install = Path.Combine(_home, "install");
        var bin = Path.Combine([install, .. Tools.InstallBinLayout]);
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "daoris-driver.cmd"), "");
        var home = Path.Combine(install, DaorisHome.InstallData);
        Directory.CreateDirectory(home);
        File.WriteAllText(DaorisHome.Entry(home, HomeEntry.DriverConfig), """{"heavyGates":{"engine":[{"name":"build","run":"dotnet build"},{"name":"e2e","run":"npm run e2e"}]}}""");
        // The repository the quest names `engine` sits in a folder of another name.
        var checkout = Path.Combine(_home, "shop-checkout");
        Directory.CreateDirectory(checkout);
        GitIn(checkout, "init", "-q", "-b", "main");
        GitIn(checkout, "config", "user.email", "sync@example.com");
        GitIn(checkout, "config", "user.name", "HEAVYRUN1b2a2");
        File.WriteAllText(Path.Combine(checkout, "README.md"), "# engine\n");
        GitIn(checkout, "add", "-A");
        GitIn(checkout, "commit", "-qm", "the starting point");

        await using var service = AskAndWaitTickTests.StandIn.Start(checkout);
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"], Trees = ["engine"], Adapter = "stub", TimeoutMinutes = 1, PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent(), Log, "ask"] },
        };
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(home, HomeEntry.Harnesses)));

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        Assert.Contains(HeavyTold.Words(["build", "e2e"]), Runs().First()["target"]!.GetValue<string>());
    }

    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, string mode)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "stub",
            TimeoutMinutes = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent(), Log, mode] },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// The pipe door's stand-in harness: it writes down where it ran, what it was handed and the commit its tree stood on. A first
    /// start takes the quest and commits the fix in its tree, then asks `backend` and waits (<c>ask</c>) or fails before closing it
    /// (<c>fail</c>); a resume or a carry-on closes it; a further part only says where it ran.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, writeFileSync } from 'node:fs';
            import { execFileSync } from 'node:child_process';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const [log, mode] = process.argv.slice(2);
            const target = process.env.DAORIS_TARGET ?? '';
            const git = (...args) => execFileSync('git', args, { encoding: 'utf8' }).trim();
            appendFileSync(log, JSON.stringify({ cwd: process.cwd(), target, head: git('rev-parse', 'HEAD') }) + '\n');
            if (target.includes('line of work already under way')) process.exit(0);
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            if (target.includes('resuming quest') || target.includes('carrying on quest')) {
              await respond({ action: 'done', reason: 'landed, on the line' });
              process.exit(0);
            }
            await respond({ action: 'take' });
            writeFileSync('fix.txt', 'the fix\n');
            git('add', '-A');
            git('commit', '-qm', 'the fix');
            if (mode === 'fail') process.exit(1);
            await respond({ action: 'wait', on: 'q2' });
            """);
        return script;
    }

    /// <summary>
    /// The protocol door's stand-in, as <see cref="AnswerContinuesTickTests"/>'s: its first prompt takes the quest, commits the fix in
    /// its tree and asks which port; told the answer on its resumed conversation, it closes the quest. Each prompt goes to the log with
    /// the commit its tree stood on.
    /// </summary>
    private string AcpAgent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync, writeFileSync } from 'node:fs';
            import { execFileSync } from 'node:child_process';
            const [log] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const git = (...args) => execFileSync('git', args, { encoding: 'utf8' }).trim();
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, cwd: process.cwd(), head: git('rev-parse', 'HEAD'), session }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            let conversation = null;
            let resumed = false;
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + session;
                heard({ method: 'session/new', conversation });
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                conversation = frame.params.sessionId;
                resumed = true;
                heard({ method: frame.method, conversation });
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                const prompt = (frame.params?.prompt ?? []).map((block) => block.text ?? '').join('\n');
                heard({ method: 'session/prompt', conversation: frame.params?.sessionId, prompt, resumed });
                if (resumed) {
                  say('Listening on the port you named.');
                  await respond({ action: 'done', reason: 'listening on the port the person named' });
                } else {
                  await respond({ action: 'take' });
                  writeFileSync('fix.txt', 'the fix\n');
                  git('add', '-A');
                  git('commit', '-qm', 'the fix');
                  say('Which port should it listen on?');
                }
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            // stdin closed: the ending. Never process.exit (STUB1): the process ends when the loop drains.
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    /// <summary>The platform's squash merge of the session's one commit onto the line, as its pull request would land it.</summary>
    private void SquashOntoLine(string tree)
    {
        var branch = GitIn(tree, "rev-parse", "--abbrev-ref", "HEAD");
        Assert.StartsWith("daoris/", branch);
        Git("merge", "--squash", branch);
        Git("commit", "-qm", $"{branch} (squashed)");
        Assert.Equal("", GitIn(_repository, "status", "--porcelain"));
        Assert.NotEqual(Rev("main"), GitIn(tree, "rev-parse", "HEAD"));
    }

    private string Rev(string revision) => GitIn(_repository, "rev-parse", revision);

    private void Git(params string[] arguments) => GitIn(_repository, arguments);

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
