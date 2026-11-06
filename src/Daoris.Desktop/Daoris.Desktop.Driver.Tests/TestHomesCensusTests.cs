using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The repository keeps no work in the OS temp folder: a test's home or scratch tree lies under <c>_fixtures/</c>, rooted by
/// <see cref="TestHomes"/>. This census reads both desktop test projects' sources and fails a file that asks the machine for its
/// temporary folder or a temporary file (the .NET calls <c>GetTempPath</c> and <c>GetTempFileName</c>) unless the file is named below
/// with the reason it must. Comments are not read.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class TestHomesCensusTests
{
    private static readonly string[] Projects = [SourceFiles.DriverTestsProject, SourceFiles.ModulesTestsProject];

    private static readonly Regex Asks = new(@"\bGetTemp(?:Path|FileName)\s*\(");

    private static readonly Regex Comments = new(@"//[^\n]*");

    /// <summary>Sources that ask for the temporary folder on purpose, each with why it is no test home.</summary>
    private static readonly Dictionary<string, string> Kept = new(StringComparer.Ordinal)
    {
        ["Agents/ScratchSignInsTests.cs"] = "pins the product's own rule that the OS temp folder is scratch to a sign-in, from paths it never creates",
        ["Workplace/SessionTreesTests.cs"] = "needs a folder no repository contains, which nothing under the checkout's _fixtures is: git walks up into the checkout's",
        ["Workplace/WorkingTreeProvenanceTests.cs"] = "needs a folder no repository contains, which nothing under the checkout's _fixtures is: git walks up into the checkout's",
        ["Loop/BackgroundWaitJudgementTests.cs"] = "needs a folder no repository contains, which nothing under the checkout's _fixtures is: git walks up into the checkout's",
        ["Service/ServiceHostLocatorTests.cs"] = "needs a folder no workspace lies above, which nothing under the checkout's _fixtures is: the locator walks up to the checkout's manifest",
        ["Knowledge/KnowledgeConnectorTests.cs"] = "needs a folder no workspace lies above, which nothing under the checkout's _fixtures is: the locator walks up to the checkout's manifest",
        ["TestGit.cs"] = "stops a repository search at the temp folder, as the product's search does, and creates nothing there",
    };

    internal static int Asked(string source) => Asks.Matches(Comments.Replace(source.Replace("\r\n", "\n"), "")).Count;

    private static SortedDictionary<string, int> Scan()
    {
        var found = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var project in Projects)
        {
            var folder = SourceFiles.Project(project);
            foreach (var path in SourceFiles.All(project, "*.cs").Where(path => Path.GetFileName(path) != nameof(TestHomesCensusTests) + ".cs"))
            {
                var count = Asked(File.ReadAllText(path));
                if (count > 0) found[project + "/" + Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/')] = count;
            }
        }

        return found;
    }

    private static string Name(string file) => file[(file.IndexOf('/', StringComparison.Ordinal) + 1)..];

    [Fact]
    public void No_test_roots_a_home_under_the_OS_temp_folder_but_the_sources_named_here()
    {
        var rooted = Scan().Keys.Where(file => !Kept.ContainsKey(Name(file))).ToList();

        Assert.True(rooted.Count == 0, $"roots scratch under the OS temp folder, not TestHomes.Root, in: {string.Join(", ", rooted)}");
    }

    [Fact]
    public void A_source_kept_here_still_asks_for_the_temp_folder()
    {
        var asking = Scan().Keys.Select(Name).ToHashSet();
        var stale = Kept.Keys.Where(file => !asking.Contains(file)).ToList();

        Assert.True(stale.Count == 0, $"named here but no longer asking for the temp folder, so delete its row: {string.Join(", ", stale)}");
    }

    [Fact]
    public void The_census_sees_the_calls()
    {
        Assert.Equal(1, Asked("var home = Path.GetTempPath();"));
        Assert.Equal(1, Asked("var file = Path.GetTempFileName();"));
        Assert.Equal(0, Asked("// Path.GetTempPath() is no longer used\nvar home = TestHomes.Root;"));
    }
}
