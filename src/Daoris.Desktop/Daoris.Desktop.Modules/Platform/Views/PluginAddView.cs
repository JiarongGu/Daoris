using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris plugin add &lt;folder&gt;</c> as the card shows it (<c>PLUGIN_ADD</c>): a plugin's folder copied into the home under its id, read by
/// the catalogue's own reader as the route reads it, never from in or around the home, and never over one installed (that is the replace,
/// <c>PLUGIN_REPLACE</c>, a press of its own). The card says what it will run, as its manifest writes it.
/// </summary>
public sealed class PluginAddView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "PLUGIN_ADD";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var folder = PressPayload.Text(payload, "folder");
        if (folder.Length == 0 || !Path.IsPathRooted(folder)) return PressViewed.Refused("an add names the plugin's folder as a whole path on this machine.");
        var machine = await world.MachineAsync(cancellationToken).ConfigureAwait(false);
        if (machine.Facts.Home is not { } home) return PressViewed.Refused("this machine's plugins were not read, so nothing is asked.");

        var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if (PluginInstall.Placement(home, source) is { } misplaced) return PressViewed.Refused(misplaced);
        if (!Directory.Exists(source)) return PressViewed.Refused($"there is no folder `{source}` on this machine.");
        var (manifest, refusal) = PluginInstall.Read(source, machine.Facts.Reserved);
        if (refusal is not null) return PressViewed.Refused(refusal);
        if (PluginNamed.Installed(machine.Facts.Plugins, manifest!.Id) is not null)
        {
            return PressViewed.Refused($"plugin `{manifest.Id}` is already installed on this machine: an add never replaces one. "
                + $"`daoris plugin update {manifest.Id}` takes a newer copy from where it came from, keeping what it kept.");
        }

        return PressViewed.Shown(new PressView("plugin-add", PluginNamed.Facts(("id", manifest.Id)), $"daoris plugin add {Spelled(source)}")
        {
            Plugin = PressPlugin.Read(manifest.Id, source),
        });
    }

    /// <summary>A path as a terminal takes it: quoted where it holds a space.</summary>
    private static string Spelled(string word) => word.Contains(' ') ? $"\"{word}\"" : word;
}
