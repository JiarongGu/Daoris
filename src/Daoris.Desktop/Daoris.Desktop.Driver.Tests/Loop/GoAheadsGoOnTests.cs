using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GOAHEAD2, the owner's case on the install (2026-10-08): a driven session asked two go-aheads on its ask and parked, the
/// person answered both on the ask, and the session stayed waiting on them for minutes, until the same words were said to
/// it. The answers to the go-aheads a parked session asked are its answer: the one that leaves none of them waiting goes on
/// with the same session, once, as an answer to its park does (D131, ANSWER2), its turn carrying each go-ahead's answer.
/// </summary>
/// <remarks>
/// Driven over the real client, the real planner and a look's real scheduling, as <c>AnswerGoesOnOnceTests</c> is. The
/// service's doors stand in, in-process: its go-ahead door keeps the park's blank answer as the service does
/// (<c>SessionLedger.GoOnWithGoAheadsAsync</c>, held by the service's <c>GoAheadTests</c>). The resumed run stands in for
/// <c>Driver.ContinueAsync</c>, its turn composed by the driver's own <see cref="Daoris.Driver.Driver.ResumedAfterAsync"/> from
/// the ask as the real client reads it. The real resume over a protocol stub is the <c>Process</c> half's.
/// </remarks>
public sealed class GoAheadsGoOnTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-goahead2-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(TimeSpan.FromSeconds(60));

    public GoAheadsGoOnTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        _ledger.Publish("q1", "engine", from: "ask #a1");
        _ledger.Park("s1", "q1", Path.Combine(_home, "trees", "s-1"));
        _ledger.GoAheadAsked("a1", "s1", "write", "production", "dashboard configuration");
        _ledger.GoAheadAsked("a1", "s1", "release", "production", "comparison report");
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// 🔴 The row's own case, through the terminal's door (<c>daoris-driver ask --go-ahead</c>), the ask page's twin: the first
    /// of two answered keeps the session parked and begins nothing; the second, the last it waited on, sends it on at the next
    /// look, once though a look comes while it works, its turn the park's answer and then both go-aheads' answers, each saying
    /// which, approved or refused, and the person's words.
    /// </summary>
    [Fact]
    public async Task Answering_the_last_go_ahead_a_parked_session_asked_goes_on_with_it_once_and_the_first_of_two_does_not()
    {
        var (driver, runs) = Driver();
        var service = _ledger.Client();

        var first = await service.AnswerGoAheadAsync("a1", 1, approved: true, "run the put", goesOn: true);
        var waiting = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.True(first.Ok, first.Message);
        Assert.DoesNotContain(waiting.Considerations, c => c.GoesOn || c.Verdict == StartVerdict.Start);
        Assert.Equal(("awaiting-person", 0), (State("s1"), Said("s1")));

        var last = await service.AnswerGoAheadAsync("a1", 2, approved: false, "not on production yet", goesOn: true);
        var goes = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        await Poll.Until(() => State("s1") == "working", () => Seen(), Bound);
        var during = await driver.TickAsync(_closing.Token).WaitAsync(Bound);
        runs.End();
        await driver.Running.EndedAsync(_closing.Token).WaitAsync(Bound);
        await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.True(last.Ok, last.Message);
        var planned = Assert.Single(goes.Considerations);
        Assert.Equal((StartVerdict.Start, true, "s1"), (planned.Verdict, planned.GoesOn, planned.Resumes?.Session));
        Assert.DoesNotContain(during.Considerations, c => c.Verdict == StartVerdict.Start || c.GoesOn);
        Assert.True(runs.Began == 1, Seen());
        Assert.True(_ledger.MovesOf("s1").SequenceEqual(["working", "completed"]), Seen());
        Assert.Equal(0, Said("s1"));

        var turn = Assert.Single(runs.Turns);
        Assert.Equal(2, turn.Count);
        Assert.Equal("carry on.", turn[0]);
        Assert.StartsWith("The person has also answered the go-aheads you asked on this ask:", turn[1], StringComparison.Ordinal);
        Assert.Contains("- Go-ahead 1, write on production: \"dashboard configuration\" — approved", turn[1], StringComparison.Ordinal);
        Assert.Contains("  > run the put", turn[1], StringComparison.Ordinal);
        Assert.Contains("- Go-ahead 2, release on production: \"comparison report\" — refused", turn[1], StringComparison.Ordinal);
        Assert.Contains("  > not on production yet", turn[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// The client's door leaves a park to its caller unless told otherwise: the session page's door answers the go-ahead and
    /// then the park itself, with the person's words alone (KNOWUSE1a2), so the go-ahead's answer keeps nothing on the park.
    /// </summary>
    [Fact]
    public async Task A_go_ahead_answered_by_a_door_that_answers_the_park_itself_keeps_nothing_on_the_park()
    {
        var (driver, runs) = Driver();
        var service = _ledger.Client();

        await service.AnswerGoAheadAsync("a1", 1, approved: true, null);
        await service.AnswerGoAheadAsync("a1", 2, approved: true, null);
        var look = await driver.TickAsync(_closing.Token).WaitAsync(Bound);

        Assert.DoesNotContain(look.Considerations, c => c.GoesOn);
        Assert.Equal(("awaiting-person", 0, 0), (State("s1"), Said("s1"), runs.Began));
    }

    private string State(string session) => _ledger.Session(session)["state"]!.GetValue<string>();

    private int Said(string session) => _ledger.Session(session)["said"]?.AsArray().Count ?? 0;

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
    /// A run that goes on in the record the person's words wait on, standing in for <c>Driver.ContinueAsync</c>: told it
    /// opened, its record moved to working, its turn composed as the real run composes it (the words waiting, then what the
    /// driver adds after them) and kept for the test, then working until the test ends it, its words taken off by their ids.
    /// </summary>
    private sealed class GoOnRuns(ServiceClient service)
    {
        private readonly TaskCompletionSource _ending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<IReadOnlyList<string>> _turns = [];
        private int _began;

        /// <summary>How many runs were begun, on any record.</summary>
        public int Began => Volatile.Read(ref _began);

        /// <summary>Each run's turn as the protocol door prompts it: a block per word, then the driver's appendix.</summary>
        public IReadOnlyList<IReadOnlyList<string>> Turns
        {
            get { lock (_turns) return [.. _turns]; }
        }

        public Func<Consideration, Action, CancellationToken, Task<StartRun>> Runner => RunAsync;

        /// <summary>End the run that works: its words taken and its record completed.</summary>
        public void End() => _ending.TrySetResult();

        private async Task<StartRun> RunAsync(Consideration start, Action opened, CancellationToken ct)
        {
            var quest = start.Quest;
            var record = start.Resumes ?? throw new InvalidOperationException($"the stand-in only goes on; #{quest.Id} was planned as a start");
            Interlocked.Increment(ref _began);
            opened();
            await service.AdvanceAsync(record.Session, "working", Continuations.WorkingNoted, ct: ct);

            var resume = new ResumeAsk(
                "conversation-1", record.Waiting, "It goes on.",
                await Daoris.Driver.Driver.ResumedAfterAsync(service, quest, record.Session, language: null, ct));
            lock (_turns) _turns.Add(resume.Blocks);

            await _ending.Task.WaitAsync(ct);
            await service.TakenAsync(record.Session, resume.Ids, ct: CancellationToken.None);
            await service.AdvanceAsync(record.Session, "completed", note: "Did what the go-aheads allowed.", ct: CancellationToken.None);
            return new StartRun(
                $"completed  session {record.Session} (#{quest.Id} → {quest.To}) [resumed its conversation]: Did what the go-aheads allowed.",
                true, new SessionEnded(record.Session, quest.To, "completed", ByPerson: false, Quest: quest.Id));
        }
    }
}
