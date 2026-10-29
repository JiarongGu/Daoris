using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 row d-ii (D212's note of part 5; the foundation design §10 row 1d-ii's proof, §3.4 rule 3) end to end: a late word to a closed
/// quest's driven part reaches the conversation that went on from it. The service's own host runs in a process of its own over a scratch home
/// (<see cref="RealServiceHost"/>), so the say door, the keyed open's one step, its receipt and the settlement trigger are the real ones; the
/// driver's chat runner goes on from the driven part as a closed quest's part goes on by itself where its own conversation is lost, on the
/// <c>acp-stub</c> stand-in agent. The person's word said after the move read the part and before the conversation's open moves with that
/// open, beside the word it read: the agent is handed each once, the late word's file from the conversation's own folder; each is shown there
/// by its own event under its id before the answer; neither waits anywhere after, and the late word's retry named to the driven part is
/// answered settled.
/// </summary>
/// <remarks>
/// No model, no account, no sign-in, no real agent binary: the stand-in agent writes down each prompt with its account and the links it was
/// handed. The host is the service; nothing of an install running here is reached, and this process's environment is read and never set.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class LateWordServiceProcessTests : IAsyncLifetime
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "late-word", Guid.NewGuid().ToString("N")[..8]);

    private RealServiceHost? _host;
    private ServiceClient? _client;
    private ServiceClient? _say;
    private HttpClient? _http;
    private readonly AtOpen _atOpen = new() { InnerHandler = new HttpClientHandler() };

    private string Home => Path.Combine(_scratch, "home");

    private string Root => Path.Combine(_scratch, "engine");

    private string Heard => Path.Combine(_scratch, "heard.txt");

    private SessionEvents Events => new(DaorisHome.Entry(Home, HomeEntry.Sessions));

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _http?.Dispose();
        _say?.Dispose();
        _client?.Dispose();
        _host?.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_scratch);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The driver's client, its every request through <see cref="AtOpen"/>, beside a plain one for the person's late word, and the repository
    /// registered at a checkout that is a plain folder: a conversation may open on any tree.
    /// </summary>
    private async Task ServiceAsync()
    {
        _host = await RealServiceHost.StartAsync(Path.Combine(_scratch, "service"));
        _client = new ServiceClient(_host.Url, null, new HttpClient(_atOpen, disposeHandler: false));
        _say = new ServiceClient(new ServiceHttp(_host.Url, key: null));
        _http = new HttpClient { BaseAddress = new Uri(_host.Url) };
        Directory.CreateDirectory(Root);
        var (ok, message) = await _client.RegisterAsync(new JsonObject
        {
            ["repository"] = "engine",
            ["root"] = Root,
            ["packs"] = new JsonArray(),
            ["domain"] = new JsonObject { ["summary"] = "An engine.", ["owns"] = new JsonArray("its own tree"), ["accepts"] = new JsonArray("a quest") },
        });
        Assert.True(ok, message);

        foreach (var account in new[] { "account-1", "account-2" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "stub", account));
        new HarnessSettings().WithRotation("stub", ["account-1", "account-2"]).Save(DaorisHome.Entry(Home, HomeEntry.Harnesses));
    }

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
        config.Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        return config;
    }

    /// <summary>The words waiting on a record, as the service lists them.</summary>
    private async Task<List<string>> WaitingAsync(string session)
    {
        using var records = JsonDocument.Parse(await _say!.SessionRecordsJsonAsync(default));
        var record = records.RootElement.EnumerateArray().Single(each => each.GetProperty("id").GetString() == session);
        if (!record.TryGetProperty("said", out var said) || said.ValueKind != JsonValueKind.Array) return [];
        return [.. said.EnumerateArray().Select(word => word.GetProperty("id").GetString()!)];
    }

    private IReadOnlyList<string> Prompts => [.. StubFile.Lines(Heard).Where(line => line.StartsWith("prompt[", StringComparison.Ordinal))];

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    [Fact]
    public async Task A_late_word_to_a_closed_quests_driven_part_reaches_the_conversation_that_went_on_from_it_once_under_its_id()
    {
        await ServiceAsync();
        var published = await _http!.PostAsJsonAsync("/api/quests", new { from = "game", to = "engine", title = "Note the cache", body = "Say that the frame cache exists." });
        published.EnsureSuccessStatusCode();
        using var quest = JsonDocument.Parse(await published.Content.ReadAsStringAsync());
        var questId = quest.RootElement.GetProperty("quest").GetProperty("id").GetString()!;
        var (driven, opened) = await _client!.OpenSessionAsync(questId, "acp-stub", tree: Root);
        Assert.True(driven is not null, opened);
        foreach (var state in new[] { "starting", "working", "completed" }) await _client.AdvanceAsync(driven!, state);
        var closed = await _http.PostAsJsonAsync($"/api/quests/{questId}/respond", new { action = "decline", reason = "The person closed it." });
        Assert.True(closed.IsSuccessStatusCode, await closed.Content.ReadAsStringAsync());

        var read = (await _client.SayAsync(driven!, "Also log the port.")).Word?.Id ?? throw new InvalidOperationException("the word was not kept");
        ChatFiles.Keep(Home, driven!, [new ChatUpload("trace.txt", Encoding.UTF8.GetBytes("exit 3"))]);
        string? late = null;
        // Said by the person after the move read the part, as the conversation's open is on its way.
        _atOpen.Before = async () =>
            late = (await _say!.SayAsync(driven!, "And use 9090.", ["trace.txt"], key: "press/late")).Word?.Id;

        var adapters = AdapterSet.Built();
        using var runner = new ChatRunner(
            _client, adapters, Home, new SessionProcesses(DaorisHome.Entry(Home, HomeEntry.Sessions)), events: Events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(Home, HomeEntry.Harnesses)));
        var answer = await runner.GoOnLostAsync(driven!, ContinueWhy.Of(ContinueWhy.Gone), Config());

        Assert.True(answer.Sent, answer.Message);
        Assert.NotNull(late);
        var to = answer.SessionId!;
        await Poll.Until(
            () => Prompts.Count > 0 && Events.After(to, 0).Events.Any(e => e.Kind == SessionEventKind.Message),
            () => $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], record [{string.Join(" | ", Events.After(to, 0).Events.Select(e => $"{e.Kind}:{e.Id}:{e.Text}"))}]");
        var handed = Assert.Single(Prompts);
        Assert.Equal((1, 1), (Count(handed, "Also log the port."), Count(handed, "And use 9090.")));
        Assert.Contains(ChatFiles.Find(Home, to, "trace.txt")!.Path.Replace('\\', '/'), handed.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

        var shown = Events.After(to, 0).Events.ToList();
        var first = shown.FindIndex(e => e.Kind == SessionEventKind.User && e.Id == read);
        var second = shown.FindIndex(e => e.Kind == SessionEventKind.User && e.Id == late);
        Assert.True(first >= 0 && second > first && second < shown.FindIndex(e => e.Kind == SessionEventKind.Message), string.Join(" | ", shown.Select(e => $"{e.Kind}:{e.Id}")));
        Assert.Equal(["trace.txt"], shown[second].Files!);
        var went = Assert.Single(Events.After(driven!, 0).Events, e => e.Kind == SessionEventKind.Note && e.To == to);
        Assert.Equal([read, late!], went.Words!);

        Assert.Empty(await WaitingAsync(driven!));
        for (var waited = TimeSpan.Zero; (await WaitingAsync(to)).Count > 0 && waited < Poll.Patience; waited += TimeSpan.FromMilliseconds(100))
        {
            await Task.Delay(100);
        }

        Assert.Empty(await WaitingAsync(to));
        var retry = await _http.PostAsJsonAsync($"/api/sessions/{driven}/say", new { text = "And use 9090.", key = "press/late", settled = true });
        using var retried = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        Assert.True((int)retry.StatusCode == 200 && retried.RootElement.TryGetProperty("settled", out _), retried.RootElement.GetRawText());
        Assert.Empty(await WaitingAsync(driven!));
        Assert.Empty(await WaitingAsync(to));
    }

    /// <summary>The driver's requests to the host, passed on as they are, once telling <see cref="Before"/> as a conversation's open is on its way.</summary>
    private sealed class AtOpen : DelegatingHandler
    {
        private int _told;

        public Func<Task>? Before { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/sessions/chat" && Before is { } before
                && Interlocked.Exchange(ref _told, 1) == 0)
            {
                await before().ConfigureAwait(false);
            }

            return await base.SendAsync(request, ct).ConfigureAwait(false);
        }
    }

    /// <summary>A stand-in ACP agent on the stub's accounts: it names its conversation and writes each prompt down with its account and links.</summary>
    private string Agent()
    {
        var script = Path.Combine(_scratch, "acp-late-word-agent.mjs");
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
                const links = frame.params.prompt.filter((block) => block.type === 'resource_link').map((block) => ' | link: ' + block.uri).join('');
                note(`prompt[${account}]: ` + text.replace(/\r?\n/g, ' ') + links);
                await new Promise((resolve) => setTimeout(resolve, 50));
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'heard it' } } } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
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
        var script = Path.Combine(_scratch, "text-agent.mjs");
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
