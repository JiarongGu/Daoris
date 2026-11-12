using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The growth switch as <c>driver.json</c> keeps it, <c>growth</c> by repository and <c>workspaceGrowth</c> by workspace: whether an agent's
/// press that opens a department (<c>repository create</c>) or a room (<c>place --room</c>) runs as the access mode says (<see cref="Mode"/>,
/// the default), is put to the person on a card even in full access (<see cref="Asked"/>), or is refused even there (<see cref="Off"/>). The key's
/// shape, read and written by the settings' own parser; what the switch means, its resolution, edit and words are <see cref="GrowthRules"/>'s.
/// </summary>
public static class GrowthKey
{
    public const string Mode = "mode";
    public const string Asked = "asked";
    public const string Off = "off";

    public static readonly IReadOnlyList<string> Values = [Mode, Asked, Off];

    /// <summary>Whether <paramref name="value"/> is one of the three, in the case the file keeps.</summary>
    public static bool Is(string? value) => value is not null && Values.Contains(value, StringComparer.Ordinal);

    /// <summary>A map as <c>driver.json</c> holds it: each name trimmed, a blank one and a value that is none of the three not read, a name written twice in any case read where first readable.</summary>
    internal static IReadOnlyDictionary<string, string> Map(JsonElement root, string name)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            var named = property.Name.Trim();
            if (named.Length == 0 || property.Value.ValueKind != JsonValueKind.String || property.Value.GetString() is not { } value || !Is(value) || map.ContainsKey(named)) continue;
            map[named] = value;
        }

        return map;
    }

    /// <summary>A map written as <c>driver.json</c> keeps it, only when set.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, string> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(name);
        foreach (var (key, value) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal)) writer.WriteString(key, value);
        writer.WriteEndObject();
    }
}
