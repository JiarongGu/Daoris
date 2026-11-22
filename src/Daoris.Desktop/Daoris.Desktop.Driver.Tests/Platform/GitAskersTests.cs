using Daoris.Driver;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 Every git ask is counted by its caller (the look's stage, the opinion gate, a door's read, a machine-facts topic, a check, the run-step
/// consumer) with the time it waited for its place: said in the look's stage line, and by the day in `daoris bus stats`, batched into the
/// bus's measures, never one write per git.
/// </summary>
public sealed class GitAskersTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "git-askers", Guid.NewGuid().ToString("N")[..8]);

    public GitAskersTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private static GitRunAsk Read(string repository) =>
        new(Path.GetFullPath(Path.Combine("_askers", repository)), Path.GetFullPath(Path.Combine("_askers", repository)), GitWait.Read);

    [Fact]
    public async Task The_scheduler_counts_each_ask_by_the_caller_the_flow_names_and_the_time_it_waited()
    {
        var tally = new GitAskTally();
        var scheduler = new GitScheduler(4, 2, 2) { Said = (_, _) => { }, Tally = tally };

        async Task AskAsync(string caller, int at)
        {
            using (GitAskers.Is(caller)) await scheduler.RunAsync(Read($"r{at}"), _ => Task.FromResult(at), CancellationToken.None);
        }

        await AskAsync(GitAskers.Gate, 1);
        await AskAsync(GitAskers.Gate, 2);
        await AskAsync(GitAskers.Door, 3);
        await scheduler.RunAsync(Read("r4"), _ => Task.FromResult(4), CancellationToken.None);

        var taken = tally.Take().ToDictionary(row => row.Caller);
        Assert.Equal(2, taken[GitAskers.Gate].Count);
        Assert.Equal(1, taken[GitAskers.Door].Count);
        Assert.Equal(1, taken[GitAskers.Other].Count);
        Assert.Empty(tally.Take());
    }

    [Fact]
    public async Task A_span_says_its_flows_asks_as_the_looks_unless_a_caller_inside_names_itself()
    {
        var tally = new GitAskTally();
        var scheduler = new GitScheduler(4, 2, 2) { Said = (_, _) => { }, Tally = tally };
        var stages = new LookStages { Asks = tally };

        await stages.TimeAsync("start", async () =>
        {
            await scheduler.RunAsync(Read("a"), _ => Task.FromResult(1), CancellationToken.None);
            using (GitAskers.Is(GitAskers.Check)) await scheduler.RunAsync(Read("b"), _ => Task.FromResult(1), CancellationToken.None);
            return 0;
        });

        var taken = tally.Take().ToDictionary(row => row.Caller);
        Assert.Equal(1, taken[GitAskers.Look].Count);
        Assert.Equal(1, taken[GitAskers.Check].Count);
    }

    [Fact]
    public async Task The_stats_verb_sums_each_callers_asks_and_names_the_top_callers_and_kinds_over_the_looks_that_told_them()
    {
        await using (var bus = BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = new MovedClock() }))
        {
            bus.GitAsked(GitAskers.Gate, "status", 2, 340);
            bus.GitAsked(GitAskers.Gate, "status", 3, 60);
            bus.GitAsked(GitAskers.Gate, "diff", 1, 10);
            bus.GitAsked(GitAskers.Door, "log", 1, 5);
        }

        Assert.Equal(0, BusStatsCommand.Run(["stats"], _home, () => new MovedClock().GetUtcNow(), out var said));
        Assert.Contains("git asks by caller: door n=1 waited 5 ms; opinion-gate n=6 waited 410 ms", said, StringComparison.Ordinal);
        Assert.Contains("git asks by caller and kind (top 12): opinion-gate/status n=5 waited 400 ms; door/log n=1 waited 5 ms; opinion-gate/diff n=1 waited 10 ms", said, StringComparison.Ordinal);
    }
}
