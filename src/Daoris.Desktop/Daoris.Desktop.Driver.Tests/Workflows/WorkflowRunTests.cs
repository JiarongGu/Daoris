using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOW1c (D157 point 11; the workflow design §5.1–§5.2, §7): a run is a chain's work in one repository, read as steps from the
/// records that already exist, never stored and never read from the conversation. A state table: each row stands in the records a
/// run reads (its quests and their sessions, the go-aheads on its ask, the review's gate, the second opinions, the landing record,
/// the acceptance a session's record keeps, the due list and a pull request's kept answer) and says where each step stands.
/// </summary>
public sealed class WorkflowRunTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_bound_graph_keeps_partial_steps_and_a_task_added_look_without_colliding_ids()
    {
        var config = DriverConfig.Parse(LookRule);
        var binding = new WorkflowRunBinding("q1", "engine", "aurora", "release", WorkflowLevels.Task, T)
        { Version = 1, Digest = "bound-digest" };
        IReadOnlyList<NamedStep> named = [
            new("look", WorkflowKinds.Work, []),
            new("permit", WorkflowKinds.GoAhead, [new("act", "publish"), new("on", "local")]),
            new("land", WorkflowKinds.Landing, [new("form", "merge"), new("accept", "you")]),
        ];
        var process = WorkflowProcess.Of(config, "engine", "aurora", binding, named);
        var graph = WorkflowRunGraph.Read(process, WorkflowCurrent.Derive(config, "engine", "aurora", []), []);
        var facts = new WorkflowRunFacts("engine", graph)
        {
            Process = process, Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")],
            Review = new ReviewGateState(ReviewStates.NotShown, new ReviewDecision(ReviewLevels.Chain, "local")),
        };

        var run = WorkflowRuns.Derive(facts, "aurora");

        Assert.Equal(["look", "permit", "added-look", "land"], run.Steps.Select(step => step.Step.Id));
        Assert.Equal(WorkflowRunStates.CannotStart, run.Steps[1].State);
        Assert.Equal(WorkflowLimits.Says(WorkflowLimits.GoAheadPartial), run.Steps[1].Words);
        Assert.Contains(WorkflowLimits.Says(WorkflowLimits.GoAheadPartial), WorkflowRunWords.Said(run.Steps[1]));
        Assert.Equal(ReviewLevels.Chain, run.Steps[2].Added);
        Assert.Equal(WorkflowRunStates.WaitingOnYou, run.Steps[2].State);
        Assert.Equal("permit", run.At);
        var said = WorkflowRunWords.Say([run], null);
        Assert.Contains("`release` v1 workflow", said);
        Assert.DoesNotContain("Current workflow", said);
    }

    /// <summary>A branch landing pushed by a plugin that lands and answers its state.</summary>
    private const string PullRequestRule =
        """{"landings":{"engine":{"form":"branch","pattern":"work/{quest}","plugin":"example.pull-request"}}}""";

    private const string AutomaticRule =
        """{"landings":{"engine":{"form":"branch","pattern":"work/{quest}","autoAccept":true}}}""";

    private const string LookRule =
        """{"reviews":{"engine":{"required":true,"environments":[{"name":"local","kind":"local","procedure":"README.md","address":"http://localhost:4200"}]}}}""";

    private const string OpinionRule = """{"opinions":{"engine":{"reviewers":["codex-acp"]}}}""";

    private const string RequiredOpinionRule = """{"opinions":{"engine":{"reviewers":["codex-acp"],"required":true}}}""";

    private const string OpinionAndLookRule =
        """{"opinions":{"engine":{"reviewers":["codex-acp"]}},"reviews":{"engine":{"required":true,"environments":[{"name":"local","kind":"local","procedure":"README.md","address":"http://localhost:4200"}]}}}""";

    private static readonly IReadOnlyList<WorkflowPlugin> Plugins = [new("example.pull-request", [HookPoints.Land, HookPoints.State])];

    /// <summary>One row of the table: the rules, the records, and where each step stands, then where the run stands.</summary>
    private sealed record Row(string Config, Func<WorkflowRunFacts, WorkflowRunFacts> Records, (string Step, string State, string Detail)[] Expect, string? At);

    private static readonly Dictionary<string, Row> Rows = new()
    {
        // The work.
        ["an open quest no session works on yet is queued"] = new("{}", facts => facts with { Quests = [Quest("q1", "Open")] },
            [("work", "working", "queued"), ("landing", "not-reached", "")], "work"),
        ["a session at work is the agent working"] = new("{}", facts => facts with { Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "working")] },
            [("work", "working", "agent"), ("landing", "not-reached", "")], "work"),
        ["a session parked asking waits on you"] = new("{}", facts => facts with { Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "awaiting-person")] },
            [("work", "waiting-on-you", "park")], "work"),
        ["a park you answered goes on at the driver's next look"] = new("{}", facts => facts with
        {
            Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "awaiting-person") with { Answer = "yes, dev only" }],
        }, [("work", "working", "answered")], "work"),
        ["a go-ahead a session asked waits on you"] = new("{}", facts => facts with
        {
            Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "working")],
            GoAheads = [new GoAheadView(1, "write", "dev", "write the configuration", [new GoAheadRequestView("s1", "q1", T, "it needs one")])],
        }, [("work", "waiting-on-you", "go-ahead")], "work"),
        ["a go-ahead you answered holds nothing"] = new("{}", facts => facts with
        {
            Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "working")],
            GoAheads = [new GoAheadView(1, "write", "dev", "write it", [new GoAheadRequestView("s1", "q1", T, "why")]) { Answer = new(true, null, T) }],
        }, [("work", "working", "agent")], "work"),
        ["a done held for your yes waits on you"] = new("{}", facts => facts with
        {
            Quests = [Quest("q1", "Done") with { Held = true, Hold = "evidence-missing" }], Sessions = [Session("s1", "q1", "completed")],
        }, [("work", "waiting-on-you", "held"), ("landing", "not-reached", "")], "work"),
        ["a question asked of another repository waits on its agent"] = new("{}", facts => facts with
        {
            Quests = [Quest("q1", "Taken") with { Awaits = "q9" }], Sessions = [Session("s1", "q1", "stood-down")],
        }, [("work", "waiting-on-agent", "awaits")], "work"),
        ["a last session that failed is failed"] = new("{}", facts => facts with
        {
            Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "failed") with { Note = "the build broke" }],
        }, [("work", "failed", "failed")], "work"),
        ["a session you stopped is stopped"] = new("{}", facts => facts with { Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "stopped")] },
            [("work", "stopped", "stopped")], "work"),
        ["a stop that was not yours waits for the driver"] = new("{}", facts => facts with
        {
            Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "stopped") with { Interrupted = true }],
        }, [("work", "working", "queued")], "work"),
        ["a taken quest whose session finished waits for your done"] = new("{}", facts => facts with
        {
            Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "completed")],
        }, [("work", "waiting-on-you", "unclosed")], "work"),
        ["a declined quest stops the work"] = new("{}", facts => facts with { Quests = [Quest("q1", "Declined")] },
            [("work", "stopped", "declined"), ("landing", "not-reached", "")], "work"),
        ["a chain's later step still at work keeps the work going"] = new("{}", facts => facts with
        {
            Quests = [Quest("q1", "Done"), Quest("q2", "Taken")], Sessions = [Session("s1", "q1", "completed"), Session("s2", "q2", "working", 1)],
        }, [("work", "working", "agent")], "work"),

        // A closure graph's work (FABRIC-WORKFLOW2d, §E5): finished at its unheld, sealed submissions, before any Done.
        ["a closure chain's submissions unheld and sealed finish the work before its Done"] = new("{}", facts => facts with
        {
            Quests = [Bound("q1", 1), Bound("q2", 2, last: true) with { Parent = "q1", GrewFrom = Made(1) }],
            Sessions = [Session("s1", "q1", "completed"), Session("s2", "q2", "completed", 1)],
        }, [("work", "done", "submitted"), ("landing", "waiting-on-you", "accept")], "landing"),
        ["a sealed submission passes the work to the chain's next step"] = new("{}", facts => facts with
        {
            Quests = [Bound("q1", 1), Bound("q2", null, last: true) with { Parent = "q1", GrewFrom = Made(1) }],
            Sessions = [Session("s1", "q1", "completed"), Session("s2", "q2", "working", 1)],
        }, [("work", "working", "agent"), ("landing", "not-reached", "")], "work"),
        ["a held submission waits for your yes"] = new("{}", facts => facts with
        {
            Quests = [Bound("q1", 1, last: true) with { Held = true, Hold = "evidence-missing" }], Sessions = [Session("s1", "q1", "completed")],
        }, [("work", "waiting-on-you", "held"), ("landing", "not-reached", "")], "work"),
        ["a submission its step does not stand on is not sealed"] = new("{}", facts => facts with
        {
            Quests = [Bound("q1", 3), Bound("q2", null, last: true) with { Parent = "q1", GrewFrom = Made(1) }],
            Sessions = [Session("s1", "q1", "completed")],
        }, [("work", "waiting-on-you", "unclosed"), ("landing", "not-reached", "")], "work"),
        ["a reopened submission takes the finish back and says so"] = new("{}", facts => facts with
        {
            Quests = [Bound("q1", null), Withdrawn(Bound("q2", null, last: true) with { Status = "Open" })],
            Sessions = [Session("s1", "q1", "completed")],
        }, [("work", "waiting-on-agent", "reopened"), ("landing", "not-reached", "")], "work"),

        // The landing.
        ["work finished waits for your accept"] = new("{}", facts => facts with
        {
            Quests = [Quest("q1", "Done"), Quest("q2", "Done")], Sessions = [Session("s1", "q1", "completed"), Session("s2", "q2", "completed", 1)],
        }, [("work", "done", "finished"), ("landing", "waiting-on-you", "accept")], "landing"),
        ["an acceptance the session's record keeps is landed into the line"] = new("{}", facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")],
            Accepted = new Dictionary<string, DateTimeOffset> { ["s1"] = T.AddMinutes(5) },
        }, [("work", "done", "finished"), ("landing", "done", "merge")], null),
        ["work that made no commits has nothing to land"] = new("{}", facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed") with { Evidence = "no commits landed" }],
        }, [("landing", "done", "nothing")], null),
        ["an automatic landing lands at the driver's next look"] = new(AutomaticRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")],
            AutoLandings = [new AutoLanding("s1", "q1", "engine", "aurora", "tree", T)],
        }, [("landing", "working", "automatic")], "landing"),
        ["an automatic landing its last try could not land cannot start"] = new(AutomaticRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")],
            AutoLandings = [new AutoLanding("s1", "q1", "engine", "aurora", "tree", T) { Tries = [new AutoTry(T, AutoLandingCode.Uncommitted)] }],
        }, [("landing", "cannot-start", "refused")], "landing"),
        ["an automatic landing whose work reached the line already is done"] = new(AutomaticRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")],
            AutoLandings = [new AutoLanding("s1", "q1", "engine", "aurora", "tree", T) { Tries = [new AutoTry(T, AutoLandingCode.Carried)], Closed = T }],
        }, [("landing", "done", "already")], null),
        ["a tree discarded before it landed stops the landing"] = new(AutomaticRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")],
            AutoLandings = [new AutoLanding("s1", "q1", "engine", "aurora", "tree", T) { Tries = [new AutoTry(T, AutoLandingCode.Gone)], Closed = T }],
        }, [("landing", "stopped", "gone")], "landing"),
        ["a teammate's work lands on their machine"] = new("{}", facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("origin/s1", "q1", "completed")],
        }, [("landing", "not-known", "elsewhere")], "landing"),

        // The pull request.
        ["a landed branch's pull request waits for your merge"] = new(PullRequestRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")],
            Landings = [Landed("s1") with { PullRequestState = new PullRequestState(PullRequestStates.Open) { AskedAt = T } }],
        }, [("work", "done", "finished"), ("landing", "done", "branch"), ("pull-request", "waiting-on-you", "merge")], "pull-request"),
        ["a pull request the platform completed is merged"] = new(PullRequestRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")],
            Landings = [Landed("s1") with { PullRequestState = new PullRequestState(PullRequestStates.Completed) { At = T.AddHours(2) } }],
        }, [("pull-request", "done", "merged")], null),
        ["a pull request abandoned stops"] = new(PullRequestRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")],
            Landings = [Landed("s1") with { PullRequestState = new PullRequestState(PullRequestStates.Abandoned) }],
        }, [("pull-request", "stopped", "abandoned")], "pull-request"),
        ["a branch its plugin did not push cannot open one"] = new(PullRequestRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")], Landings = [Landed("s1") with { Pushed = false }],
        }, [("pull-request", "cannot-start", "not-pushed")], "pull-request"),
        ["a failed ask newer than the answer is not known"] = new(PullRequestRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")],
            Landings =
            [
                Landed("s1") with
                {
                    PullRequestState = new PullRequestState(PullRequestStates.Open) { AskedAt = T },
                    PullRequestAskFailed = new PullRequestAskFailed("timed-out", "example.pull-request", T.AddHours(1)),
                },
            ],
        }, [("pull-request", "not-known", "ask-failed")], "pull-request"),
        ["work landed into its line opens no pull request"] = new(PullRequestRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")],
            Accepted = new Dictionary<string, DateTimeOffset> { ["s1"] = T },
        }, [("landing", "done", "merge"), ("pull-request", "skipped", "no-branch")], null),
        ["no pull request is reached before the landing"] = new(PullRequestRule, facts => facts with { Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "working")] },
            [("landing", "not-reached", ""), ("pull-request", "not-reached", "")], "work"),
        ["work with nothing to land opens no pull request"] = new(PullRequestRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed") with { Evidence = "no commits landed" }],
        }, [("landing", "done", "nothing"), ("pull-request", "skipped", "nothing")], null),

        // The look.
        ["a look is not reached while the work runs"] = new(LookRule, facts => facts with
        {
            Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "working")], Review = Gate(ReviewStates.NotShown),
        }, [("work", "working", "agent"), ("look", "not-reached", ""), ("landing", "not-reached", "")], "work"),
        ["no set-up step shows it yet: it waits for you"] = new(LookRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")], Review = Gate(ReviewStates.NotShown),
        }, [("work", "done", "finished"), ("look", "waiting-on-you", "not-shown"), ("landing", "not-reached", "")], "look"),
        ["the set-up step at work is being set up"] = new(LookRule, facts => facts with
        {
            Quests = [Quest("q1", "Done"), SetUpStep("q2", "Taken")], Sessions = [Session("s1", "q1", "completed"), Session("s2", "q2", "working", 1)],
            Review = Gate(ReviewStates.BeingSetUp, SetUpStep("q2", "Taken")),
        }, [("work", "done", "finished"), ("look", "working", "being-set-up")], "look"),
        ["a set-up shown waits for your look"] = new(LookRule, facts => facts with
        {
            Quests = [Quest("q1", "Done"), SetUpStep("q2", "Done")], Sessions = [Session("s1", "q1", "completed"), Session("s2", "q2", "completed", 1)],
            Review = Gate(ReviewStates.Shown, SetUpStep("q2", "Done")) with { SetUp = new QuestSetUpView(Commit) { At = T, Session = "s2" } },
        }, [("look", "waiting-on-you", "shown"), ("landing", "not-reached", "")], "look"),
        ["your not yet waits on the set-up step's agent"] = new(LookRule, facts => facts with
        {
            Quests = [Quest("q1", "Done"), SetUpStep("q2", "Done")], Sessions = [Session("s1", "q1", "completed")],
            Review = Gate(ReviewStates.NotYet, SetUpStep("q2", "Done")) with
            {
                SetUp = new QuestSetUpView(Commit), Verdict = new QuestReviewVerdictView(ReviewVerdicts.NotYet) { Words = "the header overlaps" },
            },
        }, [("look", "waiting-on-agent", "not-yet")], "look"),
        ["a review that does not hold the newest commits waits to be shown again"] = new(LookRule, facts => facts with
        {
            Quests = [Quest("q1", "Done"), SetUpStep("q2", "Done")], Sessions = [Session("s1", "q1", "completed")],
            Review = Gate(ReviewStates.NotHeld, SetUpStep("q2", "Done")) with { SetUp = new QuestSetUpView(Commit) },
        }, [("look", "waiting-on-agent", "not-held")], "look"),
        ["reviewed, the landing waits for your accept"] = new(LookRule, facts => facts with
        {
            Quests = [Quest("q1", "Done"), SetUpStep("q2", "Done")], Sessions = [Session("s1", "q1", "completed")],
            Review = Gate(ReviewStates.Reviewed, SetUpStep("q2", "Done")) with { SetUp = new QuestSetUpView(Commit) },
        }, [("look", "done", "reviewed"), ("landing", "waiting-on-you", "accept")], "landing"),
        ["your skip passes the look over"] = new(LookRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")],
            Review = new ReviewGateState(ReviewStates.Skipped, new ReviewDecision(ReviewLevels.Skip, null)
            {
                Repository = "engine", SkippedOn = "q1", Words = "a typo", Skip = new QuestReviewVerdictView(ReviewVerdicts.Skipped) { At = T },
            }),
        }, [("look", "skipped", "skip"), ("landing", "waiting-on-you", "accept")], "landing"),
        ["the ask's choice turns the look off for this work"] = new(LookRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")],
            Review = new ReviewGateState(ReviewStates.None, new ReviewDecision(ReviewLevels.Ask, null) { Repository = "engine", Choice = "off" }),
        }, [("look", "skipped", "off"), ("landing", "waiting-on-you", "accept")], "landing"),
        ["a look the chain's choice adds is drawn for this work"] = new("{}", facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")], Review = Gate(ReviewStates.NotShown, level: ReviewLevels.Chain),
        }, [("work", "done", "finished"), ("look", "waiting-on-you", "not-shown"), ("landing", "not-reached", "")], "look"),
        ["a gate that could not be read is not known"] = new(LookRule, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")],
            Review = new ReviewGateState(ReviewStates.Unread, new ReviewDecision(ReviewLevels.Nothing, null)) { Problem = "the service did not answer" },
        }, [("look", "not-known", "unread"), ("landing", "not-reached", "")], "look"),
        ["a landing done is what the look let go"] = new(LookRule, facts => facts with
        {
            Quests = [Quest("q1", "Done"), SetUpStep("q2", "Done")], Sessions = [Session("s1", "q1", "completed")],
            // The tree went with the landing, so git could not say the reviewed set-up holds the tip.
            Review = Gate(ReviewStates.NotHeld, SetUpStep("q2", "Done")) with { SetUp = new QuestSetUpView(Commit) },
            Accepted = new Dictionary<string, DateTimeOffset> { ["s1"] = T },
        }, [("look", "done", "reviewed"), ("landing", "done", "merge")], null),

        // The second opinion: its gate's states (XAGENT1f), the run standing at it while the gate holds.
        ["an opinion is not reached while the work runs"] = new(OpinionRule, facts => facts with
        {
            Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "working")], Opinion = OpinionAt(OpinionGateStates.WaitsChain),
        }, [("work", "working", "agent"), ("opinion", "not-reached", ""), ("landing", "not-reached", "")], "work"),
        ["an opinion not asked yet is asked at the driver's next look"] = new(OpinionRule, facts => Done(facts) with { Opinion = OpinionAt(OpinionGateStates.NotAsked) },
            [("work", "done", "finished"), ("opinion", "working", "not-asked"), ("landing", "not-reached", "")], "opinion"),
        ["an opinion being read is the reviewer working"] = new(OpinionRule, facts => Done(facts) with { Opinion = OpinionAt(OpinionGateStates.Reading) },
            [("opinion", "working", "reading"), ("landing", "not-reached", "")], "opinion"),
        ["findings with the working session wait on its agent"] = new(OpinionRule, facts => Done(facts) with { Opinion = OpinionAt(OpinionGateStates.WithSession) },
            [("opinion", "waiting-on-agent", "with-session"), ("landing", "not-reached", "")], "opinion"),
        ["the commits made in answer read again are the reviewer working"] = new(OpinionRule, facts => Done(facts) with { Opinion = OpinionAt(OpinionGateStates.ReadAgain) },
            [("opinion", "working", "read-again")], "opinion"),
        ["a dispute waits on you"] = new(OpinionRule, facts => Done(facts) with
        {
            Opinion = OpinionAt(OpinionGateStates.Disputed) with { Disputes = new OpinionDisputes([1, 2], []) },
        }, [("opinion", "waiting-on-you", "disputed"), ("landing", "not-reached", "")], "opinion"),
        ["commits no other agent read wait on you"] = new(OpinionRule, facts => Done(facts) with { Opinion = OpinionAt(OpinionGateStates.CommitsSince) with { Since = 2 } },
            [("opinion", "waiting-on-you", "commits-since"), ("landing", "not-reached", "")], "opinion"),
        ["an opinion the rule requires and none can be had waits on you"] = new(RequiredOpinionRule, facts => Done(facts) with
        {
            Opinion = OpinionAt(OpinionGateStates.Unavailable, required: true) with { Code = "no-reviewer" },
        }, [("opinion", "waiting-on-you", "unavailable"), ("landing", "not-reached", "")], "opinion"),
        ["an opinion none can be had of, not required, is passed over"] = new(OpinionRule, facts => Done(facts) with
        {
            Opinion = OpinionAt(OpinionGateStates.Unavailable) with { Code = "no-reviewer" },
        }, [("opinion", "skipped", "unavailable"), ("landing", "waiting-on-you", "accept")], "landing"),
        ["a settled opinion lets the landing go"] = new(OpinionRule, facts => Done(facts) with { Opinion = OpinionAt(OpinionGateStates.Settled) },
            [("opinion", "done", "settled"), ("landing", "waiting-on-you", "accept")], "landing"),
        ["your go on anyway lets the landing go"] = new(OpinionRule, facts => Done(facts) with
        {
            Opinion = OpinionAt(OpinionGateStates.Anyway) with { Person = new OpinionPersonWord(OpinionPersonSaid.Anyway, Commit, T) { Words = "a typo" } },
        }, [("opinion", "done", "anyway"), ("landing", "waiting-on-you", "accept")], "landing"),
        ["your own look lets the landing go"] = new(OpinionRule, facts => Done(facts) with
        {
            Opinion = OpinionAt(OpinionGateStates.Myself) with { Person = new OpinionPersonWord(OpinionPersonSaid.Myself, Commit, T) },
        }, [("opinion", "done", "myself")], "landing"),
        ["a press that answered what it showed lets the landing go"] = new(OpinionRule, facts => Done(facts) with
        {
            Opinion = OpinionAt(OpinionGateStates.Answered) with { Person = new OpinionPersonWord(OpinionPersonSaid.Press, Commit, T) },
        }, [("opinion", "done", "answered")], "landing"),
        ["a gate that could not be read holds the run, not known"] = new(OpinionRule, facts => Done(facts) with
        {
            Opinion = OpinionAt(OpinionGateStates.Unread) with { Problem = "the service did not answer" },
        }, [("opinion", "not-known", "unread"), ("landing", "not-reached", "")], "opinion"),
        ["a gate with no tree to read it in is not known"] = new(OpinionRule, facts => Done(facts) with { Opinion = null },
            [("opinion", "not-known", "unread")], "opinion"),
        ["a rule that reads only before next steps asks nothing of the landing"] = new(OpinionRule, facts => Done(facts) with { Opinion = OpinionAt(OpinionGateStates.None) },
            [("opinion", "skipped", "none"), ("landing", "waiting-on-you", "accept")], "landing"),
        ["no opinion step where no rule stands"] = new("{}", facts => Done(facts) with { Opinion = OpinionAt(OpinionGateStates.Reading) },
            [("work", "done", "finished"), ("landing", "waiting-on-you", "accept")], "landing"),
        ["a landing done is what the opinion let go, as its record keeps it"] = new(OpinionRule, facts => Done(facts) with
        {
            Opinion = OpinionAt(OpinionGateStates.Unread),
            Accepted = new Dictionary<string, DateTimeOffset> { ["s1"] = T },
        }, [("opinion", "done", "landed"), ("landing", "done", "merge")], null),
        ["a branch's landing record says the opinion that let it go"] = new(OpinionRule, facts => Done(facts) with
        {
            Landings = [Landed("s1") with { Opinion = new LandingOpinion(OpinionGateStates.Anyway) { Words = "a typo" } }],
        }, [("opinion", "done", "anyway"), ("landing", "done", "branch")], null),
        ["the look waits while an agent is at work on the opinion"] = new(OpinionAndLookRule, facts => Done(facts) with
        {
            Review = Gate(ReviewStates.NotShown), Opinion = OpinionAt(OpinionGateStates.Reading),
        }, [("opinion", "working", "reading"), ("look", "not-reached", ""), ("landing", "not-reached", "")], "opinion"),
        ["the look goes on beside a dispute, whose Reviewed answers it"] = new(OpinionAndLookRule, facts => Done(facts) with
        {
            Review = Gate(ReviewStates.NotShown), Opinion = OpinionAt(OpinionGateStates.Disputed) with { Disputes = new OpinionDisputes([1], []) },
        }, [("opinion", "waiting-on-you", "disputed"), ("look", "waiting-on-you", "not-shown"), ("landing", "not-reached", "")], "opinion"),
    };

    /// <summary>The work done, its one quest closed and its session completed.</summary>
    private static WorkflowRunFacts Done(WorkflowRunFacts facts) =>
        facts with { Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")] };

    public static TheoryData<string> RowNames() => [.. Rows.Keys];

    [Theory]
    [MemberData(nameof(RowNames))]
    public void Each_step_stands_where_its_records_say(string name)
    {
        var row = Rows[name];
        var current = WorkflowCurrent.Derive(DriverConfig.Parse(row.Config), "engine", "aurora", Plugins);

        var run = WorkflowRuns.Derive(row.Records(new WorkflowRunFacts("engine", current)), "aurora");

        foreach (var (step, state, detail) in row.Expect)
        {
            var drawn = run.Steps.SingleOrDefault(each => each.Step.Id == step);
            Assert.True(drawn is not null, $"{name}: no {step} step in [{string.Join(", ", run.Steps.Select(each => each.Step.Id))}]");
            Assert.True((state, detail) == (drawn.State, drawn.Detail), $"{name}: {step} is {drawn.State}/{drawn.Detail}, not {state}/{detail}");
        }

        Assert.True(row.At == run.At, $"{name}: the run stands at {run.At ?? "nothing"}, not {row.At ?? "nothing"}");
    }

    [Fact]
    public void The_steps_are_Current_s_in_order_with_the_rules_cells()
    {
        var current = WorkflowCurrent.Derive(DriverConfig.Parse(PullRequestRule), "engine", "aurora", Plugins);
        var run = WorkflowRuns.Derive(new WorkflowRunFacts("engine", current) { Quests = [Quest("q1", "Open")] }, "aurora", "a1");

        Assert.Equal(current.Steps, run.Steps.Select(step => step.Step));
        Assert.Equal(["q1"], run.Quests);
        Assert.Equal("a1", run.Ask);
        Assert.Equal("aurora", run.Workspace);
        Assert.All(run.Steps, step => Assert.Null(step.Added));
    }

    [Fact]
    public void A_look_the_work_asked_for_says_which_level_added_it()
    {
        var current = WorkflowCurrent.Derive(DriverConfig.Parse("{}"), "engine", "aurora", []);
        var facts = new WorkflowRunFacts("engine", current)
        {
            Quests = [Quest("q1", "Done")],
            Sessions = [Session("s1", "q1", "completed")],
            Review = Gate(ReviewStates.NotShown, level: ReviewLevels.Ask),
        };

        var look = WorkflowRuns.Derive(facts, "aurora").Steps.Single(step => step.Step.Kind == WorkflowKinds.Look);

        Assert.Equal(ReviewLevels.Ask, look.Added);
        Assert.Equal("local", look.Environment);
        Assert.Equal(WorkflowParticipation.AgentAndYou, look.Step.Participation);
        Assert.Equal(WorkflowPress.Reviewed, look.Step.Press);
    }

    [Fact]
    public void A_step_says_the_facts_its_words_and_its_door_are_made_of()
    {
        var current = WorkflowCurrent.Derive(DriverConfig.Parse(PullRequestRule), "engine", "aurora", Plugins);
        var facts = new WorkflowRunFacts("engine", current)
        {
            Quests = [Quest("q1", "Done"), Quest("q2", "Done")],
            Sessions = [Session("s1", "q1", "completed"), Session("s2", "q2", "completed", 1)],
            Landings =
            [
                Landed("s2") with
                {
                    PullRequest = "https://example.test/pull/7", AcceptedBy = AcceptedBy.Auto,
                    PullRequestState = new PullRequestState(PullRequestStates.Open) { AskedAt = T.AddHours(1) },
                },
            ],
        };

        var run = WorkflowRuns.Derive(facts, "aurora");
        var (work, landing, pullRequest) = (run.Steps[0], run.Steps[1], run.Steps[2]);

        Assert.Equal((2, 2, "q2", "s2", "claude-code"), (work.Count, work.Of, work.Quest, work.Session, work.Agent));
        Assert.Equal(("work/q1", "s2", AcceptedBy.Auto, "example.pull-request"), (landing.Branch, landing.Session, landing.Code, landing.Plugin));
        Assert.Equal(("https://example.test/pull/7", PullRequestStates.Open, T.AddHours(1)), (pullRequest.PullRequest, pullRequest.Code, pullRequest.At));
        Assert.Equal("s2", run.Session);
    }

    /// <summary>
    /// The opinion's step says the gate's facts its words and its door are made of: who reads it and in which session, how many
    /// findings are with the working session, how many are disputed or how many commits nobody read, and the person's words.
    /// </summary>
    [Fact]
    public void The_opinion_s_step_says_the_gate_s_facts()
    {
        var current = WorkflowCurrent.Derive(DriverConfig.Parse(OpinionRule), "engine", "aurora", []);
        WorkflowRunStep Opinion(OpinionGateState gate) =>
            WorkflowRuns.Derive(Done(new WorkflowRunFacts("engine", current)) with { Opinion = gate }, "aurora").Steps.Single(step => step.Step.Kind == WorkflowKinds.Opinion);

        var reading = Opinion(OpinionAt(OpinionGateStates.Reading));
        Assert.Equal(("r1", "Codex (OpenAI)", WorkflowRuntime.Partial), (reading.Session, reading.Agent, reading.Step.Runtime));
        var with = Opinion(OpinionAt(OpinionGateStates.WithSession));
        Assert.Equal(("s1", 3), (with.Session, with.Count));
        var disputed = Opinion(OpinionAt(OpinionGateStates.Disputed) with { Disputes = new OpinionDisputes([1], [4]) });
        Assert.Equal(("s1", 2), (disputed.Session, disputed.Count));
        Assert.Equal(3, Opinion(OpinionAt(OpinionGateStates.CommitsSince) with { Since = 3 }).Count);
        var anyway = Opinion(OpinionAt(OpinionGateStates.Anyway) with { Person = new OpinionPersonWord(OpinionPersonSaid.Anyway, Commit, T) { Words = "a typo" } });
        Assert.Equal(("a typo", OpinionPersonSaid.Anyway, T), (anyway.Words, anyway.Code, anyway.At));
        Assert.Equal("no-reviewer", Opinion(OpinionAt(OpinionGateStates.Unavailable, required: true) with { Code = "no-reviewer" }).Code);
        Assert.Equal("q2", Opinion(OpinionAt(OpinionGateStates.WaitsChain) with { Later = "q2" }).Quest);
    }

    [Fact]
    public void A_go_ahead_names_its_number_its_session_and_its_act()
    {
        var current = WorkflowCurrent.Derive(DriverConfig.Parse("{}"), "engine", "aurora", []);
        var facts = new WorkflowRunFacts("engine", current)
        {
            Quests = [Quest("q1", "Taken")],
            Sessions = [Session("s1", "q1", "working")],
            GoAheads = [new GoAheadView(3, "write", "dev", "write the configuration to dev", [new GoAheadRequestView("s1", "q1", T, "it needs one")])],
        };

        var work = WorkflowRuns.Derive(facts, "aurora", "a1").Steps[0];

        Assert.Equal((3, "s1", "write the configuration to dev", T), (work.GoAhead, work.Session, work.Words, work.At));
    }

    [Fact]
    public void A_session_s_evidence_says_how_many_commits_it_has_to_land()
    {
        Assert.Equal(0, WorkflowRuns.CommitsOf(Session("s1", "q1", "completed") with { Evidence = "no commits landed" }));
        Assert.Equal(2, WorkflowRuns.CommitsOf(Session("s1", "q1", "completed") with { Evidence = "commits landed:\nabc1234 one\ndef5678 two" }));
        // Unread, or none written, may still have work to land.
        Assert.Null(WorkflowRuns.CommitsOf(Session("s1", "q1", "completed") with { Evidence = "no commits readable" }));
        Assert.Null(WorkflowRuns.CommitsOf(Session("s1", "q1", "completed") with { Evidence = null }));
    }

    [Fact]
    public void Every_state_and_detail_the_table_holds_is_a_known_code()
    {
        var states = typeof(WorkflowRunStates).GetFields().Select(field => (string)field.GetValue(null)!).ToHashSet();
        // The opinion's details are its gate's own states (XAGENT1f).
        var details = typeof(WorkflowRunDetails).GetFields().Concat(typeof(OpinionGateStates).GetFields())
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetValue(null)!).Append("").ToHashSet();

        foreach (var (name, row) in Rows)
        {
            Assert.All(row.Expect, expected => Assert.True(states.Contains(expected.State), $"{name}: {expected.State}"));
            Assert.All(row.Expect, expected => Assert.True(details.Contains(expected.Detail), $"{name}: {expected.Detail}"));
        }
    }

    /// <summary>
    /// Whose runs are read: a quest's run is its own repository's, read over its whole chain (FABRIC-WORKFLOW2e) and naming the repository
    /// to run; a session's through its quest, an intake's through its ask; an ask's, each chain it asked, one run per repository. A chat
    /// serves no quest and has none.
    /// </summary>
    [Fact]
    public void A_run_is_read_for_a_quest_a_session_or_an_ask()
    {
        QuestView To(string id, string to, string? parent = null, string from = "ask #a1") => new(id, from, to, id, "", "Open") { Parent = parent };
        IReadOnlyList<QuestView> quests =
        [
            To("q1", "engine"), To("q2", "engine", "q1"), To("q3", "game", "q2"), To("q4", "game"), To("q5", "engine", from: "person"),
        ];
        IReadOnlyList<TracedSession> sessions =
        [
            Session("s1", "q2", "working"),
            new("i1", "ask #a1", "completed") { Ask = "a1" },
            new("c1", "engine", "working") { Kind = "chat" },
        ];
        string[][] Ids(IReadOnlyList<IReadOnlyList<QuestView>> chains) => [.. chains.Select(chain => chain.Select(quest => quest.Id).ToArray())];
        var trees = new Trees();

        Assert.Equal([["q1", "q2", "q3"]], Ids(WorkflowRunReader.ChainsOf(new(Quest: "#q2"), quests, sessions, trees).Chains));
        Assert.Equal([["q1", "q2", "q3"]], Ids(WorkflowRunReader.ChainsOf(new(Quest: "q3"), quests, sessions, trees).Chains));
        Assert.Equal([["q1", "q2", "q3"]], Ids(WorkflowRunReader.ChainsOf(new(Session: "s1"), quests, sessions, trees).Chains));
        Assert.Equal(["engine", "game", "engine", null, null], new[]
        {
            WorkflowRunReader.ChainsOf(new(Quest: "#q2"), quests, sessions, trees).Only, WorkflowRunReader.ChainsOf(new(Quest: "q3"), quests, sessions, trees).Only,
            WorkflowRunReader.ChainsOf(new(Session: "s1"), quests, sessions, trees).Only, WorkflowRunReader.ChainsOf(new(Session: "i1"), quests, sessions, trees).Only,
            WorkflowRunReader.ChainsOf(new(Ask: "a1"), quests, sessions, trees).Only,
        });
        Assert.Equal([["q1", "q2", "q3"], ["q4"]], Ids(WorkflowRunReader.ChainsOf(new(Session: "i1"), quests, sessions, trees).Chains));
        Assert.Equal([["q1", "q2", "q3"], ["q4"]], Ids(WorkflowRunReader.ChainsOf(new(Ask: "a1"), quests, sessions, trees).Chains));

        Assert.Equal(WorkflowNoRun.Said(WorkflowNoRun.ChatCheckout), WorkflowRunReader.ChainsOf(new(Session: "c1"), quests, sessions, trees).Problem);
        Assert.Equal("No quest `#q9` is here.", WorkflowRunReader.ChainsOf(new(Quest: "q9"), quests, sessions, trees).Problem);
        Assert.Equal("No session `s9` is recorded here.", WorkflowRunReader.ChainsOf(new(Session: "s9"), quests, sessions, trees).Problem);
        Assert.Contains("name one of them", WorkflowRunReader.ChainsOf(new(), quests, sessions, trees).Problem);
        Assert.Null(WorkflowRunReader.ChainsOf(new(), quests, sessions, trees).Code);
    }

    /// <summary>
    /// CHATFLOW1a (the chat-flow design §4.2, §10): a session that names no run is answered by a code, in §4.2's order, its English the
    /// code's sentence. A reviewer's <c>o-…</c> clone, a helper's and a partner's records stand in trees this home opened, and read as what
    /// they are, never as a chat; a chat in a tree of its own reads <c>chat-own-tree</c> and never <c>chat-checkout</c>. An intake still
    /// reads its ask's runs, and <c>ask-no-quest</c> before any; a chat that took a quest reads that quest's run, and so does its
    /// hand-over's next record, which carries no <c>took_quest</c> of its own; a closed quest's session gone on as a conversation in the
    /// quest's tree (its stored origin <c>quest:&lt;id&gt;</c>) reads that quest's run, never a chat run.
    /// </summary>
    [Fact]
    public void A_session_that_names_no_run_is_answered_by_its_code_in_the_designs_order()
    {
        QuestView To(string id, string to, string? parent = null, string from = "ask #a1") => new(id, from, to, id, "", "Open") { Parent = parent };
        IReadOnlyList<QuestView> quests = [To("q1", "engine"), To("q2", "engine", "q1"), To("q4", "game", from: "person"), To("q5", "engine", from: "person")];
        TracedSession Chat(string id, string? tree, string? entry = null) =>
            new(id, "engine", "working") { Kind = "chat", Tree = tree, Entry = entry ?? $"session:{id}/engine", Created = T };
        IReadOnlyList<TracedSession> sessions =
        [
            new("i1", "ask #a1", "completed") { Ask = "a1", Entry = "ask:a1/intake" },
            new("i2", "ask #a2", "completed") { Ask = "a2", Entry = "ask:a2/intake" },
            new("h1", "daoris:help", "working") { Kind = "chat", Entry = "session:h1/daoris:help" },
            Chat("r1", "trees/default/engine/o-1a2b3c4d") with { Opinion = "op1" },
            Chat("hp1", "trees/default/engine/s-c2c2c2c2") with { Serves = "c2" },
            Chat("p1", "trees/default/engine/p-p1a2b3c4") with { Serves = "c2", Piece = "p1a2b3c4" },
            new("a@one/c9", "engine", "completed") { Kind = "chat", Tree = "trees/default/engine/s-c9c9c9c9" },
            Chat("c1", "work/engine"),
            Chat("c2", "trees/default/engine/s-c2c2c2c2"),
            Chat("c3", "trees/default/engine/s-c3c3c3c3"),
            Chat("c4", "trees/default/engine/s-c4c4c4c4") with { TookQuest = "q4" },
            Chat("c5", "trees/default/engine/s-c4c4c4c4", "session:c4/engine") with { From = "c4", Created = T.AddMinutes(5) },
            Chat("g1", "trees/default/engine/q-q5", "quest:q5/engine") with { From = "s5" },
            Chat("g2", "trees/default/engine/q-q1", "ask:a1/engine") with { From = "s1" },
        ];
        var trees = new Trees(holds: tree => tree.StartsWith("trees/", StringComparison.Ordinal), stands: tree => !tree.Contains("s-c3", StringComparison.Ordinal));
        string[][] Ids(IReadOnlyList<IReadOnlyList<QuestView>> chains) => [.. chains.Select(chain => chain.Select(quest => quest.Id).ToArray())];
        (string? Code, string? Problem, string[][] Chains) Read(WorkflowRunAsk asked)
        {
            var read = WorkflowRunReader.ChainsOf(asked, quests, sessions, trees);
            return (read.Code, read.Problem, Ids(read.Chains));
        }

        void NoRun(string session, string code, string? id = null)
        {
            var (read, problem, chains) = Read(new(Session: session));
            Assert.Equal((code, WorkflowNoRun.Said(code, id)), (read, problem));
            Assert.Empty(chains);
        }

        NoRun("s9", WorkflowNoRun.NoSession, "s9");
        NoRun("a@one/c9", WorkflowNoRun.Teammate);
        NoRun("i2", WorkflowNoRun.AskNoQuest, "a2");
        NoRun("h1", WorkflowNoRun.AskDaoris);
        NoRun("r1", WorkflowNoRun.Reviewer);
        NoRun("p1", WorkflowNoRun.Partner);
        NoRun("hp1", WorkflowNoRun.Helper);
        NoRun("c3", WorkflowNoRun.ChatTreeGone);
        NoRun("c1", WorkflowNoRun.ChatCheckout);
        NoRun("c2", WorkflowNoRun.ChatOwnTree);
        Assert.Equal(((string?)WorkflowNoRun.NoQuest, (string?)"No quest `#q9` is here."), (Read(new(Quest: "q9")).Code, Read(new(Quest: "q9")).Problem));
        Assert.Equal(((string?)WorkflowNoRun.AskNoQuest, (string?)WorkflowNoRun.Said(WorkflowNoRun.AskNoQuest, "a2")), (Read(new(Ask: "a2")).Code, Read(new(Ask: "a2")).Problem));

        void Runs(string session, params string[][] chains)
        {
            var (code, problem, read) = Read(new(Session: session));
            Assert.Equal(((string?)null, (string?)null), (code, problem));
            Assert.Equal(chains, read);
        }

        Runs("i1", ["q1", "q2"]);
        Runs("c4", ["q4"]);
        Runs("c5", ["q4"]);
        Runs("g1", ["q5"]);
        Runs("g2", ["q1", "q2"]);
        Assert.Equal("game", WorkflowRunReader.ChainsOf(new(Session: "c5"), quests, sessions, trees).Only);
    }

    /// <summary>
    /// CHATFLOW1a corrected by Codex's read of the accepted text (the chat-flow design §4.1, finding 6): a record's run is the concrete
    /// work its line continues, never its origin read alone. A quest an ask sent, its worker gone on as a conversation, reads that quest's
    /// run and not every run of the ask; a quest that inherits its publisher's origin from another tree is its worker's continuation's
    /// work, never an own-tree chat; and a genuine intake keeps its ask-wide view.
    /// </summary>
    [Fact]
    public void A_continued_conversation_reads_the_concrete_work_its_line_continues_never_its_origin_alone()
    {
        QuestView To(string id, string from = "ask #a1") => new(id, from, "engine", id, "", "Done");
        IReadOnlyList<QuestView> quests = [To("q1"), To("q6"), To("q7", "daoris") with { PublishedBy = "pub" }];
        TracedSession Chat(string id, string tree, string entry, string from) =>
            new(id, "engine", "working") { Kind = "chat", Tree = tree, Entry = entry, From = from, Created = T.AddMinutes(5) };
        IReadOnlyList<TracedSession> sessions =
        [
            new("i1", "ask #a1", "completed") { Ask = "a1", Entry = "ask:a1/intake" },
            new("pub", "engine", "working") { Kind = "chat", Tree = "trees/default/engine/s-pub00000", Entry = "session:pub/engine" },
            Session("w1", "q1", "completed") with { Tree = "trees/default/engine/s-w1000000", Entry = "ask:a1/engine" },
            Session("w7", "q7", "completed") with { Tree = "trees/default/engine/s-w7000000", Entry = "session:pub/engine" },
            Chat("g1", "trees/default/engine/s-w1000000", "ask:a1/engine", "w1"),
            Chat("g7", "trees/default/engine/s-w7000000", "session:pub/engine", "w7"),
        ];
        var trees = new Trees(holds: tree => tree.StartsWith("trees/", StringComparison.Ordinal));
        string[][] Chains(string session) =>
            [.. WorkflowRunReader.ChainsOf(new(Session: session), quests, sessions, trees).Chains.Select(chain => chain.Select(quest => quest.Id).ToArray())];

        Assert.Equal([["q1"]], Chains("g1"));
        Assert.Equal([["q7"]], Chains("g7"));
        Assert.Equal([["q1"], ["q6"]], Chains("i1"));
        Assert.Equal(WorkflowNoRun.ChatOwnTree, WorkflowRunReader.ChainsOf(new(Session: "pub"), quests, sessions, trees).Code);
        Assert.Equal(["q1", "q7", null, null], new[] { "g1", "g7", "pub", "i1" }.Select(id => WorkflowRunReader.WorkOf(sessions.First(each => each.Id == id), sessions)));
    }

    /// <summary>
    /// Finding 6's run facts: a conversation that continues a quest's worker is one of that quest's work records, so the run reads its
    /// worker, its tree and its landing from it as from the worker, though its own record keeps <c>quest</c> null.
    /// </summary>
    [Fact]
    public async Task A_continued_conversation_contributes_its_worker_tree_and_landing_to_its_quests_run()
    {
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(new ContinuedService()));
        var sources = new WorkflowRunSources(service, Path.Combine(AppContext.BaseDirectory, "no-home-" + Guid.NewGuid().ToString("N")[..8]),
            DriverConfig.Empty, [], _ => "aurora");
        var asked = new List<string>();
        var records = new Trees(stands: tree => { asked.Add(tree); return true; }) { Accepted = { ["g1"] = T.AddMinutes(30) } };

        var run = Assert.Single((await WorkflowRunReader.ReadAsync(new WorkflowRunAsk(Session: "g1"), sources, records)).Runs);

        Assert.Equal(["q1"], run.Quests);
        Assert.Equal("g1", run.Session);
        Assert.Contains("trees/default/engine/s-w1000000", asked);
        var landing = run.Steps.Single(step => step.Step.Kind == WorkflowKinds.Landing);
        Assert.Equal((WorkflowRunStates.Done, WorkflowRunDetails.Merge, "g1"), (landing.State, landing.Detail, landing.Session));
        Assert.False(Directory.Exists(sources.Home));
    }

    /// <summary>The service a continued conversation's run reads: a quest an ask sent, done; its worker; and the conversation going on from it.</summary>
    private sealed class ContinuedService : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.PathAndQuery switch
            {
                "/api/quests?includeClosed=true" =>
                    """[{"id":"q1","from":"ask #a1","to":"engine","title":"Quest q1","body":"","status":"Done","updated":"2026-10-09T09:30:00Z"}]""",
                "/api/sessions?includeClosed=true" => """
                    [{"id":"w1","repository":"engine","state":"completed","quest":"q1","adapter":"claude-code","tree":"trees/default/engine/s-w1000000",
                      "entry":"ask:a1/engine","created":"2026-10-09T09:00:00Z","updated":"2026-10-09T09:20:00Z","evidence":"commits landed:\nabc1234 the change"},
                     {"id":"g1","repository":"engine","state":"completed","kind":"chat","from":"w1","adapter":"claude-code","tree":"trees/default/engine/s-w1000000",
                      "entry":"ask:a1/engine","created":"2026-10-09T09:40:00Z","updated":"2026-10-09T09:50:00Z","evidence":"commits landed:\ndef5678 the follow-up"}]
                    """,
                _ => "[]",
            };
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    /// <summary>Which trees this home opened and which still stand, for the reader's classification of a chat (CHATFLOW1a).</summary>
    private sealed class Trees(Func<string, bool>? holds = null, Func<string, bool>? stands = null) : IWorkflowRunRecords
    {
        public IReadOnlyList<LandedBranch> Landings() => [];

        public IReadOnlyList<AutoLanding> AutoLandings() => [];

        public Dictionary<string, DateTimeOffset> Accepted { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, DateTimeOffset> Acceptances(IReadOnlyList<TracedSession> sessions) => Accepted;

        public bool Stands(string tree) => stands?.Invoke(tree) ?? true;

        public bool Holds(string tree) => holds?.Invoke(tree) ?? false;

        public NodeRecord? Record(string unit) => null;

        public IReadOnlyList<GrownBranch> Branches() => [];
    }

    /// <summary>
    /// FABRIC-WORKFLOW2d's proof (§E5): a two-step chain bound to a closure graph finishes its work at its unheld, sealed submissions, so
    /// the landing is reached before any Done; the graph's close then writes both Done at once, and the run reads finished as before.
    /// </summary>
    [Fact]
    public void A_two_step_closure_chain_reaches_its_landing_before_Done_and_then_closes_at_once()
    {
        var current = WorkflowCurrent.Derive(DriverConfig.Parse("{}"), "engine", "aurora", Plugins);
        IReadOnlyList<TracedSession> sessions = [Session("s1", "q1", "completed"), Session("s2", "q2", "completed", 1)];
        var first = Bound("q1", 1);
        var second = Bound("q2", 2, last: true) with { Parent = "q1", GrewFrom = Made(1) };

        var submitted = WorkflowRuns.Derive(new WorkflowRunFacts("engine", current) { Quests = [first, second], Sessions = sessions }, "aurora");

        var work = submitted.Steps[0];
        Assert.Equal((WorkflowRunStates.Done, WorkflowRunDetails.Submitted), (work.State, work.Detail));
        Assert.Equal((2, 2, "q2"), (work.Count, work.Of, work.Quest));
        Assert.Equal(second.Submission!.At, work.At);
        Assert.Equal("landing", submitted.At);
        Assert.Equal("The agent finished: 2 quests here are submitted, and close at the workflow's end.", WorkflowRunWords.Said(work));

        // The close: both Done in one transaction, each keeping the submission it closed on.
        var closed = WorkflowRuns.Derive(new WorkflowRunFacts("engine", current)
        {
            Quests = [first with { Status = "Done" }, second with { Status = "Done" }], Sessions = sessions,
        }, "aurora");

        Assert.Equal((WorkflowRunStates.Done, WorkflowRunDetails.Finished), (closed.Steps[0].State, closed.Steps[0].Detail));
        Assert.Equal(2, closed.Steps[0].Count);
        Assert.Equal(submitted.Steps.Skip(1), closed.Steps.Skip(1));
        Assert.Equal("landing", closed.At);
    }

    /// <summary>
    /// FABRIC-WORKFLOW2d: the person's reopen (FABRIC-CLOSE1c) takes the seal back, so the run reads unfinished again and says why: its step
    /// withdrawn while open or held while taken, the quest taken with no submission. A step from before, which names no submission it grew
    /// from, seals whichever is current, as the service's seal reads it.
    /// </summary>
    [Fact]
    public void A_reopen_takes_the_completion_back_and_says_so()
    {
        var current = WorkflowCurrent.Derive(DriverConfig.Parse("{}"), "engine", "aurora", Plugins);
        IReadOnlyList<TracedSession> sessions = [Session("s1", "q1", "completed"), Session("s2", "q2", "completed", 1)];
        WorkflowRun Read(params QuestView[] quests) =>
            WorkflowRuns.Derive(new WorkflowRunFacts("engine", current) { Quests = quests, Sessions = sessions }, "aurora");
        var second = Bound("q2", 2, last: true) with { Parent = "q1", GrewFrom = Made(1) };

        Assert.Equal("landing", Read(Bound("q1", 1), second).At);

        foreach (var step in new[] { Withdrawn(Bound("q2", null, last: true) with { Status = "Open" }), Withdrawn(second) })
        {
            var reopened = Read(Bound("q1", null), step);

            var work = reopened.Steps[0];
            Assert.Equal((WorkflowRunStates.WaitingOnAgent, WorkflowRunDetails.Reopened), (work.State, work.Detail));
            Assert.Equal(("q1", 0), (work.Quest, work.Count));
            Assert.Equal("work", reopened.At);
            Assert.Equal(WorkflowRunStates.NotReached, reopened.Steps.Single(each => each.Step.Kind == WorkflowKinds.Landing).State);
            Assert.Equal("You reopened quest #q1's submission: it waits for its agent to make it ready again.", WorkflowRunWords.Said(work));
        }

        // A step from before names no submission: it seals the current one, as it always did.
        Assert.Equal("landing", Read(Bound("q1", 1), second with { GrewFrom = null }).At);
    }

    /// <summary>
    /// FABRIC-CLOSE1c7 (D173's WORKFLOW2d note, choice 2): a source made ready again while its started step stays held by the reopen waits
    /// on the person's go-on or abandon, said with the step's door at the new submission's moment, never as the source's <c>unclosed</c>.
    /// An untaken step is rebound by that submission instead, and a session at work on the source is said as it was.
    /// </summary>
    [Fact]
    public void A_source_made_ready_again_waits_on_the_persons_go_on_with_its_started_step()
    {
        var current = WorkflowCurrent.Derive(DriverConfig.Parse("{}"), "engine", "aurora", Plugins);
        IReadOnlyList<TracedSession> ended = [Session("s1", "q1", "completed"), Session("s2", "q2", "stood-down", 1), Session("s3", "q1", "completed", 2)];
        WorkflowRun Read(IReadOnlyList<TracedSession> sessions, params QuestView[] quests) =>
            WorkflowRuns.Derive(new WorkflowRunFacts("engine", current) { Quests = quests, Sessions = sessions }, "aurora");
        var ready = Bound("q1", 2);
        var held = Withdrawn(Bound("q2", 2, last: true));

        var run = Read(ended, ready, held);

        var work = run.Steps[0];
        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.GoOn), (work.State, work.Detail));
        Assert.Equal(("q2", 0, ready.Submission!.At), (work.Quest, work.Count, work.At));
        Assert.Equal("work", run.At);
        Assert.Equal(
            "The work you reopened is ready again: quest #q2, its next step, waits for you to go on with it on the new work or abandon it.",
            WorkflowRunWords.Said(work));

        // The same step in another repository, read through the whole chain the reader hands in.
        var there = held with { To = "game" };
        var whole = WorkflowRuns.Derive(new WorkflowRunFacts("engine", current) { Quests = [ready], Chain = [ready, there], Sessions = ended }, "aurora");
        Assert.Equal((WorkflowRunDetails.GoOn, "q2"), (whole.Steps[0].Detail, whole.Steps[0].Quest));

        // A session at work on the source is said as it was; a held source is the person's yes before their go-on.
        Assert.Equal(WorkflowRunDetails.Agent, Read([.. ended.SkipLast(1), Session("s3", "q1", "working", 2)], ready, held).Steps[0].Detail);
        Assert.Equal(WorkflowRunDetails.Held, Read(ended, ready with { Held = true }, held).Steps[0].Detail);
    }

    /// <summary>
    /// FABRIC-WORKFLOW2d: a quest no closure graph holds reads exactly as it did, whatever a host answers of a submission or a step's source:
    /// only its Done finishes it. The owner's schema-1 runs hold none, and the table's every other row stands as it did.
    /// </summary>
    [Fact]
    public void An_unbound_run_reads_exactly_as_before()
    {
        var current = WorkflowCurrent.Derive(DriverConfig.Parse("{}"), "engine", "aurora", Plugins);
        IReadOnlyList<TracedSession> sessions = [Session("s1", "q1", "completed"), Session("s2", "q2", "completed", 1)];
        IReadOnlyList<QuestView> carried =
        [
            Bound("q1", 1) with { Closure = null },
            Bound("q2", 2, last: true) with { Closure = null, Parent = "q1", GrewFrom = Made(1) },
        ];
        IReadOnlyList<QuestView> plain = [.. carried.Select(quest => quest with { Submission = null, GrewFrom = null, SourceReopened = null })];

        var run = WorkflowRuns.Derive(new WorkflowRunFacts("engine", current) { Quests = carried, Sessions = sessions }, "aurora");
        var before = WorkflowRuns.Derive(new WorkflowRunFacts("engine", current) { Quests = plain, Sessions = sessions }, "aurora");

        Assert.Equal(before.Steps, run.Steps);
        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Unclosed), (run.Steps[0].State, run.Steps[0].Detail));
        Assert.Equal("work", run.At);

        // A reopened step under an unbound quest says nothing of a reopen: only a closure graph's quest has a submission to reopen.
        var under = WorkflowRuns.Derive(new WorkflowRunFacts("engine", current)
        {
            Quests = [carried[0] with { Submission = null }, Withdrawn(carried[1])], Sessions = sessions,
        }, "aurora");
        Assert.Equal(WorkflowRunDetails.Unclosed, under.Steps[0].Detail);
    }

    /// <summary>
    /// FABRIC-WORKFLOW2d: a submission's step another repository holds seals it through the whole chain, which the reader hands in; a run
    /// handed only this repository's quests judges by those, and finds no step to seal it.
    /// </summary>
    [Fact]
    public void A_step_in_another_repository_seals_through_the_whole_chain()
    {
        var current = WorkflowCurrent.Derive(DriverConfig.Parse("{}"), "engine", "aurora", Plugins);
        var here = Bound("q1", 1) with { Then = [new QuestStepView("game", "Next", "")] };
        var there = new QuestView("q2", "ask #a1", "game", "Quest q2", "", "Open") { Parent = "q1", GrewFrom = Made(1), Updated = T };
        IReadOnlyList<TracedSession> sessions = [Session("s1", "q1", "completed")];

        var whole = WorkflowRuns.Derive(new WorkflowRunFacts("engine", current) { Quests = [here], Chain = [here, there], Sessions = sessions }, "aurora");
        var alone = WorkflowRuns.Derive(new WorkflowRunFacts("engine", current) { Quests = [here], Sessions = sessions }, "aurora");

        Assert.Equal((WorkflowRunStates.Done, WorkflowRunDetails.Submitted), (whole.Steps[0].State, whole.Steps[0].Detail));
        Assert.Equal("landing", whole.At);
        Assert.Equal("work", alone.At);
    }

    /// <summary>
    /// FABRIC-WORKFLOW2e: a quest's run and a session's run, read as the reader reads them (the whole chain, the quest's own repository
    /// as the run), see a step in another repository: a sealed submission reads Done/Submitted, a reopened one WaitingOnAgent/Reopened.
    /// </summary>
    [Fact]
    public void A_quests_and_a_sessions_run_see_a_step_in_another_repository()
    {
        var current = WorkflowCurrent.Derive(DriverConfig.Parse("{}"), "engine", "aurora", Plugins);
        QuestView Here(bool reopened) => Bound("q1", reopened ? null : 1) with { Then = [new QuestStepView("game", "Next", "")] };
        QuestView There(bool reopened) => new QuestView("q2", "ask #a1", "game", "Quest q2", "", "Open") { Parent = "q1", GrewFrom = Made(1), Updated = T }
            with { SourceReopened = reopened ? Made(1) : null, Held = reopened, Hold = reopened ? "source-reopened" : null };
        IReadOnlyList<TracedSession> sessions = [Session("s1", "q1", "completed")];

        WorkflowRun Read(WorkflowRunAsk asked, bool reopened)
        {
            var (chains, _, only, _, _) =WorkflowRunReader.ChainsOf(asked, [Here(reopened), There(reopened)], sessions, new Trees());
            var chain = chains.Single();
            Assert.Equal("engine", only);
            return WorkflowRuns.Derive(new WorkflowRunFacts("engine", current)
            {
                Quests = [.. chain.Where(quest => quest.To == only)], Chain = chain, Sessions = sessions,
            }, "aurora");
        }

        foreach (var asked in new[] { new WorkflowRunAsk(Quest: "q1"), new WorkflowRunAsk(Session: "s1") })
        {
            var sealedRun = Read(asked, false);
            Assert.Equal((WorkflowRunStates.Done, WorkflowRunDetails.Submitted), (sealedRun.Steps[0].State, sealedRun.Steps[0].Detail));
            Assert.Equal("landing", sealedRun.At);

            var reopened = Read(asked, true);
            Assert.Equal((WorkflowRunStates.WaitingOnAgent, WorkflowRunDetails.Reopened), (reopened.Steps[0].State, reopened.Steps[0].Detail));
            Assert.Equal("work", reopened.At);
        }

        // No closure graph (every run on the owner's install): the whole chain changes nothing; only a Done finishes it.
        var unbound = Here(false) with { Closure = null, Submission = null };
        var plain = WorkflowRuns.Derive(new WorkflowRunFacts("engine", current)
        {
            Quests = [unbound], Chain = [unbound, There(false) with { GrewFrom = null }], Sessions = sessions,
        }, "aurora");
        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Unclosed), (plain.Steps[0].State, plain.Steps[0].Detail));
        Assert.Equal("work", plain.At);
    }

    /// <summary>
    /// FABRIC-WORKFLOW2e: the whole chain moves a quest run's review level in the two reads of <see cref="ReviewGate.Decide"/> that look past
    /// the run's repository, as every landing door's whole-chain read already does; rows 1 and 2 stay this repository's.
    /// </summary>
    [Fact]
    public void The_whole_chain_moves_a_quest_runs_review_level_through_two_rows_only()
    {
        QuestView In(string id, string to, string? parent = null) => new(id, "ask #a1", to, id, "", "Open") { Parent = parent };
        var cut = new[] { In("q2", "game", "q1") };

        // Row 3: the chain's review choice, set on a quest in another repository.
        var chosen = In("q1", "engine") with { Review = new QuestReviewChoiceView("off") };
        var whole = new[] { chosen, cut[0] };
        Assert.Equal(ReviewLevels.Nothing, ReviewGate.Decide(cut, "game", null, null).Level);
        Assert.Equal(ReviewLevels.Chain, ReviewGate.Decide(whole, "game", null, null).Level);

        // The set-up step another repository's open quest composes for this one.
        var composing = In("q1", "engine") with { Then = [new QuestStepView("game", "Next", "") { SetUpIn = "local" }] };
        Assert.Equal(ReviewLevels.SetUpStep, ReviewGate.Decide(new[] { composing, cut[0] }, "game", null, null).Level);

        // Rows 1 and 2 read only this repository's part: a skip or a published set-up step in another repository moves nothing.
        var skipped = In("q1", "engine") with { Verdicts = [new QuestReviewVerdictView(ReviewVerdicts.Skipped)] };
        var step = In("q3", "engine", "q2") with { SetUpIn = "local" };
        Assert.Equal(ReviewLevels.Nothing, ReviewGate.Decide(new[] { skipped, cut[0] }, "game", null, null).Level);
        Assert.Equal(ReviewLevels.Nothing, ReviewGate.Decide(new[] { cut[0], step }, "game", null, null).Level);
    }

    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private static QuestView Quest(string id, string status) => new(id, "ask #a1", "engine", $"Quest {id}", "", status) { Updated = T };

    /// <summary>The closure graph the chain's quests are bound to (FABRIC-CLOSE1a).</summary>
    private const string Graph = "gph-0123";

    /// <summary>A submission made on this machine at <paramref name="sequence"/>, as a step names the one it grew from.</summary>
    private static QuestSubmissionRef Made(long sequence) => new("desk", sequence);

    /// <summary>
    /// A quest a closure graph holds (§E5): taken, its current submission made at <paramref name="sequence"/> where one is, and a step of
    /// its chain still to publish unless it is the chain's <paramref name="last"/>.
    /// </summary>
    private static QuestView Bound(string id, long? sequence, bool last = false) => Quest(id, "Taken") with
    {
        Closure = Graph,
        Submission = sequence is { } made ? new QuestSubmissionView("desk", made, T.AddMinutes(made), "ready") : null,
        Then = last ? [] : [new QuestStepView("engine", "Next", "")],
    };

    /// <summary>The step <c>q1</c>'s first submission published, that submission reopened by the person (FABRIC-CLOSE1c): withdrawn or held.</summary>
    private static QuestView Withdrawn(QuestView step) => step with
    {
        Parent = "q1", GrewFrom = Made(1), SourceReopened = Made(1), Held = true, Hold = "source-reopened",
    };

    private static QuestView SetUpStep(string id, string status) => Quest(id, status) with { SetUpIn = "local" };

    /// <summary>A session on a quest, its evidence naming one commit; <paramref name="later"/> orders it after the ones before.</summary>
    private static TracedSession Session(string id, string quest, string state, int later = 0) => new(id, "engine", state)
    {
        Quest = quest,
        Adapter = "claude-code",
        Created = T.AddMinutes(later * 10),
        Updated = T.AddMinutes(later * 10 + 5),
        Evidence = "commits landed:\nabc1234 the change",
    };

    private static LandedBranch Landed(string session) =>
        new("engine", "aurora", "work/q1", "main", Commit, session, "q1", "Quest q1", T)
        {
            Plugin = "example.pull-request", Pushed = true, PullRequest = "https://example.test/pull/1",
        };

    /// <summary>
    /// The gate in a state, a level having asked for a look in <c>local</c>: the set-up step's row where one is published, else the
    /// level named, the repository's rule by default.
    /// </summary>
    private static ReviewGateState Gate(string state, QuestView? step = null, string? level = null) =>
        new(state, new ReviewDecision(level ?? (step is null ? ReviewLevels.Repository : ReviewLevels.SetUpStep), "local")
        {
            Repository = "engine", SetUpStep = step,
        });

    private static OpinionView Opinion(string state) =>
        new("op1", "landing", OpinionViews.FirstPass, "s1", "r1", "engine", "base0", Commit, "codex-acp", "another maker's agent", state)
        {
            Product = "Codex",
            Maker = "OpenAI",
        };

    private static OpinionFindingView Finding(int number) => new(number, "should", "src/app.ts:4", "a claim", "", "", "sure");

    /// <summary>
    /// The second opinion's gate in a state for session <c>s1</c>'s work, as <see cref="OpinionGate.JudgeAsync"/> answers it: the
    /// repository's rule reading before landing, required where said, and Codex's pass where one is read.
    /// </summary>
    private static OpinionGateState OpinionAt(string state, bool required = false) => new(state, "engine")
    {
        Rule = new ResolvedOpinion(new OpinionRule(["codex-acp"], [OpinionRules.Landing], Required: required), OpinionSource.Repository),
        Session = "s1",
        Tip = Commit,
        Opinion = state is OpinionGateStates.None or OpinionGateStates.WaitsChain or OpinionGateStates.NotAsked or OpinionGateStates.Unread
            ? null
            : Opinion(state == OpinionGateStates.Reading ? OpinionViews.Reading : OpinionViews.GivenState) with
            {
                Findings = state == OpinionGateStates.Reading ? null : [Finding(1), Finding(2), Finding(3)],
                HandedTo = "s1",
            },
    };
}
