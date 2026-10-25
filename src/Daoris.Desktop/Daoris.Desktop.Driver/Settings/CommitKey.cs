using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// How work is committed in a repository (PROCESS1a, D164's amendment point 1): <c>as-you-go</c> (the default) commits each piece
/// that passes its checks, <c>ask</c> waits for the person's yes before each commit, <c>at-end</c> makes one commit when the work
/// is done. <see cref="Convention"/> is the person's words on how a commit is written: one paragraph, trimmed, none where blank.
/// </summary>
public sealed record CommitRule(string Mode, string? Convention)
{
    /// <summary>What stands where nothing is set: commits as the work goes, with no convention named.</summary>
    public static CommitRule Default { get; } = new(CommitKey.AsYouGo, null);
}

/// <summary>
/// The commit rule as <c>driver.json</c> keeps it, <c>commits</c> and <c>workspaceCommits</c> (PROCESS1a, D164's amendment point 1;
/// FOUNDATION1 mod-iii): the key's shape, its limits and the sentences that say what does not read, read and written by the settings'
/// own parser. What the rule means, its resolution, edit and words, stays with the commit rule (<see cref="CommitRules"/>), which
/// answers through this for what it shares.
/// </summary>
public static class CommitKey
{
    public const string AsYouGo = "as-you-go";
    public const string Ask = "ask";
    public const string AtEnd = "at-end";

    public static readonly IReadOnlyList<string> Modes = [AsYouGo, Ask, AtEnd];

    /// <summary>The longest convention kept, in characters.</summary>
    public const int ConventionLimit = 400;

    private const string RuleShape = "a commit rule is an object with its `mode` and an optional `convention`.";
    private const string ModeShape = "`mode` is `as-you-go`, `ask` or `at-end`: whether commits are made as the work goes, wait for your yes first, or are made once at the end.";
    private const string ConventionShape = "`convention` is your words on how a commit is written: one paragraph with no line break, at most 400 characters.";
    /// <summary>A rule as an entry or a door spells it, before it is judged: a null mode or convention is absent.</summary>
    internal sealed record Spelled(object? Mode, object? Convention);

    /// <summary>An entry read as a rule, with its first problem: an object whose mode is one of the three (absent is `as-you-go`) and whose convention is one short paragraph.</summary>
    public static (CommitRule? Rule, string? Problem) Read(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return (null, RuleShape);
        return Judge(new Spelled(Spell(value, "mode"), Spell(value, "convention")));
    }

    private static object? Spell(JsonElement value, string name) =>
        !value.TryGetProperty(name, out var written) || written.ValueKind == JsonValueKind.Null
            ? null
            : written.ValueKind == JsonValueKind.String ? written.GetString() : false;

    internal static (CommitRule? Rule, string? Problem) Judge(Spelled spelled)
    {
        if (spelled.Mode is not null && (spelled.Mode is not string text || !Modes.Contains(text))) return (null, ModeShape);
        string? convention = null;
        if (spelled.Convention is not null)
        {
            if (spelled.Convention is not string words) return (null, ConventionShape);
            convention = words.Trim();
            if (convention.Length > ConventionLimit || convention.Contains('\n') || convention.Contains('\r')) return (null, ConventionShape);
            if (convention.Length == 0) convention = null;
        }

        return (new CommitRule((string?)spelled.Mode ?? AsYouGo, convention), null);
    }

    /// <summary>A map of commit rules as <c>driver.json</c> holds it: each name trimmed, a blank one and a rule with a problem not read, a name written twice in any case read where first readable.</summary>
    internal static IReadOnlyDictionary<string, CommitRule> Map(JsonElement root, string name)
    {
        var map = new Dictionary<string, CommitRule>(StringComparer.OrdinalIgnoreCase);
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
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, CommitRule> map)
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

    /// <summary>One rule as kept: its mode, then its convention where it has one.</summary>
    public static void Write(Utf8JsonWriter writer, CommitRule rule)
    {
        writer.WriteStartObject();
        writer.WriteString("mode", rule.Mode);
        if (rule.Convention is not null) writer.WriteString("convention", rule.Convention);
        writer.WriteEndObject();
    }
}
