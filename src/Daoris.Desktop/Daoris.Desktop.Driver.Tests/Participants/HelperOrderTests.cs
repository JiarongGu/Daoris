using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The asks for a helper (a second reading) that wait for a place under the cap go in the one order: the class of the asker's quest, aged from
/// the ask's arrival, then the order asked. A held asker's ask is not begun, and what it does not begin it keeps as found.
/// </summary>
public sealed class HelperOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private static HelperAsk Ask(string id, string asker, int minutesAgo) =>
        new(id, asker, "Why does it drop the last day?", null, HelperAsk.ByAgent, Now.AddMinutes(-minutesAgo));

    private static Snapshot Of(PriorityEvent[]? events = null, HoldEvent[]? holds = null) =>
        new([new QuestView("q1", "Asker", "reports", "One", "Why.", "Taken"), new QuestView("q2", "ask #cafee7", "reports", "Two", "Why.", "Taken")], [],
            [new SessionView("s1", "reports", "working") { Quest = "q1" }, new SessionView("s2", "reports", "working") { Quest = "q2" }])
        {
            Priority = new PriorityLook(events ?? [], PriorityRules.Default, Now) { Holds = holds ?? [] },
        };

    private static PriorityEvent Set(string target, string id, string @class) =>
        new(Now.AddMinutes(-1), target, id, "games", @class, "it matters", "the person") { Until = @class == PriorityClasses.Urgent ? Now.AddHours(1) : null };

    private static string[] Order(HelperAsk[] asks, Snapshot snapshot) => [.. HelperOrder.Of(asks, snapshot).Select(each => each.Id)];

    [Fact]
    public void An_urgent_askers_ask_goes_before_an_older_normal_one_and_the_rest_keep_the_order_asked()
    {
        var asks = new[] { Ask("a1", "s1", 30), Ask("a2", "s2", 20), Ask("a3", "s9", 10) };

        Assert.Equal(["a1", "a2", "a3"], Order(asks, Of()));
        Assert.Equal(["a2", "a1", "a3"], Order(asks, Of([Set(PriorityBook.Quest, "q2", PriorityClasses.Urgent)])));
        Assert.Equal(["a2", "a1", "a3"], Order(asks, Of([Set(PriorityBook.Ask, "cafee7", PriorityClasses.High)])));
    }

    [Fact]
    public void An_ask_of_an_asker_the_look_does_not_know_is_normal()
    {
        Assert.Equal(["a3", "a1"], Order([Ask("a1", "gone", 30), Ask("a3", "s1", 10)], Of([Set(PriorityBook.Quest, "q1", PriorityClasses.High)])));
    }

    [Fact]
    public void An_ask_waiting_past_its_bound_is_ordered_as_high()
    {
        var asks = new[] { Ask("old", "s1", 150), Ask("new", "s2", 5) };

        Assert.Equal(["old", "new"], Order(asks, Of([Set(PriorityBook.Quest, "q2", PriorityClasses.High)])));
    }

    [Fact]
    public void A_held_askers_ask_is_held_and_the_ask_of_its_ask_is_held_too()
    {
        var hold = new HoldEvent(Now.AddMinutes(-2), PriorityBook.Quest, "q1", "games", true, "make room", "the person");
        var ask = new HoldEvent(Now.AddMinutes(-2), PriorityBook.Ask, "cafee7", "games", true, "make room", "the person");

        Assert.True(HelperOrder.Held(Ask("a1", "s1", 5), Of(holds: [hold])));
        Assert.False(HelperOrder.Held(Ask("a2", "s2", 5), Of(holds: [hold])));
        Assert.True(HelperOrder.Held(Ask("a2", "s2", 5), Of(holds: [ask])));
        Assert.False(HelperOrder.Held(Ask("a1", "s1", 5), Of(holds: [hold, hold with { At = Now, Held = false }])));
        Assert.False(HelperOrder.Held(Ask("a9", "gone", 5), Of(holds: [hold, ask])));
    }
}
