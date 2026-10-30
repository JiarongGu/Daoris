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
        new(@"(?:HelpRoom|KnowledgeRoomIdentity)\.Repository\b"),
        new(@"\bHelpRepository\b"),
        new(@"\bIsHelp\("),
    ];

    private static readonly Regex Trailing = new(@"\s//.*$");

    internal static int Counted(string source) =>
        source.Split('\n').Count(raw => !raw.TrimStart().StartsWith("//", StringComparison.Ordinal) && Reads.Any(read => read.IsMatch(Trailing.Replace(raw, ""))));

    /// <summary>The files, by path, of the sources that read a mark: how many lines each reads is not the rule's (CENSUSRULES1).</summary>
    internal static SortedSet<string> Reading(IEnumerable<(string File, string Source)> sources) =>
        new(sources.Where(each => Counted(each.Source) > 0).Select(each => each.File), StringComparer.Ordinal);

    private static SortedSet<string> Scan()
    {
        var folder = SourceFiles.Project(SourceFiles.ModulesProject);
        return Reading(SourceFiles.All(SourceFiles.ModulesProject, "*.cs").Where(path => Path.GetFileName(path) != "ParticipantWork.cs")
            .Select(path => (Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(path))));
    }

    internal static List<string> Differences(IReadOnlyCollection<string> scanned, IReadOnlyCollection<string> listed) =>
    [
        .. scanned.Where(file => !listed.Contains(file)).Select(file => $"{file} reads a mark and is not listed"),
        .. listed.Where(file => !scanned.Contains(file)).Select(file => $"{file} is listed and reads none now"),
    ];

    private static SortedSet<string> Listed()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            SourceFiles.WorkspaceFolder(), "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "participant-work-readers.json")));
        return new(table.RootElement.GetProperty("modules").EnumerateObject().Select(each => each.Name), StringComparer.Ordinal);
    }

    /// <summary>🔴 Every file of the modules that reads a mark is listed and every listed file reads one: a new reader fails until it is named.</summary>
    [Fact]
    public void Every_file_that_reads_a_mark_in_the_modules_is_listed_and_every_listed_file_reads_one()
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
        var stand = listed.Select(file => (File: file, Source: "if (record.Serves is not null) { }")).ToList();

        Assert.Equal(1, Counted("if (record.Serves is not null) { }"));
        Assert.Equal(1, Counted("if (session.Repository == HelpRoom.Repository) { }\n// record.Opinion is null\nvar quiet = 1; // record.Serves is not null"));
        Assert.Equal(0, Counted("var seen = record.Serves;"));
        Assert.Equal(1, Counted("if (record.Repository == KnowledgeRoomIdentity.Repository) { }"));
        Assert.Empty(Differences(Reading(stand), listed));
        Assert.Empty(Differences(Reading(stand.Select(each => each.File == known
            ? (each.File, each.Source + "\nif (record.Opinion is null) { }\nif (record.Piece is null) { }") : each)), listed));
        Assert.Equal(["Loop/Planted.cs reads a mark and is not listed"],
            Differences(Reading(stand.Append(("Loop/Planted.cs", "if (record.Serves is null) { }"))), listed));
        Assert.Equal([$"{known} is listed and reads none now"],
            Differences(Reading(stand.Select(each => each.File == known ? (each.File, "var seen = record.Serves;") : each)), listed));
    }
}
