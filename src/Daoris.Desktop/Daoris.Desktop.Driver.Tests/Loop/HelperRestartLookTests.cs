using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A helper's pass the shell's close cut off, seen at the next start's look: the close is no answer, so the ask runs again while its asker
/// waits on it, with no word from the person; a pass that ended by itself with nothing to say is still refused as no answer.
/// </summary>
/// <remarks>The look's helper stage over <see cref="OpinionStandIn"/>, its pass a stand-in, as <c>HelperLookTests</c> drives it.</remarks>
public sealed class HelperRestartLookTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-helper-restart-" + Guid.NewGuid().ToString("N")[..8]);

    private const string Helper = "h1000001";

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-12T10:00:00Z");

    public HelperRestartLookTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private HelperFiles Files => new(_home);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_pass_the_close_cut_runs_again_and_one_that_ended_with_nothing_is_refused(bool cut)
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Helpers));
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Helpers, "a1.ask.json"), new JsonObject
        {
            ["id"] = "a1", ["asker"] = "s1", ["question"] = "Why does it drop the last day?", ["agent"] = null, ["askedBy"] = "agent",
            ["at"] = At.ToString("O"),
        }.ToJsonString());
        Files.WriteState(new HelperState("a1", HelperStates.Started, null, Helper, At));
        var host = new OpinionStandIn().Session("s1", "completed", tree: "trees/default/reports/s-1", kind: "drive")
            .Session(Helper, "stopped", adapter: "codex-acp", kind: "chat");
        host.Set(Helper, "serves", "s1");
        if (cut) host.Set(Helper, "interrupted", true);
        var driver = new Daoris.Driver.Driver(host.Client(), DriverConfig.Empty with
        {
            Actors = StaffedRoles.On("claude-code-acp"),
            Helpers = new Dictionary<string, HelperRule> { ["reports"] = new(HelperRules.On, []) },
        }, ProviderRegistry.Built(), _home)
        {
            HelperPasses = (_, _, _) => throw new InvalidOperationException("no helper starts in this look"),
        };

        var events = new List<string>();
        await driver.HelpersAsync(new Snapshot([], [], []), 0, events, CancellationToken.None);
        var until = DateTime.UtcNow.AddSeconds(15);
        while (Files.ReadState("a1")?.State == HelperStates.Started && DateTime.UtcNow < until) await Task.Delay(20);

        var state = Files.ReadState("a1")!;
        Assert.Equal(cut ? (HelperStates.Approved, null) : (HelperStates.Refused, HelperWhy.NoAnswer), (state.State, state.Code));
    }
}
