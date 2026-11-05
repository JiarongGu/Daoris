namespace Daoris.Driver;

/// <summary>
/// What a spawn wrote under the home for one session (its rules, its servers, its skills), removed when the session ends. A file or
/// a folder something still holds open is left for the next start of the same session to replace.
/// </summary>
public static class SpawnFiles
{
    /// <summary>The session's file goes with it; gone is fine, and gone twice.</summary>
    public static void Remove(string? path)
    {
        if (path is null) return;
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left for the next start to overwrite.
        }
    }

    /// <summary>The session's folder goes with it, whole.</summary>
    public static void RemoveFolder(string? folder)
    {
        if (folder is null) return;
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left for the next start of the same session to replace.
        }
    }
}

/// <summary>Runs one action on disposal — for a cleanup that has to ride a `using` beside a process's own.</summary>
internal sealed class Disposer(Action onDispose) : IDisposable
{
    public void Dispose() => onDispose();
}
