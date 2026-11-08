using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What the git class censuses read of the driver's sources: the fields whose declared type matches, and the files that make a call,
/// each named against its census's list both ways, so a member left out and a member that no longer exists are each said.
/// </summary>
internal static partial class SourceCensus
{
    /// <summary>The driver's sources by file name, as read now.</summary>
    public static List<(string File, string Source)> Driver(Func<string, bool>? within = null) =>
        [.. SourceFiles.All(SourceFiles.DriverProject, "*.cs").Where(path => within?.Invoke(path) ?? true)
            .Select(path => (Path.GetFileName(path), File.ReadAllText(path)))];

    /// <summary>Each field of <paramref name="sources"/> whose declared type <paramref name="holds"/> matches, as <c>File Field</c>.</summary>
    public static SortedSet<string> Fields(IEnumerable<(string File, string Source)> sources, Regex holds) =>
        new(sources.SelectMany(each => Field().Matches(Code(each.Source))
                .Where(field => holds.IsMatch(field.Groups["type"].Value) && !Call().IsMatch(field.Groups["type"].Value))
                .Select(field => $"{each.File} {field.Groups["name"].Value}")),
            StringComparer.Ordinal);

    /// <summary>Each file of <paramref name="sources"/> that makes a call <paramref name="call"/> matches.</summary>
    public static SortedSet<string> Callers(IEnumerable<(string File, string Source)> sources, Regex call) =>
        new(sources.Where(each => call.IsMatch(Code(each.Source))).Select(each => each.File), StringComparer.Ordinal);

    /// <summary>What <paramref name="found"/> holds that <paramref name="listed"/> does not, and what is listed but no longer found.</summary>
    public static List<string> Problems(IReadOnlyCollection<string> found, IEnumerable<string> listed, string what)
    {
        var named = listed.ToHashSet(StringComparer.Ordinal);
        return
        [
            .. found.Where(each => !named.Contains(each)).Select(each => $"{each} {what} and is not listed"),
            .. named.Where(each => !found.Contains(each)).Order(StringComparer.Ordinal).Select(each => $"{each} is listed and {what} no longer"),
        ];
    }

    /// <summary>
    /// The census's own proof: run over the real sources with each member of <paramref name="listed"/> left out in turn, every run is red
    /// and names that member, so no listed member stands for nothing.
    /// </summary>
    public static List<string> Unproved(IReadOnlyCollection<string> found, IReadOnlyCollection<string> listed, string what) =>
        [.. listed.Where(member => !Problems(found, listed.Where(each => each != member).ToList(), what).Contains($"{member} {what} and is not listed"))];

    private static string Code(string source) => LineComment().Replace(source.Replace("\r\n", "\n"), "");

    [GeneratedRegex(@"^[ \t]*(?:(?:private|internal|public|protected|static|readonly|volatile)[ \t]+)+(?<type>[^=;{}\n]+?)[ \t]+(?<name>_?[A-Za-z]\w*)[ \t]*(?:=(?!>)|;)", RegexOptions.Multiline)]
    private static partial Regex Field();

    // A method's name followed by its parameters, where a default value would read as a field's.
    [GeneratedRegex(@"\w[ \t]*\(")]
    private static partial Regex Call();

    [GeneratedRegex(@"//[^\n]*")]
    private static partial Regex LineComment();
}
