namespace Daoris.Driver;

/// <summary>
/// Where an install carries its doctrine tool: one bundled file, <c>app/cli/daoris.mjs</c>, which the publish lays (<c>CLI_BUNDLE</c>;
/// <c>desktop-publish.test.ts</c> holds the twin). Found by the install's files, never by a <c>PATH</c> lookup.
/// </summary>
public static class DoctrineTool
{
    /// <summary>The bundle's file name, in <c>app/cli/</c>.</summary>
    public const string Bundle = "daoris.mjs";

    /// <summary>The bundle the install of a home carries, or null where its file is not there.</summary>
    public static string? OfHome(string home) =>
        Existing(Path.Combine(DaorisHome.FolderOf(Path.GetFullPath(home)).Folder, DaorisHome.InstallApp, "cli", Bundle));

    /// <summary>The bundle beside the desktop's program (<c>app/daoris-driver/</c>), or null where its file is not there.</summary>
    public static string? BesideProgram(string programFolder) => Existing(Path.Combine(programFolder, "..", "cli", Bundle));

    private static string? Existing(string entry) => File.Exists(Path.GetFullPath(entry)) ? Path.GetFullPath(entry) : null;
}
