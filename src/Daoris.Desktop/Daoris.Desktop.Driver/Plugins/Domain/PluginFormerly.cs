using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A manifest's old ids: each a plugin id other than its own, once. A problem refuses the plugin, since an
/// id read wrongly would move another plugin's records.
/// </summary>
public static class PluginFormerly
{
    public static (IReadOnlyList<string> Formerly, string? Problem) Read(string id, JsonElement root)
    {
        if (!root.TryGetProperty("formerly", out var declared)) return ([], null);
        if (declared.ValueKind != JsonValueKind.Array) return ([], "`formerly` must be an array of the plugin ids this plugin was.");
        var read = new List<string>();
        foreach (var row in declared.EnumerateArray())
        {
            var named = row.ValueKind == JsonValueKind.String ? row.GetString()! : row.GetRawText();
            if (row.ValueKind != JsonValueKind.String || !PluginIds.IsId(named)) return ([], $"`formerly` names `{named}`, which is not a plugin id.");
            if (named == id) return ([], $"`formerly` names the plugin's own id `{id}`.");
            if (read.Contains(named, StringComparer.Ordinal)) return ([], $"`formerly` names `{named}` twice.");
            read.Add(named);
        }

        return (read, null);
    }

    /// <summary>
    /// Each former id the installed, sound plugins declare, to the id that plugin has now: what a record still naming the old id is read as.
    /// The first plugin in catalogue order to declare an id keeps it.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Of(string home) => Of(PluginCatalog.Load(home));

    /// <inheritdoc cref="Of(string)"/>
    public static IReadOnlyDictionary<string, string> Of(PluginCatalog catalog)
    {
        var now = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in catalog.Plugins.Where(each => each.Problem is null))
        {
            foreach (var former in plugin.Manifest.Formerly) now.TryAdd(former, plugin.Manifest.Id);
        }

        return now;
    }
}
