using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A line's queued landings form in the one order: the class of the run each belongs to, aged where it waited past its bound, then the
/// queue's own order (an <c>order first</c> or <c>last</c>, else arrival). A held run's landing stands aside and returns at its first arrival;
/// a candidate already formed is no queued item and is never cut.
/// </summary>
public sealed class LandingOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private static IntegrationRequest Request(string id, string? quest, int minutesAgo, long? order = null) =>
        new(id, "engine", "aurora", $"feature/{id}", "1111111111111111111111111111111111111111", IntegrationRoutes.Recorded, "main", IntegrationRequester.Card,
            new LandingIntegrate().Resolved(), Now.AddMinutes(-minutesAgo)) { Quest = quest, Order = order };

    private static PriorityEvent Set(string target, string id, string @class) =>
        new(Now.AddMinutes(-1), target, id, "games", @class, "it matters", "the person") { Until = @class == PriorityClasses.Urgent ? Now.AddHours(1) : null };

    private static PriorityLook Look(PriorityEvent[]? events = null, HoldEvent[]? holds = null) =>
        new(events ?? [], PriorityRules.Default, Now) { Holds = holds ?? [] };

    private static string[] Order(IReadOnlyList<IntegrationRequest> queued, PriorityLook look, Func<IntegrationRequest, string?>? askOf = null) =>
        [.. LandingOrder.Of(queued, look, askOf ?? (_ => null)).Select(each => each.Id)];

    [Fact]
    public void An_urgent_landing_forms_before_an_older_normal_one()
    {
        var queued = new[] { Request("old", "q1", 30), Request("mid", "q2", 20), Request("fix", "q3", 5) };

        Assert.Equal(["fix", "old", "mid"], Order(queued, Look([Set(PriorityBook.Quest, "q3", PriorityClasses.Urgent)])));
    }

    [Fact]
    public void Classes_come_in_their_order_and_one_class_keeps_the_queue_s_own_order_so_an_order_first_still_leads_it()
    {
        var queued = new[] { Request("low", "q1", 30), Request("a", "q2", 25), Request("b", "q3", 20, order: -1), Request("hi", "q4", 1) };
        var events = new[] { Set(PriorityBook.Quest, "q1", PriorityClasses.Low), Set(PriorityBook.Quest, "q4", PriorityClasses.High) };

        Assert.Equal(["hi", "b", "a", "low"], Order([queued[3], queued[2], queued[1], queued[0]], Look(events)));
    }

    [Fact]
    public void A_landing_reads_its_ask_s_class_and_a_chain_s_later_quest_s_class_through_its_run()
    {
        var queued = new[] { Request("one", "q1", 30), Request("two", "q2", 20), Request("three", "q3", 10) };
        var ask = (IntegrationRequest request) => request.Quest == "q2" ? "a9" : null;
        var events = new[] { Set(PriorityBook.Ask, "a9", PriorityClasses.High), Set(PriorityBook.Quest, "q3", PriorityClasses.Urgent) with { Run = "q3" } };

        Assert.Equal(["three", "two", "one"], Order(queued, Look(events), ask));
    }

    [Fact]
    public void A_normal_landing_past_its_bound_is_ordered_as_high_and_keeps_its_place_among_them()
    {
        var queued = new[] { Request("aged", "q2", 150), Request("high", "q1", 10), Request("fresh", "q3", 5) };

        Assert.Equal(["aged", "high", "fresh"], Order(queued, Look([Set(PriorityBook.Quest, "q1", PriorityClasses.High)])));
        Assert.Equal(["high", "aged", "fresh"], Order([queued[1], queued[0], queued[2]], Look([Set(PriorityBook.Quest, "q1", PriorityClasses.High)])));
    }

    [Fact]
    public void A_held_runs_landing_stands_aside_and_returns_at_its_first_arrival_when_lifted()
    {
        var queued = new[] { Request("first", "q1", 30), Request("second", "q2", 20), Request("third", "q3", 10) };
        var hold = new HoldEvent(Now.AddMinutes(-2), PriorityBook.Quest, "q1", "games", true, "make room", "the person");
        var lift = hold with { At = Now.AddMinutes(-1), Held = false };

        Assert.Equal(["second", "third"], Order(queued, Look(holds: [hold])));
        Assert.Equal(["first", "second", "third"], Order(queued, Look(holds: [hold, lift])));
    }

    [Fact]
    public void A_landing_of_no_quest_is_normal_and_is_never_held()
    {
        var queued = new[] { Request("records", null, 30), Request("work", "q1", 20) };

        Assert.Equal(["work", "records"], Order(queued, Look([Set(PriorityBook.Quest, "q1", PriorityClasses.High)])));
        Assert.Equal(["records"], Order(queued, Look(holds: [new HoldEvent(Now, PriorityBook.Quest, "q1", "games", true, "x", "the person")])));
    }
}
