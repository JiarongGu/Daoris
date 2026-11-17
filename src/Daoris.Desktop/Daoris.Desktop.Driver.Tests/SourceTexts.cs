using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one scan of the source tree for a run: the text of a source file, read from disk once a run and shared by every census that
/// reads it (the compilations of the desktop projects, the say-door censuses, the git class censuses and the walks of the other
/// censuses ask here), so a file linked into several projects, or read by several censuses, is read once, not once for each. Each
/// read is counted by path (<see cref="DiskReads"/>) so a test can hold that no file is read twice.
/// </summary>
internal static partial class SourceTexts
{
    private static readonly ConcurrentDictionary<string, Lazy<string>> Read = new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, int> Counted = new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConditionalWeakTable<string, string> Stripped = new();

    /// <summary>The text of the file at <paramref name="path"/>, read at its first ask.</summary>
    internal static string Of(string path)
    {
        var full = Path.GetFullPath(path);
        return Read.GetOrAdd(full, key => new Lazy<string>(() =>
        {
            Counted.AddOrUpdate(key, 1, (_, count) => count + 1);
            return File.ReadAllText(key);
        })).Value;
    }

    /// <summary>How many times each file has come from disk this run, by full path: one for every file a census reads.</summary>
    internal static IReadOnlyDictionary<string, int> DiskReads => Counted;

    /// <summary>
    /// The text of <paramref name="source"/> with its line comments taken out and its line ends as <c>\n</c>, made once for each text:
    /// every census that matches code runs over it, and the same driver text is matched by many.
    /// </summary>
    internal static string Code(string source) => Stripped.GetValue(source, text => LineComment().Replace(text.Replace("\r\n", "\n"), ""));

    [GeneratedRegex(@"//[^\n]*")]
    private static partial Regex LineComment();
}
