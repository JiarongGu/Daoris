using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The run's one scan of the sources: each file is walked and read once, and no test walks or reads past it.</summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class SourceTextsTests
{
    [Fact]
    public void Two_censuses_in_one_run_read_each_driver_file_once()
    {
        var first = SourceCensus.Driver();
        var second = SourceCensus.Driver();
        var third = SourceFiles.All(SourceFiles.DriverProject, "*.cs").Select(SourceTexts.Of).ToList();

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first.Count, third.Count);
        Assert.Same(first[0].Source, second[0].Source);
        var driver = SourceFiles.All(SourceFiles.DriverProject, "*.cs").Select(Path.GetFullPath).ToList();
        Assert.NotEmpty(driver);
        Assert.All(driver, path => Assert.Equal(1, SourceTexts.DiskReads[path]));
    }

    [Fact]
    public void A_folder_is_walked_once_and_a_text_is_stripped_once()
    {
        Assert.Same(SourceFiles.All(SourceFiles.DriverProject, "*.cs"), SourceFiles.All(SourceFiles.DriverProject, "*.cs"));
        var source = SourceTexts.Of(SourceFiles.All(SourceFiles.DriverProject, "*.cs").First());
        Assert.Same(SourceTexts.Code(source), SourceTexts.Code(source));
    }

    [Fact]
    public void A_walk_of_the_sources_or_a_read_of_a_found_source_outside_the_shared_scan_is_named()
    {
        const string Planted = """
            var all = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories);
            var one = Directory.EnumerateFiles(Tests, suite + ".cs", SearchOption.AllDirectories)
                .Single();
            var text = File.ReadAllText(SourceFiles.Driver("Landing.cs"));
            var listed = Directory.GetFiles(_home, "*.json");
            var home = Directory.EnumerateFiles(home, "*", SearchOption.AllDirectories);
            var shared = SourceFiles.Under(root, "*.cs").Select(SourceTexts.Of);
            var found = SourceTexts.Of(SourceFiles.Driver("Landing.cs"));
            var aside = Directory.EnumerateFiles(site, "mine.md", SearchOption.AllDirectories).Single();
            var named = System.IO.File.ReadAllText(Daoris.Desktop.Driver.Tests.SourceFiles.Driver("Landing.cs"));
            """;

        Assert.Equal(["A.cs:1", "A.cs:2", "A.cs:4", "A.cs:10"], OwnWalks([("A.cs", Planted)]));
        Assert.Empty(OwnWalks([("SourceFiles.cs", Planted)]));
        // A file another test project compiles too walks as that project can, which has no shared scan yet.
        Assert.Empty(OwnWalks([("Shared.cs", Planted)], new HashSet<string>(["Shared.cs"])));
    }

    [Fact]
    public void No_test_of_this_project_walks_the_sources_or_reads_a_found_source_itself()
    {
        var files = SourceFiles.All(SourceFiles.DriverTestsProject, "*.cs").Select(path => (Path.GetFileName(path), SourceTexts.Of(path)));

        var linked = Directory.EnumerateDirectories(Path.Combine(WorkspaceRoot.Folder, "src"))
            .SelectMany(top => Directory.EnumerateDirectories(top).Prepend(top))
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj"))
            .SelectMany(project => Linked().Matches(SourceTexts.Of(project)).Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("NoConsoleStarts.cs", linked);
        Assert.Empty(OwnWalks(files, linked));
    }

    /// <summary>A test's own walk of the sources (a wildcard or built name, at any depth) or read of a found source, as <c>file:line</c>.</summary>
    internal static IReadOnlyList<string> OwnWalks(IEnumerable<(string File, string Text)> files, IReadOnlySet<string>? linked = null)
    {
        var found = new List<string>();
        foreach (var (file, text) in files)
        {
            if (file is "SourceFiles.cs" or "SourceTextsTests.cs" || linked?.Contains(file) == true) continue;
            var code = SourceTexts.Code(text);
            foreach (Match walk in Walk().Matches(code))
            {
                var arguments = Arguments(code, walk.Index + walk.Length);
                if (arguments.Contains("SearchOption.AllDirectories", StringComparison.Ordinal) && SourcePattern().IsMatch(arguments)) found.Add(At(file, code, walk.Index));
            }

            foreach (Match read in FoundRead().Matches(code)) found.Add(At(file, code, read.Index));
        }

        return found;
    }

    private static string Arguments(string code, int open)
    {
        var depth = 1;
        var at = open;
        for (; at < code.Length && depth > 0; at++) depth += code[at] switch { '(' => 1, ')' => -1, _ => 0 };
        return code[open..at];
    }

    private static string At(string file, string code, int index) => $"{file}:{code.AsSpan(0, index).Count('\n') + 1}";

    [GeneratedRegex(@"\bDirectory\.(?:Enumerate|Get)Files\(")]
    private static partial Regex Walk();

    [GeneratedRegex(@"""[^""]*\*[^""]*\.(?:cs|ts\*?|tsx|mjs|md|json|\*)""|\+\s*""\.(?:cs|ts|tsx|mjs|md|json)""")]
    private static partial Regex SourcePattern();

    [GeneratedRegex(@"\bFile\.Read(?:AllText|AllLines|Lines)\(\s*(?:[\w.]+\.)?SourceFiles\.")]
    private static partial Regex FoundRead();

    [GeneratedRegex(@"Include=""[^""]*Daoris\.Desktop\.Driver\.Tests\\(?:[^""\\]+\\)*([^""\\]+\.cs)""")]
    private static partial Regex Linked();
}
