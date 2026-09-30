using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One model's own effort, which the tool reads before the account's for that model.</summary>
public sealed record ModelEffort(string Model, string Effort);

/// <summary>What an account's settings file says about its model and effort, or why it could not be read.</summary>
/// <param name="Model">The account's model, or null for the tool's own default.</param>
/// <param name="Effort">The account's effort, or null for the tool's own default.</param>
/// <param name="PerModel">The efforts set per model, in the file's order.</param>
/// <param name="Problem">The sentence saying why the file could not be read; null when it could, or was absent.</param>
public sealed record AgentSettingsRead(
    string? Model, string? Effort, IReadOnlyList<ModelEffort> PerModel, string? Problem);

/// <summary>One change to a key: a value sets it, and null clears it.</summary>
public sealed record AgentSettingEdit(string? Value);

/// <summary>
/// An account's own settings — its model and its effort — in the tool's own file (AGT6, D98).
/// </summary>
/// <remarks>
/// <para><b>The file is the account's, and the tool's.</b> Claude Code keeps an account's defaults in
/// <c>settings.json</c> in its configuration home: the user tier of its settings cascade, and the file
/// its ACP adapter reads at <c>CLAUDE_CONFIG_DIR/settings.json</c> (<c>claude-agent-acp</c> 0.84.0,
/// <c>dist/settings.js</c>). A Daoris account IS such a home (D49 §4), so Daoris changes the keys a
/// person asked it to — <c>model</c>, <c>effortLevel</c>, and an effort per model under
/// <c>modelSettings</c> — and nothing else in it.</para>
///
/// <para>The twin of the CLI's <c>agentsettings.ts</c>. They share no code; the FILE is the contract, and
/// the same seven rules are asserted on both sides (<c>AgentSettingsTests</c>):</para>
/// <list type="number">
/// <item>The file is the tool's own <c>settings.json</c>, in the account's configuration home.</item>
/// <item>No file is the tool's defaults: nothing set, and reading creates nothing.</item>
/// <item>A file that is not a JSON object is a sentence to a read and a refusal to a write, which leaves
/// it exactly as it was.</item>
/// <item>A write changes only what it names; every other key keeps its place and its value.</item>
/// <item>An effort is low, medium, high or xhigh — <c>max</c> is refused, since the tool keeps it for
/// one session only; a model is one word, and the tool's own aliases are offered first.</item>
/// <item>Clearing removes the key; an entry per model left empty goes, and so does an empty
/// <c>modelSettings</c>.</item>
/// <item>A new file holds exactly what was written, in the tool's formatting: two-space indent, LF, and
/// a final newline.</item>
/// </list>
/// </remarks>
public static class AgentSettings
{
    /// <summary>The tool's own settings file, in the account's configuration home. The CLI's <c>AGENT_SETTINGS_FILE</c>.</summary>
    public const string FileName = "settings.json";

    /// <summary>
    /// The model names the tool itself accepts as aliases, offered before a free field for a full id: the
    /// Agent SDK's list at 0.3.284 (the one <c>claude-agent-acp</c> 0.84.0 carries), with <c>default</c>,
    /// the tool's own default. The CLI's <c>AGENT_MODELS</c> is the other copy.
    /// </summary>
    public static readonly IReadOnlyList<string> Models =
        ["default", "sonnet", "opus", "haiku", "fable", "best", "sonnet[1m]", "opus[1m]", "fable[1m]", "opusplan"];

    /// <summary>
    /// The efforts the tool's settings keep: its schema's <c>effortLevel</c>. <c>max</c> exists for one
    /// session and is never written to a file. The CLI's <c>AGENT_EFFORTS</c> is the other copy.
    /// </summary>
    public static readonly IReadOnlyList<string> Efforts = ["low", "medium", "high", "xhigh"];

    /// <summary>What the account's file says about its model and effort (rules 1–3).</summary>
    public static AgentSettingsRead Read(string path)
    {
        var (root, _, problem) = Load(path);
        if (root is null) return new AgentSettingsRead(null, null, [], problem);

        var perModel = new List<ModelEffort>();
        if (root["modelSettings"] is JsonObject models)
        {
            foreach (var (model, entry) in models)
            {
                if (entry is JsonObject held && Text(held["effortLevel"]) is { } effort) perModel.Add(new ModelEffort(model, effort));
            }
        }

        // The tool ignores a model that is not text, so it is not set as far as anyone can tell.
        return new AgentSettingsRead(Text(root["model"]), Text(root["effortLevel"]), perModel, null);
    }

    /// <summary>
    /// Change the named keys in the account's file and nothing else (rules 3–7), then read it back.
    /// </summary>
    /// <remarks>
    /// Every value is judged before anything is written, so a refused edit leaves the file as it was. A
    /// write that would change nothing writes nothing: the file is the tool's, and a rewrite that only
    /// re-spaced it would still be a change to somebody else's file.
    /// </remarks>
    /// <param name="perModel">An effort per model, by the tool's canonical model name.</param>
    /// <exception cref="DriverException">A value the tool would not read, or a file this build cannot.</exception>
    public static AgentSettingsRead Write(
        string path, AgentSettingEdit? model = null, AgentSettingEdit? effort = null,
        IReadOnlyDictionary<string, AgentSettingEdit>? perModel = null)
    {
        var modelValue = model?.Value is { } m ? JudgeModel(m) : null;
        var effortValue = effort?.Value is { } e ? JudgeEffort(e) : null;
        var perModelValues = (perModel ?? new Dictionary<string, AgentSettingEdit>())
            .Select(entry => (Model: JudgeModel(entry.Key), Effort: entry.Value.Value is { } v ? JudgeEffort(v) : null))
            .ToList();

        var (root, text, problem) = Load(path);
        if (root is null) throw Unwritable(problem!);

        if (model is not null)
        {
            if (modelValue is null) root.Remove("model");
            else root["model"] = modelValue;
        }

        if (effort is not null)
        {
            if (effortValue is null) root.Remove("effortLevel");
            else root["effortLevel"] = effortValue;
        }

        foreach (var (name, value) in perModelValues)
        {
            if (root["modelSettings"] is null && !root.ContainsKey("modelSettings"))
            {
                if (value is null) continue;
                root["modelSettings"] = new JsonObject();
            }

            if (root["modelSettings"] is not JsonObject settings)
            {
                throw Unwritable($"its `modelSettings` in `{path}` is not an object");
            }

            if (!settings.ContainsKey(name))
            {
                if (value is null) continue;
                settings[name] = new JsonObject();
            }

            if (settings[name] is not JsonObject entry)
            {
                throw Unwritable($"its entry for `{name}` in `{path}` is not an object");
            }

            if (value is null)
            {
                entry.Remove("effortLevel");
                if (entry.Count == 0) settings.Remove(name);
            }
            else
            {
                entry["effortLevel"] = value;
            }

            if (settings.Count == 0) root.Remove("modelSettings");
        }

        // The tool's own formatting: two-space indent, LF, and the final newline it had (a new file gets one).
        var next = root.ToJsonString(Written);
        if (text is null || text.EndsWith('\n')) next += "\n";
        if (next != text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            AtomicFile.WriteText(path, next);
        }

        return Read(path);
    }

    /// <summary>A model name the tool could be handed: one word, with no space in it and nothing unprintable.</summary>
    public static string JudgeModel(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Length > 200 || trimmed.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            throw new DriverException(
                $"`{value}` is not a model name the tool could read — one word, such as {string.Join(", ", Models.Skip(1).Take(3))} "
                + "or a full model id.");
        }

        return trimmed;
    }

    /// <summary>An effort the tool's settings keep (rule 5).</summary>
    public static string JudgeEffort(string value)
    {
        var trimmed = value.Trim();
        if (trimmed == "max")
        {
            throw new DriverException(
                "`max` is an effort the tool keeps for one session only — its settings never hold it, so it cannot be "
                + $"an account's default. Choose it for one conversation instead, or one of {string.Join(", ", Efforts)}.");
        }

        return Efforts.Contains(trimmed, StringComparer.Ordinal)
            ? trimmed
            : throw new DriverException(
                $"`{value}` is not an effort the tool's settings keep — one of {string.Join(", ", Efforts)}.");
    }

    /// <summary>The file as an object, or why it is not one. Absent is an empty object and no problem.</summary>
    private static (JsonObject? Root, string? Text, string? Problem) Load(string path)
    {
        if (!File.Exists(path)) return (new JsonObject(), null, null);

        var text = File.ReadAllText(path).ReplaceLineEndings("\n");
        try
        {
            return JsonNode.Parse(text) is JsonObject root
                ? (root, text, null)
                : (null, text, $"`{path}` is not a JSON object");
        }
        catch (JsonException error)
        {
            return (null, text, $"`{path}` could not be read ({error.Message})");
        }
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    private static DriverException Unwritable(string problem) =>
        new($"{problem}, so nothing was written to it — fix it, or change the setting with the tool itself.");

    /// <summary>
    /// The tool's own formatting, as near as a re-serialisation comes: two-space indent, LF, and text left
    /// as text rather than escaped — the same options the trust flag's writer uses.
    /// </summary>
    private static readonly JsonSerializerOptions Written = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
