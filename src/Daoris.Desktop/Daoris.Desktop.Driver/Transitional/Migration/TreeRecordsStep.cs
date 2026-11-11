using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The tree files records and install logs that outlived their tree: each is named by the hash of its tree's path
/// (<see cref="TreeFilesRecord.IdOf"/>), and one whose hash is no folder standing under the trees home's workspaces and repositories is
/// removed, uncopied, since nothing reads it again. The shared records (<c>common.</c>, <c>setup.</c>) and anything else there are left,
/// and the step run again changes nothing.
/// </summary>
public sealed partial class TreeRecordsStep : IHomeMigrationStep
{
    public string Name => "tree-records";

    [GeneratedRegex(@"^(?<id>[0-9a-f]{20})(\.json|\.[0-9a-f]{8}\.log)$")]
    private static partial Regex Mine();

    public void Run(HomeMigrationRun run)
    {
        var folder = Path.GetDirectoryName(DaorisHome.Entry(run.Home, HomeEntry.TreeFiles, "x"))!;
        if (!Directory.Exists(folder)) return;
        var standing = Standing(run.Home);
        foreach (var file in Directory.EnumerateFiles(folder).ToList())
        {
            if (Mine().Match(Path.GetFileName(file)) is not { Success: true } match || standing.Contains(match.Groups["id"].Value)) continue;
            try
            {
                File.Delete(file);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            run.Count(Name, "removed");
        }
    }

    /// <summary>The ids of every tree folder under the trees home, the set-aside folders left out.</summary>
    private static HashSet<string> Standing(string home)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var trees = DaorisHome.Entry(home, HomeEntry.Trees);
        if (!Directory.Exists(trees)) return ids;
        foreach (var workspace in Directory.EnumerateDirectories(trees).Where(each => !Path.GetFileName(each).StartsWith('.')))
        {
            foreach (var repository in Directory.EnumerateDirectories(workspace))
            {
                foreach (var tree in Directory.EnumerateDirectories(repository)) ids.Add(TreeFilesRecord.IdOf(tree));
            }
        }

        return ids;
    }
}
