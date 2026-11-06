using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1c (the co-agent design §2.3 step 9, §8.3, §9 row 3): a conversation leads a piece. The person's ask for a piece is taken for a
/// conversation working in a tree of its own, and the piece's answer, handed by the look through the host's partner door, reaches the
/// conversation as a turn of its own between its turns: kept while its turn runs, said once that turn has ended, as Daoris's words around
/// the partner's claim and never the person's.
/// </summary>
/// <remarks>
/// A real conversation on the ACP stand-in, run by the real runner over the in-process ledger of <see cref="ChatGoOnTests"/>, and the real
/// look's hand over the same record, so the class is in the real-process half. The partner's own pass (its clone, its job, its keeping) is
/// <see cref="PartnerProcessTests"/>': here the piece stands answered, as that pass leaves it.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class PartnerChatProcessTests : IDisposable
{
    private const string Partner = "pt1";

    private const string Word = "wp1";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-partner-chat-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public PartnerChatProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private DriverConfig Config()
    {
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return config;
    }

    private string Seen(ChatLedger ledger, SessionEvents events, string id) =>
        $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], state {ledger.State(id)}, "
        + $"record [{string.Join(" | ", events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Origin}:{e.Text}"))}]";

    /// <summary>
    /// 🔴 The conversation leads the piece: the person's ask is written for it, and the answer handed while its turn runs is said as its
    /// next turn, after that turn's end, once, and recorded as no person's words.
    /// </summary>
    [Fact]
    public async Task A_conversation_leads_a_piece_and_hears_its_answer_as_a_turn_of_its_own_between_turns()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        var adapters = ProviderRegistry.Built();
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        var lead = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        await Poll.Until(() => ledger.State(lead) == "working", () => Seen(ledger, events, lead));

        // 🔴 A conversation in a tree of its own leads a piece: the person's ask is written for it, at its tree's commit.
        var head = new string('a', 40);
        var asked = await PartnerPersonAsk.AskAsync(
            _home, client, lead, "Write the port's test.", ["src"], "codex-acp", (_, _) => Task.FromResult(new PieceBase(head, [])), DateTimeOffset.UtcNow);
        Assert.True(asked.Asked is not null, asked.Message);
        var files = new PartnerFiles(_home);
        var ask = files.ReadAsk(asked.Asked!)!;
        Assert.Equal((lead, head), (ask.Asker, ask.Base));

        // The piece stands answered, as the partner's pass leaves it.
        var commit = new string('b', 40);
        files.WriteState(new PartnerState(ask.Id, PartnerStates.Answered, null, DateTimeOffset.UtcNow)
        {
            Partner = Partner, Agent = "codex-acp", Commit = commit, CopyRemoved = true,
        });
        files.WriteFacts(new PartnerFacts(ask.Id, commit, ["src/port.test.ts"], [], [], DateTimeOffset.UtcNow));
        Assert.True(files.WriteAnswer(new PartnerAnswer(ask.Id, "The port's test is written.", null, DateTimeOffset.UtcNow) { Partner = Partner }));

        // A turn runs as the look hands the answer.
        Assert.True(runner.Say(lead, "slow"));
        await Poll.Until(() => StubFile.Lines(Heard).Contains("prompt: slow") && runner.Taking(lead), () => Seen(ledger, events, lead));
        using var door = new PartnerDoor(ledger, lead, _root);
        using var hands = new ServiceClient(ChatLedger.Url, null, new HttpClient(door, disposeHandler: false));
        var driver = new Daoris.Driver.Driver(hands, config, adapters, _home, events: events);
        await driver.PiecesAsync(new Snapshot([], [], []), 0, [], CancellationToken.None);

        Assert.Equal(PartnerStates.Handed, files.ReadState(ask.Id)!.State);
        Assert.Equal((PartnerDelivery.Taken, lead), (files.ReadReceipt(ask.Id)!.Outcome, files.ReadReceipt(ask.Id)!.To));
        var hand = Assert.Single(door.Hands);
        Assert.Contains($"git merge --no-ff {PieceRefs.Of(ask.Id)}", hand);
        Assert.True(runner.Taking(lead), "the answer was handed while the conversation's turn ran");

        // 🔴 Said as the conversation's next turn, after the turn that ran ended, once.
        await Poll.Until(() => StubFile.Text(Heard).Contains($"git merge --no-ff {PieceRefs.Of(ask.Id)}"), () => Seen(ledger, events, lead));
        var heard = StubFile.Text(Heard);
        Assert.True(heard.IndexOf("ended: slow", StringComparison.Ordinal) < heard.IndexOf("git merge --no-ff", StringComparison.Ordinal), heard);
        await Poll.Until(() => ledger.Taken.Any(taken => taken.Session == lead && taken.Ids.Contains(Word, StringComparison.Ordinal)), () => Seen(ledger, events, lead));
        await Poll.Until(() => !runner.Taking(lead), () => Seen(ledger, events, lead));
        Assert.Equal(1, heard.Split("git merge --no-ff", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain(
            events.After(lead, 0).Events,
            e => e.Kind == SessionEventKind.User && e.Origin == "person" && (e.Text ?? "").Contains("git merge", StringComparison.Ordinal));
    }

    /// <summary>
    /// The local host's partner door over the ledger (<c>POST /api/partners/{partner}/hand</c>): the word kept on the lead's record marked
    /// with the partner, as the service's <c>PartnerDesk.HandAsync</c> keeps it, and answered with the record that holds it. Every other
    /// request is the ledger's.
    /// </summary>
    private sealed class PartnerDoor(ChatLedger ledger, string lead, string tree) : HttpMessageHandler
    {
        // The ledger is the runner's too: this door sends through it and never disposes it.
        private readonly HttpMessageInvoker _ledger = new(ledger, disposeHandler: false);

        public List<string> Hands { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            if (request.Method != HttpMethod.Post || !path.StartsWith("/api/partners/", StringComparison.Ordinal) || !path.EndsWith("/hand", StringComparison.Ordinal))
            {
                return await _ledger.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            var text = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))!["text"]!.GetValue<string>();
            lock (Hands) Hands.Add(text);
            new ChatLedger.Words(ledger, lead).Helped(Word, text, Partner);
            var answer = new JsonObject
            {
                ["message"] = "kept",
                ["said"] = new JsonObject { ["id"] = Word, ["text"] = text },
                ["session"] = new JsonObject { ["id"] = lead, ["state"] = "working", ["kind"] = "chat", ["tree"] = tree },
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer.ToJsonString(), Encoding.UTF8, "application/json") };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _ledger.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A stand-in ACP agent: names its conversation <c>conv-1</c>, writes down each prompt and each turn's end; a prompt <c>slow</c> takes
    /// four seconds, the time a look's hand needs to land inside it.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-partner-chat-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard] = process.argv.slice(2);
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let conversation = null;
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-1';
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/prompt') {
                const raw = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                const text = raw.replace(/(\n\n| \| )?Daoris's standing block for this session: [\s\S]*$/, '');
                note('prompt: ' + text);
                await new Promise((resolve) => setTimeout(resolve, text === 'slow' ? 4000 : 100));
                say('heard');
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                note('ended: ' + (text === 'slow' ? 'slow' : 'other'));
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }
}
