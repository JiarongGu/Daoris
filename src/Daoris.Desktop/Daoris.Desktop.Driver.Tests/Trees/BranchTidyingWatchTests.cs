using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTOTIDY1 over real git, through the watch: its look runs the tidy beside it. A class of its own (PROC1g): the watch ticks on
/// its poll, which a neighbour's load could hold, so it runs one at a time, where the look's other cases run four at once
/// (<see cref="BranchTidyingFixture"/>).
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class BranchTidyingWatchTests : BranchTidyingFixture
{
    /// <summary>The watch's look runs the tidy beside it, and a later look's report says what went.</summary>
    [Fact]
    public async Task The_watch_s_look_removes_an_empty_branch_and_says_so_in_a_report()
    {
        var root = await RepositoryAsync("engine");
        var landed = await LandedTreeAsync(new SessionTrees(_home), root);
        _ledger.Register("engine", root);
        var config = Path.Combine(_home, "driver.json");
        await File.WriteAllTextAsync(config, """{ "drivable": [], "pollSeconds": 1 }""");
        using var log = Log();
        var watch = new DriverWatch(
            _ledger.Client(), config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")))
        {
            Log = log,
            Tidying = new BranchTidying(_home) { Pace = TimeSpan.FromMinutes(5) },
        };
        var said = new List<string>();
        var watching = watch.RunAsync(
            (report, _) =>
            {
                lock (said) said.AddRange(report.Events.Where(line => line.StartsWith("tidy", StringComparison.Ordinal)));
                return Task.CompletedTask;
            },
            onError: null, _closing.Token);
        try
        {
            await Poll.Until(() => { lock (said) return said.Count > 0; }, () => "no look said the tidy", Bound);

            lock (said) Assert.StartsWith(Removed(landed), Assert.Single(said), StringComparison.Ordinal);
            Assert.DoesNotContain(landed.Branch, await GitAsync(root, "branch", "--list", "daoris/*"), StringComparison.Ordinal);
            Assert.Single(Events("branch.tidied"));
        }
        finally
        {
            await _closing.CancelAsync();
            try { await watching.WaitAsync(Bound); } catch (OperationCanceledException) { }
        }
    }
}
