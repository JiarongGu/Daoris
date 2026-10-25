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
        Assert.Equal("g", said.Of(LookClass.Wait, "start", "g"));
        Assert.Throws<ArgumentException>(() => said.Of(LookClass.News, "stop", "h"));

        var logged = Logged();
        Assert.Equal(
            ["retry/rules", "own/meeting", "wait/helper", "progress/hand-over", "wait/start"],
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
    /// NOTIFY2h2: a producer's lines are each classed as it adds them (<see cref="LookLines"/>), handed on unchanged and in order; a news
    /// one is its notice.
    /// </summary>
    [Fact]
    public void A_producers_lines_are_each_classed_as_they_are_made()
    {
        var lines = new LookLines(Said(), "reopen");
        lines.Retry("a");
        lines.Own("b");
        lines.Wait("c");
        lines.Progress("d");
        lines.Of(LookClass.Progress, "e");
        lines.News(new LookNews(LookSaid.ReopenTreeGone, "q1", "s1", "q1", "games"), "f");

        Assert.Equal(["a", "b", "c", "d", "e", "f"], lines.Said);
        Assert.Equal(["retry", "own", "wait", "progress", "progress", "news"], Logged().Select(each => (string?)each["data"]!["class"]));
        Assert.All(Logged(), each => Assert.Equal("reopen", (string?)each["data"]!["family"]));
        Assert.Equal(new Notice("look-reopen-tree-gone-q1", Notices.Look, Now, Workspace: "games", Session: "s1", Quest: "q1", Code: LookSaid.ReopenTreeGone),
            Assert.Single(Notices.Read(_home, Now)!));
    }

    /// <summary>
    /// NOTIFY2h2: a run's line, said where the look drains it, by the class the pass that made it gave it (a meeting's failure news, a
    /// helper's wait its retry); one its pass did not class is a session's end, <c>progress</c>, or the person's own, <c>own</c>.
    /// </summary>
    [Fact]
    public void A_runs_line_is_said_by_the_class_its_pass_gave_it()
    {
        var said = Said();

        Assert.Equal("m", said.Ran(LookClassed.Noticed("meeting", new LookNews(LookSaid.MeetingFailed, "m1")), byPerson: false, "m"));
        Assert.Equal("w", said.Say(new LookLine("w", new LookClassed(LookClass.Retry, "helper"))));
        Assert.Equal("o", said.Ran(new LookClassed(LookClass.Own, "brainstorm"), byPerson: false, "o"));
        Assert.Equal("e", said.Ran(null, byPerson: false, "e"));
        Assert.Equal("p", said.Ran(null, byPerson: true, "p"));
        Assert.Throws<ArgumentException>(() => said.Ran(new LookClassed(LookClass.News, "meeting"), byPerson: false, "x"));

        Assert.Equal(
            ["news/meeting", "retry/helper", "own/brainstorm", "progress/ended", "own/ended"],
            Logged().Select(each => $"{each["data"]!["class"]}/{each["data"]!["family"]}"));
        Assert.Equal("look-meeting-failed-m1", Assert.Single(Notices.Read(_home, Now)!).Id);
    }

    /// <summary>
    /// LIMITRESUME1a (D206 point 1; the limit-resume design §3.1): a quest's line a limit cut off says what became of it as its own news,
    /// not as an <i>Ended</i>: keyed by the limited part, so a start held look after look keeps one notice of its wait and one of its
    /// going on. A start that carries on any other end, or that the person's words make, says none.
    /// </summary>
    [Fact]
    public void A_line_a_limit_cut_off_says_its_own_news_once_per_part()
    {
        var quest = new QuestView("q1", "game", "engine", "Expose a streaming budget", "A body.", "Taken");
        var limited = new PriorSession("s3", null, "failed") { Limit = true };
        StartBegun Begun(Consideration start) => new("q1", null, "engine", "games", "claude-code", start);
        var carryOn = new Consideration(quest, StartVerdict.Start, "carries on") { Resumes = limited };

        var waits = Daoris.Driver.Driver.LimitedLine(Begun(carryOn));
        Assert.Equal(new LookNews(LookSaid.LimitWaits, "s3", "s3", "q1", "games"), waits);
        Assert.Null(Daoris.Driver.Driver.LimitedLine(Begun(carryOn with { Resumes = limited with { Limit = false } })));
        Assert.Null(Daoris.Driver.Driver.LimitedLine(Begun(carryOn with { GoesOn = true })));
        Assert.Null(Daoris.Driver.Driver.LimitedLine(new StartBegun(null, "a1", "ask #a1", "games", "claude-code")));

        var said = Said();
        said.News("start", waits!, "held  #q1 → engine: every account cools");
        said.News("start", waits!, "held  #q1 → engine: every account cools");
        said.News("start", waits! with { Code = LookSaid.LimitWentOn }, $"start  #q1: {Daoris.Driver.Driver.LimitWentOnLine}");

        Assert.Equal(
            ["look-limit-waits-s3", "look-limit-went-on-s3"],
            Notices.Read(_home, Now)!.Select(notice => notice.Id).Order(StringComparer.Ordinal));
    }

    /// <summary>Every news code has its words in both of the page's catalogues (the translation contract): a code is never shown raw.</summary>
    [Fact]
    public void Every_news_code_has_its_words_in_en_and_zh()
    {
        foreach (var language in new[] { "en", "zh" })
        {
            var path = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Web", "src", "locales", language, "notifications.json");
            var catalogue = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Assert.All(LookSaid.NewsCodes, code =>
                Assert.False(string.IsNullOrWhiteSpace((string?)catalogue[$"notifications.done.look.{code}"]), $"{language} has no words for {code}"));
        }
    }

    /// <summary>
    /// NOTIFY2h2's census (D196's NOTIFY2h note): the look's deeper producers, which hand it their lines whole, class each line where they
    /// make it. Every look line one of them makes (a family's word and two spaces, as the report's lines read) is made by a statement that
    /// classes it, and every run a meeting's, a brainstorm's or a helper's pass ends carries its class; one that does not fails here,
    /// naming its file and line.
    /// </summary>
    [Fact]
    public void Every_line_the_looks_deeper_producers_make_has_a_class()
    {
        var sources = SourceFiles.All(SourceFiles.DriverProject, "*.cs")
            .Where(path => Producers.Contains(Path.GetFileName(path)))
            .ToList();
        Assert.Equal(Producers.Order(StringComparer.Ordinal), sources.Select(Path.GetFileName).Order(StringComparer.Ordinal));

        var unclassed = new List<string>();
        var found = 0;
        foreach (var path in sources)
        {
            var text = File.ReadAllText(path).Replace("\r\n", "\n");
            foreach (Match made in Made.Matches(text))
            {
                var statement = Statement(text, made.Index);
                if (NotTheLook.Any(each => text.AsSpan(made.Index).StartsWith(each, StringComparison.Ordinal)) || OwnRecord.IsMatch(statement)) continue;
                found++;
                if (!ClassedStatement.IsMatch(statement)) unclassed.Add($"{Path.GetFileName(path)}:{LineOf(text, made.Index)}");
            }

            foreach (Match run in Runs.Matches(text))
            {
                found++;
                if (!Statement(text, run.Index).Contains("Look =", StringComparison.Ordinal)) unclassed.Add($"{Path.GetFileName(path)}:{LineOf(text, run.Index)} (a run)");
            }
        }

        Assert.True(found > 60, $"the census found {found} lines; the pattern no longer reads the producers");
        Assert.True(unclassed.Count == 0, $"made with no class: {string.Join(", ", unclassed)}");
    }

    /// <summary>The producers that hand the look their lines whole (2h's list, D196's NOTIFY2h note), and the passes whose runs end beside it.</summary>
    private static readonly string[] Producers =
    [
        "ChannelReach.cs", "ReopenTelling.cs", "ReviewDesk.cs", "RuleProposals.cs", "AutoLander.cs", "WorkspaceSetup.cs",
        "RegistrationFollow.cs", "Hooks.cs", "BranchTidying.cs", "UncertainWords.cs", "DriverMeetings.cs", "DriverBrainstorms.cs",
        "DriverHelpers.cs",
    ];

    /// <summary>A look line made: a string that opens with a family's word and two spaces.</summary>
    private static readonly Regex Made = new(@"\$?""[a-z][a-z-]*  ", RegexOptions.CultureInvariant);

    /// <summary>A run a pass beside the look ends with.</summary>
    private static readonly Regex Runs = new(@"\bnew StartRun\(", RegexOptions.CultureInvariant);

    /// <summary>A statement that classes what it makes: through <see cref="LookSaid"/>, a producer's <see cref="LookLines"/>, or a class carried up.</summary>
    private static readonly Regex ClassedStatement = new(
        @"\.(Retry|Own|Wait|Progress|News|Of|Say|Ran)\(|\b(Helper|Ran)\(LookClass\.|\bLookClassed\b", RegexOptions.CultureInvariant);

    /// <summary>
    /// Lines in those files the look does not say as they are made: a plugin's answer to a state change, which its one teller logs
    /// (<c>Hooks.ChangedAsync</c>); and a registry outcome's words, which a terminal prints as they are and the look says through
    /// <c>RegistrationFollow.EventLine(followed, said)</c>, classed there.
    /// </summary>
    private static readonly string[] NotTheLook =
    [
        "$\"plugin  {id}: {error.Message}\";",
        "$\"registry  {followed.Repository}: {followed.Said}\",",
    ];

    /// <summary>
    /// A turn's or a helper pass's own record, whose line nothing in the look reads: the pass reads its codes and state, and its run's
    /// line is the one the look says (<see cref="Runs"/>).
    /// </summary>
    private static readonly Regex OwnRecord = new(@"\bnew (BrainstormTurnRun|HelperPassRun)\(", RegexOptions.CultureInvariant);

    /// <summary>The statement around <paramref name="at"/>: from the last <c>;</c> ending a line before it to the first after.</summary>
    private static string Statement(string text, int at)
    {
        var from = text.LastIndexOf(";\n", at, StringComparison.Ordinal) + 1;
        var to = text.IndexOf(";\n", at, StringComparison.Ordinal);
        return text[from..(to < 0 ? text.Length : to)];
    }

    private static int LineOf(string text, int at) => text[..at].Count(c => c == '\n') + 1;

    /// <summary>
    /// The census (§9's proof): every line a producer of the look adds to the look's lines goes through <see cref="LookSaid"/> with its
    /// class, in every source that adds one; a line with no class fails here, naming its file and line. A list another producer
    /// already classed and that is only carried on is named below, by its exact text, and a producer handed the look's classing
    /// (<c>Lines</c>) classes each line it makes (NOTIFY2h2, the census above).
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
                if (HandsLines.IsMatch(Statement(text, add.Index))) continue;
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
        @"^((_?context|_said)\.Said|Lines|said|Said)\.(Retry|Own|Wait|Progress|News|Of|Say|Ran)\(", RegexOptions.CultureInvariant);

    /// <summary>A statement that hands the look's classing to the producer whose lines it adds (NOTIFY2h2): <c>Lines</c> as an argument.</summary>
    private static readonly Regex HandsLines = new(@"[(,]\s*Lines\s*[),]", RegexOptions.CultureInvariant);

    /// <summary>The lists only carried on, each classed by the producer that made it.</summary>
    private static readonly string[] Carried =
    [
        // A settle's pending lines, classed where each pass said them.
        "lines);",
        // A failed look's lines, classed as that look made them.
        "said.Events);",
        // The lander's lines, classed where it made them (NOTIFY2h2: the lander is handed `Lines` as its `Look`).
        "said);",
    ];
}
