using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests.Workflows;

/// <summary>
/// A performed plugin step whose platform waits on someone: its plugin's progress names the actor (a release's Production stage waiting on
/// its approver), the attempt keeps it unsettled, and the run's step reads waiting on that actor, filled by its holder, with the act and the
/// release's page, never working with nothing said. Seen failing first.
/// </summary>
public sealed class StepWaitingTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-stepwaiting-" + Guid.NewGuid().ToString("N")[..8]);

    private const string Plugin = "daoris-azure-devops";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";
    private const string Page = "https://dev.azure.com/acme/Proj/_releaseProgress?releaseId=90";
    private const string Act = "Release-12 of app-release: Approve Production in Azure DevOps: asked of Release Approvers since 2026-11-16 09:50 UTC.";
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 51, 0, TimeSpan.Zero);

    private static readonly NodeDeclaration Release = new("release", Plugin + "/release", 1, NodeReach.Performed);

    private static readonly NodeAttempt Sent = new("release", 1, Plugin,
        new WorkPerformCall("run:q7", Plugin + "/release", 1, "i-1", "k-1", Tip, [], Now.AddMinutes(60)), AttemptStates.Sent);

    private static readonly StepProgress Kept = new("approver", Act, Page, "Production", Now);

    private static JsonElement Frame(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void A_progress_bound_to_its_attempt_reads_with_the_actor_it_waits_on_and_an_unbound_one_or_an_outcome_naming_an_actor_does_not()
    {
        var step = WorkWire.ReadStep(Frame($$"""
            {"step":"{{Plugin}}/release","invocation":"i-1","state":"progress","actor":"approver",
             "evidence":{"url":"{{Page}}","environment":"Production","message":"{{Act}}"},"at":"2026-11-16T09:51:00Z","id":"i-1-p1"}
            """), out var field);

        Assert.Null(field);
        Assert.Equal(WorkStates.Progress, step!.Outcome.State);
        Assert.Equal("approver", step.Outcome.Actor);
        Assert.Equal(Page, step.Outcome.Evidence?.Url);
        Assert.Equal("i-1", step.Invocation);
        Assert.Contains("\"actor\":\"approver\"", WorkWire.Step(step).ToJsonString(), StringComparison.Ordinal);

        Assert.Null(WorkWire.ReadStep(Frame($$"""
            {"step":"{{Plugin}}/release","repository":"app","branch":"main","commit":"{{Tip}}","state":"progress","at":"2026-11-16T09:51:00Z","id":"e1"}
            """), out var unbound));
        Assert.Equal("state", unbound);
        Assert.Null(WorkWire.ReadStep(Frame($$"""
            {"step":"{{Plugin}}/release","invocation":"i-1","state":"reached","actor":"approver","at":"2026-11-16T09:51:00Z","id":"i-1"}
            """), out var named));
        Assert.Equal("actor", named);
    }

    /// <summary>A run's record as the runtime keeps it: the release step active on its attempt, which the record holds a status for.</summary>
    private static NodeRecord Holding(NodeAttempt attempt) => new("run:q7")
    {
        Statuses = new Dictionary<string, NodeStatus> { ["release"] = new("run:q7", "release", NodeStates.Active) { Attempt = 1, Tries = 1, Unreconciled = 1 } },
        Attempts = [attempt with { RecordedAt = Now, Generation = 1, Deliveries = 1 }],
    };

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public void A_progress_is_kept_on_the_attempt_out_and_settles_nothing()
    {
        var (next, result) = WorkHearing.Progressed(Holding(Sent), "run:q7", "i-1", Kept);

        Assert.Equal(PerformedWhat.Noted, result.What);
        var kept = Assert.Single(next!.Attempts);
        Assert.Equal(AttemptStates.Sent, kept.State);
        Assert.Equal(Kept, kept.Progress);

        var (unchanged, replay) = WorkHearing.Progressed(Holding(Sent with { State = AttemptStates.Settled }), "run:q7", "i-1", Kept);
        Assert.Null(unchanged);
        Assert.Equal(PerformedWhat.Replay, replay.What);
    }

    [Fact]
    public void A_wait_the_record_keeps_reads_back_the_same_after_a_restart_and_a_progress_going_on_with_its_parts_unsaid()
    {
        var (waiting, _) = WorkHearing.Progressed(Holding(Sent), "run:q7", "i-1", Kept);
        new NodeRecordFiles(_home, () => true).Write(waiting!);

        var read = new NodeRecordFiles(_home, () => true).Read("run:q7")!;

        Assert.Equal(NodeRecordFiles.ToJson(waiting!), NodeRecordFiles.ToJson(read));
        Assert.Equal(Kept, StepWaiting.Of(read, Release, read.Statuses["release"]));
        var going = Kept with { Actor = null, Url = null, Environment = null };
        Assert.Equal(going, Assert.Single(NodeRecordFiles.Parse(NodeRecordFiles.ToJson(Holding(Sent with { Progress = going })))!.Attempts).Progress);
    }

    [Fact]
    public void A_release_whose_production_stage_waits_on_an_approval_reads_waiting_on_its_approver_filled_by_you_with_the_act_and_its_page()
    {
        var record = new NodeRecord("run:q7") { Attempts = [Sent with { Progress = Kept }] };
        var active = new NodeStatus("run:q7", "release", NodeStates.Active) { Attempt = 1 };

        var progress = StepWaiting.Of(record, Release, active);
        var wait = StepWaiting.WaitOf(progress!, CastHolders.StepOf(CastHolders.Person));
        Assert.Equal(StepHolders.Agent, StepWaiting.WaitOf(progress!).Values["holder"]);

        Assert.Equal(StepWaiting.Actor, wait.Code);
        Assert.Equal("approver", wait.Values["actor"]);
        Assert.Equal(StepHolders.Person, wait.Values["holder"]);
        Assert.Equal(Page, wait.Values["url"]);
        Assert.Equal(Now, wait.Since);
        var step = new WorkflowRunStep(WorkflowGraphRuns.Cell(Release, null), WorkflowRunStates.Waiting, WorkflowGraphDetails.Active) { Node = Release.Kind, Wait = wait };
        Assert.Equal("waiting", WorkflowRunWords.State(step.State));
        Assert.Equal($"Waiting on `approver`: you. {Act.TrimEnd('.')}. Open {Page}", WorkflowRunWords.Said(step));

        // A settled attempt, or one that says it goes on, waits on no actor.
        Assert.Null(StepWaiting.Of(new NodeRecord("run:q7") { Attempts = [Sent with { State = AttemptStates.Settled, Progress = Kept }] }, Release, active));
        var going = StepWaiting.WaitOf(Kept with { Actor = null, Words = "Release-12 of app-release: Production deploying." });
        Assert.Equal(StepWaiting.Going, going.Code);
        Assert.False(going.Values.ContainsKey("holder"));
    }
}
