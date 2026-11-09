using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The declarations' boundary (the design's §43, §57, §61): a banned name is the build's error in every shipping project but the
/// store's folder, the home's table and the allowance table's files, and nothing else quiets it. The walk finds every banned reference
/// itself, so the analyzer's silence is never what holds it. The list is empty until a row moves a symbol's last use, so each proof here
/// bans a symbol of a test's own compilation.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class DeclarationBoundaryTests
{
    private const string Names = "Daoris.Desktop.Driver/Workplace/Declarations/PlantedNames.cs";
    private const string Reader = "Daoris.Desktop.Driver/Planted/Reader.cs";
    private const string Kept = "Daoris.Desktop.Driver/Planted/Kept.cs";
    private const string Other = "Daoris.Desktop.Modules/Planted/Other.cs";
    private const string Unbound = "Daoris.Desktop.Driver/Planted/Unbound.cs";
    private const string Gates = "F:Planted.Names.Gates";
    private const string Lanes = "F:Planted.Names.Lanes";

    private static readonly (string Path, string Text)[] Planted =
    [
        (Names, "namespace Planted;\npublic static class Names\n{\n    public static readonly string Gates = \"g\";\n    public static readonly string Lanes = \"l\";\n}\n"
            + "internal static class StoreReads\n{\n    internal static string Read() => Names.Gates;\n}\n"),
        (Reader, "namespace Planted;\ninternal static class Reader\n{\n    internal static string Both() => Names.Gates + Names.Lanes;\n}\n"),
        (Kept, "namespace Planted;\ninternal static class Kept\n{\n    internal static int None() => 0;\n}\n"),
        (Other, "namespace Planted;\ninternal static class Other\n{\n    internal static string Read() => Names.Gates;\n}\n"),
        (Unbound, "namespace Planted;\ninternal static class Unbound\n{\n    internal static string Read(Missing held) => held.Gates;\n}\n"),
    ];

    private static readonly Lazy<Compilation> PlantedCompilation = new(() => DesktopCompilations.Compile("Planted", Planted, driverBuilt: true));

    [Fact]
    public void Nothing_quiets_the_banned_name_error_or_switches_the_analyzer_off()
    {
        var problems = BoundaryConfig.Problems(BoundaryConfig.Files());
        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Theory]
    [InlineData("src/Daoris.Desktop/Daoris.Desktop.Driver/Planted.cs", "#pragma warning disable RS0030\n", "quiets RS0030 with a #pragma")]
    [InlineData("src/Daoris.Desktop/Daoris.Desktop.Driver/Planted.cs", "  #pragma warning disable // for now\n", "quiets every warning with a #pragma naming no id")]
    [InlineData("src/Daoris.Desktop/Daoris.Desktop.Driver/Planted.cs", "[SuppressMessage(\"ApiDesign\", \"RS0030:Do not use banned APIs\")]\n", "quiets RS0030 with a SuppressMessage")]
    [InlineData("src/Daoris.Desktop/Daoris.Desktop.Driver/Daoris.Desktop.Driver.csproj", "<NoWarn>$(NoWarn);RS0030</NoWarn>", "quiets RS0030 with NoWarn")]
    [InlineData("src/Daoris.Desktop/Planted.props", "<WarningsNotAsErrors>RS0030</WarningsNotAsErrors>", "keeps RS0030 from failing the build with WarningsNotAsErrors")]
    [InlineData("Directory.Build.props", "<RunAnalyzers>false</RunAnalyzers>", "switches the analyzers off")]
    [InlineData("src/Daoris.Desktop/Daoris.Desktop.App/Daoris.Desktop.App.csproj", "<RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>", "switches the analyzers off")]
    [InlineData("src/Daoris.Desktop/Daoris.Desktop.GitDoor/Daoris.Desktop.GitDoor.csproj", "<IsTestProject>true</IsTestProject>", "ships and is marked a test project, so it builds without the analyzer")]
    [InlineData("src/Daoris.Desktop/Daoris.Desktop.Modules/.editorconfig", "[*.cs]\ndotnet_diagnostic.RS0030.severity = none\n", "is a second .editorconfig")]
    [InlineData("src/Daoris.Desktop/.globalconfig", "is_global = true\n", "is a .globalconfig")]
    public void Each_way_to_quiet_the_error_planted_turns_the_census_red_naming_it(string path, string text, string said)
    {
        var problems = BoundaryConfig.Problems(BoundaryConfig.Files().Where(file => file.Path != path).Append((path, text)));

        Assert.Contains($"{path} {said}", problems);
    }

    [Theory]
    [InlineData("[*.cs]\ndotnet_analyzer_diagnostic.category-ApiDesign.severity = none\n", "[*.cs] sets dotnet_analyzer_diagnostic.category-apidesign.severity")]
    [InlineData("[*.cs]\ndotnet_analyzer_diagnostic.severity = none\n", "[*.cs] sets dotnet_analyzer_diagnostic.severity")]
    [InlineData("[Daoris.Desktop.Driver/**]\ndotnet_diagnostic.RS0030.severity = warning\n", "[Daoris.Desktop.Driver/**] sets RS0030 to warning")]
    public void A_severity_added_to_the_one_editorconfig_turns_the_census_red_naming_it(string added, string said)
    {
        var config = File.ReadAllText(Path.Combine(DesktopCompilations.Desktop, ".editorconfig")) + "\n" + added;
        var problems = BoundaryConfig.Problems(BoundaryConfig.Files().Select(file => file.Path == BoundaryConfig.EditorConfig ? (file.Path, config) : file));

        Assert.Contains($"{BoundaryConfig.EditorConfig} {said}", problems);
    }

    [Fact]
    public void Every_banned_reference_in_every_shipping_project_is_in_the_store_or_an_allowance_row_and_every_row_is_used()
    {
        var ids = BannedNames.Listed();
        var uses = DesktopCompilations.Each.SelectMany(compilation => BannedNames.Uses(compilation, ids)).Distinct();
        var sections = BoundaryConfig.Sections(File.ReadAllText(Path.Combine(DesktopCompilations.Desktop, ".editorconfig"))).Quiet;
        var problems = DeclarationAllowances.Problems(uses, DeclarationAllowances.Rows, sections,
            file => File.Exists(Path.Combine(DesktopCompilations.Desktop, file)));
        problems.AddRange(ids.Where(id => DesktopCompilations.Each.All(compilation => BannedNames.Unresolved(compilation, [id]).Any()))
            .Select(id => $"{id} is banned and names no symbol"));

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void The_walk_over_a_tests_compilation_is_red_on_each_use_no_row_names_and_each_row_or_section_that_stands_for_nothing()
    {
        var uses = BannedNames.Uses(PlantedCompilation.Value, [Gates, Lanes]);
        ImmutableArray<Allowance> rows =
        [
            new(Reader, [Gates], "a planted row"),
            new(Kept, [Gates], "a row whose use is deleted"),
            new("Daoris.Desktop.Driver/Planted/Gone.cs", [Gates], "a row whose file is gone"),
            new("Daoris.Desktop.Driver/Planted/Unsectioned.cs", [Gates], "a row with no section"),
        ];
        string[] sections = [Reader, Kept, "Daoris.Desktop.Driver/Planted/Stray.cs"];
        var exists = Planted.Select(file => file.Path).Append("Daoris.Desktop.Driver/Planted/Unsectioned.cs").ToHashSet();

        var problems = DeclarationAllowances.Problems(uses, rows, sections, exists.Contains);

        Assert.Equal(
            [
                $"{Reader}:4 names {Lanes}, which no allowance row names",
                $"{Unbound}:4 names Gates, which does not bind, so the walk cannot follow it",
                $"{Other}:4 names {Gates}, which no allowance row names",
                $"{Kept} is an allowance row for {Gates} and names it no longer",
                "Daoris.Desktop.Driver/Planted/Gone.cs is an allowance row and is gone",
                $"Daoris.Desktop.Driver/Planted/Unsectioned.cs is an allowance row for {Gates} and names it no longer",
                "Daoris.Desktop.Driver/Planted/Unsectioned.cs is an allowance row with no section",
                "[Daoris.Desktop.Driver/Planted/Stray.cs] quiets RS0030 and is no allowance row's",
            ],
            problems);
        Assert.Contains(new BannedUse(Names, 9, Gates), uses);
    }

    [Fact]
    public async Task The_analyzer_is_red_on_a_banned_use_in_a_tests_compilation_and_silent_on_it_under_the_stores_folder()
    {
        var found = await BannedAnalyzer.RunAsync((Microsoft.CodeAnalysis.CSharp.CSharpCompilation)PlantedCompilation.Value, [Gates, Lanes]);

        Assert.Equal(
            [(Reader, DiagnosticSeverity.Error), (Reader, DiagnosticSeverity.Error), (Other, DiagnosticSeverity.Error)],
            found.OrderBy(each => each.File, StringComparer.Ordinal));
        Assert.DoesNotContain(found, each => each.File == Names);
    }
}
