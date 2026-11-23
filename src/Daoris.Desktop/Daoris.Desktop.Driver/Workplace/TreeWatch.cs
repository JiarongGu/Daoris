namespace Daoris.Driver;

/// <summary>
/// The file system's own word that something in a tree changed, kept from before its judgement until it is moved aside: a write in place
/// to a file deep in the tree moves no stamp at its top (<see cref="TreeStamp"/>), and walking the tree to find it under the tidy's hold is
/// what the hold no longer does. Any event, or an overflow of the system's buffer, is a change; a tree read after one is kept.
/// </summary>
internal sealed class TreeWatch : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private string? _seen;

    private TreeWatch(FileSystemWatcher watcher) => _watcher = watcher;

    /// <summary>The first path the watch saw change, as a clause; null where none did.</summary>
    internal string? Said => Volatile.Read(ref _seen) is { } path ? $"{path} changed between the judgement and the move" : null;

    /// <summary>
    /// Watching <paramref name="tree"/> and everything under it; null where the system gives no watch, which leaves the tree's stamps
    /// as the only signal, as no watch would anywhere a tree is not on a file system that reports changes.
    /// </summary>
    internal static TreeWatch? Start(string tree)
    {
        try
        {
            var watcher = new FileSystemWatcher(tree)
            {
                IncludeSubdirectories = true,
                InternalBufferSize = 64 * 1024,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.DirectoryName
                    | NotifyFilters.Attributes | NotifyFilters.CreationTime,
            };
            var watch = new TreeWatch(watcher);
            watcher.Changed += (_, change) => watch.Saw(change.FullPath);
            watcher.Created += (_, change) => watch.Saw(change.FullPath);
            watcher.Deleted += (_, change) => watch.Saw(change.FullPath);
            watcher.Renamed += (_, change) => watch.Saw(change.FullPath);
            watcher.Error += (_, _) => watch.Saw("the system's list of changes overflowed, so something");
            watcher.EnableRaisingEvents = true;
            return watch;
        }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    private void Saw(string path) => Interlocked.CompareExchange(ref _seen, path, null);

    public void Dispose() => _watcher.Dispose();
}
