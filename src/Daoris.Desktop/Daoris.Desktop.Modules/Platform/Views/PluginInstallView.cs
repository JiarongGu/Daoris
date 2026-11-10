using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris plugin add --offer &lt;id&gt;</c> as the card shows it (<c>PLUGIN_INSTALL</c>): one of the install's own plugins copied into the home,
/// judged as <see cref="PluginInstall.AddOffer"/> judges it (an offer the install carries, sound, and not installed here). The card says
/// what it will run and what it needs.
/// </summary>
public sealed class PluginInstallView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "PLUGIN_INSTALL";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var wanted = PressPayload.Text(payload, "offer");
        var machine = await world.MachineAsync(cancellationToken).ConfigureAwait(false);
        if (machine.Facts.Home is not { } home) return PressViewed.Refused("this machine's plugins were not read, so nothing is asked.");

        var offers = PluginOffers.Load(machine.Facts.OffersFolder, home, machine.Facts.Reserved);
        if (offers.FirstOrDefault(each => string.Equals(each.Id, wanted, StringComparison.OrdinalIgnoreCase)) is not { } offer)
        {
            return PressViewed.Refused($"this install offers no plugin `{wanted}` — "
                + (offers.Count > 0 ? $"it offers {PluginNamed.Names(offers.Select(each => each.Id))}." : "it offers none."));
        }

        if (offer.Problem is { } problem) return PressViewed.Refused($"Daoris's own `{offer.Id}` cannot be installed as it stands: {problem}");
        if (offer.Installed || PluginNamed.Installed(machine.Facts.Plugins, offer.Id) is not null)
        {
            return PressViewed.Refused($"plugin `{offer.Id}` is already installed on this machine: an install never replaces one. "
                + $"`daoris plugin update {offer.Id}` takes the install's newer copy, keeping what it kept.");
        }

        return PressViewed.Shown(new PressView("plugin-install", PluginNamed.Facts(("id", offer.Id)), $"daoris plugin add --offer {offer.Id}")
        {
            Plugin = PressPlugin.Of(offer.Manifest) with { Needs = offer.Needs },
        });
    }
}
