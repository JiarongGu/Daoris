using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every Process class of the two desktop test projects names what it needs with <c>[Trait(Category.Needs, Needed.X)]</c>, one trait
/// for each real dependency it starts or reaches (git, a process, a harness, a browser, the network, the filesystem). A merge reads the
/// traits to choose which dependency tests it runs; a Process class with none would be chosen by nothing.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class NeedsTraitTests
{
    private static readonly Regex Declaration = new(
        @"^(public|internal)\s+(?:(?:sealed|static|abstract|partial)\s+)*class\s+(?<name>\w+)", RegexOptions.CultureInvariant);

    /// <summary>
    /// Files over the size budget (<c>tools/size-ratchet.json</c>), which may not grow by a trait line: each is tagged when it is split
    /// (the timing census's plan splits the tick classes first), and the file leaves this list then.
    /// </summary>
    private static readonly HashSet<string> OverTheSizeBudget = new(StringComparer.Ordinal)
    {
        "AgentsCommandTests.cs", "HarnessTests.cs", "LimitWaitsProcessTests.cs", "AccountRotationTickTests.cs",
        "AnswerContinuesTickTests.cs", "ChatGoesOnProcessTests.cs", "ChatTransientProcessTests.cs", "SessionMessagesTickTests.cs",
        "PluginKitTests.cs", "OpinionResumeTickTests.cs", "RemoteSyncTests.cs", "IntakeTests.cs", "IntegrationFormGitTests.cs",
        "IntegrationPromoteGitTests.cs",
    };

    /// <summary>The Process classes of <paramref name="files"/> whose attributes carry no Needs trait.</summary>
    internal static IReadOnlyList<string> Missing(IEnumerable<(string Path, string Text)> files)
    {
        var missing = new List<string>();
        foreach (var (path, text) in files)
        {
            if (OverTheSizeBudget.Contains(System.IO.Path.GetFileName(path))) continue;
            var lines = text.Split('\n');
            for (var at = 0; at < lines.Length; at++)
            {
                if (!Declaration.IsMatch(lines[at])) continue;
                var attributes = new List<string>();
                for (var back = at - 1; back >= 0 && (lines[back].TrimStart().StartsWith('[') || lines[back].TrimStart().StartsWith("///", StringComparison.Ordinal)); back--)
                {
                    if (lines[back].TrimStart().StartsWith('[')) attributes.Add(lines[back]);
                }

                var process = attributes.Any(line => line.Contains("Category.Process", StringComparison.Ordinal));
                var needs = attributes.Any(line => line.Contains("Category.Needs", StringComparison.Ordinal));
                if (process && !needs) missing.Add($"{System.IO.Path.GetFileName(path)}: {Declaration.Match(lines[at]).Groups["name"].Value}");
            }
        }

        return [.. missing.Distinct().Order(StringComparer.Ordinal)];
    }

    [Fact]
    public void A_process_class_without_a_needs_trait_is_found_and_one_with_it_is_not()
    {
        const string Body = "\npublic sealed class XTests\n{\n}\n";

        Assert.Equal(["A.cs: XTests"], Missing([("A.cs", "[Trait(Category.Name, Category.Process)]" + Body)]));
        Assert.Empty(Missing([("B.cs", "[Trait(Category.Name, Category.Process)]\n[Trait(Category.Needs, Needed.Git)]" + Body)]));
        Assert.Empty(Missing([("C.cs", "[Trait(Category.Name, Category.Census)]" + Body)]));
    }

    [Fact]
    public void Every_process_class_of_the_two_test_projects_names_what_it_needs()
    {
        var files = new[] { SourceFiles.DriverTestsProject, SourceFiles.ModulesTestsProject }
            .SelectMany(project => SourceFiles.All(project, "*.cs"))
            .Select(path => (path, File.ReadAllText(path)));

        Assert.Empty(Missing(files));
    }
}
