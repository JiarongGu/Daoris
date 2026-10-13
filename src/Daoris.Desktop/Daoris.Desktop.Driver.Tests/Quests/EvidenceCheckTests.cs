using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// EVID1b (D144 §3, §5): the driver reads a done's evidence and posts its verdict to the one door that takes it
/// (<c>POST /api/quests/{id}/evidence</c>, EVID1a's), keeps the verdict in the session record's evidence bundle, and says one
/// <c>evidence.checked</c> line in the machine log, counts and codes only. The service's answer comes back in its words: kept,
/// not waiting (409), refused (400), no quest (404), or a host older than the door.
/// </summary>
/// <remarks>A service and git standing in, in process: the fast half (MOD8).</remarks>
public sealed class EvidenceCheckTests : IDisposable
{
    private readonly string _tree = Path.Combine(Path.GetTempPath(), "daoris-evidence-check-" + Guid.NewGuid().ToString("N")[..8]);

    public EvidenceCheckTests() => Directory.CreateDirectory(_tree);

    public void Dispose()
    {
        try { Directory.Delete(_tree, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private const string QuestJson = """
        [{"id":"q1","from":"ask #a1","to":"reports","title":"t","body":"b","status":"Done","held":true,
          "hold":"evidence-missing","awaitsEvidence":true,
          "requirements":[{"quote":"write it up","check":"a page","evidence":[{"path":"docs/report.md"},{"gate":"web"}]},
                          {"quote":"keep it","check":"kept","evidence":[]}],
          "answers":[{"requirement":1,"met":"written"},{"requirement":2,"met":"kept"}],
          "evidence":{"commit":"0123456789ABCDEF0123456789abcdef01234567","how":"session-end","session":"s1",
            "at":"2026-10-07T01:02:03+00:00","machine":"laptop",
            "items":[{"requirement":1,"path":"docs/report.md","result":"case","spelled":"Docs/report.md","changed":false},
                     {"requirement":1,"gate":"web","result":"no-queue"}]}},
         {"id":"q2","from":"reports","to":"backend","title":"t","body":"b","status":"Open"}]
        """;

    [Fact]
    public async Task The_clients_read_of_a_quest_carries_each_requirements_evidence_its_hold_and_what_was_read()
    {
        using var service = Client(request => request.RequestUri!.AbsolutePath == "/api/quests" ? (HttpStatusCode.OK, QuestJson) : null);

        var quest = (await service.FindQuestAsync("q1"))!;
        var plain = (await service.FindQuestAsync("q2"))!;

        Assert.Equal(
            [
                new QuestRequirementView("write it up", "a page") { Evidence = [new("docs/report.md"), new(null, "web")] },
                new QuestRequirementView("keep it", "kept"),
            ],
            quest.Requirements);
        Assert.Equal((EvidenceCodes.MissingHold, true), (quest.Hold, quest.AwaitsEvidence));
        Assert.Equal(
            new EvidenceVerdict(
                "0123456789abcdef0123456789abcdef01234567", EvidenceCodes.SessionEnd,
                [
                    new EvidenceRead(1, "docs/report.md", null, EvidenceCodes.Case) { Spelled = "Docs/report.md", Changed = false },
                    new EvidenceRead(1, null, "web", EvidenceCodes.NoQueue),
                ])
            {
                Session = "s1", At = DateTimeOffset.Parse("2026-10-07T01:02:03+00:00"), Machine = "laptop",
            },
            quest.Evidence);
        // Absent is none: a quest that names none, and a host from before evidence.
        Assert.Equal((null, false, null), (plain.Hold, plain.AwaitsEvidence, plain.Evidence));
        Assert.Empty(plain.Requirements);
    }

    [Fact]
    public async Task A_verdict_goes_through_the_evidence_door_as_the_door_takes_it()
    {
        string? posted = null;
        string? path = null;
        using var service = Client(request =>
        {
            path = request.RequestUri!.AbsolutePath;
            posted = request.Content!.ReadAsStringAsync().Result;
            return (HttpStatusCode.OK, """{"quest":{"id":"q1"},"message":"Read the evidence of quest `#q1` at `01234567`: each of its 1 item is there, so what it held goes on."}""");
        });
        var verdict = new EvidenceVerdict(
            new string('a', 40), EvidenceCodes.SessionEnd,
            [
                new EvidenceRead(1, "docs/report.md", null, EvidenceCodes.Found) { Object = new string('b', 40), Changed = true },
                new EvidenceRead(2, "Docs/x.md", null, EvidenceCodes.Case) { Spelled = "docs/x.md" },
                new EvidenceRead(3, null, "web", EvidenceCodes.NoQueue),
            ])
        { Session = "s1" };

        var answer = await service.EvidenceAsync("#q1", verdict);

        Assert.Equal(new EvidencePosted(EvidencePosted.Kept, "Read the evidence of quest `#q1` at `01234567`: each of its 1 item is there, so what it held goes on."), answer);
        Assert.Equal("/api/quests/q1/evidence", path);
        using var body = JsonDocument.Parse(posted!);
        Assert.Equal(
            """{"commit":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","how":"session-end","session":"s1","items":[{"requirement":1,"path":"docs/report.md","result":"found","object":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","changed":true},{"requirement":2,"path":"Docs/x.md","result":"case","spelled":"docs/x.md"},{"requirement":3,"gate":"web","result":"no-queue"}]}""",
            JsonSerializer.Serialize(body.RootElement));
        // The terminal's check names no session.
        Assert.DoesNotContain("\"session\":", (verdict with { Session = null }).Json());
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, """{"error":"Quest `#q1`'s done was accepted as it stood: nothing waits on its evidence."}""", EvidencePosted.NotWaiting)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"Item 1 names exactly one of `path` or `gate`. Nothing was kept."}""", EvidencePosted.Refused)]
    [InlineData(HttpStatusCode.NotFound, """{"error":"No quest `#q1`. Ids come from `quest_list`."}""", EvidencePosted.Refused)]
    public async Task A_refusal_is_an_answer_in_the_services_words(HttpStatusCode status, string body, string outcome)
    {
        using var service = Client(_ => (status, body));

        var answer = await service.EvidenceAsync("q1", new EvidenceVerdict(new string('a', 40), EvidenceCodes.Sweep, []));

        Assert.Equal(outcome, answer.Outcome);
        Assert.Equal(JsonDocument.Parse(body).RootElement.GetProperty("error").GetString(), answer.Message);
    }

    [Fact]
    public async Task A_host_older_than_the_door_is_said_to_be_older_never_read_as_nothing()
    {
        using var service = Client(_ => null);

        var answer = await service.EvidenceAsync("q1", new EvidenceVerdict(new string('a', 40), EvidenceCodes.Sweep, []));

        Assert.Equal(EvidencePosted.NoDoor, answer.Outcome);
        Assert.Contains("older than this driver", answer.Message);
    }

    /// <summary>
    /// 🔴 A session's end: the tree's HEAD read, the verdict posted with its session and how, the record's bundle saying it, and
    /// one machine-log line with counts and codes, never a path.
    /// </summary>
    [Fact]
    public async Task A_sessions_end_reads_posts_keeps_and_says_one_line()
    {
        var git = new StandInGit(_tree);
        var before = git.Commit("base", ("README.md", "r"));
        var head = git.Commit("done", ("README.md", "r"), ("docs/report.md", "report"));
        string? posted = null;
        using var service = Client(request =>
        {
            posted = request.Content!.ReadAsStringAsync().Result;
            return (HttpStatusCode.OK, """{"quest":{"id":"q1"},"message":"Read the evidence: each of its 2 items is there, so what it held goes on."}""");
        });
        var lines = new List<EvidenceLine>();
        service.EvidenceLined += lines.Add;
        var quest = Quest(["docs/report.md", "README.md"]);

        var outcome = await EvidenceCheck.RunAsync(
            service, quest, new EvidenceAt(_tree, EvidenceCodes.SessionEnd) { Base = before, Session = "s1" }, git.Read);

        Assert.Equal(EvidencePosted.Kept, outcome.Posted.Outcome);
        Assert.True(outcome.Released);
        Assert.True(outcome.Verdict!.Found);
        Assert.Contains($"\"commit\":\"{head}\"", posted);
        Assert.Contains("\"session\":\"s1\"", posted);
        Assert.Equal(
            $"evidence read at {head} (session-end): 2 of 2 found, kept\n"
            + "- requirement 1 `docs/report.md`: found, changed by this work\n"
            + "- requirement 1 `README.md`: found, unchanged by this work",
            outcome.Bundle);

        var line = Assert.Single(lines);
        Assert.Equal("evidence.checked", line.Event);
        Assert.Equal(
            [
                ("quest", (object?)"q1"), ("session", "s1"), ("how", "session-end"), ("items", 2), ("found", 2), ("missing", 0),
                ("uncommitted", 0), ("case", 0), ("noQueue", 0), ("outcome", "kept"),
            ],
            line.Data);
    }

    [Fact]
    public async Task What_was_not_found_is_kept_and_said_and_the_hold_stays_the_services()
    {
        var git = new StandInGit(_tree);
        git.Commit("done", ("Docs/Report.md", "report"));
        File.WriteAllText(Path.Combine(_tree, "draft.md"), "x");
        using var service = Client(_ => (HttpStatusCode.OK, """{"quest":{"id":"q1"},"message":"Read the evidence: requirement 1's `docs/report.md` is not there."}"""));
        var lines = new List<EvidenceLine>();
        service.EvidenceLined += lines.Add;

        var outcome = await EvidenceCheck.RunAsync(
            service, Quest(["docs/report.md", "draft.md", "gone.md"], gate: "web"), new EvidenceAt(_tree, EvidenceCodes.Sweep) { Session = "s1" }, git.Read);

        Assert.Equal(
            $"evidence read at {git.Head} (sweep): 0 of 4 found, kept\n"
            + "- requirement 1 `docs/report.md`: case, the commit spells it `Docs/Report.md`\n"
            + "- requirement 1 `draft.md`: uncommitted, in the tree and not in the commit\n"
            + "- requirement 1 `gone.md`: missing\n"
            + "- requirement 1 gate `web`: no-queue, read from the landing queue, which does not run it here",
            outcome.Bundle);
        Assert.Contains(("missing", (object?)1), lines.Single().Data);
        Assert.Contains(("case", (object?)1), lines.Single().Data);
        Assert.Contains(("noQueue", (object?)1), lines.Single().Data);
    }

    /// <summary>Unread is never found (D143): nothing is posted, the bundle says why, and the line says it was unread.</summary>
    [Fact]
    public async Task A_read_that_could_not_be_made_posts_nothing_and_says_why()
    {
        var git = new StandInGit(Path.GetDirectoryName(_tree)!);
        var asked = 0;
        using var service = Client(_ =>
        {
            asked++;
            return (HttpStatusCode.OK, "{}");
        });
        var lines = new List<EvidenceLine>();
        service.EvidenceLined += lines.Add;

        var outcome = await EvidenceCheck.RunAsync(
            service, Quest(["docs/report.md"]), new EvidenceAt(_tree, EvidenceCodes.SessionEnd) { Session = "s1" }, git.Read);

        Assert.Equal(0, asked);
        Assert.Null(outcome.Verdict);
        Assert.Equal(EvidencePosted.Unread, outcome.Posted.Outcome);
        Assert.StartsWith("evidence not read: the tree it is read in is gone", outcome.Bundle);
        Assert.Contains(("outcome", (object?)"unread"), lines.Single().Data);
        Assert.Contains(("items", (object?)1), lines.Single().Data);
    }

    /// <summary>A host that did not answer is said, never thrown: a session's end must still write its record.</summary>
    [Fact]
    public async Task A_host_that_did_not_answer_is_said_and_never_thrown()
    {
        var git = new StandInGit(_tree);
        git.Commit("done", ("docs/report.md", "report"));
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new Throwing()));

        var outcome = await EvidenceCheck.RunAsync(
            service, Quest(["docs/report.md"]), new EvidenceAt(_tree, EvidenceCodes.SessionEnd) { Session = "s1" }, git.Read);

        Assert.Equal(EvidencePosted.Unanswered, outcome.Posted.Outcome);
        Assert.False(outcome.Released);
        Assert.Contains("evidence read at", outcome.Bundle);
        Assert.Contains("not kept: the service did not answer", outcome.Bundle);
    }

    [Fact]
    public void The_dones_commit_is_read_back_from_a_records_bundle()
    {
        var commit = new string('c', 40);

        Assert.Equal(commit, EvidenceCheck.DonesCommit($"commits landed:\nabc1234 work\nevidence read at {commit} (session-end): 1 of 1 found, kept\n- x"));
        Assert.Equal(commit, EvidenceCheck.DonesCommit($"evidence read at {commit} (sweep): 0 of 1 found, kept"));
        // The terminal's own read is not a driven end's.
        Assert.Null(EvidenceCheck.DonesCommit($"evidence read at {commit} (terminal): 1 of 1 found, kept"));
        Assert.Null(EvidenceCheck.DonesCommit("commits landed:\nabc1234 work"));
        Assert.Null(EvidenceCheck.DonesCommit(null));
    }

    private static QuestView Quest(IReadOnlyList<string> paths, string? gate = null) =>
        new("q1", "ask #a1", "reports", "Build the report", "b", "Done")
        {
            Requirements =
            [
                new("write it up", "a page")
                {
                    Evidence = [.. paths.Select(path => new QuestEvidenceItem(path)), .. gate is null ? [] : new[] { new QuestEvidenceItem(null, gate) }],
                },
            ],
            Answers = [new(1, "written", null, null)],
            AwaitsEvidence = true,
            Hold = EvidenceCodes.Unread,
        };

    private static ServiceClient Client(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) =>
        new("http://stand-in", null, new HttpClient(new StandIn(answer)));

    /// <summary>A service standing in: answered, or 404 with no JSON where the answer is null.</summary>
    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (status, body) = answer(request) ?? (HttpStatusCode.NotFound, "");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class Throwing : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("No connection could be made because the target machine actively refused it.");
    }
}
