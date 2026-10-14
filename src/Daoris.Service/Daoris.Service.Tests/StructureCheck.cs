using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Service.Tests;

/// <summary>
/// The service core's map (STRUCT5, D166): each module the core holds, every file still at the core's top by name with the
/// module it is to move to, and the tests of a moved module by file name. A trimmed copy of the driver's gate: that one is
/// internal to the driver's tests, and the service's tests may not reference it. Read from <c>fixtures/structure.json</c>.
/// </summary>
internal sealed record StructureMap(IReadOnlyList<string> Modules, IReadOnlyDictionary<string, string> Root)
{
    /// <summary>The tests of a module that has moved, by file name, with the module (or test-only folder) each sits under.</summary>
    public IReadOnlyDictionary<string, string> Tests { get; init; } = new Dictionary<string, string>();

    /// <summary>Folders of the tests that hold a door's tests and no core module's: the MCP door's.</summary>
    public IReadOnlyList<string> TestFolders { get; init; } = [];

    public static StructureMap Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new StructureMap(Names(root.GetProperty("modules")), Strings(root.GetProperty("root")))
        {
            Tests = root.TryGetProperty("tests", out var tests) ? Strings(tests) : new Dictionary<string, string>(),
            TestFolders = root.TryGetProperty("testFolders", out var folders) ? Names(folders) : [],
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
/// The structure gate's rules over the core's sources as (path, text) pairs, each path relative to the core's folder with
/// <c>/</c> between its parts (the code-structure design §5):
/// <list type="bullet">
/// <item><c>top</c>: a file at the core's top is one the map's <c>root</c> list names, and every name on it is still there,
/// so the list only shrinks.</item>
/// <item><c>module</c>: a file in a folder is in a module's folder; the core keeps no <c>Application</c> or <c>Infrastructure</c>
/// folder, and no <c>Domain</c> in this pass.</item>
/// <item><c>mirror</c>: a test the map lists sits under its module's folder, and a test folder is a module's or support.</item>
/// <item><c>composition</c>: nothing outside <c>Composition</c> names a type declared in it, in code only.</item>
/// </list>
/// </summary>
internal static class StructureCheck
{
    /// <summary>The composition root: it may use every module, and no module uses it.</summary>
    public const string Composition = "Composition";

    private static readonly string[] TestSupport = ["fixtures", "golden", "bin", "obj", "TestResults"];

    public static IReadOnlyList<StructureBreach> Check(StructureMap map, IEnumerable<(string Path, string Text)> sources) =>
        Check(map, sources, []);

    public static IReadOnlyList<StructureBreach> Check(StructureMap map, IEnumerable<(string Path, string Text)> sources, IEnumerable<string> tests)
    {
        var breaches = new List<StructureBreach>();
        Mirror(map, tests.ToList(), breaches);
        var files = sources.Select(source => new Source(source.Path, source.Text)).ToList();

        foreach (var (file, module) in map.Root)
        {
            if (!map.Modules.Contains(module))
            {
                breaches.Add(new("structure.json", 0, "map", $"{file} is mapped to {module}, which is no module of the map"));
            }

            if (!files.Any(source => source.Path == file))
            {
                breaches.Add(new(file, 0, "top", "structure.json lists it at the core's top, where it is not: a move takes its entry off the list"));
            }
        }

        foreach (var source in files)
        {
            var parts = source.Path.Split('/');
            if (parts.Length == 1)
            {
                if (!map.Root.ContainsKey(source.Path))
                {
                    breaches.Add(new(source.Path, 1, "top", "a new file at the core's top: put it in its module's folder (structure.json's modules), not here"));
                }
            }
            else if (!map.Modules.Contains(parts[0]))
            {
                breaches.Add(new(source.Path, 1, "module", $"the folder {parts[0]} is no module's: a source goes in its module's folder (structure.json's modules)"));
            }
            else if (parts.Length > 2 && parts[1] is "Application" or "Infrastructure" or "Domain")
            {
                breaches.Add(new(source.Path, 1, "module", $"{parts[0]} holds no {parts[1]} folder: the core's modules keep their files at their top, or in a scope folder"));
            }
        }

        var declared = Declarations(files.Where(source => source.Path.StartsWith(Composition + "/", StringComparison.Ordinal)));
        foreach (var source in files.Where(source => source.Path.Contains('/') && !source.Path.StartsWith(Composition + "/", StringComparison.Ordinal)))
        {
            var named = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match use in Identifier().Matches(source.Code))
            {
                if (declared.TryGetValue(use.Value, out var where) && named.Add(use.Value))
                {
                    breaches.Add(new(source.Path, source.LineOf(use.Index), "composition",
                        $"names {use.Value}, declared in {where.Path}: Composition is the composition root, and no module uses it"));
                }
            }
        }

        return breaches
            .OrderBy(breach => breach.File, StringComparer.Ordinal)
            .ThenBy(breach => breach.Line)
            .ThenBy(breach => breach.Rule, StringComparer.Ordinal)
            .ThenBy(breach => breach.Why, StringComparer.Ordinal)
            .ToList();
    }

    private static void Mirror(StructureMap map, List<string> tests, List<StructureBreach> breaches)
    {
        foreach (var path in tests)
        {
            var parts = path.Split('/');
            if (parts.Length > 1 && !map.Modules.Contains(parts[0]) && !map.TestFolders.Contains(parts[0]) && !TestSupport.Contains(parts[0]))
            {
                breaches.Add(new(path, 0, "mirror", $"the folder {parts[0]} is no module's: a test folder is a module's, a door's, or fixtures, golden or a build output"));
            }
        }

        foreach (var (name, module) in map.Tests)
        {
            if (!map.Modules.Contains(module) && !map.TestFolders.Contains(module))
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

    /// <summary>The types the given sources declare at their top level, by name: a nested type is named through its outer one.</summary>
    private static Dictionary<string, Source> Declarations(IEnumerable<Source> sources)
    {
        var declared = new Dictionary<string, Source>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            var top = BlockNamespace().IsMatch(source.Code) ? 1 : 0;
            var depth = Depths(source.Code);
            foreach (Match declaration in Declaration().Matches(source.Code))
            {
                var name = declaration.Groups["name"].Value;
                if (name is "class" or "struct" || depth[declaration.Index] != top) continue;
                declared.TryAdd(name, source);
            }
        }

        return declared;
    }

    private static int[] Depths(string code)
    {
        var depths = new int[code.Length];
        var depth = 0;
        for (var i = 0; i < code.Length; i++)
        {
            if (code[i] == '}') depth--;
            depths[i] = depth;
            if (code[i] == '{') depth++;
        }

        return depths;
    }

    /// <summary>
    /// A C# source's code: its comments, strings and characters blanked, every offset and line kept, so a word in a sentence,
    /// a literal or a comment is no use of anything. An interpolated string's holes stay code.
    /// </summary>
    public static string CodeOf(string text)
    {
        var code = text.ToCharArray();
        var at = 0;
        Code(text, code, ref at, hole: false);
        return new string(code);
    }

    private static void Code(string text, char[] code, ref int at, bool hole)
    {
        var depth = 0;
        while (at < text.Length)
        {
            var c = text[at];
            if (c == '/' && At(text, at + 1) == '/')
            {
                var end = text.IndexOf('\n', at);
                at = Blank(code, at, end < 0 ? text.Length : end);
            }
            else if (c == '/' && At(text, at + 1) == '*')
            {
                var end = text.IndexOf("*/", at + 2, StringComparison.Ordinal);
                at = Blank(code, at, end < 0 ? text.Length : end + 2);
            }
            else if (c == '\'')
            {
                var end = at + 1;
                while (end < text.Length && text[end] != '\'' && text[end] != '\n') end += text[end] == '\\' ? 2 : 1;
                at = Blank(code, at, Math.Min(end + 1, text.Length));
            }
            else if (Literal(text, at) is { } literal)
            {
                at = Quoted(text, code, at, literal);
            }
            else
            {
                if (c == '{') depth++;
                if (c == '}' && depth-- == 0 && hole) return;
                at++;
            }
        }
    }

    private static (int Dollars, bool Verbatim, int Quotes, int Body)? Literal(string text, int at)
    {
        if (text[at] is not ('"' or '$' or '@')) return null;
        var i = at;
        var dollars = 0;
        var verbatim = false;
        while (At(text, i) == '$') { dollars++; i++; }
        if (At(text, i) == '@') { verbatim = true; i++; }
        while (At(text, i) == '$') { dollars++; i++; }
        if (At(text, i) != '"') return null;
        var quotes = Run(text, i, '"');
        return quotes >= 3 ? (dollars, false, quotes, i + quotes) : (dollars, verbatim, 1, i + 1);
    }

    private static int Quoted(string text, char[] code, int at, (int Dollars, bool Verbatim, int Quotes, int Body) literal)
    {
        var (dollars, verbatim, quotes, i) = literal;
        var from = at;
        while (i < text.Length)
        {
            var c = text[i];
            if (quotes >= 3 && c == '"' && Run(text, i, '"') >= quotes) return Blank(code, from, i + quotes);
            if (quotes == 1 && verbatim && c == '"')
            {
                if (At(text, i + 1) == '"') { i += 2; continue; }
                return Blank(code, from, i + 1);
            }

            if (quotes == 1 && !verbatim)
            {
                if (c == '\\') { i += 2; continue; }
                if (c == '"') return Blank(code, from, i + 1);
                if (c == '\n') return Blank(code, from, i);
            }

            if (dollars > 0 && c == '{')
            {
                var run = Run(text, i, '{');
                var opens = quotes >= 3 ? run >= dollars : run % 2 == 1;
                i += run;
                if (!opens) continue;
                Blank(code, from, i);
                Code(text, code, ref i, hole: true);
                from = i;
                for (var closing = quotes >= 3 ? dollars : 1; closing > 0 && At(text, i) == '}'; closing--) i++;
                continue;
            }

            i++;
        }

        return Blank(code, from, text.Length);
    }

    private static int Blank(char[] code, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            if (code[i] is not ('\n' or '\r')) code[i] = ' ';
        }

        return to;
    }

    private static char At(string text, int i) => i < text.Length ? text[i] : '\0';

    private static int Run(string text, int i, char c)
    {
        var end = i;
        while (end < text.Length && text[end] == c) end++;
        return end - i;
    }

    // Plain regexes, not generated ones: the partials gate (tools/partials.json) allows no partial type outside its list.
    private static readonly Regex IdentifierPattern = new(@"(?<![\w.@])[A-Za-z_]\w*");

    private static readonly Regex DeclarationPattern = new(
        @"\b(?:class|struct|interface|enum|record)\s+(?<name>[A-Za-z_]\w*)|\bdelegate\s+[\w.<>\[\]?, ]+?\s+(?<name>[A-Za-z_]\w*)\s*[<(]");

    /// <summary>A namespace with a body, whose types sit one brace deep; a file-scoped one's sit at the top.</summary>
    private static readonly Regex BlockNamespacePattern = new(@"^\s*namespace\s+[\w.]+\s*\{", RegexOptions.Multiline);

    private static Regex Identifier() => IdentifierPattern;

    private static Regex Declaration() => DeclarationPattern;

    private static Regex BlockNamespace() => BlockNamespacePattern;

    /// <summary>One source, its path and its code.</summary>
    private sealed class Source(string path, string text)
    {
        private int[]? _lines;

        public string Path { get; } = path;

        public string Code { get; } = CodeOf(text);

        public int LineOf(int offset)
        {
            _lines ??= Code.Select((c, i) => (c, i)).Where(pair => pair.c == '\n').Select(pair => pair.i).ToArray();
            var at = Array.BinarySearch(_lines, offset);
            return (at < 0 ? ~at : at) + 1;
        }
    }
}
