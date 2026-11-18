using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run whose landing has reached keeps every step it reached when its own session goes on: words after the landing are the session's
/// turn, and what it commits then, or its tree brought to the line the work merged into, is new work for the chain's next quest, never
/// this run's. Before the landing the session's going on takes the run back to what the line holds.
/// </summary>
public sealed class LandedRunGoesOnTests : NodeRunFixture
{
    /// <summary>
    /// The release path's shape, cut to what the run reads: the claim, what the line holds, the Accept, the landing, the release, and the
    /// config change after it, which keeps the run going once the release has reached.
    /// </summary>
    private const string Released = """
        {"schemaVersion":2,"entries":["work"],"end":"config",
         "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"content","kind":"content.holds","version":1,"reach":"observed"},
                  {"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"landing","kind":"land","version":1,"reach":"performed"},
                  {"id":"release","kind":"acme.deploy/deploy","version":1,"reach":"performed"},
                  {"id":"config","kind":"task","version":1,"reach":"pressed"}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"content"},{"id":"b","kind":"after","from":"content","to":"review"},
                  {"id":"c","kind":"after","from":"review","to":"landing"},{"id":"d","kind":"after","from":"landing","to":"release"},
                  {"id":"e","kind":"after","from":"release","to":"config"}]}
        """;

    private static readonly string[] Reached = ["work", "content", "review", "landing", "release"];

    private QuestView Done(long sequence) =>
        RunHome.Quest(RunId, "Done") with { Submission = new QuestSubmissionView("m1", sequence, _now, $"answer {sequence}") };

    /// <summary>The run's branch at <paramref name="tip"/>, its quest's handback numbered <paramref name="sequence"/>, landed where <paramref name="landed"/>.</summary>
    private void OnBranch(string tip, long sequence, bool landed)
    {
        _branchTips["daoris/s1"] = tip;
        var branch = landed
            ? RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId], RunHome.Landing("unit-b1", "feature/q7", Tip1, _now, RunId))
            : RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId]);
        _units = [RunHome.QuestUnit(Done(sequence)), branch];
    }

    private static string BranchRevision(long sequence) =>
        ReviewWork.RevisionOf(ReviewKinds.Branch, new ReviewReference { Branch = "daoris/s1", Tip = Tip1 }, [new ReviewPart(RunId, "The work") { Machine = "m1", Sequence = sequence }]);

    /// <summary>A pass whose look reads what the line holds of the work: none of its commits held anywhere, so <c>content</c> lets it go.</summary>
    private Task<NodeRuntimePass> PassAsync(NodeRuntime runtime) =>
        runtime.PassAsync(RunLook() with { Content = (_, _, _) => Task.FromResult<HoldContent?>(new HoldContent(null, 1, 1)) }, CancellationToken.None);

    /// <summary>The run accepted at <see cref="NodeRunFixture.Tip1"/>: its review reached on the person's Accept, its landing begun.</summary>
    private async Task<NodeRuntime> AcceptedAsync()
    {
        BoundTo(Released);
        OnBranch(Tip1, 1, landed: false);
        var runtime = Runtime();
        await PassAsync(runtime);
        await PassAsync(runtime);
        Assert.Equal((NodeStates.Reached, Tip1), (RunStatus("content").State, RunStatus("content").Acceptance?.SubjectRevision));
        var review = RunStatus("review");
        Assert.Equal(NodeStates.Active, review.State);
        Assert.True(RunNodeWords.Word(Files, RunId, "review", NodeWordActs.Accept, null, () => _now, attempt: review.Attempt, revision: BranchRevision(1)).Done);
        await PassAsync(runtime);
        Assert.Equal(NodeStates.Reached, RunStatus("review").State);
        return runtime;
    }

    /// <summary>The run landed at <see cref="NodeRunFixture.Tip1"/> and its release sent once, reached where <paramref name="released"/>.</summary>
    private async Task<(NodeRuntime Runtime, Dictionary<string, NodeStatus> Before)> LandedAsync(bool released)
    {
        var runtime = await AcceptedAsync();
        OnBranch(Tip1, 1, landed: true);
        await PassAsync(runtime);
        Assert.Equal((NodeStates.Reached, NodeStates.Active), (RunStatus("landing").State, RunStatus("release").State));
        var invocation = Assert.Single(_port.Sent).Invocation;
        if (released)
        {
            _port.Heard.Enqueue(new(Plugin, 1, new WorkStepFrame("deploy", new WorkOutcome(WorkStates.Reached), _now, "e1", Invocation: invocation)));
            await runtime.PassAsync(RunLook(whole: false), CancellationToken.None);
            await PassAsync(runtime);
            Assert.Equal((NodeStates.Reached, NodeStates.Active), (RunStatus("release").State, RunStatus("config").State));
        }

        return (runtime, Files.Read(RunUnit)!.Statuses.ToDictionary(each => each.Key, each => each.Value, StringComparer.Ordinal));
    }

    private void Kept(Dictionary<string, NodeStatus> before, IEnumerable<string> nodes)
    {
        foreach (var node in nodes)
        {
            var (was, now) = (before[node], RunStatus(node));
            Assert.True((was.State, was.Attempt, was.Acceptance?.Id) == (now.State, now.Attempt, now.Acceptance?.Id),
                $"{node}: {was.State}#{was.Attempt} became {now.State}#{now.Attempt}");
        }

        Assert.Single(_port.Sent);
    }

    /// <summary>
    /// The install's case: the release reached, the person's words reopen the session, its tree is brought to the line its pull request was
    /// merged into and it hands its turn back. Nothing the run reached moves: no step asks again, nothing is sent again.
    /// </summary>
    [Fact]
    public async Task A_landed_and_released_runs_session_given_words_and_taking_a_turn_keeps_every_step_it_reached()
    {
        var (runtime, before) = await LandedAsync(released: true);

        OnBranch(Tip2, 2, landed: true);
        await PassAsync(runtime);
        await PassAsync(runtime);

        Kept(before, [.. Reached, "config"]);
        Assert.Null(Files.Read(RunUnit)!.Ended);
    }

    /// <summary>A commit the session makes once its run has landed is new work: the run waits on its release, never asking the Accept again.</summary>
    [Fact]
    public async Task A_commit_after_the_landing_never_sends_the_run_back_to_its_review()
    {
        var (runtime, before) = await LandedAsync(released: false);

        OnBranch(Tip2, 1, landed: true);
        await PassAsync(runtime);
        await PassAsync(runtime);

        Kept(before, ["work", "content", "review", "landing", "release"]);
        Assert.Equal(NodeStates.Active, RunStatus("release").State);
    }

    /// <summary>Before the landing the session's going on is the work moving: what the line holds is judged again at its new tip.</summary>
    [Fact]
    public async Task Before_the_landing_the_sessions_going_on_still_takes_the_run_back_to_what_the_line_holds()
    {
        var runtime = await AcceptedAsync();
        var accepted = RunStatus("review");

        OnBranch(Tip2, 2, landed: false);
        await PassAsync(runtime);
        await PassAsync(runtime);

        var content = RunStatus("content");
        Assert.Equal((NodeStates.Reached, Tip2), (content.State, content.Acceptance?.SubjectRevision));
        Assert.True(RunStatus("review").Attempt > accepted.Attempt);
        Assert.NotEqual(NodeStates.Reached, RunStatus("review").State);
        Assert.Empty(_port.Sent);
    }
}
