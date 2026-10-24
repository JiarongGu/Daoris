using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CHATFLOW1c (the chat-flow design §4.1–§4.4, amended by Codex's read, finding 7, and by the Opus check of that amendment, findings 1 to 3):
/// a chat in a tree of its own runs its repository's workflow, keyed by its line, <c>chat-&lt;first&gt;</c>. Its later steps follow its
/// candidate, never its displayed work step; its landing is read for the tip its tree stands at, live or ended; its opinion waits on the
/// person's Accept, which asks it (<c>chat-at-accept</c>, CHATFLOW1d), and then reads its gate as a quest's does; its checks are said not yet
/// enforced; and a line that takes a quest is that quest's from the take on.
/// </summary>
public sealed class WorkflowRunChatTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyList<WorkflowPlugin> Plugins = [new("example.pull-request", [HookPoints.Land, HookPoints.State])];
    private const string OpinionRule = """{"opinions":{"engine":{"reviewers":["codex-acp"]}}}""";

    private const string ChatTree = "trees/default/engine/s-c1c1c1c1";
    private const string TipA = "aaaa1111aaaa1111aaaa1111aaaa1111aaaa1111";
    private const string TipB = "bbbb2222bbbb2222bbbb2222bbbb2222bbbb2222";

    /// <summary>A record of the chat line <c>c1</c> began in its own tree; <paramref name="later"/> orders it after the ones before.</summary>
    private static TracedSession ChatOn(string id, string state = "working", int later = 0) => new(id, "engine", state)
    {
        Kind = "chat", Tree = ChatTree, Entry = "session:c1/engine", From = later == 0 ? null : "c1", Adapter = "claude-code",
        Created = T.AddMinutes(later * 10), Updated = T.AddMinutes(later * 10 + 5),
    };

    /// <summary>
    /// A chat run derived from its line's records, its tree and its landings, under the rules <paramref name="config"/> sets; its opinion's
    /// gate <paramref name="opinion"/>, else one no press has asked yet (CHATFLOW1d).
    /// </summary>
    private static WorkflowRun ChatRunOf(
        string config, ChatRunFacts chat, IReadOnlyList<TracedSession> line, IReadOnlyList<LandedBranch>? landings = null,
        Dictionary<string, DateTimeOffset>? accepted = null, OpinionGateState? opinion = null)
    {
        var parsed = DriverConfig.Parse(config);
        var current = WorkflowCurrent.Derive(parsed, "engine", "aurora", Plugins);
        return WorkflowRuns.Derive(new WorkflowRunFacts("engine", current)
        {
            Sessions = line,
            Review = new ReviewGateState(ReviewStates.None, new ReviewDecision(ReviewLevels.Nothing, null) { Repository = "engine" }),
            Opinion = opinion ?? new OpinionGateState(OpinionGateStates.NotAsked, "engine")
            {
                Rule = OpinionRules.Resolve(parsed, "engine", "aurora"), Run = chat.Run, Session = line[^1].Id,
            },
            Landings = landings ?? [],
            Accepted = accepted ?? new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase),
            Chat = chat,
        }, "aurora");
    }

    private static WorkflowRunStep StepOf(WorkflowRun run, string kind) => run.Steps.Single(step => step.Step.Kind == kind);

    private static (string State, string Detail) Where(WorkflowRun run, string kind) => (StepOf(run, kind).State, StepOf(run, kind).Detail);

    /// <summary>
    /// Codex's finding 7: the view before the first Accept, in its candidate's wait, ended, and after the landing. While a turn runs the work
    /// is the agent's and nothing later is reached; between turns, or ended, with commits beyond the line the opinion and the landing stand
    /// in view before any landing; once a landing is kept for the tip the tree stands at, the work and the landing read done.
    /// </summary>
    [Fact]
    public void A_chat_run_s_later_steps_follow_its_candidate_never_its_work_step()
    {
        var turning = ChatRunOf(OpinionRule, new ChatRunFacts("chat-c1") { Tip = TipA, Beyond = 2 }, [ChatOn("c1")]);
        Assert.Equal("chat-c1", turning.ChatRun);
        Assert.Equal((WorkflowRunStates.Working, WorkflowRunDetails.Agent), Where(turning, WorkflowKinds.Work));
        Assert.Equal(WorkflowRunStates.NotReached, StepOf(turning, WorkflowKinds.Opinion).State);
        Assert.Equal(WorkflowRunStates.NotReached, StepOf(turning, WorkflowKinds.Landing).State);

        var talking = ChatRunOf(OpinionRule, new ChatRunFacts("chat-c1") { Tip = TipA, Beyond = 0, Between = true }, [ChatOn("c1")]);
        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Talk), Where(talking, WorkflowKinds.Work));
        Assert.Equal(WorkflowRunStates.NotReached, StepOf(talking, WorkflowKinds.Landing).State);

        // Ended with commits beyond the line: the landing still waits on the person's Accept, on the line's newest record.
        var ended = ChatRunOf(OpinionRule, new ChatRunFacts("chat-c1") { Tip = TipA, Beyond = 2 }, [ChatOn("c1", "completed")]);
        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Land), Where(ended, WorkflowKinds.Work));
        Assert.Equal(2, StepOf(ended, WorkflowKinds.Work).Count);
        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Accept, "c1"),
            (StepOf(ended, WorkflowKinds.Landing).State, StepOf(ended, WorkflowKinds.Landing).Detail, StepOf(ended, WorkflowKinds.Landing).Session));
        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.ChatAtAccept), Where(ended, WorkflowKinds.Opinion));

        // Ended with nothing beyond the line and nothing landed: the conversation stopped.
        var stopped = ChatRunOf(OpinionRule, new ChatRunFacts("chat-c1") { Tip = TipA, Beyond = 0 }, [ChatOn("c1", "completed")]);
        Assert.Equal((WorkflowRunStates.Stopped, WorkflowRunDetails.Stopped), Where(stopped, WorkflowKinds.Work));

        // After the landing: done for the tip it landed, the opinion still the one the press passed over, never `landed`.
        var landed = ChatRunOf(OpinionRule, new ChatRunFacts("chat-c1") { Tip = TipA, Beyond = 2, Between = true }, [ChatOn("c1")],
            [new LandedBranch("engine", "aurora", "chat/the-change", "main", TipA, "c1", null, "The change", T.AddMinutes(30))]);
        Assert.Equal((WorkflowRunStates.Done, WorkflowRunDetails.Landed), Where(landed, WorkflowKinds.Work));
        Assert.Equal((WorkflowRunStates.Done, WorkflowRunDetails.Branch), Where(landed, WorkflowKinds.Landing));
        Assert.Equal((WorkflowRunStates.Done, WorkflowRunDetails.Landed), Where(landed, WorkflowKinds.Opinion));
    }

    /// <summary>
    /// CHATFLOW1d (Codex's finding 7, the rows c left to d): the view during review and during the answer's delivery. Once the press asked the
    /// opinion it reads as asked, then being read, the landing not reached past it; while the findings are with the conversation and its
    /// answering turn runs, the work reads the agent's and the opinion stays in view, never not reached; a dispute waits on the person.
    /// </summary>
    [Fact]
    public void A_chat_run_s_opinion_stays_in_view_once_its_press_asked_it_while_the_answering_turn_runs()
    {
        var rule = OpinionRules.Resolve(DriverConfig.Parse(OpinionRule), "engine", "aurora");
        OpinionGateState Gate(string state, bool asked = false) =>
            new(state, "engine") { Rule = rule, Run = "chat-c1", Session = "c1", Asked = asked };
        var reader = new OpinionView("o1", "landing", "first", "c1", "r1", "engine", TipA, TipA, "codex-acp", ReviewerLabels.AnotherMaker, "reading");

        var asked = ChatRunOf(OpinionRule, new ChatRunFacts("chat-c1") { Tip = TipA, Beyond = 2, Between = true }, [ChatOn("c1")],
            opinion: Gate(OpinionGateStates.NotAsked, asked: true));
        Assert.Equal((WorkflowRunStates.Working, OpinionGateStates.NotAsked), Where(asked, WorkflowKinds.Opinion));
        Assert.Equal(WorkflowRunStates.NotReached, StepOf(asked, WorkflowKinds.Landing).State);

        var reading = ChatRunOf(OpinionRule, new ChatRunFacts("chat-c1") { Tip = TipA, Beyond = 2, Between = true }, [ChatOn("c1")],
            opinion: Gate(OpinionGateStates.Reading) with { Opinion = reader });
        Assert.Equal((WorkflowRunStates.Working, OpinionGateStates.Reading), Where(reading, WorkflowKinds.Opinion));

        // The answering turn runs: the work is the agent's again, and the opinion it answers stays in view.
        var answering = ChatRunOf(OpinionRule, new ChatRunFacts("chat-c1") { Tip = TipA, Beyond = 2 }, [ChatOn("c1")],
            opinion: Gate(OpinionGateStates.WithSession) with { Opinion = reader with { State = "given", Findings = [] } });
        Assert.Equal((WorkflowRunStates.Working, WorkflowRunDetails.Agent), Where(answering, WorkflowKinds.Work));
        Assert.Equal((WorkflowRunStates.WaitingOnAgent, OpinionGateStates.WithSession), Where(answering, WorkflowKinds.Opinion));

        var disputed = ChatRunOf(OpinionRule, new ChatRunFacts("chat-c1") { Tip = TipA, Beyond = 2, Between = true }, [ChatOn("c1")],
            opinion: Gate(OpinionGateStates.Disputed) with { Opinion = reader with { State = "given", Findings = [] }, Disputes = new([1], []) });
        Assert.Equal((WorkflowRunStates.WaitingOnYou, OpinionGateStates.Disputed), Where(disputed, WorkflowKinds.Opinion));
    }

    /// <summary>
    /// The Opus check's finding 1, its first proof row: a live chat between turns with commits beyond the line reads <c>waiting-on-you</c>,
    /// <c>accept</c>, though its record stays <c>working</c>; never <c>done</c>, <c>nothing</c> as a quest's ended-records reading would say.
    /// </summary>
    [Fact]
    public void A_live_chat_between_turns_with_commits_beyond_the_line_waits_for_your_Accept()
    {
        var run = ChatRunOf(OpinionRule, new ChatRunFacts("chat-c1") { Tip = TipA, Beyond = 1, Between = true }, [ChatOn("c1")]);

        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Land), Where(run, WorkflowKinds.Work));
        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Accept), Where(run, WorkflowKinds.Landing));
        Assert.Equal("c1", StepOf(run, WorkflowKinds.Landing).Session);
        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.ChatAtAccept), Where(run, WorkflowKinds.Opinion));
        // It stands at its work, which waits on the person's Accept, the landing beside it.
        Assert.Equal(WorkflowKinds.Work, run.Steps.Single(step => step.Step.Id == run.At).Step.Kind);
    }

    /// <summary>
    /// The Opus check's finding 1, its second proof row: a merge landing under a rule that names no opinion leaves nothing beyond the line, and
    /// reads <c>done</c> after it lands, the work and the landing both, never <c>not-reached</c>; a commit after it is a candidate again.
    /// </summary>
    [Fact]
    public void A_merge_landing_under_a_rule_that_names_no_opinion_reads_done_after_it_lands()
    {
        var accepted = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase) { ["c1"] = T.AddMinutes(30) };

        var run = ChatRunOf("{}", new ChatRunFacts("chat-c1") { Tip = TipA, Beyond = 0, Between = true }, [ChatOn("c1")], accepted: accepted);

        Assert.DoesNotContain(run.Steps, step => step.Step.Kind == WorkflowKinds.Opinion);
        Assert.Equal((WorkflowRunStates.Done, WorkflowRunDetails.Landed), Where(run, WorkflowKinds.Work));
        Assert.Equal((WorkflowRunStates.Done, WorkflowRunDetails.Merge), Where(run, WorkflowKinds.Landing));
        Assert.DoesNotContain(run.Steps, step => step.State == WorkflowRunStates.NotReached);
        Assert.Null(run.At);

        var again = ChatRunOf("{}", new ChatRunFacts("chat-c1") { Tip = TipB, Beyond = 1, Between = true }, [ChatOn("c1")], accepted: accepted);
        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Accept), Where(again, WorkflowKinds.Landing));
    }

    /// <summary>
    /// The Opus check's finding 1, its third proof row: after a branch landing, a second candidate reads <c>accept</c> again with its own
    /// opinion, never the first landing's; the landing advanced to it by the hand-over's record reads done for that tip, with what that
    /// advance kept.
    /// </summary>
    [Fact]
    public void A_second_candidate_after_a_branch_landing_reads_accept_again_with_its_own_opinion()
    {
        var first = new LandedBranch("engine", "aurora", "chat/the-change", "main", TipA, "c1", null, "The change", T.AddMinutes(30))
        {
            Opinion = new LandingOpinion(OpinionGateStates.Settled) { Reviewer = "codex-acp", Tip = TipA, At = T.AddMinutes(25) },
        };
        IReadOnlyList<TracedSession> line = [ChatOn("c1", "completed"), ChatOn("c2", later: 4)];

        var second = ChatRunOf(OpinionRule, new ChatRunFacts("chat-c1") { Tip = TipB, Beyond = 3, Between = true }, line, [first]);
        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Land), Where(second, WorkflowKinds.Work));
        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Accept, "c2"),
            (StepOf(second, WorkflowKinds.Landing).State, StepOf(second, WorkflowKinds.Landing).Detail, StepOf(second, WorkflowKinds.Landing).Session));
        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.ChatAtAccept), Where(second, WorkflowKinds.Opinion));

        var atFirst = ChatRunOf(OpinionRule, new ChatRunFacts("chat-c1") { Tip = TipA, Beyond = 1, Between = true }, line, [first]);
        Assert.Equal((WorkflowRunStates.Done, OpinionGateStates.Settled, TipA),
            (StepOf(atFirst, WorkflowKinds.Opinion).State, StepOf(atFirst, WorkflowKinds.Opinion).Detail, StepOf(atFirst, WorkflowKinds.Opinion).Commit));

        var advanced = first with { Tip = TipB, Advances = [new LandedAdvance(TipA, TipB, T.AddMinutes(60), "c2")] };
        var landedAgain = ChatRunOf(OpinionRule, new ChatRunFacts("chat-c1") { Tip = TipB, Beyond = 3, Between = true }, line, [advanced]);
        Assert.Equal((WorkflowRunStates.Done, WorkflowRunDetails.Branch, "c2"),
            (StepOf(landedAgain, WorkflowKinds.Landing).State, StepOf(landedAgain, WorkflowKinds.Landing).Detail, StepOf(landedAgain, WorkflowKinds.Landing).Session));
        Assert.Equal((WorkflowRunStates.Done, WorkflowRunDetails.Landed), Where(landedAgain, WorkflowKinds.Opinion));
    }

    /// <summary>
    /// Row c's first proof: a chat's tree with commits shows its run and its checks, Current's <c>sessionChecks</c>, marked not yet enforced;
    /// its opinion passed over with <c>chat-not-asked</c>'s words; and a hand-over's second record reads the same run, keyed by the line's
    /// first record. Read over a stand-in service and records, so nothing is written under the home.
    /// </summary>
    [Fact]
    public async Task A_chat_s_tree_with_commits_shows_its_run_and_its_checks_and_its_hand_over_reads_the_same_run()
    {
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new ChatService(ChatRecords)));
        var sources = new WorkflowRunSources(service, Path.Combine(AppContext.BaseDirectory, "no-home-" + Guid.NewGuid().ToString("N")[..8]),
            DriverConfig.Parse("""{"opinions":{"engine":{"reviewers":["codex-acp"]}},"sessionChecks":{"engine":"light"}}"""), [], _ => "aurora");
        var records = new WorkflowRunTests.Trees(holds: tree => tree.StartsWith("trees/", StringComparison.Ordinal)) { BetweenTurns = { "c2" }, Tips = { [ChatTree] = new(TipA, 2) } };

        foreach (var asked in new[] { "c1", "c2" })
        {
            var read = await WorkflowRunReader.ReadAsync(new WorkflowRunAsk(Session: asked), sources, records);
            var run = Assert.Single(read.Runs);
            Assert.Null(read.Code);
            Assert.Equal(("chat-c1", "c2"), (run.ChatRun, run.Session));
            Assert.Empty(run.Quests);
            Assert.Equal("light", WorkflowRunWords.ChecksOf(run));
            Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Land), Where(run, WorkflowKinds.Work));
            Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Accept, "c2"),
                (StepOf(run, WorkflowKinds.Landing).State, StepOf(run, WorkflowKinds.Landing).Detail, StepOf(run, WorkflowKinds.Landing).Session));
            Assert.Equal("A second opinion from `codex-acp` is asked when you press Accept.", WorkflowRunWords.Said(StepOf(run, WorkflowKinds.Opinion)));

            var said = WorkflowRunWords.Say(read.Runs, asked);
            Assert.Contains("engine · Current workflow", said);
            Assert.Contains("chat run chat-c1", said);
            Assert.Contains(WorkflowRunWords.ChatChecks("light"), said);
        }

        Assert.False(Directory.Exists(sources.Home));
    }

    /// <summary>The line <c>c1</c> began in its own tree, ended, and its hand-over <c>c2</c>, live, on the same tree and line.</summary>
    private const string ChatRecords = """
        [{"id":"c1","repository":"engine","state":"completed","kind":"chat","adapter":"claude-code","tree":"trees/default/engine/s-c1c1c1c1",
          "entry":"session:c1/engine","created":"2026-10-09T09:00:00Z","updated":"2026-10-09T09:30:00Z"},
         {"id":"c2","repository":"engine","state":"working","kind":"chat","from":"c1","adapter":"codex-acp","tree":"trees/default/engine/s-c1c1c1c1",
          "entry":"session:c1/engine","created":"2026-10-09T09:40:00Z","updated":"2026-10-09T09:50:00Z"}]
        """;

    /// <summary>
    /// Row c's first proof, under a steps version: the run bound to the repository's default, a steps version naming its work's
    /// <c>checks</c>, shows that version's checks, read from its binding.
    /// </summary>
    [Fact]
    public async Task A_chat_run_bound_to_a_steps_version_shows_that_version_s_checks()
    {
        var home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workflow-run-chat", Guid.NewGuid().ToString("N")[..8]);
        try
        {
            WorkflowStore.Add(home, new WorkflowVersionAdded("chat-steps", "A conversation's steps", "terminal", "2026-10-09T09:00:00Z", JsonNode.Parse(
                """[{"id":"work","kind":"work","checks":"heavy-at-end"},{"id":"landing","kind":"landing","form":"merge","accept":"you"}]""")), kept: []);
            var config = DriverConfig.Parse("""{"workflows":{"engine":{"default":"chat-steps"}},"sessionChecks":{"engine":"light"}}""");
            Assert.True(ChatRuns.Bind(config, home, "chat-c1", "engine", "aurora", [], T).Wrote);
            using var service = new ServiceClient("http://stand-in", null, new HttpClient(new ChatService(ChatRecords)));
            var records = new WorkflowRunTests.Trees(holds: tree => tree.StartsWith("trees/", StringComparison.Ordinal)) { BetweenTurns = { "c2" }, Tips = { [ChatTree] = new(TipA, 1) } };

            var run = Assert.Single((await WorkflowRunReader.ReadAsync(
                new WorkflowRunAsk(Session: "c2"), new WorkflowRunSources(service, home, config, [], _ => "aurora"), records)).Runs);

            Assert.True(run.Process!.Named);
            Assert.Equal("heavy-at-end", WorkflowRunWords.ChecksOf(run));
            Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Accept), Where(run, WorkflowKinds.Landing));
        }
        finally
        {
            GitSeed.Discard(home);
        }
    }

    /// <summary>
    /// The Opus check's finding 3: a chat run that landed, whose line then takes a quest, reads that quest's run from the take on. The
    /// acceptance its chat run made before the take is not counted there; one made after it is; and the chat run's binding is left as it
    /// stands, read by nothing, for the session delete of the record <c>c1</c>.
    /// </summary>
    [Fact]
    public async Task A_chat_run_whose_line_then_takes_a_quest_reads_that_quest_s_run_without_its_earlier_landing()
    {
        var home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "workflow-run-chat", Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Assert.True(ChatRuns.Bind(DriverConfig.Empty, home, "chat-c1", "engine", "aurora", [], T).Wrote);
            using var service = new ServiceClient("http://stand-in", null, new HttpClient(new ChatService("""
                [{"id":"c1","repository":"engine","state":"completed","kind":"chat","adapter":"claude-code","tree":"trees/default/engine/s-c1c1c1c1",
                  "entry":"session:c1/engine","created":"2026-10-09T09:00:00Z","updated":"2026-10-09T09:30:00Z","evidence":"commits landed:\nabc1234 the change"},
                 {"id":"c2","repository":"engine","state":"working","kind":"chat","from":"c1","tookQuest":"q4","adapter":"claude-code",
                  "tree":"trees/default/engine/s-c1c1c1c1","entry":"session:c1/engine","created":"2026-10-09T10:00:00Z","updated":"2026-10-09T10:10:00Z"}]
                """)
            {
                Quests = """[{"id":"q4","from":"person","to":"engine","title":"Quest q4","body":"","status":"Done","updated":"2026-10-09T11:00:00Z"}]""",
                Claims = { ["q4"] = """{"claim":"held","taken":"2026-10-09T10:05:00Z"}""" },
            }));
            var sources = new WorkflowRunSources(service, home, DriverConfig.Empty, [], _ => "aurora");
            var records = new WorkflowRunTests.Trees(holds: tree => tree.StartsWith("trees/", StringComparison.Ordinal)) { Accepted = { ["c1"] = T.AddMinutes(30) } };

            var run = Assert.Single((await WorkflowRunReader.ReadAsync(new WorkflowRunAsk(Session: "c1"), sources, records)).Runs);
            Assert.Equal(["q4"], run.Quests);
            Assert.Null(run.ChatRun);
            // Its chat run's acceptance at 09:30 was made before the take at 10:05: never read as the quest's landing.
            Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Accept), Where(run, WorkflowKinds.Landing));

            records.Accepted["c1"] = T.AddHours(2);
            var after = Assert.Single((await WorkflowRunReader.ReadAsync(new WorkflowRunAsk(Session: "c2"), sources, records)).Runs);
            Assert.Equal((WorkflowRunStates.Done, WorkflowRunDetails.Merge), Where(after, WorkflowKinds.Landing));

            Assert.True(File.Exists(WorkflowRunBindings.PathOf(home, "chat-c1")));
        }
        finally
        {
            GitSeed.Discard(home);
        }
    }

    /// <summary>The service a chat run's read reads: its records, its quests and each quest's claim, every other door empty.</summary>
    private sealed class ChatService(string sessions) : HttpMessageHandler
    {
        public string Quests { get; init; } = "[]";

        public Dictionary<string, string> Claims { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = path switch
            {
                "/api/quests?includeClosed=true" => Quests,
                "/api/sessions?includeClosed=true" => sessions,
                _ when path.StartsWith("/api/quests/", StringComparison.Ordinal) && path.EndsWith("/claim", StringComparison.Ordinal)
                    => Claims.GetValueOrDefault(path["/api/quests/".Length..^"/claim".Length], "{}"),
                _ => "[]",
            };
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
