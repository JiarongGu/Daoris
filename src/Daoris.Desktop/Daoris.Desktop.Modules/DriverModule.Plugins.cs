using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// This machine's plugins, the page's `bridge/plugins.ts` (MOD5; D64): the catalogue and the install's
/// offers, enable, disable and remove, an update, an offer installed (PLUG9), and the kit's New and Try
/// (PLUG8).
/// </summary>
public sealed partial class DriverModule
{
    /// <summary>
    /// The folder holding the install's offers — Daoris's own example plugins, none installed until a press
    /// (PLUG9 d, D103). Null finds them beside the running application, else beside the home; a test names
    /// its own, since every test's home shares one parent.
    /// </summary>
    public string? Offers { get; init; }

    private string OffersFolder => Offers ?? PluginOffers.FolderFor(_loop.Home, AppContext.BaseDirectory);

    // This machine's plugins (D64): the catalogue as the driver reads it, each with what it
    // declares, what it speaks on, whether it is running, and why it contributes nothing when
    // it does not. Machine paths ride this bridge like every path here.
    [DriverRoute("PLUGINS")]
    private async Task<object?> PluginsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var catalog = PluginCatalog.Load(_loop.Home, AdapterSet.Built().Names);
        var running = new HashSet<string>(_loop.RunningPlugins, StringComparer.Ordinal);
        return new
        {
            Folder = Path.Combine(_loop.Home, PluginCatalog.Folder),
            // Where a new plugin may speak (PLUG8): the kit's points, which are the driver's.
            Kit = new { Points = PluginKit.Points.Select(point => new { point.Name, point.Kind }).ToArray() },
            Plugins = catalog.Plugins.Select(plugin => new
            {
                plugin.Manifest.Id,
                plugin.Manifest.Name,
                plugin.Manifest.Version,
                plugin.Manifest.Description,
                plugin.Enabled,
                plugin.Problem,
                Harnesses = plugin.Manifest.Harnesses.Select(h => h.Name).ToArray(),
                Points = plugin.Manifest.Hooks?.Points ?? [],
                Running = running.Contains(plugin.Manifest.Id),
                plugin.Folder,
                plugin.Data,
                // Where it came from (PLUG9 c): what an Update re-reads, or why there is none.
                Source = SourceOf(plugin.Folder),
            }).ToArray(),
            // Daoris's own plugins the install carries (PLUG9 d), installed only by a press.
            OffersFolder,
            Offers = PluginOffers.Load(OffersFolder, _loop.Home, AdapterSet.Built().Names).Select(offer => new
            {
                offer.Id,
                offer.Manifest.Name,
                offer.Manifest.Version,
                offer.Manifest.Description,
                offer.Problem,
                Harnesses = offer.Manifest.Harnesses.Select(h => h.Name).ToArray(),
                Points = offer.Manifest.Hooks?.Points ?? [],
                Servers = offer.Manifest.Servers.Select(s => s.Name).ToArray(),
                offer.Needs,
                offer.Installed,
            }).ToArray(),
        };
    }

    // The screen's half of `daoris plugin update <id>` (PLUG9 c, D50): without `apply`, what an update
    // would change, or why it cannot, for the row to show before the press; with it, the update.
    [DriverRoute("PLUGIN_UPDATE")]
    private async Task<object?> PluginUpdateAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        if (request.Payload is { } payload && payload.TryGetProperty("apply", out var apply) && apply.ValueKind == JsonValueKind.True)
        {
            var updated = await UpdatePluginAsync(id).ConfigureAwait(false);
            return new { updated.Id, Applied = true, Refusal = (string?)null, Source = updated.Source.Said, updated.From, Changes = ChangesOf(updated.Changes) };
        }

        var (plan, refusal) = PluginInstall.PlanUpdate(_loop.Home, id, AdapterSet.Built().Names, OffersFolder);
        return new
        {
            Id = plan?.Id ?? id,
            Applied = false,
            Refusal = refusal,
            Source = plan?.Source.Said,
            plan?.From,
            Changes = ChangesOf(plan?.Changes ?? []),
        };
    }

    // The screen's Install beside one of the install's offers (PLUG9 d): `daoris plugin add --offer <id>`'s
    // copy, the offer recorded. 🔴 Nothing starts at the press; the loop starts it at its next look.
    [DriverRoute("PLUGIN_INSTALL")]
    private async Task<object?> PluginInstallAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var offer = PayloadHelper.GetRequiredValue<string>(request.Payload, "offer");
        var added = PluginInstall.AddOffer(_loop.Home, OffersFolder, offer, AdapterSet.Built().Names);
        _loop.Nudge();
        return new { added.Id, added.Name, added.Version };
    }

    // The screen's half of `daoris plugin enable|disable|remove` (D50): a row in
    // `plugins.json`, or the install folder gone with the data folder named and kept.
    [DriverRoute("PLUGIN_ACTION")]
    private async Task<object?> PluginActionAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var action = PayloadHelper.GetRequiredValue<string>(request.Payload, "action");
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id");
        var entry = InstalledPlugin(id);

        switch (action)
        {
            case "enable" or "disable":
                SwitchPlugin(entry, action == "enable");
                break;
            case "remove":
            {
                // The install folder goes; what the plugin kept is NAMED and stays — it is
                // the person's to throw away, the same judgement Forget makes for an account.
                // 🔴 Its hook process first, then the folder moved aside WHOLE (REV3): on
                // Windows a running hook holds its folder, and a recursive delete took every
                // file it could before failing, stranding a plugin with no manifest.
                await _loop.StopPluginAsync(entry.Manifest.Id).ConfigureAwait(false);
                var aside = Path.Combine(
                    Path.GetDirectoryName(entry.Folder)!, $".removing-{entry.Manifest.Id}-{Guid.NewGuid():N}");
                try
                {
                    Directory.Move(entry.Folder, aside);
                }
                catch (Exception held) when (held is IOException or UnauthorizedAccessException)
                {
                    throw Refusals.Because(
                        Refusals.PluginBusy,
                        $"`{entry.Manifest.Id}` was not removed: something on this machine still has a file in "
                        + "its folder open. Close it, or wait for the plugin's own process to end, and remove it "
                        + "again. Nothing was taken.",
                        ("id", entry.Manifest.Id));
                }

                // Aside is a dot-folder, which the catalogue never reads, so a delete that
                // cannot finish leaves nothing that looks like a plugin.
                try { Directory.Delete(aside, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                PluginState.Enable(_loop.Home, entry.Manifest.Id);
                break;
            }
            default:
                throw Refusals.Because(
                    Refusals.PluginActionUnknown,
                    $"unknown plugin action '{action}' — one of: enable, disable, remove",
                    ("action", action));
        }

        // The loop reconciles its hook processes against the catalogue each tick; asked to
        // look now, so a plugin switched off stops before the person has finished reading.
        _loop.Nudge();
        return new
        {
            Id = entry.Manifest.Id,
            Action = action,
            Data = Directory.Exists(entry.Data) ? entry.Data : null,
        };
    }

    // The plugin kit's screen half (PLUG8, D101): `daoris-driver plugins new` from a form. It writes
    // a plugin's folder into the one the person named, a plugins repository's typically, and
    // installs nothing: making a plugin is work, reviewed before `daoris plugin add`.
    [DriverRoute("PLUGIN_NEW")]
    private async Task<object?> PluginNewAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var points = request.Payload is { } payload && payload.TryGetProperty("points", out var named)
            && named.ValueKind == JsonValueKind.Array
                ? named.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
                : [];
        var plan = PluginKit.Plan(
            PayloadHelper.GetRequiredValue<string>(request.Payload, "id"), points,
            PayloadHelper.GetRequiredValue<string>(request.Payload, "folder"));
        var files = PluginKit.Write(plan);
        return new { plan.Id, plan.Folder, plan.Points, Files = files };
    }

    // `daoris-driver plugins try` from a button: an installed plugin by its id, or a folder by
    // its path, started as the driver would and every answer read by the driver's own reader.
    // It may take as long as the driver waits at the points tried — two minutes for a landing.
    [DriverRoute("PLUGIN_TRY")]
    private async Task<object?> PluginTryAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var options = new TrialOptions(Point: Optional(request, "point"));
        var trial = Optional(request, "id") is { } id
            ? await PluginKit.TryInstalledAsync(_loop.Home, id, options, cancellationToken)
            : Optional(request, "folder") is { } folder
                ? await PluginKit.TryFolderAsync(_loop.Home, folder, options, cancellationToken)
                : throw new DriverException("a try needs an installed plugin's id or a folder.");
        return new
        {
            trial.Plugin,
            trial.Folder,
            trial.Command,
            trial.Passed,
            trial.Summary,
            Steps = trial.Steps.Select(step => new { step.Name, step.Ok, step.Sentence }).ToArray(),
            trial.Said,
        };
    }

    /// <summary>Where an installed plugin came from, for its row: a folder, an offer, none recorded, or a record that does not read.</summary>
    private static object SourceOf(string installFolder)
    {
        var (source, problem) = PluginSource.Read(installFolder);
        return new
        {
            Kind = problem is not null ? "unread" : source is null ? "none" : source.Offer is not null ? "offer" : "folder",
            Folder = source?.Folder,
            Offer = source?.Offer,
            Problem = problem,
        };
    }

    private static object[] ChangesOf(IReadOnlyList<PluginChange> changes) =>
        [.. changes.Select(change => (object)new { change.What, change.Was, change.Now })];

    /// <summary>
    /// An update, as <c>PLUGIN_UPDATE</c> and Ask Daoris's update door make it (PLUG9 c): judged, the plugin's
    /// hook stopped first (on Windows a running process holds its folder), the install folder swapped, and
    /// the loop asked to look, which starts the new one as it does any plugin.
    /// </summary>
    private async Task<PluginUpdatePlan> UpdatePluginAsync(string id)
    {
        var reserved = AdapterSet.Built().Names;
        var (plan, refusal) = PluginInstall.PlanUpdate(_loop.Home, id, reserved, OffersFolder);
        if (plan is null) throw new DriverException($"{refusal} Nothing was replaced.");
        await _loop.StopPluginAsync(plan.Id).ConfigureAwait(false);
        var updated = PluginInstall.Update(_loop.Home, plan.Id, reserved, OffersFolder);
        _loop.Nudge();
        return updated;
    }

    /// <summary>An installed plugin by its id, or the Plugins screen's refusal (<c>PLUGIN_ACTION</c>, PLUG9's door).</summary>
    private PluginEntry InstalledPlugin(string id) =>
        PluginCatalog.Load(_loop.Home).Plugins
            .FirstOrDefault(p => string.Equals(p.Manifest.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw Refusals.Because(Refusals.PluginUnknown, $"no plugin `{id}` on this machine.", ("id", id));

    /// <summary>
    /// A plugin switched on or off: a row in <c>plugins.json</c>. <c>PLUGIN_ACTION</c> and Ask Daoris's plugin
    /// door (PLUG9) both call this, so the screen and the card cannot drift.
    /// </summary>
    private void SwitchPlugin(PluginEntry entry, bool on)
    {
        if (on) PluginState.Enable(_loop.Home, entry.Manifest.Id);
        else PluginState.Disable(_loop.Home, entry.Manifest.Id);
    }
}
