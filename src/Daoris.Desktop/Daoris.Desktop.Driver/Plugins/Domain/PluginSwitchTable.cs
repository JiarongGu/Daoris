using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One word the person wrote: an interface or point of a plugin, on or off at a place.</summary>
public sealed record PluginSwitchWord(string What, string Place, bool On);

/// <summary>
/// <c>plugins.json</c> as the switches read it: the plugins disabled whole, and by plugin, by interface or point, by place, the person's
/// word. Read for one place, the narrowest place holding a word decides, else everything is on; <c>storage</c> reads the machine's word
/// alone, and a disabled plugin is off whatever its switches say.
/// </summary>
public sealed class PluginSwitchTable
{
    private readonly IReadOnlyList<string> _disabled;

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>> _rows;

    public PluginSwitchTable(
        IReadOnlyList<string> disabled, IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>> rows) =>
        (_disabled, _rows) = (disabled, rows);

    public static PluginSwitchTable Empty { get; } = new([], new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>>());

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>> Rows => _rows;

    public bool IsOn(string plugin, string what, PluginPlace place)
    {
        if (_disabled.Contains(plugin, StringComparer.OrdinalIgnoreCase)) return false;
        if (!_rows.TryGetValue(plugin, out var row) || !row.TryGetValue(what, out var places)) return true;
        foreach (var key in what == PluginInterfaces.Storage ? [PluginPlace.MachineKey] : place.Chain)
        {
            if (places.TryGetValue(key, out var on)) return on;
        }

        return true;
    }

    /// <summary>Every word written for <paramref name="plugin"/>, by what it switches and then its place.</summary>
    public IReadOnlyList<PluginSwitchWord> Words(string plugin) =>
        _rows.TryGetValue(plugin, out var row)
            ? [.. row.OrderBy(each => each.Key, StringComparer.Ordinal)
                .SelectMany(each => each.Value.OrderBy(place => place.Key, StringComparer.Ordinal).Select(place => new PluginSwitchWord(each.Key, place.Key, place.Value)))]
            : [];

    /// <summary>The <c>switches</c> object read from a state file's root; anything of another shape is no word.</summary>
    public static PluginSwitchTable From(JsonElement root, IReadOnlyList<string> disabled)
    {
        var rows = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, bool>>>(StringComparer.OrdinalIgnoreCase);
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(PluginSwitches.Key, out var switches) && switches.ValueKind == JsonValueKind.Object)
        {
            foreach (var plugin in switches.EnumerateObject().Where(each => each.Value.ValueKind == JsonValueKind.Object))
            {
                var row = new Dictionary<string, IReadOnlyDictionary<string, bool>>(StringComparer.Ordinal);
                foreach (var what in plugin.Value.EnumerateObject().Where(each => each.Value.ValueKind == JsonValueKind.Object))
                {
                    var places = what.Value.EnumerateObject()
                        .Where(each => each.Value.ValueKind is JsonValueKind.True or JsonValueKind.False && PluginPlace.FromKey(each.Name) is not null)
                        .ToDictionary(each => each.Name, each => each.Value.GetBoolean(), StringComparer.Ordinal);
                    if (places.Count > 0) row[what.Name] = places;
                }

                if (row.Count > 0) rows[plugin.Name] = row;
            }
        }

        return new(disabled, rows);
    }
}
