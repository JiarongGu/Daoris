namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The scope a census judges (<see cref="CensusScope"/>), and the census of censuses: every class carrying the Census trait is marked
/// diff-able or whole once, and a diff-able one hands itself to the scope, so its mark is what it does.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class CensusScopeTests
{
    // A planted census: a file that names "Banned" breaks its rule, which holds of each file alone.
    private static readonly (string Path, string Text)[] Planted =
    [
        ("src/Daoris.Desktop/Daoris.Desktop.Driver/Clean.cs", "internal static class Clean;"),
        ("src/Daoris.Desktop/Daoris.Desktop.Driver/Broken.cs", "internal static class Broken { Banned b; }"),
    ];

    private static List<string> Violations(CensusScope.Judging judging, IReadOnlySet<string>? scope) =>
        [.. CensusScope.Judged(judging, Planted, each => each.Path, scope).Where(each => each.Text.Contains("Banned", StringComparison.Ordinal)).Select(each => each.Path)];

    private static HashSet<string> Scope(params string[] paths) => new(paths, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void A_diff_census_passes_on_a_scope_that_leaves_the_planted_violation_out_and_fails_on_one_that_holds_it()
    {
        Assert.Empty(Violations(CensusScope.Judging.Diff, Scope("src/Daoris.Desktop/Daoris.Desktop.Driver/Clean.cs")));
        Assert.Equal(["src/Daoris.Desktop/Daoris.Desktop.Driver/Broken.cs"], Violations(CensusScope.Judging.Diff, Scope("src/Daoris.Desktop/Daoris.Desktop.Driver/Broken.cs")));
        Assert.Equal(["src/Daoris.Desktop/Daoris.Desktop.Driver/Broken.cs"], Violations(CensusScope.Judging.Diff, null));
    }

    [Fact]
    public void A_whole_census_ignores_the_scope()
    {
        Assert.Equal(["src/Daoris.Desktop/Daoris.Desktop.Driver/Broken.cs"], Violations(CensusScope.Judging.Whole, Scope("src/Daoris.Desktop/Daoris.Desktop.Driver/Clean.cs")));
        Assert.Equal(["src/Daoris.Desktop/Daoris.Desktop.Driver/Broken.cs"], Violations(CensusScope.Judging.Whole, Scope()));
    }

    [Fact]
    public void A_full_path_is_judged_as_the_scope_holds_it_and_a_listed_member_by_its_file()
    {
        var full = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver", "Broken.cs");
        Assert.Equal([full], CensusScope.Judged(CensusScope.Judging.Diff, [full, Path.Combine(WorkspaceRoot.Folder, "Other.cs")], path => path, Scope("src/Daoris.Desktop/Daoris.Desktop.Driver/Broken.cs")));

        string[] listed = ["Broken.cs _field", "Kept.cs", "Gone.cs _old"];
        var scope = Scope("src/Daoris.Desktop/Daoris.Desktop.Driver/Sub/Broken.cs", "src/Daoris.Desktop/Daoris.Desktop.Driver/Gone.cs", "docs/Kept.cs");
        Assert.Equal(["Broken.cs _field", "Gone.cs _old"], SourceCensus.Listed(CensusScope.Judging.Diff, listed, scope));
        Assert.Equal(listed, SourceCensus.Listed(CensusScope.Judging.Whole, listed, scope));
        Assert.Equal(listed, SourceCensus.Listed(CensusScope.Judging.Diff, listed, null));
    }

    [Fact]
    public void The_scope_is_read_from_the_file_the_variable_names_and_a_named_file_that_is_missing_is_an_error()
    {
        Assert.Null(CensusScope.Load(null));
        Assert.Null(CensusScope.Load(" "));
        var folder = ScratchFolders.New("census-scope-");
        Directory.CreateDirectory(folder);
        try
        {
            var file = Path.Combine(folder, "scope.txt");
            File.WriteAllText(file, "src\\A.cs\n\n  docs/B.md \r\n");
            Assert.Equal(["docs/B.md", "src/A.cs"], CensusScope.Load(file)!.Order(StringComparer.Ordinal));
            Assert.ThrowsAny<IOException>(() => CensusScope.Load(Path.Combine(folder, "missing.txt")));
        }
        finally
        {
            ScratchFolders.Remove(folder);
        }
    }

    [Fact]
    public void The_census_of_censuses_is_red_on_an_unmarked_census_one_marked_twice_and_a_mark_that_names_none()
    {
        Assert.Equal(
            ["B is a census marked neither diff-able nor whole: list it in CensusMarks.cs", "C is marked both diff-able and whole", "D is marked and is no census"],
            CensusScope.Unmarked(["A", "B", "C"], diff: ["A", "C"], whole: ["C", "D"]));
        Assert.Empty(CensusScope.Unmarked(["A", "B"], diff: ["A"], whole: ["B"]));
        Assert.Throws<InvalidOperationException>(() => CensusScope.MarkOf("B", ["A"], ["C"]));
    }

    [Fact]
    public void Every_census_of_this_project_is_marked_once_and_each_diff_able_one_hands_itself_to_the_scope()
    {
        var censuses = CensusScope.Censuses(typeof(CensusScopeTests).Assembly).ToList();
        Assert.Equal(censuses.Count, censuses.Distinct(StringComparer.Ordinal).Count());
        var problems = CensusScope.Unmarked(censuses, CensusMarks.Diff, CensusMarks.Whole).ToList();

        var sources = SourceFiles.All(SourceFiles.DriverTestsProject, "*.cs").Select(SourceTexts.Of).ToList();
        problems.AddRange(CensusMarks.Diff
            .Where(name => !sources.Any(text => text.Contains($"class {name}", StringComparison.Ordinal) && text.Contains($"typeof({name})", StringComparison.Ordinal)))
            .Select(name => $"{name} is marked diff-able and never hands itself to the scope (typeof({name}))"));
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }
}
