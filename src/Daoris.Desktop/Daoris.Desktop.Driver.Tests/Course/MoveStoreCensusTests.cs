using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-i part 1 (D212; the foundation design §10's row 1f-i): the move record is read by nothing. No production source names a type
/// the move store's two files declare, but the one wiring through which the workplace's delete takes a record's moves with it: rows f-ii to
/// f-vi, h and i adopt it, so until then Daoris works exactly as before. A source that names it is a row's adoption, and moves this check.
/// </summary>
/// <remarks>Not a twin of <see cref="CourseVocabularyTests"/>' check of the course's vocabulary (<c>twins.md</c>): it shares that check's shape and reads other files.</remarks>
public sealed class MoveStoreCensusTests
{
    private static string Driver => Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");

    /// <summary>The move store's own files, which declare its types.</summary>
    private static readonly string[] Own = ["Course/MoveStore.cs", "Course/MoveRecords.cs"];

    /// <summary>The one production reader allowed: the workplace's delete port, opened over a home.</summary>
    private const string Wiring = "Course/WorkplaceMoveRecordsWiring.cs";

    private static string Relative(string path) => Path.GetRelativePath(Driver, path).Replace('\\', '/');

    [Fact]
    public void No_production_source_names_the_move_store_but_the_wiring_of_the_workplace_s_delete()
    {
        var names = Own
            .SelectMany(file => Regex.Matches(
                    CourseCensusTests.WithoutComments(File.ReadAllText(Path.Combine(Driver, file))),
                    @"^public\s+(?:abstract\s+|sealed\s+|static\s+|readonly\s+)*(?:record(?:\s+struct)?|enum|class|struct)\s+(\w+)",
                    RegexOptions.Multiline)
                .Select(match => match.Groups[1].Value))
            .ToList();
        Assert.True(names.Count >= 10, $"found {names.Count} types: the pattern has drifted");
        Assert.Contains("MoveStore", names);

        var readers = new List<string>();
        foreach (var path in CourseCensusTests.ProductionFiles())
        {
            if (Own.Contains(Relative(path))) continue;
            var text = CourseCensusTests.WithoutComments(File.ReadAllText(path));
            readers.AddRange(names.Where(name => Regex.IsMatch(text, $@"\b{name}\b")).Select(name => $"{Relative(path)} names {name}"));
        }

        Assert.Equal([$"{Wiring} names MoveStore"], readers);
    }

    [Fact]
    public void The_delete_reaches_a_record_s_moves_through_the_workplace_s_port_which_names_no_course_type()
    {
        Assert.IsType<WorkplaceMoveRecordStoreOperations>(WorkplaceMoveRecords.Current);
        var home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "move-store-census", Guid.NewGuid().ToString("N")[..8]);
        var opened = WorkplaceMoveRecords.Current.Open(home);
        Assert.IsType<MoveStore>(opened);
        Assert.Equal(DesktopStore.LocationIn(home), opened.StoreLocation);
        Assert.Empty(opened.MovesOf(["s1"]));
        Assert.Empty(opened.ForgetMoves(["s1"]));
        Assert.False(Directory.Exists(home));

        var port = File.ReadAllText(Path.Combine(Driver, "Workplace", "WorkplaceMoveRecords.cs"));
        Assert.DoesNotMatch(@"\bMove(Store|Record|Claim|Decision)\b", CourseCensusTests.WithoutComments(port));
        Assert.Contains("WorkplaceMoveRecords.Current.Open(home)", File.ReadAllText(Path.Combine(Driver, "Workplace", "SessionHomeFiles.cs")));
    }
}
