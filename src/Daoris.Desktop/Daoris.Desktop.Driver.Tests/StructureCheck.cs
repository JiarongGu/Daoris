using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The driver's map (STRUCT1, D166): each module the design names (§2), with the layers its folder may hold (§3), and every
/// file still at the driver's top, by name, with the module it is to move to. <see cref="Split"/> notes a file that serves two
/// domains: it moves with the one it mostly serves, and is split when its module is layered. Read from
/// <c>fixtures/structure.json</c>.
/// </summary>
internal sealed record StructureMap(IReadOnlyDictionary<string, IReadOnlyList<string>> Modules, IReadOnlyDictionary<string, string> Root)
{
    /// <summary>A file of <see cref="Root"/> that serves two domains, and why.</summary>
    public IReadOnlyDictionary<string, string> Split { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// The tests of a module that has moved, by file name, with the module whose folder each is to sit under in the driver's
    /// tests (STRUCT2a). A test not listed is not constrained.
    /// </summary>
    public IReadOnlyDictionary<string, string> Tests { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// The modules whose layers have landed (STRUCT3): each of their files sits in one of its layer folders, so a file
    /// loose in the module's folder is a breach rather than a file not yet layered.
    /// </summary>
    public IReadOnlyList<string> Layered { get; init; } = [];

    /// <summary>The map as <c>structure.json</c> writes it, its modules in its order; <c>tests</c> and <c>layered</c> are optional.</summary>
    public static StructureMap Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var modules = new OrderedDictionary<string, IReadOnlyList<string>>();
        foreach (var module in root.GetProperty("modules").EnumerateObject())
        {
            modules.Add(module.Name, module.Value.EnumerateArray().Select(layer => layer.GetString()!).ToList());
        }

        return new StructureMap(modules, Strings(root.GetProperty("root")))
        {
            Split = Strings(root.GetProperty("split")),
            Tests = root.TryGetProperty("tests", out var tests) ? Strings(tests) : new Dictionary<string, string>(),
            Layered = root.TryGetProperty("layered", out var layered) ? [.. layered.EnumerateArray().Select(module => module.GetString()!)] : [],
        };
    }

    private static Dictionary<string, string> Strings(JsonElement table) =>
        table.EnumerateObject().ToDictionary(entry => entry.Name, entry => entry.Value.GetString()!, StringComparer.Ordinal);
}

/// <summary>One rule a source breaks: where (<see cref="Line"/> 0 is the file as a whole), which rule, and why.</summary>
internal sealed record StructureBreach(string File, int Line, string Rule, string Why)
{
    public override string ToString() => $"{File}:{Line} [{Rule}] {Why}";
}

/// <summary>
/// The structure gate's rules (the code-structure design §5), over a driver's sources as (path, text) pairs, each path
/// relative to the driver's folder with <c>/</c> between its parts:
/// <list type="bullet">
/// <item><c>top</c>: a file at the driver's top is one the map's <c>root</c> list names, and every name on it is still at the
/// top, so the list only shrinks and a new file lands in its module's folder.</item>
/// <item><c>module</c>: a file in a folder is in a module's folder, and in a layer folder only of a layer that module holds.</item>
/// <item><c>layer</c>: a file of a module the map lists as <c>layered</c> sits in one of its layer folders (STRUCT3a).</item>
/// <item><c>pure</c>: a <c>Domain</c> file does no I/O, by <see cref="Impure"/>'s tokens.</item>
/// <item><c>reach</c>: no file names a type declared in another module's <c>Infrastructure</c> (an <c>Infrastructure</c> may
/// name <c>Platform</c>'s, and <c>Loop</c> composes them all), and a <c>Domain</c> file names none declared in an
/// <c>Application</c> or an <c>Infrastructure</c>.</item>
/// <item><c>loop</c>: nothing outside <c>Loop</c> names a type declared in it.</item>
/// </list>
/// A file still at the top is placed by no folder, so only the first rule reads it; the others read the files in module
/// folders, and the types declared there, by name, in code only: a comment or a string names nothing. A static member a
/// C# 14 <c>extension(T)</c> block declares is named as <c>T.Member</c>, so it is read by that name and belongs to the layer
/// of the file that declares it (STRUCT3a): a Domain type's I/O call kept where its callers make it is still its layer's.
/// </summary>
internal static partial class StructureCheck
{
    /// <summary>The composition root: it may use every module, and no module uses it.</summary>
    public const string Loop = "Loop";

    /// <summary>The shared kernel, whose <c>Infrastructure</c> an <c>Infrastructure</c> may name.</summary>
    public const string Platform = "Platform";

    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure"];

    /// <summary>What a <c>Domain</c> may not name, and why: the world comes in as values, or through a port (design §3).</summary>
    internal static readonly IReadOnlyList<(Regex Token, string Why)> Impure =
    [
        (new(@"(?<![\w.])File\."), "reads or writes a file"),
        (new(@"(?<![\w.])(?:FileInfo|FileStream|FileSystemWatcher)\b"), "reads or writes a file"),
        (new(@"(?<![\w.])Directory\."), "reads or writes a folder"),
        (new(@"(?<![\w.])DirectoryInfo\b"), "reads a folder"),
        (new(@"(?<![\w.])Path\.(?:GetTempPath|GetTempFileName|GetFullPath)\b"), "asks the machine where a path is"),
        (new(@"(?<![\w.])Process\.|\bnew\s+Process\b|(?<![\w.])ProcessStartInfo\b"), "starts or reads a process"),
        (new(@"(?<![\w.])(?:HttpClient|Socket|TcpClient|UdpClient|WebSocket|ClientWebSocket)\b"), "talks to the network"),
        (new(@"(?<![\w.])Environment\."), "reads the machine's environment"),
        (new(@"(?<![\w.])DateTime(?:Offset)?\.(?:Now|UtcNow|Today)\b"), "reads the clock"),
        (new(@"(?<![\w.])(?:TimeProvider\.System|Stopwatch)\b"), "reads the clock"),
        (new(@"(?<![\w.])Console\."), "writes to the terminal"),
        (new(@"(?<![\w.])WorkingTree\."), "runs git"),
    ];

    /// <summary>Every rule the sources break, in order of file, line and rule.</summary>
    public static IReadOnlyList<StructureBreach> Check(StructureMap map, IEnumerable<(string Path, string Text)> sources) =>
        Check(map, sources, []);

    /// <summary>Every rule the sources and the tests' paths (relative to the tests' folder, <c>/</c> between parts) break.</summary>
    public static IReadOnlyList<StructureBreach> Check(StructureMap map, IEnumerable<(string Path, string Text)> sources, IEnumerable<string> tests)
    {
        var breaches = new List<StructureBreach>();
        Mirror(map, tests.ToList(), breaches);
        var files = sources.Select(source => new Source(source.Path, source.Text, Place(map, source.Path))).ToList();

        foreach (var (file, module) in map.Root)
        {
            if (!map.Modules.ContainsKey(module))
            {
                breaches.Add(new("structure.json", 0, "map", $"{file} is mapped to {module}, which is no module of the map"));
            }

            if (!files.Any(source => source.Path == file))
            {
                breaches.Add(new(file, 0, "top", "structure.json lists it at the driver's top, where it is not: a move takes its entry off the list"));
            }
        }

        foreach (var module in map.Layered.Where(module => !map.Modules.ContainsKey(module)))
        {
            breaches.Add(new("structure.json", 0, "map", $"{module} is listed as layered, and is no module of the map"));
        }

        foreach (var source in files)
        {
            var parts = source.Path.Split('/');
            if (parts.Length == 1)
            {
                if (!map.Root.ContainsKey(source.Path))
                {
                    breaches.Add(new(source.Path, 1, "top", "a new file at the driver's top: put it in its module's folder (structure.json's modules), not here"));
                }
            }
            else if (!map.Modules.TryGetValue(parts[0], out var held))
            {
                breaches.Add(new(source.Path, 1, "module", $"the folder {parts[0]} is no module's: a source goes in its module's folder (structure.json's modules)"));
            }
            else if (parts.Length > 2 && Layers.Contains(parts[1]) && !held.Contains(parts[1]))
            {
                var layers = held.Count == 0 ? "none" : string.Join(", ", held);
                breaches.Add(new(source.Path, 1, "module", $"{parts[0]} holds no {parts[1]} layer: its layers are {layers}"));
            }
            else if (map.Layered.Contains(parts[0]) && !(parts.Length > 2 && held.Contains(parts[1])))
            {
                breaches.Add(new(source.Path, 1, "layer", $"{parts[0]} is layered: each of its files sits in its {string.Join(", ", held.SkipLast(1))} or {held[^1]}"));
            }
        }

        var placed = files.Where(source => source.Place is not null).ToList();
        var declared = Declarations(placed);
        foreach (var (name, where) in ExtensionMembers(placed)) declared[name] = where;
        foreach (var source in placed)
        {
            var (module, layer) = source.Place!.Value;
            if (layer == "Domain")
            {
                foreach (var (token, why) in Impure)
                {
                    foreach (Match use in token.Matches(source.Code))
                    {
                        breaches.Add(new(source.Path, source.LineOf(use.Index), "pure",
                            $"{use.Value.Trim()} {why}: a Domain is pure; it takes the world as values or through a port"));
                    }
                }
            }

            var named = new HashSet<string>(StringComparer.Ordinal);
            void Named(string name, int at)
            {
                if (!declared.TryGetValue(name, out var where) || where.Any(place => place.Path == source.Path) || !named.Add(name)) return;
                var line = source.LineOf(at);
                foreach (var place in where)
                {
                    var (theirs, theirLayer) = place.Place!.Value;
                    var why = Reach(module, layer, theirs, theirLayer);
                    if (why is not null)
                    {
                        breaches.Add(new(source.Path, line, why.Value.Rule, $"names {name}, declared in {place.Path}: {why.Value.Why}"));
                        break;
                    }
                }
            }

            foreach (Match use in Identifier().Matches(source.Code)) Named(use.Value, use.Index);
            foreach (Match use in MemberUse().Matches(source.Code)) Named($"{use.Groups["type"].Value}.{use.Groups["member"].Value}", use.Index);
        }

        return breaches
            .OrderBy(breach => breach.File, StringComparer.Ordinal)
            .ThenBy(breach => breach.Line)
            .ThenBy(breach => breach.Rule, StringComparer.Ordinal)
            .ThenBy(breach => breach.Why, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Folders of the tests' project that are no module's: fixtures and the build's own output.</summary>
    private static readonly string[] TestSupport = ["fixtures", "golden", "bin", "obj", "TestResults"];

    /// <summary>The <c>mirror</c> rule: a listed test sits under its module's folder, and a test folder is a module's or support.</summary>
    private static void Mirror(StructureMap map, List<string> tests, List<StructureBreach> breaches)
    {
        foreach (var path in tests)
        {
            var parts = path.Split('/');
            if (parts.Length > 1 && !map.Modules.ContainsKey(parts[0]) && !TestSupport.Contains(parts[0]))
            {
                breaches.Add(new(path, 0, "mirror", $"the folder {parts[0]} is no module's: a test folder is a module's, or fixtures, golden or a build output"));
            }
        }

        foreach (var (name, module) in map.Tests)
        {
            if (!map.Modules.ContainsKey(module))
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

    /// <summary>Why a file of one module and layer may not name a type of another, or null when it may.</summary>
    private static (string Rule, string Why)? Reach(string module, string? layer, string theirs, string? theirLayer)
    {
        if (theirs == Loop && module != Loop) return ("loop", "Loop is the composition root, and no module uses it");
        if (layer == "Domain" && theirLayer is "Application" or "Infrastructure")
        {
            return ("reach", "a Domain names nothing of an Application or an Infrastructure");
        }

        var composes = module == Loop || (theirs == Platform && layer == "Infrastructure");
        if (theirs != module && theirLayer == "Infrastructure" && !composes)
        {
            return ("reach", "a module reaches another through its Domain and Application, never its Infrastructure");
        }

        return null;
    }

    /// <summary>The module and layer a path's folders place it in, or null for a file at the top or in no module's folder.</summary>
    private static (string Module, string? Layer)? Place(StructureMap map, string path)
    {
        var parts = path.Split('/');
        if (parts.Length == 1 || !map.Modules.TryGetValue(parts[0], out var held)) return null;
        return (parts[0], parts.Length > 2 && held.Contains(parts[1]) ? parts[1] : null);
    }

    /// <summary>The types each placed source declares at its top level, by name: a nested type is named through its outer one.</summary>
    private static Dictionary<string, List<Source>> Declarations(IEnumerable<Source> sources)
    {
        var declared = new Dictionary<string, List<Source>>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            var top = BlockNamespace().IsMatch(source.Code) ? 1 : 0;
            var depth = Depths(source.Code);
            foreach (Match declaration in Declaration().Matches(source.Code))
            {
                var name = declaration.Groups["name"].Value;
                if (name is "class" or "struct" || depth[declaration.Index] != top) continue;
                if (!declared.TryGetValue(name, out var where)) declared[name] = where = [];
                if (!where.Contains(source)) where.Add(source);
            }
        }

        return declared;
    }

    /// <summary>
    /// The static members each placed source's <c>extension(T)</c> blocks declare, by <c>T.Member</c> (STRUCT3a): a call names
    /// one through its receiver, so by the type's name alone it would be the receiver's layer's, not its own. An instance
    /// extension member is called on a value, which no name says, and is not read.
    /// </summary>
    private static Dictionary<string, List<Source>> ExtensionMembers(IEnumerable<Source> sources)
    {
        var declared = new Dictionary<string, List<Source>>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            var code = source.Code;
            var depth = Depths(code);
            foreach (Match block in Extension().Matches(code))
            {
                var open = block.Index + block.Length - 1;
                var close = open + 1;
                while (close < code.Length && !(code[close] == '}' && depth[close] == depth[open])) close++;
                foreach (Match member in StaticMember().Matches(code, open + 1))
                {
                    if (member.Index >= close) break;
                    if (depth[member.Index] != depth[open] + 1) continue;
                    var name = $"{block.Groups["receiver"].Value}.{member.Groups["name"].Value}";
                    if (!declared.TryGetValue(name, out var where)) declared[name] = where = [];
                    if (!where.Contains(source)) where.Add(source);
                }
            }
        }

        return declared;
    }

    /// <summary>The brace depth at each offset of a source's code.</summary>
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

    /// <summary>Walks code from <paramref name="at"/> to the end, or, in a hole, to the brace that closes it.</summary>
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

    /// <summary>A string literal's opening at an offset: its <c>$</c>s, whether it is verbatim, its quotes, and where its text starts.</summary>
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

    /// <summary>Blanks a string literal but its interpolation holes, and returns the offset after it.</summary>
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

    [GeneratedRegex(@"(?<![\w.@])[A-Za-z_]\w*")]
    private static partial Regex Identifier();

    [GeneratedRegex(@"\b(?:class|struct|interface|enum|record)\s+(?<name>[A-Za-z_]\w*)|\bdelegate\s+[\w.<>\[\]?, ]+?\s+(?<name>[A-Za-z_]\w*)\s*[<(]")]
    private static partial Regex Declaration();

    /// <summary>A use of a member through a type's name, <c>T.Member</c>, which a static extension member is named by.</summary>
    [GeneratedRegex(@"(?<![\w.@])(?<type>[A-Za-z_]\w*)\s*\.\s*(?<member>[A-Za-z_]\w*)")]
    private static partial Regex MemberUse();

    /// <summary>A C# 14 extension block's head, up to its opening brace: <c>extension(T)</c> or <c>extension(T value)</c>.</summary>
    [GeneratedRegex(@"\bextension\s*(?:<[^<>]*>\s*)?\(\s*(?<receiver>[A-Za-z_]\w*)\s*(?:<[^()]*>\s*)?(?:[A-Za-z_]\w*\s*)?\)\s*(?:where\b[^{]*)?\{")]
    private static partial Regex Extension();

    /// <summary>A static member's declaration, up to its name and what follows it: a method's <c>(</c>, a property's <c>=&gt;</c> or <c>{</c>.</summary>
    [GeneratedRegex(@"\bstatic\b[^;{}()=]*?\b(?<name>[A-Za-z_]\w*)\s*(?:<[^<>(){};]*>\s*)?(?:\(|=>|\{)")]
    private static partial Regex StaticMember();

    /// <summary>A namespace with a body, whose types sit one brace deep; a file-scoped one's sit at the top.</summary>
    [GeneratedRegex(@"^\s*namespace\s+[\w.]+\s*\{", RegexOptions.Multiline)]
    private static partial Regex BlockNamespace();

    /// <summary>One source, its code, and the module and layer its folders place it in.</summary>
    private sealed class Source(string path, string text, (string Module, string? Layer)? place)
    {
        private int[]? _lines;

        public string Path { get; } = path;

        public string Code { get; } = CodeOf(text);

        public (string Module, string? Layer)? Place { get; } = place;

        public int LineOf(int offset)
        {
            _lines ??= Code.Select((c, i) => (c, i)).Where(pair => pair.c == '\n').Select(pair => pair.i).ToArray();
            var at = Array.BinarySearch(_lines, offset);
            return (at < 0 ? ~at : at) + 1;
        }
    }
}
