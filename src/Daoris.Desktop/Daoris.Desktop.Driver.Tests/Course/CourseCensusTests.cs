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
