namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A test's scratch folder removed once what held it lets go: a killed stand-in host's files while Windows releases
/// them, a module's machine log for a moment after the test, git's read-only objects. Asked again a bounded number of
/// times, and a folder still held after the last is left behind rather than failing the test it served (HOSTCLEAN1).
/// </summary>
internal static class ScratchFolders
{
    /// <summary>Removes <paramref name="path"/>: true once it is gone, false when it was still held after the last try.</summary>
    /// <param name="attempts">How many times it is asked, the first included.</param>
    /// <param name="waitMs">The wait between two tries.</param>
    public static bool Remove(string path, int attempts = 10, int waitMs = 200)
    {
        for (var attempt = 1; ; attempt++)
        {
            if (!Directory.Exists(path)) return true;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                Directory.Delete(path, recursive: true);
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException && attempt < attempts)
            {
                Thread.Sleep(waitMs);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
