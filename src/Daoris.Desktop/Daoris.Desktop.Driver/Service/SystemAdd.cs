using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// Adding a system reached only through its console as a department: the registration names its kind and its adapter's system,
/// <c>&lt;plugin&gt;/&lt;system&gt;</c>, and never a root, since it has no folder. It holds no doctrine, so it is registered and not adopted.
/// </summary>
public static class SystemAdd
{
    /// <summary>What the add would send for <paramref name="system"/> to <paramref name="service"/>, named <paramref name="name"/> or the system's id.</summary>
    /// <exception cref="RegistryRefusal">The word names no system, or no plugin installed in <paramref name="home"/> and on adapts it.</exception>
    public static ConnectPlan Plan(string home, string system, string? name, string? workspace, string service)
    {
        if (SystemPlace.Of(system?.Trim()) is not { } place)
        {
            throw new RegistryRefusal(RegistryRefusal.NotDeclared, $"`{system}` names no system: it is `<plugin>/<system>`, the plugin's id and its system's id there.", 1);
        }

        if (!PluginCatalog.Load(home).Plugins.Any(each => each.Contributes && each.Manifest.Id == place.Plugin && each.Manifest.Systems.Any(declared => declared.Id == place.System)))
        {
            throw new RegistryRefusal(RegistryRefusal.NotDeclared, $"no plugin installed and on adapts system `{place}`: install `{place.Plugin}`, or switch it on.", 1);
        }

        var repository = string.IsNullOrWhiteSpace(name) ? place.System : name.Trim();
        var body = new JsonObject
        {
            ["repository"] = repository,
            ["adopted"] = false,
            ["kind"] = PlaceKind.System,
            ["system"] = place.ToString(),
        };
        if (!string.IsNullOrEmpty(workspace)) body["workspace"] = workspace;
        return new ConnectPlan(repository, service, body);
    }
}
