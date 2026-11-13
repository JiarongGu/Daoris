using System.Collections.Concurrent;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The text of a source file, read from disk once a run and shared by every census that reads it: the compilations of the desktop
/// projects and the say-door censuses ask here, so a file linked into several projects is read once, not once for each.
/// </summary>
internal static class SourceTexts
{
    private static readonly ConcurrentDictionary<string, string> Read = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The text of the file at <paramref name="path"/>, read at its first ask.</summary>
    internal static string Of(string path) => Read.GetOrAdd(Path.GetFullPath(path), File.ReadAllText);
}
