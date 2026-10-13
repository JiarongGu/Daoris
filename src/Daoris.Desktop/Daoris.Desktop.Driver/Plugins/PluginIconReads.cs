namespace Daoris.Driver;

/// <summary>
/// A plugin's icon read from its folder (PLUGUI2, D140; the catalogue design §3): the file through the module's
/// Infrastructure, its bytes judged by <see cref="PluginIcon"/>'s rules. Named <c>PluginIcon.Read</c>, where its callers
/// always named it, as a static member of an extension block (STRUCT3a, D166's note).
/// </summary>
public static class PluginIconReads
{
    extension(PluginIcon)
    {
        /// <summary>
        /// The icon a manifest declares, judged as a file in <paramref name="folder"/>, by the file's rules in order: there and a
        /// file; at most <see cref="PluginIcon.MaxBytes"/>; then what its bytes are (<see cref="PluginIcon.Judge"/>). A
        /// manifest's own problem is said first.
        /// </summary>
        public static PluginIconRead Read(string folder, PluginManifest manifest)
        {
            if (manifest.IconProblem is { } declared) return new(null, null, declared);
            if (manifest.Icon is not { } icon) return PluginIconRead.None;

            var (exists, length, bytes) = PluginFolders.ReadIcon(Path.Combine([folder, .. icon.Split('/')]), PluginIcon.MaxBytes);
            if (!exists) return PluginIcon.Refused(icon, "is not a file in the plugin's folder.");
            if (length > PluginIcon.MaxBytes) return PluginIcon.Refused(icon, "is larger than 32 KiB, the most an icon may be.");

            // Daoris's words, never the system's: its message names the file's whole path, which the page must not learn.
            return bytes is null ? PluginIcon.Refused(icon, "could not be read.") : PluginIcon.Judge(icon, bytes);
        }
    }
}
