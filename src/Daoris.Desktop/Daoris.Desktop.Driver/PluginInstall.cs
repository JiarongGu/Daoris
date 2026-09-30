using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// `daoris plugin add`'s copy, the driver's twin (PLUG9): a plugin's folder read by the catalogue's own
/// reader, then copied into the home under its manifest's id. Ask Daoris's <c>plugin</c> proposal is
/// judged by <see cref="Read"/> and applied by <see cref="Add"/>.
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
    public static PluginManifest Add(string home, string folder, IEnumerable<string> reservedHarnesses)
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
            Directory.Move(staging, target);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try { Directory.Delete(staging, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw new DriverException($"`{manifest.Id}` was not added: {error.Message} Nothing was installed.");
        }

        return manifest;
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
