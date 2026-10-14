namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The structure gate for the desktop modules library (STRUCT5a, D166; the code-structure design §5): every source sits in
/// its module's folder, or on <c>fixtures/structure.json</c>'s list of the files not yet moved, which only shrinks; the
/// library keeps no <c>Application</c>, <c>Infrastructure</c> or <c>Domain</c> folder; a listed test sits under its module's
/// folder; a bridge domain's handler class is a <c>*Routes.cs</c> in <c>Bridge/Routes/</c> and the only file that marks a
/// route. The rules are held on sources made in memory, one case each, and then run over the library's own tree.
/// </summary>
public sealed class StructureTests
{
    private static readonly StructureMap Map = new(
        ["Bridge", "Loop", "Platform"],
        new Dictionary<string, string> { ["DriverLoop.cs"] = "Loop" });

    private static readonly (string, string)[] Unmoved = [("DriverLoop.cs", "class DriverLoop { }")];

    private static IReadOnlyList<string> Check(params (string Path, string Text)[] sources) =>
        StructureCheck.Check(Map, [.. Unmoved, .. sources]).Select(breach => breach.ToString()).ToList();

    [Fact]
    public void A_file_at_the_top_is_one_the_list_names_and_every_name_on_it_is_still_at_the_top()
    {
        Assert.Empty(Check());

        Assert.Equal(
            ["Fresh.cs:1 [top] a new file at the library's top: put it in its module's folder (structure.json's modules), not here"],
            Check(("Fresh.cs", "class Fresh { }")));

        // A move takes its entry off the list, or the list says a file is where it is not.
        Assert.Equal(
            ["DriverLoop.cs:0 [top] structure.json lists it at the library's top, where it is not: a move takes its entry off the list"],
            StructureCheck.Check(Map, [("Loop/DriverLoop.cs", "class DriverLoop { }")]).Select(breach => breach.ToString()));
    }

    [Fact]
    public void The_list_maps_each_file_it_names_to_a_module_it_declares()
    {
        var map = Map with { Root = new Dictionary<string, string> { ["DriverLoop.cs"] = "Gardens" } };

        Assert.Equal(
            ["structure.json:0 [map] DriverLoop.cs is mapped to Gardens, which is no module of the map"],
            StructureCheck.Check(map, Unmoved).Select(breach => breach.ToString()));
    }

    [Fact]
    public void A_file_in_a_folder_is_in_a_module_s_folder_and_the_library_keeps_no_layer_folders()
    {
        Assert.Empty(Check(("Platform/Clipped.cs", "class Clipped { }"), ("Bridge/Routes/TreesRoutes.cs", "class TreesRoutes { }"), ("Loop/Deep/Tick.cs", "class Tick { }")));

        Assert.Equal(
            [
                "Gardens/Rose.cs:1 [module] the folder Gardens is no module's: a source goes in its module's folder (structure.json's modules)",
                "Platform/Application/Clock.cs:1 [module] Platform holds no Application folder: the modules keep their files in scope folders named for what they hold",
                "Platform/Deep/Domain/Wire.cs:1 [module] Platform holds no Domain folder: the modules keep their files in scope folders named for what they hold",
                "Platform/Infrastructure/Http.cs:1 [module] Platform holds no Infrastructure folder: the modules keep their files in scope folders named for what they hold",
            ],
            Check(
                ("Gardens/Rose.cs", "class Rose { }"),
                ("Platform/Application/Clock.cs", "class Clock { }"),
                ("Platform/Deep/Domain/Wire.cs", "class Wire { }"),
                ("Platform/Infrastructure/Http.cs", "class Http { }")));
    }

    [Fact]
    public void A_Routes_file_sits_in_the_bridge_s_routes_folder_and_only_such_a_file_marks_a_route()
    {
        Assert.Empty(Check(
            ("Bridge/Routes/TreesRoutes.cs", "[DriverRoute(\"TREES\")] static void Trees() { }"),
            ("Platform/Note.cs", "// [DriverRoute( in a comment is no mark\nclass Note { }")));

        Assert.Equal(
            [
                "Bridge/GitRoutes.cs:1 [routes] a *Routes.cs is a bridge domain's handler class, and sits in Bridge/Routes/",
                "Platform/Stray.cs:2 [routes] marks a route with [DriverRoute(, and only a *Routes.cs does: a bridge domain is one handler class",
            ],
            Check(("Bridge/GitRoutes.cs", "class GitRoutes { }"), ("Platform/Stray.cs", "class Stray\n{ [DriverRoute(\"X\")] void X() { } }")));
    }

    private static IReadOnlyList<string> Mirror(IEnumerable<string> tests, params string[] listed) =>
        StructureCheck.Check(Map with { Tests = listed.ToDictionary(name => name, _ => "Loop") }, Unmoved, tests)
            .Select(breach => breach.ToString()).ToList();

    [Fact]
    public void A_test_the_map_lists_sits_under_its_module_s_folder_and_one_not_listed_is_not_constrained()
    {
        Assert.Empty(Mirror(["Loop/TickTests.cs", "Loop/Deep/WaitTests.cs", "LooseTests.cs", "Platform/OtherTests.cs"], "TickTests.cs", "WaitTests.cs"));

        Assert.Equal(
            [
                "Platform/TickTests.cs:0 [mirror] sits under Platform, and the map lists it under Loop: a test mirrors its module's folder",
                "WaitTests.cs:0 [mirror] sits at the top of the tests, and the map lists it under Loop: a test mirrors its module's folder",
            ],
            Mirror(["WaitTests.cs", "Platform/TickTests.cs"], "WaitTests.cs", "TickTests.cs"));
    }

    [Fact]
    public void A_name_the_map_lists_that_is_found_nowhere_is_a_stale_entry_or_a_module_the_map_does_not_declare()
    {
        Assert.Equal(
            ["structure.json:0 [map] GoneTests.cs is listed under Loop, and no such test is in the tests' tree: a delete takes its entry off the list"],
            Mirror(["Loop/TickTests.cs"], "GoneTests.cs"));

        var map = Map with { Tests = new Dictionary<string, string> { ["TickTests.cs"] = "Gardens" } };
        Assert.Contains(
            "structure.json:0 [map] TickTests.cs is a test mapped to Gardens, which is no module of the map",
            StructureCheck.Check(map, Unmoved, ["Gardens/TickTests.cs"]).Select(breach => breach.ToString()));

        Assert.Equal(
            ["structure.json:0 [map] Shared.cs is listed as a loose fixture at the tests' top, and is not there"],
            StructureCheck.Check(Map with { Loose = ["Shared.cs"] }, Unmoved, ["Loop/TickTests.cs"]).Select(breach => breach.ToString()));
    }

    [Fact]
    public void A_top_level_folder_of_the_tests_is_a_module_s_or_fixtures_golden_or_build_output()
    {
        Assert.Empty(Mirror(["Loop/TickTests.cs", "fixtures/B.cs", "golden/C.cs", "bin/D.cs", "obj/E.cs", "TestResults/F.cs"]));

        Assert.Equal(
            ["Gardens/RoseTests.cs:0 [mirror] the folder Gardens is no module's: a test folder is a module's, or fixtures, golden or a build output"],
            Mirror(["Gardens/RoseTests.cs"]));
    }

    [Fact]
    public void The_map_names_the_library_s_six_modules()
    {
        var map = StructureMap.Read(File.ReadAllText(MapFile()));

        Assert.Equal(["Bridge", "Browser", "Windows", "Update", "Loop", "Platform"], map.Modules);
        Assert.All(map.Tests.Values, module => Assert.True(map.Modules.Contains(module), $"a test is listed under {module}, which is no module of the map"));
    }

    [Fact]
    public void The_library_s_own_tree_keeps_its_structure()
    {
        var map = StructureMap.Read(File.ReadAllText(MapFile()));
        var library = SourceFiles.Project(SourceFiles.ModulesProject);
        var sources = Files(library)
            .Select(path => (Relative(library, path), File.ReadAllText(path)))
            .ToList();
        var testsFolder = SourceFiles.Project(SourceFiles.ModulesTestsProject);
        var tests = Files(testsFolder).Select(path => Relative(testsFolder, path)).ToList();

        var breaches = StructureCheck.Check(map, sources, tests);

        Assert.True(sources.Count >= 50, $"the gate read only {sources.Count} sources: its walk no longer reaches the library's tree");
        Assert.True(tests.Count >= 90, $"the gate read only {tests.Count} tests: its walk no longer reaches the tests' tree");
        Assert.True(breaches.Count == 0, "each breaks the library's structure (the code-structure design §5):\n" + string.Join('\n', breaches));
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
        Path.Combine(SourceFiles.Project(SourceFiles.ModulesTestsProject), "fixtures", "structure.json");
}
