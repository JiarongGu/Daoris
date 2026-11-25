using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A resumed Claude Code session is sent the tools its servers serve now: the real <c>claude</c> on the path, started and
/// resumed by the native door's own recipes, a stand-in server whose one tool gains a parameter between the start and the resume, and a
/// keyless stand-in model on loopback that keeps the tools each request carried. Claude Code records the system prompt and the tools in
/// the conversation (<c>prompt_snapshot</c>) and, left to itself, sends that record again at every resume, a new tool name added and an
/// existing tool's new parameters never; the door turns the record off, so the resumed request carries the parameter.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class ClaudeResumedToolsProcessTests : IAsyncDisposable
{
    private const string Tool = "mcp__stand-in__ask";

    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(2);

    private readonly string _root = Path.Combine(TestHomes.Root, "resumed-tools-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInModel _model = StandInModel.Start();

    public ClaudeResumedToolsProcessTests() => Directory.CreateDirectory(_root);

    public async ValueTask DisposeAsync()
    {
        await _model.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [HarnessOnPathFact("claude")]
    public async Task A_resumed_session_is_sent_the_parameter_its_server_gained_after_the_start()
    {
        var tree = Path.Combine(_root, "tree");
        var home = Path.Combine(_root, "home");
        Directory.CreateDirectory(tree);
        Directory.CreateDirectory(home);
        var toolsFile = Path.Combine(_root, "tools.json");
        File.WriteAllText(toolsFile, StandInServer.Tools(site: false));
        var script = Path.Combine(_root, "server.mjs");
        File.WriteAllText(script, StandInServer.Script);
        var node = PackageStarts.Node(null, null) ?? throw new InvalidOperationException("node is not on PATH");
        var servers = Path.Combine(_root, "servers.mcp.json");
        File.WriteAllText(servers, new JsonObject
        {
            ["mcpServers"] = new JsonObject
            {
                ["stand-in"] = new JsonObject { ["command"] = node, ["args"] = new JsonArray(script, toolsFile) },
            },
        }.ToJsonString());

        var spawn = new ClaudeCodeSpawn();
        var request = new SpawnRequest("test", tree, "http://127.0.0.1:1") { Command = [HarnessOnPathFactAttribute.Located("claude")!] };
        var started = await RunAsync(spawn, spawn.Start(request), servers, tree, home, "Say hello.");
        var conversation = started.Conversation ?? throw new InvalidOperationException($"no session id in the start's output: {started.Output}");
        Assert.All(_model.ToolsSent(Tool), properties => Assert.DoesNotContain("site", properties));
        var before = _model.Requests;

        File.WriteAllText(toolsFile, StandInServer.Tools(site: true));
        var resumed = await RunAsync(spawn, spawn.Resume(request with { Resume = conversation })!, servers, tree, home, "Say hello again.");

        var sent = _model.ToolsSent(Tool, after: before);
        Assert.True(sent.Count > 0, $"the resume sent no request carrying {Tool}: {resumed.Output}");
        Assert.All(sent, properties => Assert.Contains("site", properties));
    }

    /// <summary>The proof in <c>grants.json</c> of the native door: its own hand-over reaches the agent, the model is sent the server's tool.</summary>
    [HarnessOnPathFact("claude")]
    public async Task The_native_door_s_agent_lists_the_tools_of_the_knowledge_server_it_is_handed()
    {
        var tree = Path.Combine(_root, "tree");
        var home = Path.Combine(_root, "home");
        Directory.CreateDirectory(tree);
        Directory.CreateDirectory(home);
        var plan = new ConnectorPlan("grants-native", [StandInServer.Entry(_root, KnowledgeConnector.ServerName)]);
        var spawn = new ClaudeCodeSpawn();
        var request = new SpawnRequest("test", tree, "http://127.0.0.1:1")
        {
            Command = [HarnessOnPathFactAttribute.Located("claude")!], Mode = "acceptEdits",
        };

        var ran = await RunAsync(spawn, spawn.Start(request), servers: null, tree, home, "Say hello.", plan);

        StandInServer.AssertListed(_model.ToolNames(), KnowledgeConnector.ServerName, ran.Output);
    }

    private sealed record Ran(string? Conversation, string Output);

    /// <summary>One run of a recipe under a scratch home, the stand-in model and the servers file, the prompt on its input; bounded.</summary>
    private async Task<Ran> RunAsync(
        ClaudeCodeSpawn spawn, ProcessRecipe recipe, string? servers, string tree, string home, string prompt, ConnectorPlan? plan = null)
    {
        var info = new ProcessStartInfo(recipe.Program)
        {
            WorkingDirectory = tree, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true,
        };
        foreach (var argument in recipe.Arguments) info.ArgumentList.Add(argument);
        if (plan is not null) spawn.Hand(info, Path.Combine(_root, "daoris"), plan);
        else spawn.HandServers(info, servers!);
        foreach (var name in new[] { "ANTHROPIC_AUTH_TOKEN", "CLAUDE_CODE_OAUTH_TOKEN", "CLAUDE_CODE_USE_BEDROCK", "CLAUDE_CODE_USE_VERTEX" })
        {
            info.Environment.Remove(name);
        }

        info.Environment["CLAUDE_CONFIG_DIR"] = home;
        info.Environment["ANTHROPIC_BASE_URL"] = _model.Url;
        info.Environment["ANTHROPIC_API_KEY"] = "placeholder-not-a-key";
        info.Environment["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1";
        foreach (var (name, value) in ClaudeCodeSpawn.StayPinned) info.Environment[name] = value;
        foreach (var (name, value) in spawn.ToolsUpFront) info.Environment[name] = value;

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();
        using var bound = new CancellationTokenSource(Bound);
        try
        {
            await process.WaitForExitAsync(bound.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"claude ran past {Bound}: {await error}");
        }

        var said = await output;
        string? conversation = null;
        foreach (var line in said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                using var frame = JsonDocument.Parse(line);
                if (frame.RootElement.TryGetProperty("session_id", out var id) && id.ValueKind == JsonValueKind.String) conversation ??= id.GetString();
            }
            catch (JsonException)
            {
            }
        }

        Assert.True(process.ExitCode == 0, $"claude exited {process.ExitCode}: {said}\n{await error}");
        return new Ran(conversation, said);
    }
}

/// <summary>A test that runs a harness's program with no account behind it: skipped, saying why, where the program is not on the path.</summary>
internal sealed class HarnessOnPathFactAttribute : FactAttribute
{
    /// <param name="program">The program's file name as the path names it, without its extension.</param>
    public HarnessOnPathFactAttribute(string program)
    {
        if (Located(program) is null) Skip = $"`{program}` is not on this machine's path";
    }

    /// <summary>The program's file on the path (<see cref="LiveAgentFactAttribute.Located"/>).</summary>
    public static string? Located(string program) => LiveAgentFactAttribute.Located(program);
}
