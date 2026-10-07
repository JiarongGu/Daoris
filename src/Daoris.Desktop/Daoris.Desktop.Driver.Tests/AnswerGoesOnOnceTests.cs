using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ANSWER2, the owner's case on the install (2026-10-08): two driven sessions parked to ask the person, the person answered
/// each from the window, and each then ended <c>failed</c> with the service refusing <i>working → working</i>. The answer's
/// path keeps the words on the record and moves nothing; the first look takes the park up and moves it to working, and its
/// run takes the words off only as it concludes. A look while it worked read the words still on it and took it up a second
/// time, whose move to working the ledger refused, and the driver failed the session it was resuming.
/// </summary>
/// <remarks>
/// Driven over the real client, the real planner and a look's real scheduling, with the service's doors and the resumed run
/// standing in, in-process (DEV3's way): the run stands in for <c>Driver.ContinueAsync</c>, told it opened before its working
/// move as the real one is, and failing its record with the ledger's sentence where that move is refused, as the real one
/// does. The real resume over a protocol stub is <c>AnswerContinuesTickTests</c>' and <c>SessionMessagesTickTests</c>', in the
/// <c>Process</c> half.
/// </remarks>
public sealed class AnswerGoesOnOnceTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-answer2-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(TimeSpan.FromSeconds(60));

    public AnswerGoesOnOnceTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        _ledger.Publish("q1", "engine");
        _ledger.Park("s1", "q1", Path.Combine(_home, "trees", "s-1"));
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// 🔴 The row's own case: the person answers through the say door the window's box reaches (<c>SESSION_INPUT</c> →
    /// <c>SessionWords</c> → <c>ServiceClient.SayAsync</c>), the next look takes the park up, and a look comes while the resumed
    /// run works with the answer still on its record. The record moves to working once, the session carries on, and it ends as
    /// its run does, the answer taken off it.
    /// </summary>
    [Fact]
    public async Task An_answered_park_goes_on_once_though_a_look_comes_while_it_works()
    {
        var (driver, runs) = Driver();
        var said = await _ledger.Client().SayAsync("s1", "Port 8080.");
        Assert.True(said.Kept, said.Message);

        var first = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        await Poll.Until(() => State("s1") == "working", () => Seen(), Bound);
        var second = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        runs.End();
        await driver.Running.EndedAsync(_closing.Token).WaitAsync(Bound);
        var third = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        var planned = Assert.Single(first.Considerations);
        Assert.Equal((StartVerdict.Start, true, "s1"), (planned.Verdict, planned.GoesOn, planned.Resumes?.Session));
        Assert.DoesNotContain(second.Considerations, c => c.Verdict == StartVerdict.Start || c.GoesOn);
        Assert.True(runs.Began == 1, Seen());
        Assert.True(_ledger.MovesOf("s1").SequenceEqual(["working", "completed"]), Seen());
        Assert.DoesNotContain("cannot move", _ledger.Session("s1")["note"]?.GetValue<string>() ?? "", StringComparison.Ordinal);
        Assert.Empty(_ledger.Session("s1")["said"]!.AsArray());
        Assert.Equal(("s1", "completed"), (Assert.Single(third.Concluded).Session, third.Concluded[0].State));
    }

    /// <summary>
    /// 🔴 The moment before the move: a run that took the park up is told it opened before its process starts and moves the
    /// record, so a look in between still reads it parked with the answer and plans it to go on. The look begins no second run
    /// on that record, and says why; the first moves it to working once and carries on.
    /// </summary>
    [Fact]
    public async Task A_look_before_the_resumed_run_moved_its_record_begins_no_second_run_on_it()
    {
        var (driver, runs) = Driver();
        var moving = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runs.Moving = moving.Task;
        await _ledger.Client().SayAsync("s1", "Port 8080.");

        await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        var between = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        moving.TrySetResult();
        await Poll.Until(() => State("s1") == "working", () => Seen(), Bound);
        runs.End();
        await driver.Running.EndedAsync(_closing.Token).WaitAsync(Bound);

        Assert.True(runs.Began == 1, Seen());
        Assert.Contains(
            "going on  session s1 (#q1 → engine): a run of this machine's already goes on with the person's words in it.", between.Events);
        Assert.True(_ledger.MovesOf("s1").SequenceEqual(["working", "completed"]), Seen());
        Assert.Equal("completed", State("s1"));
    }

    /// <summary>
    /// Once the run that took the record up has ended, the record is the next run's to take up: words said to it after it ended
    /// go on in it again, in one run.
    /// </summary>
    [Fact]
    public async Task Words_said_after_the_run_ended_go_on_again_in_one_run()
    {
        var (driver, runs) = Driver();
        await _ledger.Client().SayAsync("s1", "Port 8080.");
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        await Poll.Until(() => State("s1") == "working", () => Seen(), Bound);
        runs.End();
        await driver.Running.EndedAsync(_closing.Token).WaitAsync(Bound);
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        runs.Again();
        var said = await _ledger.Client().SayAsync("s1", "And log it.");
        Assert.True(said.Kept, said.Message);
        var reopened = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        await Poll.Until(() => State("s1") == "working", () => Seen(), Bound);
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        runs.End();
        await driver.Running.EndedAsync(_closing.Token).WaitAsync(Bound);

        Assert.True(Assert.Single(reopened.Considerations).GoesOn);
        Assert.True(runs.Began == 2, Seen());
        Assert.True(_ledger.MovesOf("s1").SequenceEqual(["working", "completed", "working", "completed"]), Seen());
    }

    /// <summary>
    /// 🔴 The unobserved task beside each failure: a run the driver gives up before it awaited what it was reading (here, the
    /// record refusing to move) leaves that capture to fail on its own. Observed, its failure is written with its reason, to the
    /// machine log as an <c>error</c> that names its place, and to the session's console, never left to the finalizer.
    /// </summary>
    [Fact]
    public async Task A_given_up_runs_capture_is_observed_and_its_failure_written_with_its_reason()
    {
        var at = new DateTimeOffset(2026, 10, 8, 20, 43, 37, TimeSpan.Zero);
        using var log = new MachineLog(_home, "desktop", () => at);
        var output = new SessionOutput();
        var capture = Task.FromException(new IOException("The process cannot access the file 's1.log' because it is being used by another process."));

        await Daoris.Driver.Driver.AbandonedAsync(process: null, capture, "s1", log, output);

        var line = Assert.Single(StubFile.Lines(Path.Combine(_home, MachineLog.Folder, "2026-10-08.desktop.jsonl")));
        Assert.Contains("\"level\":\"error\",\"event\":\"error\"", line);
        Assert.Contains($"\"where\":\"{Daoris.Driver.Driver.AbandonedRun}\"", line);
        Assert.Contains("\"type\":\"System.IO.IOException\"", line);
        Assert.Contains("because it is being used by another process.", line);
        Assert.Contains(
            output.Tail("s1").Lines,
            said => said.Text == "— the run ended here, since its record would not move, failed as its output was read: "
                                 + "IOException: The process cannot access the file 's1.log' because it is being used by another process.");
    }

    /// <summary>A capture that ended cleanly, or was cancelled with its run, is no failure: nothing is written.</summary>
    [Fact]
    public async Task A_given_up_runs_capture_that_ended_cleanly_writes_nothing()
    {
        using var log = new MachineLog(_home, "desktop");
        var output = new SessionOutput();

        await Daoris.Driver.Driver.AbandonedAsync(process: null, Task.CompletedTask, "s1", log, output);
        await Daoris.Driver.Driver.AbandonedAsync(process: null, Task.FromCanceled(new CancellationToken(canceled: true)), "s1", log, output);

        Assert.False(Directory.Exists(Path.Combine(_home, MachineLog.Folder)) && Directory.EnumerateFiles(Path.Combine(_home, MachineLog.Folder)).Any());
        Assert.Empty(output.Tail("s1").Lines);
    }

    /// <summary>
    /// The point of it: the given-up capture's failure never reaches the finalizer, where it was an AggregateException whose only
    /// place was <i>an unobserved task</i>. The same failure left alone does, which shows the probe can see one.
    /// </summary>
    [Fact]
    public async Task A_given_up_runs_capture_never_reaches_the_finalizer()
    {
        Assert.False(await ReachesTheFinalizerAsync(capture => Daoris.Driver.Driver.AbandonedAsync(null, capture, "s1", log: null, output: null)));
        Assert.True(await ReachesTheFinalizerAsync(observe: null));
    }

    /// <summary>
    /// Whether a failed capture, observed by <paramref name="observe"/> or not at all, has its exception rethrown by the
    /// finalizer, recognised by a message nobody else's task carries (MachineLogTests' probe).
    /// </summary>
    private static async Task<bool> ReachesTheFinalizerAsync(Func<Task, Task>? observe)
    {
        var marker = "answer2-" + Guid.NewGuid().ToString("N");
        var reached = false;
        void Seen(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception.InnerExceptions.Any(inner => inner.Message == marker)) Volatile.Write(ref reached, true);
        }

        TaskScheduler.UnobservedTaskException += Seen;
        try
        {
            await FailUnawaitedAsync(marker, observe);
            for (var attempt = 0; attempt < 10 && !Volatile.Read(ref reached); attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                await Task.Delay(20);
            }

            return Volatile.Read(ref reached);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Seen;
        }
    }

    /// <summary>A failed task made in a frame of its own, so nothing here keeps it alive once it is let go.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Task FailUnawaitedAsync(string marker, Func<Task, Task>? observe)
    {
        var failed = Task.FromException(new IOException(marker));
        return observe is null ? Task.CompletedTask : observe(failed);
    }

    private string State(string session) => _ledger.Session(session)["state"]!.GetValue<string>();

    /// <summary>The record and its moves, for a failure to say whole.</summary>
    private string Seen() => $"record: {_ledger.Session("s1").ToJsonString()}\nmoves: {string.Join(", ", _ledger.MovesOf("s1"))}";

    private (Daoris.Driver.Driver Driver, GoOnRuns Runs) Driver()
    {
        var service = _ledger.Client();
        var runs = new GoOnRuns(service);
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with { Drivable = ["engine"], Adapter = "stub", Cap = 3, PollSeconds = 1, Trees = ["engine"] };
        var driver = new Daoris.Driver.Driver(
            service, config, adapters, _home, harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")))
        {
            Runner = runs.Runner,
        };
        return (driver, runs);
    }

    /// <summary>
    /// A run that goes on in the record the person's words wait on, standing in for <c>Driver.ContinueAsync</c> (ANSWER1a,
    /// MSG1b): told it opened at once, as the real run is before its process starts; then, once <see cref="Moving"/> lets it,
    /// its record moved to working, a refusal there failing the record with the ledger's sentence as the real run's catch
    /// does; then working until the test ends it, its words taken off by their ids and the record completed.
    /// </summary>
    private sealed class GoOnRuns(ServiceClient service)
    {
        private TaskCompletionSource _ending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _began;

        /// <summary>How many runs were begun, on any record.</summary>
        public int Began => Volatile.Read(ref _began);

        /// <summary>What a run waits on between being told it opened and moving its record: done at once unless a test holds it.</summary>
        public Task Moving { get; set; } = Task.CompletedTask;

        public Func<Consideration, Action, CancellationToken, Task<StartRun>> Runner => RunAsync;

        /// <summary>End the run that works: its words taken and its record completed.</summary>
        public void End() => _ending.TrySetResult();

        /// <summary>A later run waits for its own end.</summary>
        public void Again() => _ending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        private async Task<StartRun> RunAsync(Consideration start, Action opened, CancellationToken ct)
        {
            var quest = start.Quest;
            var record = start.Resumes ?? throw new InvalidOperationException($"the stand-in only goes on; #{quest.Id} was planned as a start");
            var ending = _ending.Task;
            Interlocked.Increment(ref _began);
            opened();
            await Moving.WaitAsync(ct);

            try
            {
                await service.AdvanceAsync(record.Session, "working", Continuations.WorkingNoted, ct: ct);
            }
            catch (DriverException error)
            {
                await service.AdvanceAsync(record.Session, "failed", Observation.Failure(error.Message), ct: CancellationToken.None);
                return new StartRun(
                    $"failed  session {record.Session} (#{quest.Id} → {quest.To}): {error.Message}", true,
                    new SessionEnded(record.Session, quest.To, "failed", ByPerson: false, error.Message, Quest: quest.Id));
            }

            await ending.WaitAsync(ct);
            await service.TakenAsync(record.Session, [.. record.Waiting.Select(word => word.Id).OfType<string>()], ct: CancellationToken.None);
            await service.AdvanceAsync(record.Session, "completed", note: "Listening on the port the person named.", ct: CancellationToken.None);
            return new StartRun(
                $"completed  session {record.Session} (#{quest.Id} → {quest.To}) [resumed its conversation]: Listening on the port the person named.",
                true, new SessionEnded(record.Session, quest.To, "completed", ByPerson: false, Quest: quest.Id));
        }
    }
}
