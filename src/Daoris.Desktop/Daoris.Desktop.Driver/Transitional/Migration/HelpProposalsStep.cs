namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's proposals as an older build kept them, a file each in a folder of their own. The box is gone (a place to open is a link
/// in its answer, every change a press), so nothing reads them: each file is copied to the salvage folder and named in the migration's
/// line, and the folder is removed. It ends when the owner's install finds no folder; it is then deleted, with
/// <see cref="HomeEntry.HelpProposals"/> in the three home tables.
/// </summary>
public sealed class HelpProposalsStep : IHomeMigrationStep
{
    public string Name => "help-proposals";

    public void Run(HomeMigrationRun run)
    {
        var folder = DaorisHome.Entry(run.Home, HomeEntry.HelpProposals);
        if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            run.Keep(file);
            run.Said(file);
        }

        Directory.Delete(folder, recursive: true);
    }
}
