namespace Daoris.Driver;

/// <summary>
/// An installed plugin's record of where it came from (PLUG9 c, D103), on this machine: read from its install folder,
/// written into a staged copy, and a folder's source made whole. Named on <see cref="PluginSource"/>, where its callers
/// always named them, as static members of an extension block (STRUCT3a, D166's note); the record's rules stay the Domain's.
/// </summary>
public static class PluginSourceReads
{
    extension(PluginSource)
    {
        /// <summary>A folder a plugin is added from, recorded as its whole path on this machine.</summary>
        public static PluginSource FromFolder(string folder) => new(PluginFolders.WholePath(folder), null);

        /// <summary>An installed plugin's record: none where there is no file, or the named reason one does not read.</summary>
        public static (PluginSource? Source, string? Problem) Read(string installFolder) =>
            PluginFolders.TextIfThere(Path.Combine(installFolder, PluginSource.FileName)) is { } text
                ? PluginSource.Parse(text)
                : (null, null);

        /// <summary>Written into a staged copy before it is renamed into place, so the record and the install move together.</summary>
        internal static void Write(string installFolder, PluginSource source) =>
            PluginFolders.WriteFile(Path.Combine(installFolder, PluginSource.FileName), source.Json());
    }
}
