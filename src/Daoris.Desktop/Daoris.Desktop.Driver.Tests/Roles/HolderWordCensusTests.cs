using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The word <c>you</c> is read in one place, which sets the first holder; no runtime source compares against it.</summary>
[Trait(Category.Name, Category.Census)]
public sealed partial class HolderWordCensusTests
{
    [Fact]
    public void No_runtime_source_branches_on_you_beyond_the_holder_it_sets()
    {
        var driver = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver");
        var branching = new List<string>();
        foreach (var folder in new[] { "Workflows/Kinds", "Workflows/Nodes", "Workflows/Cast", "Roles" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(driver, folder), "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "HolderWords.cs") continue;
                if (Compared().IsMatch(File.ReadAllText(file))) branching.Add(Path.GetRelativePath(driver, file));
            }
        }

        Assert.Empty(branching);
        Assert.Contains("HolderWords.First", File.ReadAllText(Path.Combine(driver, "Workflows", "Cast", "AttemptTenures.cs")), StringComparison.Ordinal);
    }

    [GeneratedRegex("""(==|!=|is)\s*(RequestOptions\.You|HolderWords\.You|"you")|("you"|RequestOptions\.You)\s*(==|!=)""")]
    private static partial Regex Compared();
}
