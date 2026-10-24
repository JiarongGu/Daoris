using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a helper an agent asks for does (MIXAGENT2a-i, D163 point 5; the mixed agents design §5): <c>ask</c> waits for the person's
/// yes, <c>on</c> starts it at once within the limits, <c>off</c> offers no tool. <see cref="Agents"/> are the adapters the person
/// lets be asked, in the order they are tried; none named is any other maker's first (D155 point 4).
/// </summary>
public sealed record HelperRule(string Mode, IReadOnlyList<string> Agents)
{
    /// <summary>
    /// What stands where nothing is set (HELPERDEFAULT1, D163's 2026-10-24 note): <c>on</c> for Daoris's own agents, those it has set
    /// up and signed in on this machine (<see cref="SetUp"/>), which the driver reads from its accounts at each judgement
    /// (<see cref="Over"/>). Never written to <c>driver.json</c>: a rule the person sets there replaces it whole.
    /// </summary>
    public static HelperRule Default { get; } = new(HelperRules.On, []) { SetUp = true };

    /// <summary>
    /// The default's agents are this machine's set-up ones (HELPERDEFAULT1): an agent's ask naming another is refused
    /// <see cref="HelperWhy.NotSetUp"/>, and one naming none with none set up <see cref="HelperWhy.NoneSetUp"/>.
    /// </summary>
    public bool SetUp { get; init; }

    /// <summary>The default with this machine's set-up agents as its agents; a rule the person set is returned as it is.</summary>
    public HelperRule Over(IReadOnlyList<string> setUp) => SetUp ? this with { Agents = setUp } : this;
}

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
    public const string Ask = "ask";
    public const string On = "on";
    public const string Off = "off";

    public static readonly IReadOnlyList<string> Modes = [Ask, On, Off];

    public const string DefaultSource = "default";

    private const string RuleShape = "a helpers rule is an object with its `mode` and the `agents` it may ask.";
    private const string ModeShape = "`mode` is `ask`, `on` or `off`: whether a helper an agent asks for waits for your yes, starts at once, or is not offered.";
    private const string AgentShape = "each agent is an adapter's name, such as `codex-acp` or `dsh`.";
    internal const string Scope = "a helpers rule is set for a repository or a workspace — name exactly one.";
    private const string OneAtATime = "one change at a time: set its mode or its agents, or clear.";

    /// <summary>A rule as an entry or a door spells it, before it is judged: a null mode or agents is absent.</summary>
    private sealed record Spelled(object? Mode, IReadOnlyList<string?>? Agents);

    /// <summary>An entry read as a rule, with its first problem: an object whose mode is one of the three (absent is `ask`) and whose agents are each named once.</summary>
    public static (HelperRule? Rule, string? Problem) Read(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return (null, RuleShape);
        object? mode = null;
        if (value.TryGetProperty("mode", out var written) && written.ValueKind != JsonValueKind.Null)
        {
            mode = written.ValueKind == JsonValueKind.String ? written.GetString() : false;
        }

        IReadOnlyList<string?>? agents = null;
        if (value.TryGetProperty("agents", out var listed) && listed.ValueKind != JsonValueKind.Null)
        {
            agents = listed.ValueKind == JsonValueKind.Array ? Texts(listed) : [null];
        }

        return Judge(new Spelled(mode, agents));
    }

    private static (HelperRule? Rule, string? Problem) Judge(Spelled spelled)
    {
        if (spelled.Mode is not null && (spelled.Mode is not string text || !Modes.Contains(text))) return (null, ModeShape);
        var agents = new List<string>();
        foreach (var each in spelled.Agents ?? [])
        {
            if (each is null || each.Trim().Length == 0) return (null, AgentShape);
            var agent = each.Trim();
            if (agents.Any(kept => string.Equals(kept, agent, StringComparison.OrdinalIgnoreCase)))
            {
                return (null, $"`{agent}` is named twice — each agent once, in the order they are tried.");
            }

            agents.Add(agent);
        }

        return (new HelperRule((string?)spelled.Mode ?? Ask, agents), null);
    }

    private static List<string?> Texts(JsonElement list) =>
        [.. list.EnumerateArray().Select(each => each.ValueKind == JsonValueKind.String ? each.GetString() : null)];

    /// <summary>A map of helpers rules as <c>driver.json</c> holds it: each name trimmed, a blank one and a rule with a problem not read, a name written twice in any case read where first readable.</summary>
    internal static IReadOnlyDictionary<string, HelperRule> Map(JsonElement root, string name)
    {
        var map = new Dictionary<string, HelperRule>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            var named = property.Name.Trim();
            if (named.Length == 0 || Read(property.Value) is not ({ } rule, null) || map.ContainsKey(named)) continue;
            map[named] = rule;
        }

        return map;
    }

    /// <summary>A map written as <c>driver.json</c> keeps it, only when set.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, HelperRule> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, rule) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(key);
            Write(writer, rule);
        }

        writer.WriteEndObject();
    }

    /// <summary>One rule as kept: its mode, then its agents in order.</summary>
    public static void Write(Utf8JsonWriter writer, HelperRule rule)
    {
        writer.WriteStartObject();
        writer.WriteString("mode", rule.Mode);
        writer.WriteStartArray("agents");
        foreach (var agent in rule.Agents) writer.WriteStringValue(agent);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

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
            var judged = Judge(new Spelled(
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
                Agents = given.TryGetProperty("agents", out var agents) ? agents.ValueKind == JsonValueKind.Array ? Texts(agents) : [null] : null,
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
