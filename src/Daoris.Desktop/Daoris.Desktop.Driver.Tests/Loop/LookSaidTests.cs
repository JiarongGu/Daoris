using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// NOTIFY2h (D196 point 7; the notification design §5.1, §9): each line the look says is classed where it is made and written as the
/// declared machine-log event <c>look.said</c>, its class and its family and never its words; a news line is a coded notice in
/// <i>Done and failed</i>, whatever the notify switch says; and the census: every line a producer of the look adds goes through
/// <see cref="LookSaid"/>, so a line with no class fails here. Files only, in a home under <c>_fixtures/</c>.
/// </summary>
public sealed class LookSaidTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "look-said", Guid.NewGuid().ToString("N")[..8]);

    private readonly MachineLog _log;

    private int _noticed;

    public LookSaidTests()
    {
        Directory.CreateDirectory(_home);
        _log = new MachineLog(_home, "desktop", () => Now);
    }

    public void Dispose()
    {
        _log.Dispose();
        GitSeed.Discard(_home);
    }

    private LookSaid Said() => new(_home, () => _log, () => _noticed++, () => Now);

    private JsonObject[] Logged() =>
        [.. StubFile.Lines(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-10-22.desktop.jsonl"))
            .Select(line => JsonNode.Parse(line)!.AsObject())
            .Where(line => (string?)line["event"] == LookSaid.Event)];

    /// <summary>§5.1: each class is written with its family, the line handed on unchanged and never written to the log.</summary>
    [Fact]
    public void Each_line_is_written_as_look_said_with_its_class_and_family_and_never_its_words()
    {
        var said = Said();
        const string line = "rules  the proposals could not be settled this tick: the file is busy";

        Assert.Equal(line, said.Retry("rules", line));
        Assert.Equal("b", said.Own("meeting", "b"));
        Assert.Equal("c", said.Wait("helper", "c"));
        Assert.Equal("d", said.Progress("hand-over", "d"));
        Assert.Equal(["e", "f"], said.Each(LookClass.Progress, "channel", ["e", "f"]));
        Assert.Equal("g", said.Of(LookClass.Wait, "start", "g"));
        Assert.Throws<ArgumentException>(() => said.Of(LookClass.News, "stop", "h"));

        var logged = Logged();
        Assert.Equal(
            ["retry/rules", "own/meeting", "wait/helper", "progress/hand-over", "progress/channel", "progress/channel", "wait/start"],
            logged.Select(each => $"{each["data"]!["class"]}/{each["data"]!["family"]}"));
        Assert.All(logged, each => Assert.Equal(["class", "family"], each["data"]!.AsObject().Select(field => field.Key)));
        Assert.DoesNotContain(logged, each => each.ToJsonString().Contains("busy", StringComparison.Ordinal));
        Assert.Empty(Notices.Read(_home, Now)!);
        Assert.Equal(0, _noticed);
    }

    /// <summary>§5.1: a news line is a coded notice of the look's, the line's words nowhere in it; said again, it is one notice.</summary>
    [Fact]
    public void A_news_line_is_kept_once_as_a_coded_notice()
    {
        var said = Said();
        var news = new LookNews(LookSaid.LostClaim, "s1", Session: "s1", Quest: "q1", Workspace: "games");
        const string line = "stop  session s1 (#q1): stopped by this machine's driver";

        Assert.Equal(line, said.News("stop", news, line));
        said.News("stop", news, line);

        var notice = Assert.Single(Notices.Read(_home, Now)!);
        Assert.Equal(new Notice("look-lost-claim-s1", Notices.Look, Now, Workspace: "games", Session: "s1", Quest: "q1", Code: LookSaid.LostClaim), notice);
        Assert.Equal(["news/stop", "news/stop"], Logged().Select(each => $"{each["data"]!["class"]}/{each["data"]!["family"]}"));
        // Told at each keep: the windows' slice reads the record again and pushes only what moved.
        Assert.Equal(2, _noticed);
        Assert.DoesNotContain("driver", File.ReadAllText(Notices.PathOf(_home)), StringComparison.Ordinal);
    }

    /// <summary>The meeting and the brainstorm the person stopped make no notice (R3): their lines are <c>own</c>.</summary>
    [Fact]
    public void What_the_person_stopped_makes_no_notice()
    {
        var said = Said();
        said.Own("meeting", "meeting `m1` stopped by the person before round 1 ran: nothing of it went to any agent.");
        said.Own("brainstorm", "brainstorm `b1` stopped by the person before it ran: nothing went to any agent.");

        Assert.Empty(Notices.Read(_home, Now)!);
        Assert.Equal(0, _noticed);
    }

    /// <summary>No home keeps no notice, and no log writes nothing: a look a test runs bare still classes its lines.</summary>
    [Fact]
    public void None_classes_and_keeps_nothing()
    {
        Assert.Equal("x", LookSaid.None.News("stop", new LookNews(LookSaid.LostClaim, "s1"), "x"));
        Assert.Equal("y", LookSaid.None.Retry("rules", "y"));
        Assert.False(File.Exists(Notices.PathOf(_home)));
    }

    /// <summary>A refused write is logged as <c>notices.refused</c> and dropped, and tells no window (§2.5).</summary>
    [Fact]
    public void A_refused_notice_is_logged_and_dropped()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Notices.PathOf(_home))!);
        File.WriteAllText(Notices.PathOf(_home), """{ "schemaVersion": 99, "notices": {} }""");

        Said().News("meeting", new LookNews(LookSaid.MeetingCut, "m1"), "meeting `m1` was cut short");

        Assert.Equal(0, _noticed);
        Assert.Contains(
            StubFile.Lines(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Logs), "2026-10-22.desktop.jsonl")),
            line => line.Contains("\"event\":\"notices.refused\"", StringComparison.Ordinal));
    }

    /// <summary>
    /// The census (§9's proof): every line a producer of the look adds to the look's lines goes through <see cref="LookSaid"/> with its
    /// class, in every source that adds one; a line with no class fails here, naming its file and line. A list another producer
    /// already classed and that is only carried on is named below, by its exact text.
    /// </summary>
    [Fact]
    public void Every_line_the_look_adds_has_a_class()
    {
        var sources = SourceFiles.All(SourceFiles.DriverProject, "*.cs")
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Loop{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           || Path.GetFileName(path) == "WorkflowRunStart.cs")
            .ToList();
        Assert.Contains(sources, path => Path.GetFileName(path) == "Driver.cs");

        var unclassed = new List<string>();
        var found = 0;
        foreach (var path in sources)
        {
            var text = File.ReadAllText(path).Replace("\r\n", "\n");
            foreach (Match add in Adds.Matches(text))
            {
                found++;
                var argument = text[(add.Index + add.Length)..].TrimStart();
                if (Classed.IsMatch(argument) || Carried.Any(each => argument.StartsWith(each, StringComparison.Ordinal))) continue;
                var line = text[..add.Index].Count(c => c == '\n') + 1;
                unclassed.Add($"{Path.GetFileName(path)}:{line}");
            }
        }

        Assert.True(found > 40, $"the census found {found} adds; the pattern no longer reads the look's sources");
        Assert.Empty(unclassed);
    }

    /// <summary>An add to the look's lines: <c>events</c>, the watch's <c>carried</c> and a settle's <c>pending</c>.</summary>
    private static readonly Regex Adds = new(@"\b(events|carried|pending)\.(Add|AddRange)\(", RegexOptions.CultureInvariant);

    /// <summary>A line classed where it is made: the driver's <c>Lines</c>, a class's <c>Said</c>, a static producer's <c>said</c>.</summary>
    private static readonly Regex Classed = new(
        @"^((_?context|_said)\.Said|Lines|said|Said)\.(Retry|Own|Wait|Progress|News|Each|Of)\(", RegexOptions.CultureInvariant);

    /// <summary>The lists only carried on, each classed by the producer that made it.</summary>
    private static readonly string[] Carried =
    [
        // A settle's pending lines, classed where each pass said them.
        "lines);",
        // A failed look's lines, classed as that look made them.
        "said.Events);",
    ];
}
