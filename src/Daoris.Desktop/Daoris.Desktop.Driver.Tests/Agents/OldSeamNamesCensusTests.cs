using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1t's census of the old seam's names: the set of doors is the <c>ProviderRegistry</c> and there is no second class for it, so no
/// source names <c>AdapterSet</c>, asks a registry for its own <c>Registry</c> or makes one with <c>ProviderRegistry.Of</c>. A name leaves this
/// list when its last reader goes (<c>ISessionAdapter</c>, <c>HarnessToolchain</c>, <c>LegacyDoor</c> and the <c>Adapter*</c> readers are
/// still read by the code the row's remaining clauses move).
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class OldSeamNamesCensusTests
{
    private static readonly Regex Gone = new(@"\bAdapterSet\b|ProviderRegistry\.Of\(|\b(?:Adapters|adapters|set|built|live)\.Registry\b", RegexOptions.Compiled);

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
