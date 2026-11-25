using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// *Send now* on each protocol door's adapter at the version Daoris pins, and a resume on <c>codex-acp</c>, driven through Daoris's own
/// session (<see cref="AcpSession"/>) over the adapter's real pipes and kept in the frame tape every sitting keeps
/// (<see cref="FrameTape"/>), against a stub model on loopback (<c>tools/fake-model.mjs</c>): no account, no key, no model. A steer lands
/// as <c>injected</c>, the turn's one prompt is answered <c>end_turn</c>, the model's next call carries the word, and the session
/// updates the adapter sent are read as <c>fixtures/providers/&lt;id&gt;/corpus.jsonl</c> reads them. Each case runs only where the
/// pinned adapter is in the install's toolchain (<see cref="PinnedAdapterFactAttribute"/>). The measurements are
/// <c>docs/2026-10-03-steer-evidence.md</c> §10.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Harness)]
public sealed class AcpAdapterDoorProcessTests : IAsyncDisposable
{
    private const string Word = "put MANGO in your reply";

    private const string Marker = "PERIWINKLE-4721";

    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(2);

    /// <summary>The stub model's host: it serves on loopback, says where, and gives the calls it received when a line says <c>calls</c>.</summary>
    private const string ModelHost = """
        import { createInterface } from 'node:readline';
        const { startFakeModel } = await import(process.argv[2]);
        const model = await startFakeModel();
        const say = (value) => process.stdout.write(JSON.stringify(value) + '\n');
        say({ url: model.url });
        for await (const line of createInterface({ input: process.stdin })) {
          if (line.trim() === 'calls') say({ calls: model.calls });
        }
        await model.close();
        """;

    private readonly string _root = Path.Combine(TestHomes.Root, "adapter-door-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly List<Process> _started = [];

    private readonly List<IDisposable> _held = [];

    public AcpAdapterDoorProcessTests() => Directory.CreateDirectory(_root);

    public ValueTask DisposeAsync()
    {
        foreach (var held in _held) held.Dispose();
        foreach (var process in _started)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }

            process.Dispose();
        }

        GitSeed.Discard(_root);
        return ValueTask.CompletedTask;
    }

    [PinnedAdapterFact("claude-code-acp")]
    public async Task Claude_Code_s_adapter_takes_a_steer_mid_tool_as_injected_and_the_next_model_call_carries_the_word()
    {
        var run = await StartAsync("claude-code-acp");
        var session = await run.OpenAsync();

        var turn = session.PromptAsync("run the TOOL step", CancellationToken.None);
        await run.InTheToolAsync();
        var steered = await session.SteeredAsync(new ChatMessage(Word, []) { Id = "said-claude" }, CancellationToken.None, "conversation").WaitAsync(Bound);

        Assert.Equal("injected", steered);
        Assert.Equal("end_turn", await turn.WaitAsync(Bound));
        Assert.True(await run.CarriedAsync(Word), "the model's calls after the steer did not carry the word");
        run.AssertAnswered("_session/steering", "injected");
        run.AssertUpdatesAreTheCorpus();
        session.Release();
    }

    [PinnedAdapterFact("codex-acp")]
    public async Task Codex_s_adapter_takes_a_steer_mid_tool_as_injected_and_the_next_model_call_carries_the_word()
    {
        var run = await StartAsync("codex-acp");
        var session = await run.OpenAsync();

        var turn = session.PromptAsync("run the TOOL step", CancellationToken.None);
        await run.InTheToolAsync();
        var steered = await session.SteeredAsync(new ChatMessage(Word, []) { Id = "said-codex" }, CancellationToken.None, "conversation").WaitAsync(Bound);

        Assert.Equal("injected", steered);
        Assert.Equal("end_turn", await turn.WaitAsync(Bound));
        Assert.True(await run.CarriedAsync(Word), "the model's calls after the steer did not carry the word");
        run.AssertAnswered("_session/steering", "injected");
        run.AssertUpdatesAreTheCorpus();
        session.Release();
    }

    [PinnedAdapterFact("codex-acp")]
    public async Task Codex_s_adapter_resumes_its_own_conversation_and_answers_a_turn_in_it()
    {
        var first = await StartAsync("codex-acp");
        var opened = await first.OpenAsync();
        Assert.Equal("end_turn", await opened.PromptAsync($"Remember this: the code word is {Marker}.", CancellationToken.None).WaitAsync(Bound));
        var conversation = first.Conversation!;
        opened.Release();
        first.Stop();

        var second = await StartAsync("codex-acp", again: first);
        await second.NoteCallsAsync();
        var resumed = await second.ResumeAsync(conversation);

        Assert.Equal("end_turn", await resumed.PromptAsync("What is the code word?", CancellationToken.None).WaitAsync(Bound));
        Assert.True(await second.CarriedAsync(Marker), "the resumed prompt's model request did not carry the first sitting's words");
        second.AssertAnswered("session/resume", null);
        second.AssertUpdatesAreTheCorpus(every: false);
        resumed.Release();
    }

    [PinnedAdapterFact("claude-code-acp")]
    public async Task Claude_Code_s_adapter_lists_the_tools_of_the_knowledge_server_its_session_is_handed() =>
        await ListsTheKnowledgeServerAsync("claude-code-acp");

    [PinnedAdapterFact("codex-acp")]
    public async Task Codex_s_adapter_lists_the_tools_of_the_knowledge_server_its_session_is_handed() =>
        await ListsTheKnowledgeServerAsync("codex-acp");

    /// <summary>The proof in <c>grants.json</c> of a protocol door: the model is sent the knowledge server's tool.</summary>
    private async Task ListsTheKnowledgeServerAsync(string door)
    {
        var run = await StartAsync(door);
        var plan = new ConnectorPlan("grants-" + door, [StandInServer.Entry(run.Folder, KnowledgeConnector.ServerName)]);
        var session = await run.OpenAsync(plan);

        Assert.Equal("end_turn", await session.PromptAsync("Say hello.", CancellationToken.None).WaitAsync(Bound));
        var calls = await run.Model.CallsAsync();
        var tools = calls.SelectMany(call => call!["tools"]!.AsArray().Select(tool => (string?)tool ?? "")).Distinct(StringComparer.Ordinal).ToList();

        StandInServer.AssertListed(tools, KnowledgeConnector.ServerName, $"{calls.Count} model calls");
        session.Release();
    }

    private async Task<Drive> StartAsync(string door, Drive? again = null)
    {
        var model = again?.Model ?? await ModelServer.StartAsync(_root, _started);
        var drive = new Drive(door, _root, model, _started, _held, again?.Folder);
        return drive;
    }

    /// <summary>The adapter's process and the session over it.</summary>
    private sealed class Drive
    {
        private readonly string _door;
        private readonly string _folder;
        private readonly List<Process> _started;
        private readonly List<IDisposable> _held;
        private readonly string _tape;
        private Process? _process;
        private int _seen;

        public Drive(string door, string root, ModelServer model, List<Process> started, List<IDisposable> held, string? folder = null)
        {
            (_door, Model, _started, _held) = (door, model, started, held);
            _folder = folder ?? Path.Combine(root, door + "-" + Guid.NewGuid().ToString("N")[..6]);
            Directory.CreateDirectory(Path.Combine(_folder, "work"));
            Directory.CreateDirectory(Path.Combine(_folder, "home"));
            _tape = FrameTape.PathOf(Path.Combine(_folder, "session.jsonl"));
        }

        public ModelServer Model { get; }

        public string Folder => _folder;

        public string? Conversation { get; private set; }


        public async Task<AcpSession> OpenAsync(ConnectorPlan? plan = null)
        {
            var session = Connect();
            await session.OpenAsync(Path.Combine(_folder, "work"), CancellationToken.None, plan).WaitAsync(Bound);
            return session;
        }

        public async Task<AcpSession> ResumeAsync(string conversation)
        {
            var session = Connect();
            await session.ResumeAsync(Path.Combine(_folder, "work"), conversation, CancellationToken.None).WaitAsync(Bound);
            return session;
        }

        public void Stop()
        {
            try
            {
                if (_process is not { HasExited: false }) return;
                _process.StandardInput.Close();
                if (!_process.WaitForExit(TimeSpan.FromSeconds(15))) _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }

        /// <summary>The tool is in flight: the adapter announced it, and its sleep has run two seconds.</summary>
        public async Task InTheToolAsync()
        {
            var waited = Stopwatch.StartNew();
            while (waited.Elapsed < TimeSpan.FromSeconds(40))
            {
                if (Tape().Any(entry => entry.Direction == "in" && (string?)entry.Frame["params"]?["update"]?["sessionUpdate"] == "tool_call"))
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    _seen = (await Model.CallsAsync()).Count;
                    return;
                }

                await Task.Delay(200);
            }

            throw new TimeoutException("the adapter never announced a tool");
        }

        /// <summary>The model calls so far are before what this sitting is about.</summary>
        public async Task NoteCallsAsync() => _seen = (await Model.CallsAsync()).Count;

        /// <summary>Whether a model call made after the steer carries the word.</summary>
        public async Task<bool> CarriedAsync(string word)
        {
            var calls = await Model.CallsAsync();
            return calls.Skip(_seen).Any(call => call["input"]!.AsArray().Any(item => item!.GetValue<string>().Contains(word, StringComparison.Ordinal)));
        }

        /// <summary>The tape holds the answer to <paramref name="method"/>, a result whose <c>outcome</c> is <paramref name="outcome"/> where one is said.</summary>
        public void AssertAnswered(string method, string? outcome)
        {
            var frames = Tape();
            var asked = frames.Where(entry => entry.Direction == "out" && (string?)entry.Frame["method"] == method).ToList();
            Assert.NotEmpty(asked);
            var answered = frames.Where(entry => entry.Direction == "in" && asked.Any(ask => ask.Frame["id"]?.ToJsonString() == entry.Frame["id"]?.ToJsonString())
                && entry.Frame["result"] is not null).ToList();
            Assert.NotEmpty(answered);
            if (outcome is not null) Assert.Contains(answered, entry => (string?)entry.Frame["result"]?["outcome"] == outcome);
        }

        /// <summary>Each session update the adapter sent has a recorded frame of its kind at the pin and is read as that frame is, and, where <paramref name="every"/>, each recorded frame of the pin has its kind among them.</summary>
        public void AssertUpdatesAreTheCorpus(bool every = true)
        {
            var door = BuiltDoors.Of(_door);
            var corpus = ProviderCorpus.Of((_door == "codex-acp" ? "codex" : "claude-code"), _door);
            var recorded = corpus.Where(frame => frame.Adapter == PinnedAdapterFactAttribute.VersionOf(_door)).ToList();
            Assert.NotEmpty(recorded);
            var updates = Tape().Where(entry => entry.Direction == "in" && (string?)entry.Frame["method"] == "session/update")
                .Select(entry => entry.Frame["params"]!["update"]!.AsObject()).ToList();
            Assert.NotEmpty(updates);
            foreach (var update in updates)
            {
                var read = ProviderCorpus.Read(door, new CorpusFrame(_door, "live", null, "live", JsonSerializer.SerializeToElement(update), []));
                var same = recorded.Where(frame => Said(frame.Frame) == Said(update)).ToList();
                Assert.True(same.Count > 0, $"{_door} {PinnedAdapterFactAttribute.VersionOf(_door)}: an update of kind {Said(update)} has no recorded frame in the corpus");
                Assert.Contains(same, frame => frame.Events.SequenceEqual(read));
            }

            foreach (var frame in recorded.Where(_ => every))
            {
                Assert.Contains(updates, update => Said(update) == Said(frame.Frame));
            }
        }

        private AcpSession Connect()
        {
            var start = Spawn();
            var process = _process = Process.Start(start)!;
            _started.Add(process);
            var tape = new FrameTape(_tape, _ => { }, new MovedClock());
            _held.Add(tape);
            var posture = _door == "codex-acp" ? "agent-full-access" : "bypassPermissions";
            return new AcpSession(
                tape.Hearing(process.StandardOutput), tape.Speaking(process.StandardInput), _ => { }, posture: posture,
                onConversation: id => Conversation = id);
        }

        private ProcessStartInfo Spawn()
        {
            var info = new ProcessStartInfo("node")
            {
                WorkingDirectory = Path.Combine(_folder, "work"), RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = System.Text.Encoding.UTF8,
                UseShellExecute = false, CreateNoWindow = true,
            };
            info.ArgumentList.Add(PinnedAdapterFactAttribute.Script(_door));
            foreach (var name in new[] { "ANTHROPIC_AUTH_TOKEN", "CLAUDE_CODE_OAUTH_TOKEN", "CLAUDE_CODE_USE_BEDROCK", "CLAUDE_CODE_USE_VERTEX", "OPENAI_API_KEY" })
            {
                info.Environment.Remove(name);
            }

            info.Environment["ANTHROPIC_API_KEY"] = "placeholder-not-a-key";
            info.Environment["ANTHROPIC_BASE_URL"] = Model.Url;
            info.Environment["CLAUDE_CONFIG_DIR"] = Path.Combine(_folder, "home");
            info.Environment["CODEX_HOME"] = Path.Combine(_folder, "home");
            info.Environment["DISABLE_UPDATES"] = "1";
            foreach (var (name, value) in BuiltDoors.Of(_door).Spawned().ToolsUpFront) info.Environment[name] = value;
            info.Environment["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1";
            info.Environment["STUB_KEY"] = "placeholder-not-a-key";
            info.Environment["CODEX_CONFIG"] = new JsonObject
            {
                ["model_provider"] = "stub",
                ["model"] = "stub-model",
                ["model_providers"] = new JsonObject
                {
                    ["stub"] = new JsonObject { ["name"] = "stub", ["base_url"] = Model.Url + "/v1", ["env_key"] = "STUB_KEY", ["wire_api"] = "responses" },
                },
            }.ToJsonString();
            return info;
        }

        private static string Said(JsonElement frame) =>
            frame.GetProperty("sessionUpdate").GetString() + "/" + (frame.TryGetProperty("status", out var status) ? status.GetString() : "");

        private static string Said(JsonObject update) => (string)update["sessionUpdate"]! + "/" + (string?)update["status"];

        private string[] ReadTape()
        {
            using var file = new FileStream(_tape, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(file);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }

        private List<(string Direction, JsonObject Frame)> Tape() =>
        [
            .. ReadTape().Where(line => line.Length > 0).Select(line => JsonNode.Parse(line)!.AsObject())
                .Where(entry => entry["frame"] is JsonObject)
                .Select(entry => ((string)entry["dir"]!, entry["frame"]!.AsObject())),
        ];
    }

    /// <summary>The stub model's process, asked for the calls it received.</summary>
    private sealed class ModelServer
    {
        private readonly Process _process;
        private readonly SemaphoreSlim _one = new(1, 1);

        private ModelServer(Process process, string url) => (_process, Url) = (process, url);

        public string Url { get; }

        public static async Task<ModelServer> StartAsync(string root, List<Process> started)
        {
            var script = Path.Combine(root, "model-host-" + Guid.NewGuid().ToString("N")[..6] + ".mjs");
            File.WriteAllText(script, ModelHost);
            var fake = new Uri(Path.Combine(GitSeed.RepoRoot(), "tools", "fake-model.mjs")).AbsoluteUri;
            var info = new ProcessStartInfo("node")
            {
                RedirectStandardInput = true, RedirectStandardOutput = true, StandardOutputEncoding = System.Text.Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true,
            };
            info.ArgumentList.Add(script);
            info.ArgumentList.Add(fake);
            var process = Process.Start(info)!;
            started.Add(process);
            var first = await process.StandardOutput.ReadLineAsync().WaitAsync(Bound);
            return new ModelServer(process, (string)JsonNode.Parse(first!)!["url"]!);
        }

        public async Task<JsonArray> CallsAsync()
        {
            await _one.WaitAsync();
            try
            {
                await _process.StandardInput.WriteLineAsync("calls");
                await _process.StandardInput.FlushAsync();
                var line = await _process.StandardOutput.ReadLineAsync().WaitAsync(Bound);
                return JsonNode.Parse(line!)!["calls"]!.AsArray();
            }
            finally
            {
                _one.Release();
            }
        }
    }
}

/// <summary>
/// A case that runs a protocol door's adapter at the version Daoris pins: skipped, saying why, where the install's toolchain
/// (<c>cache/toolchain/&lt;tool&gt;/&lt;version&gt;</c>, found upward from the tests' own tree) does not hold it. The version is the one the
/// door's measured ability names, so a pin that moves is read here before it is trusted.
/// </summary>
internal sealed class PinnedAdapterFactAttribute : FactAttribute
{
    public PinnedAdapterFactAttribute(string door)
    {
        if (Script(door) is null) Skip = $"the pinned `{door}` adapter is not in an install's toolchain beside this tree";
    }

    /// <summary>The adapter version the door's measured steering reads name.</summary>
    public static string VersionOf(string door) =>
        AbilityReading.Of(BuiltDoors.Of(door), AbilityName.WordsAtNextStep).At?.Adapter
        ?? throw new InvalidOperationException($"{door} states no adapter version its words were measured at");

    /// <summary>The adapter's program file at its pin, or null where the toolchain holds none.</summary>
    public static string? Script(string door)
    {
        var package = door == "codex-acp" ? "codex-acp" : "claude-agent-acp";
        for (var folder = new DirectoryInfo(GitSeed.RepoRoot()); folder is not null; folder = folder.Parent)
        {
            var script = Path.Combine(
                folder.FullName, "cache", "toolchain", door, VersionOf(door), "node_modules", "@agentclientprotocol", package, "dist", "index.js");
            if (File.Exists(script)) return script;
        }

        return null;
    }
}
