namespace Daoris.Driver;

/// <summary>
/// `daoris plugin add`'s copy, the driver's twin (PLUG9): a plugin's folder read by the catalogue's own
/// reader, then copied into the home under its manifest's id. Ask Daoris's <c>plugin</c> proposal is
/// judged by <see cref="Read"/> and applied by <see cref="Add(string, string, IEnumerable{string})"/>. Since PLUG9 (c) and (d) (D103) an add
/// records where the plugin came from, an offer of the install is added by its id, and an installed
/// plugin with a record is updated from it.
/// </summary>
/// <remarks>
/// <para><b>A twin of the CLI's <c>plugins.ts</c></b> (`.claude/knowledge/twins.md`): the same refusals in
/// the same words, a copy staged beside and renamed in, links neither followed nor copied, and the data
/// folder untouched. They share no code, and each side's tests hold the same table.</para>
///
/// <para><b>One difference, deliberate:</b> this adds, and never replaces an installed plugin. Replacing
/// one wholesale is <c>daoris plugin add</c>'s at a terminal; a proposal that could overwrite an installed
/// plugin from a checkout would change what runs under a card that said it adds.</para>
///
/// <para>🔴 <b>Nothing a plugin declares is started here.</b> Adding copies a folder; the driver loop
/// starts a hook at its next look, as it does any plugin, and a harness or a server runs only with a
/// session.</para>
///
/// <para>The use case (STRUCT3a): the refusals and their order are here, the folders it copies, moves and forgets are
/// <see cref="PluginFolders"/>'.</para>
/// </remarks>
public static class PluginInstall
{
    /// <summary>
    /// What `plugin add` would read from a folder: its manifest with the placeholders as written, or why
    /// nothing would be copied — no manifest, one the catalogue would refuse, or one this build refuses.
    /// </summary>
    public static (PluginManifest? Manifest, string? Refusal) Read(string folder, IEnumerable<string> reservedHarnesses)
    {
        var path = Path.Combine(folder, PluginCatalog.ManifestName);
        if (!PluginFolders.HasFile(path))
        {
            return (null, $"no `{PluginCatalog.ManifestName}` in {folder} — a plugin is a folder with a manifest at its root.");
        }

        // Read as the catalogue would, with the folder it lands in named by its own id: the id decides
        // where it goes, so the folder it comes from may be called anything.
        var (manifest, problem) = PluginCatalog.ReadAsWritten(IdOf(path), path);
        if (problem is not null) return (null, $"`{PluginCatalog.ManifestName}` in {folder}: {problem}");
        return PluginCatalog.RefusedByThisBuild(manifest, reservedHarnesses) is { } refused
            ? (null, $"plugin `{manifest.Id}` {refused}")
            : (manifest, null);
    }

    /// <summary>
    /// Why a folder is no place to add a plugin from, as it stands to the home: inside it is Daoris's own
    /// (an installed plugin, or what one kept), and one holding it would be copied into itself. Null when neither.
    /// </summary>
    public static string? Placement(string home, string folder)
    {
        var from = Path.TrimEndingDirectorySeparator(PluginFolders.WholePath(folder));
        var own = Path.TrimEndingDirectorySeparator(PluginFolders.WholePath(home));
        if (Within(from, own)) return $"{folder} is inside Daoris's home, which holds what Daoris installed and kept — add a plugin from the repository that holds it.";
        if (Within(own, from)) return $"{folder} holds Daoris's home, and copying it would copy the home into itself — name the plugin's own folder.";
        return null;
    }

    /// <summary>
    /// Copy a plugin in under its id, as `daoris plugin add` does for one not yet installed: staged beside
    /// as a dot-folder the catalogue never reads, then renamed into place. Answers the manifest as written.
    /// </summary>
    /// <exception cref="DriverException">The folder is refused, the id is installed, or the copy failed; nothing is installed.</exception>
    public static PluginManifest Add(string home, string folder, IEnumerable<string> reservedHarnesses) =>
        AddFrom(home, folder, PluginSource.FromFolder(folder), reservedHarnesses);

    /// <summary>
    /// One of Daoris's own plugins the install offers, added by its id with the same copy (PLUG9 d), the
    /// offer recorded so a republish's newer one can be taken by <see cref="Update"/>.
    /// </summary>
    /// <param name="offers">The install's offers folder (<see cref="PluginOffers"/>).</param>
    /// <exception cref="DriverException">Not an id, not offered, refused, or installed already; nothing is installed.</exception>
    public static PluginManifest AddOffer(string home, string? offers, string id, IEnumerable<string> reservedHarnesses)
    {
        var reserved = reservedHarnesses.ToList();
        if (!PluginCatalog.IsId(id.ToLowerInvariant())) throw new DriverException(NotAnId(id));
        var offered = PluginOffers.Load(offers, home, reserved);
        var offer = offered.FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new DriverException($"this install offers no plugin `{id}` — "
                + (offered.Count > 0 ? $"it offers {string.Join(", ", offered.Select(each => $"`{each.Id}`"))}." : "it offers none."));
        return AddFrom(home, offer.Folder, PluginSource.FromOffer(offer.Id), reserved);
    }

    /// <summary>
    /// A package's plugin folder, extracted by <see cref="PluginPackage"/> into a stage of its own under the home,
    /// added with the same copy and the package recorded (PLUGDIST1a, D120 §5.7 step 4). That stage is the one
    /// folder inside the home an add copies from, since the reader made it and nothing else writes there.
    /// </summary>
    internal static PluginManifest Add(string home, string stage, PluginPackageOrigin package, IEnumerable<string> reservedHarnesses) =>
        AddFrom(home, stage, PluginSource.FromPackage(package), reservedHarnesses, unpacked: true);

    private static PluginManifest AddFrom(string home, string folder, PluginSource source, IEnumerable<string> reservedHarnesses, bool unpacked = false)
    {
        if (!unpacked && Placement(home, folder) is { } misplaced) throw new DriverException($"{misplaced} Nothing was copied.");
        var (read, refusal) = Read(folder, reservedHarnesses);
        if (refusal is not null) throw new DriverException($"{refusal} Nothing was copied.");
        var manifest = read!;

        var root = DaorisHome.Entry(home, HomeEntry.Plugins);
        var target = Path.Combine(root, manifest.Id);
        if (PluginFolders.There(target))
        {
            throw new DriverException(
                $"plugin `{manifest.Id}` is already installed on this machine. Replacing it is `daoris plugin add <folder>` "
                + "at a terminal, which replaces it wholesale and keeps what it kept. Nothing was copied.");
        }

        PluginFolders.Make(root);
        var staging = Path.Combine(root, $".adding-{manifest.Id}-{Guid.NewGuid():N}");
        try
        {
            PluginFolders.CopyTree(folder, staging);
            // Where it came from, in the copy before it is renamed in (PLUG9 c): one move for both.
            PluginSource.Write(staging, source);
            PluginFolders.MoveInto(staging, target);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            PluginFolders.Forget(staging);
            throw new DriverException($"`{manifest.Id}` was not added: {error.Message} Nothing was installed.");
        }

        return manifest;
    }

    /// <summary>
    /// What updating an installed plugin would do, or why it cannot (PLUG9 c): its record, the source it
    /// names read by the catalogue's own reader, the same plugin by id, nothing this build refuses — and
    /// what changes. The refusals come in the CLI's order and words (<c>planUpdate</c>), and nothing is written.
    /// </summary>
    /// <param name="offers">The install's offers folder, where an offer's source is found; null is the one beside the home.</param>
    public static (PluginUpdatePlan? Plan, string? Refusal) PlanUpdate(
        string home, string id, IEnumerable<string> reservedHarnesses, string? offers = null, bool shipped = false)
    {
        var reserved = reservedHarnesses.ToList();
        static (PluginUpdatePlan?, string?) Refuse(string refusal) => (null, refusal);
        if (!PluginCatalog.IsId(id.ToLowerInvariant())) return Refuse(NotAnId(id));
        var entry = PluginCatalog.Load(home, reserved).Plugins
            .FirstOrDefault(plugin => string.Equals(Path.GetFileName(plugin.Folder), id, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return Refuse($"no plugin `{id}` on this machine — `daoris plugin list` shows what there is.");
        var installed = Path.GetFileName(entry.Folder)!;

        // LANDPIN3: the person's press to take the install's own copy of an example they hold an older copy of, whatever record
        // the copy has or lacks. A copy whose files they changed is never replaced, and one not older is never taken back.
        PluginSource? source;
        string? problem;
        string? shippedFrom = null;
        if (shipped)
        {
            var folder = offers ?? PluginOffers.BesideHome(home);
            var offer = PluginOffers.Load(folder, home, reserved).FirstOrDefault(each => string.Equals(each.Id, installed, StringComparison.OrdinalIgnoreCase));
            if (offer is not { Problem: null })
            {
                return Refuse($"Daoris's own `{installed}` is not offered by this install, so there is no shipped copy to take. "
                    + $"`daoris plugin remove {installed}` takes the installed one out.");
            }

            var (written, _) = PluginCatalog.ReadAsWritten(installed, Path.Combine(entry.Folder, PluginCatalog.ManifestName));
            switch (PluginOffers.ShippedOf(entry.Folder, written, offer)?.State)
            {
                case PluginShipped.Changed:
                    return Refuse($"plugin `{installed}` has files changed here that this install's {offer.Manifest.Version} does not have, "
                        + "so it is left as it is. Move your changes out, `daoris plugin remove` it and install it again to take the shipped one.");
                case PluginShipped.Older:
                    break;
                default:
                    return Refuse($"plugin `{installed}` is not older than the {offer.Manifest.Version} this install ships, so there is nothing to take.");
            }

            (source, problem, shippedFrom) = (PluginSource.FromOffer(installed), null, offer.Folder);
        }
        else
        {
            (source, problem) = PluginSource.Read(entry.Folder);
        }

        if (problem is not null)
        {
            return Refuse($"plugin `{installed}`'s record of where it came from does not read, so there is nothing to update "
                + $"it from: {problem} `daoris plugin add <folder>` replaces it and records it again.");
        }

        if (source is null)
        {
            return Refuse($"plugin `{installed}` has no record of where it came from: it was added before Daoris kept one, or "
                + "copied in by hand, so there is nothing to update it from. `daoris plugin add <folder>` replaces it wholesale "
                + "and records where it came from.");
        }

        // PLUGDIST1a: a package is installed whole, and this build reads no package source to take a newer
        // version from; the CLI's `planUpdate` says the same, word for word.
        if (source.Package is not null)
        {
            return Refuse($"plugin `{installed}` came from {source.Said}, and a plugin from a package is installed whole. "
                + $"A newer package takes its place: `daoris plugin remove {installed}`, then "
                + "`daoris-driver plugins install <file.nupkg>`, and what it kept stays where it is.");
        }

        string from;
        if (shippedFrom is not null)
        {
            from = shippedFrom;
        }
        else if (source.Offer is { } offer)
        {
            from = Path.Combine(offers ?? PluginOffers.BesideHome(home), offer);
            if (!PluginFolders.HasFile(Path.Combine(from, PluginCatalog.ManifestName)))
            {
                return Refuse($"Daoris's own `{offer}` is not offered by this install any more, so there is nothing to "
                    + $"update `{installed}` from. It stays as it is; `daoris plugin remove {installed}` takes it out.");
            }
        }
        else
        {
            from = source.Folder!;
        }

        if (Placement(home, from) is { } misplaced) return Refuse(misplaced);
        if (!PluginFolders.There(from))
        {
            return Refuse($"the folder `{installed}` was added from is not there any more: {from}. `daoris plugin add <folder>` "
                + "from where it is now replaces it and records the new place.");
        }

        var path = Path.Combine(from, PluginCatalog.ManifestName);
        if (!PluginFolders.HasFile(path)) return Refuse($"no `{PluginCatalog.ManifestName}` in {from} — a plugin is a folder with a manifest at its root.");

        // Read as the catalogue would, named by its own id, which is then held to the installed one.
        var (next, unsound) = PluginCatalog.ReadAsWritten(IdOf(path), path);
        if (unsound is not null) return Refuse($"`{PluginCatalog.ManifestName}` in {from}: {unsound}");
        if (!string.Equals(next.Id, installed, StringComparison.OrdinalIgnoreCase))
        {
            return Refuse($"{from} now holds plugin `{next.Id}`, not `{installed}` — an update replaces a plugin "
                + "with its own next version, never with another plugin.");
        }

        if (PluginCatalog.RefusedByThisBuild(next, reserved) is { } refused) return Refuse($"plugin `{installed}` {refused}");

        var (current, _) = PluginCatalog.ReadAsWritten(installed, Path.Combine(entry.Folder, PluginCatalog.ManifestName));
        return (new PluginUpdatePlan(installed, source, from, Changes(current, next)), null);
    }

    /// <summary>
    /// Update an installed plugin from its source: judged again, copied beside, the record written into the
    /// copy, the installed folder moved aside WHOLE and the copy renamed into place. `.data/` is its sibling
    /// and never touched. 🔴 Nothing is started: the caller stops the plugin's hook first, and the loop starts
    /// the new one at its next look.
    /// </summary>
    /// <exception cref="DriverException">Refused, or its folder is held open; the installed version is untouched.</exception>
    public static PluginUpdatePlan Update(string home, string id, IEnumerable<string> reservedHarnesses, string? offers = null, bool shipped = false)
    {
        var (plan, refusal) = PlanUpdate(home, id, reservedHarnesses, offers, shipped);
        if (plan is null) throw new DriverException($"{refusal} Nothing was replaced.");

        var root = DaorisHome.Entry(home, HomeEntry.Plugins);
        var target = Path.Combine(root, plan.Id);
        var stamp = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(root, $".updating-{plan.Id}-{stamp}");
        var aside = Path.Combine(root, $".removing-{plan.Id}-{stamp}");
        try
        {
            PluginFolders.CopyTree(plan.From, staging);
            PluginSource.Write(staging, plan.Source);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            PluginFolders.Forget(staging);
            throw new DriverException($"`{plan.Id}` was not updated: {error.Message} The installed version is untouched.");
        }

        try
        {
            PluginFolders.Move(target, aside);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            PluginFolders.Forget(staging);
            throw new DriverException(
                $"`{plan.Id}` was not updated: something on this machine still has its folder open — a running desktop's "
                + $"hook process, most likely. `daoris plugin disable {plan.Id}`, give the desktop a moment to stop it, then "
                + "update it again. The installed version is untouched.");
        }

        try
        {
            PluginFolders.MoveInto(staging, target);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            PluginFolders.Move(aside, target);
            PluginFolders.Forget(staging);
            throw new DriverException($"`{plan.Id}` was not updated: {error.Message} The installed version is untouched.");
        }

        // A dot-folder is never read as a plugin, so one that cannot be deleted now is nobody's.
        PluginFolders.Forget(aside);
        return plan;
    }

    /// <summary>What an update changes, from what is installed to what the source now holds, each as written (<see cref="PluginChange.Between"/>).</summary>
    public static IReadOnlyList<PluginChange> Changes(PluginManifest was, PluginManifest now) => PluginChange.Between(was, now);

    private static string NotAnId(string id) =>
        $"`{id}` is not a plugin id — one is lowercase letters, digits, dots and dashes, like `acme.quiet-hours`; "
        + "`daoris plugin list` shows what there is.";

    /// <summary>The id a manifest names, or empty where it names none or does not read — the catalogue then says why.</summary>
    internal static string IdOf(string path) =>
        (PluginFolders.ReadText(path).Text is { } text ? PluginCatalog.IdIn(text) : null) ?? "";

    private static bool Within(string path, string folder)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(path, folder, comparison)
            || path.StartsWith(folder + Path.DirectorySeparatorChar, comparison);
    }
}
