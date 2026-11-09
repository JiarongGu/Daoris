namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The literal check over every shipping desktop project (the design's §43, §61): outside the store's folder no string names a
/// declaration, by its tail or folded through <c>string</c> and <c>Path</c>, and no constant names a home declaration entry to
/// <c>HomeEntry.Named</c>, but the rows here, each a file of the same name that is no repository's declaration or the one holder of
/// the names. Shown red on each planted name and on a row left out.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class DeclarationLiteralsCensusTests
{
    private const string What = "names a declaration";

    // The planted sources compile as this assembly, which the driver lets see its internals.
    private const string Planted = "Daoris.Desktop.Driver.Tests";

    /// <summary>Each member that writes a declaration's name out, by its file and member, with why it may.</summary>
    private static readonly Dictionary<string, string> Rows = new(StringComparer.Ordinal)
    {
        ["Daoris.Desktop.Modules/Browser/EngineProfile.cs EngineProfile.RecordPath"] = "a browser profile's own daoris.json, no repository's",
        ["Daoris.Desktop.Driver/Service/ServiceHostLocator.cs ServiceHostLocator.Candidates"] = "an existence walk up to the build's workspace",
        ["Daoris.Desktop.Driver/Knowledge/KnowledgeConnector.cs KnowledgeConnector.WorkspaceMarker"] = "the marker of the build's workspace, a constant the walks name",
        ["Daoris.Desktop.Driver/Knowledge/BaseHosts.cs BaseHosts.PassedOver"] = "the existence walk up to the build's workspace by WorkspaceMarker",
        ["Daoris.Desktop.Driver/Platform/DaorisHome.cs HomeEntry.DriverConfig"] = "the home's table, which declares the rows and lists them",
        ["Daoris.Desktop.Driver/Platform/DaorisHome.cs HomeEntry.Files"] = "the home's table",
        ["Daoris.Desktop.Driver/Platform/DaorisHome.cs HomeEntry.FilesFound"] = "the home's table",
        ["Daoris.Desktop.Driver/Platform/DaorisHome.cs HomeEntry.FilesAsks"] = "the home's table",
        ["Daoris.Desktop.Driver/Platform/DaorisHome.cs HomeEntry.Workflows"] = "the home's table",
        ["Daoris.Desktop.Driver/Platform/DeclarationNames.cs DeclarationNames.Manifest"] = "the one holder of every declaration's name, read-only so never folded",
        ["Daoris.Desktop.Driver/Platform/DeclarationNames.cs DeclarationNames.Rules"] = "the one holder of the names",
        ["Daoris.Desktop.Driver/Platform/DeclarationNames.cs DeclarationNames.Gates"] = "the one holder of the names",
        ["Daoris.Desktop.Driver/Platform/DeclarationNames.cs DeclarationNames.Lanes"] = "the one holder of the names",
        ["Daoris.Desktop.Driver/Platform/DeclarationNames.cs DeclarationNames.DriverConfig"] = "the one holder of the names",
        ["Daoris.Desktop.Driver/Platform/DeclarationNames.cs DeclarationNames.PersonRules"] = "the one holder of the names",
        ["Daoris.Desktop.Driver/Platform/DeclarationNames.cs DeclarationNames.FoundRules"] = "the one holder of the names",
        ["Daoris.Desktop.Driver/Platform/DeclarationNames.cs DeclarationNames.FileAsks"] = "the one holder of the names",
    };

    private static readonly Lazy<SortedSet<string>> Found = new(() => Keys(DesktopCompilations.Each.SelectMany(DeclarationLiterals.Hits)));

    private static SortedSet<string> Keys(IEnumerable<LiteralHit> hits) => new(hits.Select(hit => $"{hit.File} {hit.Member}"), StringComparer.Ordinal);

    [Fact]
    public void No_shipping_source_outside_the_store_writes_a_declarations_name_but_the_rows()
    {
        var problems = SourceCensus.Problems(Found.Value, Rows.Keys, What);
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void Each_row_left_out_turns_the_census_red_naming_it()
    {
        Assert.Empty(SourceCensus.Unproved(Found.Value, Rows.Keys, What));
    }

    [Theory]
    [InlineData("File.ReadAllText(Path.Combine(root, \"daoris.gates.json\"))", "daoris.gates.json")]
    [InlineData("\"daoris.\" + \"gates.json\"", "daoris.gates.json")]
    [InlineData("Path.Combine(home, \"data\", \"driver.json\")", "driver.json")]
    [InlineData("$\"{root}:daoris.gates.json\"", "daoris.gates.json")]
    [InlineData("Path.Combine(home, \"data\", \"workflows\")", "data/workflows")]
    [InlineData("Path.ChangeExtension(Path.Combine(root, \"daoris.gates\"), \"json\")", "daoris.json")]
    [InlineData("Path.ChangeExtension(Path.Combine(root, \"daoris.gates.\"), \"json\")", "daoris.gates.json")]
    [InlineData("string.Concat(\"daoris.gates\", \".json\")", "daoris.gates.json")]
    [InlineData("Path.Combine(home, string.Join(\"/\", \"data\", \"driver.json\"))", "driver.json")]
    [InlineData("\"DAORIS.JSON\".ToLowerInvariant()", "daoris.json")]
    [InlineData("File.ReadAllText(DaorisHome.Entry(home, HomeEntry.Named(\"driver\")!))", "the home entry driver")]
    [InlineData("HomeEntry.Table.First(entry => entry.Name == \"workflows\").Name", "the home entry workflows")]
    public void A_planted_name_turns_the_census_red_naming_it(string expression, string named)
    {
        var compilation = DesktopCompilations.Compile(Planted,[("Daoris.Desktop.Modules/Planted.cs",
            "using Daoris.Driver;\nnamespace Planted;\ninternal static class Reads\n{\n    internal static object Read(string root, string home) => "
            + expression + ";\n}\n")], driverBuilt: true);

        var hits = DeclarationLiterals.Hits(compilation);

        Assert.Contains(new LiteralHit("Daoris.Desktop.Modules/Planted.cs", 5, "Reads.Read", named), hits);
        Assert.Equal(["Daoris.Desktop.Modules/Planted.cs Reads.Read names a declaration and is not listed"],
            SourceCensus.Problems(Keys(hits), [], What));
    }

    [Fact]
    public void A_declarations_name_read_through_its_holder_or_from_data_is_no_literal()
    {
        var compilation = DesktopCompilations.Compile(Planted,[("Daoris.Desktop.Modules/Planted.cs",
            "using Daoris.Driver;\nnamespace Planted;\ninternal static class Reads\n{\n    internal static string Read(string root, string name) => "
            + "Path.Combine(root, DeclarationNames.Gates) + Path.Combine(root, name) + HomeEntry.Named(name)?.Name + \"the gates file\";\n}\n")], driverBuilt: true);

        Assert.Empty(DeclarationLiterals.Hits(compilation));
    }
}
