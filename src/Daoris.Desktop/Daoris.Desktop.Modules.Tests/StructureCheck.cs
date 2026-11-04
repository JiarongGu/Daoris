using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The desktop modules library's map (D166): each module it holds and the shared test fixtures that stay loose at the tests'
/// top. A file's module, and a test's, is its first folder, so no file is listed. A trimmed copy of the service core's gate.
/// Read from <c>fixtures/structure.json</c>.
/// </summary>
internal sealed record StructureMap(IReadOnlyList<string> Modules)
{
    /// <summary>The shared fixtures every module's tests use, which stay at the tests' top.</summary>
    public IReadOnlyList<string> Loose { get; init; } = [];

    public static StructureMap Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new StructureMap(Names(root.GetProperty("modules")))
        {
            Loose = root.TryGetProperty("loose", out var loose) ? Names(loose) : [],
        };
    }

    private static List<string> Names(JsonElement list) => list.EnumerateArray().Select(name => name.GetString()!).ToList();
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
/// <item><c>top</c>: no file sits at the library's top.</item>
/// <item><c>module</c>: a file in a folder is in a declared module's folder, and no <c>Application</c>, <c>Infrastructure</c>
/// or <c>Domain</c> folder is at any depth.</item>
/// <item><c>mirror</c>: a test folder is a module's or support.</item>
/// <item><c>routes</c>: a <c>*Routes.cs</c> sits in <c>Bridge/Routes/</c>, and a <c>[DriverRoute(</c> mark sits only in one
/// (the desktop README's rule: a bridge domain is one handler class).</item>
/// <item><c>map</c>: each loose fixture the map names is there.</item>
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
        foreach (var (path, text) in sources)
        {
            var parts = path.Split('/');
            if (parts.Length == 1)
            {
                breaches.Add(new(path, 1, "top", "a new file at the library's top: put it in its module's folder (structure.json's modules), not here"));
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
    }
}
