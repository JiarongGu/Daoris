using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris plugin update &lt;id&gt; --yes</c> as the card shows it (<c>PLUGIN_UPDATE</c> with <c>apply</c>): an installed plugin's folder replaced
/// from the source it recorded, judged by <see cref="PluginInstall.PlanUpdate"/> in the words the verb refuses with, and what changes.
/// The source's folder is a path on this machine, which the card never names: an offer is named by its id.
/// </summary>
public sealed class PluginUpdateView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "PLUGIN_UPDATE";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var id = PressPayload.Text(payload, "id");
        var shipped = payload is { } body && PressPayload.Flag(body, "shipped");
        var machine = await world.MachineAsync(cancellationToken).ConfigureAwait(false);
        if (machine.Facts.Home is not { } home) return PressViewed.Refused("this machine's plugins were not read, so nothing is asked.");

        var (plan, refusal) = PluginInstall.PlanUpdate(home, id, machine.Facts.Reserved, machine.Facts.OffersFolder, shipped);
        if (plan is null) return PressViewed.Refused(refusal ?? $"plugin `{id}` cannot be updated.");

        var view = plan.Source.Offer is { } offer
            ? new PressView("plugin-update-offer", PluginNamed.Facts(("id", plan.Id), ("offer", offer)), $"daoris plugin update {plan.Id} --yes")
            : new PressView("plugin-update", PluginNamed.Facts(("id", plan.Id)), $"daoris plugin update {plan.Id} --yes");
        return PressViewed.Shown(view with
        {
            Plugin = PressPlugin.Read(plan.Id, plan.From) with { Changes = plan.Changes },
            Also = plan.Changes.Count == 0 ? [new PressSaid("plugin-update-same")] : null,
        });
    }
}
