using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// The look's tidy of a folder department's copies (design §4.7): a copy whose every run ended with no return waiting on it, and that
/// no session works in, is an orphan nothing will bring back, and is moved aside into the trees' <c>.tidied</c> as a tree is, where the
/// keeping pass removes it once left untouched for the policy's days; its sidecar goes. A copy that names no run is never judged, since
/// whose it is cannot be read.
/// </summary>
public static class CopyTidy
{
    /// <summary>Each orphan copy moved aside, or kept where the move did not happen.</summary>
    /// <param name="inUse">The folders sessions hold: a copy one works in is no orphan.</param>
    public static IReadOnlyList<CopyTidied> Tidy(string home, IReadOnlySet<string> inUse, DateTimeOffset now)
    {
        var records = new NodeRecordFiles(home);
        List<CopyTidied> tidied = [];
        foreach (var copy in FolderCopies.All(home).ToList())
        {
            if (copy.Runs.Count == 0 || inUse.Any(folder => TreeGit.SamePath(folder, copy.Folder))) continue;
            if (copy.Runs.Select(run => records.Read(RunUnits.IdOf(run))).Any(record => record?.Ended is null || Returning(record))) continue;
            var to = Path.Combine(new SessionTrees(home).TreesRoot, TreeSetAside.SetAsideFolder, copy.Workspace, copy.Department,
                $"{copy.Id}-{now.UtcDateTime.ToString(TreeSetAside.SetAsideStamp, CultureInfo.InvariantCulture)}");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                if (Directory.Exists(copy.Folder)) Directory.Move(copy.Folder, to);
                File.Delete(copy.Folder + ".json");
                tidied.Add(new(copy, Directory.Exists(to) ? to : null, null));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                tidied.Add(new(copy, null, error.Message.TrimEnd().TrimEnd('.')));
            }
        }

        return tidied;
    }

    /// <summary>The tidy's line for <paramref name="tidied"/>, the set-aside wait <paramref name="days"/> long.</summary>
    public static string Said(CopyTidied tidied, double days)
    {
        var (copy, name) = (tidied.Copy, Path.GetFileName(Path.TrimEndingDirectorySeparator(tidied.Copy.Root)));
        if (tidied.Kept is { } why) return $"tidy  {copy.Department}: copy `{copy.Id}` of `{name}` stays, though its run ended and nothing returns it, since it could not be moved aside ({why}).";
        return tidied.MovedTo is { } to
            ? $"tidy  {copy.Department}: copy `{copy.Id}` of `{name}`: its run ended and nothing returns it, so it was moved aside to {to}, and goes once it is left untouched for {days:0} days."
            : $"tidy  {copy.Department}: copy `{copy.Id}` of `{name}`: its run ended and nothing returns it, and its folder was already gone, so its record went.";
    }

    private static bool Returning(NodeRecord record) =>
        record.Statuses.Values.Any(status => status.State == NodeStates.Active && status.Code is ReturnNode.PlaceBusy or ReturnNode.BothChanged);
}

/// <summary>A copy the tidy took: moved aside to <paramref name="MovedTo"/>, or kept for <paramref name="Kept"/>.</summary>
public sealed record CopyTidied(FolderCopy Copy, string? MovedTo, string? Kept);
