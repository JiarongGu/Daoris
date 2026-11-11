using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Whom a session's plugin tools serve: the session, the place its switches are read at, and its language.</summary>
public sealed record PluginToolsAt(string Session, PluginPlace Place, string Language)
{
    /// <summary>The one plugin whose tools are listed, for a step's session; null lists every plugin's.</summary>
    public string? Plugin { get; init; }

    /// <summary>The attempt a step's session performs, whose <c>step_answer</c> is listed beside the plugin's tools; null for none.</summary>
    public string? Step { get; init; }

    /// <summary>A quest's session in its repository and workspace, in the repository's session language (English where none is set or it is another).</summary>
    public static PluginToolsAt OfQuest(string session, DriverConfig config, string repository, string? workspace) =>
        new(session, new PluginPlace(repository, workspace), SessionLanguages.Resolve(config, repository, workspace)?.Code is "zh" ? "zh" : "en");
}

/// <summary>
/// The <c>daoris-plugins</c> MCP server a session is handed (<c>daoris plugin tools</c>): newline-delimited JSON-RPC on stdio, the
/// handshake, <c>tools/list</c> of <see cref="PluginToolList"/> at the session's place in its language, and <c>tools/call</c> as one round of
/// the command (<see cref="PluginToolCall"/>). The person's switch <c>tools</c> per place is the opt-in, so no call asks an approval.
/// </summary>
public sealed class PluginToolsServer(
    string home, string session, PluginPlace place, string language, PluginLog log, PluginCommandRun.Starter? start = null)
{
    public const string Name = "daoris-plugins";

    /// <summary>The versions this server speaks, the newest last: a client's own is answered where it is one of them.</summary>
    private static readonly string[] Versions = ["2024-11-05", "2025-03-26", "2025-06-18"];

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly PluginToolsAt _at = new(session, place, language);

    /// <summary>
    /// <c>daoris plugin tools</c>: serves the session named in <paramref name="words"/> on stdio until its harness closes the input. Its
    /// stdout carries frames alone, so a refusal is said on <paramref name="error"/>: exit 2 with no home or no session named.
    /// </summary>
    public static async Task<int> ServeAsync(
        string? home, IReadOnlyList<string> words, MachineLog? log, TextReader input, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (home is null)
        {
            await error.WriteLineAsync("plugin tools: no Daoris home is named (DAORIS_HOME).").ConfigureAwait(false);
            return 2;
        }

        if (PluginToolsHand.Read(words) is not { } at)
        {
            await error.WriteLineAsync("plugin tools: takes --session <id> [--repository <id>] [--workspace <name>] [--language en|zh].").ConfigureAwait(false);
            return 2;
        }

        await new PluginToolsServer(home, at, new PluginLog(log)).RunAsync(input, output, ct).ConfigureAwait(false);
        return 0;
    }

    public PluginToolsServer(string home, PluginToolsAt at, PluginLog log, PluginCommandRun.Starter? start = null)
        : this(home, at.Session, at.Place, at.Language, log, start) => _at = at;

    private IReadOnlyList<PluginToolCommand> Tools() =>
        [.. PluginToolList.At(home, _at.Place).Where(tool => _at.Plugin is null || tool.Plugin.Manifest.Id == _at.Plugin)];

    private static readonly object AnswerSchema = new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["state"] = new { type = "string", @enum = new[] { WorkStates.Reached, WorkStates.Failed } },
            ["decision"] = new { type = "string", description = "the route it reached by, one the step declares" },
            ["code"] = new { type = "string", description = "why it failed, a short lowercase code" },
            ["url"] = new { type = "string", description = "the address of what it made, such as the pull request" },
            ["message"] = new { type = "string", description = "what happened, in a sentence" },
            ["outputs"] = new { type = "object", description = "each output the step declares, by name, with its id" },
        },
        required = new[] { "state", "message" },
    };

    private object[] Answering() => _at.Step is null ? [] :
    [
        new
        {
            name = StepAnswer.Tool, title = "Answer the step",
            description = "Says what the step came to, once: reached with its evidence, or failed with a code. The step's answer is this call.",
            inputSchema = AnswerSchema, annotations = new { readOnlyHint = false, openWorldHint = false },
        },
    ];

    private object StepAnswered(JsonElement? arguments)
    {
        var refused = StepAnswer.AskOf(home, _at.Step!) is { } ask ? StepAnswer.Answer(home, ask, arguments) : "this step's ask is not kept here.";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["content"] = new[] { new { type = "text", text = refused ?? "The step's answer is kept." } },
            ["isError"] = refused is not null,
        };
    }

    /// <summary>Answers each line until the input ends, one line out per request, nothing for a notification.</summary>
    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken ct)
    {
        while (await input.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (line.Trim().Length == 0) continue;
            object? answer;
            try
            {
                using var document = JsonDocument.Parse(line);
                answer = await AnswerAsync(document.RootElement, ct).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                answer = Failure(null, -32700, "not JSON");
            }

            if (answer is null) continue;
            await output.WriteAsync(JsonSerializer.Serialize(answer, Wire) + "\n").ConfigureAwait(false);
            await output.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>The answer to one frame; null for a notification, which is answered nothing.</summary>
    public async Task<object?> AnswerAsync(JsonElement frame, CancellationToken ct)
    {
        if (frame.ValueKind != JsonValueKind.Object) return Failure(null, -32600, "not a request");
        JsonElement? id = frame.TryGetProperty("id", out var given) && given.ValueKind is JsonValueKind.Number or JsonValueKind.String ? given.Clone() : null;
        var method = frame.TryGetProperty("method", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString() : null;
        if (id is null) return null;
        JsonElement? parameters = frame.TryGetProperty("params", out var held) && held.ValueKind == JsonValueKind.Object ? held : null;
        return method switch
        {
            "initialize" => Success(id, Opened(parameters)),
            "ping" => Success(id, new Dictionary<string, object>()),
            "tools/list" => Success(id, new { tools = Tools().Select(Listed).Concat(Answering()).ToList() }),
            "tools/call" => await CalledAsync(id, parameters, ct).ConfigureAwait(false),
            _ => Failure(id, -32601, $"`{method}` is not a method of this server"),
        };
    }

    private static object Opened(JsonElement? parameters)
    {
        var asked = parameters is { } given && given.TryGetProperty("protocolVersion", out var version) && version.ValueKind == JsonValueKind.String
            ? version.GetString()
            : null;
        return new
        {
            protocolVersion = asked is not null && Versions.Contains(asked) ? asked : Versions[^1],
            capabilities = new { tools = new { listChanged = false } },
            serverInfo = new { name = Name, version = "1" },
        };
    }

    private object Listed(PluginToolCommand tool) => new
    {
        name = tool.Name,
        title = tool.Command.Title.In(_at.Language),
        description = tool.Command.About.In(_at.Language),
        inputSchema = tool.Schema(),
        annotations = new { title = tool.Command.Title.In(_at.Language), readOnlyHint = !tool.Command.Writes, openWorldHint = true },
    };

    private async Task<object> CalledAsync(JsonElement? id, JsonElement? parameters, CancellationToken ct)
    {
        var name = parameters is { } given && given.TryGetProperty("name", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString() : null;
        if (name == StepAnswer.Tool && _at.Step is not null)
            return Success(id, StepAnswered(parameters!.Value.TryGetProperty("arguments", out var answered) ? answered : null));
        if (Tools().FirstOrDefault(tool => tool.Name == name) is not { } tool)
            return Failure(id, -32602, $"no tool `{name}` is handed here: it is not a plugin's tool, or its plugin's tools are switched off at {_at.Place.Key}");
        JsonElement? arguments = parameters!.Value.TryGetProperty("arguments", out var args) ? args : null;
        return Success(id, await PluginToolCall.CallAsync(home, tool, arguments, _at, log, ct, start).ConfigureAwait(false));
    }

    private static object Success(JsonElement? id, object result) => new { jsonrpc = "2.0", id, result };

    private static object Failure(JsonElement? id, int code, string message) => new { jsonrpc = "2.0", id, error = new { code, message } };
}
