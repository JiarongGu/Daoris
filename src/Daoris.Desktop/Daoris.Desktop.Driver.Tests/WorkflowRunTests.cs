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
    /// Whose runs are read: a quest's chain in its own repository, the chain's other repositories being their own runs; a session's
    /// through its quest, an intake's through its ask; an ask's, each chain it asked. A chat serves no quest and has none.
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

        Assert.Equal([["q1", "q2"]], Ids(WorkflowRunReader.ChainsOf(new(Quest: "#q2"), quests, sessions).Chains));
        Assert.Equal([["q3"]], Ids(WorkflowRunReader.ChainsOf(new(Quest: "q3"), quests, sessions).Chains));
        Assert.Equal([["q1", "q2"]], Ids(WorkflowRunReader.ChainsOf(new(Session: "s1"), quests, sessions).Chains));
        Assert.Equal([["q1", "q2", "q3"], ["q4"]], Ids(WorkflowRunReader.ChainsOf(new(Session: "i1"), quests, sessions).Chains));
        Assert.Equal([["q1", "q2", "q3"], ["q4"]], Ids(WorkflowRunReader.ChainsOf(new(Ask: "a1"), quests, sessions).Chains));

        Assert.Equal("session `c1` serves no quest, so no workflow runs for it.", WorkflowRunReader.ChainsOf(new(Session: "c1"), quests, sessions).Problem);
        Assert.Equal("no quest `#q9` is here.", WorkflowRunReader.ChainsOf(new(Quest: "q9"), quests, sessions).Problem);
        Assert.Equal("no session `s9` is recorded here.", WorkflowRunReader.ChainsOf(new(Session: "s9"), quests, sessions).Problem);
        Assert.Contains("name one of them", WorkflowRunReader.ChainsOf(new(), quests, sessions).Problem);
    }

    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private static QuestView Quest(string id, string status) => new(id, "ask #a1", "engine", $"Quest {id}", "", status) { Updated = T };

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
