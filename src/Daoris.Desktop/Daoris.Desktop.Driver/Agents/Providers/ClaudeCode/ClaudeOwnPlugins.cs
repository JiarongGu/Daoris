using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The plugins the person's own run of Claude Code loads: each its user settings' <c>enabledPlugins</c> turns on, at the folder its
/// user-scope install names in <c>plugins/installed_plugins.json</c> (version 2's shape: an id's installs, each with its scope and
/// <c>installPath</c>). Handed in place by the door's session-only way, as their own run reads them, and never copied or removed.
/// </summary>
/// <remarks>
/// A plugin installed for one project alone is that project's: its own settings enable it where the person's run works in it, so a
/// session is handed only the user-scope installs. A folder that is gone is passed by, as the harness would refuse it.
/// </remarks>
internal static class ClaudeOwnPlugins
{
    /// <summary>The install folders of the plugins <paramref name="own"/>'s user settings enable, in the settings' order.</summary>
    public static IReadOnlyList<string> Enabled(string own)
    {
        try
        {
            var enabled = Read(Path.Combine(own, "settings.json"), root =>
                root.TryGetProperty("enabledPlugins", out var map) && map.ValueKind == JsonValueKind.Object
                    ? [.. map.EnumerateObject().Where(entry => entry.Value.ValueKind == JsonValueKind.True).Select(entry => entry.Name)]
                    : new List<string>());
            if (enabled.Count == 0) return [];

            return Read(Path.Combine(own, "plugins", "installed_plugins.json"), root =>
                root.TryGetProperty("plugins", out var installs) && installs.ValueKind == JsonValueKind.Object
                    ? [.. enabled.SelectMany(id => UserInstall(installs, id)).Where(Directory.Exists)]
                    : new List<string>());
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException or JsonException)
        {
            // A file their own run cannot read loads no plugin there either; the session starts as it would have.
            return [];
        }
    }

    private static IEnumerable<string> UserInstall(JsonElement installs, string id) =>
        installs.TryGetProperty(id, out var each) && each.ValueKind == JsonValueKind.Array
            ? each.EnumerateArray()
                .Where(install => install.ValueKind == JsonValueKind.Object
                                  && install.TryGetProperty("scope", out var scope) && scope.ValueKind == JsonValueKind.String && scope.GetString() == "user"
                                  && install.TryGetProperty("installPath", out var path) && path.ValueKind == JsonValueKind.String)
                .Select(install => install.GetProperty("installPath").GetString()!)
                .Take(1)
            : [];

    private static List<string> Read(string file, Func<JsonElement, List<string>> read)
    {
        if (!File.Exists(file)) return [];
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        return document.RootElement.ValueKind == JsonValueKind.Object ? read(document.RootElement) : [];
    }
}
