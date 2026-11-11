using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The one resolver of the person's word on a plugin's interfaces and points, per place, read at every door that reaches a plugin:
/// <c>plugins.json</c>'s <c>switches</c>, beside <c>disabled</c> (<see cref="PluginSwitchTable"/>). An unreadable file switches nothing
/// off, and an edit over it is refused as <see cref="PluginState"/>'s is.
/// </summary>
public static class PluginSwitches
{
    public const string Key = "switches";

    public const string Commands = PluginInterfaces.Commands;

    /// <summary>The commands marked <c>tool</c>, as agents are handed them.</summary>
    public const string Tools = "tools";

    public const string Views = PluginInterfaces.Views;

    /// <summary>The interfaces a switch names; any other word is a point the plugin speaks on.</summary>
    public static IReadOnlyList<string> Interfaces { get; } = [Commands, Tools, Views, PluginInterfaces.Storage];

    public static PluginSwitchTable Read(string home)
    {
        var disabled = PluginState.Load(home).Disabled;
        try
        {
            if (PluginFolders.TextIfThere(PluginState.PathIn(home)) is not { } text) return new(disabled, PluginSwitchTable.Empty.Rows);
            using var document = JsonDocument.Parse(text);
            return PluginSwitchTable.From(document.RootElement, disabled);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            // Read as nothing switched off, the plugin as installed; an edit is refused by the state's own check.
            return new(disabled, PluginSwitchTable.Empty.Rows);
        }
    }

    public static bool IsOn(string home, string plugin, string what, PluginPlace? place = null) =>
        Read(home).IsOn(plugin, what, place ?? PluginPlace.Machine);

    /// <summary>The person's word at one place; <c>on</c> at the machine is the default, and leaves no word.</summary>
    /// <exception cref="DriverException">A word naming no interface or point, no place, storage away from the machine, or a file that does not read.</exception>
    public static void Set(string home, string plugin, string what, string place, bool on)
    {
        if (Refusal(what, place) is { } refusal) throw new DriverException(refusal);
        Edit(home, rows =>
        {
            var row = rows.TryGetValue(plugin, out var held) ? held : rows[plugin] = new(StringComparer.Ordinal);
            var places = row.TryGetValue(what, out var kept) ? kept : row[what] = new(StringComparer.Ordinal);
            if (on && place == PluginPlace.MachineKey) places.Remove(place);
            else places[place] = on;
            if (places.Count == 0) row.Remove(what);
            if (row.Count == 0) rows.Remove(plugin);
        });
    }

    /// <summary>Why a word cannot be written: one naming no interface or point, no place, or storage away from the machine; null where it can.</summary>
    public static string? Refusal(string what, string place) =>
        !Interfaces.Contains(what, StringComparer.Ordinal) && !HookPoints.All.Contains(what, StringComparer.Ordinal)
            ? $"`{what}` is no interface or point a plugin is switched by: one of {string.Join(", ", Interfaces)}, or a point it speaks on ({string.Join(", ", HookPoints.All)})."
        : PluginPlace.FromKey(place) is null ? $"`{place}` is no place: `machine`, `workspace:<name>` or `repository:<id>`."
        : what == PluginInterfaces.Storage && place != PluginPlace.MachineKey
            ? "a plugin's storage is switched at the machine alone: its processes share one folder wherever they run."
        : null;

    /// <summary>A renamed plugin's words carried to its new id where that has none; false where the old id had none.</summary>
    public static bool Renamed(string home, string from, string to)
    {
        if (Read(home).Words(from).Count == 0) return false;
        Edit(home, rows =>
        {
            var moved = rows[from];
            rows.Remove(from);
            rows.TryAdd(to, moved);
        });
        return true;
    }

    /// <summary>A removed plugin's words, gone with it.</summary>
    public static void Cleared(string home, string plugin)
    {
        if (Read(home).Words(plugin).Count > 0) Edit(home, rows => rows.Remove(plugin));
    }

    private static void Edit(string home, Action<Dictionary<string, Dictionary<string, Dictionary<string, bool>>>> change)
    {
        _ = PluginState.Editable(home);
        var read = Read(home).Rows;
        var edited = read.ToDictionary(
            plugin => plugin.Key,
            plugin => plugin.Value.ToDictionary(what => what.Key, what => what.Value.ToDictionary(place => place.Key, place => place.Value, StringComparer.Ordinal), StringComparer.Ordinal),
            StringComparer.OrdinalIgnoreCase);
        change(edited);
        var path = PluginState.PathIn(home);
        JsonKeeping.Save(path, File.Exists(path), Written(read), Written(edited.ToDictionary(
            plugin => plugin.Key,
            plugin => (IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>)plugin.Value.ToDictionary(
                what => what.Key, what => (IReadOnlyDictionary<string, bool>)what.Value, StringComparer.Ordinal))));
    }

    private static string Written(IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>> rows) =>
        JsonFile.Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartObject(Key);
            foreach (var (plugin, row) in rows.OrderBy(each => each.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject(plugin);
                foreach (var (what, places) in row.OrderBy(each => each.Key, StringComparer.Ordinal))
                {
                    writer.WriteStartObject(what);
                    foreach (var (place, on) in places.OrderBy(each => each.Key, StringComparer.Ordinal)) writer.WriteBoolean(place, on);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        });
}
