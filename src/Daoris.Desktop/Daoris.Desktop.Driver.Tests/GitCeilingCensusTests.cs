using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITISOLATE1 (the FIX-LOG entry of that name): a test's git runs capped at its fixture, so a fixture folder that is no repository
/// never climbs into the checkout's, whose config every worktree shares. The helpers cap each start (<see cref="TestGit.Ceiling"/>,
/// <see cref="TestGit.Past"/>); this census reads both desktop test projects' sources and fails a file that builds a git start
/// (<c>new ProcessStartInfo("git")</c>, <c>FileName = "git"</c>, <c>WorkingTree.GitStart</c>) and names none of <c>TestGit.Past</c>,
/// <c>TestGit.Ceiling</c>, <c>GIT_CEILING_DIRECTORIES</c> or <c>--git-dir</c>. A start that reaches no repository says so below.
/// </summary>
public sealed class GitCeilingCensusTests
{
    private static readonly string[] Projects = [SourceFiles.DriverTestsProject, "Daoris.Desktop.Modules.Tests"];

    private static readonly Regex Starts = new(@"ProcessStartInfo\s*\(\s*""git""|\bFileName\s*=\s*""git""|\bGitStart\s*\(");

    private static readonly Regex Caps = new(@"\bTestGit\s*\.\s*(?:Past|Ceiling)\s*\(|GIT_CEILING_DIRECTORIES|--git-dir");

    private static readonly Regex Comments = new(@"//[^\n]*");

    /// <summary>
    /// The git starts a source builds when it caps none: how many, or zero where it has none or names a cap. Comments are not read.
    /// </summary>
    internal static int Uncapped(string source)
    {
        var code = Comments.Replace(source.Replace("\r\n", "\n"), "");
        return Caps.IsMatch(code) ? 0 : Starts.Matches(code).Count;
    }

    /// <summary>Sources that build a git start which reaches no repository, each with what it does instead.</summary>
    private static readonly Dictionary<string, string> Reaches = new(StringComparer.Ordinal)
    {
        ["Install/ToolsChildrenTests.cs"] = "reads the start info GitStart makes for a tool record and starts nothing",
        ["Install/ToolsChildProcessTests.cs"] = "a stub git file answers --version through a node script, in a folder with no repository",
        ["Loop/LookWaitTests.cs"] = "the start goes to a stand-in starter that starts nothing",
        ["Participants/RunningSessionsTests.cs"] = "the start goes to a stand-in starter that starts nothing",
        ["Bridge/ReleaseStandIn.cs"] = "a stand-in process that asks git for --version and waits for a file, in no repository",
        ["Roles/HolderKeyStripTests.cs"] = "builds a start info to read its variables and starts nothing",
    };

    private static SortedDictionary<string, int> Scan()
    {
        var found = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var project in Projects)
        {
            var folder = SourceFiles.Project(project);
            foreach (var path in SourceFiles.All(project, "*.cs").Where(path => Path.GetFileName(path) is not (nameof(GitCeilingCensusTests) + ".cs" or nameof(TestGitCeilingTests) + ".cs")))
            {
                var count = Uncapped(File.ReadAllText(path));
                if (count > 0) found[project + "/" + Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/')] = count;
            }
        }

        return found;
    }

    [Fact]
    public void Every_git_start_in_the_tests_is_capped_at_its_fixture_or_named_here_as_reaching_no_repository()
    {
        var uncapped = Scan().Keys.Where(file => !Reaches.ContainsKey(file[(file.IndexOf('/', StringComparison.Ordinal) + 1)..])).ToList();

        Assert.True(uncapped.Count == 0, $"git started with no ceiling (TestGit.Past or TestGit.Ceiling) or --git-dir in: {string.Join(", ", uncapped)}");
    }

    [Fact]
    public void A_source_named_as_reaching_no_repository_still_has_such_a_start()
    {
        var found = Scan().Keys.Select(file => file[(file.IndexOf('/', StringComparison.Ordinal) + 1)..]).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(Reaches.Keys.Where(file => !found.Contains(file)));
    }

    [Theory]
    [InlineData("var info = new ProcessStartInfo(\"git\") { WorkingDirectory = folder };", 1)]
    [InlineData("var info = new ProcessStartInfo(\"git\"); Process.Start(info);\nvar again = new ProcessStartInfo(\"git\", \"status\");", 2)]
    [InlineData("var (info, refusal) = WorkingTree.GitStart(root, arguments, home);", 1)]
    [InlineData("var info = new ProcessStartInfo { FileName = \"git\" };", 1)]
    [InlineData("var info = new ProcessStartInfo(\"git\"); Process.Start(TestGit.Past(info));", 0)]
    [InlineData("Process.Start(TestGit.Ceiling(WorkingTree.GitStart(root, a, h).Info));", 0)]
    [InlineData("info.Environment[\"GIT_CEILING_DIRECTORIES\"] = root; new ProcessStartInfo(\"git\");", 0)]
    [InlineData("new ProcessStartInfo(\"git\") { ArgumentList = { \"--git-dir\", \"x\" } };", 0)]
    [InlineData("// new ProcessStartInfo(\"git\")\nvar nothing = 1;", 0)]
    [InlineData("var info = new ProcessStartInfo(\"node\");", 0)]
    public void A_start_that_names_no_cap_is_counted_and_one_that_names_a_cap_or_is_a_comment_is_not(string source, int uncapped) =>
        Assert.Equal(uncapped, Uncapped(source));
}
