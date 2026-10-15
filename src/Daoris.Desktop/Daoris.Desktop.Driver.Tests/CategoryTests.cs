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

    [Fact]
    public void The_driver_s_own_tests_keep_the_rules()
    {
        var project = SourceFiles.Project(SourceFiles.DriverTestsProject);
        var sources = SourceFiles.All(SourceFiles.DriverTestsProject, "*.cs")
            .Select(path => (Path.GetRelativePath(project, path).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(path)))
            .ToList();

        var marked = sources.Sum(source => ParallelCheck.Marked(source.Item2).Count);
        var breaches = ParallelCheck.Check(sources);

        Assert.True(marked >= 30, $"the check found only {marked} classes run four at once: its walk or its pattern no longer reaches them");
        Assert.True(breaches.Count == 0, "each a class that cannot run four at once beside the others (PROC1b):\n" + string.Join('\n', breaches));
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

        /// <summary>The classes a source's code declares with the <c>Parallel=Git</c> trait: each one's name and its base's.</summary>
        internal static IReadOnlyList<(string Name, string? Base, bool Process)> Marked(string text) =>
            Declared(StructureCheck.CodeOf(text)).Where(each => GitTrait().IsMatch(each.Attributes))
                .Select(each => (each.Name, each.Base, ProcessTrait().IsMatch(each.Attributes))).ToList();

        internal static IReadOnlyList<string> Check(IReadOnlyList<(string Path, string Text)> sources)
        {
            var codes = sources.Select(source => (source.Path, Code: StructureCheck.CodeOf(source.Text))).ToList();
            var where = new Dictionary<string, (string Path, string Code)>(StringComparer.Ordinal);
            foreach (var (path, code) in codes)
            {
                foreach (var declared in Declared(code)) where.TryAdd(declared.Name, (path, code));
            }

            var breaches = new List<string>();
            foreach (var (path, code) in codes)
            {
                foreach (var declared in Declared(code).Where(each => GitTrait().IsMatch(each.Attributes)))
                {
                    if (!ProcessTrait().IsMatch(declared.Attributes))
                    {
                        breaches.Add($"{path}: {declared.Name} runs four at once (Parallel=Git) and is no Process class: the fast half would run it too");
                    }

                    breaches.AddRange(EnvironmentUses(path, code, declared.Name, through: null));
                    var seen = new HashSet<string>(StringComparer.Ordinal) { declared.Name };
                    var parent = declared.Base;
                    while (parent is not null && seen.Add(parent) && where.TryGetValue(parent, out var file))
                    {
                        breaches.AddRange(EnvironmentUses(file.Path, file.Code, declared.Name, through: parent));
                        var name = parent;
                        parent = Declared(file.Code).FirstOrDefault(each => each.Name == name).Base;
                    }
                }
            }

            return breaches;
        }

        private static IEnumerable<string> EnvironmentUses(string path, string code, string name, string? through)
        {
            var via = through is null ? "" : $", through its base {through}";
            return Environmental
                .SelectMany(use => use.Pattern.Matches(code).Select(match => (Line: LineOf(code, match.Index), use.Name)))
                .OrderBy(use => use.Line)
                .Select(use => $"{path}:{use.Line}: {name} runs four at once (Parallel=Git) and reads or sets the process's environment ({use.Name}){via}: the classes beside it share it");
        }

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
    }
}
