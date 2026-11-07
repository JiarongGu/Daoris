using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A terminal's visit to one of Daoris's trees: the <c>unworked</c> mark taken off before the terminal opens there, and whatever the
/// files stage runs in that tree (an install or the hook) ended with everything it started, so nothing of Daoris's goes on writing
/// beside the person. The stage then stops at its next read of the mark.
/// </summary>
public static class TreeVisit
{
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> s_running = new(StringComparer.Ordinal);

    /// <summary>
    /// The visit into <paramref name="directory"/>: true where the terminal may open; false where it lies in a tree whose mark could
    /// not be removed, which opens nothing.
    /// </summary>
    public static bool Take(string home, string directory)
    {
        if (TreeOf(home, directory) is not { } tree) return true;
        if (!TreeFilesRecord.Unmark(home, tree)) return false;
        if (s_running.TryGetValue(TreeFilesRecord.IdOf(tree), out var running))
        {
            try
            {
                running.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The stage ended as the visit came.
            }
        }

        return true;
    }

    /// <summary>The stage's watch over its tree while it runs: its token is cancelled by a visit.</summary>
    internal static Watched Watch(string tree)
    {
        var id = TreeFilesRecord.IdOf(tree);
        var source = new CancellationTokenSource();
        s_running[id] = source;
        return new Watched(id, source);
    }

    /// <summary>The tree with a files record that holds <paramref name="directory"/>, or null where none does.</summary>
    private static string? TreeOf(string home, string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return null;
        for (var at = new DirectoryInfo(Path.GetFullPath(directory)); at is not null; at = at.Parent)
        {
            if (File.Exists(TreeFilesRecord.PathOf(home, at.FullName))) return at.FullName;
        }

        return null;
    }

    internal sealed class Watched(string id, CancellationTokenSource source) : IDisposable
    {
        internal CancellationToken Token => source.Token;

        public void Dispose()
        {
            s_running.TryRemove(new KeyValuePair<string, CancellationTokenSource>(id, source));
            source.Dispose();
        }
    }
}
