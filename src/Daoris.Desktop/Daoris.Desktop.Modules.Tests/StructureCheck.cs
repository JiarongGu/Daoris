using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The desktop modules library's map (STRUCT5a, D166): each module it holds, every file still at its top by name with the
/// module it is to move to, the tests of a moved module by file name, and the shared test fixtures that stay loose at the
/// tests' top. A trimmed copy of the service core's gate (STRUCT5b). Read from <c>fixtures/structure.json</c>.
/// </summary>
internal sealed record StructureMap(IReadOnlyList<string> Modules, IReadOnlyDictionary<string, string> Root)
{
    /// <summary>The tests of a module that has moved, by file name, with the module each sits under.</summary>
    public IReadOnlyDictionary<string, string> Tests { get; init; } = new Dictionary<string, string>();

    /// <summary>The shared fixtures every module's tests use, which stay at the tests' top.</summary>
    public IReadOnlyList<string> Loose { get; init; } = [];

    public static StructureMap Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new StructureMap(Names(root.GetProperty("modules")), Strings(root.GetProperty("root")))
        {
            Tests = root.TryGetProperty("tests", out var tests) ? Strings(tests) : new Dictionary<string, string>(),
            Loose = root.TryGetProperty("loose", out var loose) ? Names(loose) : [],
        };
    }

    private static List<string> Names(JsonElement list) => list.EnumerateArray().Select(name => name.GetString()!).ToList();

    private static Dictionary<string, string> Strings(JsonElement table) =>
        table.EnumerateObject().ToDictionary(entry => entry.Name, entry => entry.Value.GetString()!, StringComparer.Ordinal);
}

/// <summary>One rule a source breaks: where (<see cref="Line"/> 0 is the file as a whole), which rule, and why.</summary>
internal sealed record StructureBreach(string File, int Line, string Rule, string Why)
{
    public override string ToString() => $"{File}:{Line} [{Rule}] {Why}";
}

/// <summary>
/// The structure gate's rules over the library's sources as (path, text) pairs, each path relative to the library's folder
/// with <c>/</c> between its parts (the code-structure design §5):
/// <list type="bullet">
/// <item><c>top</c>: a file at the library's top is one the map's <c>root</c> list names, and every name on it is still there,
/// so the list only shrinks.</item>
/// <item><c>module</c>: a file in a folder is in a declared module's folder, and no <c>Application</c>, <c>Infrastructure</c>
/// or <c>Domain</c> folder is at any depth.</item>
/// <item><c>mirror</c>: a test the map lists sits under its module's folder, and a test folder is a module's or support.</item>
/// <item><c>routes</c>: a <c>*Routes.cs</c> sits in <c>Bridge/Routes/</c>, and a <c>[DriverRoute(</c> mark sits only in one
/// (the desktop README's rule: a bridge domain is one handler class).</item>
/// <item><c>map</c>: the map names declared modules and files that exist.</item>
/// </list>
/// There is no composition rule as the service core has: the routes, the context, the remotes and the session words all
/// name <c>DriverLoop</c>, so no module is the root that the rest may not name.
/// </summary>
internal static class StructureCheck
{
    /// <summary>Where the bridge's handler classes sit.</summary>
    public const string RoutesFolder = "Bridge/Routes";

    private static readonly string[] TestSupport = ["fixtures", "golden", "bin", "obj", "TestResults"];

    private static readonly string[] Layers = ["Application", "Infrastructure", "Domain"];

    // Plain regexes, not generated ones: the partials gate (tools/partials.json) allows no partial type outside its list.
    private static readonly Regex Mark = new(@"\[DriverRoute\(", RegexOptions.Multiline);

    public static IReadOnlyList<StructureBreach> Check(StructureMap map, IEnumerable<(string Path, string Text)> sources) =>
        Check(map, sources, []);

    public static IReadOnlyList<StructureBreach> Check(StructureMap map, IEnumerable<(string Path, string Text)> sources, IEnumerable<string> tests)
    {
        var breaches = new List<StructureBreach>();
        Mirror(map, tests.ToList(), breaches);
        var files = sources.ToList();

        foreach (var (file, module) in map.Root)
        {
            if (!map.Modules.Contains(module))
            {
                breaches.Add(new("structure.json", 0, "map", $"{file} is mapped to {module}, which is no module of the map"));
            }

            if (!files.Any(source => source.Path == file))
            {
                breaches.Add(new(file, 0, "top", "structure.json lists it at the library's top, where it is not: a move takes its entry off the list"));
            }
        }

        foreach (var (path, text) in files)
        {
            var parts = path.Split('/');
            if (parts.Length == 1)
            {
                if (!map.Root.ContainsKey(path))
                {
                    breaches.Add(new(path, 1, "top", "a new file at the library's top: put it in its module's folder (structure.json's modules), not here"));
                }
            }
            else if (!map.Modules.Contains(parts[0]))
            {
                breaches.Add(new(path, 1, "module", $"the folder {parts[0]} is no module's: a source goes in its module's folder (structure.json's modules)"));
            }
            else if (parts[1..^1].FirstOrDefault(part => Layers.Contains(part)) is { } layer)
            {
                breaches.Add(new(path, 1, "module", $"{parts[0]} holds no {layer} folder: the modules keep their files in scope folders named for what they hold"));
            }

            var isRoutes = parts[^1].EndsWith("Routes.cs", StringComparison.Ordinal);
            // A handler class still at the top is the top rule's to name; one in a folder is held here.
            if (isRoutes && parts.Length > 1 && string.Join('/', parts[..^1]) != RoutesFolder)
            {
                breaches.Add(new(path, 1, "routes", $"a *Routes.cs is a bridge domain's handler class, and sits in {RoutesFolder}/"));
            }

            if (!isRoutes && Marked(text) is { } line)
            {
                breaches.Add(new(path, line, "routes", "marks a route with [DriverRoute(, and only a *Routes.cs does: a bridge domain is one handler class"));
            }
        }

        return breaches
            .OrderBy(breach => breach.File, StringComparer.Ordinal)
            .ThenBy(breach => breach.Line)
            .ThenBy(breach => breach.Rule, StringComparer.Ordinal)
            .ThenBy(breach => breach.Why, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The line of the first <c>[DriverRoute(</c> in code (a comment line is none), or null.</summary>
    private static int? Marked(string text)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal) && Mark.IsMatch(lines[i])) return i + 1;
        }

        return null;
    }

    private static void Mirror(StructureMap map, List<string> tests, List<StructureBreach> breaches)
    {
        foreach (var path in tests)
        {
            var parts = path.Split('/');
            if (parts.Length > 1 && !map.Modules.Contains(parts[0]) && !TestSupport.Contains(parts[0]))
            {
                breaches.Add(new(path, 0, "mirror", $"the folder {parts[0]} is no module's: a test folder is a module's, or fixtures, golden or a build output"));
            }
        }

        foreach (var name in map.Loose)
        {
            if (!tests.Contains(name))
            {
                breaches.Add(new("structure.json", 0, "map", $"{name} is listed as a loose fixture at the tests' top, and is not there"));
            }
        }

        foreach (var (name, module) in map.Tests)
        {
            if (!map.Modules.Contains(module))
            {
                breaches.Add(new("structure.json", 0, "map", $"{name} is a test mapped to {module}, which is no module of the map"));
            }

            var found = tests.Where(path => path.Split('/')[^1] == name).ToList();
            if (found.Count == 0)
            {
                breaches.Add(new("structure.json", 0, "map", $"{name} is listed under {module}, and no such test is in the tests' tree: a delete takes its entry off the list"));
            }

            foreach (var path in found.Where(path => path.Split('/')[0] != module || !path.Contains('/')))
            {
                var where = path.Contains('/') ? $"under {path.Split('/')[0]}" : "at the top of the tests";
                breaches.Add(new(path, 0, "mirror", $"sits {where}, and the map lists it under {module}: a test mirrors its module's folder"));
            }
        }
    }
}
