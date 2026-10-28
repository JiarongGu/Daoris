using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Where a repository's helpers rule was set: <see cref="HelperRules.DefaultSource"/> where nothing was.</summary>
public sealed record ResolvedHelpers(HelperRule Rule, string Source);

/// <summary>What a set changes, before it is judged: each field null where it is not named; an agents list that is not one is spelled <c>[null]</c>.</summary>
public sealed record HelperSet
{
    public string? Mode { get; init; }
    public IReadOnlyList<string?>? Agents { get; init; }
    public bool ModeGiven { get; init; }

    public bool Names => ModeGiven || Mode is not null || Agents is not null;
}

/// <summary>One change to a helpers rule, as every door makes it: for a repository or a workspace, set what it names, or clear.</summary>
public sealed record HelperEdit
{
    public string? Repository { get; init; }
    public string? Workspace { get; init; }
    public HelperSet? Set { get; init; }
    public bool Clear { get; init; }
}

/// <summary>
/// The helpers rule, <c>helpers</c> and <c>workspaceHelpers</c> in <c>driver.json</c>: read, refused, resolved, said and edited —
/// the one place every door asks. Modelled on <see cref="OpinionRules"/>, simpler: a rule is a mode and the agents.
/// </summary>
/// <remarks>
/// One table, this suite's <c>fixtures/helper-rules.json</c>, cell for cell (<c>HelperRulesTests</c>); its CLI twin, <c>helpers.ts</c>,
/// left with the driver family's TypeScript (PUSH1f2b, D194 point 7). Nothing here starts a helper: the driver's helper session (the
/// next row) reads the rule, and `off` is its to apply.
/// </remarks>
public static class HelperRules
{
    public const string Ask = HelperKey.Ask;
    public const string On = HelperKey.On;
    public const string Off = HelperKey.Off;

    public static readonly IReadOnlyList<string> Modes = HelperKey.Modes;

    public const string DefaultSource = "default";

    internal const string Scope = "a helpers rule is set for a repository or a workspace — name exactly one.";
    private const string OneAtATime = "one change at a time: set its mode or its agents, or clear.";

    /// <summary>An entry read as a rule, with its first problem: an object whose mode is one of the three (absent is `ask`) and whose agents are each named once; the settings' <see cref="HelperKey.Read"/>.</summary>
    public static (HelperRule? Rule, string? Problem) Read(JsonElement value) => HelperKey.Read(value);

    /// <summary>One rule as kept: its mode, then its agents in order (the settings' <see cref="HelperKey.Write"/>).</summary>
    public static void Write(Utf8JsonWriter writer, HelperRule rule) => HelperKey.Write(writer, rule);

    /// <summary>
    /// The rule standing for a repository: its own, else its workspace's (one in no workspace being in <c>default</c>), else
    /// <see cref="HelperRule.Default"/> from <see cref="DefaultSource"/> — names matched in any case, as every door matches them. The
    /// default's agents are left for the driver to read from this machine's accounts (<see cref="HelperRule.Over"/>).
    /// </summary>
    public static ResolvedHelpers Resolve(DriverConfig config, string repository, string? workspace) =>
        config.Helpers.TryGetValue(repository.Trim(), out var own)
            ? new ResolvedHelpers(own, OpinionSource.Repository)
            : config.WorkspaceHelpers.TryGetValue(RemoteTarget.Workspace(workspace), out var shared)
                ? new ResolvedHelpers(shared, OpinionSource.Workspace)
                : new ResolvedHelpers(HelperRule.Default, DefaultSource);

    /// <summary>A rule as the list and the room say it: its mode, then the agents in the order they are tried.</summary>
    public static string Listed(HelperRule rule) =>
        rule.Agents.Count == 0 ? rule.Mode : $"{rule.Mode}: {string.Join(", else ", rule.Agents.Select(agent => $"`{agent}`"))}";

    /// <summary>
    /// What the terminal and Ask Daoris's card say of a mode as it is set (TWINWORDS1, <c>landing-says.json</c>'s <c>helpers</c>): one
    /// function, <c>daoris-driver driver helpers</c>' and the card's.
    /// </summary>
    public static string Says(string mode) => mode switch
    {
        On => "A helper an agent asks for starts at once, within the limits: two at once for a session, one turn each.",
        Off => "A helper is not offered to a session here: only your own press asks one.",
        _ => "Each helper an agent asks for waits for your yes.",
    };

    /// <summary>
    /// One change made to the config, or its refusal thrown in the twin's words, nothing changed — the CLI's
    /// <c>applyHelperEdit</c>. A set changes what it names over the rule set there, or over the default; the key keeps the
    /// spelling first written.
    /// </summary>
    /// <exception cref="DriverException">The edit's refusal, in the twin's words.</exception>
    public static DriverConfig Apply(DriverConfig config, HelperEdit edit)
    {
        var repository = edit.Repository?.Trim() is { Length: > 0 } r ? r : null;
        var workspace = edit.Workspace?.Trim() is { Length: > 0 } w ? w : null;
        if ((repository is null) == (workspace is null)) throw new DriverException(Scope);

        var set = edit.Set is { Names: true } named ? named : null;
        if (new[] { set is not null, edit.Clear }.Count(change => change) != 1) throw new DriverException(OneAtATime);

        var map = repository is not null ? config.Helpers : config.WorkspaceHelpers;
        var scope = (repository ?? workspace)!;
        var key = map.Keys.FirstOrDefault(k => string.Equals(k, scope, StringComparison.OrdinalIgnoreCase)) ?? scope;
        var own = map.TryGetValue(key, out var standing) ? standing : null;

        HelperRule? next = null;
        if (!edit.Clear)
        {
            var judged = HelperKey.Judge(new HelperKey.Spelled(
                set!.ModeGiven || set.Mode is not null ? (object?)set.Mode ?? false : own?.Mode,
                set.Agents ?? (own is null ? null : [.. own.Agents])));
            next = judged.Rule ?? throw new DriverException(judged.Problem!);
        }

        var changed = new Dictionary<string, HelperRule>(map, StringComparer.OrdinalIgnoreCase);
        if (next is null) changed.Remove(key);
        else changed[key] = next;
        return repository is not null ? config with { Helpers = changed } : config with { WorkspaceHelpers = changed };
    }

    /// <summary>
    /// An edit as the screen's route and the shared table spell it: <c>repository</c> or <c>workspace</c>, then <c>set</c> or
    /// <c>clear</c>. A mode that is not a string is kept to be refused in the twin's words, and so is an agents list that is not one.
    /// </summary>
    public static HelperEdit EditOf(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return new HelperEdit();

        HelperSet? set = null;
        if (root.TryGetProperty("set", out var given) && given.ValueKind == JsonValueKind.Object)
        {
            var modeGiven = given.TryGetProperty("mode", out var mode);
            set = new HelperSet
            {
                ModeGiven = modeGiven,
                // A mode that is no string is refused as one that is no mode: the empty word.
                Mode = modeGiven ? mode.ValueKind == JsonValueKind.String ? mode.GetString() : "" : null,
                Agents = given.TryGetProperty("agents", out var agents) ? agents.ValueKind == JsonValueKind.Array ? HelperKey.Texts(agents) : [null] : null,
            };
        }

        return new HelperEdit
        {
            Repository = Json.Text(root, "repository"),
            Workspace = Json.Text(root, "workspace"),
            Set = set,
            Clear = root.TryGetProperty("clear", out var clear) && clear.ValueKind == JsonValueKind.True,
        };
    }

    /// <summary>A rule as compact JSON, for a test to compare with the shared table's cells.</summary>
    public static string ToJson(HelperRule rule)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            Write(writer, rule);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
