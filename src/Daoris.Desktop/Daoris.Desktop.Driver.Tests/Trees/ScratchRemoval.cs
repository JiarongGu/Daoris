namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A scratch folder of repositories removed whole: git writes its objects read-only, a copy keeps that, and
/// <c>Directory.Delete</c> refuses a read-only file on Windows, so each file is made writable first, as <c>GitTree</c>'s cleanup
/// does. A link is never walked into, so nothing it points at outside the folder changes. A file something still holds (a
/// process a test started, a scanner) is waited for, 12 tries a quarter second apart; the last refusal is thrown, as <c>Directory.Delete</c> throws it.
/// </summary>
internal static class ScratchRemoval
{
    /// <param name="folder">The scratch folder to remove.</param>
    /// <param name="retry">A test's synchronous control at a refused attempt before the next one; otherwise wait a quarter second.</param>
    public static void Remove(string folder, Action<int>? retry = null)
    {
        for (var attempt = 1; Directory.Exists(folder); attempt++)
        {
            try
            {
                var walk = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
                foreach (var file in Directory.EnumerateFiles(folder, "*", walk))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(folder, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 12) throw;
                if (retry is null) Thread.Sleep(250);
                else retry(attempt);
            }
        }
    }

    /// <summary>
    /// A <see cref="Remove"/> whose last refusal is let go: what a test's <c>Dispose</c> calls, so a straggler's hold
    /// on a file leaves the folder for a later clear and never fails the test that has already passed.
    /// </summary>
    public static void Discard(string folder)
    {
        try
        {
            Remove(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Scratch left here is under the ignored fixtures folder; the retries in Remove have already waited.
        }
    }
}
