namespace Daoris.Driver;

/// <summary>
/// A folder just written, renamed into its place. Windows refuses a folder's rename while any program holds a file inside it open, and a
/// scanner, an indexer or a git reading the tree does for a moment after the files are written, so a refusal is asked again, a bounded
/// number of times, before it is said with its cause.
/// </summary>
internal static class FolderRename
{
    /// <summary>How many times the rename is tried, the first included.</summary>
    internal const int Tries = 12;

    /// <summary>The test seam: run with the try just made instead of the wait before the next. Production sets none.</summary>
    internal static readonly AsyncLocal<Action<int>?> Waiting = new();

    /// <summary><paramref name="from"/> renamed to <paramref name="to"/>, an absent folder; an existing one is the caller's to judge and is never retried.</summary>
    /// <exception cref="IOException">The rename was refused on every try, said with the program-held cause; or <paramref name="to"/> exists.</exception>
    internal static void Into(string from, string to, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Move(from, to);
                return;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException && !Directory.Exists(to))
            {
                if (attempt == Tries)
                {
                    throw new IOException(
                        $"the folder it made could not be renamed to `{Path.GetFileName(to.TrimEnd(Path.DirectorySeparatorChar))}` after {Tries} tries, "
                        + $"since a program kept a file in it open: {error.Message.Trim()}", error);
                }

                ct.ThrowIfCancellationRequested();
                if (Waiting.Value is { } wait) wait(attempt);
                else Thread.Sleep(Math.Min(attempt * 50, 400));
            }
        }
    }
}
