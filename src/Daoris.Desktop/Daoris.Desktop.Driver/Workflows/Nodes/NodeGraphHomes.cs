using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A graph's JSON, compact, as the config keeps it (the settings' <see cref="CycleText"/>).</summary>
public static class NodeGraphHomes
{
    /// <summary>
    /// <paramref name="json"/> as the config keeps it: compact, its keys in the order written, its text unescaped where JSON allows, so the
    /// file says what the person wrote. Throws <see cref="JsonException"/> where it is not JSON (the settings' <see cref="CycleText.Compact(string)"/>).
    /// </summary>
    public static string Compact(string json) => CycleText.Compact(json);

    /// <summary><paramref name="element"/>'s JSON, compact (the settings' <see cref="CycleText.Compact(JsonElement)"/>).</summary>
    public static string Compact(JsonElement element) => CycleText.Compact(element);
}
