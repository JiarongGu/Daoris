using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// EVID1b (D144 §3, §5): <c>daoris-driver quest check &lt;id&gt; [--commit &lt;sha&gt;]</c>, the terminal's door to a done's
/// evidence. It reads the done's commit a driven end on this machine read, or the commit named, which must be the done's or
/// come after it on the same history; with no driven end on record, the commit must be named, since Daoris never guesses it
/// from what stands now (D143 §3). It posts what it read as the terminal's, prints the verdict, and exits 0 when all is
/// found, 1 when anything is missing or unread, and 2 when a store did not answer.
/// </summary>
/// <remarks>A service and git standing in, in process: the fast half (MOD8).</remarks>
public sealed class QuestCheckCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-quest-check-" + Guid.NewGuid().ToString("N")[..8]);

    private string Checkout => Path.Combine(_root, "reports");

    private string SessionTree => Path.Combine(_root, "trees", "s1");

    public QuestCheckCommandTests()
    {
        Directory.CreateDirectory(Checkout);
        Directory.CreateDirectory(SessionTree);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Theory]
    [InlineData(new[] { "check", "q1" }, "q1", null)]
    [InlineData(new[] { "check", "#q1", "--commit", "abc1234" }, "q1", "abc1234")]
    [InlineData(new[] { "check", "--commit", "ABC1234DEF", "q1" }, "q1", "abc1234def")]
    public void The_line_is_read_as_a_quest_and_perhaps_a_commit(string[] args, string quest, string? commit)
    {
        Assert.Equal(new QuestCheckAsk(quest, commit), QuestCheckCommand.Read(args, out var problem));
        Assert.Null(problem);
    }

    [Theory]
    [InlineData(new[] { "check" }, "names the quest")]
    [InlineData(new[] { "check", "q1", "q2" }, "one quest")]
    [InlineData(new[] { "check", "q1", "--commit" }, "--commit")]
    [InlineData(new[] { "check", "q1", "--commit", "HEAD" }, "commit id")]
    [InlineData(new[] { "check", "q1", "--commit", "-x" }, "commit id")]
    [InlineData(new[] { "check", "q1", "--all" }, "--all")]
    public void A_line_that_does_not_read_says_why(string[] args, string said)
    {
        Assert.Null(QuestCheckCommand.Read(args, out var problem));
        Assert.Contains(said, problem);
        Assert.StartsWith("usage: daoris-driver quest check <id> [--commit <sha>]", QuestCheckCommand.Usage);
    }

    [Fact]
    public void Only_check_is_asked_for_and_the_usage_names_it()
    {
        Assert.True(QuestCheckCommand.Asks(["check", "q1"]));
        Assert.False(QuestCheckCommand.Asks(["accept", "q1"]));
        Assert.Contains("quest check <id> [--commit <sha>]", DriverCommand.Usage);
    }

    /// <summary>🔴 With a driven end on record and no commit named, the done's commit that end read is read again.</summary>
    [Fact]
    public async Task With_no_commit_named_the_dones_commit_a_driven_end_read_is_read_again()
    {
        var git = new StandInGit(SessionTree);
        var before = git.Commit("base", ("README.md", "r"));
        var done = git.Commit("done", ("README.md", "r2"));
        var service = new Recorded { Quest = Waiting() };
        service.Sessions.Add(Record("s1", SessionTree, before, $"commits landed:\n{done[..7]} done\nevidence read at {done} (session-end): 0 of 1 found, not kept: the service did not answer (refused)\n- requirement 1 `docs/report.md`: missing"));
        var said = new StringWriter();

        var exit = await Run(service, git, said, "check", "q1");

        Assert.Equal(1, exit);
        var posted = Assert.Single(service.Evidence);
        Assert.Equal((done, "terminal"), (posted["commit"]!.GetValue<string>(), posted["how"]!.GetValue<string>()));
        Assert.Null(posted["session"]);
        Assert.Contains($"evidence read at {done} (terminal): 0 of 1 found, kept", said.ToString());
        Assert.Contains("- requirement 1 `docs/report.md`: missing", said.ToString());
        Assert.Contains("Read the evidence of quest `#q1`.", said.ToString());
    }

    /// <summary>Work that arrived later on the same history lifts the hold: the commit named comes after the done's.</summary>
    [Fact]
    public async Task A_commit_after_the_dones_on_its_history_is_read_and_all_found_exits_0()
    {
        var git = new StandInGit(SessionTree);
        var before = git.Commit("base", ("README.md", "r"));
        var done = git.Commit("done", ("README.md", "r2"));
        var later = git.Commit("later", ("README.md", "r2"), ("docs/report.md", "report"));
        var service = new Recorded { Quest = Waiting() };
        service.Sessions.Add(Record("s1", SessionTree, before, $"evidence read at {done} (session-end): 0 of 1 found, kept"));
        var said = new StringWriter();

        var exit = await Run(service, git, said, "check", "q1", "--commit", later[..10]);

        Assert.Equal(0, exit);
        Assert.Equal(later, Assert.Single(service.Evidence)["commit"]!.GetValue<string>());
        Assert.Contains("- requirement 1 `docs/report.md`: found, changed by this work", said.ToString());
    }

    [Fact]
    public async Task A_commit_before_the_dones_is_refused_and_nothing_is_posted()
    {
        var git = new StandInGit(SessionTree);
        var before = git.Commit("base", ("docs/report.md", "old"));
        var done = git.Commit("done", ("README.md", "r2"));
        var service = new Recorded { Quest = Waiting() };
        service.Sessions.Add(Record("s1", SessionTree, before, $"evidence read at {done} (session-end): 0 of 1 found, kept"));
        var said = new StringWriter();

        var exit = await Run(service, git, said, "check", "q1", "--commit", before);

        Assert.Equal(1, exit);
        Assert.Empty(service.Evidence);
        Assert.Contains($"does not come after the done's commit `{done[..8]}`", said.ToString());
    }

    /// <summary>🔴 With no driven end on record, the commit must be named: Daoris never guesses it from what stands now (D143 §3).</summary>
    [Fact]
    public async Task With_no_driven_end_on_record_the_commit_must_be_named()
    {
        var git = new StandInGit(Checkout);
        var head = git.Commit("done", ("docs/report.md", "report"));
        var service = new Recorded { Quest = Waiting() };
        var unnamed = new StringWriter();
        var named = new StringWriter();

        var refused = await Run(service, git, unnamed, "check", "q1");
        Assert.Equal(1, refused);
        Assert.Empty(service.Evidence);
        Assert.Contains("name the commit", unnamed.ToString());
        Assert.Contains("`daoris-driver quest check q1 --commit <sha>`", unnamed.ToString());

        var read = await Run(service, git, named, "check", "q1", "--commit", head);
        Assert.Equal(0, read);
        Assert.Equal(head, Assert.Single(service.Evidence)["commit"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_done_whose_evidence_was_found_says_so_and_exits_0()
    {
        var git = new StandInGit(Checkout);
        var commit = new string('a', 40);
        var service = new Recorded
        {
            Quest = Waiting() with
            {
                AwaitsEvidence = false, Hold = null, Held = false,
                Evidence = new EvidenceVerdict(commit, EvidenceCodes.SessionEnd,
                    [new EvidenceRead(1, "docs/report.md", null, EvidenceCodes.Found) { Object = new string('b', 40) }]),
            },
        };
        var said = new StringWriter();

        Assert.Equal(0, await Run(service, git, said, "check", "q1"));
        Assert.Empty(service.Evidence);
        Assert.Contains("was found at `aaaaaaaa` (session-end)", said.ToString());
    }

    [Fact]
    public async Task A_quest_that_waits_on_no_evidence_is_said_and_exits_1()
    {
        var git = new StandInGit(Checkout);
        var service = new Recorded { Quest = Waiting() with { Status = "Taken", AwaitsEvidence = false, Hold = null, Held = false, Answers = [] } };
        var missing = new Recorded();
        var said = new StringWriter();

        Assert.Equal(1, await Run(service, git, said, "check", "q1"));
        Assert.Contains("is Taken", said.ToString());
        Assert.Equal(1, await Run(missing, git, said, "check", "q1"));
        Assert.Contains("no quest `#q1`", said.ToString());
        Assert.Empty(service.Evidence);
    }

    [Fact]
    public async Task A_repository_with_no_checkout_here_is_said_and_exits_1()
    {
        var git = new StandInGit(Checkout);
        var service = new Recorded { Quest = Waiting() with { To = "elsewhere" } };
        var said = new StringWriter();

        Assert.Equal(1, await Run(service, git, said, "check", "q1", "--commit", new string('a', 40)));
        Assert.Contains("no checkout of `elsewhere`", said.ToString());
    }

    [Fact]
    public async Task A_service_that_did_not_answer_exits_2()
    {
        var git = new StandInGit(Checkout);
        using var client = new ServiceClient("http://stand-in", null, new HttpClient(new Throwing()));
        var said = new StringWriter();

        var exit = await QuestCheckCommand.RunAsync(new QuestCheckAsk("q1", null), new QuestCheckWorld(client) { Git = git.Read }, said);

        Assert.Equal(2, exit);
        Assert.Contains("did not answer", said.ToString());
    }

    private async Task<int> Run(Recorded service, StandInGit git, StringWriter said, params string[] args)
    {
        using var client = service.Client(Checkout);
        var ask = QuestCheckCommand.Read(args, out var problem) ?? throw new InvalidOperationException(problem);
        return await QuestCheckCommand.RunAsync(ask, new QuestCheckWorld(client) { Git = git.Read }, said);
    }

    private static QuestView Waiting() =>
        new("q1", "ask #a1", "reports", "Build the report", "b", "Done")
        {
            Requirements = [new("write it up", "a page") { Evidence = [new QuestEvidenceItem("docs/report.md")] }],
            Answers = [new(1, "written", null, null)],
            AwaitsEvidence = true,
            Hold = EvidenceCodes.MissingHold,
            Held = true,
        };

    private static JsonObject Record(string id, string tree, string baseCommit, string evidence) => new()
    {
        ["id"] = id, ["repository"] = "reports", ["state"] = "completed", ["kind"] = "driven", ["quest"] = "q1", ["tree"] = tree,
        ["baseCommit"] = baseCommit, ["evidence"] = evidence, ["created"] = "2026-10-07T01:00:00Z", ["updated"] = "2026-10-07T02:00:00Z",
    };

    /// <summary>The doors the check reads and posts to: one quest, the records, the registry, and the evidence door, recorded.</summary>
    private sealed class Recorded
    {
        public QuestView? Quest { get; init; }

        public List<JsonObject> Sessions { get; } = [];

        public List<JsonObject> Evidence { get; } = [];

        public ServiceClient Client(string checkout) => new("http://stand-in", null, new HttpClient(new Handler(this, checkout)));

        private (HttpStatusCode, string) Answer(HttpRequestMessage request, string checkout)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/api/sessions") return (HttpStatusCode.OK, new JsonArray([.. Sessions.Select(s => s.DeepClone())]).ToJsonString());
            if (request.Method == HttpMethod.Get && path == "/api/registry")
            {
                return (HttpStatusCode.OK, new JsonArray(new JsonObject
                {
                    ["repository"] = "reports", ["adopted"] = true, ["registered"] = true, ["root"] = checkout, ["workspace"] = "default",
                }).ToJsonString());
            }

            if (request.Method == HttpMethod.Get && path == "/api/quests")
            {
                return (HttpStatusCode.OK, Quest is not { } quest ? "[]" : new JsonArray(Json(quest)).ToJsonString());
            }

            if (request.Method == HttpMethod.Post && path == "/api/quests/q1/evidence")
            {
                Evidence.Add(JsonNode.Parse(request.Content!.ReadAsStringAsync().Result)!.AsObject());
                return (HttpStatusCode.OK, """{"quest":{"id":"q1"},"message":"Read the evidence of quest `#q1`."}""");
            }

            return (HttpStatusCode.NotFound, "");
        }

        private static JsonNode Json(QuestView quest)
        {
            var json = new JsonObject
            {
                ["id"] = quest.Id, ["from"] = quest.From, ["to"] = quest.To, ["title"] = quest.Title, ["body"] = quest.Body, ["status"] = quest.Status,
                ["held"] = quest.Held, ["hold"] = quest.Hold, ["awaitsEvidence"] = quest.AwaitsEvidence,
                ["requirements"] = new JsonArray([.. quest.Requirements.Select(r => (JsonNode)new JsonObject
                {
                    ["quote"] = r.Quote, ["check"] = r.Check,
                    ["evidence"] = new JsonArray([.. r.Evidence.Select(e => (JsonNode)new JsonObject { ["path"] = e.Path })]),
                })]),
                ["answers"] = new JsonArray([.. quest.Answers.Select(a => (JsonNode)new JsonObject { ["requirement"] = a.Requirement, ["met"] = a.Met })]),
            };
            if (quest.Evidence is { } read) json["evidence"] = JsonNode.Parse(read.Json());
            return json;
        }

        private sealed class Handler(Recorded service, string checkout) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var (status, body) = service.Answer(request, checkout);
                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
            }
        }
    }

    private sealed class Throwing : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("No connection could be made because the target machine actively refused it.");
    }
}
