using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// What a plugin's press puts on the card of what it runs, each command as its manifest writes it (<c>${plugin}</c> included): its
/// hook process and the points it speaks on, the harnesses it declares, the servers it hands, and for an update what changes or for an
/// offer what it needs.
/// </summary>
/// <param name="Command">Its hook process; null where it speaks on no point.</param>
public sealed record PressPlugin(
    string Id, string Name, string Version, IReadOnlyList<string>? Command, IReadOnlyList<string> Points,
    IReadOnlyList<PressPluginPart> Harnesses, IReadOnlyList<PressPluginPart> Servers)
{
    /// <summary>What an offer says it needs on the machine, in its README's words.</summary>
    public IReadOnlyList<string> Needs { get; init; } = [];

    /// <summary>What an update changes, each side as the manifests write it.</summary>
    public IReadOnlyList<PluginChange> Changes { get; init; } = [];

    /// <summary>The manifest as written in <paramref name="folder"/>: the catalogue's entry expands the placeholders, where the card shows what the file says.</summary>
    public static PressPlugin Read(string id, string folder)
    {
        var (manifest, _) = PluginCatalog.ReadAsWritten(id, Path.Combine(folder, PluginCatalog.ManifestName));
        return Of(manifest);
    }

    public static PressPlugin Of(PluginManifest manifest) => new(
        manifest.Id, manifest.Name, manifest.Version, manifest.Hooks?.Command, manifest.Hooks?.Points ?? [],
        [.. manifest.Harnesses.Select(harness => new PressPluginPart(harness.Name, harness.Command))],
        [.. manifest.Servers.Select(server => new PressPluginPart(server.Name, server.Command))]);
}

/// <summary>A harness a plugin declares, or a server it hands: its name, and the command that runs it.</summary>
public sealed record PressPluginPart(string Name, IReadOnlyList<string> Command);

/// <summary>
/// What an approval of a plugin's server for Ask Daoris names, or its withdrawal: the plugin, its name, the server, each tool it reads with
/// and each it writes outside Daoris with, as declared, and the writes accepted (for a withdrawal, those the approval had accepted).
/// </summary>
public sealed record PressGrant(
    string Plugin, string Name, string Server, IReadOnlyList<string> Reads, IReadOnlyList<string> Writes, IReadOnlyList<string> Accepted)
{
    public bool Withdraw { get; init; }
}

/// <summary>The plugins a refusal names, and the one a press names found among those installed.</summary>
internal static class PluginNamed
{
    public static PluginEntry? Installed(PluginCatalog catalog, string id) =>
        catalog.Plugins.FirstOrDefault(entry => string.Equals(entry.Manifest.Id, id, StringComparison.OrdinalIgnoreCase));

    public static string NotInstalled(PluginCatalog catalog, string id)
    {
        var installed = catalog.Plugins.Select(entry => entry.Manifest.Id).ToList();
        return $"no plugin `{id}` on this machine — " + (installed.Count > 0 ? $"installed here: {Names(installed)}." : "none is installed here.");
    }

    public static string Names(IEnumerable<string> names) => string.Join(", ", names.Select(name => $"`{name}`"));

    public static Dictionary<string, string> Facts(params (string Name, string Value)[] facts) =>
        facts.ToDictionary(fact => fact.Name, fact => fact.Value, StringComparer.Ordinal);
}
