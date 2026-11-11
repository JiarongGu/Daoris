using System.Text.Json;

using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session's <c>daoris-plugins</c> server: the tools it lists follow each plugin's <c>tool</c> marks and the switches at the session's
/// place, and a call runs one round of the command <c>from: agent</c>, its words the text, its data the structured content, an ask or a
/// missing argument refused, since no person answers a tool call.
/// </summary>
public sealed class PluginToolsServerTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-plugin-tools-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<JsonElement> _frames = [];
    private static readonly PluginPlace Engine = new(Repository: "engine", Workspace: "games");

    public PluginToolsServerTests() => Install("acme-tickets", """
        { "commands": [
            { "name": "open", "title": { "en": "Open a ticket", "zh": "开工单" }, "about": { "en": "Opens a ticket.", "zh": "开一个工单。" }, "tool": true, "writes": true,
              "args": [{ "name": "title", "type": "text", "required": true }, { "name": "size", "type": "choice", "choices": ["s", "l"] }, { "name": "urgent", "type": "boolean" }] },
            { "name": "status", "about": "Says what is kept.", "tool": true },
            { "name": "login", "about": "Keeps a token.", "interactive": true },
            { "name": "logout", "about": "Forgets it." } ] }
        """);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Install(string id, string interfaces)
    {
        var folder = DaorisHome.Entry(_home, HomeEntry.Plugins, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, PluginCatalog.ManifestName),
            $$"""{ "id": "{{id}}", "hooks": { "command": ["node", "plugin.mjs"], "points": ["work/state"] }, "interfaces": {{interfaces}} }""");
    }

    private sealed class Channel(Func<JsonElement, string> answer, List<JsonElement> frames) : IHookChannel
    {
        public IReadOnlyList<string> Points => [];

        public bool Alive => true;

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => Task.FromResult(HookDecision.Allow);

        public Task EndedAsync(object payload, CancellationToken ct) => Task.CompletedTask;

        public void Listen(Action<JsonElement?>? steps) { }

        public Task WatchAsync(WorkWatchFrame frame, CancellationToken ct) => Task.CompletedTask;

        public Task<JsonElement> CommandAsync(object frame, CancellationToken ct)
        {
            var sent = JsonSerializer.SerializeToElement(frame);
            frames.Add(sent);
            return Task.FromResult(JsonDocument.Parse(answer(sent)).RootElement.Clone());
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private PluginToolsServer Server(Func<JsonElement, string> answer, string language = "en") =>
        new(_home, "s-1", Engine, language, PluginLog.None, (_, _) => Task.FromResult<IHookChannel>(new Channel(answer, _frames)));

    private static JsonElement Frame(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static async Task<JsonElement> Ask(PluginToolsServer server, string json) =>
        JsonSerializer.SerializeToElement(await server.AnswerAsync(Frame(json), CancellationToken.None));

    [Fact]
    public void The_list_holds_each_tool_command_by_plugin_and_name_and_leaves_out_the_rest()
    {
        var names = PluginToolList.At(_home, Engine).Select(tool => tool.Name).ToList();

        Assert.Equal(["acme-tickets_open", "acme-tickets_status"], names);
    }

    [Fact]
    public void The_list_follows_the_tools_and_commands_switches_at_the_session_s_place()
    {
        PluginSwitches.Set(_home, "acme-tickets", PluginSwitches.Tools, "repository:engine", on: false);

        Assert.Empty(PluginToolList.At(_home, Engine));
        Assert.Equal(2, PluginToolList.At(_home, new PluginPlace(Repository: "tools", Workspace: "games")).Count);

        PluginSwitches.Set(_home, "acme-tickets", PluginSwitches.Tools, "repository:engine", on: true);
        PluginSwitches.Set(_home, "acme-tickets", PluginSwitches.Commands, "workspace:games", on: false);
        Assert.Empty(PluginToolList.At(_home, Engine));
    }

    [Fact]
    public void A_session_is_handed_the_server_only_where_it_lists_a_tool()
    {
        var at = new PluginToolsAt("s-1", Engine, "zh");

        var entry = PluginToolsHand.Entry(_home, at, program: "daoris-driver");

        Assert.NotNull(entry);
        Assert.Equal(PluginToolsServer.Name, entry.Name);
        Assert.Equal("daoris-driver", entry.Command);
        Assert.Equal(["plugin", "tools", "--session", "s-1", "--repository", "engine", "--workspace", "games", "--language", "zh"], entry.Arguments);
        Assert.Equal(Path.GetFullPath(_home), entry.Environment[DaorisHome.Variable]);

        PluginSwitches.Set(_home, "acme-tickets", PluginSwitches.Tools, PluginPlace.MachineKey, on: false);
        Assert.Null(PluginToolsHand.Entry(_home, at, program: "daoris-driver"));
        Assert.Null(PluginToolsHand.Entry(_home, at with { Place = PluginPlace.Machine }, program: null));
    }

    [Fact]
    public void The_server_answers_the_handshake_and_lists_each_tool_in_the_session_s_language_with_its_schema()
    {
        var server = Server(_ => "{}", language: "zh");

        var opened = Ask(server, """{ "jsonrpc": "2.0", "id": 1, "method": "initialize", "params": { "protocolVersion": "2025-06-18", "capabilities": {} } }""").Result;
        Assert.Equal("2025-06-18", opened.GetProperty("result").GetProperty("protocolVersion").GetString());
        Assert.Equal(PluginToolsServer.Name, opened.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.True(opened.GetProperty("result").GetProperty("capabilities").TryGetProperty("tools", out _));

        var listed = Ask(server, """{ "jsonrpc": "2.0", "id": 2, "method": "tools/list" }""").Result.GetProperty("result").GetProperty("tools");
        var open = listed.EnumerateArray().Single(tool => tool.GetProperty("name").GetString() == "acme-tickets_open");
        Assert.Equal("开一个工单。", open.GetProperty("description").GetString());
        Assert.Equal("开工单", open.GetProperty("title").GetString());
        Assert.False(open.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
        var schema = open.GetProperty("inputSchema");
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.Equal(["title"], schema.GetProperty("required").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal("string", schema.GetProperty("properties").GetProperty("title").GetProperty("type").GetString());
        Assert.Equal(["s", "l"], schema.GetProperty("properties").GetProperty("size").GetProperty("enum").EnumerateArray().Select(each => each.GetString()));
        Assert.Equal("boolean", schema.GetProperty("properties").GetProperty("urgent").GetProperty("type").GetString());
        var status = listed.EnumerateArray().Single(tool => tool.GetProperty("name").GetString() == "acme-tickets_status");
        Assert.True(status.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
    }

    [Fact]
    public async Task A_call_is_one_round_from_an_agent_with_its_session_place_and_arguments_its_words_and_data_answered()
    {
        var server = Server(_ => """{ "said": ["opened #7"], "data": { "id": 7 }, "exit": 0 }""");

        var answered = await Ask(server, """
            { "jsonrpc": "2.0", "id": 3, "method": "tools/call", "params": { "name": "acme-tickets_open", "arguments": { "title": "Fix it", "urgent": true } } }
            """);

        var frame = Assert.Single(_frames);
        Assert.Equal("open", frame.GetProperty("command").GetString());
        Assert.Equal(CommandWire.Agent, frame.GetProperty("from").GetString());
        Assert.Equal("s-1", frame.GetProperty("session").GetString());
        Assert.Equal("engine", frame.GetProperty("place").GetProperty("id").GetString());
        Assert.Equal("Fix it", frame.GetProperty("args").GetProperty("title").GetString());
        Assert.True(frame.GetProperty("args").GetProperty("urgent").GetBoolean());
        var result = answered.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());
        Assert.Equal("opened #7", result.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal(7, result.GetProperty("structuredContent").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task A_round_that_asks_is_refused_since_no_person_answers_a_tool_call_and_no_second_round_is_run()
    {
        var server = Server(_ => """{ "said": ["Paste your token."], "ask": { "name": "token", "prompt": "Token", "masked": true } }""");

        var result = (await Ask(server, """{ "jsonrpc": "2.0", "id": 4, "method": "tools/call", "params": { "name": "acme-tickets_status", "arguments": {} } }"""))
            .GetProperty("result");

        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Contains("no person", result.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Single(_frames);
    }

    [Fact]
    public async Task A_missing_argument_or_a_switched_off_tool_is_refused_before_any_process_starts_and_an_exit_is_an_error()
    {
        var server = Server(_ => """{ "said": ["the service refused the token"], "exit": 1 }""");

        var missing = (await Ask(server, """{ "jsonrpc": "2.0", "id": 5, "method": "tools/call", "params": { "name": "acme-tickets_open", "arguments": {} } }"""))
            .GetProperty("result");
        Assert.True(missing.GetProperty("isError").GetBoolean());
        Assert.Contains("`title`", missing.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Empty(_frames);

        var failed = (await Ask(server, """{ "jsonrpc": "2.0", "id": 6, "method": "tools/call", "params": { "name": "acme-tickets_status" } }"""))
            .GetProperty("result");
        Assert.True(failed.GetProperty("isError").GetBoolean());
        Assert.Equal("the service refused the token", failed.GetProperty("content")[0].GetProperty("text").GetString());

        PluginSwitches.Set(_home, "acme-tickets", PluginSwitches.Tools, "workspace:games", on: false);
        var off = await Ask(server, """{ "jsonrpc": "2.0", "id": 7, "method": "tools/call", "params": { "name": "acme-tickets_status" } }""");
        Assert.Equal(-32602, off.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Single(_frames);
    }

    [Fact]
    public async Task A_notification_is_answered_nothing_and_an_unknown_method_is_not_found()
    {
        var server = Server(_ => "{}");

        Assert.Null(await server.AnswerAsync(Frame("""{ "jsonrpc": "2.0", "method": "notifications/initialized" }"""), CancellationToken.None));
        var unknown = await Ask(server, """{ "jsonrpc": "2.0", "id": 8, "method": "resources/list" }""");
        Assert.Equal(-32601, unknown.GetProperty("error").GetProperty("code").GetInt32());
        var pinged = await Ask(server, """{ "jsonrpc": "2.0", "id": 9, "method": "ping" }""");
        Assert.Equal(JsonValueKind.Object, pinged.GetProperty("result").ValueKind);
    }

    [Fact]
    public async Task The_stdio_loop_answers_one_line_per_request_and_ends_with_its_input()
    {
        var server = Server(_ => """{ "said": ["ok"] }""");
        var input = new StringReader(
            """{ "jsonrpc": "2.0", "id": 1, "method": "initialize", "params": { "protocolVersion": "2024-11-05" } }""" + "\n"
            + """{ "jsonrpc": "2.0", "method": "notifications/initialized" }""" + "\n"
            + "not json\n"
            + """{ "jsonrpc": "2.0", "id": 2, "method": "tools/call", "params": { "name": "acme-tickets_status" } }""" + "\n");
        var output = new StringWriter();

        await server.RunAsync(input, output, CancellationToken.None);

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Equal(-32700, JsonDocument.Parse(lines[1]).RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal("ok", JsonDocument.Parse(lines[2]).RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
    }
}
