using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every test class that walks a project's sources by text (<c>SourceFiles.All</c>, <c>SourceFiles.Project</c>, a scan for
/// <c>*.cs</c>) carries <c>[Trait(Category.Name, Category.Census)]</c>, so the <c>censuses</c> gate (<c>--filter Category=Census</c>) runs every
/// census in a minute. A Process class is the Process half's, and a class with no test reads nothing the run could refuse.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class CensusTraitTests
{
    private static readonly Regex Reads = new(
        @"SourceFiles\.All\(|SourceFiles\.Project\(|(Enumerate|Get)Files\([^;]*""\*\.(cs|ts|tsx|mjs)""", RegexOptions.CultureInvariant);

    private static readonly Regex Declaration = new(
        @"^(public|internal)\s+(?:(?:sealed|static|abstract|partial)\s+)*class\s+(?<name>\w+)", RegexOptions.CultureInvariant);

    /// <summary>The classes of <paramref name="files"/> that read sources by text and carry no Census trait on any part of them.</summary>
    internal static IReadOnlyList<string> Missing(IEnumerable<(string Path, string Text)> files)
    {
        var carrying = new HashSet<string>(StringComparer.Ordinal);
        var reading = new List<(string Path, string Class)>();
        foreach (var (path, text) in files)
        {
            if (path.EndsWith("CensusTraitTests.cs", StringComparison.Ordinal) || path.EndsWith("SourceFiles.cs", StringComparison.Ordinal)) continue;
            var lines = text.Split('\n');
            var heads = Enumerable.Range(0, lines.Length).Where(index => Declaration.IsMatch(lines[index])).ToList();
            for (var at = 0; at < heads.Count; at++)
            {
                var name = Declaration.Match(lines[heads[at]]).Groups["name"].Value;
                var attributes = new List<string>();
                for (var back = heads[at] - 1; back >= 0 && (lines[back].TrimStart().StartsWith('[') || lines[back].TrimStart().StartsWith("///", StringComparison.Ordinal) || lines[back].Trim().Length == 0); back--)
                {
                    attributes.Add(lines[back]);
                }

                if (attributes.Any(line => line.Contains("Category.Census", StringComparison.Ordinal))) carrying.Add(name);
                var segment = string.Join('\n', lines.Skip(heads[at]).Take((at + 1 < heads.Count ? heads[at + 1] : lines.Length) - heads[at]));
                var test = segment.Contains("[Fact", StringComparison.Ordinal) || segment.Contains("[Theory", StringComparison.Ordinal);
                var process = attributes.Any(line => line.Contains("Category.Process", StringComparison.Ordinal));
                if (test && !process && (name.Contains("Census", StringComparison.Ordinal) || Reads.IsMatch(segment))) reading.Add((path, name));
            }
        }

        return [.. reading.Where(each => !carrying.Contains(each.Class)).Select(each => $"{Path.GetFileName(each.Path)}: {each.Class}").Distinct().Order(StringComparer.Ordinal)];
    }

    [Fact]
    public void A_class_that_walks_sources_must_carry_the_trait_a_process_class_or_one_without_tests_need_not()
    {
        const string Walks = "{\n    [Fact]\n    public void T() => SourceFiles.All(\"P\", \"*.cs\");\n}\n";

        Assert.Equal(["A.cs: ATests"], Missing([("A.cs", "public sealed class ATests\n" + Walks)]));
        Assert.Empty(Missing([("B.cs", "[Trait(Category.Name, Category.Census)]\npublic sealed class BTests\n" + Walks)]));
        Assert.Empty(Missing([("C.cs", "[Trait(Category.Name, Category.Process)]\npublic sealed class CTests\n" + Walks)]));
        Assert.Empty(Missing([("D.cs", "public static class DHelper\n{\n    public static object Go() => SourceFiles.All(\"P\", \"*.cs\");\n}\n")]));
        Assert.Empty(Missing([("E.cs", "public sealed class ETests\n{\n    [Fact]\n    public void T() => SourceFiles.Driver(\"X.cs\");\n}\n")]));
    }

    [Fact]
    public void A_partial_class_carries_the_trait_on_any_of_its_parts()
    {
        const string Walks = "{\n    [Fact]\n    public void T() => SourceFiles.All(\"P\", \"*.cs\");\n}\n";

        Assert.Empty(Missing([
            ("F.cs", "[Trait(Category.Name, Category.Census)]\npublic sealed partial class FTests\n{\n}\n"),
            ("F2.cs", "public sealed partial class FTests\n" + Walks),
        ]));
    }

    [Fact]
    public void Every_class_of_the_two_test_projects_that_walks_sources_carries_the_trait()
    {
        var files = new[] { SourceFiles.DriverTestsProject, SourceFiles.ModulesTestsProject }
            .SelectMany(project => SourceFiles.All(project, "*.cs"))
            .Select(path => (path, File.ReadAllText(path)));

        Assert.Empty(Missing(files));
    }
}
