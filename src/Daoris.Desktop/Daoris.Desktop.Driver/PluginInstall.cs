using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Where an installed plugin came from (PLUG9 c, D102): the folder it was added from, a whole path, or the
/// offer of this install it was installed from, by id. Exactly one of the two.
/// </summary>
/// <remarks>
/// <para>It is <see cref="FileName"/> in the plugin's install folder, written into the staged copy before
/// the swap: replaced with the install, removed with it, and absent from a folder copied in by hand, so
/// "no record" is said and never guessed. The catalogue reads nothing but the manifest.</para>
///
/// <para>A twin of the CLI's <c>readPluginSource</c> in <c>plugins.ts</c>: the same file, the same shapes,
/// the same problems in the same words, and each side's tests hold the same table.</para>
/// </remarks>
public sealed record PluginSource(string? Folder, string? Offer)
{
    public const string FileName = ".daoris-source.json";

    public static PluginSource FromFolder(string folder) => new(Path.GetFullPath(folder), null);

    public static PluginSource FromOffer(string id) => new(null, id);

    /// <summary>Where it came from, said for a person.</summary>
    public string Said => Offer is { } offer ? $"Daoris's own plugins, offered by this install (`{offer}`)" : Folder!;

    /// <summary>An installed plugin's record: none where there is no file, or the named reason one does not read.</summary>
    public static (PluginSource? Source, string? Problem) Read(string installFolder)
    {
        var path = Path.Combine(installFolder, FileName);
        if (!File.Exists(path)) return (null, null);
        static (PluginSource?, string?) Unread(string why) => (null, $"`{FileName}` does not read ({why}).");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException error)
        {
            return Unread($"it is not JSON: {error.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Unread("it is not a JSON object");
            var folder = Text(root, "folder");
            var offer = Text(root, "offer");
            if (folder is not null && offer is not null) return Unread("it names both a folder and an offer");
            if (folder is not null) return Path.IsPathFullyQualified(folder) ? (new PluginSource(folder, null), null) : Unread("its folder is not a whole path");
            if (offer is not null) return PluginCatalog.IsId(offer) ? (new PluginSource(null, offer), null) : Unread("its offer is not a plugin id");
            return Unread("it names neither a folder nor an offer");
        }
    }

    /// <summary>Written into a staged copy before it is renamed into place, so the record and the install move together.</summary>
    internal static void Write(string installFolder, PluginSource source)
    {
        var json = source.Offer is { } offer
            ? JsonSerializer.Serialize(new { offer }, Indented)
            : JsonSerializer.Serialize(new { folder = source.Folder }, Indented);
        File.WriteAllText(Path.Combine(installFolder, FileName), json.Replace("\r\n", "\n") + "\n", new System.Text.UTF8Encoding(false));
    }

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>One thing an update changes (PLUG9 c): `version`, `command`, `points`, `harnesses` or `servers`, each side as its manifest writes it; empty is none.</summary>
public sealed record PluginChange(string What, string Was, string Now);

/// <summary>What updating an installed plugin would do: the source it re-reads, the folder that is, and what changes.</summary>
/// <param name="From">The folder the update copies from, resolved on this machine.</param>
public sealed record PluginUpdatePlan(string Id, PluginSource Source, string From, IReadOnlyList<PluginChange> Changes);

/// <summary>
/// `daoris plugin add`'s copy, the driver's twin (PLUG9): a plugin's folder read by the catalogue's own
/// reader, then copied into the home under its manifest's id. Ask Daoris's <c>plugin</c> proposal is
/// judged by <see cref="Read"/> and applied by <see cref="Add"/>. Since PLUG9 (c) and (d) (D102) an add
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
        if (!File.Exists(path))
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
        var from = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var own = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
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

    private static PluginManifest AddFrom(string home, string folder, PluginSource source, IEnumerable<string> reservedHarnesses)
    {
        if (Placement(home, folder) is { } misplaced) throw new DriverException($"{misplaced} Nothing was copied.");
        var (read, refusal) = Read(folder, reservedHarnesses);
        if (refusal is not null) throw new DriverException($"{refusal} Nothing was copied.");
        var manifest = read!;

        var root = Path.Combine(home, PluginCatalog.Folder);
        var target = Path.Combine(root, manifest.Id);
        if (Directory.Exists(target))
        {
            throw new DriverException(
                $"plugin `{manifest.Id}` is already installed on this machine. Replacing it is `daoris plugin add <folder>` "
                + "at a terminal, which replaces it wholesale and keeps what it kept. Nothing was copied.");
        }

        Directory.CreateDirectory(root);
        var staging = Path.Combine(root, $".adding-{manifest.Id}-{Guid.NewGuid():N}");
        try
        {
            CopyTree(folder, staging);
            // Where it came from, in the copy before it is renamed in (PLUG9 c): one move for both.
            PluginSource.Write(staging, source);
            Directory.Move(staging, target);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try { Directory.Delete(staging, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
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
        string home, string id, IEnumerable<string> reservedHarnesses, string? offers = null)
    {
        var reserved = reservedHarnesses.ToList();
        static (PluginUpdatePlan?, string?) Refuse(string refusal) => (null, refusal);
        if (!PluginCatalog.IsId(id.ToLowerInvariant())) return Refuse(NotAnId(id));
        var entry = PluginCatalog.Load(home, reserved).Plugins
            .FirstOrDefault(plugin => string.Equals(Path.GetFileName(plugin.Folder), id, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return Refuse($"no plugin `{id}` on this machine — `daoris plugin list` shows what there is.");
        var installed = Path.GetFileName(entry.Folder)!;

        var (source, problem) = PluginSource.Read(entry.Folder);
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

        string from;
        if (source.Offer is { } offer)
        {
            from = Path.Combine(offers ?? PluginOffers.BesideHome(home), offer);
            if (!File.Exists(Path.Combine(from, PluginCatalog.ManifestName)))
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
        if (!Directory.Exists(from))
        {
            return Refuse($"the folder `{installed}` was added from is not there any more: {from}. `daoris plugin add <folder>` "
                + "from where it is now replaces it and records the new place.");
        }

        var path = Path.Combine(from, PluginCatalog.ManifestName);
        if (!File.Exists(path)) return Refuse($"no `{PluginCatalog.ManifestName}` in {from} — a plugin is a folder with a manifest at its root.");

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
    public static PluginUpdatePlan Update(string home, string id, IEnumerable<string> reservedHarnesses, string? offers = null)
    {
        var (plan, refusal) = PlanUpdate(home, id, reservedHarnesses, offers);
        if (plan is null) throw new DriverException($"{refusal} Nothing was replaced.");

        var root = Path.Combine(home, PluginCatalog.Folder);
        var target = Path.Combine(root, plan.Id);
        var stamp = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(root, $".updating-{plan.Id}-{stamp}");
        var aside = Path.Combine(root, $".removing-{plan.Id}-{stamp}");
        try
        {
            CopyTree(plan.From, staging);
            PluginSource.Write(staging, plan.Source);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Forget(staging);
            throw new DriverException($"`{plan.Id}` was not updated: {error.Message} The installed version is untouched.");
        }

        try
        {
            Directory.Move(target, aside);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Forget(staging);
            throw new DriverException(
                $"`{plan.Id}` was not updated: something on this machine still has its folder open — a running desktop's "
                + $"hook process, most likely. `daoris plugin disable {plan.Id}`, give the desktop a moment to stop it, then "
                + "update it again. The installed version is untouched.");
        }

        try
        {
            Directory.Move(staging, target);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Directory.Move(aside, target);
            Forget(staging);
            throw new DriverException($"`{plan.Id}` was not updated: {error.Message} The installed version is untouched.");
        }

        // A dot-folder is never read as a plugin, so one that cannot be deleted now is nobody's.
        Forget(aside);
        return plan;
    }

    /// <summary>What an update changes, from what is installed to what the source now holds, each as written — the CLI's <c>changesBetween</c>.</summary>
    public static IReadOnlyList<PluginChange> Changes(PluginManifest was, PluginManifest now)
    {
        static Dictionary<string, string> Said(PluginManifest manifest) => new(StringComparer.Ordinal)
        {
            ["version"] = manifest.Version,
            ["command"] = Line(manifest.Hooks?.Command ?? []),
            ["points"] = string.Join(", ", manifest.Hooks?.Points ?? []),
            ["harnesses"] = string.Join("; ", manifest.Harnesses.Select(harness => $"{harness.Name} ({Line(harness.Command)})")),
            ["servers"] = string.Join("; ", manifest.Servers.Select(server => $"{server.Name} ({Line(server.Command)})")),
        };
        var before = Said(was);
        var after = Said(now);
        return [.. new[] { "version", "command", "points", "harnesses", "servers" }
            .Where(what => before[what] != after[what])
            .Select(what => new PluginChange(what, before[what], after[what]))];
    }

    /// <summary>A command as a terminal takes it: a word holding a space quoted, as the proposal card spells it.</summary>
    private static string Line(IEnumerable<string> command) =>
        string.Join(" ", command.Select(word => word.Contains(' ') ? $"\"{word}\"" : word));

    private static string NotAnId(string id) =>
        $"`{id}` is not a plugin id — one is lowercase letters, digits, dots and dashes, like `acme.quiet-hours`; "
        + "`daoris plugin list` shows what there is.";

    private static void Forget(string folder)
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The id a manifest names, or empty where it names none or does not read — the catalogue then says why.</summary>
    private static string IdOf(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                    ? id.GetString()!
                    : "";
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    /// <summary>File by file; a link is neither followed nor copied, as the CLI's copy does (D3).</summary>
    private static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var entry in new DirectoryInfo(from).EnumerateFileSystemInfos())
        {
            if (entry.LinkTarget is not null) continue;
            var target = Path.Combine(to, entry.Name);
            if (entry is DirectoryInfo directory) CopyTree(directory.FullName, target);
            else if (entry is FileInfo file) file.CopyTo(target);
        }
    }

    private static bool Within(string path, string folder)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(path, folder, comparison)
            || path.StartsWith(folder + Path.DirectorySeparatorChar, comparison);
    }
}
