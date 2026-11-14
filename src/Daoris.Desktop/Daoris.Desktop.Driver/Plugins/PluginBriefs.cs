using System.Text.Json;

namespace Daoris.Driver;

public static class PluginBriefs
{
    public const int Limit = 1_000;

    public static string? Read(JsonElement manifest)
    {
        if (!manifest.TryGetProperty("brief", out var value) || value.ValueKind != JsonValueKind.String) return null;
        var line = value.GetString()!.Trim();
        return line.Length is > 0 and <= Limit && !line.Any(char.IsControl) ? line : null;
    }

    public static IReadOnlyList<string> At(string home, PluginPlace place, IReadOnlyList<ServerEntry> handed)
    {
        var commands = handed.FirstOrDefault(server => server.Name == PluginToolsServer.Name);
        var at = commands is null ? null : PluginToolsHand.Read(commands.Arguments.Skip(2).ToList());
        var tools = at is null ? [] : PluginToolList.At(home, place)
            .Where(tool => (at.Plugin is null || at.Plugin == tool.Plugin.Manifest.Id)
                && (commands!.Tools is null || commands.Tools.Contains(tool.Name)))
            .Select(tool => tool.Plugin.Manifest.Id).ToHashSet(StringComparer.Ordinal);
        return [.. PluginCatalog.Load(home).Contributing
            .Where(plugin => plugin.Manifest.Brief is not null
                && PluginSwitches.IsOn(home, plugin.Manifest.Id, PluginSwitches.Tools, place)
                && (tools.Contains(plugin.Manifest.Id) || plugin.Manifest.Servers.Any(declared =>
                    handed.Any(server => server.Name == declared.Name && server.Tools is not { Count: 0 }))))
            .Select(plugin => plugin.Manifest.Brief!)];
    }
}
