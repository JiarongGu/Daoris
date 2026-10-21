using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The work wire's contract, <c>work.perform@1</c> (FABRIC-PERFORM1a; the fabric events design §C4, §D6): each message written and read back
/// is the message written, and a plugin's event of the wrong shape is refused naming its first broken field (§C4's fifth rule, <c>shape</c>).
/// </summary>
public sealed class WorkWireTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private static readonly DateTimeOffset At = new(2026, 10, 17, 9, 30, 0, TimeSpan.Zero);

    private static JsonElement Parse(JsonNode node) => JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void The_points_and_methods_are_named_once()
    {
        Assert.Equal(["work/watch", "work/step", "work/perform"], HookPoints.Work);
        Assert.Empty(HookPoints.Work.Intersect(HookPoints.All));
        Assert.Equal(("hook/work/watch", "hook/work/perform", "hook/work/reconcile", "work/step"),
            (WorkWire.WatchMethod, WorkWire.PerformMethod, WorkWire.ReconcileMethod, WorkWire.StepMethod));
        Assert.Equal("work.perform@1", WorkWire.Capability);
    }

    [Fact]
    public void A_watch_frame_round_trips()
    {
        var frame = new WorkWatchFrame(3, Resync: true,
        [
            new WatchedUnit("engine", "game", "D:/fam/engine", "quest/abc123", Commit, "main", "https://example.test/pr/7", ["deploy", "smoke"]),
            new WatchedUnit("site", "game", "D:/fam/site", "quest/def456", Commit, null, null, ["deploy"]),
        ]);

        var written = WorkWire.Watch(frame);
        var read = WorkWire.ReadWatch(Parse(written));

        Assert.NotNull(read);
        Assert.Equal(written.ToJsonString(), WorkWire.Watch(read).ToJsonString());
        Assert.Equal((3, true, 2), (read.Generation, read.Resync, read.Units.Count));
        Assert.Equal(["deploy", "smoke"], read.Units[0].Awaiting);
        Assert.Null(read.Units[1].PullRequest);
        // The names are the wire's, spelled here and nowhere else.
        Assert.Equal(
            """{"generation":3,"resync":true,"units":[{"repository":"engine","workspace":"game","root":"D:/fam/engine","branch":"quest/abc123","tip":"0123456789abcdef0123456789abcdef01234567","line":"main","pullRequest":"https://example.test/pr/7","awaiting":["deploy","smoke"]},{"repository":"site","workspace":"game","root":"D:/fam/site","branch":"quest/def456","tip":"0123456789abcdef0123456789abcdef01234567","line":null,"pullRequest":null,"awaiting":["deploy"]}]}""",
            written.ToJsonString());
    }

    [Fact]
    public void An_observed_step_event_round_trips_as_a_notification()
    {
        var step = new WorkStepFrame("deployed", new WorkOutcome(WorkStates.Reached,
                Evidence: new WorkEvidence("deploy-41", "https://example.test/deploys/41", "preview", "Deployed — 预览")),
            At, "event-1", "engine", "quest/abc123", Commit);

        var frame = WorkWire.Step(step);
        var element = Parse(frame);
        var read = WorkWire.ReadStep(element.GetProperty("params"), out var field);

        Assert.Null(field);
        Assert.NotNull(read);
        Assert.Equal(frame.ToJsonString(), WorkWire.Step(read).ToJsonString());
        // A notification: no id, never answered.
        Assert.Equal("work/step", element.GetProperty("method").GetString());
        Assert.False(element.TryGetProperty("id", out _));
        Assert.Equal((At, "Deployed — 预览"), (read.At, read.Outcome.Evidence!.Message));
    }

    [Fact]
    public void A_performed_step_s_completion_round_trips_bound_to_its_invocation()
    {
        var reached = new WorkStepFrame("acme.deploy/deploy", new WorkOutcome(WorkStates.Reached,
                Outputs: new Dictionary<string, WorkOutput>
                {
                    ["preview"] = new("acme.deploy/preview@1:p-41", "r1"),
                    ["log"] = new("acme.deploy/log@1:l-41", null),
                }),
            At, "event-2", Invocation: "i1");
        var failed = new WorkStepFrame("deploy", new WorkOutcome(WorkStates.Failed, Code: "rate-limited"), At, "event-3", Invocation: "i2");

        foreach (var step in new[] { reached, failed })
        {
            var frame = WorkWire.Step(step);
            var read = WorkWire.ReadStep(Parse(frame).GetProperty("params"), out var field);

            Assert.Null(field);
            Assert.NotNull(read);
            Assert.Equal(frame.ToJsonString(), WorkWire.Step(read).ToJsonString());
            Assert.Equal(step.Invocation, read.Invocation);
            Assert.Null(read.Commit);
        }
    }

    [Fact]
    public void A_perform_call_and_its_answer_round_trip()
    {
        var call = new WorkPerformCall("u1", "acme.deploy/deploy", 1, "i1", "k1", "r1", ["acme.build/artifact@1:a1"], At.AddMinutes(10));

        var written = WorkWire.Perform(call);
        var read = WorkWire.ReadPerform(Parse(written));

        Assert.NotNull(read);
        Assert.Equal(written.ToJsonString(), WorkWire.Perform(read).ToJsonString());
        Assert.Equal(("k1", 1, At.AddMinutes(10)), (read.IdempotencyKey, read.StepVersion, read.DeadlineAt));

        // The answer acknowledges receipt of this invocation, and nothing else.
        Assert.True(WorkWire.ReadPerformAnswer(Parse(WorkWire.PerformAnswer("i1")), "i1"));
        Assert.False(WorkWire.ReadPerformAnswer(Parse(WorkWire.PerformAnswer("i2")), "i1"));
        Assert.False(WorkWire.ReadPerformAnswer(Parse("""{"received":true}"""), "i1"));
        Assert.False(WorkWire.ReadPerformAnswer(Parse("true"), "i1"));
        Assert.False(WorkWire.ReadPerformAnswer(default, "i1"));
    }

    public static TheoryData<string, string?, string?> ReconcileAnswers => new()
    {
        { ReconcileStates.Done, WorkStates.Reached, null },
        { ReconcileStates.Done, WorkStates.Failed, "unavailable" },
        { ReconcileStates.Failed, WorkStates.Failed, "timeout" },
        { ReconcileStates.NotStarted, null, null },
        { ReconcileStates.Running, null, null },
        { ReconcileStates.Unknown, null, null },
    };

    [Theory]
    [MemberData(nameof(ReconcileAnswers))]
    public void A_reconcile_call_and_each_answer_round_trip(string state, string? outcome, string? code)
    {
        var call = new WorkReconcileCall("u1", "acme.deploy/deploy", "i1", "k1", "r1");
        var written = WorkWire.Reconcile(call);
        var read = WorkWire.ReadReconcile(Parse(written));
        Assert.NotNull(read);
        Assert.Equal(written.ToJsonString(), WorkWire.Reconcile(read).ToJsonString());

        var answer = new WorkReconcileAnswer(state, outcome is null ? null : new WorkOutcome(outcome, code));
        var answered = WorkWire.ReconcileAnswer(answer);
        var heard = WorkWire.ReadReconcileAnswer(Parse(answered));

        Assert.NotNull(heard);
        Assert.Equal(answered.ToJsonString(), WorkWire.ReconcileAnswer(heard).ToJsonString());
        Assert.Equal((state, outcome, code), (heard.State, heard.Outcome?.State, heard.Outcome?.Code));
    }

    [Theory]
    [InlineData("""{"state":"finished"}""")]
    [InlineData("""{"state":"done"}""")]
    [InlineData("""{"state":"done","outcome":{"state":"unknown"}}""")]
    [InlineData("""{"state":"failed","outcome":{"state":"reached"}}""")]
    [InlineData("""{"state":"failed"}""")]
    [InlineData("""{"state":"running","outcome":{"state":"reached"}}""")]
    [InlineData("""{"state":"done","outcome":{"state":"reached","outputs":{"Preview":{"ref":"acme.deploy/preview@1:p"}}}}""")]
    [InlineData("""["done"]""")]
    public void A_reconcile_answer_of_the_wrong_shape_is_no_answer(string json) =>
        Assert.Null(WorkWire.ReadReconcileAnswer(Parse(json)));

    /// <summary>The step event's shape, field by field: the first broken one is named, and the event is no event (§C4's <c>shape</c>).</summary>
    [Theory]
    [InlineData("the least observed", """{"step":"deployed","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","repository":"engine","branch":"quest/a","commit":"0123456789abcdef0123456789abcdef01234567"}""", null)]
    [InlineData("a sha-256 commit", """{"step":"deployed","state":"unknown","at":"2026-10-17T09:30:00+08:00","id":"e1","repository":"engine","branch":"quest/a","commit":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"}""", null)]
    [InlineData("the least completion", """{"step":"deploy","state":"failed","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1"}""", null)]
    [InlineData("a field it does not know is passed over", """{"step":"deploy","state":"failed","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1","plugin":"acme.other"}""", null)]
    [InlineData("not an object", """["deploy"]""", "params")]
    [InlineData("no step", """{"state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1"}""", "step")]
    [InlineData("a step in capitals", """{"step":"Deploy","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1"}""", "step")]
    [InlineData("a state it does not know", """{"step":"deploy","state":"done","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1"}""", "state")]
    [InlineData("a time with no offset", """{"step":"deploy","state":"reached","at":"2026-10-17T09:30:00","id":"e1","invocation":"i1"}""", "at")]
    [InlineData("a time that is a number", """{"step":"deploy","state":"reached","at":1760693400,"id":"e1","invocation":"i1"}""", "at")]
    [InlineData("no id", """{"step":"deploy","state":"reached","at":"2026-10-17T09:30:00Z","invocation":"i1"}""", "id")]
    [InlineData("an observed step with no repository", """{"step":"deployed","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","branch":"quest/a","commit":"0123456789abcdef0123456789abcdef01234567"}""", "repository")]
    [InlineData("an observed step with no branch", """{"step":"deployed","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","repository":"engine","commit":"0123456789abcdef0123456789abcdef01234567"}""", "branch")]
    [InlineData("a short commit", """{"step":"deployed","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","repository":"engine","branch":"quest/a","commit":"0123456"}""", "commit")]
    [InlineData("a commit in capitals", """{"step":"deployed","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","repository":"engine","branch":"quest/a","commit":"0123456789ABCDEF0123456789ABCDEF01234567"}""", "commit")]
    [InlineData("an invocation that is a number", """{"step":"deploy","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","invocation":7}""", "invocation")]
    [InlineData("a code in capitals", """{"step":"deploy","state":"failed","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1","code":"Timeout"}""", "code")]
    [InlineData("evidence as text", """{"step":"deploy","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1","evidence":"done"}""", "evidence")]
    [InlineData("an address that is no web page", """{"step":"deploy","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1","evidence":{"url":"file:///etc/passwd"}}""", "evidence")]
    [InlineData("outputs on a failure", """{"step":"deploy","state":"failed","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1","outputs":{"preview":{"ref":"acme.deploy/preview@1:p"}}}""", "outputs")]
    [InlineData("an output that is no reference", """{"step":"deploy","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1","outputs":{"preview":{"ref":"p-41"}}}""", "outputs")]
    [InlineData("an output named in capitals", """{"step":"deploy","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1","outputs":{"Preview":{"ref":"acme.deploy/preview@1:p"}}}""", "outputs")]
    public void A_step_event_is_read_by_shape(string name, string json, string? field)
    {
        var read = WorkWire.ReadStep(Parse(json), out var broken);

        Assert.True(field == broken, $"{name}: {broken}");
        Assert.Equal(field is null, read is not null);
    }

    /// <summary>The bounds on an event's words (§C4, §D6): a message of 300 characters, 8 outputs, a reference of 200, and one past each.</summary>
    [Theory]
    [InlineData(300, 8, 200, null)]
    [InlineData(301, 8, 200, "evidence")]
    [InlineData(300, 9, 200, "outputs")]
    [InlineData(300, 8, 201, "outputs")]
    public void An_event_s_words_are_bounded(int message, int outputs, int reference, string? field)
    {
        const string schema = "acme.deploy/preview@1:";
        var named = Enumerable.Range(0, outputs).ToDictionary(
            at => $"out-{at}", at => new WorkOutput(schema + new string('p', reference - schema.Length), null));
        var step = new WorkStepFrame("deploy", new WorkOutcome(WorkStates.Reached,
            Evidence: new WorkEvidence(null, null, null, new string('m', message)), Outputs: named), At, "e1", Invocation: "i1");

        WorkWire.ReadStep(Parse(WorkWire.Step(step)).GetProperty("params"), out var broken);

        Assert.Equal(field, broken);
    }

    /// <summary>FABRIC-PERFORM1d (§D3): what an attempt created rides its completion and its reconciliation, whatever its state, as receipts.</summary>
    [Fact]
    public void Resources_round_trip_on_a_completion_and_a_reconciliation()
    {
        IReadOnlyList<WorkResource> made = [new("acme.deploy/preview@1", "p-41"), new("acme.deploy/site@1", "s-41")];
        var failed = new WorkStepFrame("deploy", new WorkOutcome(WorkStates.Failed, Code: "timeout") { Resources = made }, At, "e1", Invocation: "i1");

        var frame = WorkWire.Step(failed);
        var read = WorkWire.ReadStep(Parse(frame).GetProperty("params"), out var field);

        Assert.Null(field);
        Assert.Equal(made, read!.Outcome.Resources);
        Assert.Equal(frame.ToJsonString(), WorkWire.Step(read).ToJsonString());
        Assert.Contains("""
            "resources":[{"schema":"acme.deploy/preview@1","id":"p-41"},{"schema":"acme.deploy/site@1","id":"s-41"}]
            """.Trim(), frame.ToJsonString(), StringComparison.Ordinal);

        var running = new WorkReconcileAnswer(ReconcileStates.Running) { Resources = made };
        var heard = WorkWire.ReadReconcileAnswer(Parse(WorkWire.ReconcileAnswer(running)));
        Assert.Equal(made, heard!.Resources);

        // None said is none, and an outcome said with none reads back as the one written.
        Assert.Empty(WorkWire.ReadReconcileAnswer(Parse("""{"state":"running","resources":[]}"""))!.Resources);
        var plain = new WorkOutcome(WorkStates.Reached);
        Assert.Equal(plain, WorkWire.ReadStep(Parse(WorkWire.Step(new WorkStepFrame("deploy", plain, At, "e1", Invocation: "i1"))).GetProperty("params"), out _)!.Outcome);
    }

    [Theory]
    [InlineData("""{"step":"deploy","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1","resources":{"schema":"acme.deploy/preview@1","id":"p"}}""")]
    [InlineData("""{"step":"deploy","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1","resources":[{"schema":"preview","id":"p"}]}""")]
    [InlineData("""{"step":"deploy","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1","resources":[{"schema":"acme.deploy/preview@1"}]}""")]
    [InlineData("""{"step":"deploy","state":"reached","at":"2026-10-17T09:30:00Z","id":"e1","invocation":"i1","resources":[{"schema":"acme.deploy/preview@1","id":"p"},{"schema":"acme.deploy/preview@1","id":"p"}]}""")]
    public void A_completion_naming_resources_of_the_wrong_shape_is_no_event(string json)
    {
        Assert.Null(WorkWire.ReadStep(Parse(json), out var field));
        Assert.Equal("resources", field);
    }

    /// <summary>§D3's bound on one frame's resources: eight, and one past it.</summary>
    [Theory]
    [InlineData(8, true)]
    [InlineData(9, false)]
    public void A_frames_resources_are_bounded(int count, bool reads)
    {
        var answer = new WorkReconcileAnswer(ReconcileStates.Running)
        {
            Resources = [.. Enumerable.Range(0, count).Select(at => new WorkResource("acme.deploy/preview@1", $"p-{at}"))],
        };

        Assert.Equal(reads, WorkWire.ReadReconcileAnswer(Parse(WorkWire.ReconcileAnswer(answer))) is not null);
    }

    public static TheoryData<string, string?, string?> CleanupAnswers => new()
    {
        { CleanupDispositions.Removed, "deleted:p-41", null },
        { CleanupDispositions.AlreadyGone, null, null },
        { CleanupDispositions.Retained, null, "a reused environment — 复用的环境" },
        { CleanupDispositions.Failed, null, "locked" },
    };

    /// <summary>FABRIC-PERFORM1d (§D3): <c>work/cleanup { receipt, resource, idempotencyKey }</c>, and each disposition, written and read back.</summary>
    [Theory]
    [MemberData(nameof(CleanupAnswers))]
    public void A_cleanup_call_and_each_answer_round_trip(string disposition, string? proof, string? message)
    {
        Assert.Equal("hook/work/cleanup", WorkWire.CleanupMethod);
        var call = new WorkCleanupCall("r-1", "p-41", "c-1");
        var written = WorkWire.Cleanup(call);
        Assert.Equal("""{"receipt":"r-1","resource":"p-41","idempotencyKey":"c-1"}""", written.ToJsonString());
        Assert.Equal(call, WorkWire.ReadCleanup(Parse(written)));

        var answer = new WorkCleanupAnswer(disposition, proof, message);
        Assert.Equal(answer, WorkWire.ReadCleanupAnswer(Parse(WorkWire.CleanupAnswer(answer))));
        Assert.Equal(disposition != CleanupDispositions.Failed, CleanupDispositions.Terminal(disposition));
    }

    [Theory]
    [InlineData("""{"disposition":"deleted"}""")]
    [InlineData("""{"state":"removed"}""")]
    [InlineData("""{"disposition":"removed","proof":7}""")]
    [InlineData("""["removed"]""")]
    public void A_cleanup_answer_of_the_wrong_shape_is_no_answer(string json) =>
        Assert.Null(WorkWire.ReadCleanupAnswer(Parse(json)));
}
