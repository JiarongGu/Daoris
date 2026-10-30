using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1p parts 3 and 4 (the integration design §17.5, R5's C32 and C33): a run's <c>request</c> node publishes its quest once under the
/// request's key, reads its wait from this machine's projection of that quest, holds a decline for the person, and is asked again or gone on
/// without by the person's two acts on the run's node door. Each seen failing first.
/// </summary>
public sealed class RequestNodeRunTests : NodeRunFixture
{
    private const string AskGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"ask","kind":"request","version":1,"reach":"waited",
                   "options":{"to":"game","title":"Expose the frame cap","body":"The game paces on it.","start":"automatic"}},
                  {"id":"handed","kind":"handed","version":1,"reach":"observed"}],
         "wires":[{"id":"c>a","kind":"after","from":"claimed","to":"ask"},{"id":"a>h","kind":"after","from":"ask","to":"handed"}]}
        """;

    private readonly Door _door = new();

    /// <summary>The exchange as the request's door meets it: a quest kept by its key, the same key answered with the quest it made.</summary>
    private sealed class Door : IRequestPort
    {
        public Dictionary<string, QuestView> Made { get; } = [];

        public List<RequestAsk> Sent { get; } = [];

        /// <summary>Set, the next publish is kept and its answer lost, as a host that died after its write.</summary>
        public bool LoseAnswer { get; set; }

        public Task<RequestPublished> PublishAsync(RequestAsk ask, CancellationToken ct)
        {
            Sent.Add(ask);
            if (!Made.TryGetValue(ask.Key, out var quest))
            {
                quest = new QuestView($"r{Made.Count + 1}", ask.From, ask.To, ask.Title, ask.Body, "Open") { Request = ask.Key };
                Made[ask.Key] = quest;
            }

            if (LoseAnswer)
            {
                LoseAnswer = false;
                throw new HttpRequestException("the answer was lost");
            }

            return Task.FromResult(new RequestPublished(true, null, "published", quest));
        }
    }

    private NodeRuntime Started()
    {
        var runtime = Runtime();
        runtime.Requests = _door;
        return runtime;
    }

    /// <summary>The run's work done, and the quests the exchange holds as this machine's projection reads them.</summary>
    private void Read()
    {
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId]), .. _door.Made.Values.Select(RunHome.QuestUnit)];
    }

    private void Hold(string key, string status) => _door.Made[key] = _door.Made[key] with { Status = status };

    private NodeStatus Ask => RunStatus("ask");

    /// <summary>
    /// The start publishes its quest once, under the key <c>&lt;run&gt;/&lt;node&gt;/&lt;n&gt;</c> written on the run's record first, and a restart
    /// finds the quest by that key in its projection and sends nothing again.
    /// </summary>
    [Fact]
    public async Task The_start_publishes_its_quest_once_under_its_key_and_a_restart_finds_it_first()
    {
        BoundTo(AskGraph);
        Read();
        var first = Started();

        await RunPassAsync(first);
        var sent = Assert.Single(_door.Sent);
        Assert.Equal(($"{RunId}/ask/1", "game", "Expose the frame cap"), (sent.Key, sent.To, sent.Title));
        var attempt = Assert.Single(Files.Read(RunUnit)!.Attempts, each => each.Node == "ask");
        Assert.Equal(($"{RunId}/ask/1", NodeStates.Active), (attempt.Call.IdempotencyKey, Ask.State));

        Read();
        var restarted = Started();
        await RunPassAsync(restarted);
        await RunPassAsync(restarted);
        Assert.Single(_door.Sent);
        Assert.Equal(NodeStates.Active, Ask.State);
    }

    /// <summary>C32: a publish whose answer was lost is sent again under the same key and makes no second quest.</summary>
    [Fact]
    public async Task A_lost_publish_answer_is_sent_again_under_its_key_and_makes_no_duplicate()
    {
        BoundTo(AskGraph);
        Read();
        _door.LoseAnswer = true;
        var runtime = Started();

        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Unknown, Ask.State);

        await RunPassAsync(runtime);
        Assert.Equal(2, _door.Sent.Count);
        Assert.Single(_door.Made);
        Assert.Equal(NodeStates.Active, Ask.State);
    }

    /// <summary>It is reached on the quest's done, read from this machine's projection and nothing else.</summary>
    [Fact]
    public async Task The_request_is_reached_on_the_quests_done_through_the_projection()
    {
        BoundTo(AskGraph);
        Read();
        var runtime = Started();
        await RunPassAsync(runtime);

        Hold($"{RunId}/ask/1", "Taken");
        Read();
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Active, Ask.State);

        Hold($"{RunId}/ask/1", "Done");
        Read();
        await RunPassAsync(runtime);
        Assert.Equal((NodeStates.Reached, NodeSource.Commitment), (Ask.State, Ask.By));
        Assert.Equal("quest:r1", Ask.Acceptance!.Evidence!.Ref);
        Assert.Single(_door.Sent);
    }

    /// <summary>Under <c>until: taken</c> another machine's taking reaches it, the quest read as the projection holds it.</summary>
    [Fact]
    public async Task Under_until_taken_a_quest_another_machine_took_reaches_it()
    {
        BoundTo(AskGraph.Replace("\"start\":\"automatic\"", "\"start\":\"automatic\",\"until\":\"taken\""));
        Read();
        var runtime = Started();
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Active, Ask.State);

        Hold($"{RunId}/ask/1", "Taken");
        Read();
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Reached, Ask.State);
    }

    /// <summary>A decline fails the node by its authority with the code the page words, and says so once.</summary>
    [Fact]
    public async Task A_decline_fails_the_request_and_holds_it_for_the_person()
    {
        BoundTo(AskGraph);
        Read();
        var runtime = Started();
        await RunPassAsync(runtime);

        Hold($"{RunId}/ask/1", "Declined");
        Read();
        var pass = await RunPassAsync(runtime);
        Assert.Equal((NodeStates.Failed, RequestOptions.DeclinedCode), (Ask.State, Ask.Code));
        Assert.Single(pass.Said, line => line.Contains("was declined", StringComparison.Ordinal));
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Failed, Ask.State);
        Assert.Single(_door.Sent);
    }

    /// <summary>Said as a standing say-so: a request left to the person's send publishes nothing and says it waits.</summary>
    [Fact]
    public async Task A_request_not_set_to_start_automatically_waits_for_the_persons_send_and_says_so()
    {
        BoundTo(AskGraph.Replace(",\"start\":\"automatic\"", ""));
        Read();
        var pass = await RunPassAsync(Started());

        Assert.Empty(_door.Sent);
        Assert.Equal(NodeStates.Ready, Ask.State);
        Assert.Contains(pass.Said, line => line.Contains("waits for your send", StringComparison.Ordinal));
    }

    /// <summary>A request to the run's own repository is never sent.</summary>
    [Fact]
    public async Task A_request_to_the_runs_own_repository_is_never_sent()
    {
        BoundTo(AskGraph.Replace("\"to\":\"game\"", "\"to\":\"engine\""));
        Read();
        var pass = await RunPassAsync(Started());

        Assert.Empty(_door.Sent);
        Assert.Contains(pass.Said, line => line.Contains("own repository", StringComparison.Ordinal));
    }

    /// <summary>
    /// C33: after a decline the person's <c>ask-again</c> goes through the door and the pass as the table's failed to ready, and the node
    /// publishes anew under the next <c>n</c>.
    /// </summary>
    [Fact]
    public async Task Ask_again_publishes_anew_under_the_next_n()
    {
        BoundTo(AskGraph);
        Read();
        var runtime = Started();
        await RunPassAsync(runtime);
        Hold($"{RunId}/ask/1", "Declined");
        Read();
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Failed, Ask.State);
        Assert.Equal([NodeWordActs.AskAgain, NodeWordActs.GoOn], RunNodeWords.Acts(AskNode(), Ask));

        var worded = RunNodeWords.Word(Files, RunId, "ask", NodeWordActs.AskAgain, null, () => _now);
        Assert.True(worded.Done, worded.Refused);
        Read();
        await RunPassAsync(runtime, whole: false);

        Assert.Equal([$"{RunId}/ask/1", $"{RunId}/ask/2"], _door.Sent.Select(each => each.Key));
        Assert.Equal((NodeStates.Active, 2), (Ask.State, Ask.Attempt));
        Assert.Equal(2, _door.Made.Count);
    }

    /// <summary>C33: <c>go-on</c> with words passes the node as a skip does, so a wire after it fires; without words it is refused.</summary>
    [Fact]
    public async Task Go_on_without_it_passes_the_after_wire_and_keeps_the_persons_words()
    {
        BoundTo(AskGraph);
        Read();
        var runtime = Started();
        await RunPassAsync(runtime);
        Hold($"{RunId}/ask/1", "Declined");
        Read();
        await RunPassAsync(runtime);

        var handedBefore = RunStatus("handed").State;
        Assert.Equal(NodeRefusal.NoWords, RunNodeWords.Word(Files, RunId, "ask", NodeWordActs.GoOn, null, () => _now).Refused);
        var worded = RunNodeWords.Word(Files, RunId, "ask", NodeWordActs.GoOn, "the engine team has no time; I will do it myself", () => _now);
        Assert.True(worded.Done, worded.Refused);
        await RunPassAsync(runtime, whole: false);

        Assert.Equal((NodeStates.Skipped, NodeSource.Person, "the engine team has no time; I will do it myself"), (Ask.State, Ask.By, Ask.Words));
        Assert.Equal(NodeStates.Blocked, handedBefore);
        Assert.Equal((NodeStates.Active, "unit-b1"), (RunStatus("handed").State, RunStatus("handed").Binding!.Subject));
        Assert.Single(_door.Sent);
    }

    private NodeDeclaration AskNode() => new("ask", NodeKinds.Request, 1, NodeReach.Waited);
}
