using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1e part 1 (the foundation design §3.1's census sentence, the brief §7.2): every site that opens a part <c>from</c> another
/// or resumes with words, as a shape found in a source file. A file with a shape either reads the course or is named <i>not yet moved</i>
/// in <c>fixtures/course-sites.json</c> with the survey's paths it decides, the row that adopts it (h for conversations, i for driven
/// lines) and whether it opens a part and hands words (<c>hands</c>, which row d-ii's later parts read). Nothing reads the course yet, so
/// every site is named. A named file that is gone, a count that differs and a site in no file all fail.
/// </summary>
/// <remarks>
/// Not a twin of another census (<c>twins.md</c>): <c>LineAccountTests</c> lists the readers of an account's cool-off and
/// <c>ProviderCensusTests</c> the sites that name a provider; this one lists where a path decides how a line goes on, and each names
/// other files for other reasons. It shares their shape alone.
/// </remarks>
public sealed class CourseCensusTests
{
    /// <summary>The projects a site can be written in: the driver, its host, the modules and the app.</summary>
    private static readonly string[] Projects = ["Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host", "Daoris.Desktop.Modules", "Daoris.Desktop.App"];

    private static string Root => Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop");

    /// <summary>Every production source of <see cref="Projects"/>, never a build's output.</summary>
    internal static IEnumerable<string> ProductionFiles() => Projects
        .SelectMany(project => Directory.EnumerateFiles(Path.Combine(Root, project), "*.cs", SearchOption.AllDirectories))
        .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
            && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar));

    /// <summary>A source's code with its whole-line comments taken out, so a doc that spells a shape is no site.</summary>
    internal static string WithoutComments(string text) =>
        string.Join('\n', text.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    private static JsonElement Table()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(Root, "Daoris.Desktop.Driver.Tests", "fixtures", "course-sites.json")));
        return document.RootElement.Clone();
    }

    private static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    /// <summary>
    /// Every file with a shape of a site reads the course or is named not yet moved with exactly that shape's count, every named site is
    /// there, and the survey's paths A to N are each placed or named a non-decision.
    /// </summary>
    [Fact]
    public void Every_site_that_decides_how_a_line_goes_on_reads_the_course_or_is_named_not_yet_moved()
    {
        var table = Table();
        var shapes = table.GetProperty("shapes").EnumerateObject()
            .ToDictionary(shape => shape.Name, shape => new Regex(shape.Value.GetString()!, RegexOptions.Multiline | RegexOptions.Singleline));
        var named = table.GetProperty("sites").EnumerateArray()
            .ToDictionary(site => (File: site.GetProperty("file").GetString()!, Shape: site.GetProperty("shape").GetString()!));
        var reads = new Regex(@"\b(LineCourse|CourseFacts)\b");

        var found = new HashSet<(string File, string Shape)>();
        var unplaced = new List<string>();
        var drifted = new List<string>();
        foreach (var path in ProductionFiles())
        {
            var relative = Relative(path);
            var text = WithoutComments(File.ReadAllText(path));
            foreach (var (shape, pattern) in shapes)
            {
                var count = pattern.Matches(text).Count;
                if (count == 0) continue;

                found.Add((relative, shape));
                if (reads.IsMatch(text)) continue;

                if (!named.TryGetValue((relative, shape), out var site)) unplaced.Add($"{relative} ({shape} x{count})");
                else if (site.GetProperty("count").GetInt32() != count) drifted.Add($"{relative} {shape}: named {site.GetProperty("count").GetInt32()}, found {count}");
            }
        }

        Assert.True(found.Count >= table.GetProperty("floor").GetInt32(), $"the census found {found.Count} sites: its shapes have drifted");
        Assert.True(unplaced.Count == 0, "decides how a line goes on, reads no course and is not named not yet moved: " + string.Join(", ", unplaced));
        Assert.True(drifted.Count == 0, "named with another count: " + string.Join(", ", drifted));

        var gone = named.Keys.Where(key => !found.Contains(key) && !File.Exists(Path.Combine(Root, key.File))).Select(key => key.File).Distinct().ToList();
        Assert.True(gone.Count == 0, "named not yet moved but not there: " + string.Join(", ", gone));
        var stale = named.Keys.Where(key => !found.Contains(key)).Select(key => $"{key.File} ({key.Shape})").ToList();
        Assert.True(stale.Count == 0, "named, but its shape is no longer there: " + string.Join(", ", stale));

        // Each site is named with its adopting row, its status and whether it hands words; the fields are closed.
        foreach (var site in named.Values)
        {
            Assert.Equal("not yet moved", site.GetProperty("status").GetString());
            Assert.Contains(site.GetProperty("row").GetString(), new[] { "h", "i" });
            Assert.True(site.GetProperty("hands").ValueKind is JsonValueKind.True or JsonValueKind.False, site.GetProperty("file").GetString());
            Assert.NotEmpty(site.GetProperty("paths").EnumerateArray());
            Assert.True(site.GetProperty("count").GetInt32() > 0);
        }

        var placed = named.Values.SelectMany(site => site.GetProperty("paths").EnumerateArray().Select(path => path.GetString()!)).ToHashSet();
        var notDecisions = table.GetProperty("notDecisions").EnumerateArray().ToDictionary(item => item.GetProperty("path").GetString()!);
        foreach (var path in "ABCDEFGHIJKLMN".Select(letter => letter.ToString()))
        {
            Assert.True(placed.Contains(path) || notDecisions.ContainsKey(path), $"path {path} is neither placed at a site nor named a non-decision");
        }

        Assert.Equal(["L", "M"], notDecisions.Keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.DoesNotContain("M", placed);
    }

    /// <summary>
    /// The answers-alone hold's readers are the sites that build the hold (<c>new AnswersAloneHold(</c>): the chat's go-on and the
    /// helper's words, the driven look and the supervisor's door, each named with its row.
    /// </summary>
    [Fact]
    public void The_answers_alone_hold_sites_are_named()
    {
        var holds = Table().GetProperty("sites").EnumerateArray()
            .Where(site => site.GetProperty("shape").GetString() == "answers-alone-read")
            .Select(site => Path.GetFileName(site.GetProperty("file").GetString()!))
            .OrderBy(file => file, StringComparer.Ordinal);
        Assert.Equal(["ChatGoOn.cs", "ChatHelperWords.cs", "Driver.cs", "SupervisorDoor.cs"], holds);
    }

    /// <summary>
    /// The closed values of a site's <c>moving</c>, which says how the open of a site that hands words moves them (FOUNDATION1d-ii part 4; part
    /// 5 adopted the driven carry-on and the refused open, so no site waits on a later part).
    /// </summary>
    private static readonly string[] Moving = ["moves", "keyed", "no move"];

    /// <summary>
    /// FOUNDATION1d-ii parts 4 and 5 (the foundation design §3.4 rule 3): every site that opens a part and hands it words names how its open
    /// moves them, and none that does not: <c>moves</c>, its open asked to move the old part's words (<c>MovedWords.Asked</c>) and its delivery
    /// read from the destination's moved words (<c>MovedWords.Moving</c>), the driven carry-on and the refused open among them since part 5;
    /// <c>keyed</c>, the hand-over's take (FOUNDATION1f-v part a), which asks its open under the move's key with nothing named and nothing read,
    /// as the curve's does, so the host moves every word of the person's kept on the old part meanwhile and the new part's own go-on says it
    /// after the brief; it hands no word of its own, so it reads none; <c>no move</c>, a site
    /// that hands words to its own record or to a fresh one, with no record left, the driven go-on's resume among them. The ended limit move's
    /// box members that read the destination's moved words were named until row f-vi retired the box (O6-F13; FOUNDATION1f-vi part a).
    /// </summary>
    [Fact]
    public void Every_site_that_hands_words_names_how_its_open_moves_them()
    {
        var table = Table();
        foreach (var site in table.GetProperty("sites").EnumerateArray())
        {
            var file = site.GetProperty("file").GetString()!;
            var hands = site.GetProperty("hands").GetBoolean();
            var named = site.TryGetProperty("moving", out var moving) ? moving.GetString() : null;
            Assert.True(hands == (named is not null), $"{file} ({site.GetProperty("shape").GetString()}): hands {hands}, moving {named ?? "none"}");
            if (named is null) continue;

            Assert.Contains(named, Moving);
            if (named == "no move") continue;
            var text = WithoutComments(File.ReadAllText(Path.Combine(Root, file)));
            Assert.True(Regex.IsMatch(text, @"\bMovedWords\.Asked\("), $"{file} is named as moving its words but asks no open to move them");
            if (named != "moves") continue;
            Assert.True(Regex.IsMatch(text, @"\bMovedWords\.(Moving|OnAsync)\("), $"{file} is named as moving its words but reads no destination's moved words");
        }

        // The ended limit move's box members were named here until row f-vi retired the box (FOUNDATION1f-vi part a): its move is read from the
        // store's record, so the table names none and the move's source has none of them.
        Assert.False(table.TryGetProperty("box", out _), "the ended limit move's box members are retired");
        var source = WithoutComments(File.ReadAllText(Path.Combine(Root, "Daoris.Desktop.Driver/Course/Chat/LimitMove.cs")));
        Assert.DoesNotMatch(@"\b(DeliveredAsync|DeliveredToAsync|TakeUpBoxesAsync|TakeUpMovedAsync|box\.Moved)\b", source);
    }

    /// <summary>
    /// FOUNDATION1f-ii part a (the foundation design §3.3): every move that keeps its move record is named in <c>records</c> with its member
    /// and the row that adopted it, and none that does not: a source that decides a move through the conversations' holder
    /// (<c>Moves.BeginAsync(</c>) is named, and each named member decides through it. The live limit move is the first and the curve's move
    /// the second (f-iii part a), the conversation's hand-over's ask the next (f-v part a, through the holder it is handed) and the quest's
    /// hand-over's the driven member's (f-v part c); rows f-iii's other parts to f-vi add theirs.
    /// </summary>
    [Fact]
    public void Every_move_that_keeps_its_record_is_named_with_its_member()
    {
        var named = Table().GetProperty("records").EnumerateArray().ToList();
        var deciding = ProductionFiles()
            .Where(path => Regex.IsMatch(WithoutComments(File.ReadAllText(path)), @"\b[Mm]oves\.BeginAsync\("))
            .Select(Relative).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(deciding, named.Select(record => record.GetProperty("file").GetString()!).Distinct().Order(StringComparer.Ordinal));
        foreach (var record in named)
        {
            var source = WithoutComments(File.ReadAllText(Path.Combine(Root, record.GetProperty("file").GetString()!)));
            var member = record.GetProperty("member").GetString()!;
            var at = Regex.Match(source, @"\bTask(<[^>]+>)?\??\s+" + member + @"\(");
            Assert.True(at.Success, $"{member} is not in {record.GetProperty("file").GetString()}");
            var next = Regex.Match(source[(at.Index + at.Length)..], @"\n    (public|internal|private|protected)\s");
            var body = source.Substring(at.Index, next.Success ? next.Index + at.Length : source.Length - at.Index);
            Assert.Matches(@"\b[Mm]oves\.BeginAsync\(", body);
            Assert.Matches(@"^f-(ii|iii|iv|v|vi)$", record.GetProperty("row").GetString()!);
        }

        Assert.Contains(named, record => record.GetProperty("member").GetString() == "MoveAsync" && record.GetProperty("row").GetString() == "f-ii");
        Assert.Contains(
            named,
            record => record.GetProperty("file").GetString()!.EndsWith("/CurveMove.cs", StringComparison.Ordinal) && record.GetProperty("row").GetString() == "f-iii");
    }

    /// <summary>
    /// Path M, SWITCHCTX1's carry, has no code: the census reserves it, and fails the day a source writes a carried move, so the path is
    /// placed at its site then.
    /// </summary>
    [Fact]
    public void The_carry_has_no_code_yet()
    {
        var carried = ProductionFiles().Where(path => Regex.IsMatch(WithoutComments(File.ReadAllText(path)), @"moved\.carried")).Select(Relative).ToList();
        Assert.True(carried.Count == 0, "the carry is written in: " + string.Join(", ", carried) + "; place path M");
    }
}
