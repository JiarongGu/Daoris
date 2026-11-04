namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The structure gate for the desktop modules library (D166; the code-structure design §5): every source sits in its module's
/// folder, which is its module; the library keeps no <c>Application</c>, <c>Infrastructure</c> or <c>Domain</c> folder; a test
/// folder is a module's; a bridge domain's handler class is a <c>*Routes.cs</c> in <c>Bridge/Routes/</c> and the only file that
/// marks a route. The rules are held on sources made in memory, one case each, and then run over the library's own tree.
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class StructureTests
{
    private static readonly StructureMap Map = new(["Bridge", "Loop", "Platform"]);

    private static IReadOnlyList<string> Check(params (string Path, string Text)[] sources) =>
        StructureCheck.Check(Map, sources).Select(breach => breach.ToString()).ToList();

    [Fact]
    public void No_file_sits_at_the_library_s_top()
    {
        Assert.Empty(Check(("Loop/DriverLoop.cs", "class DriverLoop { }")));

        Assert.Equal(
            ["Fresh.cs:1 [top] a new file at the library's top: put it in its module's folder (structure.json's modules), not here"],
            Check(("Fresh.cs", "class Fresh { }")));
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

    private static IReadOnlyList<string> Mirror(IEnumerable<string> tests, StructureMap? map = null) =>
        StructureCheck.Check(map ?? Map, [], tests).Select(breach => breach.ToString()).ToList();

    [Fact]
    public void A_loose_fixture_the_map_names_is_at_the_tests_top()
    {
        Assert.Empty(Mirror(["Shared.cs", "Loop/TickTests.cs"], Map with { Loose = ["Shared.cs"] }));

        Assert.Equal(
            ["structure.json:0 [map] Shared.cs is listed as a loose fixture at the tests' top, and is not there"],
            Mirror(["Loop/TickTests.cs"], Map with { Loose = ["Shared.cs"] }));
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
