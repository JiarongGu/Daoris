using System.Text.Json.Nodes;
using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An agent's done in a workflow's run is its submission, so its turn's end waits on the run's next step (<c>awaiting-step</c>) for as long
/// as the run goes on, whether or not its record kept that wait: a session an earlier end read as finished (<c>ended.submitted</c>,
/// <c>completed</c>) whose run still waits on the person at <c>holds</c> goes on with their words and ends awaiting the step again, never
/// <c>completed</c>. Seen failing first: the resume read the run's wait only from the record's note, so the owner's yes ended it completed
/// once more while the run held at <c>holds</c>.
/// </summary>
public sealed class SubmittedConclusionTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-submitted-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The work, its quest's holds, then the person's Accept before the landing.</summary>
    private static JsonNode Graph() => JsonNode.Parse("""
        {"schemaVersion":2,"entries":["work"],"end":"landing","nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},
          {"id":"holds","kind":"quest.holds","version":1,"reach":"observed"},
          {"id":"review","kind":"review","version":1,"reach":"pressed"},
          {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"branch","pattern":"docs/{quest}"}}],
         "wires":[{"id":"a","kind":"after","from":"work","to":"holds"},{"id":"b","kind":"after","from":"holds","to":"review"},
          {"id":"c","kind":"after","from":"review","to":"landing"}]}
        """)!;

    private static readonly QuestView Quest = new("q1", "daoris", "web-app", "Docs", "", "Taken");

    private static readonly QuestView Submitted = Quest with
    {
        Closure = "c1", Submission = new QuestSubmissionView("m1", 1, At, "Built.", Commit: "abc1234"),
    };

    private sealed class World(IReadOnlyList<QuestView> quests) : IWorkflowStartWorld
    {
        public Task<IReadOnlyList<QuestView>> EveryQuestAsync(CancellationToken ct) => Task.FromResult(quests);

        public Task<AskView?> FindAskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        public void Chosen(LandingLine line)
        {
        }
    }

    private async Task<World> BoundAsync()
    {
        StoredSteps.AddGraph(WorkflowPorts.Store(_home), "docs-to-pr", "Documentation", "2026-11-18T08:00:00Z", Graph());
        var config = DriverConfig.Parse("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}""");
        var world = new World([Quest]);
        var bound = await WorkflowRunStart.AtStartAsync(
            new WorkflowStarted(Quest, "work"), world, WorkflowPorts.Store(_home), WorkflowPorts.Runs(_home), config, [], () => At, CancellationToken.None);
        Assert.Equal(WorkflowBindCame.Bound, bound.Came);
        return world;
    }

    private Task<StepWait?> WaitsAsync(World world, IReadOnlyList<NotePart>? kept, QuestView handed, bool going) =>
        WorkflowRunStart.TurnWaitsAsync(kept, Quest, handed, world, WorkflowPorts.Runs(_home), WorkflowPorts.Store(_home), _ => going, CancellationToken.None);

    [Fact]
    public async Task A_session_ended_completed_on_its_submission_goes_on_with_words_and_awaits_its_runs_step_while_the_run_waits_at_holds()
    {
        var world = await BoundAsync();
        var ended = Observation.Conclude(0, "Taken", submitted: true);
        Assert.Equal(("completed", "ended.submitted"), (ended.State, ended.Parts![0].Code));

        var waits = await WaitsAsync(world, ended.Parts, Submitted, going: true);
        var again = Observation.Resumed(0, "completed", "Taken", "Taken", submitted: true, waits: waits);

        Assert.Equal(new StepWait("q1", "work", 1, "abc1234"), waits);
        Assert.Equal(SessionStates.AwaitingStep, again.State);
        Assert.Equal(waits, StepWaits.From(again.Parts));
    }

    [Fact]
    public async Task An_awaiting_step_session_taking_words_awaits_the_same_step_again_and_a_run_that_ended_its_sessions_lets_the_done_end_it()
    {
        var world = await BoundAsync();
        var kept = new StepWait("q1", "work", 1, "abc1234").Note().Parts;

        var again = Observation.Resumed(0, SessionStates.AwaitingStep, "Taken", "Taken", submitted: true, waits: await WaitsAsync(world, kept, Submitted, going: true));
        var over = Observation.Resumed(0, "completed", "Taken", "Taken", submitted: true, waits: await WaitsAsync(world, null, Submitted, going: false));

        Assert.Equal((SessionStates.AwaitingStep, "q1"), (again.State, StepWaits.From(again.Parts)?.Run));
        Assert.Equal(("completed", "ended.submitted"), (over.State, over.Parts![0].Code));
        Assert.Null(await WaitsAsync(world, null, Quest, going: true));
    }
}
