namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A test's scratch home, named under <see cref="TestHomes.Root"/> and removed once what held it lets go: a killed
/// stand-in host's files while Windows releases them, a module's machine log for a moment after the test, git's
/// read-only objects. Asked again a bounded number of times, and a folder still held after the last is left behind
/// rather than failing the test it served. Linked into the modules' tests.
/// </summary>
internal static class ScratchFolders
{
    /// <summary>A fresh path under <see cref="TestHomes.Root"/>: <paramref name="name"/> and eight hex digits. Nothing is created.</summary>
    public static string New(string name) => Path.Combine(TestHomes.Root, name + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Removes <paramref name="path"/>: true once it is gone, false when it is still held after the last try.</summary>
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
