using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A written conversation resumed on the real agent: the provider's seeding writes a fixed session (a code word said in its history, a branch
/// named only in its context entries) as the agent's own conversation in its own home, and the real program, resumed under that id with a
/// stand-in question, names both, which only the written conversation holds. Each runs only where its program is on the path and real model
/// turns are asked for (<see cref="LiveAgentFactAttribute"/>); what it wrote is taken out of that home after.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Network)]
public sealed class SeedResumeProcessTests : IDisposable
{
    private static readonly DateTimeOffset Moment = new MovedClock().GetUtcNow();

    private const string Word = "PERIWINKLE-4721";

    private const string Branch = "heron-9";

    private const string Question =
        "Answer from this conversation alone, using no tool: what is the deployment code word, and what is the release branch named? Reply in one line.";

    private static readonly TimeSpan Bound = TimeSpan.FromMinutes(4);

    private readonly string _tree = Path.Combine(TestHomes.Root, "seed-resume-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly List<string> _placed = [];

    public SeedResumeProcessTests() => Directory.CreateDirectory(_tree);

    public void Dispose()
    {
        foreach (var path in _placed)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                else File.Delete(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
        }

        try { Directory.Delete(_tree, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static SessionEvent[] History =>
    [
        new() { Kind = SessionEventKind.User, Text = $"Remember this for later: the deployment code word is {Word}.", Origin = "person" },
        new() { Kind = SessionEventKind.Message, Text = $"Noted: the deployment code word is {Word}." },
    ];

    private static ContextEntry[] Context =>
    [
        new(ContextKind.Work, "Keep the deployment notes."),
        new(ContextKind.Found, $"The release branch is named {Branch}.") { Subject = "release" },
    ];

    /// <summary>Claude Code resumes the conversation written in its project folder and names what only it holds.</summary>
    [LiveAgentFact("claude")]
    public async Task Claude_Code_goes_on_from_a_conversation_written_for_it()
    {
        var home = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } set
            ? set
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        var conversation = Guid.NewGuid().ToString();
        var placed = ClaudeSeeding.Seeding.Place(new SeedAsk("claude-code", home, _tree, conversation, History, Context, Moment));
        Assert.Null(placed.Refused);
        // The project folder is named after this test's own tree, so it holds this conversation alone and goes whole.
        _placed.Add(Path.Combine(home, Path.GetDirectoryName(Assert.Single(placed.Files))!));

        var info = new ProcessStartInfo(LiveAgentFactAttribute.Located("claude")!)
        {
            WorkingDirectory = _tree, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "-p", Question, "--resume", placed.Conversation, "--output-format", "text" }) info.ArgumentList.Add(argument);
        var (output, error) = await RunAsync(info);

        Assert.True(output.Contains(Word, StringComparison.Ordinal) && output.Contains(Branch, StringComparison.Ordinal), $"answered: {output}\nerror: {error}");
    }

    /// <summary>Codex, through its protocol door, loads the thread written under its day and names what only it holds.</summary>
    [LiveAgentFact("codex-acp")]
    public async Task Codex_goes_on_from_a_thread_written_for_it()
    {
        var home = Environment.GetEnvironmentVariable("CODEX_HOME") is { Length: > 0 } set
            ? set
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        var placed = CodexSeeding.Seeding.Place(new SeedAsk("codex-acp", home, _tree, Guid.NewGuid().ToString(), History, Context, Moment));
        Assert.Null(placed.Refused);
        _placed.AddRange(placed.Files.Select(name => Path.Combine(home, name)));

        var shim = LiveAgentFactAttribute.Located("codex-acp")!;
        var script = Path.Combine(Path.GetDirectoryName(shim)!, "node_modules", "@agentclientprotocol", "codex-acp", "dist", "index.js");
        var info = File.Exists(script) ? new ProcessStartInfo("node") { ArgumentList = { script } } : new ProcessStartInfo(shim);
        info.WorkingDirectory = _tree;
        info.RedirectStandardInput = info.RedirectStandardOutput = info.RedirectStandardError = true;
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        var answer = await AskAsync(info, placed.Conversation);

        Assert.True(answer.Contains(Word, StringComparison.Ordinal) && answer.Contains(Branch, StringComparison.Ordinal), $"answered: {answer}");
    }

    /// <summary>dsh, started as Daoris starts it, resumes the session written under its home, and holds nothing of it back: no model is asked.</summary>
    [LiveAgentFact("dsh")]
    public async Task dsh_resumes_a_session_written_for_it()
    {
        SessionEvent[] withTools =
        [
            .. History,
            new() { Kind = SessionEventKind.Tool, Id = "call-1", ToolName = "read", Input = "{\"path\":\"notes.md\"}", Status = "pending" },
            new() { Kind = SessionEventKind.Tool, Id = "call-1", Output = "the notes", Status = "completed" },
            new() { Kind = SessionEventKind.Message, Text = "The notes are read." },
        ];
        var (info, session) = DshWritten(withTools);

        var answer = await AskAsync(info, session, "session/resume", prompt: false);

        Assert.Equal("", answer);
    }

    /// <summary>dsh, started as Daoris starts it with its key, goes on in the session written under its home and names what only it holds.</summary>
    [LiveAgentFact("dsh", key: "DEEPSEEK_API_KEY")]
    public async Task dsh_goes_on_from_a_session_written_for_it()
    {
        var (info, session) = DshWritten(History);

        var answer = await AskAsync(info, session, "session/resume");

        Assert.True(answer.Contains(Word, StringComparison.Ordinal) && answer.Contains(Branch, StringComparison.Ordinal), $"answered: {answer}");
    }

    private (ProcessStartInfo Info, string Session) DshWritten(IReadOnlyList<SessionEvent> history)
    {
        var home = Path.Combine(_tree, "dsh-home");
        var placed = DshSeeding.Seeding.Place(new SeedAsk("dsh", home, _tree, Guid.NewGuid().ToString(), history, Context, Moment));
        Assert.Null(placed.Refused);
        var recipe = new DshSpawn().Start(new SpawnRequest("test", _tree, "http://127.0.0.1:1") { Command = [LiveAgentFactAttribute.Located("dsh")!] });
        var info = new ProcessStartInfo(recipe.Program)
        {
            WorkingDirectory = _tree, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in recipe.Arguments) info.ArgumentList.Add(argument);
        foreach (var (name, value) in recipe.Variables) info.Environment[name] = value;
        info.Environment["DSH_HOME"] = home;
        return (info, placed.Conversation);
    }

    private static async Task<(string Output, string Error)> RunAsync(ProcessStartInfo info)
    {
        using var process = Process.Start(info)!;
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var bound = new CancellationTokenSource(Bound);
        try
        {
            await process.WaitForExitAsync(bound.Token);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }

        return (await output, await error);
    }

    /// <summary>The protocol's steps on the conversation: the opening, its load or resume, one prompt (none: stop once taken); the agent's words to it.</summary>
    private static async Task<string> AskAsync(ProcessStartInfo info, string conversation, string method = "session/load", bool prompt = true)
    {
        using var process = Process.Start(info)!;
        using var bound = new CancellationTokenSource(Bound);
        var stderr = process.StandardError.ReadToEndAsync(bound.Token);
        var answer = new StringBuilder();
        var asked = false;
        async Task Send(JsonObject frame)
        {
            await process.StandardInput.WriteLineAsync(frame.ToJsonString());
            await process.StandardInput.FlushAsync();
        }

        async Task<JsonObject> Answer(int id)
        {
            while (await process.StandardOutput.ReadLineAsync(bound.Token) is { } line)
            {
                if (JsonNode.Parse(line) is not JsonObject frame) continue;
                if (frame["method"] is not null && frame["id"] is { } request)
                {
                    var reply = frame["method"]!.GetValue<string>() == "session/request_permission"
                        ? new JsonObject { ["jsonrpc"] = "2.0", ["id"] = request.DeepClone(), ["result"] = new JsonObject { ["outcome"] = new JsonObject { ["outcome"] = "cancelled" } } }
                        : new JsonObject { ["jsonrpc"] = "2.0", ["id"] = request.DeepClone(), ["error"] = new JsonObject { ["code"] = -32601, ["message"] = "not offered" } };
                    await Send(reply);
                }
                else if (asked && frame["params"]?["update"] is JsonObject update && update["sessionUpdate"]?.GetValue<string>() == "agent_message_chunk")
                {
                    answer.Append(update["content"]?["text"]?.GetValue<string>());
                }
                else if (frame["id"]?.GetValue<int>() == id)
                {
                    return frame;
                }
            }

            throw new InvalidOperationException($"the agent left before answering {id}: {await stderr}");
        }

        try
        {
            await Send(new JsonObject
            {
                ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "initialize",
                ["params"] = new JsonObject { ["protocolVersion"] = 1, ["clientCapabilities"] = new JsonObject() },
            });
            await Answer(1);
            await Send(new JsonObject
            {
                ["jsonrpc"] = "2.0", ["id"] = 2, ["method"] = method,
                ["params"] = new JsonObject { ["sessionId"] = conversation, ["cwd"] = info.WorkingDirectory, ["mcpServers"] = new JsonArray() },
            });
            var loaded = await Answer(2);
            Assert.True(loaded["error"] is null, $"the load was refused: {loaded}");
            if (!prompt) return "";
            asked = true;
            await Send(new JsonObject
            {
                ["jsonrpc"] = "2.0", ["id"] = 3, ["method"] = "session/prompt",
                ["params"] = new JsonObject
                {
                    ["sessionId"] = conversation, ["prompt"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = Question }),
                },
            });
            var ended = await Answer(3);
            Assert.True(ended["error"] is null, $"the prompt failed: {ended}");
            return answer.ToString();
        }
        finally
        {
            process.StandardInput.Close();
            if (!process.WaitForExit(5_000)) process.Kill(entireProcessTree: true);
        }
    }
}
