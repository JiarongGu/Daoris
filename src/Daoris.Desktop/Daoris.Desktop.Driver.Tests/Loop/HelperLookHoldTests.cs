using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A held asker's ask for a helper, in <see cref="HelperLookTests"/>'s harness: it is not begun, says so once, and starts once the hold is lifted.</summary>
public sealed class HelperLookHoldTests : IDisposable
{
    private readonly HelperLookTests _look = new();

    public void Dispose() => _look.Dispose();

    private static Snapshot Of(params HoldEvent[] holds) =>
        new([new QuestView("q1", "Asker", "reports", "One", "Why.", "Taken")], [], [new SessionView("s1", "reports", "working") { Quest = "q1" }])
        {
            Priority = new PriorityLook([], PriorityRules.Default, HelperLookTests.At) { Holds = holds },
        };

    [Fact]
    public async Task A_held_askers_ask_stands_aside_says_so_once_and_starts_once_lifted()
    {
        _look.Ask("a1");
        var host = _look.Host();
        var pass = new HelperLookTests.Pass(_look.Files) { Answer = "Line 42." };
        var driver = _look.Driver(host, pass);
        var hold = new HoldEvent(HelperLookTests.At.AddMinutes(-2), PriorityBook.Quest, "q1", "games", true, "make room", "the person");
        List<string> first = [], again = [];

        await driver.HelpersAsync(Of(hold), 0, first, CancellationToken.None);
        await driver.HelpersAsync(Of(hold), 0, again, CancellationToken.None);

        Assert.Empty(pass.Asked);
        Assert.Contains(first, line => line.Contains("stands aside, held by the person: make room"));
        Assert.DoesNotContain(again, line => line.Contains("stands aside"));

        await driver.HelpersAsync(Of(hold, hold with { At = HelperLookTests.At.AddMinutes(-1), Held = false }), 0, [], CancellationToken.None);
        await _look.Until(() => host.HelperHands.Count == 1);
        Assert.Single(pass.Asked);
    }
}
