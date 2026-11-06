using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1t's census of the old seam's names: the registry is made of providers alone and a reader asks a door's record, its
/// provider's parts or the account agent, so no source names the adapter seam (<c>ISessionAdapter</c>, its one implementer
/// <c>LegacyDoor</c>, its table <c>AdapterTable</c> or the <c>AdapterSet</c> before it), the toolchain (<c>HarnessToolchain</c>, whose program
/// is a <c>ToolSpec</c> and whose accounts are the <c>AccountAgent</c>'s, nor its converters <c>ToolSpecs</c>, <c>PoolFacts.Of</c> and
/// <c>TablesPart.Of</c>), the adapter readers of a door (<c>AdapterAbilities</c>, <c>AdapterParts</c>, <c>AdapterClaudeSeam</c>,
/// <c>AdapterChoiceVariables</c>), or asks a registry for its own <c>Registry</c> or makes one with <c>ProviderRegistry.Of</c>.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class OldSeamNamesCensusTests
{
    private static readonly Regex Gone = new(
        @"\b(?:AdapterSet|ISessionAdapter|LegacyDoor|AdapterTable|HarnessToolchain|ToolSpecs|AdapterAbilities|AdapterParts|AdapterClaudeSeam|AdapterChoiceVariables)\b"
        + @"|\b(?:PoolFacts|TablesPart)\.Of\(|ProviderRegistry\.Of\(|\b(?:Adapters|adapters|set|built|live)\.Registry\b",
        RegexOptions.Compiled);

    [Fact]
    public void No_source_names_the_adapter_set_or_asks_a_registry_for_its_own()
    {
        var root = Path.Combine(WorkspaceRoot.Folder, "src");
        var found = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !path.EndsWith("OldSeamNamesCensusTests.cs", StringComparison.Ordinal))
            .Where(path => Gone.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        Assert.Empty(found);
    }
}
