using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Service.Tests;

/// <summary>
/// FOUNDATION1c, census B (D212; the foundation design §10, row c), the service's half: every line of the service's Core, Http and Mcp
/// sources that reads a record's <c>Serves</c>, <c>Opinion</c>, <c>Ask</c>, <c>Piece</c>, <c>HelpMode</c> or the Ask Daoris repository to test
/// its presence or identity is counted per file, each file named in <c>fixtures/participant-work-readers.json</c> beside this suite, which the
/// driver's and the modules' <c>ParticipantWorkReadersTests</c> read over their own sources. A new line, or a new file, fails until it is
/// listed with the row that moves it. A copy of the driver tests' scan: the test projects share no code.
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

    private static readonly string[] Projects = ["Daoris.Service.Core", "Daoris.Service.Http", "Daoris.Service.Mcp"];

    internal static int Counted(string source) =>
        source.Split('\n').Count(raw => !raw.TrimStart().StartsWith("//", StringComparison.Ordinal) && Reads.Any(read => read.IsMatch(Trailing.Replace(raw, ""))));

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("no workspace root above the test binaries");
    }

    /// <summary>Each source of the three projects with the lines it reads, the rule's own file left out, by its path from the services' folder, each project named by its last word.</summary>
    private static SortedDictionary<string, int> Scan()
    {
        var services = Path.Combine(Root(), "src", "Daoris.Service");
        var found = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var project in Projects)
        {
            var folder = Path.Combine(services, project);
            foreach (var path in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/');
                if (relative.Split('/')[0] is "bin" or "obj" || Path.GetFileName(path) == "ParticipantWork.cs") continue;
                if (Counted(File.ReadAllText(path)) is > 0 and var lines) found[project["Daoris.Service.".Length..] + "/" + relative] = lines;
            }
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
            Root(), "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "participant-work-readers.json")));
        return new(table.RootElement.GetProperty("service").EnumerateObject()
            .ToDictionary(each => each.Name, each => each.Value.GetProperty("lines").GetInt32()), StringComparer.Ordinal);
    }

    /// <summary>🔴 Every line of the service that reads a mark is a listed file's, line for line: a new reader fails until it is named.</summary>
    [Fact]
    public void Every_reader_of_a_mark_in_the_service_is_listed_file_for_file_and_line_for_line()
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

        Assert.Equal(1, Counted("if (session.Serves is not null) { }"));
        Assert.Equal(1, Counted("if (SessionLedger.IsHelp(session)) { }\n// record.Opinion is null\nvar quiet = 1; // record.Serves is not null"));
        Assert.Equal(0, Counted("var seen = record.Serves;"));
        Assert.Equal([$"{known} reads {scanned[known] + 1} line(s) and the table lists {scanned[known]}"],
            Differences(new SortedDictionary<string, int>(scanned) { [known] = scanned[known] + 1 }, listed));
        Assert.Equal(["Core/Sessions/Planted.cs reads 1 line(s) and is not listed"],
            Differences(new SortedDictionary<string, int>(scanned) { ["Core/Sessions/Planted.cs"] = 1 }, listed));
    }
}
