using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// EVID1b (D144 §3): a done that waits on its evidence is read when the session that made it ends, at its tree's HEAD then,
/// before its record is written, and the record's evidence bundle (D46 §4) gains the verdict beneath its commits. The orphan
/// sweep (D104) reads a lost session's done the same way, with the same code, as it ends its record. A quest that waits on no
/// evidence ends exactly as before: git is asked nothing more and nothing is posted.
/// </summary>
/// <remarks>A service and git standing in, in process: the fast half (MOD8).</remarks>
public sealed class EvidenceAtEndTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-evidence-end-" + Guid.NewGuid().ToString("N")[..8]);

    private string Tree => Path.Combine(_root, "tree");

    public EvidenceAtEndTests() => Directory.CreateDirectory(Tree);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static QuestView Done(bool awaits = true) =>
        new("q1", "ask #a1", "reports", "Build the report", "b", "Done")
        {
            Requirements = [new("write it up", "a page") { Evidence = [new QuestEvidenceItem("docs/report.md")] }],
            Answers = [new(1, "written", null, null)],
            AwaitsEvidence = awaits,
            Hold = awaits ? EvidenceCodes.Unread : null,
            Held = awaits,
        };

    [Fact]
    public async Task A_done_waiting_on_its_evidence_is_read_at_its_end_and_the_verdict_goes_beneath_its_commits()
    {
        var git = new StandInGit(Tree);
        var before = git.Commit("base", ("README.md", "r"));
        var head = git.Commit("report", ("README.md", "r"), ("docs/report.md", "report"));
        var service = new Recorded();
        using var client = service.Client();

        var evidence = await EvidenceCheck.AtEndAsync(
            client, Done(), new EvidenceAt(Tree, EvidenceCodes.SessionEnd) { Base = before, Session = "s1" },
            $"commits landed:\n{head[..7]} report", git.Read, CancellationToken.None);

        Assert.Equal(
            $"commits landed:\n{head[..7]} report\n"
            + $"evidence read at {head} (session-end): 1 of 1 found, kept\n"
            + "- requirement 1 `docs/report.md`: found, changed by this work",
            evidence);
        var posted = Assert.Single(service.Evidence);
        Assert.Equal(("q1", head, "session-end", "s1"), (posted.Quest, posted.Body["commit"]!.GetValue<string>(),
            posted.Body["how"]!.GetValue<string>(), posted.Body["session"]!.GetValue<string>()));
    }

    [Fact]
    public async Task A_quest_that_waits_on_no_evidence_ends_as_it_did()
    {
        var git = new StandInGit(Tree);
        git.Commit("report", ("docs/report.md", "report"));
        var service = new Recorded();
        using var client = service.Client();

        var evidence = await EvidenceCheck.AtEndAsync(
            client, Done(awaits: false), new EvidenceAt(Tree, EvidenceCodes.SessionEnd) { Session = "s1" }, "no commits landed", git.Read,
            CancellationToken.None);
        var gone = await EvidenceCheck.AtEndAsync(
            client, null, new EvidenceAt(Tree, EvidenceCodes.SessionEnd) { Session = "s1" }, "no commits landed", git.Read, CancellationToken.None);

        Assert.Equal(("no commits landed", "no commits landed"), (evidence, gone));
        Assert.Empty(git.Calls);
        Assert.Empty(service.Evidence);
    }

    /// <summary>
    /// 🔴 The sweep ends a lost session's record with what it read of its done's evidence: at its tree's HEAD, changed from the
    /// base its record names, posted with <c>sweep</c> and the session, and kept beneath the commits on the record it ends.
    /// </summary>
    [Fact]
    public async Task The_sweep_reads_a_lost_dones_evidence_and_keeps_it_on_the_record_it_ends()
    {
        var git = new StandInGit(Tree);
        var before = git.Commit("base", ("README.md", "r"));
        var head = git.Commit("report", ("README.md", "r"), ("docs/report.md", "report"));
        var service = new Recorded { Quests = [Done()] };
        service.Sessions.Add(new JsonObject
        {
            ["id"] = "s1", ["repository"] = "reports", ["state"] = "working", ["quest"] = "q1", ["tree"] = Tree, ["baseCommit"] = before,
        });
        using var client = service.Client();

        var ended = await Orphans.EndAsync(client, new SessionProcesses(Path.Combine(_root, "markers")), git: git.Read);

        Assert.Equal(["s1"], ended.Select(each => each.Id));
        var posted = Assert.Single(service.Evidence);
        Assert.Equal((head, "sweep", "s1"), (posted.Body["commit"]!.GetValue<string>(), posted.Body["how"]!.GetValue<string>(),
            posted.Body["session"]!.GetValue<string>()));
        var moved = Assert.Single(service.Moves);
        Assert.Equal(("s1", "stopped"), (moved.Session, moved.Body["state"]!.GetValue<string>()));
        Assert.Equal(
            $"commits landed:\n{head[..7]} report\nevidence read at {head} (sweep): 1 of 1 found, kept\n"
            + "- requirement 1 `docs/report.md`: found, changed by this work",
            moved.Body["evidence"]!.GetValue<string>());
    }

    /// <summary>A lost session whose quest waits on no evidence, or that the service no longer answers, is ended as before.</summary>
    [Fact]
    public async Task The_sweep_of_a_record_whose_quest_waits_on_nothing_writes_no_evidence()
    {
        var git = new StandInGit(Tree);
        git.Commit("report", ("docs/report.md", "report"));
        var service = new Recorded { Quests = [Done(awaits: false)] };
        service.Sessions.Add(new JsonObject { ["id"] = "s1", ["repository"] = "reports", ["state"] = "working", ["quest"] = "q1", ["tree"] = Tree });
        service.Sessions.Add(new JsonObject { ["id"] = "s2", ["repository"] = "reports", ["state"] = "working", ["quest"] = "q9", ["tree"] = Tree });
        service.Sessions.Add(new JsonObject { ["id"] = "s3", ["repository"] = "reports", ["state"] = "working" });
        using var client = service.Client();

        var ended = await Orphans.EndAsync(client, new SessionProcesses(Path.Combine(_root, "markers")), git: git.Read);

        Assert.Equal(["s1", "s2", "s3"], ended.Select(each => each.Id));
        Assert.Empty(service.Evidence);
        Assert.All(service.Moves, move => Assert.Null(move.Body["evidence"]));
        Assert.Empty(git.Calls);
    }

    /// <summary>The service's doors this needs, recorded: the sessions and the quests listed, the evidence door, a record's move.</summary>
    private sealed class Recorded
    {
        public IReadOnlyList<QuestView> Quests { get; init; } = [];

        public List<JsonObject> Sessions { get; } = [];

        public List<(string Quest, JsonObject Body)> Evidence { get; } = [];

        public List<(string Session, JsonObject Body)> Moves { get; } = [];

        public ServiceClient Client() => new("http://stand-in", null, new HttpClient(new Handler(this)));

        private (HttpStatusCode, string) Answer(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            JsonObject Body() => JsonNode.Parse(request.Content!.ReadAsStringAsync().Result)!.AsObject();
            if (request.Method == HttpMethod.Get && path == "/api/sessions") return (HttpStatusCode.OK, new JsonArray([.. Sessions.Select(s => s.DeepClone())]).ToJsonString());
            if (request.Method == HttpMethod.Get && path == "/api/quests") return (HttpStatusCode.OK, new JsonArray([.. Quests.Select(Json)]).ToJsonString());
            if (request.Method == HttpMethod.Post && path.EndsWith("/evidence", StringComparison.Ordinal))
            {
                Evidence.Add((path.Split('/')[3], Body()));
                return (HttpStatusCode.OK, """{"quest":{"id":"q1"},"message":"Read the evidence."}""");
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/state", StringComparison.Ordinal))
            {
                var id = path.Split('/')[3];
                var body = Body();
                Moves.Add((id, body));
                return (HttpStatusCode.OK, new JsonObject { ["session"] = new JsonObject { ["id"] = id, ["state"] = body["state"]!.DeepClone() }, ["message"] = "moved" }.ToJsonString());
            }

            return (HttpStatusCode.NotFound, "");
        }

        private static JsonNode Json(QuestView quest) => new JsonObject
        {
            ["id"] = quest.Id, ["from"] = quest.From, ["to"] = quest.To, ["title"] = quest.Title, ["body"] = quest.Body, ["status"] = quest.Status,
            ["held"] = quest.Held, ["hold"] = quest.Hold, ["awaitsEvidence"] = quest.AwaitsEvidence,
            ["requirements"] = new JsonArray([.. quest.Requirements.Select(r => (JsonNode)new JsonObject
            {
                ["quote"] = r.Quote, ["check"] = r.Check,
                ["evidence"] = new JsonArray([.. r.Evidence.Select(e => (JsonNode)(e.Path is { } p ? new JsonObject { ["path"] = p } : new JsonObject { ["gate"] = e.Gate }))]),
            })]),
            ["answers"] = new JsonArray([.. quest.Answers.Select(a => (JsonNode)new JsonObject { ["requirement"] = a.Requirement, ["met"] = a.Met })]),
        };

        private sealed class Handler(Recorded service) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var (status, body) = service.Answer(request);
                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
            }
        }
    }
}
