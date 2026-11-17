using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The word <c>you</c> is read in one place, which sets the first holder; no runtime source compares against it, nor does a reader of a
/// version's steps (<c>Workflows/Domain</c>: the named steps' reading, gate and diff, the chart's and a preset's words), each asking that
/// place for the holder a step's words cast.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class HolderWordCensusTests
{
    /// <summary>The named steps' reading, gate and diff, each a file of <c>Workflows/Domain</c>.</summary>
    private static readonly string[] NamedReaders = ["NamedStepReading.cs", "WorkflowGate.cs", "WorkflowDiff.cs"];

    [Fact]
    public void No_runtime_source_branches_on_you_beyond_the_holder_it_sets()
    {
        var driver = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");
        var branching = new List<string>();
        foreach (var folder in new[] { "Workflows/Kinds", "Workflows/Nodes", "Workflows/Cast", "Workflows/Domain", "Roles" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(driver, folder), "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "HolderWords.cs") continue;
                if (Compared().IsMatch(File.ReadAllText(file))) branching.Add(Path.GetRelativePath(driver, file));
            }
        }

        Assert.Empty(branching);
        Assert.Contains("HolderWords.First", File.ReadAllText(Path.Combine(driver, "Workflows", "Cast", "AttemptTenures.cs")), StringComparison.Ordinal);
        Assert.All(NamedReaders, name =>
            Assert.Contains("HolderWords.First(", File.ReadAllText(Path.Combine(driver, "Workflows", "Domain", name)), StringComparison.Ordinal));
    }

    [GeneratedRegex("""(==|!=|is)\s*(RequestOptions\.You|HolderWords\.You|"you")|("you"|RequestOptions\.You)\s*(==|!=)""")]
    private static partial Regex Compared();
}
