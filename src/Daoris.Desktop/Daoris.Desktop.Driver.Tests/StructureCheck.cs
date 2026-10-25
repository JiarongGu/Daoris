using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One subdomain of the driver's map (FOUNDATION1, D212; the design §14.2): its folder, whether it holds a <c>Domain/</c>, whether its
/// move has landed, and, from its namespace row on, its namespace. The map lists them bottom to top.
/// </summary>
internal sealed record Subdomain(string Name, string Folder, bool Domain)
{
    /// <summary>Whether the subdomain's move row has landed: every file placed in it sits in its folder.</summary>
    public bool Moved { get; init; }

    /// <summary>The namespace its files are in once its namespace row has landed, or none.</summary>
    public string? Namespace { get; init; }
}

/// <summary>
/// An edge family of the census: the upward edges one cut takes off together, each an exact edge, a file and the type it names, and the
/// subdomain row after which the cut lands (O4-6, X4-1.4).
/// </summary>
internal sealed record EdgeFamily(string Name, string After, IReadOnlyDictionary<string, IReadOnlyList<string>> Edges);

/// <summary>A judgement Loop still holds today, named as moving: the file, <c>Type.Member</c>, and the subdomain row after which it leaves.</summary>
internal sealed record LoopRule(string File, string Member, string After);

/// <summary>
/// The driver's map, read from <c>fixtures/structure.json</c> (the design §14.5): the subdomains in their order, every file of the driver
/// placed in one of them, the census of today's exceptions to the rules (the edges against the order, the Domain files' crossings, the rules
/// Loop holds) and the tests' folders not yet moved.
/// </summary>
internal sealed record StructureMap(IReadOnlyList<Subdomain> Subdomains, IReadOnlyDictionary<string, string> Files)
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoCrossings = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>The I/O seams a <c>Domain/</c> file names none of: types of the world, which it takes as values or through a port.</summary>
    public IReadOnlyList<string> Seams { get; init; } = [];

    /// <summary>The census of the edges against the order, by family.</summary>
    public IReadOnlyList<EdgeFamily> Families { get; init; } = [];

    /// <summary>The census of the <c>Domain/</c> files' crossings: a file, and the seam types it names or <c>async</c>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Pure { get; init; } = NoCrossings;

    /// <summary>The census of the judgements Loop holds, each to leave.</summary>
    public IReadOnlyList<LoopRule> LoopRules { get; init; } = [];

    /// <summary>Today's test folders, listed as not yet moved: the mirror rule passes over them, each mod row moving its subdomain's tests.</summary>
    public IReadOnlyList<string> TestFolders { get; init; } = [];

    /// <summary>
    /// The tests of a subdomain that has moved, by file name, with the subdomain whose folder each is to sit under in the driver's tests
    /// (STRUCT2a). A test not listed is not constrained.
    /// </summary>
    public IReadOnlyDictionary<string, string> Tests { get; init; } = new Dictionary<string, string>();

    /// <summary>The map as <c>structure.json</c> writes it, its subdomains in its order.</summary>
    public static StructureMap Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var subdomains = root.GetProperty("subdomains").EnumerateArray()
            .Select(each => new Subdomain(each.GetProperty("name").GetString()!, each.GetProperty("folder").GetString()!, each.GetProperty("domain").GetBoolean())
            {
                Moved = each.TryGetProperty("moved", out var moved) && moved.GetBoolean(),
                Namespace = each.TryGetProperty("namespace", out var space) ? space.GetString() : null,
            })
            .ToList();

        return new StructureMap(subdomains, Strings(root.GetProperty("files")))
        {
            Seams = root.GetProperty("seams").EnumerateArray().Select(each => each.GetString()!).ToList(),
            Families = root.GetProperty("edges").EnumerateArray()
                .Select(each => new EdgeFamily(each.GetProperty("family").GetString()!, each.GetProperty("after").GetString()!, Lists(each.GetProperty("files"))))
                .ToList(),
            Pure = Lists(root.GetProperty("pure")),
            LoopRules = root.GetProperty("loopRules").EnumerateArray()
                .Select(each => new LoopRule(each.GetProperty("file").GetString()!, each.GetProperty("member").GetString()!, each.GetProperty("after").GetString()!))
                .ToList(),
            TestFolders = root.GetProperty("testFolders").EnumerateArray().Select(each => each.GetString()!).ToList(),
            Tests = root.TryGetProperty("tests", out var tests) ? Strings(tests) : new Dictionary<string, string>(),
        };
    }

    private static Dictionary<string, string> Strings(JsonElement table) =>
        table.EnumerateObject().ToDictionary(entry => entry.Name, entry => entry.Value.GetString()!, StringComparer.Ordinal);

    private static Dictionary<string, IReadOnlyList<string>> Lists(JsonElement table) =>
        table.EnumerateObject().ToDictionary(
            entry => entry.Name,
            entry => (IReadOnlyList<string>)entry.Value.EnumerateArray().Select(each => each.GetString()!).ToList(),
            StringComparer.Ordinal);
}

/// <summary>One rule a source breaks: where (<see cref="Line"/> 0 is the file as a whole), which rule, and why.</summary>
internal sealed record StructureBreach(string File, int Line, string Rule, string Why)
{
    public override string ToString() => $"{File}:{Line} [{Rule}] {Why}";
}

/// <summary>
/// The structure gate's rules (the design §14.5; D166, D212), over a driver's sources as (path, text) pairs, each path relative to the
/// driver's folder with <c>/</c> between its parts:
/// <list type="bullet">
/// <item><c>place</c>: every source is placed in one subdomain, by <c>structure.json</c>'s list of files or, for new code, by the folder
/// of the subdomain it sits in; a source in no subdomain's folder that the list does not place fails, and so does an entry for a file that
/// is not there. A subdomain whose move has landed holds every file placed in it, in its folder.</item>
/// <item><c>module</c>: a subdomain's layer folder is a <c>Domain</c>, in a subdomain the map lets hold one; an <c>Application</c> or an
/// <c>Infrastructure</c> folder is refused (FLAT1a).</item>
/// <item><c>direction</c>: a file names only its own subdomain's types and those of the subdomains listed before it, read through Roslyn's
/// semantic model (<see cref="StructureAnalysis"/>): every name that binds to a type a driver file declares, however it is spelled. An edge
/// against the order is allowed only where the census names it, as an exact edge, a file and the type it names, under its edge family and
/// the subdomain row after which it is cut.</item>
/// <item><c>pure</c>: a <c>Domain</c> file does no I/O, by <see cref="Impure"/>'s tokens, names no type of the I/O seams and holds no
/// <c>async</c>, but where the census names the crossing.</item>
/// <item><c>loop</c>: nothing outside <c>Loop</c>'s folder names a type declared in it, with no exception; and a file the map places in Loop
/// declares no static <c>bool</c> rule the census does not name as moving.</item>
/// <item><c>mirror</c>: a test folder is a subdomain's, today's not yet moved or support; a test the map lists sits under its subdomain's folder.</item>
/// <item><c>namespace</c>: no global using names a subdomain's namespace.</item>
/// </list>
/// A comment or a string names nothing, a <c>cref</c> included.
/// </summary>
internal static class StructureCheck
{
    /// <summary>The composition root: it may use every subdomain, and none uses it.</summary>
    public const string Loop = "Loop";

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
        var list = sources.ToList();
        return Check(map, list, tests, StructureAnalysis.Of(list));
    }

    /// <summary>The same over an analysis already made of the sources, for a caller that reads the edges too.</summary>
    public static IReadOnlyList<StructureBreach> Check(
        StructureMap map, IReadOnlyList<(string Path, string Text)> sources, IEnumerable<string> tests, StructureAnalysis analysis)
    {
        var breaches = new List<StructureBreach>();
        Mirror(map, tests.ToList(), breaches);
        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (subdomain, at) in map.Subdomains.Select((subdomain, at) => (subdomain, at)))
        {
            if (!rank.TryAdd(subdomain.Name, at)) breaches.Add(new("structure.json", 0, "map", $"{subdomain.Name} is a subdomain of the map twice"));
        }

        var folders = map.Subdomains.GroupBy(subdomain => subdomain.Folder, StringComparer.Ordinal).ToList();
        foreach (var shared in folders.Where(group => group.Count() > 1))
        {
            breaches.Add(new("structure.json", 0, "map", $"{string.Join(" and ", shared.Select(subdomain => subdomain.Name))} share the folder {shared.Key}"));
        }

        var byName = map.Subdomains.GroupBy(subdomain => subdomain.Name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var byFolder = map.Subdomains.GroupBy(subdomain => subdomain.Folder, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var present = sources.Select(source => source.Path).ToHashSet(StringComparer.Ordinal);

        // The map's own entries: a file it places is there, in a subdomain it declares; a census row names a subdomain it declares.
        foreach (var (file, name) in map.Files)
        {
            if (!byName.ContainsKey(name))
            {
                breaches.Add(new("structure.json", 0, "map", $"{file} is placed in {name}, which is no subdomain of the map"));
            }

            if (!present.Contains(file))
            {
                breaches.Add(new(file, 0, "place", "structure.json places it, and it is not there: a move gives its entry its new path, and a delete takes it off the list"));
            }
        }

        foreach (var family in map.Families.Where(family => !byName.ContainsKey(family.After)))
        {
            breaches.Add(new("structure.json", 0, "map", $"the edge family {family.Name} is cut after {family.After}, which is no subdomain of the map"));
        }

        foreach (var rule in map.LoopRules.Where(rule => !byName.ContainsKey(rule.After)))
        {
            breaches.Add(new("structure.json", 0, "map", $"the rule {rule.Member} of {rule.File} leaves Loop after {rule.After}, which is no subdomain of the map"));
        }

        Subdomain? Place(string path)
        {
            if (map.Files.TryGetValue(path, out var name)) return byName.GetValueOrDefault(name);
            var parts = path.Split('/');
            return parts.Length > 1 ? byFolder.GetValueOrDefault(parts[0]) : null;
        }

        var loopFolder = byName.TryGetValue(Loop, out var loop) ? loop.Folder : Loop;
        foreach (var source in sources)
        {
            var parts = source.Path.Split('/');
            var placed = Place(source.Path);
            if (placed is null)
            {
                breaches.Add(new(source.Path, 1, "place", parts.Length == 1
                    ? "a file at the driver's top that structure.json does not place: put it in its subdomain's folder"
                    : $"the folder {parts[0]} is no subdomain's, and structure.json does not place the file: put it in its subdomain's folder (structure.json's subdomains)"));
            }
            else
            {
                var inFolder = parts.Length > 1 && parts[0] == placed.Folder;
                if (placed.Moved && !inFolder)
                {
                    breaches.Add(new(source.Path, 1, "place", $"{placed.Name} has moved, and the map places this file in it: its files sit in {placed.Folder}"));
                }

                if (parts.Length > 1 && byFolder.TryGetValue(parts[0], out var owner) && owner.Moved && owner.Name != placed.Name)
                {
                    breaches.Add(new(source.Path, 1, "place", $"sits in {owner.Folder}, where {owner.Name} has moved, and the map places it in {placed.Name}"));
                }

                if (inFolder && parts.Length > 2 && parts[1] == "Domain" && !placed.Domain)
                {
                    breaches.Add(new(source.Path, 1, "module", $"{placed.Name} holds no Domain layer: its layers are none"));
                }
            }

            if (parts.Length > 2 && parts[1] is "Application" or "Infrastructure")
            {
                breaches.Add(new(source.Path, 1, "module", $"{parts[0]} holds no {parts[1]} folder: a subdomain keeps its model and rules in a Domain and the rest at its top"));
            }
        }

        Purity(map, sources, analysis, breaches);
        Direction(map, analysis, rank, Place, loopFolder, breaches);
        LoopRules(map, analysis, Place, byName, breaches);

        foreach (var (file, line, name) in analysis.GlobalUsings)
        {
            foreach (var subdomain in map.Subdomains.Where(subdomain => subdomain.Namespace is not null))
            {
                if (name == subdomain.Namespace || name.StartsWith(subdomain.Namespace + ".", StringComparison.Ordinal))
                {
                    breaches.Add(new(file, line, "namespace", $"a global using of {name}: no global using names a subdomain's namespace, so a file's dependencies stay in its own usings"));
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

    /// <summary>The <c>pure</c> rule: a <c>Domain</c> file's I/O tokens, its seams and its <c>async</c>, but where the census names them.</summary>
    private static void Purity(StructureMap map, IReadOnlyList<(string Path, string Text)> sources, StructureAnalysis analysis, List<StructureBreach> breaches)
    {
        var named = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            var parts = source.Path.Split('/');
            if (parts.Length < 3 || parts[1] != "Domain") continue;
            var code = CodeOf(source.Text);
            foreach (var (token, why) in Impure)
            {
                foreach (Match use in token.Matches(code))
                {
                    breaches.Add(new(source.Path, LineOf(code, use.Index), "pure", $"{use.Value.Trim()} {why}: a Domain is pure; it takes the world as values or through a port"));
                }
            }

            var crossings = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var edge in analysis.Edges.Where(edge => edge.File == source.Path && map.Seams.Contains(edge.Type.Split('.')[0])))
            {
                crossings[edge.Type.Split('.')[0]] = edge.Line;
            }

            if (analysis.AsyncFiles.Contains(source.Path))
            {
                crossings["async"] = 0;
            }

            var allowed = map.Pure.GetValueOrDefault(source.Path) ?? [];
            foreach (var (crossing, line) in crossings.Where(crossing => !allowed.Contains(crossing.Key)))
            {
                breaches.Add(new(source.Path, line, "pure", crossing == "async"
                    ? "holds an async method: a Domain is pure; it takes the world as values or through a port, and awaits nothing"
                    : $"names {crossing}, an I/O seam: a Domain is pure; it takes the world as values or through a port"));
            }

            named[source.Path] = crossings.Keys.ToHashSet(StringComparer.Ordinal);
        }

        foreach (var (file, crossings) in map.Pure)
        {
            foreach (var crossing in crossings.Where(crossing => !(named.GetValueOrDefault(file)?.Contains(crossing) ?? false)))
            {
                breaches.Add(new("structure.json", 0, "map", $"{file} is listed as crossing {crossing} in a Domain, and it does not: a cut takes its entry off the list"));
            }
        }
    }

    /// <summary>The <c>direction</c> rule, and the <c>loop</c> rule's edges into Loop's folder.</summary>
    private static void Direction(
        StructureMap map, StructureAnalysis analysis, Dictionary<string, int> rank,
        Func<string, Subdomain?> place, string loopFolder, List<StructureBreach> breaches)
    {
        var upward = new Dictionary<(string File, string Type), TypeEdge>();
        foreach (var edge in analysis.Edges)
        {
            var from = place(edge.File);
            if (from is null) continue;
            var inLoop = edge.DeclaredIn.Any(file => file.Split('/')[0] == loopFolder);
            if (inLoop && edge.File.Split('/')[0] != loopFolder)
            {
                breaches.Add(new(edge.File, edge.Line, "loop", $"names {edge.Type}, declared in {edge.DeclaredIn.First(file => file.Split('/')[0] == loopFolder)}: Loop is the composition root, and nothing outside its folder names it"));
                continue;
            }

            var above = edge.DeclaredIn.Select(place).Where(there => there is not null && rank.GetValueOrDefault(there.Name) > rank.GetValueOrDefault(from.Name)).FirstOrDefault();
            if (above is null) continue;
            if (inLoop)
            {
                breaches.Add(new(edge.File, edge.Line, "loop", $"names {edge.Type}, declared in a file of Loop's folder, and {from.Name} is below {above.Name}: no exception names an edge into Loop"));
                continue;
            }

            upward[(edge.File, edge.Type)] = edge;
        }

        var listed = new HashSet<(string File, string Type)>();
        foreach (var family in map.Families)
        {
            foreach (var (file, types) in family.Edges)
            {
                foreach (var type in types)
                {
                    if (!listed.Add((file, type)))
                    {
                        breaches.Add(new("structure.json", 0, "map", $"{file} names {type} in two edge families, {family.Name} among them"));
                    }
                    else if (!upward.ContainsKey((file, type)))
                    {
                        breaches.Add(new("structure.json", 0, "map", $"{family.Name} lists {file} naming {type}, which is no edge against the order now: a cut takes its entry off the list"));
                    }
                }
            }
        }

        foreach (var ((file, type), edge) in upward.Where(edge => !listed.Contains(edge.Key)))
        {
            var from = place(file)!.Name;
            var above = edge.DeclaredIn.Select(place).First(there => there is not null && rank.GetValueOrDefault(there.Name) > rank.GetValueOrDefault(from))!.Name;
            breaches.Add(new(file, edge.Line, "direction",
                $"names {type}, declared in {edge.DeclaredIn[0]}: {from} is below {above}, and no edge family of structure.json lists this edge (a file and the type it names, under the row after which it is cut)"));
        }
    }

    /// <summary>The <c>loop</c> rule's judgements: Loop declares none the census does not name as moving.</summary>
    private static void LoopRules(
        StructureMap map, StructureAnalysis analysis, Func<string, Subdomain?> place, Dictionary<string, Subdomain> byName, List<StructureBreach> breaches)
    {
        var listed = map.LoopRules.Select(rule => (rule.File, rule.Member)).ToHashSet();
        foreach (var rule in analysis.Rules.Where(rule => place(rule.File)?.Name == Loop && !listed.Contains((rule.File, rule.Member))))
        {
            breaches.Add(new(rule.File, rule.Line, "loop",
                $"{rule.Member} is a rule, a static bool, in Loop: Loop holds no rules, so it moves to its subdomain as a pure function, and the census names it as moving until then"));
        }

        var found = analysis.Rules.Select(rule => (rule.File, rule.Member)).ToHashSet();
        foreach (var rule in map.LoopRules.Where(rule => !found.Contains((rule.File, rule.Member))))
        {
            breaches.Add(new("structure.json", 0, "map", $"{rule.Member} of {rule.File} is listed as a rule Loop holds, and it is not one there: a move takes its entry off the list"));
        }
    }

    /// <summary>Folders of the tests' project that are no subdomain's: fixtures and the build's own output.</summary>
    private static readonly string[] TestSupport = ["fixtures", "golden", "bin", "obj", "TestResults"];

    /// <summary>The <c>mirror</c> rule: a listed test sits under its subdomain's folder, and a test folder is a subdomain's, not yet moved, or support.</summary>
    private static void Mirror(StructureMap map, List<string> tests, List<StructureBreach> breaches)
    {
        var folders = map.Subdomains.Select(subdomain => subdomain.Folder).ToHashSet(StringComparer.Ordinal);
        foreach (var path in tests)
        {
            var parts = path.Split('/');
            if (parts.Length > 1 && !folders.Contains(parts[0]) && !map.TestFolders.Contains(parts[0]) && !TestSupport.Contains(parts[0]))
            {
                breaches.Add(new(path, 0, "mirror", $"the folder {parts[0]} is no subdomain's, and structure.json does not list it as not yet moved: a test folder is a subdomain's, today's not yet moved, fixtures, golden or a build output"));
            }
        }

        foreach (var folder in map.TestFolders.Where(folder => !tests.Any(path => path.StartsWith(folder + "/", StringComparison.Ordinal))))
        {
            breaches.Add(new("structure.json", 0, "map", $"the tests' folder {folder} is listed as not yet moved, and none is there: a move takes its entry off the list"));
        }

        foreach (var (name, subdomain) in map.Tests)
        {
            var folder = map.Subdomains.FirstOrDefault(each => each.Name == subdomain)?.Folder;
            if (folder is null)
            {
                breaches.Add(new("structure.json", 0, "map", $"{name} is a test mapped to {subdomain}, which is no subdomain of the map"));
            }

            var found = tests.Where(path => path.Split('/')[^1] == name).ToList();
            if (found.Count == 0)
            {
                breaches.Add(new("structure.json", 0, "map", $"{name} is listed under {subdomain}, and no such test is in the tests' tree: a delete takes its entry off the list"));
            }

            foreach (var path in found.Where(path => folder is not null && (path.Split('/')[0] != folder || !path.Contains('/'))))
            {
                var where = path.Contains('/') ? $"under {path.Split('/')[0]}" : "at the top of the tests";
                breaches.Add(new(path, 0, "mirror", $"sits {where}, and the map lists it under {subdomain}: a test mirrors its subdomain's folder"));
            }
        }
    }

    private static int LineOf(string code, int offset)
    {
        var line = 1;
        for (var i = 0; i < offset && i < code.Length; i++)
        {
            if (code[i] == '\n') line++;
        }

        return line;
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
}
