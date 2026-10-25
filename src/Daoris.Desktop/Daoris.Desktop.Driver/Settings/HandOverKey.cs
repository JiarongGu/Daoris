using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What an agent's ask to hand its session over to another agent does (MIXAGENT1c, D163 point 3; the mixed agents design §5):
/// <c>ask</c> (the default) waits for the person's yes or no, <c>on</c> takes it when the agent's turn ends, to the next agent in
/// the person's order, <c>off</c> refuses it. Daoris's own offer when an account is near or cut is not this rule's: an agent's ask
/// is judged by the rule alone (D191's settling of the design).
/// </summary>
/// <remarks>An object of one field, so a later field is added beside the mode, never in its place.</remarks>
public sealed record HandOverRule(string Mode)
{
    /// <summary>What stands where nothing is set: an agent's ask waits for the person's yes.</summary>
    public static HandOverRule Default { get; } = new(HandOverKey.Ask);
}

/// <summary>
/// The hand-over rule as <c>driver.json</c> keeps it, <c>handOver</c> and <c>workspaceHandOver</c> (FOUNDATION1 mod-iii): the key's shape,
/// read and written by the settings' own parser. What the rule means, its resolution, edit and words, stays with the rule
/// (<see cref="HandOverRules"/>), which answers through this for what it shares.
/// </summary>
public static class HandOverKey
{
    public const string Ask = "ask";
    public const string On = "on";
    public const string Off = "off";

    public static readonly IReadOnlyList<string> Modes = [Ask, On, Off];

    private const string RuleShape = "a hand-over rule is an object with its `mode`.";
    private const string ModeShape =
        "`mode` is `ask`, `on` or `off`: whether an agent's ask to hand its session over waits for your yes, is taken when its turn ends, or is refused.";

    /// <summary>An entry read as a rule, with its first problem: an object whose mode is one of the three (absent or null is `ask`).</summary>
    public static (HandOverRule? Rule, string? Problem) Read(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return (null, RuleShape);
        object? mode = null;
        if (value.TryGetProperty("mode", out var written) && written.ValueKind != JsonValueKind.Null)
        {
            mode = written.ValueKind == JsonValueKind.String ? written.GetString() : false;
        }

        return Judge(mode);
    }

    /// <summary>A mode judged: the rule it names, or why it is none.</summary>
    internal static (HandOverRule? Rule, string? Problem) Judge(object? mode) =>
        mode is not null && (mode is not string text || !Modes.Contains(text))
            ? (null, ModeShape)
            : (new HandOverRule((string?)mode ?? Ask), null);

    /// <summary>A map of hand-over rules as <c>driver.json</c> holds it: each name trimmed, a blank one and a rule with a problem not read, a name written twice in any case read where first readable.</summary>
    internal static IReadOnlyDictionary<string, HandOverRule> Map(JsonElement root, string name)
    {
        var map = new Dictionary<string, HandOverRule>(StringComparer.OrdinalIgnoreCase);
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
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, HandOverRule> map)
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

    /// <summary>One rule as kept: its mode.</summary>
    public static void Write(Utf8JsonWriter writer, HandOverRule rule)
    {
        writer.WriteStartObject();
        writer.WriteString("mode", rule.Mode);
        writer.WriteEndObject();
    }
}
