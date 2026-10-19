using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The Process half's two runs (PROC1b, MOD8): a class marked <c>Parallel=Git</c> runs four at once beside the others so
/// marked (<c>git.process.runsettings</c>), so it is still a Process class, which the fast half leaves out, and neither it
/// nor a base class it inherits here sets or reads a variable of the process's environment, which every class beside it
/// shares. The rules are held on sources made in memory, one case each, and then over this project's own tree.
/// </summary>
public sealed partial class CategoryTests
{
    private const string Both = "[Trait(Category.Name, Category.Process)]\n[Trait(Category.Parallel, Category.Git)]\n";

    [Fact]
    public void A_class_run_four_at_once_is_a_Process_class()
    {
        Assert.Empty(ParallelCheck.Check([("Trees/A.cs", "namespace N;\n" + Both + "public sealed class ATests { }")]));
        // A Process class run one at a time is no concern of this check, and neither is a sentence naming the trait.
        Assert.Empty(ParallelCheck.Check([
            ("B.cs", "namespace N;\n[Trait(Category.Name, Category.Process)]\npublic sealed class BTests { }"),
            ("C.cs", "namespace N;\n/// <c>[Trait(Category.Parallel, Category.Git)]</c>\npublic sealed class CTests { }"),
        ]));

        Assert.Equal(
            ["Trees/D.cs: DTests runs four at once (Parallel=Git) and is no Process class: the fast half would run it too"],
            ParallelCheck.Check([("Trees/D.cs", "namespace N;\n[Trait(Category.Parallel, Category.Git)]\npublic sealed class DTests { }")]));
    }

    [Fact]
    public void A_class_run_four_at_once_sets_and_reads_no_environment_variable()
    {
        const string reads = """
            namespace N;
            [Trait(Category.Name, Category.Process)]
            [Trait(Category.Parallel, Category.Git)]
            public sealed class ETests
            {
                // Environment.SetEnvironmentVariable in a comment, and "Environment.CurrentDirectory" in a sentence, are no use.
                private readonly string? _path = Environment.GetEnvironmentVariable("PATH");
                public void Set() => Environment.SetEnvironmentVariable("DAORIS_HOME", "x");
                public void Move() => Directory.SetCurrentDirectory("x");
            }
            """;

        Assert.Equal(
            [
                "E.cs:7: ETests runs four at once (Parallel=Git) and reads or sets the process's environment (Environment.GetEnvironmentVariable): the classes beside it share it",
                "E.cs:8: ETests runs four at once (Parallel=Git) and reads or sets the process's environment (Environment.SetEnvironmentVariable): the classes beside it share it",
                "E.cs:9: ETests runs four at once (Parallel=Git) and reads or sets the process's environment (Directory.SetCurrentDirectory): the classes beside it share it",
            ],
            ParallelCheck.Check([("E.cs", reads)]));

        // The same reads in a class run one at a time are its own to make.
        Assert.Empty(ParallelCheck.Check([("F.cs", reads.Replace("[Trait(Category.Parallel, Category.Git)]", "").Replace("ETests", "FTests"))]));
    }

    [Fact]
    public void A_base_class_it_inherits_here_is_read_as_its_own()
    {
        (string, string) fixture = ("Trees/GFixture.cs", "namespace N;\npublic abstract class GFixture\n{\n    protected GFixture() => Environment.SetEnvironmentVariable(\"X\", \"1\");\n}");

        Assert.Equal(
            ["Trees/GFixture.cs:4: GTests runs four at once (Parallel=Git) and reads or sets the process's environment (Environment.SetEnvironmentVariable), through its base GFixture: the classes beside it share it"],
            ParallelCheck.Check([fixture, ("Trees/G.cs", "namespace N;\n" + Both + "public sealed class GTests : GFixture { }")]));

        // A base this project does not declare is the product's or the framework's, and no test's to read.
        Assert.Empty(ParallelCheck.Check([("Trees/H.cs", "namespace N;\n" + Both + "public sealed class HTests : IDisposable { public void Dispose() { } }")]));
    }

    /// <summary>
    /// PROCPAR1: the Process half runs its classes several at once too (<c>process.runsettings</c>), so a Process class there sets no
    /// variable of the process's environment and measures nothing of the process as a whole, unless it runs alone, after the rest.
    /// Reading the environment is its own to do there: no class beside it moves it.
    /// </summary>
    [Fact]
    public void A_Process_class_run_beside_the_others_sets_no_environment_and_measures_no_whole_process()
    {
        const string shares = """
            namespace N;
            [Trait(Category.Name, Category.Process)]
            public sealed class JTests
            {
                private readonly string? _path = Environment.GetEnvironmentVariable("PATH");
                public void Set() => Environment.SetEnvironmentVariable("DAORIS_HOME", "x");
                public void Move() => Environment.CurrentDirectory = "x";
                public int Held() => Process.GetCurrentProcess().HandleCount;
                public bool Same() => Environment.CurrentDirectory == "x";
            }
            """;

        Assert.Equal(
            [
                "J.cs:6: JTests runs beside the other Process classes and sets the process's environment (Environment.SetEnvironmentVariable): they share it, so it runs alone, in [Collection(Alone.Name)]",
                "J.cs:7: JTests runs beside the other Process classes and sets the process's environment (Environment.CurrentDirectory =): they share it, so it runs alone, in [Collection(Alone.Name)]",
                "J.cs:8: JTests runs beside the other Process classes and measures the whole process (HandleCount): they share it, so it runs alone, in [Collection(Alone.Name)]",
            ],
            ParallelCheck.Beside([("J.cs", shares)]));

        // Alone, or in the PATH's collection, which runs alone too, it is its own to set and to measure.
        Assert.Empty(ParallelCheck.Beside([("K.cs", shares.Replace("JTests", "KTests").Replace("[Trait", "[Collection(Alone.Name)]\n[Trait"))]));
        Assert.Empty(ParallelCheck.Beside([("L.cs", shares.Replace("JTests", "LTests").Replace("[Trait", "[Collection(ProcessPath.Name)]\n[Trait"))]));

        // The fast half is another run's, and so is the git half, whose own rule reads its classes.
        Assert.Empty(ParallelCheck.Beside([("M.cs", shares.Replace("JTests", "MTests").Replace("[Trait(Category.Name, Category.Process)]", ""))]));
        Assert.Empty(ParallelCheck.Beside([("O.cs", shares.Replace("JTests", "OTests").Replace("[Trait(Category.Name, Category.Process)]\n", Both))]));
    }

    [Fact]
    public void A_base_class_a_Process_class_inherits_here_is_read_as_its_own_too()
    {
        (string, string) fixture = ("Trees/PFixture.cs", "namespace N;\npublic abstract class PFixture\n{\n    protected PFixture() => Directory.SetCurrentDirectory(\"x\");\n}");

        Assert.Equal(
            ["Trees/PFixture.cs:4: PTests runs beside the other Process classes and sets the process's environment (Directory.SetCurrentDirectory), through its base PFixture: they share it, so it runs alone, in [Collection(Alone.Name)]"],
            ParallelCheck.Beside([fixture, ("Trees/P.cs", "namespace N;\n[Trait(Category.Name, Category.Process)]\npublic sealed class PTests : PFixture { }")]));
    }

    /// <summary>
    /// What makes a class alone is its collection's definition: xUnit runs a collection that disables parallelization after every
    /// parallel one has ended, one at a time, with nothing beside it.
    /// </summary>
    [Fact]
    public void The_collections_run_alone_run_after_the_rest_with_nothing_beside_them()
    {
        foreach (var collection in new[] { typeof(Alone), typeof(ProcessPath) })
        {
            var definition = (CollectionDefinitionAttribute)Attribute.GetCustomAttribute(collection, typeof(CollectionDefinitionAttribute))!;
            Assert.True(definition.DisableParallelization, $"{collection.Name} runs beside the parallel collections");
        }
    }

    [Fact]
    public void The_driver_s_own_tests_keep_the_rules()
    {
        var project = SourceFiles.Project(SourceFiles.DriverTestsProject);
        var sources = SourceFiles.All(SourceFiles.DriverTestsProject, "*.cs")
            .Select(path => (Path.GetRelativePath(project, path).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(path)))
            .ToList();

        var marked = sources.Sum(source => ParallelCheck.Marked(source.Item2).Count);
        var breaches = ParallelCheck.Check(sources);
        var beside = ParallelCheck.Beside(sources);

        Assert.True(marked >= 30, $"the check found only {marked} classes run four at once: its walk or its pattern no longer reaches them");
        Assert.True(breaches.Count == 0, "each a class that cannot run four at once beside the others (PROC1b):\n" + string.Join('\n', breaches));
        Assert.True(beside.Count == 0, "each a Process class that cannot run beside the others (PROCPAR1):\n" + string.Join('\n', beside));
    }

    /// <summary>The check itself: a source is a path relative to the tests' project and its text.</summary>
    internal static partial class ParallelCheck
    {
        private static readonly (string Name, Regex Pattern)[] Environmental =
        [
            ("Environment.SetEnvironmentVariable", Word("Environment\\.SetEnvironmentVariable")),
            ("Environment.GetEnvironmentVariables", Word("Environment\\.GetEnvironmentVariables")),
            ("Environment.GetEnvironmentVariable", Word("Environment\\.GetEnvironmentVariable")),
            ("Environment.ExpandEnvironmentVariables", Word("Environment\\.ExpandEnvironmentVariables")),
            ("Environment.CurrentDirectory", Word("Environment\\.CurrentDirectory")),
            ("Directory.SetCurrentDirectory", Word("Directory\\.SetCurrentDirectory")),
        ];

        /// <summary>
        /// What a Process class beside the others may not touch (PROCPAR1): the environment it sets, which every class beside it reads
        /// and every child they start inherits, and the whole process it measures, whose handles every class beside it moves.
        /// </summary>
        private static readonly (string Name, string What, Regex Pattern)[] Shared =
        [
            ("Environment.SetEnvironmentVariable", "sets the process's environment", Word("Environment\\.SetEnvironmentVariable")),
            ("Directory.SetCurrentDirectory", "sets the process's environment", Word("Directory\\.SetCurrentDirectory")),
            ("Environment.CurrentDirectory =", "sets the process's environment", new(@"\bEnvironment\.CurrentDirectory\s*=(?!=)", RegexOptions.CultureInvariant)),
            ("HandleCount", "measures the whole process", Word("HandleCount")),
            ("NonpagedSystemMemorySize64", "measures the whole process", Word("NonpagedSystemMemorySize64")),
        ];

        /// <summary>The classes a source's code declares with the <c>Parallel=Git</c> trait: each one's name and its base's.</summary>
        internal static IReadOnlyList<(string Name, string? Base, bool Process)> Marked(string text) =>
            Declared(StructureCheck.CodeOf(text)).Where(each => GitTrait().IsMatch(each.Attributes))
                .Select(each => (each.Name, each.Base, ProcessTrait().IsMatch(each.Attributes))).ToList();

        internal static IReadOnlyList<string> Check(IReadOnlyList<(string Path, string Text)> sources)
        {
            var breaches = new List<string>();
            foreach (var (path, code, declared, walk) in Walk(sources, attributes => GitTrait().IsMatch(attributes)))
            {
                if (!ProcessTrait().IsMatch(declared.Attributes))
                {
                    breaches.Add($"{path}: {declared.Name} runs four at once (Parallel=Git) and is no Process class: the fast half would run it too");
                }

                foreach (var (file, text, through) in walk)
                {
                    var via = through is null ? "" : $", through its base {through}";
                    breaches.AddRange(Uses(text, Environmental.Select(use => (use.Name, use.Pattern))).Select(use =>
                        $"{file}:{use.Line}: {declared.Name} runs four at once (Parallel=Git) and reads or sets the process's environment ({use.Name}){via}: the classes beside it share it"));
                }
            }

            return breaches;
        }

        /// <summary>
        /// The Process half's own run (PROCPAR1): a Process class neither marked <c>Parallel=Git</c> nor in a collection that runs alone
        /// runs beside the other Process classes, so neither it nor a base it inherits here touches what they share.
        /// </summary>
        internal static IReadOnlyList<string> Beside(IReadOnlyList<(string Path, string Text)> sources)
        {
            var breaches = new List<string>();
            var beside = Walk(sources, attributes =>
                ProcessTrait().IsMatch(attributes) && !GitTrait().IsMatch(attributes) && !AloneCollection().IsMatch(attributes));
            foreach (var (_, _, declared, walk) in beside)
            {
                foreach (var (file, text, through) in walk)
                {
                    var via = through is null ? "" : $", through its base {through}";
                    breaches.AddRange(Uses(text, Shared.Select(use => (use.Name, use.Pattern))).Select(use =>
                        $"{file}:{use.Line}: {declared.Name} runs beside the other Process classes and {Shared.First(each => each.Name == use.Name).What} ({use.Name}){via}: they share it, so it runs alone, in [Collection(Alone.Name)]"));
                }
            }

            return breaches;
        }

        /// <summary>
        /// Each class whose attributes <paramref name="selected"/> takes, with the sources it is read through: its own, then each base
        /// this project declares, nearest first.
        /// </summary>
        private static IEnumerable<(string Path, string Code, (string Name, string? Base, string Attributes) Declared, IReadOnlyList<(string Path, string Code, string? Through)> Walk)> Walk(
            IReadOnlyList<(string Path, string Text)> sources, Func<string, bool> selected)
        {
            var codes = sources.Select(source => (source.Path, Code: StructureCheck.CodeOf(source.Text))).ToList();
            var where = new Dictionary<string, (string Path, string Code)>(StringComparer.Ordinal);
            foreach (var (path, code) in codes)
            {
                foreach (var declared in Declared(code)) where.TryAdd(declared.Name, (path, code));
            }

            foreach (var (path, code) in codes)
            {
                foreach (var declared in Declared(code).Where(each => selected(each.Attributes)))
                {
                    var walk = new List<(string Path, string Code, string? Through)> { (path, code, null) };
                    var seen = new HashSet<string>(StringComparer.Ordinal) { declared.Name };
                    var parent = declared.Base;
                    while (parent is not null && seen.Add(parent) && where.TryGetValue(parent, out var file))
                    {
                        walk.Add((file.Path, file.Code, parent));
                        var name = parent;
                        parent = Declared(file.Code).FirstOrDefault(each => each.Name == name).Base;
                    }

                    yield return (path, code, declared, walk);
                }
            }
        }

        private static IEnumerable<(int Line, string Name)> Uses(string code, IEnumerable<(string Name, Regex Pattern)> uses) =>
            uses.SelectMany(use => use.Pattern.Matches(code).Select(match => (Line: LineOf(code, match.Index), use.Name))).OrderBy(use => use.Line);

        /// <summary>Each class a code declares, with its base and the attributes between the statement before it and its name.</summary>
        private static IEnumerable<(string Name, string? Base, string Attributes)> Declared(string code)
        {
            foreach (Match match in ClassDeclaration().Matches(code))
            {
                var start = match.Index == 0 ? 0 : code.LastIndexOfAny([';', '{', '}'], match.Index - 1) + 1;
                yield return (match.Groups["name"].Value, match.Groups["base"].Success ? match.Groups["base"].Value : null, code[start..match.Index]);
            }
        }

        private static int LineOf(string code, int index) => code.AsSpan(0, index).Count('\n') + 1;

        private static Regex Word(string pattern) => new($"\\b{pattern}\\b", RegexOptions.CultureInvariant);

        [GeneratedRegex(@"\bclass\s+(?<name>\w+)(?:\s*<[^>]*>)?(?:\s*\([^)]*\))?(?:\s*:\s*(?<base>\w+))?", RegexOptions.CultureInvariant)]
        private static partial Regex ClassDeclaration();

        [GeneratedRegex(@"\bTrait\s*\(\s*Category\.Parallel\s*,\s*Category\.Git\s*\)", RegexOptions.CultureInvariant)]
        private static partial Regex GitTrait();

        [GeneratedRegex(@"\bTrait\s*\(\s*Category\.Name\s*,\s*Category\.Process\s*\)", RegexOptions.CultureInvariant)]
        private static partial Regex ProcessTrait();

        [GeneratedRegex(@"\bCollection\s*\(\s*(?:Alone|ProcessPath)\.Name\s*\)", RegexOptions.CultureInvariant)]
        private static partial Regex AloneCollection();
    }
}
