namespace Daoris.Desktop;

/// <summary>
/// A folder moved to a place that may be on another volume, which <c>Directory.Move</c> refuses: an install on another
/// drive than the profile is the ordinary case, not the odd one.
/// </summary>
public static class FolderMove
{
    /// <summary>
    /// Rename the folder where both ends share a volume; else copy it, then remove the source only once every file of it
    /// has landed.
    /// </summary>
    /// <param name="copying">Whether to copy rather than rename; null reads the volumes. A test forces it.</param>
    /// <param name="copyFile">Copies one file, never over another; a test hands in one that fails.</param>
    public static void Move(string source, string target, bool? copying = null, Action<string, string>? copyFile = null)
    {
        copying ??= !string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(target), StringComparison.OrdinalIgnoreCase);
        if (copying is not true)
        {
            Directory.Move(source, target);
            return;
        }

        copyFile ??= (from, to) => File.Copy(from, to, overwrite: false);
        var made = new List<string>();
        try
        {
            Make(target, made);
            foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
                Make(Path.Combine(target, Path.GetRelativePath(source, directory)), made);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var to = Path.Combine(target, Path.GetRelativePath(source, file));
                copyFile(file, to);
                made.Add(to);
            }
        }
        catch
        {
            // The source is whole and the target holds only what it held before: a half-copied target would count as state
            // and the move would never be tried again.
            Undo(made);
            throw;
        }

        Directory.Delete(source, recursive: true);
    }

    /// <summary>A folder made, and noted only where this call made it, so a folder that was there is never removed.</summary>
    private static void Make(string folder, List<string> made)
    {
        var missing = new Stack<string>();
        for (var at = folder; at is not null && !Directory.Exists(at); at = Path.GetDirectoryName(at)) missing.Push(at);
        Directory.CreateDirectory(folder);
        made.AddRange(missing);
    }

    /// <summary>What a failed copy made, removed newest first; a file or folder already gone is left.</summary>
    private static void Undo(List<string> made)
    {
        for (var index = made.Count - 1; index >= 0; index--)
        {
            try
            {
                if (File.Exists(made[index])) File.Delete(made[index]);
                else if (Directory.Exists(made[index])) Directory.Delete(made[index], recursive: false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // What cannot be removed stays; the first failure is the one the caller reports.
            }
        }
    }
}
