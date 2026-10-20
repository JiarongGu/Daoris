using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TRACE1 (D143): <c>daoris-driver trace &lt;commit|session|quest&gt;</c>, one read from a commit back to its ask. Six stores
/// already hold a change's provenance (the service's session records, quests and asks; this machine's events, rules files and
/// landings) and nothing read them together. Each link is printed from the store that keeps it, and a link nothing keeps is
/// said missing, never guessed.
/// </summary>
/// <remarks>
/// In-process, on stand-in records: the service is a stand-in reached through the real client, and the home a scratch folder
/// written by the driver's own writers (the events, the rules file, the landings). The suite's fast half.
/// </remarks>
public sealed class TraceTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-trace-" + Guid.NewGuid().ToString("N")[..8]);

    public TraceTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static DateTimeOffset At(int hour, int minute) => new(2026, 10, 3, hour, minute, 0, TimeSpan.Zero);

    private const string Sentence = "the dashboard figure reads zero; use the v3 bridge, as common-report does";

    private const string Answer = "common-report, not a report type of its own";

    /// <summary>The ask the quest was asked by: its sentence, an answer to the first session, and one go-ahead approved.</summary>
    private static string AskJson() =>
        $$$"""
        {"id":"a1","workspace":"work","sentence":"{{{Sentence}}}","state":"Published","tier":"intake","quests":["q1"],"intake":"i1",
         "words":[{"kind":"asked","text":"{{{Sentence}}}","at":"2026-10-03T09:00:00+00:00"},
                  {"kind":"answered","text":"{{{Answer}}}","at":"2026-10-03T09:20:00+00:00","session":"s1","quest":"q1"}],
         "goAheads":[{"number":1,"kind":"write","on":"production","act":"dashboard configuration","state":"approved",
           "asked":[{"session":"s1","quest":"q1","at":"2026-10-03T09:10:00+00:00","why":"The tile's target."}],
           "answer":{"approved":true,"words":"run the put","at":"2026-10-03T09:30:00+00:00"}}]}
        """;

    private static JsonObject Record(
        string id, string state, string? quest, int opened, int moved, string? evidence = null, string? profile = "work",
        string? harness = "2.1.4", string? tree = "C:/trees/dashboards-q1", string repository = "dashboards", string? ask = null,
        bool limit = false, string? note = null, string kind = "driven") => new()
    {
        ["id"] = id, ["quest"] = quest, ["repository"] = repository, ["adapter"] = "claude-code", ["state"] = state,
        ["note"] = note, ["evidence"] = evidence, ["created"] = At(9, 0).AddMinutes(opened).ToString("O"),
        ["updated"] = At(9, 0).AddMinutes(moved).ToString("O"), ["workspace"] = "work", ["kind"] = kind,
        ["harnessVersion"] = harness, ["profile"] = profile, ["tree"] = tree, ["baseCommit"] = tree is null ? null : "abc1234def567890",
        ["ask"] = ask, ["limit"] = limit,
    };

    /// <summary>
    /// The drift's shape (D133): an intake, then a quest's first session cut off by an account's limit, and a carry-on on another
    /// account in the same tree that closed it done and whose work landed on a branch.
    /// </summary>
    private static JsonArray Sessions() =>
    [
        Record("i1", "completed", null, 1, 3, repository: "ask #a1", ask: "a1", tree: null),
        Record("s1", "failed", "q1", 5, 15, evidence: "no commits landed", profile: "personal", harness: "2.1.3", limit: true,
            note: "the agent's turn failed: the account reached its spend limit."),
        Record("s2", "completed", "q1", 40, 60, evidence: "commits landed:\n1a2b3c4 Read the figure through v3\n9f8e7d6 Test the tile",
            note: "the quest closed done."),
    ];

    private static JsonObject Quest(
        string id, string from, string title, string status, JsonArray? requirements = null, JsonArray? answers = null) => new()
    {
        ["id"] = id, ["from"] = from, ["to"] = "dashboards", ["title"] = title, ["body"] = "Fix the figure.", ["status"] = status,
        ["note"] = status == "Done" ? "the figure reads through v3" : null, ["filed"] = At(9, 2).ToString("O"),
        ["updated"] = At(10, 0).ToString("O"), ["workspace"] = "work", ["links"] = new JsonArray(), ["attachments"] = new JsonArray(),
        ["then"] = new JsonArray(), ["parent"] = null, ["conflicts"] = new JsonArray(), ["publishedBy"] = "i1",
        ["requirements"] = requirements ?? new JsonArray(), ["answers"] = answers ?? new JsonArray(), ["held"] = false,
    };

    private static JsonArray Quests() =>
    [
        Quest("q1", "ask #a1", "Fix the dashboard figure", "Done",
            [new JsonObject { ["quote"] = "use the v3 bridge", ["check"] = "the report reads through bridge v3" }],
            [new JsonObject { ["requirement"] = 1, ["met"] = "the tile reads through v3 now" }]),
    ];

    /// <summary>
    /// A service standing in: each request recorded with its method, then answered, or 404 where nothing is. A quest's moves
    /// (FLOWCHART1a2) are answered by <paramref name="moves"/> with its status, and with none a host from before the door's 404.
    /// </summary>
    private sealed class StandIn(
        JsonArray sessions, JsonArray quests, Func<string, string?> asks, Func<string, (HttpStatusCode Status, string Body)?>? moves = null)
        : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            lock (Requests) Requests.Add($"{request.Method} {uri.PathAndQuery}");
            if (uri.AbsolutePath.StartsWith("/api/quests/", StringComparison.Ordinal) && uri.AbsolutePath.EndsWith("/moves", StringComparison.Ordinal)
                && moves?.Invoke(Uri.UnescapeDataString(uri.AbsolutePath["/api/quests/".Length..^"/moves".Length])) is var (status, answer))
            {
                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") });
            }

            var body = uri.AbsolutePath switch
            {
                "/api/sessions" when uri.Query.Contains("includeClosed=true") => sessions.ToJsonString(),
                "/api/quests" when uri.Query.Contains("includeClosed=true") => quests.ToJsonString(),
                var path when path.StartsWith("/api/asks/", StringComparison.Ordinal) => asks(path["/api/asks/".Length..]),
                _ => null,
            };
            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"error":"none"}""", Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    /// <summary>This machine's half, by the driver's own writers: each session's events, the live session's rules, a landing.</summary>
    private void Machine()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Note, Text = "opened on `personal`: the list begins there.", At = At(9, 5) });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = new string('x', 1234), At = At(9, 5) });
        events.Append("s2", new SessionEvent
        {
            Kind = SessionEventKind.Note,
            Text = "carried on from session `s1` on `work`; `personal` is cooling; its turn 3 was refused.",
            At = At(9, 40),
        });
        events.Append("s2", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "the instruction's own words " + new string('y', 2000), At = At(9, 40) });
        events.Append("s2", new SessionEvent { Kind = SessionEventKind.Message, Text = "Reading the tile.", At = At(9, 41) });

        new LandedBranches(_home).Record(new LandedBranch(
            "dashboards", "work", "feature/q1-fix-the-dashboard-figure", "main", "1a2b3c4d5e6f708192a3b4c5d6e7f80910111213", "s2", "q1",
            "Fix the dashboard figure", At(10, 5)) { From = "abc1234def567890", Plugin = "github", Pushed = true, PullRequest = "https://example.test/pull/7" });
    }

    private DriverConfig Config() => DriverConfig.Empty.WithStanding("dashboards", "test on dev first, never production", At(9, 25));

    private async Task<(int Exit, string Said, StandIn Service)> TraceAsync(
        TraceAsk asked, JsonArray? sessions = null, JsonArray? quests = null, Func<string, string?>? asks = null, DriverConfig? config = null,
        Func<string, (HttpStatusCode, string)?>? moves = null)
    {
        var standIn = new StandIn(sessions ?? Sessions(), quests ?? Quests(), asks ?? (id => id == "a1" ? AskJson() : null), moves);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));
        var said = new StringWriter { NewLine = "\n" };
        var exit = await TraceCommand.RunAsync(asked, new TraceSources(service, _home, config ?? Config()), said);
        return (exit, said.ToString(), standIn);
    }

    /// <summary>
    /// The proof, from a quest: the ask with the person's words and the go-ahead, the quest with its requirement and its answer,
    /// both sessions oldest first with their agent, account, harness, tree and base commit, the carry-on said in the driver's own
    /// words from its record, each instruction by its event and size and never its text, and the landing with its commits.
    /// </summary>
    [Fact]
    public async Task A_quest_reads_back_to_its_ask_through_both_its_sessions_and_the_carry_on()
    {
        Machine();

        var (exit, said, _) = await TraceAsync(new TraceAsk("q1"));

        Assert.Equal(0, exit);
        Assert.StartsWith("trace: quest #q1", said);
        // The ask, and the person's words verbatim, each saying how and when it was given.
        Assert.Contains("ask #a1 · the service's ask record", said);
        Assert.Contains("  > " + Sentence, said);
        Assert.Contains("2026-10-03 09:20 UTC · answered session s1, on quest #q1\n      > " + Answer, said);
        Assert.Contains("1 · write on production: \"dashboard configuration\" · approved 2026-10-03 09:30 UTC, saying: \"run the put\"", said);
        Assert.Contains("intake session i1 · completed · agent claude-code · harness 2.1.4 · account work", said);        // The quest, its requirement in the person's words with its check, and how its done answered it.
        Assert.Contains("quest #q1 → dashboards · the service's quest", said);
        Assert.Contains("1 · \"use the v3 bridge\" · check: the report reads through bridge v3", said);
        Assert.Contains("met: the tile reads through v3 now", said);
        Assert.Contains("published by session i1", said);
        // Both sessions, oldest first, each with what its record says ran it.
        var first = said.IndexOf("\nsession s1 ·", StringComparison.Ordinal);
        var second = said.IndexOf("\nsession s2 ·", StringComparison.Ordinal);
        Assert.True(first > 0 && second > first, said);
        Assert.Contains("agent claude-code · harness 2.1.3 · account personal", said);
        Assert.Contains("agent claude-code · harness 2.1.4 · account work", said);
        Assert.Contains("tree C:/trees/dashboards-q1 · grew from commit abc1234def567890", said);
        Assert.Contains("failed, by an account's limit", said);
        // The carry-on: the order on the quest is the records', and what it carried on is the driver's own line on its record.
        Assert.Contains("before it on this quest: session s1, ended failed 2026-10-03 09:15 UTC, in the same tree", said);
        Assert.Contains("its start, in the driver's words: carried on from session `s1` on `work`", said);
        // The instruction by its event and its size, never its words.
        Assert.Contains("instruction handed: event 2, 1,234 characters", said);
        Assert.Contains("instruction handed: event 2, 2,028 characters", said);
        Assert.DoesNotContain("the instruction's own words", said);
        // The evidence and the landing, each from where it is kept.
        Assert.Contains("1a2b3c4 Read the figure through v3", said);
        Assert.Contains("landing: branch feature/q1-fix-the-dashboard-figure at 1a2b3c4d5e6f708192a3b4c5d6e7f80910111213, from line main", said);
        Assert.Contains("pushed by plugin github, pull request https://example.test/pull/7", said);
    }

    /// <summary>
    /// What stood when each session started, by the moments the stores keep and nothing else: the standing answer set between
    /// the two starts, the go-ahead answered between them, and the person's words said before each.
    /// </summary>
    [Fact]
    public async Task What_stood_when_each_session_started_is_read_from_the_moments_kept()
    {
        Machine();

        var (_, said, _) = await TraceAsync(new TraceAsk("q1"));
        var s1 = Block(said, "session s1");
        var s2 = Block(said, "session s2");

        Assert.Contains("standing answer for dashboards: set 2026-10-03 09:25 UTC, after it started; what stood before is not kept", s1);
        Assert.Contains("standing answer for dashboards: set 2026-10-03 09:25 UTC, before it started:\n      > test on dev first, never production", s2);
        Assert.Contains("go-ahead 1: first asked after it started", s1);
        Assert.Contains("go-ahead 1: approved 2026-10-03 09:30 UTC, before it started", s2);
        Assert.Contains("the person's words on ask #a1: 1 of 2 said before it started", s1);
        Assert.Contains("the person's words on ask #a1: 2 of 2 said before it started", s2);
    }

    /// <summary>From a session: the same ask and quest, that session whole, and the quest's other session named for its own trace.</summary>
    [Fact]
    public async Task A_session_reads_back_to_its_ask_and_names_the_quests_other_sessions()
    {
        Machine();

        var (exit, said, _) = await TraceAsync(new TraceAsk("s2"));

        Assert.Equal(0, exit);
        Assert.StartsWith("trace: session s2", said);
        Assert.Contains("ask #a1 · the service's ask record", said);
        Assert.Contains("quest #q1 → dashboards", said);
        Assert.Contains("session records on it, oldest first: s1 failed, s2 completed", said);
        Assert.Contains("\nsession s2 ·", said);
        Assert.DoesNotContain("\nsession s1 ·", said);
        Assert.Contains("landing: branch feature/q1-fix-the-dashboard-figure", said);
    }

    /// <summary>
    /// From a commit: found in the session's evidence and as the tip of the branch its landing made, then the chain from there,
    /// by an abbreviation either way round.
    /// </summary>
    [Theory]
    [InlineData("1a2b3c4")]
    [InlineData("1a2b3c4d5e6f")]
    [InlineData("#1A2B3C4")]
    public async Task A_commit_reads_back_through_the_session_whose_evidence_and_landing_name_it(string commit)
    {
        Machine();

        var (exit, said, _) = await TraceAsync(new TraceAsk(commit));

        Assert.Equal(0, exit);
        Assert.StartsWith($"trace: commit {commit.TrimStart('#').ToLowerInvariant()}", said);
        Assert.Contains("found in session s2's evidence: 1a2b3c4 Read the figure through v3", said);
        Assert.Contains("found as the tip of branch feature/q1-fix-the-dashboard-figure, which session s2's landing made", said);
        Assert.Contains("ask #a1 · the service's ask record", said);
        Assert.Contains("quest #q1 → dashboards", said);
        Assert.Contains("\nsession s2 ·", said);
        Assert.DoesNotContain("\nsession s1 ·", said);
    }

    /// <summary>
    /// A chain with missing links, each said missing where it would have been: an ask the service no longer holds, a session
    /// with no events here, no harness version on its record, no evidence, an ended session's rules, and no landing.
    /// </summary>
    [Fact]
    public async Task A_missing_link_is_said_missing_and_never_guessed()
    {
        // Nothing of this machine's half: no events, no rules, no landing.
        JsonArray sessions = [Record("s1", "stopped", "q1", 5, 15, harness: null, profile: null)];

        var (exit, said, _) = await TraceAsync(new TraceAsk("q1"), sessions: sessions, asks: _ => null);

        Assert.Equal(0, exit);
        Assert.Contains("ask #a1 · not found: the service holds no ask by that id, which quest #q1's sender names", said);
        Assert.Contains("harness not recorded", said);
        Assert.Contains("account none named: the tool's own sign-in", said);
        Assert.Contains("evidence: none on its record", said);
        Assert.Contains("events: none on this machine for it", said);
        Assert.Contains("rules handed: not on this machine: the file goes when its session ends", said);
        Assert.Contains("landing: no branch landing on this machine names it, and no record of it here could say whether its work was accepted", said);
        Assert.DoesNotContain("instruction handed", said);
    }

    /// <summary>
    /// A landing into the line makes no branch, so <c>landings.json</c> holds none: the acceptance the press kept in the session's
    /// record is read instead (D100), and it says no commit of the merge's own.
    /// </summary>
    [Fact]
    public async Task A_merge_into_the_line_is_read_from_the_acceptance_its_record_keeps()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Append("s4", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "the quest", At = At(9, 5) });
        events.Append("s4", LandingRules.Note(new TreeLanding(true, "merged `daoris/s4` into `main` — 2 commit(s).")) with { At = At(11, 0) });
        JsonArray sessions = [Record("s4", "completed", "q1", 5, 15, evidence: "commits landed:\n7c7c7c7 Read through v3")];

        var (_, said, _) = await TraceAsync(new TraceAsk("s4"), sessions: sessions);

        Assert.Contains("    2026-10-03 11:00 UTC · the person accepted this work: merged `daoris/s4` into `main` — 2 commit(s).", said);
        Assert.Contains("landing: no branch landing on this machine names it; its record keeps the acceptance above, which is all a landing into the line (merge) keeps, without the merge's own commit", said);
    }

    /// <summary>
    /// CONTEXT1: an instruction is read by its sections from the account kept beside it, in the driver's words, each with what
    /// its bound left out, then what was handed beside it and what was not; never its words. One from before the account was
    /// kept says so.
    /// </summary>
    [Fact]
    public async Task An_instruction_is_read_by_the_sections_its_account_keeps_and_one_from_before_says_not_kept()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "the quest, from before", At = At(9, 5) });
        var composed = TargetPrompt.Composed(TargetPromptGoldenTests.Full with
        {
            Indexes = [.. Enumerable.Range(1, 10).Select(n => $"docs/INDEX{n}.md")],
        });
        var account = composed.Account.Beside(new HandedSection(HandedSections.Rules, HandedSources.Permissions, "the permission rules: 12 handed beside it") { Shown = 12 });
        events.Append("s2", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = composed.Text, Account = account, At = At(9, 40) });
        JsonArray sessions = [Record("s1", "completed", "q1", 5, 15), Record("s2", "completed", "q1", 40, 60)];

        var (_, said, _) = await TraceAsync(new TraceAsk("q1"), sessions: sessions);
        var s1 = Block(said, "session s1");
        var s2 = Block(said, "session s2");

        Assert.Contains("    its sections: not kept, since it was handed before the driver kept an account of them", s1);
        Assert.Contains(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"    its sections, as the driver composed them (event 1, {composed.Text.Length:N0} characters):"), s2);
        Assert.Contains("      the quest: ", s2);
        Assert.Contains("      the person's words: ", s2);
        Assert.Contains("      the repository's indexes: ", s2);
        Assert.Contains("        left out: 2 more counted, not named, past the 8 it names", s2);
        Assert.Contains("      the permission rules: 12 handed beside it", s2);
        // What could have been handed and was not, after what was.
        Assert.Contains("      not handed:\n        the session language: none set for its repository or its workspace on this machine\n", s2);
        Assert.Contains("        the code map: none, since its tree keeps none\n", s2);
        Assert.True(s2.IndexOf("the permission rules", StringComparison.Ordinal) < s2.IndexOf("not handed:", StringComparison.Ordinal), s2);
        Assert.DoesNotContain("The ticket asks for a daily comparison report", said);
    }

    /// <summary>A live session's rules are read from the file it was handed, by count, while it runs.</summary>
    [Fact]
    public async Task A_running_sessions_rules_are_read_from_the_file_it_was_handed()
    {
        SpawnSettings.Write(_home, "s3", new RuleLists(["Read", "Edit"], [], ["Bash(git push*)"]), ["Bash(rm -rf*)"]);
        JsonArray sessions = [Record("s3", "working", "q1", 5, 6)];

        var (_, said, _) = await TraceAsync(new TraceAsk("s3"), sessions: sessions);

        Assert.Contains("rules handed (spawn/s3.settings.json, kept while it runs): 2 allowed, 0 asked, 1 denied; 1 hard denial beside the harness's own", said);
    }

    /// <summary>
    /// A departure is read as its done wrote it and the hold as the quest keeps it, and an instruction the record cut is sized by
    /// what the record says it was, not by what it kept.
    /// </summary>
    [Fact]
    public async Task A_departure_its_hold_and_a_cut_instruction_are_read_as_kept()
    {
        new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).Append(
            "s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = new string('z', 70_000) });
        var quest = Quest("q1", "ask #a1", "Fix the dashboard figure", "Done",
            [new JsonObject { ["quote"] = "use the v3 bridge", ["check"] = "the report reads through bridge v3" }],
            [new JsonObject { ["requirement"] = 1, ["departed"] = "the bridge has no tile feed", ["quote"] = "as common-report does" }]);
        quest["held"] = true;
        JsonArray sessions = [Record("s1", "completed", "q1", 5, 15)];

        var (_, said, _) = await TraceAsync(new TraceAsk("q1"), sessions: sessions, quests: [quest]);

        Assert.Contains("departed: the bridge has no tile feed, on their words \"as common-report does\"", said);
        Assert.Contains("held: its done departed from what you required, and waits for your yes (`daoris-driver quest accept q1`)", said);
        Assert.Contains("instruction handed: event 1, 70,000 characters, of which the record keeps the first 65,536", said);
    }

    /// <summary>
    /// EVID1b (D144 §5): what was kept of a done's evidence reads back as kept: each requirement's evidence with what was read of
    /// it, the verdict's commit, how it was chosen, which session's end and which machine, and the hold's cause with its doors.
    /// </summary>
    [Fact]
    public async Task A_dones_evidence_and_its_hold_are_read_as_kept()
    {
        var commit = "0123456789abcdef0123456789abcdef01234567";
        var quest = Quest("q1", "ask #a1", "Fix the dashboard figure", "Done",
        [
            new JsonObject
            {
                ["quote"] = "use the v3 bridge", ["check"] = "the report reads through bridge v3",
                ["evidence"] = new JsonArray(new JsonObject { ["path"] = "docs/report.md" }, new JsonObject { ["path"] = "docs/tile.md" }),
            },
        ],
            [new JsonObject { ["requirement"] = 1, ["met"] = "the tile reads through v3 now" }]);
        quest["held"] = true;
        quest["hold"] = "evidence-missing";
        quest["awaitsEvidence"] = true;
        quest["evidence"] = new JsonObject
        {
            ["commit"] = commit, ["how"] = "session-end", ["session"] = "s2", ["at"] = At(10, 5).ToString("O"), ["machine"] = "laptop",
            ["items"] = new JsonArray(
                new JsonObject { ["requirement"] = 1, ["path"] = "docs/report.md", ["result"] = "found", ["object"] = new string('b', 40), ["changed"] = true },
                new JsonObject { ["requirement"] = 1, ["path"] = "docs/tile.md", ["result"] = "case", ["spelled"] = "docs/Tile.md", ["changed"] = false }),
        };

        var (_, said, _) = await TraceAsync(new TraceAsk("q1"), quests: [quest]);

        Assert.Contains("        evidence: `docs/report.md` found, changed by this work · `docs/tile.md` case, the commit spells it `docs/Tile.md`\n", said);
        Assert.Contains("  held: its evidence was not found in the commit read, and waits for you: a later commit that holds it "
            + "(`daoris-driver quest check q1 --commit <sha>`), or your yes to the done as it stands (`daoris-driver quest accept q1`)\n", said);
        Assert.Contains($"  evidence read at `{commit[..8]}` (session-end), at the end of session s2 on laptop, 2026-10-03 10:05 UTC: 1 of 2 found\n", said);
        Assert.DoesNotContain("departed from what you required", said);
    }

    /// <summary>
    /// FABRIC-CLOSE1c6: a submission whose answers name no evidence has its commit read alone at its session's end, and the trace says it as
    /// the submission's commit, never as evidence read with nothing found.
    /// </summary>
    [Fact]
    public async Task A_submissions_commit_read_alone_is_read_as_its_commit()
    {
        var commit = "0123456789abcdef0123456789abcdef01234567";
        var quest = Quest("q1", "ask #a1", "Fix the dashboard figure", "Taken");
        quest["evidence"] = new JsonObject
        {
            ["commit"] = commit, ["how"] = "session-end", ["session"] = "s2", ["at"] = At(10, 5).ToString("O"), ["machine"] = "laptop",
            ["items"] = new JsonArray(), ["submission"] = new JsonObject { ["machine"] = "laptop", ["sequence"] = 42 },
        };

        var (_, said, _) = await TraceAsync(new TraceAsk("q1"), quests: [quest]);

        Assert.Contains($"  its submission's commit, read at `{commit[..8]}` at the end of session s2 on laptop, 2026-10-03 10:05 UTC\n", said);
        Assert.DoesNotContain("evidence read at", said);
    }

    /// <summary>EVID1b: a done whose evidence nobody read yet says so, and says the driver reads it when the session ends.</summary>
    [Fact]
    public async Task A_dones_unread_evidence_says_it_is_not_read_yet()
    {
        var quest = Quest("q1", "ask #a1", "Fix the dashboard figure", "Done",
            [new JsonObject { ["quote"] = "use the v3 bridge", ["check"] = "reads", ["evidence"] = new JsonArray(new JsonObject { ["path"] = "docs/report.md" }) }],
            [new JsonObject { ["requirement"] = 1, ["met"] = "it reads" }]);
        quest["held"] = true;
        quest["hold"] = "evidence-unread";
        quest["awaitsEvidence"] = true;

        var (_, said, _) = await TraceAsync(new TraceAsk("q1"), quests: [quest]);

        Assert.Contains("        evidence: `docs/report.md` not read yet\n", said);
        Assert.Contains("  held: its evidence is not read yet: the driver reads it when the session that closed it ends, or "
            + "`daoris-driver quest check q1` reads it; your yes takes the done as it stands (`daoris-driver quest accept q1`)\n", said);
    }

    /// <summary>A quest no ask asked says so, and reads its sessions all the same.</summary>
    [Fact]
    public async Task A_quest_on_no_ask_says_it_has_none()
    {
        JsonArray quests = [Quest("q1", "reports", "Expose the figure", "Taken")];
        JsonArray sessions = [Record("s1", "working", "q1", 5, 6)];

        var (exit, said, standIn) = await TraceAsync(new TraceAsk("q1"), sessions: sessions, quests: quests);

        Assert.Equal(0, exit);
        Assert.Contains("on no ask: quest #q1 was asked by `reports`", said);
        Assert.DoesNotContain(standIn.Requests, request => request.Contains("/api/asks/", StringComparison.Ordinal));
        Assert.Contains("\nsession s1 ·", said);
    }

    /// <summary>A teammate's record ran on another machine: its account, tree and events stay there, and are said to.</summary>
    [Fact]
    public async Task A_teammates_session_says_what_stays_on_its_own_machine()
    {
        JsonArray sessions = [Record("laptop/s9", "completed", "q1", 5, 6, profile: null, tree: null)];

        var (_, said, _) = await TraceAsync(new TraceAsk("laptop/s9"), sessions: sessions);

        Assert.Contains("a teammate's record: it ran on another machine, which keeps its account, tree, events and rules", said);
        Assert.DoesNotContain("account none named", said);
    }

    /// <summary>Nothing found is an answer, exit 1, naming what was searched.</summary>
    [Fact]
    public async Task Nothing_found_says_what_was_searched()
    {
        var (exit, said, _) = await TraceAsync(new TraceAsk("deadbee"));

        Assert.Equal(1, exit);
        Assert.Contains("nothing here names `deadbee`", said);
        Assert.Contains("no session record, no quest, no session's evidence and no landing", said);
    }

    /// <summary>An id that names two kinds of thing is refused naming both, and the explicit form reads the one named.</summary>
    [Fact]
    public async Task An_id_naming_two_things_asks_which_and_the_named_kind_reads_it()
    {
        Machine();
        var sessions = Sessions();
        sessions.Add(Record("9f8e7d6", "completed", null, 70, 71, kind: "chat"));

        var (exit, said, _) = await TraceAsync(new TraceAsk("9f8e7d6"), sessions: sessions);
        Assert.Equal(1, exit);
        Assert.Contains("`9f8e7d6` names more than one thing here: session 9f8e7d6 and commit 9f8e7d6", said);
        Assert.Contains("trace session 9f8e7d6", said);

        var (named, sessionSaid, _) = await TraceAsync(new TraceAsk("9f8e7d6", TraceEntry.Session), sessions: sessions);
        Assert.Equal(0, named);
        Assert.Contains("a conversation on no quest: no quest and no ask to read", sessionSaid);

        var (commit, commitSaid, _) = await TraceAsync(new TraceAsk("9f8e7d6", TraceEntry.Commit), sessions: sessions);
        Assert.Equal(0, commit);
        Assert.Contains("found in session s2's evidence: 9f8e7d6 Test the tile", commitSaid);
    }

    /// <summary>
    /// 🔴 Read-only: the home is byte for byte what it was, and every request to the service is a read. The trace writes nothing
    /// anywhere (D143).
    /// </summary>
    [Fact]
    public async Task The_trace_writes_nothing_and_only_reads()
    {
        Machine();
        SpawnSettings.Write(_home, "s2", new RuleLists(["Read"], [], []), []);
        var before = Snapshot(_home);

        var (_, _, standIn) = await TraceAsync(new TraceAsk("q1"));

        Assert.Equal(before, Snapshot(_home));
        Assert.NotEmpty(standIn.Requests);
        Assert.All(standIn.Requests, request => Assert.StartsWith("GET ", request));
    }

    /// <summary>
    /// REVIEWENV1c (design §3.5): the landing record's review reads back on the landing it let go, the person's reviewed with its
    /// environment, set-up step and commit, or their skip with their words; a landing no review was asked of says nothing of one.
    /// </summary>
    [Fact]
    public async Task A_landings_review_reads_back_as_reviewed_or_skipped()
    {
        new LandedBranches(_home).Record(new LandedBranch(
            "dashboards", "work", "feature/q1-fix-the-dashboard-figure", "main", "1a2b3c4d5e6f708192a3b4c5d6e7f80910111213", "s2", "q1",
            "Fix the dashboard figure", At(10, 5))
        {
            AcceptedBy = AcceptedBy.Person,
            Review = new LandingReview(ReviewVerdicts.Reviewed, "local", "q2")
            {
                Commit = "0123456789abcdef0123456789abcdef01234567", At = At(10, 2), Words = "right",
            },
        });

        var (_, reviewed, _) = await TraceAsync(new TraceAsk("s2"));
        Assert.Contains("; accepted by the person's press; reviewed by you in `local` on set-up step #q2 at 01234567, 2026-10-03 10:02 UTC",
            Block(reviewed, "session s2"));

        new LandedBranches(_home).Record(new LandedBranch(
            "dashboards", "work", "feature/q1-fix-the-dashboard-figure", "main", "1a2b3c4d5e6f708192a3b4c5d6e7f80910111213", "s2", "q1",
            "Fix the dashboard figure", At(10, 5))
        {
            AcceptedBy = AcceptedBy.Person,
            Review = new LandingReview(ReviewVerdicts.Skipped, null, "q1") { Words = "a readme change" },
        });

        var (_, skipped, _) = await TraceAsync(new TraceAsk("s2"));
        Assert.Contains("; landed without a review, which you skipped on quest #q1, saying: \"a readme change\"", Block(skipped, "session s2"));
    }

    /// <summary>
    /// WORKFLOW1f (the workflow design §5.5): the landing record's named workflow reads back on the landing it decided, its version
    /// and the level that chose it; a landing under Current, and one from before, says nothing of one.
    /// </summary>
    [Fact]
    public async Task A_landings_named_workflow_reads_back_with_its_version_and_what_chose_it()
    {
        new LandedBranches(_home).Record(new LandedBranch(
            "dashboards", "work", "feature/q1-fix-the-dashboard-figure", "main", "1a2b3c4d5e6f708192a3b4c5d6e7f80910111213", "s2", "q1",
            "Fix the dashboard figure", At(10, 5))
        {
            AcceptedBy = AcceptedBy.Auto,
            Workflow = new LandingWorkflow("docs-to-pr", 2, WorkflowLevels.WorkspaceKind) { Kind = "docs" },
        });

        var (_, named, _) = await TraceAsync(new TraceAsk("s2"));
        Assert.Contains("; accepted automatically when its quest was done; as workflow `docs-to-pr` v2 says, chosen by its workspace for kind `docs`",
            Block(named, "session s2"));
        Assert.Equal("; as workflow `docs-to-pr` v1 says, its repository's default",
            TraceWords.WorkflowSaid(new LandingWorkflow("docs-to-pr", 1, WorkflowLevels.Repository)));

        new LandedBranches(_home).Record(new LandedBranch(
            "dashboards", "work", "feature/q1-fix-the-dashboard-figure", "main", "1a2b3c4d5e6f708192a3b4c5d6e7f80910111213", "s2", "q1",
            "Fix the dashboard figure", At(10, 5)) { AcceptedBy = AcceptedBy.Auto });
        var (_, current, _) = await TraceAsync(new TraceAsk("s2"));
        Assert.DoesNotContain("as workflow", Block(current, "session s2"));
    }

    /// <summary>
    /// LAND2b (D145 point 6, design §8): a landing at the quest's done is read back as accepted automatically, with the rule it
    /// was made under; the conversation's acceptance note is read as one; the due list's tries are each read by their code. A
    /// landing from before who accepted it was kept says so, never guessed.
    /// </summary>
    [Fact]
    public async Task An_automatic_acceptance_reads_back_with_its_rule_its_note_and_each_try()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Append("s2", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "the quest", At = At(9, 40) });
        var landed = new TreeLanding(true, "put the work on `feature/q1-fix-the-dashboard-figure` — 2 commit(s) from `main`.",
            "feature/q1-fix-the-dashboard-figure");
        events.Append("s2", AutoLandingNotes.Of(AutoLandingCode.Landed, landed) with { At = At(10, 5) });
        new LandedBranches(_home).Record(new LandedBranch(
            "dashboards", "work", "feature/q1-fix-the-dashboard-figure", "main", "1a2b3c4d5e6f708192a3b4c5d6e7f80910111213", "s2", "q1",
            "Fix the dashboard figure", At(10, 5))
        {
            Plugin = "acme.lands", Pushed = true, PullRequest = "https://example.test/pull/7", AcceptedBy = AcceptedBy.Auto,
            Rule = new LandedRule("acme.lands", AutoAccept: true, LandingSource.Workspace),
        });
        var due = new AutoLandings(_home);
        due.Due(new AutoLanding("s2", "q1", "dashboards", "work", "C:/trees/dashboards-q1", At(10, 0)));
        due.Tried("s2", new AutoTry(At(10, 1), AutoLandingCode.Uncommitted) { Tip = "9f8e7d6c5b4a", Status = "sha256:abcd", Uncommitted = 2 }, close: false);
        due.Tried("s2", new AutoTry(At(10, 5), AutoLandingCode.Landed)
        {
            Tip = "1a2b3c4d5e6f708192", Status = "clean", Branch = "feature/q1-fix-the-dashboard-figure", Commits = 2,
        }, close: true);

        var (exit, said, _) = await TraceAsync(new TraceAsk("s2"));
        var block = Block(said, "session s2");

        Assert.Equal(0, exit);
        Assert.Contains("2026-10-03 10:05 UTC · accepted automatically when its quest was done: its work is on `feature/q1-fix-the-dashboard-figure`.", block);
        Assert.Contains("pushed by plugin acme.lands, pull request https://example.test/pull/7; accepted automatically when its quest was done; "
            + "under the workspace's rule, naming plugin acme.lands, accepting automatically", block);
        Assert.Contains("due to land automatically since 2026-10-03 10:00 UTC (auto-landings.json, this machine's), closed 2026-10-03 10:05 UTC", block);
        Assert.Contains("2026-10-03 10:01 UTC · uncommitted · 2 uncommitted path(s) · at 9f8e7d6c5b4a", block);
        Assert.Contains("2026-10-03 10:05 UTC · landed · branch feature/q1-fix-the-dashboard-figure · 2 commit(s)", block);

        // A landing recorded before LAND2b kept who accepted it: said not kept, and no rule is made up.
        new LandedBranches(_home).Record(new LandedBranch(
            "dashboards", "work", "feature/q1-fix-the-dashboard-figure", "main", "1a2b3c4d5e6f708192a3b4c5d6e7f80910111213", "s2", "q1",
            "Fix the dashboard figure", At(10, 5)));
        var (_, older, _) = await TraceAsync(new TraceAsk("s2"));
        Assert.Contains("; who accepted it is not kept: it landed before landings kept it", older);
        Assert.DoesNotContain("'s rule, naming", Block(older, "session s2"));
    }

    /// <summary>ACCESSWORDS1: a landing the access setup accepted says so in the trace, with the level that set it; a rule's own switch does not.</summary>
    [Fact]
    public async Task A_landing_the_access_setup_accepted_reads_back_saying_so()
    {
        new LandedBranches(_home).Record(new LandedBranch(
            "dashboards", "work", "feature/q1-fix-the-dashboard-figure", "main", "1a2b3c4d5e6f708192a3b4c5d6e7f80910111213", "s2", "q1",
            "Fix the dashboard figure", At(10, 5))
        {
            AcceptedBy = AcceptedBy.Auto,
            Rule = new LandedRule(null, AutoAccept: true, LandingSource.Workspace) { Access = AccessRules.FromMachine },
        });

        var (_, said, _) = await TraceAsync(new TraceAsk("s2"));

        Assert.Contains("under the workspace's rule, naming no plugin, accepted by the access setup (machine)", Block(said, "session s2"));
    }

    /// <summary>ACCESSWORDS1c: a landing the person pressed under the access setup keeps the level, and the trace does not credit the setup with it.</summary>
    [Fact]
    public async Task A_landing_the_person_pressed_under_the_access_setup_does_not_say_the_setup_accepted_it()
    {
        new LandedBranches(_home).Record(new LandedBranch(
            "dashboards", "work", "feature/q1-fix-the-dashboard-figure", "main", "1a2b3c4d5e6f708192a3b4c5d6e7f80910111213", "s2", "q1",
            "Fix the dashboard figure", At(10, 5))
        {
            AcceptedBy = AcceptedBy.Person,
            Rule = new LandedRule(null, AutoAccept: true, LandingSource.Workspace) { Access = AccessRules.FromMachine },
        });

        var (_, said, _) = await TraceAsync(new TraceAsk("s2"));
        var block = Block(said, "session s2");

        Assert.Contains("; accepted by the person's press; under the workspace's rule, naming no plugin, accepting automatically", block);
        Assert.DoesNotContain("accepted by the access setup", block);
    }

    /// <summary>
    /// PLUGHOOK1c (D148 point 6, design §2.5): a landing reads back with what its plugin last answered about its pull request, with
    /// when, and a failed ask beside it. The trace reads the kept answer and asks nothing.
    /// </summary>
    [Fact]
    public async Task A_landing_reads_back_with_its_pull_requests_kept_answer()
    {
        Machine();
        var landings = new LandedBranches(_home);
        var entry = landings.Landing("s2")!;
        landings.Answered(entry, new PullRequestState(PullRequestStates.Completed)
        {
            PullRequest = "https://example.test/pull/7", MergeCommit = new string('a', 40), SourceCommit = new string('b', 40),
            Target = "main", How = MergeHow.Squash, At = At(11, 0), Plugin = "github", AskedAt = At(11, 2),
        });
        landings.AskFailed(landings.Landing("s2")!, new PullRequestAskFailed(PluginEvents.Late, "github", At(11, 30)));

        var (_, said, _) = await TraceAsync(new TraceAsk("s2"));

        Assert.Contains("pull request https://example.test/pull/7; its pull request: completed by squash into `main` on 2026-10-03 11:00 UTC, "
            + "as `github` answered at 2026-10-03 11:02 UTC; asking `github` again at 2026-10-03 11:30 UTC failed (`late`)", Block(said, "session s2"));
    }

    /// <summary>A service that cannot be read is said, store by store, and a trace that needed it could not: exit 2.</summary>
    /// <summary>
    /// A quest's moves as the service's door answers them (FLOWCHART1a2), in the order its history replays: a take, another machine's
    /// take kept as a conflict and dismissed by its reference, a done that held it, a second done the replay did not apply, this
    /// machine's yes still pending though its clock made it earliest, and a kind this driver does not know.
    /// </summary>
    private const string MovesJson = """
        {"quest":"q1","moves":[
          {"kind":"published","at":"2026-10-03T09:02:00+00:00","machine":"m2","sequence":1,"number":1,"applied":true},
          {"kind":"taken","at":"2026-10-03T09:05:00+00:00","machine":"m3","sequence":1,"number":2,"applied":true,"from":"Open"},
          {"kind":"conflict","at":"2026-10-03T09:06:00+00:00","machine":"m4","sequence":7,"number":3,"applied":true,"from":"Taken","attempted":"Taken"},
          {"kind":"dismissed","at":"2026-10-03T09:30:00+00:00","machine":"m3","sequence":2,"number":4,"applied":true,"from":"Taken",
           "dismisses":{"machine":"m4","sequence":7}},
          {"kind":"done","at":"2026-10-03T10:00:00+00:00","machine":"m3","sequence":3,"number":5,"applied":true,"from":"Taken","hold":"departed"},
          {"kind":"done","at":"2026-10-03T10:01:00+00:00","machine":"m5","sequence":1,"number":6,"applied":false,"from":"Done"},
          {"kind":"accepted","at":"2026-10-03T08:00:00+00:00","machine":"laptop","sequence":9,"applied":true,"from":"Done","hold":"none"},
          {"kind":"rewound","at":"2026-10-03T10:02:00+00:00","machine":"m6","sequence":1,"applied":false,"from":"Done"}
        ]}
        """;

    /// <summary>
    /// FLOWCHART1a2 (D186; the flowchart design §2.1, trap 20): the quest's link carries its moves and the terminal says them, in the
    /// order they replay and never by time: each with its machine, its time and the remote's number or none, one the replay did not
    /// apply said recorded and not applied, the hold one set or let go, a dismissal matched to its conflict by its reference, and a
    /// kind this driver does not know shown as recorded. The moves are read for the quest the trace reached, and for no other.
    /// </summary>
    [Fact]
    public async Task A_quests_moves_are_carried_on_its_link_and_said_in_the_order_they_replay()
    {
        var (exit, said, standIn) = await TraceAsync(new TraceAsk("q1"), moves: id => id == "q1" ? (HttpStatusCode.OK, MovesJson) : null);

        Assert.Equal(0, exit);
        Assert.Contains("  its moves (8), in the order they replay (the service's history, GET /api/quests/q1/moves):\n"
            + "    1 · published, on m2, 2026-10-03 09:02 UTC · the remote's #1\n"
            + "    2 · taken, on m3, 2026-10-03 09:05 UTC · the remote's #2\n"
            + "    3 · its Taken kept as a conflict, on m4, 2026-10-03 09:06 UTC · the remote's #3\n"
            + "    4 · dismissed the conflict of move 3, on m3, 2026-10-03 09:30 UTC · the remote's #4\n"
            + "    5 · done, on m3, 2026-10-03 10:00 UTC · the remote's #5 · held: departed\n"
            + "    6 · done, on m5, 2026-10-03 10:01 UTC · the remote's #6 · recorded, not applied: it stood Done\n"
            + "    7 · accepted: the person's yes to what held it, on laptop, 2026-10-03 08:00 UTC · only here: no remote has numbered it yet · let go what held it\n"
            + "    8 · `rewound`, shown as recorded, on m6, 2026-10-03 10:02 UTC · only here: no remote has numbered it yet · recorded, not applied: it stood Done\n",
            said);
        Assert.DoesNotContain("no door on a local host reads the operations themselves", said);
        Assert.Equal(["GET /api/quests/q1/moves"], standIn.Requests.Where(request => request.EndsWith("/moves", StringComparison.Ordinal)));

        using var service = new ServiceClient("http://stand-in", null, new HttpClient(
            new StandIn(Sessions(), Quests(), id => id == "a1" ? AskJson() : null, id => id == "q1" ? (HttpStatusCode.OK, MovesJson) : null)));
        var read = await Trace.ReadChainAsync(new TraceAsk("q1"), new TraceSources(service, _home, Config()));
        var link = read.Chain!.Links.Select(each => each.Quest).OfType<TraceQuestLink>().Single();
        Assert.Null(link.MovesMissing);
        Assert.Equal(["published", "taken", "conflict", "dismissed", "done", "done", "accepted", "rewound"], link.Moves!.Select(move => move.Kind));
        Assert.Equal(new TraceMoveRef("m4", 7), link.Moves![3].Dismisses);
        Assert.Equal((false, "Done"), (link.Moves![5].Applied, link.Moves![5].From));
        Assert.Null(link.Moves![6].Number);
    }

    /// <summary>
    /// Trap 18: a host from before the moves door answers it not found, which is no failure of the trace: the quest is read as before,
    /// and its moves are said not kept there. A door that fails is said, in its words, the terminal's alone.
    /// </summary>
    [Fact]
    public async Task A_host_without_the_moves_door_says_the_path_is_not_kept_and_one_that_fails_says_why()
    {
        var (exit, said, _) = await TraceAsync(new TraceAsk("q1"));

        Assert.Equal(0, exit);
        Assert.Contains("met: the tile reads through v3 now", said);
        Assert.Contains("  its moves: this service answers none, a host from before the moves door, so its path is not kept here\n", said);

        var (_, failed, _) = await TraceAsync(new TraceAsk("q1"), moves: _ => (HttpStatusCode.InternalServerError, """{"error":"the history did not read"}"""));
        Assert.Contains("  its moves could not be read: ", failed);
        Assert.Contains("the history did not read", failed);

        using var service = new ServiceClient("http://stand-in", null, new HttpClient(
            new StandIn(Sessions(), Quests(), id => id == "a1" ? AskJson() : null)));
        var link = (await Trace.ReadChainAsync(new TraceAsk("q1"), new TraceSources(service, _home, Config())))
            .Chain!.Links.Select(each => each.Quest).OfType<TraceQuestLink>().Single();
        Assert.Equal((TraceLinkGaps.NoDoor, null), (link.MovesMissing, link.Moves));
    }

    [Fact]
    public async Task A_service_that_does_not_answer_is_said_and_the_trace_could_not()
    {
        var said = new StringWriter { NewLine = "\n" };
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Refusing()));

        var exit = await TraceCommand.RunAsync(new TraceAsk("q1"), new TraceSources(service, _home, DriverConfig.Empty), said);

        Assert.Equal(2, exit);
        Assert.Contains("the service's session records could not be read:", said.ToString());
        Assert.Contains("the service's quests could not be read:", said.ToString());
    }

    /// <summary>The words the terminal takes: an id, or a kind and an id; anything else says why.</summary>
    [Theory]
    [InlineData("q1", "q1", null)]
    [InlineData("#q1", "#q1", null)]
    [InlineData("quest q1", "q1", TraceEntry.Quest)]
    [InlineData("session s2", "s2", TraceEntry.Session)]
    [InlineData("commit 1a2b3c4", "1a2b3c4", TraceEntry.Commit)]
    public void The_terminals_words_name_what_to_trace(string words, string id, string? kind)
    {
        var asked = TraceCommand.Read(words.Split(' '), out var problem);

        Assert.Null(problem);
        Assert.Equal(new TraceAsk(id, kind), asked);
    }

    [Theory]
    [InlineData("", "one commit, session or quest")]
    [InlineData("q1 q2", "one commit, session or quest")]
    [InlineData("commit xyz1", "a commit is named by at least 4 of its hexadecimal digits")]
    [InlineData("commit 1a2", "a commit is named by at least 4 of its hexadecimal digits")]
    [InlineData("branch main", "one commit, session or quest")]
    [InlineData("--help", "one commit, session or quest")]
    public void The_terminals_words_that_do_not_read_say_why(string words, string why)
    {
        Assert.Null(TraceCommand.Read(words.Split(' ', StringSplitOptions.RemoveEmptyEntries), out var problem));
        Assert.Contains(why, problem);
        Assert.StartsWith("usage: daoris-driver trace <commit|session|quest>", TraceCommand.Usage);
    }

    /// <summary>One section of the output: from its heading line to the next blank line.</summary>
    private static string Block(string said, string heading)
    {
        var start = said.IndexOf("\n" + heading + " ·", StringComparison.Ordinal);
        Assert.True(start >= 0, $"no `{heading}` in:\n{said}");
        var end = said.IndexOf("\n\n", start + 1, StringComparison.Ordinal);
        return end < 0 ? said[start..] : said[start..end];
    }

    /// <summary>Every file under a folder, by its path and a hash of its bytes.</summary>
    private static string Snapshot(string folder) =>
        string.Join("\n", Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(file => $"{Path.GetRelativePath(folder, file)} {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))}"));

    private sealed class Refusing : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("connection refused");
    }
}
