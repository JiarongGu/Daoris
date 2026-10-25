using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Which checks a development pipeline's sessions run, as <c>driver.json</c> keeps them, <c>sessionChecks</c> (FABRIC-PIPELINE1b, D192
/// point 4; FOUNDATION1 mod-iii): the key, the choices and the reading and writing of the map, by the settings' own parser. What a
/// choice means, its edit, its holding for a session and where a run reads it, stays with the checks (<see cref="SessionChecks"/>),
/// which answers through this for what it shares.
/// </summary>
public static class SessionChecksKey
{
    /// <summary>The file's key.</summary>
    public const string Key = "sessionChecks";

    /// <summary>The gates the repository does not mark heavy and the checks a change reaches; the heavy gates are the verification's.</summary>
    public const string Light = "light";

    /// <summary>The checks a change reaches while it works, then the heavy gates once, at the session's end.</summary>
    public const string HeavyAtEnd = "heavy-at-end";

    /// <summary>The choices, in the order a door offers them. The CLI's <c>SESSION_CHECKS</c> is a deliberate copy.</summary>
    public static IReadOnlyList<string> Choices { get; } = [Light, HeavyAtEnd];

    /// <summary>A choice as kept, trimmed and matched in any case, in its own spelling; null for anything else.</summary>
    public static string? Choice(string? said) =>
        said?.Trim() is { } trimmed ? Choices.FirstOrDefault(choice => string.Equals(choice, trimmed, StringComparison.OrdinalIgnoreCase)) : null;

    /// <summary>
    /// The map as <c>driver.json</c> holds it: each repository's name trimmed, a blank one and a value no choice passed over, a name written
    /// twice in any case read where first readable; none where the key is absent or no object.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Map(JsonElement root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(Key, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            var named = property.Name.Trim();
            if (named.Length == 0 || map.ContainsKey(named) || property.Value.ValueKind != JsonValueKind.String) continue;
            if (Choice(property.Value.GetString()) is { } choice) map[named] = choice;
        }

        return map;
    }

    /// <summary>The map written as <c>driver.json</c> keeps it, only when set, repositories in ordinal order.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(Key);
        foreach (var (repository, checks) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteString(repository, checks);
        writer.WriteEndObject();
    }
}
