using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A second check's flag rides the person's next words at their dispatch, whichever way they go to the door: taken from the queue as a
/// turn, or handed to the running turn at its next step. It is told once that word is written, and a later word carries only what is
/// still pending.
/// </summary>
public sealed class AskDaorisToldDispatchTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "ask-daoris-told", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    public void Dispose() => GitSeed.Discard(_home);

    private static async Task Until(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done() && watch.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(10);
        Assert.True(done());
    }

    private void Flagged(AskDaorisReports reports, string range, string call) =>
        reports.Write(new AskDaorisReport(range, "s1", AskDaorisStates.Read, At, [], [])
        {
            Reader = new SupervisorAgentHolder("second-check", 1, "codex-acp", null, null, null, OpinionPosture.PacketOnly, "s-read"),
            Independence = ReadIndependence.AnotherMaker,
            Findings = [new AskDaorisFinding(AskDaorisFinding.Flag, "sure", "a push not asked for", call, "none")],
        });

    [Fact]
    public async Task A_flag_rides_a_pumped_word_and_a_steered_word_each_once_and_is_kept_told_when_written()
    {
        var reports = new AskDaorisReports(_home);
        Flagged(reports, "r1", "p7");
        var (taken, steered) = (new List<string>(), new List<string>());
        var release = new TaskCompletionSource();
        var turns = new ChatTurns(
            ready: () => Task.FromResult(true),
            take: async (message, sent) =>
            {
                lock (taken) taken.Add(message.Text);
                sent();
                await release.Task;
            },
            interrupt: () => Task.CompletedTask, changed: _ => { },
            steer: message =>
            {
                lock (steered) steered.Add(message.Text);
                message.Went?.Invoke();
                return Task.CompletedTask;
            },
            nextStep: () => true)
        {
            Ahead = new AskDaorisTold(_home, "s1", () => At.AddHours(1)).Ahead,
        };

        turns.Say(new ChatMessage("first", []));
        await Until(() => { lock (taken) return taken.Count == 1; });
        Assert.StartsWith("Second check (codex-acp, another maker) flagged call p7", taken[0]);
        Assert.EndsWith("\n\nfirst", taken[0]);
        Assert.NotNull(reports.Find("s1", "r1")!.Findings[0].Told);

        Flagged(reports, "r2", "p8");
        turns.Say(new ChatMessage("second", []));
        await Until(() => { lock (steered) return steered.Count == 1; });
        Assert.Contains("flagged call p8", steered[0]);
        Assert.DoesNotContain("p7", steered[0]);
        Assert.NotNull(reports.Find("s1", "r2")!.Findings[0].Told);

        turns.Say(new ChatMessage("third", []));
        await Until(() => { lock (steered) return steered.Count == 2; });
        Assert.Equal("third", steered[1]);
        release.SetResult();
    }
}
