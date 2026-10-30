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
        new(@"(?:HelpRoom|KnowledgeRoomIdentity)\.Repository\b"),
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
    /// <summary>The files, by path, of the sources that read a mark: how many lines each reads is not the rule's (CENSUSRULES1).</summary>
    internal static SortedSet<string> Reading(IEnumerable<(string File, string Source)> sources) =>
        new(sources.Where(each => Counted(each.Source) > 0).Select(each => each.File), StringComparer.Ordinal);

    private static SortedSet<string> Scan()
    {
        var services = Path.Combine(Root(), "src", "Daoris.Service");
        var found = new List<(string, string)>();
        foreach (var project in Projects)
        {
            var folder = Path.Combine(services, project);
            foreach (var path in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/');
                if (relative.Split('/')[0] is "bin" or "obj" || Path.GetFileName(path) == "ParticipantWork.cs") continue;
                found.Add((project["Daoris.Service.".Length..] + "/" + relative, File.ReadAllText(path)));
            }
        }

        return Reading(found);
    }

    internal static List<string> Differences(IReadOnlyCollection<string> scanned, IReadOnlyCollection<string> listed) =>
    [
        .. scanned.Where(file => !listed.Contains(file)).Select(file => $"{file} reads a mark and is not listed"),
        .. listed.Where(file => !scanned.Contains(file)).Select(file => $"{file} is listed and reads none now"),
    ];

    private static SortedSet<string> Listed()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            Root(), "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "participant-work-readers.json")));
        return new(table.RootElement.GetProperty("service").EnumerateObject().Select(each => each.Name), StringComparer.Ordinal);
    }

    /// <summary>🔴 Every file of the service that reads a mark is listed and every listed file reads one: a new reader fails until it is named.</summary>
    [Fact]
    public void Every_file_that_reads_a_mark_in_the_service_is_listed_and_every_listed_file_reads_one()
    {
        var differences = Differences(Scan(), Listed());
        Assert.True(differences.Count == 0, string.Join('\n', differences));
    }

    /// <summary>The census fails on a file it does not list and on a listed file that reads none now, and passes one more read in a listed file (CENSUSRULES1).</summary>
    [Fact]
    public void A_new_reading_file_fails_the_census_and_one_more_read_in_a_listed_file_passes()
    {
        var listed = Listed();
        var known = listed.First();
        var stand = listed.Select(file => (File: file, Source: "if (session.Serves is not null) { }")).ToList();

        Assert.Equal(1, Counted("if (session.Serves is not null) { }"));
        Assert.Equal(1, Counted("if (SessionLedger.IsHelp(session)) { }\n// record.Opinion is null\nvar quiet = 1; // record.Serves is not null"));
        Assert.Equal(0, Counted("var seen = record.Serves;"));
        Assert.Equal(1, Counted("if (record.Repository == KnowledgeRoomIdentity.Repository) { }"));
        Assert.Empty(Differences(Reading(stand), listed));
        Assert.Empty(Differences(Reading(stand.Select(each => each.File == known
            ? (each.File, each.Source + "\nif (session.Opinion is null) { }\nif (session.Piece is null) { }") : each)), listed));
        Assert.Equal(["Core/Sessions/Planted.cs reads a mark and is not listed"],
            Differences(Reading(stand.Append(("Core/Sessions/Planted.cs", "if (session.Serves is null) { }"))), listed));
        Assert.Equal([$"{known} is listed and reads none now"],
            Differences(Reading(stand.Select(each => each.File == known ? (each.File, "var seen = record.Serves;") : each)), listed));
    }
}
