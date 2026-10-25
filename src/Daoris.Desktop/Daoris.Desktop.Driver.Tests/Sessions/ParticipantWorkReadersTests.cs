using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1c, census B (D212; the foundation design §10, row c): every line outside <c>ParticipantWork.Of</c> that reads a record's
/// <c>Serves</c>, <c>Opinion</c>, <c>Ask</c>, <c>Piece</c>, <c>HelpMode</c> or the Ask Daoris repository to test its presence or identity is
/// counted per source file of the driver library, each file named in <c>fixtures/participant-work-readers.json</c> among the service's
/// tests with what it reads and the row that moves it, and a new line, or a new file, fails until it is listed there. The shell's
/// modules' census and the service's read the same table over their own sources.
/// </summary>
public sealed class ParticipantWorkReadersTests
{
    /// <summary>The shapes a line takes that tests a mark's presence or the Ask Daoris repository's identity.</summary>
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

    /// <summary>The lines of a source that read a mark: its comment lines and its lines' trailing comments are not counted.</summary>
    internal static int Counted(string source) =>
        source.Split('\n').Count(raw => !raw.TrimStart().StartsWith("//", StringComparison.Ordinal) && Reads.Any(read => read.IsMatch(Trailing.Replace(raw, ""))));

    /// <summary>Each source of a project's folder with the lines it reads, the rule's own file left out, by its path from the folder.</summary>
    private static SortedDictionary<string, int> Scan(string project)
    {
        var folder = SourceFiles.Project(project);
        var found = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var path in SourceFiles.All(project, "*.cs").Where(path => Path.GetFileName(path) != "ParticipantWork.cs"))
        {
            if (Counted(File.ReadAllText(path)) is > 0 and var lines) found[Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/')] = lines;
        }

        return found;
    }

    /// <summary>What differs between the files a scan found and the files the table lists, each as a sentence.</summary>
    internal static List<string> Differences(IReadOnlyDictionary<string, int> scanned, IReadOnlyDictionary<string, int> listed) =>
    [
        .. scanned.Where(each => !listed.ContainsKey(each.Key)).Select(each => $"{each.Key} reads {each.Value} line(s) and is not listed"),
        .. listed.Where(each => !scanned.ContainsKey(each.Key)).Select(each => $"{each.Key} is listed with {each.Value} line(s) and reads none now"),
        .. scanned.Where(each => listed.TryGetValue(each.Key, out var lines) && lines != each.Value)
            .Select(each => $"{each.Key} reads {each.Value} line(s) and the table lists {listed[each.Key]}"),
    ];

    private static JsonElement Table(string section)
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "participant-work-readers.json")));
        return table.RootElement.GetProperty(section).Clone();
    }

    private static SortedDictionary<string, int> Listed(string section) =>
        new(Table(section).EnumerateObject().ToDictionary(each => each.Name, each => each.Value.GetProperty("lines").GetInt32()), StringComparer.Ordinal);

    /// <summary>🔴 Every line of the driver library that reads a mark is a listed file's, line for line: a new reader fails until it is named.</summary>
    [Fact]
    public void Every_reader_of_a_mark_in_the_driver_is_listed_file_for_file_and_line_for_line()
    {
        Assert.Empty(Differences(Scan(SourceFiles.DriverProject), Listed("driver")));
    }

    /// <summary>The census fails on a reader it does not list: a planted line in a listed file, and a planted file, each read as a difference.</summary>
    [Fact]
    public void A_planted_reader_fails_the_census_in_a_listed_file_and_in_a_new_one()
    {
        var listed = Listed("driver");
        // A clean scan is the table itself, so this case holds the census's reading apart from the tree's state.
        var scanned = new SortedDictionary<string, int>(listed);
        var known = listed.First().Key;

        Assert.Equal(1, Counted("if (record.Serves is not null) { }"));
        Assert.Equal(1, Counted("var held = new { Opinion: null };\nvar quiet = 1; // record.Serves is not null\n// record.Opinion is null"));
        Assert.Equal(0, Counted("var seen = record.Serves;"));
        Assert.Equal([$"{known} reads {scanned[known] + 1} line(s) and the table lists {scanned[known]}"],
            Differences(new SortedDictionary<string, int>(scanned) { [known] = scanned[known] + 1 }, listed));
        Assert.Equal(["Loop/Planted.cs reads 1 line(s) and is not listed"],
            Differences(new SortedDictionary<string, int>(scanned) { ["Loop/Planted.cs"] = 1 }, listed));
    }

    /// <summary>Each listed file says what it reads, a file that tells a kind of session naming the row that moves it or none, and no row is named the table does not hold.</summary>
    [Fact]
    public void Each_listed_file_says_what_it_reads_and_the_row_that_moves_it()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "participant-work-readers.json")));
        var rows = table.RootElement.GetProperty("rows").EnumerateObject().Select(row => row.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var section in new[] { "driver", "modules", "service" })
        {
            foreach (var file in table.RootElement.GetProperty(section).EnumerateObject())
            {
                var entry = file.Value;
                Assert.Contains(entry.GetProperty("reads").GetString(), new[] { "tells", "other" });
                Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("note").GetString()), $"{section}/{file.Name} says nothing of what it reads");
                if (entry.TryGetProperty("row", out var row))
                {
                    Assert.Equal("tells", entry.GetProperty("reads").GetString());
                    Assert.Contains(row.GetString(), rows);
                }
            }
        }
    }
}
