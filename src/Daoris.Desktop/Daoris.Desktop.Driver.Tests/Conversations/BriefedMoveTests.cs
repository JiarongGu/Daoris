using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 a-iii (D212, the design's §3.2 step 6 and §7 row 4): the briefed move, *Go on with Daoris's brief* and the same move by
/// itself where a conversation's own conversation is proven lost, takes the walk with its own account first where that is ready, rather
/// than opening on its own account as a pick, which refuses exactly while the account cools, when the person needs the part most. The
/// person's press is the one exception to an automatic part's same-account refusal, and it takes one selection, counted once. An account
/// the person picked (row a-ii) stays the one account: its part waits for it as before.
/// </summary>
/// <remarks>
/// The conversations run on the <c>acp-stub</c> adapter, a node stand-in on the protocol door whose accounts are the stub's (TOOL4j): no
/// model, no account, no sign-in, no real agent binary. Every case has its own scratch home under <c>_fixtures/</c> and sets no variable
/// of the process's environment, <c>DAORIS_HOME</c> least.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class BriefedMoveTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "briefed-move", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public BriefedMoveTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        }

        new HarnessSettings().WithRotation("stub", ["account-1", "account-2"]).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private DriverConfig Config()
    {
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["acp-stub"] = ["node", Agent(), Heard],
                ["stub"] = ["node", TextAgent()],
            },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return config;
    }

    private (ChatRunner Runner, ChatLedger Ledger, ServiceClient Client, HarnessRoster Roster) Runner()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var client = ledger.Client();
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses));
        var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: Events, harnesses: roster);
        return (runner, ledger, client, roster);
    }

    /// <summary>An account cooling for days, as the cooling store keeps it.</summary>
    private void Cooling(string account)
    {
        var now = DateTimeOffset.UtcNow;
        AccountCooling.Cool(_home, new CoolingEntry("stub", account, now.AddDays(4), Stated: true, "weekly", now, null), now);
    }

    private IReadOnlyList<string> Prompts => [.. StubFile.Lines(Heard).Where(line => line.StartsWith("prompt[", StringComparison.Ordinal))];

    private string Seen(ChatLedger ledger, string id) =>
        $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], records {ledger.Count}, state {ledger.State(id)}, "
        + $"record [{string.Join(" | ", Events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Origin}:{e.Text}"))}]";

    /// <summary>A conversation that ended on <paramref name="account"/> with the person's words waiting for it to go on.</summary>
    private void Waiting(ChatLedger ledger, string id, string account) =>
        ledger.Chat(id, "completed", "acp-stub", _root, profile: account).Say("w1", "Also log the port.");

    /// <summary>
    /// 🔴 The person's press for a conversation whose own account is ready opens the new part on that account, though the walk's own
    /// choice for a start would be the other, and counts the one start once: the brief and the words go there, and the old record's words
    /// leave it as taken by the new part.
    /// </summary>
    [Fact]
    public async Task The_persons_press_opens_on_the_own_account_where_it_is_ready()
    {
        var config = Config();
        var (runner, ledger, client, roster) = Runner();
        using var r = runner;
        using var c = client;
        Waiting(ledger, "c1", "account-2");
        var (counted, walks) = (roster.Mark(), roster.Selected);

        var answer = await runner.GoOnBriefedAsync("c1", config);

        Assert.True(answer.Sent, answer.Message);
        var next = (await client.RecordAsync(answer.SessionId!))!;
        Assert.Equal(("account-2", "c1", HandOverWhy.Resume, HandOverBy.Person), (next.Profile, next.From, next.FromWhy, next.FromBy));
        Assert.Equal((counted + 1, walks + 1), (roster.Mark(), roster.Selected));
        await Poll.Until(() => Prompts.Any(line => line.StartsWith("prompt[account-2]", StringComparison.Ordinal)), () => Seen(ledger, "c1"));
        Assert.Single(Prompts, line => line.Contains("Also log the port.", StringComparison.Ordinal));
        Assert.Empty(ledger.Said("c1"));
    }

    /// <summary>
    /// 🔴 The person's press for a conversation whose own account cools for days with another of its agent's ready: the new part walks to
    /// the ready account, never refused on the cooling one, and counts one start, which is the account it opens on.
    /// </summary>
    [Fact]
    public async Task The_persons_press_for_a_conversation_whose_account_cools_opens_on_another_ready_account()
    {
        var config = Config();
        var (runner, ledger, client, roster) = Runner();
        using var r = runner;
        using var c = client;
        Waiting(ledger, "c1", "account-1");
        Cooling("account-1");
        var counted = roster.Mark();

        var answer = await runner.GoOnBriefedAsync("c1", config);

        Assert.True(answer.Sent, answer.Message);
        var next = (await client.RecordAsync(answer.SessionId!))!;
        Assert.Equal(("account-2", "c1", HandOverWhy.Resume, HandOverBy.Person), (next.Profile, next.From, next.FromWhy, next.FromBy));
        Assert.Equal(counted + 1, roster.Mark());
        await Poll.Until(() => Prompts.Any(line => line.StartsWith("prompt[account-2]", StringComparison.Ordinal)), () => Seen(ledger, "c1"));
        Assert.Empty(ledger.Said("c1"));
        Assert.Equal(2, ledger.Count);
    }

    /// <summary>
    /// With every account of its agent cooling the press opens nothing and counts nothing: refused saying the wait, its words staying
    /// where they were.
    /// </summary>
    [Fact]
    public async Task The_persons_press_with_every_account_cooling_opens_nothing_and_counts_nothing()
    {
        var config = Config();
        var (runner, ledger, client, roster) = Runner();
        using var r = runner;
        using var c = client;
        Waiting(ledger, "c1", "account-1");
        Cooling("account-1");
        Cooling("account-2");
        var counted = roster.Mark();

        var answer = await runner.GoOnBriefedAsync("c1", config);

        Assert.False(answer.Sent);
        Assert.Null(answer.SessionId);
        Assert.False(string.IsNullOrWhiteSpace(answer.Message));
        Assert.Equal((1, counted), (ledger.Count, roster.Mark()));
        Assert.Equal(["w1"], ledger.Said("c1"));
        Assert.Empty(StubFile.Lines(Heard));
    }

    /// <summary>
    /// An account the person picked (row a-ii) stays the one account, as D205 §3.5(d) has it: its part waits for it while it cools, though
    /// another account is ready, and opens on it where it is ready.
    /// </summary>
    [Fact]
    public async Task A_conversation_whose_account_the_person_picked_keeps_to_it_in_the_new_part()
    {
        var config = Config();
        var (runner, ledger, client, roster) = Runner();
        using var r = runner;
        using var c = client;
        Waiting(ledger, "c1", "account-1");
        new HarnessConversations(_home).Pick("c1", "account-1");
        Cooling("account-1");
        var counted = roster.Mark();

        var refused = await runner.GoOnBriefedAsync("c1", config);

        Assert.False(refused.Sent);
        Assert.Null(refused.SessionId);
        Assert.Equal((1, counted), (ledger.Count, roster.Mark()));
        Assert.Equal(["w1"], ledger.Said("c1"));
        Assert.Empty(StubFile.Lines(Heard));

        // Its account ready again, the same press opens there.
        Assert.True(AccountCooling.End(_home, "stub", "account-1", DateTimeOffset.UtcNow));
        var answer = await runner.GoOnBriefedAsync("c1", config);
        Assert.True(answer.Sent, answer.Message);
        Assert.Equal("account-1", (await client.RecordAsync(answer.SessionId!))!.Profile);
    }

    /// <summary>
    /// A conversation whose own conversation is proven lost goes on by itself in the same move: no resume to keep, so its own account where
    /// that is ready, and the walk's pick where it cools, asked by Daoris and counted once.
    /// </summary>
    [Theory]
    [InlineData(false, "account-2")]
    [InlineData(true, "account-2")]
    public async Task A_lost_conversation_goes_on_by_the_walk_with_its_own_account_first_where_ready(bool cooling, string opens)
    {
        var config = Config();
        var (runner, ledger, client, roster) = Runner();
        using var r = runner;
        using var c = client;
        Waiting(ledger, "c1", cooling ? "account-1" : opens);
        if (cooling) Cooling("account-1");
        var counted = roster.Mark();

        var answer = await runner.GoOnLostAsync("c1", ContinueWhy.Of(ContinueWhy.Gone), config);

        Assert.True(answer.Sent, answer.Message);
        var next = (await client.RecordAsync(answer.SessionId!))!;
        Assert.Equal((opens, "c1", HandOverWhy.Resume, HandOverBy.Daoris), (next.Profile, next.From, next.FromWhy, next.FromBy));
        Assert.Equal(counted + 1, roster.Mark());
    }

    /// <summary>A stand-in ACP agent on the stub's accounts: it names its conversation and writes each prompt down with its account.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-briefed-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let conversation = null;
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + account;
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/prompt') {
                const text = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                note(`prompt[${account}]: ` + text.replace(/\r?\n/g, ' '));
                await new Promise((resolve) => setTimeout(resolve, 50));
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'heard it' } } } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.method === 'session/close') {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    /// <summary>The stub's command: it answers the sign-in question for the stub's accounts, as the protocol stand-in's owner.</summary>
    private string TextAgent()
    {
        var script = Path.Combine(_home, "text-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('stub 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => console.log('heard: ' + line));
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }
}
