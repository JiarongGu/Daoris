using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The census's reading of source text, by name: which files a list's signal reaches, which entries still stand, and which sources write a
/// slot's folder into a record or build a path from a slot's number. Pure over text, so a planted site is tried on a string.
/// </summary>
internal static partial class PoolCensusScan
{
    /// <summary>The files where a slot's folder name is built, and why: nowhere else spells it.</summary>
    internal static IReadOnlyDictionary<string, string> MayBuildSlotFolders { get; } = new Dictionary<string, string>
    {
        ["Daoris.Desktop.Driver/Workplace/Pool/KeptTree.cs"] = "the one place a slot's folder is built from its number",
        ["Daoris.Desktop.Driver/Workplace/Pool/PoolSite.cs"] = "the pool's act names a repository's slot folder through its site",
        ["Daoris.Desktop.Driver/Workplace/TreeStrays.cs"] = "the strays pass a slot's folder over by its prefix",
    };

    /// <summary>A source without its comment lines: a cref or an example in a comment is no site.</summary>
    internal static string Code(string source) =>
        string.Join('\n', source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    /// <summary>The files whose code matches <paramref name="signal"/> and that are in no entry of <paramref name="list"/> nor excused.</summary>
    internal static IReadOnlyList<string> Unlisted(
        IEnumerable<(string File, string Source)> sources, string signal, IEnumerable<PoolReader> list, IReadOnlyDictionary<string, string> notSites)
    {
        var listed = list.Select(each => each.File).ToHashSet(StringComparer.Ordinal);
        var pattern = new Regex(signal, RegexOptions.CultureInvariant);
        return [.. sources
            .Where(each => !listed.Contains(each.File) && !notSites.ContainsKey(each.File) && pattern.IsMatch(Code(each.Source)))
            .Select(each => each.File).OrderBy(each => each, StringComparer.Ordinal)];
    }

    /// <summary>The entries whose file is not among the sources or whose code no longer matches the entry's token.</summary>
    internal static IReadOnlyList<string> Fallen(IReadOnlyDictionary<string, string> sources, IEnumerable<PoolReader> list) =>
        [.. list.Where(each => !sources.TryGetValue(each.File, out var source) || !Regex.IsMatch(Code(source), each.Token, RegexOptions.CultureInvariant))
            .Select(each => each.File + " : " + each.Token)];

    /// <summary>What a source does that the pool's rule forbids: a slot's folder built by hand, and a slot's folder written into a record.</summary>
    internal static IReadOnlyList<string> Offences(string file, string source)
    {
        var found = new List<string>();
        var lines = Code(source).Split('\n');
        var mayBuild = MayBuildSlotFolders.ContainsKey(file);
        for (var at = 0; at < lines.Length; at++)
        {
            var line = lines[at];
            if (!mayBuild && (SlotFolderMention().IsMatch(line) || (KeptPrefixLiteral().IsMatch(line) && PathWords().IsMatch(line))))
            {
                found.Add($"{file}:{at + 1} builds a path from a slot's number: {line.Trim()}");
            }

            if (SlotPathIntoRecord().IsMatch(line))
            {
                found.Add($"{file}:{at + 1} writes a slot's folder into a record: {line.Trim()}");
            }
        }

        return found;
    }

    /// <summary>A call of the one builder, or the prefix it builds with.</summary>
    [GeneratedRegex(@"\bKeptTree\.(?:FolderIn|Prefix)\b")]
    private static partial Regex SlotFolderMention();

    /// <summary>A <c>k-</c> name spelled as a literal that takes a number, or alone.</summary>
    [GeneratedRegex(@"""k-""|[$@]""[^""\n]*\bk-\{")]
    private static partial Regex KeptPrefixLiteral();

    [GeneratedRegex(@"Path\.|Combine|[Ff]older|[Tt]rees")]
    private static partial Regex PathWords();

    /// <summary>A kept slot's path on a line that sets a record's tree.</summary>
    [GeneratedRegex(@"(?i)\b(?:slot|kept\w*)\.Path\b.*(?:\bTree\s*[:=]|WorkTree|WithTree)|(?:\bTree\s*[:=]|WorkTree|WithTree).*\b(?:slot|kept\w*)\.Path\b")]
    private static partial Regex SlotPathIntoRecord();
}
