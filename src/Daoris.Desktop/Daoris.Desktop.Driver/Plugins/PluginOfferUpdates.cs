namespace Daoris.Driver;

/// <summary>
/// Daoris's own plugins update with the build that ships them. At a start, before any hook runs, each installed copy that its
/// install made from an offer (its record names the offer) and that is older than the one this build ships is updated as
/// <c>daoris plugin update &lt;id&gt; --yes</c> does (<see cref="PluginInstall.Update"/>): its files replaced whole, its <c>.data/</c>, switch and
/// approvals kept. A plugin the person installed from a folder or a package, a copy whose files were changed since and one with no
/// record are never touched; a copy that cannot be updated now stays as it is, said in the log, and is tried at the next start.
/// </summary>
public static class PluginOfferUpdates
{
    /// <summary>The ids updated, in the catalogue's order; each logged once as <c>plugin.offer-updated</c> with both versions.</summary>
    /// <param name="offers">The offers folder this build ships (<see cref="PluginOffers.FolderFor"/>); none, or one with no offers, changes nothing.</param>
    public static IReadOnlyList<string> Take(string home, string? offers, IEnumerable<string> reservedHarnesses, MachineLog? log = null)
    {
        var reserved = reservedHarnesses.ToList();
        var shipped = PluginOffers.Load(offers, home, reserved);
        if (shipped.Count == 0) return [];

        var taken = new List<string>();
        foreach (var entry in PluginCatalog.Load(home, reserved).Plugins)
        {
            var id = Path.GetFileName(entry.Folder)!;
            if (PluginSource.Read(entry.Folder) is not ({ Offer: not null, Package: null } source, null)) continue;
            if (PluginOffers.For(shipped, source.Offer!) is not { Problem: null } offer) continue;
            var (written, _) = PluginCatalog.ReadAsWritten(id, Path.Combine(entry.Folder, PluginCatalog.ManifestName));
            if (PluginOffers.ShippedOf(entry.Folder, written, offer) is not { State: PluginShipped.Older } judged) continue;

            try
            {
                PluginInstall.Update(home, id, reserved, offers);
                taken.Add(id);
                log?.Info("plugin.offer-updated", ("plugin", id), ("from", judged.Installed), ("to", judged.Shipped));
            }
            catch (DriverException refused)
            {
                log?.Warn("plugin.offer-update-refused", ("plugin", id), ("refusal", refused.Message));
            }
        }

        return taken;
    }
}
