using System.Reflection;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-WORKFLOW2b (the fabric events design §E7, canary 38; D157 §2.4, §5.2): a bound D157 run's step states read as its decoded
/// graph's statuses, and both readings compared over recorded runs. Each row is a run's records, as <c>WorkflowRunTests</c> keeps them,
/// with where schema 1 stands, the decoded nodes' states, the graph's eligible and unsettled nodes, and every difference, all written by
/// hand: the projection is never its own expectation. Schema 1 is unchanged; the projection reads and writes nothing.
/// </summary>
public sealed class WorkflowRunNodesTests : IDisposable
{
    private const string Repo = "engine";
    private const string Space = "aurora";
    private const string RunId = "q1";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private static readonly DateTimeOffset T = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The rules the run reads beside its version: an opinion's reviewers and a review environment, as an install declares them.</summary>
    private const string Rules =
        """{"opinions":{"engine":{"reviewers":["codex-acp"]}},"reviews":{"engine":{"required":true,"environments":[{"name":"local","kind":"local","procedure":"README.md","address":"http://localhost:4200"}]}}}""";

    private static readonly IReadOnlyList<WorkflowPlugin> Plugins = [new("example.pull-request", [HookPoints.Land, HookPoints.State])];

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-workflow-run-nodes-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ---- the versions and the records

    /// <summary>A D157 step with its kind's defaults, as a stored version holds it, the fields named overriding them.</summary>
    private static NamedStep Step(string id, string kind, params (string Name, object? Value)[] fields) =>
        new(id, kind, [.. WorkflowKindTable.Of(kind)!.Fields.Select(field => new WorkflowSetting(field.Name, fields.FirstOrDefault(each => each.Name == field.Name) is { Name: not null } named ? named.Value : field.Default))]);

    private static readonly NamedStep Work = Step("work", WorkflowKinds.Work);
    private static readonly NamedStep Merge = Step("land", WorkflowKinds.Landing, ("form", LandingForm.Merge), ("accept", "you"));
    private static readonly NamedStep Branch = Step("land", WorkflowKinds.Landing, ("form", LandingForm.Branch), ("accept", "you"), ("pattern", "work/{quest}"), ("plugin", "example.pull-request"));
    private static readonly NamedStep See = Step("see", WorkflowKinds.Look, ("environment", "local"));
    private static readonly NamedStep Second = Step("second", WorkflowKinds.Opinion);
    private static NamedStep Permit(string refused) => Step("permit", WorkflowKinds.GoAhead, ("act", "publish"), ("on", "local"), ("refused", refused));
    private static readonly NamedStep Pr = Step("pr", WorkflowKinds.PullRequest);

    private static readonly NamedStep[] Basic = [Work, Merge];
    private static readonly NamedStep[] Looked = [Work, See, Merge];
    private static readonly NamedStep[] Opined = [Work, Second, See, Merge];
    private static readonly NamedStep[] Pulled = [Work, Branch, Pr];

    private static QuestView Quest(string id, string status) => new(id, "ask #a1", Repo, $"Quest {id}", "", status) { Updated = T };

    private static TracedSession Session(string id, string quest, string state) => new(id, Repo, state)
    {
        Quest = quest, Adapter = "claude-code", Created = T, Updated = T.AddMinutes(5), Evidence = "commits landed:\nabc1234 the change",
    };

    /// <summary>The work done: its one quest closed and its session completed with a commit to land.</summary>
    private static WorkflowRunFacts Done(WorkflowRunFacts facts) =>
        facts with { Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed")] };

    private static WorkflowRunFacts Accepted(WorkflowRunFacts facts) => Done(facts) with { Accepted = new Dictionary<string, DateTimeOffset> { ["s1"] = T } };

    private static LandedBranch Landed(string session) =>
        new(Repo, Space, "work/q1", "main", Commit, session, "q1", "Quest q1", T) { Plugin = "example.pull-request", Pushed = true, PullRequest = "https://example.test/pull/1" };

    private static ReviewGateState Review(string state, string level = ReviewLevels.Repository) =>
        new(state, new ReviewDecision(level, "local") { Repository = Repo });

    private static readonly ReviewGateState Skipped =
        new(ReviewStates.Skipped, new ReviewDecision(ReviewLevels.Skip, null) { Repository = Repo, SkippedOn = "q1", Words = "a typo fix" });

    private static readonly ReviewGateState Off =
        new(ReviewStates.None, new ReviewDecision(ReviewLevels.Chain, null) { Repository = Repo, Choice = "off", Words = "docs only" });

    private static OpinionGateState Opinion(string state, bool required = false) => new(state, Repo)
    {
        Rule = new ResolvedOpinion(new OpinionRule(["codex-acp"], [OpinionRules.Landing], Required: required), OpinionSource.Repository),
        Session = "s1",
        Tip = Commit,
    };

    /// <summary>A named run bound to <paramref name="version"/>, read by schema 1 from <paramref name="records"/> as the run reader does.</summary>
    private static WorkflowRun Run(IReadOnlyList<NamedStep> version, Func<WorkflowRunFacts, WorkflowRunFacts> records)
    {
        var config = DriverConfig.Parse(Rules);
        var binding = new WorkflowRunBinding(RunId, Repo, Space, "careful", WorkflowLevels.Repository, T) { Version = 1, Digest = WorkflowNamed.Digest(version) };
        var process = WorkflowProcess.Of(config, Repo, Space, binding, version) with { Run = RunId };
        var graph = WorkflowRunGraph.Read(process, WorkflowCurrent.Derive(config, Repo, Space, Plugins), Plugins);
        return WorkflowRuns.Derive(records(new WorkflowRunFacts(Repo, graph) { Process = process }), Space);
    }

    // ---- the recorded runs

    /// <summary>
    /// One recorded run: its version and records; where schema 1 stands; the decoded graph's eligible and unsettled nodes, in its order; each
    /// node's state after the run's steps are read onto it; and the differences found, each by name and the step it rests on.
    /// </summary>
    private sealed record Recorded(
        IReadOnlyList<NamedStep> Version, Func<WorkflowRunFacts, WorkflowRunFacts> Records, string? At, string[] Frontier,
        (string Node, string State)[] Nodes, (string Code, string Step)[] Differences);

    private const string Blocked = NodeStates.Blocked, Ready = NodeStates.Ready, Active = NodeStates.Active, Reached = NodeStates.Reached;

    private static readonly Dictionary<string, Recorded> Runs = new()
    {
        // The work: a claim is pending until its done is in, whatever the session did.
        ["an open quest no session works on yet"] = new(Basic, facts => facts with { Quests = [Quest("q1", "Open")] },
            "work", ["work"], [("work", Active), ("land", Blocked)], []),
        ["a done held for the person's yes is still claimed"] = new(Basic, facts => facts with
        {
            Quests = [Quest("q1", "Done") with { Held = true, Hold = "evidence-missing" }], Sessions = [Session("s1", "q1", "completed")],
        }, "work", ["work"], [("work", Active), ("land", Blocked)], []),
        ["a failed session leaves the claim pending, since nothing fails a claim"] = new(Basic, facts => facts with
        {
            Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "failed")],
        }, "work", ["work"], [("work", Active), ("land", Blocked)], []),
        ["a declined quest stops the work"] = new(Basic, facts => facts with { Quests = [Quest("q1", "Declined")] },
            "work", ["work"], [("work", NodeStates.Stopped), ("land", Blocked)], []),

        // The landing.
        ["the landing waits for the person's accept"] = new(Basic, Done, "land", ["land"], [("work", Reached), ("land", Active)], []),
        ["landed into the line: every step settled"] = new(Basic, Accepted, null, [], [("work", Reached), ("land", Reached)], []),
        ["nothing to land: the landing done with nothing"] = new(Basic, facts => facts with
        {
            Quests = [Quest("q1", "Done")], Sessions = [Session("s1", "q1", "completed") with { Evidence = "no commits landed" }],
        }, null, [], [("work", Reached), ("land", Reached)], []),

        // The look.
        ["a look shown waits on the person and holds the landing"] = new(Looked, facts => Done(facts) with { Review = Review(ReviewStates.Shown) },
            "see", ["see"], [("work", Reached), ("see", Active), ("land", Blocked)], []),
        ["the person skipped the look in their words"] = new(Looked, facts => Done(facts) with { Review = Skipped },
            "land", ["land"], [("work", Reached), ("see", NodeStates.Skipped), ("land", Active)], []),
        ["a look's skip withdrawn reads as not shown again"] = new(Looked, facts => Done(facts) with { Review = Review(ReviewStates.NotShown) },
            "see", ["see"], [("work", Reached), ("see", Active), ("land", Blocked)], []),
        ["the person's not yet rejects the look's attempt"] = new(Looked, facts => Done(facts) with
        {
            Review = Review(ReviewStates.NotYet) with { Verdict = new QuestReviewVerdictView(ReviewVerdicts.NotYet) { Words = "the button is off" } },
        }, "see", ["see"], [("work", Reached), ("see", NodeStates.Rejected), ("land", Blocked)], []),
        ["the chain's choice turns the look off, which no node's authority passes over"] = new(Looked, facts => Done(facts) with { Review = Off },
            "land", ["see"], [("work", Reached), ("see", Active), ("land", Blocked)], [(WorkflowRunDifferences.PassedByRule, "see")]),
        ["a look the chain's choice added for this work is no node of the version"] = new(Basic, facts => Done(facts) with
        {
            Review = Review(ReviewStates.NotShown, ReviewLevels.Chain),
        }, "look", ["land"], [("work", Reached), ("land", Ready)], [(WorkflowRunDifferences.Added, "look")]),

        // The second opinion.
        ["an agent at the opinion holds the look"] = new(Opined, facts => Done(facts) with
        {
            Review = Review(ReviewStates.NotShown), Opinion = Opinion(OpinionGateStates.Reading),
        }, "second", ["second"], [("work", Reached), ("second", Active), ("see", Blocked), ("land", Blocked)], []),
        ["a settled opinion lets the look go"] = new(Opined, facts => Done(facts) with
        {
            Review = Review(ReviewStates.NotShown), Opinion = Opinion(OpinionGateStates.Settled),
        }, "see", ["see"], [("second", Reached), ("see", Active), ("land", Blocked)], []),
        ["no opinion asked here: its gate passes it over"] = new(Opined, facts => Done(facts) with
        {
            Review = Review(ReviewStates.NotShown), Opinion = Opinion(OpinionGateStates.None),
        }, "see", ["see"], [("second", NodeStates.Skipped), ("see", Active), ("land", Blocked)], []),
        ["an optional opinion none could give: passed over"] = new(Opined, facts => Done(facts) with
        {
            Review = Review(ReviewStates.NotShown), Opinion = Opinion(OpinionGateStates.Unavailable),
        }, "see", ["see"], [("second", NodeStates.Skipped), ("see", Active), ("land", Blocked)], []),
        ["a required opinion none could give waits, and schema 1's look goes on beside it"] = new(Opined, facts => Done(facts) with
        {
            Review = Review(ReviewStates.NotShown), Opinion = Opinion(OpinionGateStates.Unavailable, required: true),
        }, "second", ["second"], [("second", Active), ("see", Blocked), ("land", Blocked)], [(WorkflowRunDifferences.LookBesideOpinion, "second")]),
        ["a dispute: schema 1's look goes on beside it, and the decoded order holds it"] = new(Opined, facts => Done(facts) with
        {
            Review = Review(ReviewStates.NotShown), Opinion = Opinion(OpinionGateStates.Disputed) with { Disputes = new OpinionDisputes([1], []) },
        }, "second", ["second"], [("second", Active), ("see", Blocked), ("land", Blocked)], [(WorkflowRunDifferences.LookBesideOpinion, "second")]),
        ["an opinion whose gate was not read is unknown, and the look goes on beside it"] = new(Opined, facts => Done(facts) with
        {
            Review = Review(ReviewStates.NotShown),
        }, "second", ["second"], [("second", NodeStates.Unknown), ("see", Blocked), ("land", Blocked)], [(WorkflowRunDifferences.LookBesideOpinion, "second")]),
        ["a look skipped while an agent reads the opinion: the decoded landing goes past it"] = new(Opined, facts => Done(facts) with
        {
            Review = Skipped, Opinion = Opinion(OpinionGateStates.Reading),
        }, "second", ["second", "land"], [("second", Active), ("see", NodeStates.Skipped), ("land", Ready)], [(WorkflowRunDifferences.SkipPassesEarlier, "see")]),
        ["a historical landing decides, whatever its gates say now"] = new(Opined, facts => Accepted(facts) with
        {
            Review = Review(ReviewStates.NotShown), Opinion = Opinion(OpinionGateStates.Disputed),
        }, null, [], [("work", Reached), ("second", Reached), ("see", Reached), ("land", Reached)], []),
        ["a historical landing whose opinion none could give"] = new(Opined, facts => Done(facts) with
        {
            Landings = [Landed("s1") with { Opinion = new LandingOpinion(OpinionGateStates.Unavailable) { Code = ReviewerUnavailable.NoReviewer } }],
            Review = Review(ReviewStates.NotShown), Opinion = Opinion(OpinionGateStates.Disputed),
        }, null, [], [("work", Reached), ("second", NodeStates.Skipped), ("see", Reached), ("land", Reached)], []),

        // A partial kind: a go-ahead this build cannot start, with each refusal's path.
        ["the work at a go-ahead's turn"] = new([Work, Permit("stop"), Merge], facts => facts with
        {
            Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "working")],
        }, "work", ["work"], [("work", Active), ("permit", Blocked), ("permit:rejected", Blocked), ("land", Blocked)], []),
        ["a go-ahead cannot start, and schema 1's landing goes on past it"] = new([Work, Permit("stop"), Merge], Done,
            "permit", ["permit"], [("work", Reached), ("permit", Ready), ("permit:rejected", Blocked), ("land", Blocked)],
            [(WorkflowRunDifferences.CannotStartHoldsNothing, "permit")]),
        ["a go-ahead refused to the person's task, and schema 1 landed past it"] = new([Work, Permit("wait"), Merge], Accepted,
            "permit", ["permit"], [("work", Reached), ("permit", Ready), ("permit:rejected", Blocked), ("land", Blocked)],
            [(WorkflowRunDifferences.CannotStartHoldsNothing, "permit")]),

        // The pull request.
        ["a pull request open waits on the person"] = new(Pulled, facts => Done(facts) with
        {
            Landings = [Landed("s1") with { PullRequestState = new PullRequestState(PullRequestStates.Open) { AskedAt = T } }],
        }, "pr", ["pr"], [("work", Reached), ("land", Reached), ("pr", Active)], []),
        ["a pull request merged: every step settled"] = new(Pulled, facts => Done(facts) with
        {
            Landings = [Landed("s1") with { PullRequestState = new PullRequestState(PullRequestStates.Completed) { AskedAt = T, At = T } }],
        }, null, [], [("work", Reached), ("land", Reached), ("pr", Reached)], []),
        ["a pull request abandoned fails on its plugin's answer"] = new(Pulled, facts => Done(facts) with
        {
            Landings = [Landed("s1") with { PullRequestState = new PullRequestState(PullRequestStates.Abandoned) { AskedAt = T, At = T } }],
        }, "pr", ["pr"], [("land", Reached), ("pr", NodeStates.Failed)], []),
        ["a pull request whose last ask failed is unknown"] = new(Pulled, facts => Done(facts) with
        {
            Landings = [Landed("s1") with
            {
                PullRequestState = new PullRequestState(PullRequestStates.Open) { AskedAt = T },
                PullRequestAskFailed = new PullRequestAskFailed("timeout", "example.pull-request", T.AddMinutes(1)),
            }],
        }, "pr", ["pr"], [("land", Reached), ("pr", NodeStates.Unknown)], []),
        ["landed into the line: no pull request opens, which no node's authority passes over"] = new(Pulled, Accepted,
            null, ["pr"], [("work", Reached), ("land", Reached), ("pr", Active)], [(WorkflowRunDifferences.PassedByRule, "pr")]),
    };

    public static TheoryData<string> RunNames() => [.. Runs.Keys];

    private static (WorkflowRun Run, WorkflowRunProjection Projection) Projected(Recorded recorded)
    {
        var run = Run(recorded.Version, recorded.Records);
        return (run, WorkflowRunNodes.Project(run, WorkflowGraphs.DecodeSteps(1, recorded.Version), RunId));
    }

    /// <summary>
    /// Canary 38 over each recorded run: schema 1 stands where the row says; its steps read onto the decoded nodes give the states the row
    /// says; the graph's eligible and unsettled nodes are the row's; and the differences are the row's, by name, and none other. Every step
    /// schema 1 said is kept whole beside its node: its state, its detail and every fact its words and its door are made of.
    /// </summary>
    [Theory]
    [MemberData(nameof(RunNames))]
    public void Each_recorded_run_reads_on_its_decoded_graph_as_written(string name)
    {
        var recorded = Runs[name];
        var (run, projection) = Projected(recorded);

        Assert.True(recorded.At == run.At, $"{name}: schema 1 stands at {run.At ?? "nothing"}, not {recorded.At ?? "nothing"}");
        Assert.True(projection.Readable, $"{name}: {projection.Version.Unreadable}");
        Assert.Equal(run.Steps, projection.Steps.Select(row => row.Step));
        foreach (var (node, state) in recorded.Nodes)
        {
            Assert.True(projection.Statuses.TryGetValue(node, out var status), $"{name}: no node {node}");
            Assert.True(state == status!.State, $"{name}: {node} is {status.State}, not {state}");
        }

        Assert.True(recorded.Frontier.SequenceEqual(projection.Frontier), $"{name}: the graph offers [{string.Join(", ", projection.Frontier)}], not [{string.Join(", ", recorded.Frontier)}]");
        Assert.True(recorded.Differences.SequenceEqual(projection.Differences.Select(each => (each.Code, each.Step))),
            $"{name}: the differences are [{string.Join(", ", projection.Differences.Select(each => $"{each.Code} at {each.Step}"))}]");
    }

    /// <summary>
    /// The row's proof, over every recorded run: the graph's eligible nodes equal schema 1's <c>At</c> (the one step it stands at, or none
    /// where all settled), or a named difference says why; and none is unexplained. Eligibility is named as the comparison reads it: a node
    /// whose wires hold and which is not settled, so neither a completed node (raw eligibility) nor a started one (which no start offers) is
    /// left out by accident.
    /// </summary>
    [Fact]
    public void Every_recorded_runs_eligible_nodes_equal_its_At_or_a_named_difference_says_why()
    {
        foreach (var (name, recorded) in Runs)
        {
            var (run, projection) = Projected(recorded);
            Assert.DoesNotContain(projection.Differences, each => each.Code == WorkflowRunDifferences.Unexplained);
            var equal = projection.Frontier.SequenceEqual(run.At is null ? [] : [run.At]);
            Assert.True(equal || projection.Differences.Count > 0, $"{name}: the graph offers [{string.Join(", ", projection.Frontier)}] where schema 1 stands at {run.At}, and no difference is named");
            Assert.Equal((name, equal && projection.Differences.Count == 0), (name, projection.Agrees));
        }
    }

    /// <summary>
    /// The mismatch, named and kept (the parent's decision; D173's FABRIC-WIRE1g note): schema 1 lets the look go on beside a disputed
    /// opinion, whose Reviewed answers it, and is unchanged; the decoded order holds the look until the opinion is reached. The comparison
    /// says so by name and does not agree, so the cutover settles it openly.
    /// </summary>
    [Fact]
    public void Known_difference_a_disputed_opinion_holds_the_decoded_look_that_schema_1_lets_go_on()
    {
        var (run, projection) = Projected(Runs["a dispute: schema 1's look goes on beside it, and the decoded order holds it"]);

        var look = projection.Steps.Single(row => row.Step.Step.Id == "see");
        Assert.Equal((WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.NotShown), (look.Step.State, look.Step.Detail));
        Assert.Equal((NodeStates.Blocked, false), (look.Status!.State, look.Offered));
        Assert.Equal(("second", "second"), (run.At, projection.At));
        Assert.Equal([new WorkflowRunDifference(WorkflowRunDifferences.LookBesideOpinion, "second")], projection.Differences);
        Assert.False(projection.Agrees);
    }

    /// <summary>
    /// None silently: a reading no name explains (here a landing schema 1 says done while its work still runs, which no recorded run
    /// holds) is said as unexplained at the step it is found, and the comparison does not agree.
    /// </summary>
    [Fact]
    public void A_difference_no_name_explains_is_said_as_unexplained()
    {
        var run = Run(Basic, facts => facts with { Quests = [Quest("q1", "Taken")], Sessions = [Session("s1", "q1", "working")] });
        var landing = run.Steps.Single(step => step.Step.Id == "land");
        var contradicted = run with { Steps = [run.Steps[0], landing with { State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.Merge }] };

        var projection = WorkflowRunNodes.Project(contradicted, WorkflowGraphs.DecodeSteps(1, Basic), RunId);

        Assert.Equal([new WorkflowRunDifference(WorkflowRunDifferences.Unexplained, "land")], projection.Differences);
        Assert.Equal(["work"], projection.Frontier);
        Assert.Equal(NodeStates.Blocked, projection.Statuses["land"].State);
        Assert.False(projection.Agrees);
    }

    /// <summary>
    /// The nodes decoding made are counted apart: a refused go-ahead's sink or person's task has no step of schema 1's, so it is never
    /// eligible for the comparison and never moved by it; and a step the version does not hold (a look added for the work) has no node.
    /// </summary>
    [Fact]
    public void Synthetic_nodes_are_counted_apart_and_an_added_step_has_no_node()
    {
        foreach (var (refused, kind) in new[] { ("stop", NodeKinds.Stop), ("wait", NodeKinds.Human) })
        {
            var (_, projection) = Projected(Runs[refused == "stop" ? "a go-ahead cannot start, and schema 1's landing goes on past it" : "a go-ahead refused to the person's task, and schema 1 landed past it"]);
            Assert.Equal(["permit:rejected"], projection.Synthetic);
            Assert.Equal(kind, projection.Version.Graph!.Nodes.Single(node => node.Id == "permit:rejected").Kind);
            Assert.DoesNotContain("permit:rejected", projection.Frontier);
            Assert.DoesNotContain(projection.Steps, row => row.Node?.Id == "permit:rejected");
            Assert.Equal(NodeStates.Blocked, projection.Statuses["permit:rejected"].State);
        }

        var (run, added) = Projected(Runs["a look the chain's choice added for this work is no node of the version"]);
        var look = added.Steps.Single(row => row.Step.Step.Id == "look");
        Assert.Equal((ReviewLevels.Chain, (NodeDeclaration?)null, (NodeStatus?)null), (look.Step.Added, look.Node, look.Status));
        Assert.Equal(["work", "look", "land"], run.Steps.Select(step => step.Step.Id));
    }

    // ---- the stored version, its identity and its reads

    /// <summary>The CLI twin's smallest version (its table's first row), with the identity and the graph digest the twin computed for it.</summary>
    private const string Smallest = """{ "id": "work", "kind": "work" }, { "id": "land", "kind": "landing", "form": "merge", "accept": "you" }""";
    private const string SmallestIdentity = "75906e6bfc40";
    private const string SmallestGraph = "gph-a165bc24ca1ef885986856aa914f1fc3";

    private static string File(params string[] versions) =>
        "{\n  \"id\": \"careful\",\n  \"name\": \"Careful\",\n  \"versions\": [\n"
        + string.Join(",\n", versions.Select((steps, at) => $"    {{ \"version\": {at + 1}, \"at\": \"2026-10-1{at}T09:12:00Z\", \"door\": \"screen\", \"steps\": [ {steps} ] }}"))
        + "\n  ]\n}\n";

    private void Keep(string file)
    {
        var path = WorkflowStore.PathOf(_home, "careful");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, file);
    }

    /// <summary>Run q1 bound to the file's version <paramref name="version"/> at its digest, as its first start binds it.</summary>
    private void Bind(int version)
    {
        var digest = new WorkflowFiles(_home).Load("careful")!.Read.Versions.Single(each => each.Version == version).Digest;
        Assert.True(new WorkflowRunFiles(_home).Bind(new WorkflowRunBinding(RunId, Repo, Space, "careful", WorkflowLevels.Repository, T) { Version = version, Digest = digest }));
    }

    /// <summary>The run read as the run reader reads it, its version from the store by its binding, its records the work done.</summary>
    private static WorkflowRun Read(IWorkflowStore store, IWorkflowRunStore runs)
    {
        var config = DriverConfig.Parse(Rules);
        var process = WorkflowProcesses.Read(store, runs, config, Repo, Space, RunId);
        var graph = WorkflowRunGraph.Read(process, WorkflowCurrent.Derive(config, Repo, Space, Plugins), Plugins);
        return (process.Problem ?? process.Unread) is { } problem
            ? new WorkflowRun(Repo, Space, graph, [RunId], []) { Process = process, Problem = problem }
            : WorkflowRuns.Derive(Done(new WorkflowRunFacts(Repo, graph) { Process = process, Review = Review(ReviewStates.NotShown) }), Space);
    }

    /// <summary>
    /// A newer version never leaks into a run bound to an older one, and Current stays live: v1 bound, then v2 adds an opinion, renames and
    /// reorders the look and changes the landing's choices; the run still reads on v1's graph. v1 edited in place, v1 removed, the binding
    /// damaged and the workflow's file gone each read as unreadable, never as v2 or Current.
    /// </summary>
    [Fact]
    public void A_bound_run_reads_its_own_version_and_never_a_newer_one_or_Current()
    {
        const string v1 = """{ "id": "work", "kind": "work" }, { "id": "see", "kind": "look" }, { "id": "land", "kind": "landing", "form": "merge", "accept": "you" }""";
        const string v2 = """{ "id": "work", "kind": "work" }, { "id": "second", "kind": "opinion" }, { "id": "review", "kind": "look" }, { "id": "land", "kind": "landing", "form": "branch", "accept": "automatic", "pattern": "work/{quest}" }""";
        Keep(File(v1));
        Bind(1);
        var bound = new WorkflowRunFiles(_home).Read(RunId)!.Digest;
        Keep(File(v1, v2));

        var store = new WorkflowFiles(_home);
        var runs = new WorkflowRunFiles(_home);
        var version = WorkflowRunNodes.VersionOf(store, runs, RunId)!;
        Assert.Equal((1, bound), (version.Version, version.Identity));
        Assert.Equal(["work", "see", "land"], version.Graph!.Nodes.Select(node => node.Id));
        var projection = WorkflowRunNodes.Read(store, runs, Read(store, runs))!;
        Assert.Equal(["work", "see", "land"], projection.Steps.Select(row => row.Node?.Id));
        Assert.Equal(("see", "see"), (projection.Run.At, projection.At));

        foreach (var (why, file, damage) in new (string, string?, Action?)[]
        {
            ("v1 edited in place", File(v1.Replace("\"accept\": \"you\"", "\"accept\": \"automatic\""), v2), null),
            ("v1 removed", File(v2).Replace("\"version\": 1", "\"version\": 2"), null),
            ("the binding damaged", null, () => System.IO.File.WriteAllText(WorkflowRunBindings.PathOf(_home, RunId), "{ not json")),
            ("the workflow's file gone", null, () => System.IO.File.Delete(WorkflowStore.PathOf(_home, "careful"))),
        })
        {
            Keep(File(v1, v2));
            System.IO.File.Delete(WorkflowRunBindings.PathOf(_home, RunId));
            Bind(1);
            if (file is not null) Keep(file);
            damage?.Invoke();

            var unread = WorkflowRunNodes.VersionOf(store, runs, RunId);
            Assert.True(unread is { Readable: false, Unreadable: not null }, $"{why}: it read as {unread?.Identity ?? "Current"}");
            var read = WorkflowRunNodes.Read(store, runs, Read(store, runs));
            Assert.True(read is { Readable: false, Agrees: false, Frontier.Count: 0 }, $"{why}: a projection was read");
        }

        new WorkflowRunFiles(_home).Write(RunId, new WorkflowRunBinding(RunId, Repo, Space, WorkflowSelection.Current, WorkflowLevels.Current, T));
        Assert.Null(WorkflowRunNodes.VersionOf(store, runs, RunId));
        Assert.Null(WorkflowRunNodes.VersionOf(store, runs, "q9"));
    }

    /// <summary>
    /// The identity is the stored one, unchanged: a run bound to the CLI twin's smallest version reads with the identity and the graph digest
    /// the twin computed in its own runtime, and its nodes bind that graph. A changed decoder derives another graph digest, so a run on it
    /// binds afresh, while the binding's stored identity and the files' bytes stay as they were.
    /// </summary>
    [Fact]
    public void The_bound_identity_is_unchanged_and_a_decoder_change_moves_only_the_derived_digest()
    {
        Keep(File(Smallest));
        Bind(1);
        var before = Snapshot();
        var store = new WorkflowFiles(_home);
        var runs = new WorkflowRunFiles(_home);

        var version = WorkflowRunNodes.VersionOf(store, runs, RunId)!;
        Assert.Equal((SmallestIdentity, SmallestGraph), (version.Identity, version.GraphDigest));
        Assert.Equal(SmallestIdentity, runs.Read(RunId)!.Digest);
        var projection = WorkflowRunNodes.Read(store, runs, Read(store, runs))!;
        Assert.All(projection.Statuses.Values.Where(status => status.Binding is not null), status => Assert.Equal(SmallestGraph, status.Binding!.GraphDigest));
        Assert.Equal(new NodeScope(SmallestGraph, RunId), projection.Statuses["work"].Scope);

        var changed = WorkflowGraphs.GraphDigest(version.Identity!, version.Graph!, WorkflowGraphs.DecoderVersion + 1);
        Assert.NotEqual(SmallestGraph, changed);
        Assert.Equal(SmallestGraph, WorkflowGraphs.GraphDigest(version.Identity!, version.Graph!, WorkflowGraphs.DecoderVersion));
        Assert.Equal(SmallestIdentity, runs.Read(RunId)!.Digest);
        Assert.Equal(before, Snapshot());
    }

    /// <summary>Every file under the home with its bytes and its last write, in ordinal order.</summary>
    private List<(string File, string Bytes, DateTime Written)> Snapshot() =>
        [.. Directory.GetFiles(_home, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Select(file => (file, Convert.ToBase64String(System.IO.File.ReadAllBytes(file)), System.IO.File.GetLastWriteTimeUtc(file)))];

    /// <summary>A store that reads through and refuses every write: a read that binds, repairs or persists fails here.</summary>
    private sealed class ReadOnlyStore(IWorkflowStore inner) : IWorkflowStore
    {
        public WorkflowFileFound? Load(string id) => inner.Load(id);

        public IReadOnlyList<WorkflowFileFound> List() => inner.List();

        public void Write(string id, JsonNode file) => throw new InvalidOperationException($"a read wrote the workflow `{id}`");
    }

    private sealed class ReadOnlyRuns(IWorkflowRunStore inner) : IWorkflowRunStore
    {
        public bool Bound(string run) => inner.Bound(run);

        public WorkflowRunBinding? Read(string run) => inner.Read(run);

        public bool Bind(WorkflowRunBinding binding) => throw new InvalidOperationException($"a read bound the run `{binding.Run}`");

        public void Write(string run, WorkflowRunBinding binding) => throw new InvalidOperationException($"a read wrote the run `{run}`");

        public bool AnyNamed(string repository) => inner.AnyNamed(repository);

        public IReadOnlyList<int> KeptVersions(string workflow) => inner.KeptVersions(workflow);
    }

    /// <summary>
    /// Reads that never start writing: read three times through stores that refuse every write, the home's files, their bytes and their
    /// last writes stay as they were, and the projection takes nothing that could dispatch, schedule, call a plugin or keep a record: its
    /// only inputs are the run, its version, its id and the two stores, read.
    /// </summary>
    [Fact]
    public void Reading_a_run_on_its_graph_writes_nothing_binds_nothing_and_takes_nothing_that_dispatches()
    {
        Keep(File(Smallest, """{ "id": "work", "kind": "work" }, { "id": "see", "kind": "look" }, { "id": "land", "kind": "landing", "form": "merge", "accept": "you" }"""));
        Bind(1);
        var before = Snapshot();
        var store = new ReadOnlyStore(new WorkflowFiles(_home));
        var runs = new ReadOnlyRuns(new WorkflowRunFiles(_home));

        var said = new List<string>();
        for (var again = 0; again < 3; again++)
        {
            var projection = WorkflowRunNodes.Read(store, runs, Read(store, runs))!;
            said.Add(string.Join(";", projection.Statuses.OrderBy(each => each.Key, StringComparer.Ordinal).Select(each => $"{each.Key}={each.Value.State}:{each.Value.Current?.Id}")));
        }

        Assert.Single(said.Distinct());
        Assert.Equal(before, Snapshot());
        Assert.Null(WorkflowRunNodes.VersionOf(store, runs, "q9"));
        Assert.Equal(before, Snapshot());

        Type[] reads = [typeof(IWorkflowStore), typeof(IWorkflowRunStore), typeof(WorkflowRun), typeof(DecodedVersion), typeof(string)];
        foreach (var method in typeof(WorkflowRunNodes).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            Assert.All(method.GetParameters(), parameter => Assert.Contains(parameter.ParameterType, reads));
        }
    }
}
