using System.Diagnostics;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The structure gate (STRUCT1, D166; FOUNDATION1 mod-0, D212, the design §14.5): every driver source is placed in one of the twenty-one
/// subdomains, by <c>fixtures/structure.json</c> or by the folder of the subdomain it sits in, and a file names only its own subdomain's
/// types and those of the subdomains listed before it, read through Roslyn's semantic model: every name that binds to a type a driver
/// file declares, whatever its spelling. Today's edges against the order are the census's, each an exact edge under its family and the
/// row after which it is cut; a <c>Domain</c> file does no I/O; and nothing outside Loop's folder names a type declared in it. The rules are
/// held here on sources made in memory, one case each, and then run over the driver's own tree.
/// </summary>
public sealed class StructureTests
{
    private static readonly StructureMap Map = new(
        [
            new("Platform", "Platform", false),
            new("Course", "Course", true),
            new("Help", "Help", false),
            new("Loop", "Loop", false),
        ],
        new Dictionary<string, string> { ["Landing.cs"] = "Platform" })
    {
        Seams = ["ServiceClient"],
    };

    /// <summary>A planted source: the driver's one namespace, then the body, so the body's first line is line 2.</summary>
    private static (string Path, string Text) Src(string path, string body) => (path, "namespace Daoris.Driver;\n" + body);

    private static readonly (string, string) Landing = Src("Landing.cs", "public class Landing { }");

    private static IReadOnlyList<string> Check(StructureMap map, params (string Path, string Text)[] sources) =>
        StructureCheck.Check(map, sources).Select(breach => breach.ToString()).ToList();

    private static IReadOnlyList<string> Check(params (string Path, string Text)[] sources) => Check(Map, [Landing, .. sources]);

    private static string Up(string file, int line, string type, string declared, string from, string above) =>
        $"{file}:{line} [direction] names {type}, declared in {declared}: {from} is below {above}, and no edge family of structure.json lists this edge (a file and the type it names, under the row after which it is cut)";

    /// <summary>The upper types the planted files below name: Course's, in one file each.</summary>
    private static readonly (string, string)[] Higher =
    [
        Src("Course/Higher.cs", "public class Higher { }"),
        Src("Course/Outer.cs", "public class Outer { public class Inner { } }"),
        Src("Course/Statics.cs", "public static class Statics { public static int One = 1; }"),
        Src("Course/Shapes.cs", "public record struct Point(int X, int Y);\npublic record class Named(string Name);"),
    ];

    [Fact]
    public void A_file_is_placed_by_the_list_or_by_the_folder_of_its_subdomain_and_nothing_else_is()
    {
        Assert.Empty(Check(Src("Platform/Clock.cs", "class Clock { }"), Src("Help/Room.cs", "class Room { }")));

        Assert.Equal(
            [
                "Fresh.cs:1 [place] a file at the driver's top that structure.json does not place: put it in its subdomain's folder",
                "Gardens/Rose.cs:1 [place] the folder Gardens is no subdomain's, and structure.json does not place the file: put it in its subdomain's folder (structure.json's subdomains)",
            ],
            Check(Src("Fresh.cs", "class Fresh { }"), Src("Gardens/Rose.cs", "class Rose { }")));

        // A list's entry is a file that is there, in a subdomain the map declares.
        Assert.Equal(
            [
                "Gone.cs:0 [place] structure.json places it, and it is not there: a move gives its entry its new path, and a delete takes it off the list",
                "structure.json:0 [map] Gone.cs is placed in Gardens, which is no subdomain of the map",
            ],
            StructureCheck.Check(Map with { Files = new Dictionary<string, string> { ["Landing.cs"] = "Platform", ["Gone.cs"] = "Gardens" } }, [Landing])
                .Select(breach => breach.ToString()));
    }

    [Fact]
    public void A_subdomain_that_has_moved_holds_every_file_placed_in_it_in_its_folder()
    {
        var moved = Map with
        {
            Subdomains = [Map.Subdomains[0] with { Moved = true }, .. Map.Subdomains.Skip(1)],
            Files = new Dictionary<string, string> { ["Landing.cs"] = "Platform", ["Help/Loose.cs"] = "Platform", ["Platform/Strayed.cs"] = "Help" },
        };

        // A subdomain whose move has not landed may still sit in a moved one's folder (Platform's is today's, which the rest leave a row
        // at a time): Strayed is Help's to move, so it is no breach until Help's own move has landed.
        Assert.Equal(
            [
                "Help/Loose.cs:1 [place] Platform has moved, and the map places this file in it: its files sit in Platform",
                "Landing.cs:1 [place] Platform has moved, and the map places this file in it: its files sit in Platform",
            ],
            Check(moved, Landing, Src("Help/Loose.cs", "class Loose { }"), Src("Platform/Strayed.cs", "class Strayed { }")));

        var both = moved with { Subdomains = [.. moved.Subdomains.Select(each => each.Name == "Help" ? each with { Moved = true } : each)] };
        Assert.Contains(
            "Platform/Strayed.cs:1 [place] Help has moved, and the map places this file in it: its files sit in Help",
            Check(both, Landing, Src("Help/Loose.cs", "class Loose { }"), Src("Platform/Strayed.cs", "class Strayed { }")));
    }

    [Fact]
    public void A_subdomain_keeps_its_model_and_rules_in_a_Domain_the_map_lets_it_hold_and_no_other_layer()
    {
        Assert.Empty(Check(Src("Course/Domain/Rule.cs", "class Rule { }"), Src("Help/Room/HelpRoom.cs", "class HelpRoom { }")));

        Assert.Equal(
            [
                "Loop/Application/Tick.cs:1 [module] Loop holds no Application folder: a subdomain keeps its model and rules in a Domain and the rest at its top",
                "Platform/Domain/Clock.cs:1 [module] Platform holds no Domain layer: its layers are none",
                "Platform/Infrastructure/Files.cs:1 [module] Platform holds no Infrastructure folder: a subdomain keeps its model and rules in a Domain and the rest at its top",
            ],
            Check(
                Src("Loop/Application/Tick.cs", "class Tick { }"),
                Src("Platform/Domain/Clock.cs", "class Clock { }"),
                Src("Platform/Infrastructure/Files.cs", "class Files { }")));
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
            class Folder { public Folder File => this; public string Name => ""; }
            """";

        Assert.Equal(
            [
                "Course/Domain/Reading.cs:6 [pure] File. reads or writes a file: a Domain is pure; it takes the world as values or through a port",
                "Course/Domain/Reading.cs:7 [pure] DateTimeOffset.UtcNow reads the clock: a Domain is pure; it takes the world as values or through a port",
                "Course/Domain/Reading.cs:8 [pure] Environment. reads the machine's environment: a Domain is pure; it takes the world as values or through a port",
            ],
            Check(Src("Course/Domain/Reading.cs", impure)));

        // The same I/O is the rest of the subdomain's to do, at its top.
        Assert.Empty(Check(Src("Course/Reading.cs", impure)));
    }

    [Fact]
    public void A_Domain_file_names_no_type_of_the_IO_seams_and_holds_no_async_but_where_the_census_names_it()
    {
        var seam = Src("Platform/ServiceClient.cs", "public class ServiceClient { }");
        var asks = Src("Course/Domain/Asks.cs", "class Asks { ServiceClient Client; }");
        var slow = Src("Course/Domain/Slow.cs", "class Slow { async Task Wait() => await Task.Delay(1); }");

        Assert.Equal(
            [
                "Course/Domain/Asks.cs:2 [pure] names ServiceClient, an I/O seam: a Domain is pure; it takes the world as values or through a port",
                "Course/Domain/Slow.cs:0 [pure] holds an async method: a Domain is pure; it takes the world as values or through a port, and awaits nothing",
            ],
            Check(seam, asks, slow));

        // The same names in the subdomain's top are the rest of its work.
        Assert.Empty(Check(seam, Src("Course/Asks.cs", "class Asks { ServiceClient Client; }"), Src("Course/Slow.cs", "class Slow { async Task Wait() => await Task.Delay(1); }")));

        // The census names a crossing, and only that one; an entry for a crossing that is gone is stale.
        var census = Map with
        {
            Pure = new Dictionary<string, IReadOnlyList<string>>
            {
                ["Course/Domain/Asks.cs"] = ["ServiceClient"],
                ["Course/Domain/Slow.cs"] = ["async", "ServiceClient"],
            },
        };
        Assert.Equal(
            ["structure.json:0 [map] Course/Domain/Slow.cs is listed as crossing ServiceClient in a Domain, and it does not: a cut takes its entry off the list"],
            Check(census, Landing, seam, asks, slow));
    }

    [Fact]
    public void A_type_named_in_a_higher_subdomain_is_refused_whatever_its_spelling_plainly_fully_qualified_through_global_an_alias_a_nested_type_s_alias_or_a_static_import()
    {
        // Each file below names one upper type; the gate reads each name by what it binds to, not by how it is written.
        var plain = Src("Platform/Plain.cs", "class Plain { Higher Field; }");
        var qualified = Src("Platform/Qualified.cs", "class Qualified { Daoris.Driver.Higher Field; }");
        var global = Src("Platform/Global.cs", "class Global { global::Daoris.Driver.Higher Field; }");
        var aliased = Src("Platform/Aliased.cs", "using Drv = Daoris.Driver;\nclass Aliased { Drv.Higher Field; }");
        var typeAlias = Src("Platform/TypeAlias.cs", "using Top = Daoris.Driver.Higher;\nclass TypeAlias { Top Field; }");
        var nested = Src("Platform/Nested.cs", "using Inner = Daoris.Driver.Outer.Inner;\nclass Nested { Inner Field; }");
        var statics = Src("Platform/Statics.cs", "using static Daoris.Driver.Statics;\nclass Uses { int Field = One; }");

        Assert.Equal(
            [
                Up("Platform/Aliased.cs", 3, "Higher", "Course/Higher.cs", "Platform", "Course"),
                Up("Platform/Global.cs", 2, "Higher", "Course/Higher.cs", "Platform", "Course"),
                Up("Platform/Nested.cs", 2, "Outer", "Course/Outer.cs", "Platform", "Course"),
                Up("Platform/Nested.cs", 2, "Outer.Inner", "Course/Outer.cs", "Platform", "Course"),
                Up("Platform/Plain.cs", 2, "Higher", "Course/Higher.cs", "Platform", "Course"),
                Up("Platform/Qualified.cs", 2, "Higher", "Course/Higher.cs", "Platform", "Course"),
                Up("Platform/Statics.cs", 2, "Statics", "Course/Statics.cs", "Platform", "Course"),
                Up("Platform/TypeAlias.cs", 2, "Higher", "Course/Higher.cs", "Platform", "Course"),
            ],
            Check(Map, [Landing, .. Higher, plain, qualified, global, aliased, typeAlias, nested, statics]));

        // The same names from the upper subdomain, or from the same one, are no edge.
        Assert.Empty(Check(
            Map,
            [
                Landing,
                Src("Course/Down.cs", "class Down { Higher A; Daoris.Driver.Higher B; global::Daoris.Driver.Higher C; Outer.Inner D; }"),
                Src("Platform/Low.cs", "public class Low { }"),
                Src("Course/Uses.cs", "class UsesLow { Low Field; Daoris.Driver.Low Other; }"),
                .. Higher,
            ]));
    }

    [Fact]
    public void A_member_named_like_a_higher_type_is_no_edge_and_a_type_named_in_a_file_that_declares_such_a_member_is_one()
    {
        var home = Src(
            "Platform/Home.cs",
            "class Home { public static string Higher => \"entry\"; public string Use() => Higher + Home.Higher; public Home With(string Outer) => this; }");
        var both = Src("Platform/Both.cs", "class Both { public static string Higher => \"entry\"; Higher Field; }");

        // A member's occurrence binds to the member, by the symbol and never by the spelling.
        Assert.Empty(Check(Src("Course/Higher.cs", "public class Higher { }"), Src("Course/Outer.cs", "public class Outer { }"), home));

        Assert.Equal(
            [Up("Platform/Both.cs", 2, "Higher", "Course/Higher.cs", "Platform", "Course")],
            Check(Src("Course/Higher.cs", "public class Higher { }"), home, both));
    }

    [Fact]
    public void A_record_struct_and_a_record_class_are_declared_as_the_compiler_reads_them_and_an_edge_to_each_fails_where_it_points_up()
    {
        var names = Src("Platform/Names.cs", "class Names { Point One; Named Two; }");

        Assert.Equal(
            [
                Up("Platform/Names.cs", 2, "Named", "Course/Shapes.cs", "Platform", "Course"),
                Up("Platform/Names.cs", 2, "Point", "Course/Shapes.cs", "Platform", "Course"),
            ],
            Check(Map, [Landing, .. Higher, names]));

        // From the upper subdomain, both are fine.
        Assert.Empty(Check(Map, [Landing, Src("Course/Names.cs", "class Names { Point One; Named Two; }"), .. Higher]));
    }

    [Fact]
    public void A_comment_a_string_and_a_cref_name_no_type()
    {
        var says = Src(
            "Platform/Says.cs",
            "/// <summary>See <see cref=\"Higher\"/> and Higher.</summary>\nclass Says\n{\n    // Higher in a comment.\n    string Words() => \"Higher\" + $\"{nameof(Says)} Daoris.Driver.Higher\";\n}");

        Assert.Empty(Check(Src("Course/Higher.cs", "public class Higher { }"), says));
    }

    [Fact]
    public void An_edge_against_the_order_passes_only_where_the_census_lists_it_exactly()
    {
        var higher = Src("Course/Higher.cs", "public class Higher { }");
        var other = Src("Course/Other.cs", "public class Other { }");
        var plain = Src("Platform/Plain.cs", "class Plain { Higher Field; }");
        var listed = Map with
        {
            Families = [new("Plain's names of Course", "Platform", new Dictionary<string, IReadOnlyList<string>> { ["Platform/Plain.cs"] = ["Higher"] })],
        };

        Assert.Empty(Check(listed, Landing, higher, other, plain));

        // A family admits only the edges it lists: a new type of the same file, and a new file naming the same type, fail.
        Assert.Equal(
            [Up("Platform/Plain.cs", 2, "Other", "Course/Other.cs", "Platform", "Course")],
            Check(listed, Landing, higher, other, Src("Platform/Plain.cs", "class Plain { Higher Field; Other More; }")));
        Assert.Equal(
            [Up("Platform/Fresh.cs", 2, "Higher", "Course/Higher.cs", "Platform", "Course")],
            Check(listed, Landing, higher, other, plain, Src("Platform/Fresh.cs", "class Fresh { Higher Field; }")));

        // An entry that is no edge now is stale, an edge listed twice is refused, and a family is cut after a subdomain the map has.
        var stale = Map with
        {
            Families =
            [
                new("a", "Platform", new Dictionary<string, IReadOnlyList<string>> { ["Platform/Plain.cs"] = ["Higher", "Other"] }),
                new("b", "Gardens", new Dictionary<string, IReadOnlyList<string>> { ["Platform/Plain.cs"] = ["Higher"] }),
            ],
        };
        Assert.Equal(
            [
                "structure.json:0 [map] Platform/Plain.cs names Higher in two edge families, b among them",
                "structure.json:0 [map] a lists Platform/Plain.cs naming Other, which is no edge against the order now: a cut takes its entry off the list",
                "structure.json:0 [map] the edge family b is cut after Gardens, which is no subdomain of the map",
            ],
            Check(stale, Landing, higher, other, plain).Order(StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void Nothing_outside_Loop_s_folder_names_a_type_declared_in_it_and_no_census_excepts_one()
    {
        var driver = Src("Loop/Driver.cs", "public class Driver { Tick Own; }");
        var tick = Src("Course/Tick.cs", "public class Tick { }");

        // Loop's own files, and a file below, may name what is above in the order only where the census lists it; Loop's folder is never named from outside.
        Assert.Equal(
            ["Course/Naming.cs:2 [loop] names Driver, declared in Loop/Driver.cs: Loop is the composition root, and nothing outside its folder names it"],
            Check(driver, tick, Src("Course/Naming.cs", "class Naming { Driver Watch; }")));

        // An exception for it does not except it: the gate still refuses the edge, and the entry that names no allowed edge is stale.
        var excepted = Map with
        {
            Families = [new("the driver", "Course", new Dictionary<string, IReadOnlyList<string>> { ["Course/Naming.cs"] = ["Driver"] })],
        };
        Assert.Contains(
            "Course/Naming.cs:2 [loop] names Driver, declared in Loop/Driver.cs: Loop is the composition root, and nothing outside its folder names it",
            Check(excepted, Landing, driver, tick, Src("Course/Naming.cs", "class Naming { Driver Watch; }")));

        // A file the map places in Loop but that sits outside its folder is a census exception, not an absolute edge.
        var runner = Src("Course/Runner.cs", "public class Runner { }");
        var placed = Map with { Files = new Dictionary<string, string> { ["Landing.cs"] = "Platform", ["Course/Runner.cs"] = "Loop" } };
        var using_ = Src("Course/Using.cs", "class Using { Runner Run; }");
        Assert.Equal(
            [Up("Course/Using.cs", 2, "Runner", "Course/Runner.cs", "Course", "Loop")],
            Check(placed, Landing, runner, using_));
        Assert.Empty(Check(
            placed with { Families = [new("the runner's port", "Course", new Dictionary<string, IReadOnlyList<string>> { ["Course/Using.cs"] = ["Runner"] })] },
            Landing, runner, using_));
    }

    [Fact]
    public void Loop_holds_no_rule_the_census_does_not_name_as_moving()
    {
        var look = Src("Loop/Look.cs", "class Look { static bool Due(int n) => n > 1; static int Count() => 1; static async Task<bool> Later() => true; }");

        Assert.Equal(
            ["Loop/Look.cs:2 [loop] Look.Due is a rule, a static bool, in Loop: Loop holds no rules, so it moves to its subdomain as a pure function, and the census names it as moving until then"],
            Check(look));

        var named = Map with { LoopRules = [new("Loop/Look.cs", "Look.Due", "Course")] };
        Assert.Empty(Check(named, Landing, look));

        // A rule outside Loop is its subdomain's; an entry for a rule that is gone is stale, and one that leaves after a subdomain the map lacks is refused.
        Assert.Empty(Check(Src("Course/Rule.cs", "class Rule { static bool Due(int n) => n > 1; }")));
        var stale = Map with { LoopRules = [new("Loop/Look.cs", "Look.Gone", "Gardens")] };
        Assert.Equal(
            [
                "Loop/Look.cs:2 [loop] Look.Due is a rule, a static bool, in Loop: Loop holds no rules, so it moves to its subdomain as a pure function, and the census names it as moving until then",
                "structure.json:0 [map] Look.Gone of Loop/Look.cs is listed as a rule Loop holds, and it is not one there: a move takes its entry off the list",
                "structure.json:0 [map] the rule Look.Gone of Loop/Look.cs leaves Loop after Gardens, which is no subdomain of the map",
            ],
            Check(stale, Landing, look).Order(StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void No_global_using_names_a_subdomain_s_namespace_once_it_has_one()
    {
        var spaced = Map with { Subdomains = [.. Map.Subdomains.Take(1), Map.Subdomains[1] with { Namespace = "Daoris.Driver.Course" }, .. Map.Subdomains.Skip(2)] };
        var usings = ("Platform/Usings.cs", "global using Daoris.Driver.Course;\nglobal using Daoris.Driver.Course.Domain;\nglobal using System.Text;\nnamespace Daoris.Driver;");

        Assert.Equal(
            [
                "Platform/Usings.cs:1 [namespace] a global using of Daoris.Driver.Course: no global using names a subdomain's namespace, so a file's dependencies stay in its own usings",
                "Platform/Usings.cs:2 [namespace] a global using of Daoris.Driver.Course.Domain: no global using names a subdomain's namespace, so a file's dependencies stay in its own usings",
            ],
            Check(spaced, Landing, usings));

        // Before the namespace rows, no subdomain has one, so nothing is refused.
        Assert.Empty(Check(usings));
    }

    private static IReadOnlyList<string> Mirror(IEnumerable<string> tests, StructureMap? map = null) =>
        StructureCheck.Check(map ?? Map with { TestFolders = ["Trees"] }, [Landing], tests).Select(breach => breach.ToString()).ToList();

    [Fact]
    public void A_test_the_map_lists_sits_under_its_subdomain_s_folder_and_one_not_listed_is_not_constrained()
    {
        var listed = Map with
        {
            TestFolders = ["Trees"],
            Tests = new Dictionary<string, string> { ["PluginKitTests.cs"] = "Course", ["HookTests.cs"] = "Course" },
        };

        Assert.Empty(Mirror(["Course/PluginKitTests.cs", "Course/Hooks/HookTests.cs", "LooseTests.cs", "Trees/LandingTests.cs"], listed));

        Assert.Equal(
            [
                "HookTests.cs:0 [mirror] sits at the top of the tests, and the map lists it under Course: a test mirrors its subdomain's folder",
                "Trees/PluginKitTests.cs:0 [mirror] sits under Trees, and the map lists it under Course: a test mirrors its subdomain's folder",
            ],
            Mirror(["HookTests.cs", "Trees/PluginKitTests.cs", "Trees/Other.cs"], listed));
    }

    [Fact]
    public void A_name_the_tests_list_that_is_found_nowhere_is_a_stale_entry_or_a_subdomain_the_map_does_not_declare()
    {
        Assert.Equal(
            ["structure.json:0 [map] GoneTests.cs is listed under Course, and no such test is in the tests' tree: a delete takes its entry off the list"],
            Mirror(["Trees/HookTests.cs"], Map with { TestFolders = ["Trees"], Tests = new Dictionary<string, string> { ["GoneTests.cs"] = "Course" } }));

        Assert.Contains(
            "structure.json:0 [map] HookTests.cs is a test mapped to Gardens, which is no subdomain of the map",
            Mirror(["Gardens/HookTests.cs"], Map with { Tests = new Dictionary<string, string> { ["HookTests.cs"] = "Gardens" } }));
    }

    [Fact]
    public void A_top_level_folder_of_the_tests_is_a_subdomain_s_or_a_listed_one_not_yet_moved_or_fixtures_golden_or_build_output()
    {
        Assert.Empty(Mirror(["Help/HelpTests.cs", "Course/A.cs", "Trees/Old.cs", "fixtures/B.cs", "golden/C.cs", "bin/D.cs", "obj/E.cs", "TestResults/F.cs"]));

        Assert.Equal(
            ["Gardens/RoseTests.cs:0 [mirror] the folder Gardens is no subdomain's, and structure.json does not list it as not yet moved: a test folder is a subdomain's, today's not yet moved, fixtures, golden or a build output"],
            Mirror(["Gardens/RoseTests.cs", "Trees/Old.cs"]));

        // A listed folder with no test in it is stale: its move took the entry off the list.
        Assert.Equal(
            ["structure.json:0 [map] the tests' folder Trees is listed as not yet moved, and none is there: a move takes its entry off the list"],
            Mirror(["Help/HelpTests.cs"]));
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
    public void The_map_names_the_design_s_twenty_one_subdomains_in_their_order_and_each_one_s_folder_and_Domain()
    {
        var map = StructureMap.Read(File.ReadAllText(MapFile()));

        // §14.2: bottom to top, so a subdomain names only its own types and those listed before it.
        Assert.Equal(
            [
                "Platform", "Store", "Settings", "Service", "Install", "Instance", "Events", "Knowledge", "Agents", "Workplace", "Roles",
                "Participants", "Context", "Course", "Work orders", "Review", "Plugins", "Workflows", "Setup", "Help", "Loop",
            ],
            map.Subdomains.Select(subdomain => subdomain.Name));
        Assert.Equal(21, map.Subdomains.Select(subdomain => subdomain.Folder).Distinct().Count());
        Assert.Equal("WorkOrders", map.Subdomains.Single(subdomain => subdomain.Name == "Work orders").Folder);
        // The moves that have landed, bottom up, one row each (FOUNDATION1 mod-i: Platform; mod-ii: Store; mod-iii: Settings; mod-iv: Service); no namespace row: every namespace stays
        // Daoris.Driver (D166 point 4).
        Assert.Equal(["Platform", "Store", "Settings", "Service"], map.Subdomains.Where(subdomain => subdomain.Moved).Select(subdomain => subdomain.Name));
        Assert.All(map.Subdomains, subdomain => Assert.Null(subdomain.Namespace));
        // Loop's folder is the composition root's, and holds no Domain.
        Assert.False(map.Subdomains[^1].Domain);
        Assert.Equal("Loop", map.Subdomains[^1].Name);
        Assert.All(map.Files.Values, name => Assert.Contains(map.Subdomains, subdomain => subdomain.Name == name));
        Assert.All(map.Families, family => Assert.Contains(map.Subdomains, subdomain => subdomain.Name == family.After));
    }

    /// <summary>The one analysis of the driver's own tree the cases over it share: the compile is the gate's cost, measured here.</summary>
    private static readonly Lazy<(StructureMap Map, IReadOnlyList<(string Path, string Text)> Sources, IReadOnlyList<string> Tests, StructureAnalysis Analysis, TimeSpan Took)> Tree = new(() =>
    {
        var map = StructureMap.Read(File.ReadAllText(MapFile()));
        var driver = SourceFiles.Project(SourceFiles.DriverProject);
        var sources = SourceFiles.All(SourceFiles.DriverProject, "*.cs")
            .Select(path => (Path.GetRelativePath(driver, path).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(path)))
            .ToList();
        var tests = SourceFiles.All(SourceFiles.DriverTestsProject, "*.cs")
            .Select(path => Path.GetRelativePath(SourceFiles.Project(SourceFiles.DriverTestsProject), path).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();
        var clock = Stopwatch.StartNew();
        var analysis = StructureAnalysis.Of(sources);
        return (map, sources, tests, analysis, clock.Elapsed);
    });

    [Fact]
    public void The_driver_s_own_tree_keeps_its_structure_with_today_s_exceptions_listed()
    {
        var (map, sources, tests, analysis, _) = Tree.Value;

        var breaches = StructureCheck.Check(map, sources, tests, analysis);

        Assert.True(sources.Count >= 780, $"the gate read only {sources.Count} sources: its walk no longer reaches the driver's tree");
        Assert.True(tests.Count(path => path.StartsWith("Plugins/", StringComparison.Ordinal)) >= 16, "the gate reads no Plugins tests: its walk no longer reaches the tests' tree");
        Assert.True(breaches.Count == 0, "each breaks the driver's structure (the design §14.5):\n" + string.Join('\n', breaches));
    }

    [Fact]
    public void The_driver_s_sources_compile_but_for_the_generated_partial_methods_so_each_name_binds_as_the_compiler_binds_it()
    {
        var errors = Tree.Value.Analysis.Errors();

        // CS8795: a [GeneratedRegex] or [LibraryImport] method, whose body the source generator writes and this compile has none of.
        Assert.Equal(["CS8795"], errors.Keys);
    }

    [Fact]
    public void The_census_reads_what_today_s_identifier_scan_missed_and_none_of_what_it_over_counted()
    {
        var (map, _, _, analysis, took) = Tree.Value;
        var edges = analysis.Edges.Select(edge => (edge.File, edge.Type)).ToHashSet();

        // A qualified reference (the design §14.5, X4-1.4), and a static import's target, which an identifier after a dot or in a `using` skipped.
        Assert.Contains(("Sessions/SessionsCommand.cs", "HolderKey"), edges);
        Assert.Contains(("Accounts/AccountCooling.cs", "AccountFileNodes"), edges);
        Assert.Contains(("Agents/Tools.cs", "Json"), edges);

        // DaorisHome's HomeEntry members, named like higher subdomains' types, count none: a member's occurrence is told by its symbol, never by its spelling.
        string[] members = ["DriverConfig", "Notices", "HelpProposals", "OpinionGates", "AgentOffers", "GitIdentities", "ConfiguredAccess", "PluginState", "UsageHistory", "RuleProposals", "SessionBranches"];
        Assert.Empty(analysis.Edges.Where(edge => edge.File == "Platform/DaorisHome.cs" && members.Contains(edge.Type)));
        // And no file the map places in Platform names a type of Store (§14.1), the home's reads of a file beside its writer being Platform's own (FOUNDATION1 mod-ii).
        Assert.Empty(analysis.Edges.Where(edge => map.Files.GetValueOrDefault(edge.File) == "Platform" && map.Files.TryGetValue("Store/" + edge.Type + ".cs", out _)));

        // Each seam a Domain may not name is a type the driver declares: `DesktopStore` too, since row z-i (FOUNDATION1).
        Assert.Empty(map.Seams.Where(seam => !analysis.DeclaredTypes.Contains(seam)));

        // A record struct is declared, `BoundedLine` among them.
        Assert.Contains("BoundedLine", analysis.DeclaredTypes);

        // The compile is the gate's cost in the fast half: said in the row, held under a minute here.
        Assert.True(took < TimeSpan.FromMinutes(1), $"the compile and the walk took {took.TotalSeconds:F0}s");
    }

    private static string MapFile() =>
        Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "structure.json");
}
