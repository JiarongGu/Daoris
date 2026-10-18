namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The structure gate (STRUCT1, D166; the code-structure design §5): every driver source sits in its module's folder, or on
/// <c>fixtures/structure.json</c>'s list of the files not yet moved, which only shrinks; a module keeps the onion for its model
/// and rules alone (a <c>Domain</c> folder, the rest at its top: FLAT1a, D166's note of 2026-10-14), so no module holds an
/// <c>Application</c> or an <c>Infrastructure</c> folder; a <c>Domain</c> file does no I/O; and nothing outside <c>Loop</c>, the
/// composition root, names a type declared in it. The rules are held here on sources made in memory, one case each, and then
/// run over the driver's own tree.
/// </summary>
public sealed class StructureTests
{
    private static readonly StructureMap Map = new(
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["Platform"] = [],
            ["Accounts"] = ["Domain"],
            ["Trees"] = ["Domain"],
            ["Plugins"] = ["Domain"],
            ["Help"] = [],
            ["Loop"] = [],
        },
        new Dictionary<string, string> { ["Landing.cs"] = "Trees", ["DriverWatch.cs"] = "Loop" });

    private static IReadOnlyList<string> Check(params (string Path, string Text)[] sources) =>
        StructureCheck.Check(Map, sources).Select(breach => breach.ToString()).ToList();

    private static readonly (string, string)[] Unmoved = [("Landing.cs", "class Landing { }"), ("DriverWatch.cs", "class DriverWatch { }")];

    private static IReadOnlyList<string> CheckWithUnmoved(params (string Path, string Text)[] sources) => Check([.. Unmoved, .. sources]);

    [Fact]
    public void A_file_at_the_top_is_one_the_list_names_and_every_name_on_it_is_still_at_the_top()
    {
        Assert.Empty(CheckWithUnmoved());

        Assert.Equal(
            ["Fresh.cs:1 [top] a new file at the driver's top: put it in its module's folder (structure.json's modules), not here"],
            CheckWithUnmoved(("Fresh.cs", "class Fresh { }")));

        // A move takes its entry off the list, or the list says a file is where it is not.
        Assert.Equal(
            ["Landing.cs:0 [top] structure.json lists it at the driver's top, where it is not: a move takes its entry off the list"],
            Check(("DriverWatch.cs", "class DriverWatch { }"), ("Trees/Landing.cs", "class Landing { }")));
    }

    [Fact]
    public void The_list_maps_each_file_it_names_to_a_module_it_declares()
    {
        var map = Map with { Root = new Dictionary<string, string> { ["Landing.cs"] = "Gardens" } };

        Assert.Equal(
            ["structure.json:0 [map] Landing.cs is mapped to Gardens, which is no module of the map"],
            StructureCheck.Check(map, [("Landing.cs", "class Landing { }")]).Select(breach => breach.ToString()));
    }

    [Fact]
    public void A_file_in_a_folder_sits_in_a_module_s_folder_at_its_top_or_in_the_Domain_that_module_holds()
    {
        Assert.Empty(CheckWithUnmoved(
            ("Plugins/PluginInstall.cs", "class PluginInstall { }"),
            ("Plugins/Domain/PluginOffer.cs", "class PluginOffer { }"),
            ("Help/Room/HelpRoom.cs", "class HelpRoom { }"),
            ("Platform/NoteCodes.cs", "class NoteCodes { }"),
            ("Loop/Driver.cs", "class Driver { }")));

        Assert.Equal(
            [
                "Gardens/Rose.cs:1 [module] the folder Gardens is no module's: a source goes in its module's folder (structure.json's modules)",
                "Loop/Domain/Tick.cs:1 [module] Loop holds no Domain layer: its layers are none",
                "Platform/Application/Clock.cs:1 [module] Platform holds no Application folder: a module keeps its model and rules in a Domain and the rest at its top",
                "Plugins/Infrastructure/PluginFiles.cs:1 [module] Plugins holds no Infrastructure folder: a module keeps its model and rules in a Domain and the rest at its top",
            ],
            CheckWithUnmoved(
                ("Gardens/Rose.cs", "class Rose { }"),
                ("Loop/Domain/Tick.cs", "class Tick { }"),
                ("Platform/Application/Clock.cs", "class Clock { }"),
                ("Plugins/Infrastructure/PluginFiles.cs", "class PluginFiles { }")));
    }

    [Fact]
    public void A_Domain_file_does_no_IO_and_what_its_comments_and_words_say_is_none()
    {
        const string impure = """"
            class Reading
            {
                // File.ReadAllText in a comment, and "Directory.Delete" in a sentence, are no use.
                string Words() => "Process.Start(" + @"Environment.Exit" + """DateTime.Now""";
                string Load(string path) => File.ReadAllText(path);
                DateTimeOffset At() => DateTimeOffset.UtcNow;
                string Where() => $"{Environment.MachineName} at {{Environment.UserName}}";
                string Named(Folder folder) => folder.File.Name + Path.Combine("a", "b");
            }
            """";

        Assert.Equal(
            [
                "Accounts/Domain/Reading.cs:5 [pure] File. reads or writes a file: a Domain is pure; it takes the world as values or through a port",
                "Accounts/Domain/Reading.cs:6 [pure] DateTimeOffset.UtcNow reads the clock: a Domain is pure; it takes the world as values or through a port",
                "Accounts/Domain/Reading.cs:7 [pure] Environment. reads the machine's environment: a Domain is pure; it takes the world as values or through a port",
            ],
            CheckWithUnmoved(("Accounts/Domain/Reading.cs", impure)));

        // The same I/O is the rest of the module's to do, at its top.
        Assert.Empty(CheckWithUnmoved(("Accounts/Reading.cs", impure), ("Trees/Read.cs", impure.Replace("Reading", "Read"))));
    }

    [Fact]
    public void Nothing_outside_Loop_names_a_type_declared_in_it()
    {
        var moved = Map with { Root = new Dictionary<string, string> { ["Landing.cs"] = "Trees" } };
        (string, string) watch = ("Loop/DriverWatch.cs", "public sealed class DriverWatch { }");
        IReadOnlyList<string> Moved(params (string Path, string Text)[] sources) =>
            StructureCheck.Check(moved, sources).Select(breach => breach.ToString()).ToList();

        // Loop's own files, and a file not yet moved, may.
        Assert.Empty(Moved(("Landing.cs", "class Landing { DriverWatch Watch; }"), watch, ("Loop/Driver.cs", "class Driver { DriverWatch Watch; }")));
        Assert.Equal(
            ["Plugins/PluginTrial.cs:2 [loop] names DriverWatch, declared in Loop/DriverWatch.cs: Loop is the composition root, and no module uses it"],
            Moved(("Landing.cs", "class Landing { }"), watch, ("Plugins/PluginTrial.cs", "class PluginTrial\n{ DriverWatch Watch; }")));
    }

    private static IReadOnlyList<string> Mirror(IEnumerable<string> tests, params string[] listed) =>
        StructureCheck.Check(
                Map with { Tests = listed.ToDictionary(name => name, _ => "Plugins") },
                Unmoved.Select(source => (source.Item1, source.Item2)),
                tests)
            .Select(breach => breach.ToString()).ToList();

    [Fact]
    public void A_test_the_map_lists_sits_under_its_module_s_folder_and_one_not_listed_is_not_constrained()
    {
        Assert.Empty(Mirror(["Plugins/PluginKitTests.cs", "Plugins/Hooks/HookTests.cs", "LooseTests.cs", "Trees/LandingTests.cs"], "PluginKitTests.cs", "HookTests.cs"));

        Assert.Equal(
            [
                "HookTests.cs:0 [mirror] sits at the top of the tests, and the map lists it under Plugins: a test mirrors its module's folder",
                "Trees/PluginKitTests.cs:0 [mirror] sits under Trees, and the map lists it under Plugins: a test mirrors its module's folder",
            ],
            Mirror(["HookTests.cs", "Trees/PluginKitTests.cs"], "HookTests.cs", "PluginKitTests.cs"));
    }

    [Fact]
    public void A_name_the_tests_list_that_is_found_nowhere_is_a_stale_entry_or_a_module_the_map_does_not_declare()
    {
        Assert.Equal(
            ["structure.json:0 [map] GoneTests.cs is listed under Plugins, and no such test is in the tests' tree: a delete takes its entry off the list"],
            Mirror(["Plugins/HookTests.cs"], "GoneTests.cs"));

        var map = Map with { Tests = new Dictionary<string, string> { ["HookTests.cs"] = "Gardens" } };
        Assert.Contains(
            "structure.json:0 [map] HookTests.cs is a test mapped to Gardens, which is no module of the map",
            StructureCheck.Check(map, Unmoved, ["Gardens/HookTests.cs"]).Select(breach => breach.ToString()));
    }

    [Fact]
    public void A_top_level_folder_of_the_tests_is_a_module_s_or_fixtures_golden_or_build_output()
    {
        Assert.Empty(Mirror(["Help/HelpTests.cs", "Plugins/A.cs", "fixtures/B.cs", "golden/C.cs", "bin/D.cs", "obj/E.cs", "TestResults/F.cs"]));

        Assert.Equal(
            ["Gardens/RoseTests.cs:0 [mirror] the folder Gardens is no module's: a test folder is a module's, or fixtures, golden or a build output"],
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
    public void The_map_names_the_design_s_fourteen_modules_and_the_brainstorm_s_and_the_one_folder_each_may_hold()
    {
        var map = StructureMap.Read(File.ReadAllText(MapFile()));

        // The design's fourteen, and Brainstorms (BRAINSTORM1a, D176 point 1): a bounded operation of its own, beside Review.
        Assert.Equal(
            ["Platform", "Accounts", "Agents", "Conversations", "Sessions", "Trees", "Quests", "Review", "Workflows", "Plugins", "Help", "Service", "Setup", "Brainstorms", "Loop"],
            map.Modules.Keys);
        // FLAT1a, D166's note of 2026-10-14: the onion is for a module's model and rules alone, a Domain folder and the rest at its top.
        Assert.All(map.Modules, module => Assert.True(module.Value.Count == 0 || module.Value.SequenceEqual(["Domain"]), $"{module.Key} holds {string.Join(", ", module.Value)}"));
        Assert.Empty(map.Modules["Loop"]);
        Assert.Empty(map.Modules["Platform"]);
        Assert.Equal(["Domain"], map.Modules["Conversations"]);
        Assert.Equal(["Domain"], map.Modules["Review"]);
        Assert.All(map.Split.Keys, file => Assert.True(map.Root.ContainsKey(file), $"{file} is noted for a split but is on no list"));
        Assert.Equal(16, map.Tests.Count(test => test.Value == "Plugins"));
        Assert.All(map.Tests.Values, module => Assert.True(map.Modules.ContainsKey(module), $"a test is listed under {module}, which is no module of the map"));
    }

    [Fact]
    public void The_driver_s_own_tree_keeps_its_structure()
    {
        var map = StructureMap.Read(File.ReadAllText(MapFile()));
        var driver = SourceFiles.Project(SourceFiles.DriverProject);
        var sources = SourceFiles.All(SourceFiles.DriverProject, "*.cs")
            .Select(path => (Path.GetRelativePath(driver, path).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(path)))
            .ToList();

        var tests = SourceFiles.All(SourceFiles.DriverTestsProject, "*.cs")
            .Select(path => Path.GetRelativePath(SourceFiles.Project(SourceFiles.DriverTestsProject), path).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();

        var breaches = StructureCheck.Check(map, sources, tests);

        Assert.True(sources.Count >= 290, $"the gate read only {sources.Count} sources: its walk no longer reaches the driver's tree");
        Assert.True(tests.Count(path => path.StartsWith("Plugins/", StringComparison.Ordinal)) >= 16, "the gate reads no Plugins tests: its walk no longer reaches the tests' tree");
        Assert.True(breaches.Count == 0, "each breaks the driver's structure (the code-structure design §5):\n" + string.Join('\n', breaches));
    }

    private static string MapFile() =>
        Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "structure.json");
}
