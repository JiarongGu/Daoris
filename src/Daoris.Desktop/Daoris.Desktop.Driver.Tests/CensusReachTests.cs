using Microsoft.CodeAnalysis.CSharp;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a scoped semantic census binds (<see cref="CensusReach"/>, D313): the scope's files and each file that names a type of theirs,
/// found from the reference map, which is kept by the hash of each tree's text; and the census of censuses' mark for a semantic census
/// that stays whole.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class CensusReachTests
{
    private const string Desktop = "src/Daoris.Desktop/";

    private static HashSet<string> Scope(params string[] paths) => new(paths, StringComparer.OrdinalIgnoreCase);

    private static readonly StructureMap Map = new([new("Platform", "Platform", false), new("Course", "Course", true)]);

    // Platform is below Course, so each Platform file that names a Course type breaks the direction rule.
    private static readonly (string Path, string Text)[] Driver =
    [
        ("Course/Higher.cs", "namespace Daoris.Driver;\npublic class Higher { }"),
        ("Course/Other.cs", "namespace Daoris.Driver;\npublic class Other { }"),
        ("Platform/Names.cs", "namespace Daoris.Driver;\nclass Names { Higher Field; }"),
        ("Platform/Changed.cs", "namespace Daoris.Driver;\nclass Changed { Other Field; }"),
        ("Platform/Untouched.cs", "namespace Daoris.Driver;\nclass Untouched { Other Field; }"),
    ];

    private static List<string> Breached(IReadOnlySet<string>? scope) =>
        [.. StructureCheck.Check(Map, Driver, [], StructureAnalysis.Of(Driver, scope)).Select(breach => breach.File).Distinct()];

    [Fact]
    public void A_scoped_structure_census_catches_a_breach_in_a_changed_file_and_in_one_that_names_a_changed_type_and_ignores_an_untouched_one()
    {
        var scope = Scope($"{Desktop}Daoris.Desktop.Driver/Course/Higher.cs", $"{Desktop}Daoris.Desktop.Driver/Platform/Changed.cs");

        Assert.Equal(["Platform/Changed.cs", "Platform/Names.cs"], Breached(scope));
        Assert.Equal(["Platform/Changed.cs", "Platform/Names.cs", "Platform/Untouched.cs"], Breached(null));
    }

    [Fact]
    public void A_scoped_structure_census_judges_a_listed_edge_only_where_its_file_was_walked()
    {
        var map = Map with { Families = [new("family", "Course", new Dictionary<string, IReadOnlyList<string>> { ["Platform/Untouched.cs"] = ["Higher"] })] };
        var scope = Scope($"{Desktop}Daoris.Desktop.Driver/Platform/Changed.cs");

        Assert.DoesNotContain(StructureCheck.Check(map, Driver, [], StructureAnalysis.Of(Driver, scope)), breach => breach.Rule == "map");
        Assert.Contains(StructureCheck.Check(map, Driver, [], StructureAnalysis.Of(Driver)), breach => breach.Rule == "map");
    }

    private static readonly (string Path, string Text)[] Modules =
    [
        ("Daoris.Desktop.Modules/Planted/Changed.cs", "namespace Planted;\ninternal static class Holder\n{\n    internal const string Gates = \"daoris.gates.json\";\n}\n"),
        ("Daoris.Desktop.Modules/Planted/Reads.cs", "namespace Planted;\ninternal static class Reads\n{\n    internal static string Read(string root) => Path.Combine(root, Holder.Gates);\n}\n"),
        ("Daoris.Desktop.Modules/Planted/Untouched.cs", "namespace Planted;\ninternal static class Untouched\n{\n    internal static string Read() => \"daoris.json\";\n}\n"),
    ];

    private static readonly Lazy<Microsoft.CodeAnalysis.Compilation> ModulesCompiled = new(() => DesktopCompilations.Compile("Daoris.Desktop.Driver.Tests", Modules, driverBuilt: true));

    [Fact]
    public void A_scoped_literal_census_catches_a_name_in_a_changed_file_and_one_folded_from_it_elsewhere_and_ignores_an_untouched_file()
    {
        var scope = Scope($"{Desktop}Daoris.Desktop.Modules/Planted/Changed.cs");

        Assert.Equal(
            ["Daoris.Desktop.Modules/Planted/Changed.cs", "Daoris.Desktop.Modules/Planted/Reads.cs"],
            DeclarationLiterals.Hits(ModulesCompiled.Value, scope).Select(hit => hit.File).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(3, DeclarationLiterals.Hits(ModulesCompiled.Value).Select(hit => hit.File).Distinct().Count());
    }

    [Fact]
    public void A_row_is_judged_where_its_file_was_read_or_the_scope_names_it_and_every_row_with_no_scope()
    {
        var judges = CensusReach.Judges([ModulesCompiled.Value], Scope($"{Desktop}Daoris.Desktop.Modules/Planted/Changed.cs", $"{Desktop}Daoris.Desktop.Modules/Gone.cs"));

        Assert.True(judges("Daoris.Desktop.Modules/Planted/Reads.cs"));
        Assert.True(judges("Daoris.Desktop.Modules/Gone.cs"));
        Assert.False(judges("Daoris.Desktop.Modules/Planted/Untouched.cs"));
        Assert.True(CensusReach.Judges([ModulesCompiled.Value], null)("Daoris.Desktop.Modules/Planted/Untouched.cs"));
    }

    [Fact]
    public void A_file_is_reached_through_a_global_alias_or_an_attribute_s_shorthand_and_never_through_a_comment_or_a_string()
    {
        (string, string)[] sources =
        [
            ("Daoris.Desktop.Driver/Changed.cs", "namespace N;\npublic class Changed { }\npublic class MarkAttribute : System.Attribute { }"),
            ("Daoris.Desktop.Driver/Aliases.cs", "global using Alias = N.Changed;"),
            ("Daoris.Desktop.Driver/ByAlias.cs", "class ByAlias { Alias Field; }"),
            ("Daoris.Desktop.Driver/ByMark.cs", "[N.Mark] class ByMark { }"),
            ("Daoris.Desktop.Driver/Said.cs", "// Changed\nclass Said { string Words = \"Changed MarkAttribute\"; }"),
            ("Daoris.Desktop.Driver/Other.cs", "class Other { }"),
        ];

        var reached = CensusReach.Of(sources, Scope($"{Desktop}Daoris.Desktop.Driver/Changed.cs"));

        Assert.Equal(["Aliases.cs", "ByAlias.cs", "ByMark.cs", "Changed.cs"], reached.Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_project_no_change_reaches_is_not_compiled_and_one_whose_file_changed_is()
    {
        var driver = Scope($"{Desktop}Daoris.Desktop.Driver/Platform/DaorisHome.cs");

        Assert.Empty(DesktopCompilations.Shipping.Where(project => CensusReach.Reaches(project, Scope("docs/README.md", $"{Desktop}Daoris.Desktop.Driver/Gone.cs"))));
        Assert.True(CensusReach.Reaches(SourceFiles.DriverProject, driver));
        Assert.Empty(DesktopCompilations.Judged(Scope("docs/README.md")));
    }

    [Fact]
    public void A_project_s_source_is_reached_by_a_type_a_changed_file_of_another_project_declares()
    {
        (string, string)[] modules = [("Daoris.Desktop.Modules/Uses.cs", "class Uses { HomeEntry Entry; }"), ("Daoris.Desktop.Modules/Apart.cs", "class Apart { }")];

        Assert.Equal(["Daoris.Desktop.Modules/Uses.cs"], CensusReach.Of(modules, Scope($"{Desktop}Daoris.Desktop.Driver/Platform/DaorisHome.cs")));
        Assert.Empty(CensusReach.Of(modules, Scope($"{Desktop}Daoris.Desktop.Driver/Platform/Gone.cs", "docs/README.md")));
    }

    [Fact]
    public void The_map_s_key_is_the_hash_of_its_format_and_the_source_s_text_so_a_text_is_parsed_once()
    {
        const string text = "class Keyed { CensusReachTests Field; }";
        Assert.Equal(ReferenceMap.Key(text), ReferenceMap.Key(text));
        Assert.NotEqual(ReferenceMap.Key(text), ReferenceMap.Key(text + " "));
        Assert.Matches("^[0-9A-F]{32}$", ReferenceMap.Key(text));
        Assert.NotEqual(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..32], ReferenceMap.Key(text));

        var unique = $"class Keyed{Guid.NewGuid():N} {{ }}";
        Assert.Same(ReferenceMap.Of([unique]).Single(), ReferenceMap.Of([unique]).Single());
    }

    [Fact]
    public void The_map_keeps_each_tree_s_names_through_its_file()
    {
        var names = SpelledNames.Read(CSharpSyntaxTree.ParseText("global using Short = N.Long.Name;\nnamespace N;\npublic class TagAttribute { Other Field; }\npublic delegate void Done();").GetRoot());
        Assert.Equal(["Done", "Tag", "TagAttribute"], names.Declares.Order(StringComparer.Ordinal));
        Assert.Contains("Other", names.Spells);
        Assert.Equal("Short", names.Aliases.Single().Alias);
        Assert.Equal(["N", "Long", "Name"], names.Aliases.Single().Target);

        var folder = ScratchFolders.New("census-reach-");
        Directory.CreateDirectory(folder);
        try
        {
            var file = Path.Combine(folder, "names.tsv");
            ReferenceMap.Save(file, [new("KEY", names)]);
            var read = ReferenceMap.Load(file)["KEY"];
            Assert.Equal(names.Declares.Order(StringComparer.Ordinal), read.Declares.Order(StringComparer.Ordinal));
            Assert.Equal(names.Spells.Order(StringComparer.Ordinal), read.Spells.Order(StringComparer.Ordinal));
            Assert.Equal(names.Aliases.Single().Target, read.Aliases.Single().Target);
            Assert.Empty(ReferenceMap.Load(Path.Combine(folder, "missing.tsv")));
        }
        finally
        {
            ScratchFolders.Remove(folder);
        }
    }

    [Fact]
    public void The_census_of_censuses_is_red_on_a_semantic_census_marked_whole_with_no_reason_and_a_reason_that_stands_for_none()
    {
        Assert.Equal(
            [
                "B binds the compilations and is marked whole: read the scope through CensusReach, or say in CensusMarks.SemanticWhole why its rule compares across the tree",
                "A says why it stays whole and is no semantic census marked whole",
                "D says why it stays whole and is no semantic census marked whole",
                "C stays whole and says no reason",
            ],
            CensusReach.Unexplained(["A", "B", "C"], whole: ["B", "C", "D"], why: new Dictionary<string, string> { ["A"] = "a", ["C"] = " ", ["D"] = "d" }));
        Assert.Empty(CensusReach.Unexplained(["A", "B"], whole: ["B"], why: new Dictionary<string, string> { ["B"] = "across files" }));
        Assert.Equal(
            ["DeclarationBoundaryTests", "DeclarationKeepersCensusTests", "DeclarationLiteralsCensusTests", "ShellStructureTests", "StructureTests"],
            CensusReach.Semantic(typeof(CensusReachTests).Assembly).Order(StringComparer.Ordinal));
    }
}
