using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A brainstorm's minutes (D176 points 6 and 9): each generator's, the synthesis's, the challenge's and the whole request's.</summary>
public sealed record BrainstormMinutes(int Pass, int Synthesis, int Challenge, int Request)
{
    /// <summary>D176 point 9's proposed defaults: ten a generator, ten the synthesis, ten the challenge, thirty the request.</summary>
    public static BrainstormMinutes Default { get; } = new(10, 10, 10, 30);

    /// <summary>The generators' minutes: the request's less the synthesis's and the challenge's, reserved from the start (point 6).</summary>
    public int Generators => Request - Synthesis - Challenge;
}

/// <summary>
/// The person's brainstorm policy (BRAINSTORM1b, D176 point 9): whether a brainstorm an agent asks for waits for the person's yes
/// (<c>ask</c>, the default) or is refused (<c>off</c>); the agents a brainstorm runs, in order, none named leaving each request to
/// name its own; and its minutes.
/// </summary>
public sealed record BrainstormRule(string Mode, IReadOnlyList<string> Agents, BrainstormMinutes Minutes)
{
    /// <summary>What stands where nothing is set: D176 point 9's proposed defaults.</summary>
    public static BrainstormRule Default { get; } = new(BrainstormKey.Ask, [], BrainstormMinutes.Default);
}

/// <summary>
/// The brainstorm policy as <c>driver.json</c> keeps it, <c>brainstorms</c> (this machine's rule) and <c>workspaceBrainstorms</c>
/// (FOUNDATION1 mod-iii): the key's shape, its limits and the sentences that say what does not read, read and written by the
/// settings' own parser. What the policy means, its resolution, edit and words, stays with the brainstorms
/// (<see cref="BrainstormRules"/>), which answers through this for what it shares.
/// </summary>
public static class BrainstormKey
{
    public const string Ask = "ask";
    public const string Off = "off";

    public static readonly IReadOnlyList<string> Modes = [Ask, Off];

    /// <summary>The most agents one brainstorm runs (D176 point 9: at most four makers).</summary>
    public const int MostAgents = 4;

    /// <summary>The minutes' names, in the order they are kept.</summary>
    public static readonly IReadOnlyList<string> MinuteNames = ["pass", "synthesis", "challenge", "request"];

    /// <summary>What a set's minutes are where they are no object: refused as minutes.</summary>
    public static readonly IReadOnlyDictionary<string, object?> NotMinutes = new Dictionary<string, object?> { [""] = false };

    private const string RuleShape = "a brainstorms rule is an object with its `mode`, the `agents` it runs and its `minutes`.";
    private const string ModeShape = "`mode` is `ask` or `off`: whether a brainstorm an agent asks for waits for your yes, or is refused.";
    private const string AgentShape = "each agent is an adapter's name, such as `codex-acp` or `dsh`.";
    private const string MinutesShape = "`minutes` holds whole numbers: `pass`, `synthesis` and `challenge` from 1 to 60 each, and `request` up to 240.";

    /// <summary>
    /// A rule as an entry or a door spells it, before it is judged: a null part is absent. Each minute is null where absent, a
    /// double where it is a number, and <c>false</c> where it is anything else.
    /// </summary>
    internal sealed record Spelled(object? Mode, IReadOnlyList<string?>? Agents, IReadOnlyDictionary<string, object?>? Minutes);

    /// <summary>An entry read as a rule, with its first problem: its mode, then its agents, then its minutes.</summary>
    public static (BrainstormRule? Rule, string? Problem) Read(JsonElement value)
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

        IReadOnlyDictionary<string, object?>? minutes = null;
        if (value.TryGetProperty("minutes", out var timed) && timed.ValueKind != JsonValueKind.Null) minutes = MinutesOf(timed);

        return Judge(new Spelled(mode, agents, minutes));
    }

    /// <summary>Minutes as spelled: each named one a number or <c>false</c>, an absent or null one not there.</summary>
    internal static IReadOnlyDictionary<string, object?> MinutesOf(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return NotMinutes;
        var minutes = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var name in MinuteNames)
        {
            if (!value.TryGetProperty(name, out var minute) || minute.ValueKind == JsonValueKind.Null) continue;
            minutes[name] = minute.ValueKind == JsonValueKind.Number && minute.TryGetDouble(out var number) ? number : false;
        }

        return minutes;
    }

    internal static (BrainstormRule? Rule, string? Problem) Judge(Spelled spelled, BrainstormMinutes? over = null)
    {
        if (spelled.Mode is not null && (spelled.Mode is not string text || !Modes.Contains(text))) return (null, ModeShape);
        var agents = new List<string>();
        foreach (var each in spelled.Agents ?? [])
        {
            if (each is null || each.Trim().Length == 0) return (null, AgentShape);
            var agent = each.Trim();
            if (agents.Any(kept => string.Equals(kept, agent, StringComparison.OrdinalIgnoreCase)))
            {
                return (null, $"`{agent}` is named twice — each agent once, in the order they run.");
            }

            agents.Add(agent);
        }

        if (agents.Count > MostAgents) return (null, $"a brainstorm runs at most {MostAgents} agents; {agents.Count} are named.");

        var standing = over ?? BrainstormMinutes.Default;
        var given = spelled.Minutes ?? new Dictionary<string, object?>();
        if (ReferenceEquals(given, NotMinutes)) return (null, MinutesShape);
        var values = new int[MinuteNames.Count];
        int[] kept = [standing.Pass, standing.Synthesis, standing.Challenge, standing.Request];
        for (var at = 0; at < MinuteNames.Count; at++)
        {
            if (!given.TryGetValue(MinuteNames[at], out var minute) || minute is null)
            {
                values[at] = kept[at];
                continue;
            }

            var most = MinuteNames[at] == "request" ? 240 : 60;
            if (minute is not double number || number != Math.Floor(number) || number < 1 || number > most) return (null, MinutesShape);
            values[at] = (int)number;
        }

        var minutes = new BrainstormMinutes(values[0], values[1], values[2], values[3]);
        if (minutes.Generators < 1)
        {
            return (null, string.Create(CultureInfo.InvariantCulture,
                $"the request's minutes are more than the synthesis's and the challenge's together, which are reserved from the start: {minutes.Synthesis} and {minutes.Challenge} here."));
        }

        return (new BrainstormRule((string?)spelled.Mode ?? Ask, agents, minutes), null);
    }

    internal static List<string?> Texts(JsonElement list) =>
        [.. list.EnumerateArray().Select(each => each.ValueKind == JsonValueKind.String ? each.GetString() : null)];

    /// <summary>This machine's rule as <c>driver.json</c> holds it, <c>brainstorms</c>: null where it is absent or has a problem.</summary>
    internal static BrainstormRule? Machine(JsonElement root) =>
        root.TryGetProperty("brainstorms", out var element) && Read(element) is ({ } rule, null) ? rule : null;

    /// <summary>A map of rules as <c>driver.json</c> holds it: each name trimmed, a blank one and a rule with a problem not read, a name written twice in any case read where first readable.</summary>
    internal static IReadOnlyDictionary<string, BrainstormRule> Map(JsonElement root, string name)
    {
        var map = new Dictionary<string, BrainstormRule>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            var named = property.Name.Trim();
            if (named.Length == 0 || Read(property.Value) is not ({ } rule, null) || map.ContainsKey(named)) continue;
            map[named] = rule;
        }

        return map;
    }

    /// <summary>This machine's rule written as <c>driver.json</c> keeps it, only when set.</summary>
    internal static void WriteMachine(Utf8JsonWriter writer, BrainstormRule? rule)
    {
        if (rule is null) return;
        writer.WritePropertyName("brainstorms");
        Write(writer, rule);
    }

    /// <summary>A map written as <c>driver.json</c> keeps it, only when set.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, BrainstormRule> map)
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

    /// <summary>One rule as kept, whole: its mode, its agents in order, and its four minutes.</summary>
    public static void Write(Utf8JsonWriter writer, BrainstormRule rule)
    {
        writer.WriteStartObject();
        writer.WriteString("mode", rule.Mode);
        writer.WriteStartArray("agents");
        foreach (var agent in rule.Agents) writer.WriteStringValue(agent);
        writer.WriteEndArray();
        writer.WriteStartObject("minutes");
        writer.WriteNumber("pass", rule.Minutes.Pass);
        writer.WriteNumber("synthesis", rule.Minutes.Synthesis);
        writer.WriteNumber("challenge", rule.Minutes.Challenge);
        writer.WriteNumber("request", rule.Minutes.Request);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
