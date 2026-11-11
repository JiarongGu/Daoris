using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// The tidy of a folder department's copies (design §4.7): a copy whose every run is over, and that no session works in, is an orphan
/// nothing will bring back, and is moved aside into the trees' <c>.tidied</c> as a tree is, where the keeping pass removes it once left
/// untouched for the policy's days; its sidecar goes. A copy that names no run is never judged, since whose it is cannot be read.
/// </summary>
public static class CopyTidy
{
    /// <summary>Each orphan copy moved aside, or kept where the move did not happen.</summary>
    /// <param name="inUse">The folders sessions hold: a copy one works in is no orphan.</param>
    /// <param name="over">Whether a run ended with nothing returning its copy, by its id.</param>
    public static IReadOnlyList<CopySetAside> Tidy(string home, IReadOnlySet<string> inUse, DateTimeOffset now, Func<string, bool> over)
    {
        List<CopySetAside> tidied = [];
        foreach (var copy in FolderCopies.All(home).ToList())
        {
            if (copy.Runs.Count == 0 || inUse.Any(folder => TreeGit.SamePath(folder, copy.Folder)) || !copy.Runs.All(over)) continue;
            var to = Path.Combine(new SessionTrees(home).TreesRoot, TreeSetAside.SetAsideFolder, copy.Workspace, copy.Department,
                $"{copy.Id}-{now.UtcDateTime.ToString(TreeSetAside.SetAsideStamp, CultureInfo.InvariantCulture)}");
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(copy.Root));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                if (Directory.Exists(copy.Folder)) Directory.Move(copy.Folder, to);
                File.Delete(copy.Folder + ".json");
                tidied.Add(new(copy.Workspace, copy.Department, copy.Id, name, Directory.Exists(to) ? to : null, null));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                tidied.Add(new(copy.Workspace, copy.Department, copy.Id, name, null, error.Message.TrimEnd().TrimEnd('.')));
            }
        }

        return tidied;
    }
}
