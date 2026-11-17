namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The modules' census of censuses: every class of this project carrying the Census trait is marked diff-able or whole once
/// (<c>CensusMarks.cs</c>), and a diff-able one hands itself to the scope. The scope's own cases are the driver's <c>CensusScopeTests</c>.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class CensusMarksTests
{
    [Fact]
    public void Every_census_of_this_project_is_marked_once_and_each_diff_able_one_hands_itself_to_the_scope()
    {
        var problems = CensusScope.Unmarked(CensusScope.Censuses(typeof(CensusMarksTests).Assembly), CensusScope.Diff, CensusScope.Whole).ToList();
        var sources = SourceFiles.All(SourceFiles.ModulesTestsProject, "*.cs").Select(File.ReadAllText).ToList();
        problems.AddRange(CensusScope.Diff
            .Where(name => !sources.Any(text => text.Contains($"class {name}", StringComparison.Ordinal) && text.Contains($"typeof({name})", StringComparison.Ordinal)))
            .Select(name => $"{name} is marked diff-able and never hands itself to the scope (typeof({name}))"));
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }
}
