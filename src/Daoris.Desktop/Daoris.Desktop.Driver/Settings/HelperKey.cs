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
    public static HelperRule Default { get; } = new(HelperKey.On, []) { SetUp = true };

    /// <summary>
    /// The default's agents are this machine's set-up ones (HELPERDEFAULT1): an agent's ask naming another is refused
    /// <see cref="HelperWhy.NotSetUp"/>, and one naming none with none set up <see cref="HelperWhy.NoneSetUp"/>.
    /// </summary>
    public bool SetUp { get; init; }

    /// <summary>The default with this machine's set-up agents as its agents; a rule the person set is returned as it is.</summary>
    public HelperRule Over(IReadOnlyList<string> setUp) => SetUp ? this with { Agents = setUp } : this;
}

/// <summary>
/// The helpers rule as <c>driver.json</c> keeps it, <c>helpers</c> and <c>workspaceHelpers</c> (FOUNDATION1 mod-iii): the key's shape,
/// read and written by the settings' own parser. What the rule means, its resolution, edit and words, stays with the rule
/// (<see cref="HelperRules"/>), which answers through this for what it shares.
/// </summary>
public static class HelperKey
{
    public const string Ask = "ask";
    public const string On = "on";
    public const string Off = "off";

    public static readonly IReadOnlyList<string> Modes = [Ask, On, Off];

    private const string RuleShape = "a helpers rule is an object with its `mode` and the `agents` it may ask.";
    private const string ModeShape = "`mode` is `ask`, `on` or `off`: whether a helper an agent asks for waits for your yes, starts at once, or is not offered.";
    private const string AgentShape = "each agent is an adapter's name, such as `codex-acp` or `dsh`.";

    /// <summary>A rule as an entry or a door spells it, before it is judged: a null mode or agents is absent.</summary>
    internal sealed record Spelled(object? Mode, IReadOnlyList<string?>? Agents);

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

    /// <summary>A spelling judged: the rule it names, or why it is none.</summary>
    internal static (HelperRule? Rule, string? Problem) Judge(Spelled spelled)
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

    /// <summary>A list's entries as text, each that is not text as null.</summary>
    internal static List<string?> Texts(JsonElement list) =>
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
}
