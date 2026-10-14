namespace Daoris.Service.Tests;

/// <summary>
/// The structure gate for the service core (STRUCT5, D166; the code-structure design §5): every core source sits in its
/// module's folder, or on <c>fixtures/structure.json</c>'s list of the files not yet moved, which only shrinks; the core keeps
/// no <c>Application</c>, <c>Infrastructure</c> or <c>Domain</c> folder; a listed test sits under its module's folder; and
/// nothing outside <c>Composition</c> names a type declared in it. The rules are held on sources made in memory, one case each,
/// and then run over the core's own tree.
/// </summary>
public sealed class StructureTests
{
    private static readonly StructureMap Map = new(
        ["Platform", "Remote", "Help", "Composition"],
        new Dictionary<string, string> { ["Moments.cs"] = "Platform", ["ServiceFactory.cs"] = "Composition" })
    {
        TestFolders = ["Mcp"],
    };

    private static readonly (string, string)[] Unmoved = [("Moments.cs", "class Moments { }"), ("ServiceFactory.cs", "class ServiceFactory { }")];

    private static IReadOnlyList<string> Check(params (string Path, string Text)[] sources) =>
        StructureCheck.Check(Map, [.. Unmoved, .. sources]).Select(breach => breach.ToString()).ToList();

    [Fact]
    public void A_file_at_the_top_is_one_the_list_names_and_every_name_on_it_is_still_at_the_top()
    {
        Assert.Empty(Check());

        Assert.Equal(
            ["Fresh.cs:1 [top] a new file at the core's top: put it in its module's folder (structure.json's modules), not here"],
            Check(("Fresh.cs", "class Fresh { }")));

        // A move takes its entry off the list, or the list says a file is where it is not.
        Assert.Equal(
            ["Moments.cs:0 [top] structure.json lists it at the core's top, where it is not: a move takes its entry off the list"],
            StructureCheck.Check(Map, [("ServiceFactory.cs", "class ServiceFactory { }"), ("Platform/Moments.cs", "class Moments { }")])
                .Select(breach => breach.ToString()));
    }

    [Fact]
    public void The_list_maps_each_file_it_names_to_a_module_it_declares()
    {
        var map = Map with { Root = new Dictionary<string, string> { ["Moments.cs"] = "Gardens" } };

        Assert.Equal(
            ["structure.json:0 [map] Moments.cs is mapped to Gardens, which is no module of the map"],
            StructureCheck.Check(map, [("Moments.cs", "class Moments { }")]).Select(breach => breach.ToString()));
    }

    [Fact]
    public void A_file_in_a_folder_is_in_a_module_s_folder_and_the_core_keeps_no_layer_folders()
    {
        Assert.Empty(Check(("Platform/Clipped.cs", "class Clipped { }"), ("Help/Scope/HelpBox.cs", "class HelpBox { }"), ("Composition/Other.cs", "class Other { }")));

        Assert.Equal(
            [
                "Gardens/Rose.cs:1 [module] the folder Gardens is no module's: a source goes in its module's folder (structure.json's modules)",
                "Platform/Application/Clock.cs:1 [module] Platform holds no Application folder: the core's modules keep their files at their top, or in a scope folder",
                "Remote/Domain/Wire.cs:1 [module] Remote holds no Domain folder: the core's modules keep their files at their top, or in a scope folder",
                "Remote/Infrastructure/Http.cs:1 [module] Remote holds no Infrastructure folder: the core's modules keep their files at their top, or in a scope folder",
            ],
            Check(
                ("Gardens/Rose.cs", "class Rose { }"),
                ("Platform/Application/Clock.cs", "class Clock { }"),
                ("Remote/Domain/Wire.cs", "class Wire { }"),
                ("Remote/Infrastructure/Http.cs", "class Http { }")));
    }

    [Fact]
    public void Nothing_outside_Composition_names_a_type_declared_in_it_and_a_comment_or_string_names_nothing()
    {
        var map = Map with { Root = new Dictionary<string, string> { ["Moments.cs"] = "Platform" } };
        (string, string) factory = ("Composition/ServiceFactory.cs", "public static class ServiceFactory { }");
        IReadOnlyList<string> Moved(params (string Path, string Text)[] sources) =>
            StructureCheck.Check(map, [("Moments.cs", "class Moments { ServiceFactory Made; }"), factory, .. sources]).Select(breach => breach.ToString()).ToList();

        // Composition's own files, and a file not yet moved, may.
        Assert.Empty(Moved(("Composition/Other.cs", "class Other { ServiceFactory Made; }"), ("Platform/Note.cs", "// ServiceFactory in a comment\nclass Note { string S = \"ServiceFactory\"; }")));
        Assert.Equal(
            ["Remote/Sync.cs:2 [composition] names ServiceFactory, declared in Composition/ServiceFactory.cs: Composition is the composition root, and no module uses it"],
            Moved(("Remote/Sync.cs", "class Sync\n{ ServiceFactory Made; }")));
    }

    private static IReadOnlyList<string> Mirror(IEnumerable<string> tests, params string[] listed) =>
        StructureCheck.Check(Map with { Tests = listed.ToDictionary(name => name, _ => "Remote") }, Unmoved, tests)
            .Select(breach => breach.ToString()).ToList();

    [Fact]
    public void A_test_the_map_lists_sits_under_its_module_s_folder_and_one_not_listed_is_not_constrained()
    {
        Assert.Empty(Mirror(["Remote/SyncTests.cs", "Remote/Deep/WireTests.cs", "LooseTests.cs", "Help/OtherTests.cs"], "SyncTests.cs", "WireTests.cs"));

        Assert.Equal(
            [
                "Help/SyncTests.cs:0 [mirror] sits under Help, and the map lists it under Remote: a test mirrors its module's folder",
                "WireTests.cs:0 [mirror] sits at the top of the tests, and the map lists it under Remote: a test mirrors its module's folder",
            ],
            Mirror(["WireTests.cs", "Help/SyncTests.cs"], "WireTests.cs", "SyncTests.cs"));
    }

    [Fact]
    public void A_name_the_tests_list_that_is_found_nowhere_is_a_stale_entry_or_a_module_the_map_does_not_declare()
    {
        Assert.Equal(
            ["structure.json:0 [map] GoneTests.cs is listed under Remote, and no such test is in the tests' tree: a delete takes its entry off the list"],
            Mirror(["Remote/SyncTests.cs"], "GoneTests.cs"));

        var map = Map with { Tests = new Dictionary<string, string> { ["SyncTests.cs"] = "Gardens" } };
        Assert.Contains(
            "structure.json:0 [map] SyncTests.cs is a test mapped to Gardens, which is no module of the map",
            StructureCheck.Check(map, Unmoved, ["Gardens/SyncTests.cs"]).Select(breach => breach.ToString()));
    }

    [Fact]
    public void A_top_level_folder_of_the_tests_is_a_module_s_a_door_s_or_fixtures_golden_or_build_output()
    {
        Assert.Empty(Mirror(["Help/HelpTests.cs", "Mcp/ToolTests.cs", "fixtures/B.cs", "golden/C.cs", "bin/D.cs", "obj/E.cs", "TestResults/F.cs"]));

        Assert.Equal(
            ["Gardens/RoseTests.cs:0 [mirror] the folder Gardens is no module's: a test folder is a module's, a door's, or fixtures, golden or a build output"],
            Mirror(["Gardens/RoseTests.cs"]));
    }

    [Fact]
    public void The_code_of_a_source_keeps_every_offset_and_blanks_its_comments_strings_and_characters()
    {
        const string text = "var a = \"x\\\"y\"; // c\nvar b = 'q'; /* d\ne */ var c = $\"{A(\"z\")}{{w}}\";";

        var code = StructureCheck.CodeOf(text);

        Assert.Equal(text.Length, code.Length);
        Assert.Equal(text.Split('\n').Length, code.Split('\n').Length);
        Assert.DoesNotContain("x", code);
        Assert.DoesNotContain("q", code);
        Assert.DoesNotContain("d", code);
        Assert.DoesNotContain("z", code);
        Assert.DoesNotContain("w", code);
        Assert.Contains("A(", code);
        Assert.Contains("var c", code);
    }

    [Fact]
    public void The_map_names_the_core_s_ten_modules_and_the_door_s_test_folder()
    {
        var map = StructureMap.Read(File.ReadAllText(MapFile()));

        Assert.Equal(
            ["Platform", "Composition", "Remote", "Registry", "Quests", "Asks", "Sessions", "Review", "Help", "Knowledge"],
            map.Modules);
        Assert.Equal(["Mcp"], map.TestFolders);
        Assert.All(map.Tests.Values, module => Assert.True(map.Modules.Contains(module) || map.TestFolders.Contains(module), $"a test is listed under {module}, which is no module of the map"));
    }

    [Fact]
    public void The_core_s_own_tree_keeps_its_structure()
    {
        var map = StructureMap.Read(File.ReadAllText(MapFile()));
        var core = Path.Combine(Root(), "src", "Daoris.Service", "Daoris.Service.Core");
        var sources = Files(core)
            .Select(path => (Relative(core, path), File.ReadAllText(path)))
            .ToList();
        var testsFolder = Path.Combine(Root(), "src", "Daoris.Service", "Daoris.Service.Tests");
        var tests = Files(testsFolder).Select(path => Relative(testsFolder, path)).ToList();

        var breaches = StructureCheck.Check(map, sources, tests);

        Assert.True(sources.Count >= 100, $"the gate read only {sources.Count} sources: its walk no longer reaches the core's tree");
        Assert.True(tests.Count >= 100, $"the gate read only {tests.Count} tests: its walk no longer reaches the tests' tree");
        Assert.True(breaches.Count == 0, "each breaks the core's structure (the code-structure design §5):\n" + string.Join('\n', breaches));
    }

    private static string Relative(string folder, string path) => Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>A project's sources, but its build output.</summary>
    private static IEnumerable<string> Files(string folder) =>
        Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories).Where(path =>
        {
            var first = Relative(folder, path).Split('/')[0];
            return first is not ("bin" or "obj");
        });

    private static string MapFile() =>
        Path.Combine(Root(), "src", "Daoris.Service", "Daoris.Service.Tests", "fixtures", "structure.json");

    private static string Root()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "daoris.json"))) folder = folder.Parent;
        return folder?.FullName ?? throw new InvalidOperationException("no workspace root above the test binary");
    }
}
