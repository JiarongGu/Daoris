using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A held asker's ask for a helper, in the look's own harness (<see cref="HelperLookTests"/>).</summary>
public sealed partial class HelperLookTests
{
    /// <summary>A held asker's ask is not begun and says so once, naming who held it; once the hold is lifted it starts.</summary>
    [Fact]
    public async Task A_held_askers_ask_stands_aside_and_says_so_once_and_starts_once_lifted()
    {
        Ask("a1");
        var host = Host();
        var pass = new Pass(Files) { Answer = "Line 42." };
        var driver = Driver(host, pass);
        var hold = new HoldEvent(At.AddMinutes(-2), PriorityBook.Quest, "q1", "games", true, "make room", "the person");
        Snapshot Of(params HoldEvent[] holds) =>
            new([new QuestView("q1", "Asker", "reports", "One", "Why.", "Taken")], [], [new SessionView("s1", "reports", "working") { Quest = "q1" }])
            {
                Priority = new PriorityLook([], PriorityRules.Default, At) { Holds = holds },
            };

        var first = new List<string>();
        await driver.HelpersAsync(Of(hold), 0, first, CancellationToken.None);
        var again = new List<string>();
        await driver.HelpersAsync(Of(hold), 0, again, CancellationToken.None);

        Assert.Empty(pass.Asked);
        Assert.Contains(first, line => line.Contains("stands aside, held by the person: make room"));
        Assert.DoesNotContain(again, line => line.Contains("stands aside"));

        await driver.HelpersAsync(Of(hold, hold with { At = At.AddMinutes(-1), Held = false }), 0, [], CancellationToken.None);
        await Until(() => host.HelperHands.Count == 1);
        Assert.Single(pass.Asked);
    }
}
