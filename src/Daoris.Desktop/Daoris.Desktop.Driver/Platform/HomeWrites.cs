using System.Collections.Concurrent;
using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// The marks of what this process wrote itself to a file of the home (FABRIC-EVENT4, the design §A4.1, EVENT2's rule): a writer
/// marks the file after it wrote it, and a <c>HomeWatch</c> that hears the write finds the file as marked and wakes
/// nothing, so a look's own write never wakes the look that made it. A mark is the file's stamp, so a later move by anyone else
/// is not the mark's, and is heard. A file of its own, using the base library alone, because the launcher compiles it in beside
/// <c>StagedBuild</c>, which marks the journal it writes.
/// </summary>
public static class HomeWrites
{
    private static readonly ConcurrentDictionary<string, string> Marked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>This process wrote (or removed) <paramref name="path"/> just now. Never throws.</summary>
    public static void Mine(string path)
    {
        try
        {
            Marked[Path.GetFullPath(path)] = Stamp(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Unmarked: its hint wakes one look, which is the cost of a missed mark and never a wrong answer.
        }
    }

    /// <summary>Whether <paramref name="stamp"/> is the stamp of what this process left at <paramref name="path"/>.</summary>
    internal static bool Is(string path, string stamp) =>
        Marked.TryGetValue(path, out var mine) && string.Equals(mine, stamp, StringComparison.Ordinal);

    /// <summary>A file's stamp: its length and its last write, or <c>-</c> where it is absent. What moved it moves this.</summary>
    internal static string Stamp(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? $"{info.Length}:{info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)}" : "-";
    }
}
