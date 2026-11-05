using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The script a package's program is, read from its <c>package.json</c> as npm's <c>exec</c> picks it (LAUNCHCHAIN1): the program
/// named, where one is; else the one script every name points at; else the program named for the package without its scope. What
/// starts it is node, with the script as its first argument, never the shim npm wrote beside it.
/// </summary>
public static class PackageBin
{
    /// <summary>The script's path inside the package, as its manifest writes it; null where the manifest names none that fits.</summary>
    /// <param name="manifest">The package's <c>package.json</c>, as text.</param>
    /// <param name="package">The package's name, whose unscoped part names a lone <c>bin</c> string.</param>
    /// <param name="bin">The program asked for; null lets npm's rule pick.</param>
    public static string? Of(string manifest, string package, string? bin)
    {
        Dictionary<string, string> named;
        try
        {
            using var document = JsonDocument.Parse(manifest);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("bin", out var field))
            {
                return null;
            }

            named = field.ValueKind switch
            {
                JsonValueKind.String => new(StringComparer.Ordinal) { [NpxCommand.Unscoped(package)] = field.GetString()! },
                JsonValueKind.Object => field.EnumerateObject()
                    .Where(entry => entry.Value.ValueKind == JsonValueKind.String && entry.Value.GetString() is { Length: > 0 })
                    .ToDictionary(entry => entry.Name, entry => entry.Value.GetString()!, StringComparer.Ordinal),
                _ => [],
            };
        }
        catch (JsonException)
        {
            return null;
        }

        if (bin is not null) return Inside(named.GetValueOrDefault(bin));
        var scripts = named.Values.Distinct(StringComparer.Ordinal).ToList();
        if (scripts.Count == 1) return Inside(scripts[0]);
        return Inside(named.GetValueOrDefault(NpxCommand.Unscoped(package)));
    }

    /// <summary>A script's path that stays inside its package, with forward slashes; null for one that climbs out or is rooted.</summary>
    private static string? Inside(string? script)
    {
        if (script is null) return null;
        var parts = script.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Where(part => part != ".").ToList();
        if (parts.Count == 0 || parts.Contains("..") || script.StartsWith('/') || script.Contains(':')) return null;
        return string.Join('/', parts);
    }
}
