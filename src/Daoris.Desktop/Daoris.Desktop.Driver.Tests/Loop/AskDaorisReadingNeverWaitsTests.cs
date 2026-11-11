using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A reader that hangs, answers slowly, answers no readable block or is over its budget delays nothing else: the supervisors' look hands
/// the queue its ranges and goes on at once, a decision's supervisor still starts in that look, and the conversation's next turn is
/// selected into a range of its own while the taken one is read.
/// </summary>
public sealed class AskDaorisReadingNeverWaitsTests : IDisposable
{
    private static readonly SupervisorWaiting Point =
        new(SupervisorKinds.Course, SupervisorSubject.Point("s2", "p-1"), "reports", "agent:supervisor@0", ["go-on"]) { Point = ("answers-alone", "3", "r1", "machine") };

    private readonly AskDaorisReadingQueueTests _queue = new();

    private readonly CancellationTokenSource _release = new();

    public void Dispose()
    {
        _release.Cancel();
        _release.Dispose();
        _queue.Dispose();
    }

    private sealed class Desk : ISupervisorDesk
    {
        public Task<IReadOnlyList<SupervisorWaiting>> WaitingAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SupervisorWaiting>>([Point]);

        public Task<string?> FactsAsync(SupervisorWaiting waiting, string fence, CancellationToken ct) => Task.FromResult<string?>("");

        public Task<SupervisorBlockKept> KeepAsync(SupervisorWaiting waiting, SupervisorBlock block, SupervisorHolder holder, CancellationToken ct) =>
            Task.FromResult(new SupervisorBlockKept(true, null));
    }

    private async Task<SuperviseRunEnd> Hangs(CancellationToken ct)
    {
        await Task.Delay(Timeout.Infinite, CancellationTokenSource.CreateLinkedTokenSource(ct, _release.Token).Token);
        return new SuperviseRunEnd("completed", null, null, OpinionPosture.PacketOnly);
    }

    private async Task<SuperviseRunEnd> Slow(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), CancellationTokenSource.CreateLinkedTokenSource(ct, _release.Token).Token);
        return new SuperviseRunEnd("completed", AskDaorisReadingQueueTests.Answer(), "t-1", OpinionPosture.PacketOnly);
    }

    [Theory]
    [InlineData("hangs")]
    [InlineData("slow")]
    [InlineData("malformed")]
    [InlineData("over-budget")]
    public async Task A_reader_that_hangs_is_slow_malformed_or_over_budget_delays_no_look_no_start_and_no_next_turn(string reader)
    {
        var harness = _queue.Set(
            (ask, ct) => ask.Waiting.Kind != SupervisorKinds.AskDaoris
                ? Task.FromResult(new SuperviseRunEnd("completed", null, "t-2", OpinionPosture.RulesAndCopy))
                : reader switch
                {
                    "hangs" => Hangs(ct),
                    "slow" => Slow(ct),
                    _ => AskDaorisReadingQueueTests.Ended("completed", "I could not decide."),
                },
            left => reader == "over-budget"
                ? new ReviewerChoice(null, null, ReviewerUnavailable.Refused, "Claude Code is over its budget: 40 starts today.")
                : AskDaorisReadingQueueTests.Another(left),
            decisions: true);
        harness.Driver.Context.Supervising = new Desk();
        var taken = _queue.Queued();
        var look = harness.Driver.LookPasses.In(LookPhase.Planned).OfType<SuperviseLook>().Single();

        var clock = Stopwatch.StartNew();
        await look.RunAsync(new LookRound(LookPart.Look, [], new LookState([])), CancellationToken.None);
        var looked = clock.Elapsed;
        var next = new ReadTurn("w-b", 5, 8, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false, "Now tidy up.",
            [new SelectedCall("toolu_rm", SelectedWhy.Act, "delete", "rm", null, "completed", [], null, null, [])], [], [], new Dictionary<string, int>(), []);
        if (reader is "hangs" or "slow") await AskDaorisReadingQueueTests.Until(() => _queue.Reports.Find("s1", taken.Range)!.State == AskDaorisStates.Running);
        clock.Restart();
        var joined = new AskDaorisRanges(_queue.Reports, () => DateTimeOffset.UtcNow).Take("s1", next, on: true);

        Assert.True(looked < TimeSpan.FromSeconds(2), $"the look waited {looked} on the reading");
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"the next turn waited {clock.Elapsed}");
        await AskDaorisReadingQueueTests.Until(() => harness.Asked.Any(ask => ask.Waiting.Subject == Point.Subject));
        if (reader is "hangs" or "slow") Assert.NotEqual(taken.Range, joined!.Range);
        var expected = reader switch { "hangs" or "slow" => AskDaorisStates.Running, "malformed" => AskDaorisStates.Unread, _ => AskDaorisStates.Queued };
        await AskDaorisReadingQueueTests.Until(() => _queue.Reports.Find("s1", taken.Range)!.State == expected);
    }
}
