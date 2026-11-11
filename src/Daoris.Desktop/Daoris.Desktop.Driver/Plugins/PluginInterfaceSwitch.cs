using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The person's word about each interface a plugin implements, apart from the plugin's own switch: <c>plugins.json</c>'s <c>off</c>,
/// by plugin id, the interfaces switched off. An interface is on until the person switches it off; an unreadable file switches
/// nothing off, and an edit over it is refused as <see cref="PluginState"/>'s is.
/// </summary>
public static class PluginInterfaceSwitch
{
    private const string Key = "off";

    /// <summary>The interfaces of <paramref name="id"/> switched off.</summary>
    public static IReadOnlyList<string> Off(string home, string id) => Read(home).GetValueOrDefault(id) ?? [];

    public static bool IsOn(string home, string id, string @interface) => !Off(home, id).Contains(@interface, StringComparer.Ordinal);

    /// <summary>Switch one interface of a plugin on or off.</summary>
    /// <exception cref="DriverException">An interface this build does not know, or a file that does not read.</exception>
    public static void Set(string home, string id, string @interface, bool on)
    {
        if (!PluginInterfaces.All.Contains(@interface, StringComparer.Ordinal))
            throw new DriverException($"`{@interface}` is no interface a plugin implements: one of {string.Join(", ", PluginInterfaces.All)}.");
        _ = PluginState.Editable(home);
        var read = Read(home);
        var edited = read.ToDictionary(each => each.Key, each => each.Value, StringComparer.Ordinal);
        var off = (edited.GetValueOrDefault(id) ?? []).Where(each => each != @interface).ToList();
        if (!on) off.Add(@interface);
        if (off.Count == 0) edited.Remove(id);
        else edited[id] = [.. off.Order(StringComparer.Ordinal)];
        var path = PluginState.PathIn(home);
        JsonKeeping.Save(path, File.Exists(path), Written(read), Written(edited));
    }

    private static Dictionary<string, IReadOnlyList<string>> Read(string home)
    {
        var off = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        try
        {
            if (PluginFolders.TextIfThere(PluginState.PathIn(home)) is not { } text) return off;
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty(Key, out var rows)
                || rows.ValueKind != JsonValueKind.Object) return off;
            foreach (var row in rows.EnumerateObject().Where(row => row.Value.ValueKind == JsonValueKind.Array))
            {
                off[row.Name] = [.. row.Value.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)];
            }
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            // Read as nothing switched off, the plugin as installed; an edit is refused by the state's own check.
        }

        return off;
    }

    private static string Written(Dictionary<string, IReadOnlyList<string>> off) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartObject(Key);
        foreach (var (id, interfaces) in off.OrderBy(each => each.Key, StringComparer.Ordinal))
        {
            writer.WriteStartArray(id);
            foreach (var each in interfaces) writer.WriteStringValue(each);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    });
}
