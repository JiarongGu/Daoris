namespace Daoris.Driver;

/// <summary>
/// The shell's word on Daoris's own browser for clearing its caches (KEEP1e, D195 point 4; the cleanup design §3.2, §3.3): the shell's
/// browser host, handed to the driver as it hands the in-app browser (<see cref="IInAppBrowser"/>). Only the shell can say whether its
/// browser runs, and only it can hold the browser's start, so a terminal acting alone, with no shell to ask, keeps the caches.
/// </summary>
public interface IBrowserCaches
{
    /// <summary>
    /// Run <paramref name="clear"/> while Daoris's own browser is not running, holding the host's start gate through it so none starts
    /// meanwhile; null, with nothing run, where the browser runs.
    /// </summary>
    Task<T?> WhileStoppedAsync<T>(Func<T> clear, CancellationToken ct) where T : class;
}

/// <summary>What one clear of the browser's caches did: each path's bytes removed, and each kept with why (<see cref="SpaceKept"/>).</summary>
public sealed record BrowserCachesCleared(IReadOnlyDictionary<string, long> Removed, IReadOnlyDictionary<string, string> Kept);

/// <summary>
/// The browser's caches' one removal (KEEP1e, the cleanup design §2.1, §3.2): only the paths of <see cref="SpaceKinds.BrowserCaches"/>,
/// relative to <c>browser-engine</c>'s root, each checked to lie under the root with no link on the way, moved aside by one rename, then
/// removed never following a link. A path not in the table is never touched, nor the profile's cookies, storage or preferences, nor the
/// window's profile. Run inside <see cref="IBrowserCaches.WhileStoppedAsync{T}"/>, which says the browser is not running.
/// </summary>
public static class BrowserCacheClearing
{
    /// <summary>The engine's lock in its profile folder, held open while a browser runs on it.</summary>
    public const string ProfileLock = "lockfile";

    /// <summary>Clear the paths named (each one of the table's), under <paramref name="home"/>'s browser profile.</summary>
    /// <param name="move">The rename aside; a test hands in one that refuses, as a file held open makes Windows refuse.</param>
    public static BrowserCachesCleared Clear(string home, IReadOnlyList<string> paths, Action<string, string>? move = null)
    {
        var removed = new Dictionary<string, long>(StringComparer.Ordinal);
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(DaorisHome.Entry(home, HomeEntry.BrowserEngine)));

        // A browser the host did not start (another shell's, on its way out) still holds the profile's lock: everything stays.
        if (Held(Path.Combine(root, ProfileLock)))
        {
            foreach (var path in paths) kept[path] = SpaceKept.Running;
            return new(removed, kept);
        }

        foreach (var path in paths)
        {
            if (!SpaceKinds.BrowserCaches.Contains(path, StringComparer.Ordinal))
            {
                kept[path] = SpaceKept.Refused;
                continue;
            }

            var full = Path.Combine([root, .. path.Split('/')]);
            if (Linked(root, full))
            {
                kept[path] = SetAsideKept.Linked;
                continue;
            }

            if (!Directory.Exists(full))
            {
                kept[path] = SpaceKept.Gone;
                continue;
            }

            var aside = $"{full}.{Guid.NewGuid().ToString("N")[..8]}{DownloadClearing.Aside}";
            try
            {
                AtomicFile.MoveFolder(full, aside, 5, 200, move);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                kept[path] = SpaceKept.InUse;
                continue;
            }

            try
            {
                removed[path] = TreeSetAside.DeleteNeverFollowing(aside);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Moved aside, nothing reads it; what is left goes at the next press.
                kept[path] = SpaceKept.Failed;
            }
        }

        return new(removed, kept);
    }

    /// <summary>Whether a link lies on the way from <paramref name="root"/> to <paramref name="full"/>, the root and the path included.</summary>
    private static bool Linked(string root, string full)
    {
        var at = root;
        if (IsLink(at)) return true;
        foreach (var part in Path.GetRelativePath(root, full).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (part is "" or "." or "..") return true;
            at = Path.Combine(at, part);
            if (IsLink(at)) return true;
        }

        return false;

        static bool IsLink(string path)
        {
            var attributes = new DirectoryInfo(path).Attributes;
            return (int)attributes != -1 && attributes.HasFlag(FileAttributes.ReparsePoint);
        }
    }

    /// <summary>Whether a file is held open against a delete-free open: a running engine's profile lock.</summary>
    private static bool Held(string file)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(file)) return false;
        try
        {
            using var _ = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
}
