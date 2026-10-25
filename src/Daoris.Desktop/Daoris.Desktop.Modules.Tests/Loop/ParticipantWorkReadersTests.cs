using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// FOUNDATION1c, census B (D212; the foundation design §10, row c), the shell's modules' half: every line of the modules' sources that reads a
/// record's <c>Serves</c>, <c>Opinion</c>, <c>Ask</c>, <c>Piece</c>, <c>HelpMode</c> or the Ask Daoris repository to test its presence or
/// identity is counted per file, each file named in <c>participant-work-readers.json</c> among the service's tests, which the driver's
/// <c>ParticipantWorkReadersTests</c> and the service's read over their own sources. A new line, or a new file, fails until it is listed.
/// A copy of the driver tests' scan: the test projects share no code.
/// </summary>
public sealed class ParticipantWorkReadersTests
{
    private static readonly Regex[] Reads =
    [
        new(@"\b(Serves|Opinion|Piece|HelpMode)\b[^;]*(is (not )?null|is \{|is not \{|== null|!= null|IsNullOrEmpty|IsNullOrWhiteSpace|\.Length|Length:)"),
        new(@"(IsNullOrEmpty|IsNullOrWhiteSpace)\([^)]*\.(Serves|Opinion|Piece)"),
        new(@"\.Ask\b[^;]*(is (not )?null|is \{|is not \{|== null|!= null)"),
        new(@"\{\s*(Serves|Opinion|Ask|Piece|HelpMode)\s*:"),
        new(@"HelpRoom\.Repository"),
        new(@"\bHelpRepository\b"),
        new(@"\bIsHelp\("),
    ];

    private static readonly Regex Trailing = new(@"\s//.*$");

    internal static int Counted(string source) =>
        source.Split('\n').Count(raw => !raw.TrimStart().StartsWith("//", StringComparison.Ordinal) && Reads.Any(read => read.IsMatch(Trailing.Replace(raw, ""))));

    private static SortedDictionary<string, int> Scan()
    {
        var folder = SourceFiles.Project(SourceFiles.ModulesProject);
        var found = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var path in SourceFiles.All(SourceFiles.ModulesProject, "*.cs").Where(path => Path.GetFileName(path) != "ParticipantWork.cs"))
        {
            if (Counted(File.ReadAllText(path)) is > 0 and var lines) found[Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/')] = lines;
        }

        return found;
    }

    internal static List<string> Differences(IReadOnlyDictionary<string, int> scanned, IReadOnlyDictionary<string, int> listed) =>
    [
        .. scanned.Where(each => !listed.ContainsKey(each.Key)).Select(each => $"{each.Key} reads {each.Value} line(s) and is not listed"),
        .. listed.Where(each => !scanned.ContainsKey(each.Key)).Select(each => $"{each.Key} is listed with {each.Value} line(s) and reads none now"),
        .. scanned.Where(each => listed.TryGetValue(each.Key, out var lines) && lines != each.Value)
            .Select(each => $"{each.Key} reads {each.Value} line(s) and the table lists {listed[each.Key]}"),
    ];

    private static SortedDictionary<string, int> Listed()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            SourceFiles.WorkspaceFolder(), "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "participant-work-readers.json")));
        return new(table.RootElement.GetProperty("modules").EnumerateObject()
            .ToDictionary(each => each.Name, each => each.Value.GetProperty("lines").GetInt32()), StringComparer.Ordinal);
    }

    /// <summary>🔴 Every line of the modules that reads a mark is a listed file's, line for line: a new reader fails until it is named.</summary>
    [Fact]
    public void Every_reader_of_a_mark_in_the_modules_is_listed_file_for_file_and_line_for_line()
    {
        Assert.Empty(Differences(Scan(), Listed()));
    }

    /// <summary>The census fails on a reader it does not list: a planted line in a listed file, and a planted file, each read as a difference.</summary>
    [Fact]
    public void A_planted_reader_fails_the_census_in_a_listed_file_and_in_a_new_one()
    {
        var listed = Listed();
        // A clean scan is the table itself, so this case holds the census's reading apart from the tree's state.
        var scanned = new SortedDictionary<string, int>(listed);
        var known = listed.First().Key;

        Assert.Equal(1, Counted("if (record.Serves is not null) { }"));
        Assert.Equal(1, Counted("if (session.Repository == HelpRoom.Repository) { }\n// record.Opinion is null\nvar quiet = 1; // record.Serves is not null"));
        Assert.Equal(0, Counted("var seen = record.Serves;"));
        Assert.Equal([$"{known} reads {scanned[known] + 1} line(s) and the table lists {scanned[known]}"],
            Differences(new SortedDictionary<string, int>(scanned) { [known] = scanned[known] + 1 }, listed));
        Assert.Equal(["Loop/Planted.cs reads 1 line(s) and is not listed"],
            Differences(new SortedDictionary<string, int>(scanned) { ["Loop/Planted.cs"] = 1 }, listed));
    }
}
