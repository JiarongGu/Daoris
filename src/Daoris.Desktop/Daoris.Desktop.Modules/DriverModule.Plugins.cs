using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// This machine's plugins, the page's `bridge/plugins.ts` (MOD5; D64): the catalogue and the install's
/// offers, enable, disable and remove, an update, an offer installed (PLUG9), and the kit's New and Try
/// (PLUG8). Since PLUGUI1e (D119 §4.1), what the Plugins view reads besides: a plugin's page and its activity,
/// a folder read and installed from, and a folder opened.
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
    // The Plugins view's list (PLUGUI1e, D119 §4.1) adds what each hands sessions and speaks on, the points its running
    // process listens on, its health from the loop's own record, and whether an update waits. It stays light: the
    // page asks PLUGIN for the rest.
    [DriverRoute("PLUGINS")]
    private async Task<object?> PluginsAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var reserved = AdapterSet.Built().Names;
        var catalog = PluginCatalog.Load(_loop.Home, reserved);
        var running = new HashSet<string>(_loop.RunningPlugins, StringComparer.Ordinal);
        return new
        {
            Folder = Path.Combine(_loop.Home, PluginCatalog.Folder),
            // Where a new plugin may speak (PLUG8): the kit's points, which are the driver's.
            Kit = new { Points = PluginKit.Points.Select(point => new { point.Name, point.Kind }).ToArray() },
            Plugins = catalog.Plugins.Select(plugin =>
            {
                var health = _loop.Health.Of(plugin);
                return new
                {
                    plugin.Manifest.Id,
                    plugin.Manifest.Name,
                    plugin.Manifest.Version,
                    plugin.Manifest.Description,
                    plugin.Enabled,
                    plugin.Problem,
                    Harnesses = plugin.Manifest.Harnesses.Select(h => h.Name).ToArray(),
                    Points = plugin.Manifest.Hooks?.Points ?? [],
                    // What the catalogue takes, as the rows above: a refused plugin hands nothing and speaks nowhere.
                    Servers = plugin.Manifest.Servers.Select(s => s.Name).ToArray(),
                    Hook = HookOf(plugin),
                    Listening = health.Listening,
                    // The loop's own record (D119 §2): the state, since when, and its failure while failing.
                    Health = new { health.State, health.Since, health.Failure },
                    Running = running.Contains(plugin.Manifest.Id),
                    plugin.Folder,
                    plugin.Data,
                    // Where it came from (PLUG9 c): what an Update re-reads, or why there is none.
                    Source = SourceOf(plugin.Folder),
                    Update = UpdateOf(plugin, reserved),
                };
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

    // A plugin's page (PLUGUI1e, D119 §3.2 and §4.1): the reader `daoris-driver plugins show` prints, with its health
    // from the loop's own record, since this process runs the loop. A plugin no longer here is PLUGIN_UNKNOWN, which
    // the page reads as gone by the code (D48 §6).
    [DriverRoute("PLUGIN")]
    private async Task<object?> PluginAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var entry = InstalledPlugin(PayloadHelper.GetRequiredValue<string>(request.Payload, "id"));
        // Off the caller's thread: the data folder is counted for up to two seconds (PluginPage.CountTime).
        return await Task.Run(
            () => PluginPage.Read(_loop.Home, entry.Manifest.Id, _loop.Health, OffersFolder), cancellationToken).ConfigureAwait(false);
    }

    // What a plugin did over a period (PLUGUI1e, D119 §4.1): the reader `daoris-driver plugins activity` prints, over
    // the machine log under the home. `since` is the span the page chose (`1d`, `7d`, `30d`), the log's own spans;
    // absent, the page's opening period. Its words this run are the console's ring, read apart (`TAIL_SESSION`).
    [DriverRoute("PLUGIN_ACTIVITY")]
    private async Task<object?> PluginActivityAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var period = PluginActivity.DefaultPeriod;
        if (Optional(request, "since") is { } since)
        {
            period = MachineLogReader.ParseSince(since) ?? throw Refusals.Because(
                Refusals.LogFilterUnknown,
                $"`{since}` is not a since the log reads; it takes a span such as 30m, 2h or 3d.",
                ("filter", "since"), ("value", since));
        }

        var entry = InstalledPlugin(PayloadHelper.GetRequiredValue<string>(request.Payload, "id"));
        // Off the caller's thread: a month of the log's files may be read.
        return await Task.Run(
            () => PluginActivity.Read(_loop.Home, entry.Manifest.Id, DateTimeOffset.UtcNow - period), cancellationToken).ConfigureAwait(false);
    }

    // Install from a folder's first step (PLUGUI1e, D119 §3.4): what the folder's plugin would run, shown before the
    // press, read as Ask Daoris's judge reads a folder. A refusal is an ANSWER here, since the drawer shows it in place
    // of the plugin; nothing is copied.
    [DriverRoute("PLUGIN_READ")]
    private object? PluginRead(IpcRequest request)
    {
        var folder = PayloadHelper.GetRequiredValue<string>(request.Payload, "folder");
        var (manifest, refusal) = ReadPluginFolder(folder);
        return new
        {
            Folder = folder,
            Refusal = refusal,
            Plugin = manifest is null ? null : new
            {
                manifest.Id,
                manifest.Name,
                manifest.Version,
                manifest.Description,
                // As its manifest writes it: `${plugin}` and `${data}` left as written.
                Hook = manifest.Hooks is { } hooks ? new { hooks.Command, hooks.Points } : null,
                Agents = manifest.Harnesses.Select(harness => new { harness.Name, harness.Command }).ToArray(),
                // An environment variable by name only: a value may be a key (D119 §4.1).
                Servers = manifest.Servers.Select(server => new
                {
                    server.Name,
                    server.Command,
                    Environment = server.Environment.Keys.Order(StringComparer.Ordinal).ToArray(),
                }).ToArray(),
            },
        };
    }

    // Install from a folder (PLUGUI1e, D119 §3.4): `daoris plugin add <folder>`'s copy, the driver's twin, judged as
    // PLUGIN_READ judged it. It adds and never replaces; the terminal's add is the one that replaces (§4.4).
    // 🔴 Nothing starts at the press; the loop starts what it runs at its next look.
    [DriverRoute("PLUGIN_ADD")]
    private object? PluginAdd(IpcRequest request)
    {
        var folder = PayloadHelper.GetRequiredValue<string>(request.Payload, "folder");
        var (_, refusal) = ReadPluginFolder(folder);
        if (refusal is not null) throw new DriverException($"{refusal} Nothing was copied.");
        var added = PluginInstall.Add(_loop.Home, Path.GetFullPath(folder), AdapterSet.Built().Names);
        _loop.Nudge();
        return new { added.Id, added.Name, added.Version };
    }

    // Open folder and Open the plugins folder (PLUGUI1e, D119 §4.1): the module names the folder, never the page, and
    // opens it through the window kit's launcher, as the log's module opens the log's (LOG1c). `which` is a plugin's
    // install folder, its data folder, or the plugins folder.
    [DriverRoute("PLUGIN_OPEN_FOLDER")]
    private object? PluginOpenFolder(IpcRequest request)
    {
        var which = PayloadHelper.GetRequiredValue<string>(request.Payload, "which");
        string folder;
        switch (which)
        {
            case "plugins":
                folder = Path.Combine(_loop.Home, PluginCatalog.Folder);
                break;
            case "install" or "data":
            {
                var entry = InstalledPlugin(
                    Optional(request, "id") ?? throw new DriverException($"opening a plugin's {which} folder needs its id."));
                folder = which == "install" ? entry.Folder : entry.Data;
                if (which == "data" && !Directory.Exists(folder))
                {
                    throw Refusals.Because(
                        Refusals.PluginNothingKept,
                        $"`{entry.Manifest.Id}` keeps nothing yet: it has no data folder to open.",
                        ("id", entry.Manifest.Id));
                }

                break;
            }
            default:
                throw new DriverException($"`{which}` is not a folder a plugin's page opens — one of: install, data, plugins.");
        }

        if (_openFolder is null) return new { Opened = false, Folder = folder, Which = which };
        try
        {
            // The plugins folder is made first if nothing was installed yet, as the log's folder is.
            if (which == "plugins") Directory.CreateDirectory(folder);
            _openFolder(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw Refusals.Because(
                Refusals.PluginFolderNotOpened,
                $"the folder would not open: {error.Message}",
                ("folder", folder), ("problem", error.Message));
        }

        return new { Opened = true, Folder = folder, Which = which };
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
        PluginTrial trial;
        if (Optional(request, "id") is { } id)
        {
            var took = System.Diagnostics.Stopwatch.StartNew();
            trial = await PluginKit.TryInstalledAsync(_loop.Home, id, options, cancellationToken);
            // An installed plugin's trial is part of its history, from the screen's door (PLUGUI1e, D119 §4.2), as the
            // terminal's is from its own. A folder's is shown and never kept: its id may name an installed plugin it is not.
            new PluginLog(_loop.Log).Tried(trial, took.ElapsedMilliseconds, PluginEvents.Screen);
        }
        else
        {
            trial = Optional(request, "folder") is { } folder
                ? await PluginKit.TryFolderAsync(_loop.Home, folder, options, cancellationToken)
                : throw new DriverException("a try needs an installed plugin's id or a folder.");
        }

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
    /// A taken plugin's hook for its row (PLUGUI1e): its command as its manifest writes it, the page's way, so `${plugin}`
    /// reads as written and never as the install folder; and its points. Null where it speaks nowhere, or is refused.
    /// </summary>
    private static object? HookOf(PluginEntry plugin)
    {
        if (plugin.Manifest.Hooks is not { } hooks) return null;
        var (written, _) = PluginCatalog.ReadAsWritten(plugin.Manifest.Id, Path.Combine(plugin.Folder, PluginCatalog.ManifestName));
        return new { Command = written.Hooks?.Command ?? hooks.Command, hooks.Points };
    }

    /// <summary>
    /// Whether an update waits for its row (PLUGUI1e, D119 §2): <c>waits</c> when its source declares something different
    /// in the rows D103 compares, <c>current</c> when it declares the same, and null with no record that reads, or a
    /// source an update would refuse. A plugin with no record is not asked, so the list reads one file for it.
    /// </summary>
    private string? UpdateOf(PluginEntry plugin, IEnumerable<string> reserved)
    {
        if (PluginSource.Read(plugin.Folder).Source is null) return null;
        var (plan, _) = PluginInstall.PlanUpdate(_loop.Home, plugin.Manifest.Id, reserved, OffersFolder);
        return plan is null ? null : plan.Changes.Count > 0 ? "waits" : "current";
    }

    /// <summary>
    /// A folder judged for Install from a folder (PLUGUI1e, D119 §3.4), in Ask Daoris's judge's order and words where it
    /// has them: named whole, not Daoris's own nor holding it, there, read as the catalogue reads it with nothing this
    /// build refuses, and not an id already installed. The manifest as written, or why nothing would be copied.
    /// </summary>
    private (PluginManifest? Manifest, string? Refusal) ReadPluginFolder(string folder)
    {
        if (!Path.IsPathRooted(folder))
        {
            return (null, $"a plugin is installed from a folder named whole, as Choose… gives one, and `{folder}` is not.");
        }

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if (PluginInstall.Placement(_loop.Home, full) is { } misplaced) return (null, misplaced);
        if (!Directory.Exists(full)) return (null, $"there is no folder `{full}` on this machine.");

        var (manifest, refusal) = PluginInstall.Read(full, AdapterSet.Built().Names);
        if (refusal is not null) return (null, refusal);
        var id = manifest!.Id;
        return PluginCatalog.Load(_loop.Home).Plugins.Any(p => string.Equals(p.Manifest.Id, id, StringComparison.OrdinalIgnoreCase))
            ? (null,
                $"plugin `{id}` is already installed on this machine. Installing from a folder adds and never replaces: its "
                + "page's Update… takes a newer copy from where it came from, and `daoris plugin add <folder>` at a terminal "
                + "replaces it wholesale; either keeps what it kept.")
            : (manifest, null);
    }

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
