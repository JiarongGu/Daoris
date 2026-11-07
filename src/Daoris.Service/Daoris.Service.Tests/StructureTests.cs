namespace Daoris.Service.Tests;

/// <summary>
/// The structure gate for the service core: every core source sits in its module's
/// folder, which is its module; the core keeps no <c>Application</c>, <c>Infrastructure</c> or <c>Domain</c> folder; a test
/// folder is a module's or a door's; and nothing outside <c>Composition</c> names a type declared in it. The rules are held on
/// sources made in memory, one case each, and then run over the core's own tree.
/// </summary>
public sealed class StructureTests
{
    private static readonly StructureMap Map = new(["Platform", "Remote", "Help", "Composition"])
    {
        TestFolders = ["Mcp"],
    };

    private static IReadOnlyList<string> Check(params (string Path, string Text)[] sources) =>
        StructureCheck.Check(Map, sources).Select(breach => breach.ToString()).ToList();

    [Fact]
    public void No_file_sits_at_the_core_s_top()
    {
        Assert.Empty(Check(("Platform/Moments.cs", "class Moments { }")));

        Assert.Equal(
            ["Fresh.cs:1 [top] a new file at the core's top: put it in its module's folder (structure.json's modules), not here"],
            Check(("Fresh.cs", "class Fresh { }")));
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
        (string, string) factory = ("Composition/ServiceFactory.cs", "public static class ServiceFactory { }");
        IReadOnlyList<string> Named(params (string Path, string Text)[] sources) =>
            StructureCheck.Check(Map, [factory, .. sources]).Select(breach => breach.ToString()).ToList();

        // Composition's own files may.
        Assert.Empty(Named(("Composition/Other.cs", "class Other { ServiceFactory Made; }"), ("Platform/Note.cs", "// ServiceFactory in a comment\nclass Note { string S = \"ServiceFactory\"; }")));
        Assert.Equal(
            ["Remote/Sync.cs:2 [composition] names ServiceFactory, declared in Composition/ServiceFactory.cs: Composition is the composition root, and no module uses it"],
            Named(("Remote/Sync.cs", "class Sync\n{ ServiceFactory Made; }")));
    }

    private static IReadOnlyList<string> Mirror(IEnumerable<string> tests) =>
        StructureCheck.Check(Map, [], tests).Select(breach => breach.ToString()).ToList();

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
