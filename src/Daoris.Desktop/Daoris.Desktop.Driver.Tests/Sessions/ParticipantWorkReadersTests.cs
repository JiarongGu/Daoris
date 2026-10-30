using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1c, census B (D212; the foundation design §10, row c): every line outside <c>ParticipantWork.Of</c> that reads a record's
/// <c>Serves</c>, <c>Opinion</c>, <c>Ask</c>, <c>Piece</c>, <c>HelpMode</c> or the Ask Daoris repository to test its presence or identity is
/// found per source file of the driver library, each file named in <c>fixtures/participant-work-readers.json</c> among the service's
/// tests with what it reads and the row that moves it. A file that reads and is not listed fails (the new reader the census exists to
/// catch), and so does a listed file that reads none now; how many lines a listed file reads is not pinned (CENSUSRULES1). The shell's
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
        new(@"(?:HelpRoom|KnowledgeRoomIdentity)\.Repository\b"),
        new(@"\bHelpRepository\b"),
        new(@"\bIsHelp\("),
    ];

    private static readonly Regex Trailing = new(@"\s//.*$");

    /// <summary>The lines of a source that read a mark: its comment lines and its lines' trailing comments are not counted.</summary>
    internal static int Counted(string source) =>
        source.Split('\n').Count(raw => !raw.TrimStart().StartsWith("//", StringComparison.Ordinal) && Reads.Any(read => read.IsMatch(Trailing.Replace(raw, ""))));

    /// <summary>The files, by path, of the sources that read a mark: how many lines each reads is not the rule's (CENSUSRULES1).</summary>
    internal static SortedSet<string> Reading(IEnumerable<(string File, string Source)> sources) =>
        new(sources.Where(each => Counted(each.Source) > 0).Select(each => each.File), StringComparer.Ordinal);

    /// <summary>Each source of a project's folder that reads a mark, the rule's own file left out, by its path from the folder.</summary>
    private static SortedSet<string> Scan(string project)
    {
        var folder = SourceFiles.Project(project);
        return Reading(SourceFiles.All(project, "*.cs").Where(path => Path.GetFileName(path) != "ParticipantWork.cs")
            .Select(path => (Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(path))));
    }

    /// <summary>What differs between the files a scan found and the files the table lists, each as a sentence.</summary>
    internal static List<string> Differences(IReadOnlyCollection<string> scanned, IReadOnlyCollection<string> listed) =>
    [
        .. scanned.Where(file => !listed.Contains(file)).Select(file => $"{file} reads a mark and is not listed"),
        .. listed.Where(file => !scanned.Contains(file)).Select(file => $"{file} is listed and reads none now"),
    ];

    private static JsonElement Table(string section)
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "participant-work-readers.json")));
        return table.RootElement.GetProperty(section).Clone();
    }

    private static SortedSet<string> Listed(string section) => new(Table(section).EnumerateObject().Select(each => each.Name), StringComparer.Ordinal);

    /// <summary>🔴 Every file of the driver library that reads a mark is a listed file, and every listed file reads one: a new reader fails until it is named.</summary>
    [Fact]
    public void Every_file_that_reads_a_mark_in_the_driver_is_listed_and_every_listed_file_reads_one()
    {
        var differences = Differences(Scan(SourceFiles.DriverProject), Listed("driver"));
        Assert.True(differences.Count == 0, string.Join('\n', differences));
    }

    /// <summary>The census fails on a file it does not list and on a listed file that reads none now, and passes one more read in a listed file (CENSUSRULES1).</summary>
    [Fact]
    public void A_new_reading_file_fails_the_census_and_one_more_read_in_a_listed_file_passes()
    {
        var listed = Listed("driver");
        // A clean stand-in is the table itself, one read in each file, so this case holds the census's reading apart from the tree's state.
        var known = listed.First();
        var stand = listed.Select(file => (File: file, Source: "if (record.Serves is not null) { }")).ToList();

        Assert.Equal(1, Counted("if (record.Serves is not null) { }"));
        Assert.Equal(1, Counted("var held = new { Opinion: null };\nvar quiet = 1; // record.Serves is not null\n// record.Opinion is null"));
        Assert.Equal(0, Counted("var seen = record.Serves;"));
        Assert.Equal(1, Counted("if (record.Repository == KnowledgeRoomIdentity.Repository) { }"));
        Assert.Equal(0, Counted("var name = KnowledgeRoomIdentity.RepositoryName;"));
        Assert.Empty(Differences(Reading(stand), listed));
        Assert.Empty(Differences(Reading(stand.Select(each => each.File == known
            ? (each.File, each.Source + "\nif (record.Opinion is null) { }\nif (record.Piece is null) { }") : each)), listed));
        Assert.Equal(["Loop/Planted.cs reads a mark and is not listed"],
            Differences(Reading(stand.Append(("Loop/Planted.cs", "if (record.Serves is null) { }"))), listed));
        Assert.Equal([$"{known} is listed and reads none now"],
            Differences(Reading(stand.Select(each => each.File == known ? (each.File, "var seen = record.Serves;") : each)), listed));
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
